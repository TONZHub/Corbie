using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace CorbiePet {
  public sealed class MissionNote {
    public DateTime Utc { get; set; }
    public string Text { get; set; }
  }

  public sealed class MissionState {
    public int Version { get; set; }
    public bool Active { get; set; }
    public string Title { get; set; }
    public string Source { get; set; }
    public string SchoolId { get; set; }
    public string NextStep { get; set; }
    public int StepsCompleted { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime LastProgressUtc { get; set; }
    public DateTime LastGlanceUtc { get; set; }
    public string GlanceDay { get; set; }
    public int GlanceCount { get; set; }
    public List<MissionNote> Notes { get; set; }
    public MissionState() { Notes=new List<MissionNote>(); Source="personal"; }
  }

  // Quest loop linking Blackboard, focus, breaks, mood, and the trinket hoard.
  // Lives only under %LOCALAPPDATA%\Corbie; never reads screen text.
  public sealed class MissionBoard {
    readonly string path;
    readonly bool testing;
    MissionState state;
    public MissionBoard(string path=null, bool testing=false) {
      this.path=path; this.testing=testing;
      state=new MissionState { Version=1 };
      if(!testing) Load();
    }
    public MissionState State { get { return state; } }
    public bool Active { get { return state.Active && !string.IsNullOrWhiteSpace(state.Title); } }
    public bool HasNextStep { get { return Active && !string.IsNullOrWhiteSpace(state.NextStep); } }
    public string Title { get { return state.Title; } }
    public string NextStep { get { return state.NextStep; } }
    public double MinutesSinceProgress(DateTime utcNow) {
      if(!Active || state.LastProgressUtc==DateTime.MinValue) return 0;
      return Math.Max(0,(utcNow-state.LastProgressUtc).TotalMinutes);
    }
    public int ScrapCondition(DateTime utcNow) {
      double m=MinutesSinceProgress(utcNow);
      if(m<35) return 0;
      if(m<90) return 1;
      if(m<150) return 2;
      return 3;
    }
    public bool ShouldShepherd(DateTime utcNow) {
      return Active && HasNextStep && ScrapCondition(utcNow)>=3;
    }

    public void StartPersonal(string title) {
      title=RoostMemory.Short(title,160);
      if(string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Give the mission a name first.");
      state=new MissionState {
        Version=1, Active=true, Title=title, Source="personal",
        StartedUtc=DateTime.UtcNow, LastProgressUtc=DateTime.UtcNow, Notes=new List<MissionNote>()
      };
      Save();
    }
    public void StartBlackboard(SchoolEvent item) {
      if(item==null || string.IsNullOrWhiteSpace(item.Title)) throw new ArgumentException("Select a Blackboard deadline first.");
      state=new MissionState {
        Version=1, Active=true, Title=RoostMemory.Short(item.Title,160), Source="blackboard",
        SchoolId=item.Id, StartedUtc=DateTime.UtcNow, LastProgressUtc=DateTime.UtcNow, Notes=new List<MissionNote>()
      };
      Save();
    }
    public void SetNextStep(string step) {
      if(!Active) throw new InvalidOperationException("Start a mission before naming the next step.");
      step=RoostMemory.Short(step,180);
      if(string.IsNullOrWhiteSpace(step)) throw new ArgumentException("One small, concrete next step, please.");
      state.NextStep=step; state.LastProgressUtc=DateTime.UtcNow; Save();
    }
    public void AddNote(string text) {
      if(!Active) return;
      text=RoostMemory.Short(text,220);
      if(string.IsNullOrWhiteSpace(text)) return;
      if(state.Notes==null) state.Notes=new List<MissionNote>();
      state.Notes.Add(new MissionNote { Utc=DateTime.UtcNow, Text=text });
      if(state.Notes.Count>40) state.Notes=state.Notes.Skip(state.Notes.Count-40).ToList();
      state.LastProgressUtc=DateTime.UtcNow; Save();
    }
    public bool NoteFocusComplete(string sprintTitle) {
      if(!Active) return false;
      state.LastProgressUtc=DateTime.UtcNow;
      AddNote("Focus sprint toward: "+RoostMemory.Short(sprintTitle??state.NextStep??state.Title,120));
      return true;
    }
    public bool NoteBreakComplete() {
      if(!Active) return false;
      state.LastProgressUtc=DateTime.UtcNow;
      AddNote("Took a break while on mission.");
      return true;
    }
    public bool CompleteStep() {
      if(!HasNextStep) return false;
      string done=state.NextStep;
      state.StepsCompleted++;
      state.NextStep=null;
      state.LastProgressUtc=DateTime.UtcNow;
      AddNote("Finished step: "+done);
      Save();
      return true;
    }
    public void Abandon() {
      if(!Active) return;
      state.Active=false; state.NextStep=null; Save();
    }
    public bool ShouldGlance(DateTime utcNow) {
      if(!Active || !HasNextStep) return false;
      string day=utcNow.ToLocalTime().ToString("yyyy-MM-dd");
      if(state.GlanceDay!=day) { state.GlanceDay=day; state.GlanceCount=0; }
      if(state.GlanceCount>=2) return false;
      if(state.LastGlanceUtc!=DateTime.MinValue && (utcNow-state.LastGlanceUtc).TotalMinutes<90) return false;
      if(state.LastProgressUtc!=DateTime.MinValue && (utcNow-state.LastProgressUtc).TotalMinutes<45) return false;
      return true;
    }
    public string TakeGlance(bool octopus) {
      state.LastGlanceUtc=DateTime.UtcNow;
      state.GlanceCount++;
      Save();
      string step=state.NextStep??"the next step";
      if(octopus) return "Eight arms. Zero progress on \""+step+"\". Curious strategy.";
      return "Judgmental glance engaged. Still waiting on: "+step+".";
    }
    public string StatusLine() {
      if(!Active) return "No active mission. Pick a Blackboard deadline or name a personal quest.";
      string step=HasNextStep?("Next: "+state.NextStep):"Needs one concrete next step.";
      return "Mission: "+state.Title+" - "+step+" - "+state.StepsCompleted+" step(s) done";
    }
    public string AskNextStepLine(bool octopus) {
      if(octopus) return "Mission locked: \""+state.Title+"\". Name one tiny tentacle-sized next step.";
      return "Mission accepted: \""+state.Title+"\". What's one small, concrete next step?";
    }
    void Load() {
      try {
        if(string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        if(new FileInfo(path).Length>256*1024) return;
        var loaded=new JavaScriptSerializer().Deserialize<MissionState>(File.ReadAllText(path));
        if(loaded!=null) {
          state=loaded;
          if(state.Notes==null) state.Notes=new List<MissionNote>();
        }
      } catch { }
    }
    void Save() {
      if(testing || string.IsNullOrEmpty(path)) return;
      try {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp=path+".tmp";
        File.WriteAllText(temp, new JavaScriptSerializer().Serialize(state));
        if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
      } catch { }
    }
    public static void Check() {
      var board=new MissionBoard(null,true);
      board.StartPersonal("Draft intro");
      if(!board.Active || board.Title!="Draft intro") throw new Exception("MissionBoard: start personal failed");
      board.SetNextStep("Outline paragraph one");
      if(!board.HasNextStep) throw new Exception("MissionBoard: next step missing");
      board.NoteFocusComplete("Outline paragraph one");
      if(!board.CompleteStep() || board.State.StepsCompleted!=1) throw new Exception("MissionBoard: complete step failed");
      board.SetNextStep("Write draft");
      board.State.LastProgressUtc=DateTime.UtcNow.AddHours(-2);
      board.State.LastGlanceUtc=DateTime.MinValue;
      board.State.GlanceCount=0;
      board.State.GlanceDay=DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd");
      if(!board.ShouldGlance(DateTime.UtcNow)) throw new Exception("MissionBoard: glance should fire when idle");
      string line=board.TakeGlance(false);
      if(string.IsNullOrEmpty(line) || board.State.GlanceCount!=1) throw new Exception("MissionBoard: glance count wrong");
      if(board.ShouldGlance(DateTime.UtcNow)) throw new Exception("MissionBoard: glance cooldown broken");
      board.Abandon();
      if(board.Active) throw new Exception("MissionBoard: abandon failed");
      board.StartPersonal("Cold quest");
      board.SetNextStep("Do the thing");
      board.State.LastProgressUtc=DateTime.UtcNow.AddHours(-4);
      if(board.ScrapCondition(DateTime.UtcNow)<3) throw new Exception("MissionBoard: scrap cold");
      if(!board.ShouldShepherd(DateTime.UtcNow)) throw new Exception("MissionBoard: shepherd");
    }
  }
}
