using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows;

namespace CorbiePet {
  public sealed class TerritoryCell {
    public int Screen { get; set; }
    public int Corner { get; set; } // 0 NW, 1 NE, 2 SW, 3 SE
    public double Seconds { get; set; }
    public string Label { get; set; }
  }

  public sealed class TerritoryState {
    public int Version { get; set; }
    public List<TerritoryCell> Cells { get; set; }
    public TerritoryState() {
      Version=1;
      Cells=new List<TerritoryCell>();
    }
  }

  // Claims desktop corners as roosts from dwell time. No window-text scraping.
  public sealed class TerritoryMap {
    readonly string path;
    TerritoryState state;
    string currentKey;
    DateTime lastShedUtc=DateTime.MinValue;

    public TerritoryMap(string path) {
      this.path=path;
      state=new TerritoryState();
      Load();
    }

    public TerritoryState State { get { return state; } }
    public TerritoryCell Favorite {
      get {
        if(state.Cells==null || state.Cells.Count==0) return null;
        return state.Cells.OrderByDescending(c=>c.Seconds).FirstOrDefault(c=>c.Seconds>=45);
      }
    }

    public string Tick(double dt, double left, double top, double width, double height, bool enabled, bool journalEnabled, RoostMemory roost, Random rand) {
      if(!enabled || dt<=0) { currentKey=null; return null; }
      int screen, corner;
      if(!Locate(left+width*0.5, top+height*0.5, out screen, out corner)) { currentKey=null; return null; }
      string key=screen+":"+corner;
      if(currentKey!=key) currentKey=key;
      var cell=Ensure(screen, corner);
      double before=cell.Seconds;
      cell.Seconds+=dt;
      if(cell.Seconds>60*60*8) cell.Seconds=60*60*8;
      string newlyClaimed=null;
      if(before<45 && cell.Seconds>=45) {
        newlyClaimed=cell.Label;
        if(journalEnabled && roost!=null)
          roost.Remember("territory","Claimed roost: "+cell.Label,DateTime.UtcNow);
      }
      // Rare soft claim remark into journal
      if(journalEnabled && roost!=null && cell.Seconds>90 && (DateTime.UtcNow-lastShedUtc).TotalHours>=6 && rand!=null && rand.Next(1000)==0) {
        lastShedUtc=DateTime.UtcNow;
        roost.Remember("territory","Claimed roost marked: "+cell.Label,DateTime.UtcNow);
      }
      return newlyClaimed;
    }

    public bool ContainsPoint(double x, double y, out TerritoryCell cell) {
      cell=null;
      int screen, corner;
      if(!Locate(x,y,out screen,out corner)) return false;
      cell=state.Cells.FirstOrDefault(c=>c.Screen==screen && c.Corner==corner);
      return cell!=null && cell.Seconds>=45;
    }

    public bool TryPreferredHome(double petW, double petH, out double left, out double top) {
      left=top=0;
      var fav=Favorite;
      if(fav==null) return false;
      Rect r=CornerRect(fav.Screen, fav.Corner);
      if(r.Width<=0 || r.Height<=0) return false;
      // Sit inset toward the corner's interior
      if(fav.Corner==0 || fav.Corner==2) left=r.Left+18;
      else left=r.Right-petW-18;
      if(fav.Corner==0 || fav.Corner==1) top=r.Top+18;
      else top=r.Bottom-petH-18;
      return true;
    }

    public string AsciiMap() {
      var screens=System.Windows.Forms.Screen.AllScreens;
      if(screens==null || screens.Length==0) return "No screens detected.";
      var lines=new List<string>();
      lines.Add("Territory (dwell seconds). * = claimed roost.");
      for(int i=0;i<screens.Length;i++) {
        lines.Add("Monitor "+(i+1)+":");
        string nw=Mark(i,0), ne=Mark(i,1), sw=Mark(i,2), se=Mark(i,3);
        lines.Add("  "+Pad(nw)+" | "+Pad(ne));
        lines.Add("  ---------+---------");
        lines.Add("  "+Pad(sw)+" | "+Pad(se));
      }
      var fav=Favorite;
      if(fav!=null) lines.Add("Home roost: "+fav.Label+" (~"+(int)fav.Seconds+"s)");
      else lines.Add("Home roost: still scouting (need ~45s dwell).");
      return string.Join("\n", lines);
    }

    public string StatusLine() {
      var fav=Favorite;
      if(fav==null) return "Territory: scouting";
      return "Territory home: "+fav.Label;
    }

    public bool SoftDefendChance(double shimejiX, double shimejiY, Random rand) {
      TerritoryCell cell;
      if(!ContainsPoint(shimejiX, shimejiY, out cell)) return false;
      if(rand==null) return true;
      return rand.Next(100)<55;
    }

    public void Save() {
      if(path==null) return;
      try {
        string dir=Path.GetDirectoryName(path);
        if(!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, new JavaScriptSerializer().Serialize(state));
      } catch { }
    }

    void Load() {
      try {
        if(path!=null && File.Exists(path)) {
          state=new JavaScriptSerializer().Deserialize<TerritoryState>(File.ReadAllText(path))??new TerritoryState();
        }
      } catch { state=new TerritoryState(); }
      if(state.Cells==null) state.Cells=new List<TerritoryCell>();
    }

    TerritoryCell Ensure(int screen, int corner) {
      var cell=state.Cells.FirstOrDefault(c=>c.Screen==screen && c.Corner==corner);
      if(cell==null) {
        cell=new TerritoryCell { Screen=screen, Corner=corner, Seconds=0, Label=LabelFor(screen,corner) };
        state.Cells.Add(cell);
      }
      if(string.IsNullOrEmpty(cell.Label)) cell.Label=LabelFor(screen,corner);
      return cell;
    }

    static string LabelFor(int screen, int corner) {
      string c=corner==0?"NW":corner==1?"NE":corner==2?"SW":"SE";
      return "M"+(screen+1)+"-"+c;
    }

    string Mark(int screen, int corner) {
      var cell=state.Cells.FirstOrDefault(c=>c.Screen==screen && c.Corner==corner);
      int sec=cell==null?0:(int)cell.Seconds;
      string star=sec>=45?"*":" ";
      return star+sec.ToString().PadLeft(4,' ')+"s";
    }

    static string Pad(string s) { return (s??"").PadRight(8); }

    static bool Locate(double x, double y, out int screen, out int corner) {
      screen=0; corner=0;
      var screens=System.Windows.Forms.Screen.AllScreens;
      if(screens==null || screens.Length==0) return false;
      for(int i=0;i<screens.Length;i++) {
        var wa=screens[i].WorkingArea;
        if(x>=wa.Left && x<wa.Right && y>=wa.Top && y<wa.Bottom) {
          screen=i;
          double mx=(wa.Left+wa.Right)*0.5;
          double my=(wa.Top+wa.Bottom)*0.5;
          bool east=x>=mx; bool south=y>=my;
          if(!east && !south) corner=0;
          else if(east && !south) corner=1;
          else if(!east && south) corner=2;
          else corner=3;
          return true;
        }
      }
      return false;
    }

    static Rect CornerRect(int screen, int corner) {
      var screens=System.Windows.Forms.Screen.AllScreens;
      if(screens==null || screen<0 || screen>=screens.Length) return Rect.Empty;
      var wa=screens[screen].WorkingArea;
      double hw=wa.Width*0.5, hh=wa.Height*0.5;
      if(corner==0) return new Rect(wa.Left, wa.Top, hw, hh);
      if(corner==1) return new Rect(wa.Left+hw, wa.Top, hw, hh);
      if(corner==2) return new Rect(wa.Left, wa.Top+hh, hw, hh);
      return new Rect(wa.Left+hw, wa.Top+hh, hw, hh);
    }

    public static void Check() {
      var map=new TerritoryMap(null);
      map.Tick(50, 100, 100, 40, 40, true, false, null, new Random(1)); // seed dwell
      if(map.State.Cells.Count<1) throw new Exception("Territory: tick failed");
      string ascii=map.AsciiMap();
      if(ascii.IndexOf("Monitor",StringComparison.Ordinal)<0) throw new Exception("Territory: map blank");
      map.Tick(50, 100, 100, 40, 40, false, false, null, new Random(1));
    }
  }
}
