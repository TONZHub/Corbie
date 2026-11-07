using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace CorbiePet {
  public sealed class ChronicleState {
    public int Version { get; set; }
    public DateTime LastSpokenUtc { get; set; }
    public List<string> Lines { get; set; }
    public string PinnedHighlight { get; set; }
    public ChronicleState() {
      Version=1;
      Lines=new List<string>();
      LastSpokenUtc=DateTime.MinValue;
    }
  }

  // Builds a Nest flight chronicle from real journal/mission/steam signals.
  public static class FlightChronicle {
    public static List<string> Build(IEnumerable<RoostEntry> entries, MissionBoard mission, TerritoryMap territory, MoltSeasonState molt, DateTime now, string chronicleFlavor=null) {
      var lines=new List<string>();
      var list=(entries??Enumerable.Empty<RoostEntry>()).Where(e=>e!=null && e.Utc>=now.AddDays(-7)).OrderBy(e=>e.Utc).ToList();
      int steam=list.Count(e=>string.Equals(e.Kind,"steam",StringComparison.OrdinalIgnoreCase));
      int launch=list.Count(e=>string.Equals(e.Kind,"launch",StringComparison.OrdinalIgnoreCase));
      int focus=list.Count(e=>string.Equals(e.Kind,"focus",StringComparison.OrdinalIgnoreCase));
      int missionN=list.Count(e=>string.Equals(e.Kind,"mission",StringComparison.OrdinalIgnoreCase));
      int ship=list.Count(e=>string.Equals(e.Kind,"ship",StringComparison.OrdinalIgnoreCase));
      int trinket=list.Count(e=>string.Equals(e.Kind,"trinket",StringComparison.OrdinalIgnoreCase));
      int moltShed=list.Count(e=>string.Equals(e.Kind,"molt",StringComparison.OrdinalIgnoreCase));
      int territoryN=list.Count(e=>string.Equals(e.Kind,"territory",StringComparison.OrdinalIgnoreCase));

      lines.Add("Flight chronicle - last 7 days ("+now.ToLocalTime().ToString("ddd MMM d")+")");
      if(!string.IsNullOrEmpty(chronicleFlavor)) lines.Add("Ink: "+RoostMemory.Short(chronicleFlavor,120));
      lines.Add("Signals: steam "+steam+", launches "+launch+", focus "+focus+", missions "+missionN+", ships "+ship+", shinies "+trinket+".");
      if(moltShed>0) lines.Add("Molt sheds noted: "+moltShed+".");
      if(territoryN>0) lines.Add("Territory claims: "+territoryN+".");
      if(territory!=null) lines.Add(territory.StatusLine());
      if(molt!=null) lines.Add(MoltSeason.Summary(molt, null));
      if(mission!=null && mission.Active)
        lines.Add("Active quest: "+RoostMemory.Short(mission.Title,80)+(mission.HasNextStep?(" / next: "+RoostMemory.Short(mission.NextStep,60)):"."));
      else lines.Add("Active quest: none.");

      var highlights=list.Where(e=>e.Kind=="steam"||e.Kind=="ship"||e.Kind=="mission"||e.Kind=="molt"||e.Kind=="territory")
        .Reverse().Take(8).Reverse();
      foreach(var e in highlights) {
        lines.Add(e.Utc.ToLocalTime().ToString("ddd HH:mm")+" - "+RoostMemory.Short(e.Text,140));
      }
      if(list.Count==0) lines.Add("The Nest was quiet. Corbie still has opinions.");
      return lines;
    }

    public static string PickPin(IEnumerable<string> lines) {
      var arr=(lines??Enumerable.Empty<string>()).Where(x=>!string.IsNullOrWhiteSpace(x)).ToList();
      // Prefer a dated highlight line over headers
      for(int i=arr.Count-1;i>=0;i--) {
        string t=arr[i];
        if(t.IndexOf(" - ",StringComparison.Ordinal)>=0 && t.Length>12) return RoostMemory.Short(t,160);
      }
      return arr.Count>0?RoostMemory.Short(arr[0],160):null;
    }

    public static string Speak(IEnumerable<string> lines, bool octopus) {
      var arr=(lines??Enumerable.Empty<string>()).Where(x=>!string.IsNullOrWhiteSpace(x)).Take(6).ToList();
      if(arr.Count==0) return octopus?"Chronicle empty. Even the arms are bored.":"Chronicle empty. The Nest forgot to gossip.";
      // Prefer the summary-ish early lines for speech
      string head=arr[0];
      string second=arr.Count>1?arr[1]:"";
      string third=arr.Count>2?arr[2]:"";
      if(octopus) return RoostMemory.Short("Roost report. "+head+" "+second+" "+third,280);
      return RoostMemory.Short("Roost report. "+head+" "+second+" "+third,280);
    }

    public static bool DueForWeekly(ChronicleState state, DateTime now) {
      if(state==null) return true;
      if(state.LastSpokenUtc==DateTime.MinValue) return true;
      return (now-state.LastSpokenUtc).TotalDays>=7;
    }

    public static void Check() {
      var entries=new List<RoostEntry> {
        new RoostEntry { Utc=DateTime.UtcNow.AddDays(-1), Kind="steam", Text="Playing on Steam: Fixture Quest" },
        new RoostEntry { Utc=DateTime.UtcNow.AddHours(-3), Kind="focus", Text="Finished a focus sprint: Outline" },
        new RoostEntry { Utc=DateTime.UtcNow.AddHours(-2), Kind="ship", Text="Ship ritual celebrated for v0.43" }
      };
      var lines=Build(entries, null, null, new MoltSeasonState(), DateTime.UtcNow);
      if(lines.Count<3) throw new Exception("Chronicle: too thin");
      if(lines[0].IndexOf("Flight chronicle",StringComparison.Ordinal)<0) throw new Exception("Chronicle: header");
      string speech=Speak(lines, false);
      if(speech.IndexOf("Roost report",StringComparison.Ordinal)<0) throw new Exception("Chronicle: speech");
      string pin=PickPin(lines);
      if(string.IsNullOrEmpty(pin)) throw new Exception("Chronicle: pin");
      var st=new ChronicleState();
      if(!DueForWeekly(st, DateTime.UtcNow)) throw new Exception("Chronicle: due");
      st.LastSpokenUtc=DateTime.UtcNow;
      if(DueForWeekly(st, DateTime.UtcNow.AddDays(1))) throw new Exception("Chronicle: not due yet");
    }
  }

  public sealed class ChronicleBook {
    readonly string path;
    public ChronicleState State { get; private set; }
    public ChronicleBook(string path) {
      this.path=path;
      State=new ChronicleState();
      Load();
    }
    public void ReplaceLines(List<string> lines) {
      State.Lines=lines??new List<string>();
      Save();
    }
    public void MarkSpoken(DateTime utc) {
      State.LastSpokenUtc=utc;
      Save();
    }
    public void Pin(string highlight) {
      State.PinnedHighlight=RoostMemory.Short(highlight,160);
      Save();
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
          State=new JavaScriptSerializer().Deserialize<ChronicleState>(File.ReadAllText(path))??new ChronicleState();
      } catch { State=new ChronicleState(); }
      if(State.Lines==null) State.Lines=new List<string>();
    }
  }
}
