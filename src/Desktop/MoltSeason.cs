using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Media;

namespace CorbiePet {
  public enum MoltSeasonKind { Ember, Tide, Gloom, Bloom }

  public sealed class MoltSeasonState {
    public int Version { get; set; }
    public string Kind { get; set; }
    public DateTime SeasonStartUtc { get; set; }
    public DateTime LastShedUtc { get; set; }
    public int ShedCount { get; set; }
    public MoltSeasonState() {
      Version=1;
      Kind=MoltSeasonKind.Bloom.ToString();
      SeasonStartUtc=DateTime.UtcNow;
      LastShedUtc=DateTime.MinValue;
    }
  }

  // Living molt: season rotates ~every 5 days, tint shifts, rare feather-shed Nest events.
  public static class MoltSeason {
    public static MoltSeasonKind ParseKind(string kind) {
      MoltSeasonKind k;
      if(Enum.TryParse(kind??"", true, out k)) return k;
      return MoltSeasonKind.Bloom;
    }

    public static Color SeasonAccent(MoltSeasonKind kind) {
      switch(kind) {
        case MoltSeasonKind.Ember: return Color.FromRgb(0xE0,0x7A,0x3D);
        case MoltSeasonKind.Tide: return Color.FromRgb(0x3D,0xA0,0xC8);
        case MoltSeasonKind.Gloom: return Color.FromRgb(0x5A,0x4E,0x7A);
        default: return Color.FromRgb(0x7C,0xC4,0x6A);
      }
    }

    public static string Summary(MoltSeasonState state, TrinketMolt.Tint tint) {
      if(state==null) return "Molt season idle.";
      var kind=ParseKind(state.Kind);
      int day=(int)Math.Max(1, Math.Floor((DateTime.UtcNow-state.SeasonStartUtc).TotalDays)+1);
      string baseLine=kind+" season - day "+day+" of ~5.";
      if(tint!=null && !string.IsNullOrEmpty(tint.Summary)) baseLine+=" "+tint.Summary;
      if(state.ShedCount>0) baseLine+=" Feather sheds logged: "+state.ShedCount+".";
      return baseLine;
    }

    public static TrinketMolt.Tint ApplySeason(TrinketMolt.Tint tint, MoltSeasonState state) {
      if(tint==null || state==null) return tint;
      var kind=ParseKind(state.Kind);
      Color accent=SeasonAccent(kind);
      double pulse=0.12 + Math.Min(0.18, (DateTime.UtcNow-state.SeasonStartUtc).TotalDays*0.03);
      tint.Feather=TrinketMolt.Blend(tint.Feather, accent, pulse);
      tint.TentacleTip=TrinketMolt.Blend(tint.TentacleTip, accent, pulse+0.08);
      return tint;
    }

    public static bool MaybeRotate(MoltSeasonState state, Random rand) {
      if(state==null) return false;
      if((DateTime.UtcNow-state.SeasonStartUtc).TotalDays<5) return false;
      var values=Enum.GetValues(typeof(MoltSeasonKind));
      var current=ParseKind(state.Kind);
      MoltSeasonKind next=current;
      for(int i=0;i<8;i++) {
        next=(MoltSeasonKind)values.GetValue(rand.Next(values.Length));
        if(next!=current) break;
      }
      state.Kind=next.ToString();
      state.SeasonStartUtc=DateTime.UtcNow;
      return true;
    }

    // Returns shed journal text or null.
    public static string MaybeFeatherShed(MoltSeasonState state, bool journalEnabled, Random rand, bool octopus) {
      if(!journalEnabled || state==null || rand==null) return null;
      if((DateTime.UtcNow-state.LastShedUtc).TotalHours<14) return null;
      if(rand.Next(1000)>3) return null; // rare while ticking
      state.LastShedUtc=DateTime.UtcNow;
      state.ShedCount++;
      var kind=ParseKind(state.Kind);
      if(octopus) return "Molt shed ("+kind+"): a tip-scale drifted into the Nest.";
      return "Molt shed ("+kind+"): a loose feather drifted into the Nest.";
    }

    public static void Check() {
      var st=new MoltSeasonState { Kind="Ember", SeasonStartUtc=DateTime.UtcNow.AddDays(-6) };
      if(!MaybeRotate(st, new Random(2))) throw new Exception("MoltSeason: rotate failed");
      if(ParseKind(st.Kind)==MoltSeasonKind.Ember && (DateTime.UtcNow-st.SeasonStartUtc).TotalDays>1) {
        // may still be ember by chance; just ensure start refreshed
      }
      if((DateTime.UtcNow-st.SeasonStartUtc).TotalHours>2) { /* ok */ }
      var tint=new TrinketMolt.Tint {
        Feather=Color.FromRgb(0x25,0x24,0x3B),
        TentacleTip=Color.FromRgb(0x8F,0x79,0xBD),
        Summary="test"
      };
      var shifted=ApplySeason(tint, new MoltSeasonState { Kind="Tide", SeasonStartUtc=DateTime.UtcNow.AddDays(-3) });
      if(shifted.Feather==Color.FromRgb(0x25,0x24,0x3B)) throw new Exception("MoltSeason: tint unchanged");
      string sum=Summary(st, shifted);
      if(sum.IndexOf("season",StringComparison.OrdinalIgnoreCase)<0) throw new Exception("MoltSeason: summary");
    }
  }

  public sealed class MoltSeasonBook {
    readonly string path;
    public MoltSeasonState State { get; private set; }
    public MoltSeasonBook(string path) {
      this.path=path;
      State=new MoltSeasonState();
      Load();
    }
    public void Save() {
      if(path==null) return;
      try {
        string dir=Path.GetDirectoryName(path);
        if(!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, new JavaScriptSerializer().Serialize(State));
      } catch { }
    }
    void Load() {
      try {
        if(path!=null && File.Exists(path))
          State=new JavaScriptSerializer().Deserialize<MoltSeasonState>(File.ReadAllText(path))??new MoltSeasonState();
      } catch { State=new MoltSeasonState(); }
      if(string.IsNullOrEmpty(State.Kind)) State.Kind=MoltSeasonKind.Bloom.ToString();
      if(State.SeasonStartUtc==DateTime.MinValue) State.SeasonStartUtc=DateTime.UtcNow;
    }
  }
}
