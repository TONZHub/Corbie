using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;

namespace CorbiePet {
  public sealed class RoostEntry {
    public DateTime Utc { get; set; }
    public string Kind { get; set; }
    public string Text { get; set; }
    public override string ToString() { return Utc.ToLocalTime().ToString("ddd HH:mm")+" · "+Text; }
  }
  public sealed class ForageDecision {
    public string Key { get; set; }
    public DateTime UntilUtc { get; set; }
  }
  public sealed class RoostData {
    public int PreferencesVersion { get; set; }
    public bool JournalEnabled { get; set; }
    public bool ContextEnabled { get; set; }
    public bool CursorPlay { get; set; }
    public bool AttentionBids { get; set; }
    public bool ForageEnabled { get; set; }
    public bool ClipboardEnabled { get; set; }
    public bool FoleyEnabled { get; set; }
    public bool SkyRoostEnabled { get; set; }
    public bool PesterBossEnabled { get; set; }
    public bool ShimejiEnabled { get; set; }
    public bool WakePhraseEnabled { get; set; }
    public bool MischiefEnabled { get; set; }
    public bool PerchPersonalityEnabled { get; set; }
    public bool LaunchReactEnabled { get; set; }
    public bool GameSessionBuddyEnabled { get; set; }
    public bool TerritoryEnabled { get; set; }
    public bool MoltSeasonEnabled { get; set; }
    public bool ChronicleEnabled { get; set; }
    public bool AmbientVisionEnabled { get; set; }
    public bool VoiceEnabled { get; set; }
    public bool TaskbarButton { get; set; }
    public List<RoostEntry> Entries { get; set; }
    public List<ForageDecision> Decisions { get; set; }
    public List<TrinketItem> Trinkets { get; set; }
    public string FocusTitle { get; set; }
    public double FocusRemaining { get; set; }
    public RoostData() {
      PreferencesVersion=5;
      JournalEnabled=ContextEnabled=CursorPlay=ClipboardEnabled=FoleyEnabled=SkyRoostEnabled=PesterBossEnabled=true;
      ShimejiEnabled=true;
      WakePhraseEnabled=true;
      MischiefEnabled=true;
      PerchPersonalityEnabled=true;
      LaunchReactEnabled=true;
      GameSessionBuddyEnabled=true;
      TerritoryEnabled=true;
      MoltSeasonEnabled=true;
      ChronicleEnabled=true;
      AmbientVisionEnabled=false;
      TaskbarButton=true;
      AttentionBids=false;
      VoiceEnabled=PiperVoice.Configured;
      Entries=new List<RoostEntry>(); Decisions=new List<ForageDecision>(); Trinkets=new List<TrinketItem>();
    }
  }
  // All mutations run on the UI thread; workers only receive copied decisions.
  public sealed class RoostMemory {
    readonly string path;
    public RoostData Data { get; private set; }
    public string Error { get; private set; }
    public RoostMemory(string path) {
      this.path=path; Data=new RoostData();
      try {
        if(path!=null && File.Exists(path)) {
          if(new FileInfo(path).Length>1024*1024) throw new IOException("Journal is too large.");
          Data=new JavaScriptSerializer().Deserialize<RoostData>(File.ReadAllText(path))??new RoostData();
        }
      } catch(Exception) { Error="The saved Roost journal could not be read. A fresh journal is in use."; }
      // v0.36 makes the long-running pet discoverable in the taskbar. Existing
      // users get the button once and can turn it off again from Behavior.
      if(Data.PreferencesVersion<2) { Data.TaskbarButton=true; }
      if(Data.PreferencesVersion<3) { Data.ShimejiEnabled=true; }
      if(Data.PreferencesVersion<4) {
        Data.WakePhraseEnabled=true;
        Data.MischiefEnabled=true;
        Data.PerchPersonalityEnabled=true;
      }
      if(Data.PreferencesVersion<5) {
        // Keep ambient off by default for existing installs; menu choice now persists once set.
      }
      if(Data.PreferencesVersion<6) {
        Data.LaunchReactEnabled=true;
      }
      if(Data.PreferencesVersion<7) {
        Data.GameSessionBuddyEnabled=true;
      }
      if(Data.PreferencesVersion<8) {
        Data.TerritoryEnabled=true;
        Data.MoltSeasonEnabled=true;
        Data.ChronicleEnabled=true;
      }
      Data.PreferencesVersion=8;
      if(Data.Entries==null) Data.Entries=new List<RoostEntry>();
      if(Data.Decisions==null) Data.Decisions=new List<ForageDecision>();
      if(Data.Trinkets==null) Data.Trinkets=new List<TrinketItem>();
      Prune(DateTime.UtcNow);
    }
    public static string Short(string text,int limit) {
      text=(text??"").Replace('\r',' ').Replace('\n',' ').Trim();
      return text.Length>limit?text.Substring(0,limit):text;
    }
    public void Prune(DateTime now) {
      Data.Entries.RemoveAll(x=>x==null || x.Utc<now.AddDays(-7) || x.Utc>now.AddMinutes(1));
      Data.Entries=Data.Entries.OrderBy(x=>x.Utc).Skip(Math.Max(0,Data.Entries.Count-300)).ToList();
      Data.Decisions.RemoveAll(x=>x==null || x.UntilUtc<=now);
      if(Data.Decisions.Count>1000) Data.Decisions.RemoveRange(0,Data.Decisions.Count-1000);
    }
    public void Remember(string kind,string text,DateTime now) {
      Prune(now); if(!Data.JournalEnabled) return;
      text=Short(text,220);
      if(Data.Entries.Any(x=>x.Kind==kind && x.Text==text && (now-x.Utc).TotalMinutes<30)) return;
      Data.Entries.Add(new RoostEntry { Utc=now,Kind=kind,Text=text }); Save();
    }
    public void Decide(ForageItem item,bool keep) {
      Data.Decisions.RemoveAll(x=>x.Key==item.Key);
      Data.Decisions.Add(new ForageDecision { Key=item.Key,UntilUtc=keep?DateTime.MaxValue:DateTime.UtcNow.AddDays(1) }); Save();
    }
    public TrinketItem ScavengeTrinket(Random rand) {
      if(Data.Trinkets==null) Data.Trinkets=new List<TrinketItem>();
      var item=TrinketCatalog.RollTrinket(Data.Trinkets.Select(x=>x.Id),rand);
      if(item!=null) {
        Data.Trinkets.Insert(0,item);
        if(Data.Trinkets.Count>50) Data.Trinkets.RemoveRange(50,Data.Trinkets.Count-50);
        Remember("trinket","Scavenged a shiny trinket: "+item.Name+" ("+item.Rarity+")",DateTime.UtcNow);
        Save();
      }
      return item;
    }
    public TrinketItem ScavengePlayTrinket(Random rand, string biasId) {
      if(Data.Trinkets==null) Data.Trinkets=new List<TrinketItem>();
      var item=TrinketCatalog.RollPlayTrinket(Data.Trinkets.Select(x=>x.Id),rand,biasId);
      if(item!=null) {
        Data.Trinkets.Insert(0,item);
        if(Data.Trinkets.Count>50) Data.Trinkets.RemoveRange(50,Data.Trinkets.Count-50);
        Remember("trinket","Game-night shiny: "+item.Name+" ("+item.Rarity+")",DateTime.UtcNow);
        Save();
      }
      return item;
    }
    public void Clear() { Data.Entries.Clear(); Save(); }
    public void Save() {
      if(path==null) return;
      try {
        Prune(DateTime.UtcNow); Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp=path+".tmp"; File.WriteAllText(temp,new JavaScriptSerializer().Serialize(Data));
        if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
        Error=null;
      } catch(Exception) { Error="Corbie could not save Roost settings. Check that his local data folder is writable."; }
    }
  }
  public sealed class ForageItem {
    public string Path,Name,Reason,Key;
    public long Bytes;
    public DateTime ModifiedUtc;
    public override string ToString() { return Name+" · "+RecycleBinHoard.FormatBytes(Bytes)+" · "+Reason; }
    public bool Unchanged() {
      try { var f=new FileInfo(Path); return f.Exists && (f.Attributes&FileAttributes.ReparsePoint)==0 && f.Length==Bytes && f.LastWriteTimeUtc==ModifiedUtc; }
      catch { return false; }
    }
  }
  public sealed class ForageResult {
    public List<ForageItem> Items=new List<ForageItem>();
    public bool Limited;
  }
  public static class DownloadsForager {
    [DllImport("shell32.dll")] static extern int SHGetKnownFolderPath(ref Guid id,uint flags,IntPtr token,out IntPtr path);
    public static string Folder() {
      var id=new Guid("374DE290-123F-4565-9164-39C4925E467B"); IntPtr value;
      int hr=SHGetKnownFolderPath(ref id,0,IntPtr.Zero,out value);
      if(hr!=0) Marshal.ThrowExceptionForHR(hr);
      try { return Marshal.PtrToStringUni(value); } finally { Marshal.FreeCoTaskMem(value); }
    }
    public static ForageResult Scan(string folder,DateTime now,IEnumerable<ForageDecision> decisions,CancellationToken token) {
      var hidden=new HashSet<string>(decisions.Where(x=>x.UntilUtc>now).Select(x=>x.Key));
      var result=new ForageResult(); var files=new List<ForageItem>(); int count=0;
      foreach(string path in Directory.EnumerateFiles(folder)) {
        token.ThrowIfCancellationRequested(); if(++count>1000) { result.Limited=true; break; }
        try {
          var f=new FileInfo(path);
          if((f.Attributes&(FileAttributes.ReparsePoint|FileAttributes.Hidden|FileAttributes.System))!=0) continue;
          files.Add(new ForageItem { Path=f.FullName,Name=f.Name,Bytes=f.Length,ModifiedUtc=f.LastWriteTimeUtc,
            Key=f.FullName.ToUpperInvariant()+"|"+f.Length+"|"+f.LastWriteTimeUtc.Ticks });
        } catch(IOException) { } catch(UnauthorizedAccessException) { }
      }
      foreach(var f in files) {
        token.ThrowIfCancellationRequested(); double days=(now-f.ModifiedUtc).TotalDays;
        string ext=System.IO.Path.GetExtension(f.Name).ToLowerInvariant();
        if(ext==".crdownload" || ext==".part" || ext==".tmp" || days<1 || hidden.Contains(f.Key)) continue;
        string baseName=System.Text.RegularExpressions.Regex.Replace(System.IO.Path.GetFileNameWithoutExtension(f.Name),@" \(\d+\)$","")+ext;
        bool copy=!string.Equals(baseName,f.Name,StringComparison.OrdinalIgnoreCase) && files.Any(x=>x!=f && x.Bytes==f.Bytes && string.Equals(x.Name,baseName,StringComparison.OrdinalIgnoreCase));
        if(copy) f.Reason="possible duplicate (name and size only)";
        else if(days>=14 && (ext==".exe" || ext==".msi" || ext==".msix")) f.Reason="installer unchanged for "+(int)days+" days";
        else if(days>=30) f.Reason="unchanged for "+(int)days+" days";
        if(f.Reason!=null) result.Items.Add(f);
      }
      result.Items=result.Items.OrderByDescending(x=>x.Bytes).Take(40).ToList(); return result;
    }
  }
  public sealed class FocusSprint {
    public string Title { get; private set; }
    public double Remaining { get; private set; }
    public bool Paused { get; set; }
    public bool Active { get { return Remaining>0 && !string.IsNullOrEmpty(Title); } }
    public bool CompletionPending { get; private set; }
    public void Start(string title,int minutes) {
      if(string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Give this sprint one small task first.");
      Title=RoostMemory.Short(title,180); Remaining=Math.Max(5,Math.Min(60,minutes))*60; Paused=false; CompletionPending=false;
    }
    public void Restore(string title,double seconds) {
      if(string.IsNullOrWhiteSpace(title) || double.IsNaN(seconds) || seconds<=0) return;
      Title=RoostMemory.Short(title,180); Remaining=Math.Min(3600,seconds); Paused=true;
    }
    public void Step(double seconds,bool canWork) {
      if(!Active || Paused || !canWork) return;
      Remaining=Math.Max(0,Remaining-Math.Max(0,Math.Min(1,seconds)));
      if(Remaining==0) CompletionPending=true;
    }
    public void Cancel() { Remaining=0; Title=null; CompletionPending=false; }
    public void Acknowledge() { CompletionPending=false; }
    public string Status(bool held) {
      if(CompletionPending) return "Sprint complete · ready for a break";
      if(!Active) return "Choose one small task. Finishing a sprint does not mark schoolwork submitted.";
      return (Paused?"Paused":held?"Resting while you are away or on a break":"Focusing")+" · "+TimeSpan.FromSeconds(Math.Ceiling(Remaining)).ToString(@"mm\:ss")+" · "+Title;
    }
  }
  public sealed class CursorGame {
    double still,cooldown=8,pursuit;
    double previousX=double.NaN,previousY;
    public bool Chasing { get { return pursuit>0; } }
    public bool Step(double dt,double x,double y,bool nearby,bool allowed) {
      double moved=double.IsNaN(previousX)?100:Math.Abs(x-previousX)+Math.Abs(y-previousY);
      previousX=x; previousY=y; cooldown=Math.Max(0,cooldown-dt);
      if(!allowed || !nearby) { still=0; pursuit=0; return false; }
      if(moved>30) { pursuit=0; still=0; cooldown=Math.Max(3,cooldown); return false; }
      still=moved<3?still+dt:0;
      if(pursuit>0) { pursuit-=dt; if(pursuit<=0) { cooldown=18; still=0; return true; } }
      else if(cooldown<=0 && still>1.5) { pursuit=0.8; still=0; }
      return false;
    }
  }
  public sealed class AttentionBid {
    Action pending; double left;
    public bool Active { get { return pending!=null; } }
    public void Offer(Action action) { if(!Active) { pending=action; left=7; } }
    public void Step(double dt,bool allowed) { if(!allowed || (Active && (left-=dt)<=0)) Cancel(); }
    public void Cancel() { pending=null; left=0; }
    public bool Accept() { var action=pending; Cancel(); if(action==null) return false; action(); return true; }
  }
  public static class SensoryContext {
    public static string RecentScreen(string text,DateTime captured,DateTime now) {
      var age=now-captured;
      if(string.IsNullOrWhiteSpace(text) || age.TotalSeconds<0 || age.TotalMinutes>2) return "No recent screen observation. Ask for a screen look if needed.";
      return "Fallible summary from a screen look "+(int)age.TotalSeconds+" seconds ago (not a live view): "+RoostMemory.Short(text,650);
    }
  }
}
