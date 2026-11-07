using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace CorbiePet {
  public sealed class NestMemoryState {
    public int Version { get; set; }
    public string FavoritePerch { get; set; }
    public Dictionary<string,double> PerchSeconds { get; set; }
    public string LastHotApp { get; set; }
    public int HotAppHits { get; set; }
    public int FocusStreak { get; set; }
    public int TreatCount { get; set; }
    public int[] TreatHours { get; set; }
    public DateTime LastSessionEndUtc { get; set; }
    public string LastMood { get; set; }
    public double LastAffection { get; set; }
    public DateTime LastFavoriteSeenUtc { get; set; }
    public NestMemoryState() {
      PerchSeconds=new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase);
      TreatHours=new int[24];
    }
  }

  // Continuity that shows up as behavior: preferred perches, launch greeting,
  // mood nudges, and sparse reunion remarks. Never stores transcripts or screen text.
  public sealed class NestMemory {
    readonly string path;
    readonly Random random=new Random();
    NestMemoryState state;
    string currentPerch;
    double reunionCooldown;
    DateTime lastFavoriteRemarkUtc=DateTime.MinValue;

    public NestMemory(string path) {
      this.path=path;
      state=new NestMemoryState();
      Load();
    }

    public NestMemoryState State { get { return state; } }
    public string FavoritePerch { get { return state.FavoritePerch; } }
    public int FocusStreak { get { return state.FocusStreak; } }

    public static string ProcessNameFromWindow(IntPtr hwnd) {
      if(hwnd==IntPtr.Zero) return null;
      uint pid;
      ProcessTriage.GetWindowThreadProcessId(hwnd,out pid);
      if(pid==0) return null;
      try {
        using(var process=Process.GetProcessById((int)pid)) {
          string name=process.ProcessName;
          if(string.IsNullOrEmpty(name)) return null;
          if(string.Equals(name,"Corbie",StringComparison.OrdinalIgnoreCase) ||
             name.StartsWith("Corbie-",StringComparison.OrdinalIgnoreCase)) return null;
          return name;
        }
      } catch { return null; }
    }

    public void Tick(double dt,bool journalEnabled) {
      if(dt<=0) return;
      reunionCooldown=Math.Max(0,reunionCooldown-dt);
      if(!journalEnabled || string.IsNullOrEmpty(currentPerch)) return;
      double seconds;
      state.PerchSeconds.TryGetValue(currentPerch,out seconds);
      state.PerchSeconds[currentPerch]=seconds+dt;
      RefreshFavorite();
    }

    public void NotePerch(string processName,bool journalEnabled) {
      if(!journalEnabled || string.IsNullOrEmpty(processName)) { currentPerch=null; return; }
      currentPerch=processName;
      if(string.Equals(processName,state.FavoritePerch,StringComparison.OrdinalIgnoreCase))
        state.LastFavoriteSeenUtc=DateTime.UtcNow;
    }

    public void NoteHotApp(string friendlyName,bool journalEnabled) {
      if(!journalEnabled || string.IsNullOrWhiteSpace(friendlyName)) return;
      if(string.Equals(state.LastHotApp,friendlyName,StringComparison.OrdinalIgnoreCase)) state.HotAppHits++;
      else { state.LastHotApp=friendlyName; state.HotAppHits=1; }
      Save();
    }

    public void NoteTreat(DateTime localNow,bool journalEnabled) {
      if(!journalEnabled) return;
      state.TreatCount++;
      state.TreatHours[Math.Max(0,Math.Min(23,localNow.Hour))]++;
      Save();
    }

    public void NoteFocusComplete(bool journalEnabled) {
      if(!journalEnabled) { state.FocusStreak=0; Save(); return; }
      state.FocusStreak=Math.Min(99,state.FocusStreak+1);
      Save();
    }

    public void NoteSessionEnd(MoodSnapshot snapshot,bool journalEnabled) {
      if(snapshot==null) return;
      state.LastSessionEndUtc=DateTime.UtcNow;
      state.LastMood=snapshot.Mood;
      state.LastAffection=snapshot.Affection;
      if(journalEnabled) RefreshFavorite();
      Save();
    }

    public void ApplyOnLaunch(MoodBrain mood,bool journalEnabled) {
      if(mood==null || !journalEnabled) return;
      if(state.LastSessionEndUtc==DateTime.MinValue) return;
      double hours=(DateTime.UtcNow-state.LastSessionEndUtc).TotalHours;
      if(hours>=0 && hours<=36) {
        // Soft reunion: a little fondness and curiosity, never a hard jump.
        for(int i=0;i<2;i++) mood.Conversation();
        if(state.FocusStreak>=2) mood.FileSuccess();
        if(state.TreatCount>=3) mood.Treat();
      }
    }

    public string ContinuityGreeting(bool octopus,bool journalEnabled) {
      if(!journalEnabled || state.LastSessionEndUtc==DateTime.MinValue) return null;
      double hours=(DateTime.UtcNow-state.LastSessionEndUtc).TotalHours;
      if(hours<0 || hours>72) return null;
      string perch=string.IsNullOrEmpty(state.FavoritePerch)?null:ProcessTriage.GetFriendlyName(state.FavoritePerch);
      if(state.FocusStreak>=3 && perch!=null)
        return octopus
          ? "Back again. Focus streak "+state.FocusStreak+". I already claimed a tentacle-grip near "+perch+"."
          : "Back again. Focus streak "+state.FocusStreak+". I already reserved the best perch near "+perch+".";
      if(state.HotAppHits>=2 && !string.IsNullOrEmpty(state.LastHotApp))
        return "Still here. If "+state.LastHotApp+" starts cooking the machine again, I'll peck first and shout later.";
      if(perch!=null)
        return octopus
          ? "Back near "+perch+" again. Eight arms ready to supervise this shambles."
          : "Back near "+perch+" again. Right. Feathers up. Let's see what you ruin today.";
      if(state.TreatCount>0 && PreferredTreatHour()==DateTime.Now.Hour)
        return "This is usually bribery hour. I remember. Where's my snack, you forgetful plum?";
      return null;
    }

    public string MaybeReunionRemark(string processName,bool octopus,bool journalEnabled) {
      if(!journalEnabled || string.IsNullOrEmpty(processName) || string.IsNullOrEmpty(state.FavoritePerch)) return null;
      if(!string.Equals(processName,state.FavoritePerch,StringComparison.OrdinalIgnoreCase)) return null;
      if(reunionCooldown>0) return null;
      if((DateTime.UtcNow-lastFavoriteRemarkUtc).TotalMinutes<20) return null;
      if(state.PerchSeconds.Values.Sum()<120) return null;
      reunionCooldown=180;
      lastFavoriteRemarkUtc=DateTime.UtcNow;
      string label=ProcessTriage.GetFriendlyName(processName);
      return octopus
        ? "Ah. Back to "+label+". My favorite window-shaped rock."
        : "Ah. "+label+" again. Home perch. Do not make it weird.";
    }

    public bool Prefers(string processName) {
      return !string.IsNullOrEmpty(processName) && string.Equals(processName,state.FavoritePerch,StringComparison.OrdinalIgnoreCase);
    }

    public string StatusLine() {
      if(state.LastSessionEndUtc==DateTime.MinValue) return "Nest memory empty -- habits form as you work.";
      var parts=new List<string>();
      if(!string.IsNullOrEmpty(state.FavoritePerch)) parts.Add("favorite perch "+ProcessTriage.GetFriendlyName(state.FavoritePerch));
      if(state.FocusStreak>0) parts.Add("focus streak "+state.FocusStreak);
      if(state.TreatCount>0) parts.Add(state.TreatCount+" treats remembered");
      if(state.HotAppHits>=2 && !string.IsNullOrEmpty(state.LastHotApp)) parts.Add("grudge: "+state.LastHotApp);
      return parts.Count==0?"Nest memory warming up":string.Join(" - ",parts);
    }

    public void Clear() {
      state=new NestMemoryState();
      currentPerch=null;
      Save();
    }

    int PreferredTreatHour() {
      int best=12,score=-1;
      for(int h=0;h<24;h++) if(state.TreatHours[h]>score) { score=state.TreatHours[h]; best=h; }
      return score<=0?-1:best;
    }

    void RefreshFavorite() {
      if(state.PerchSeconds==null || state.PerchSeconds.Count==0) return;
      var best=state.PerchSeconds.OrderByDescending(kv=>kv.Value).First();
      if(best.Value>=60) state.FavoritePerch=best.Key;
      // Cap dictionary growth.
      if(state.PerchSeconds.Count>40) {
        foreach(var drop in state.PerchSeconds.OrderBy(kv=>kv.Value).Take(state.PerchSeconds.Count-30).Select(kv=>kv.Key).ToList())
          state.PerchSeconds.Remove(drop);
      }
    }

    void Load() {
      try {
        if(path==null || !File.Exists(path)) return;
        if(new FileInfo(path).Length>256*1024) return;
        var data=new JavaScriptSerializer().Deserialize<NestMemoryState>(File.ReadAllText(path));
        if(data==null) return;
        state=data;
        if(state.PerchSeconds==null) state.PerchSeconds=new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase);
        else state.PerchSeconds=new Dictionary<string,double>(state.PerchSeconds,StringComparer.OrdinalIgnoreCase);
        if(state.TreatHours==null || state.TreatHours.Length!=24) state.TreatHours=new int[24];
      } catch { state=new NestMemoryState(); }
    }

    public void Save() {
      if(path==null) return;
      try {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        state.Version=1;
        string temp=path+".tmp";
        File.WriteAllText(temp,new JavaScriptSerializer().Serialize(state));
        if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
      } catch { }
    }

    public static void Check() {
      string folder=Path.Combine(Path.GetTempPath(),"Corbie-nest-"+Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(folder);
      try {
        string file=Path.Combine(folder,"nest-memory.json");
        var nest=new NestMemory(file);
        nest.NotePerch("code",true);
        for(int i=0;i<70;i++) nest.Tick(1,true);
        nest.NoteFocusComplete(true); nest.NoteFocusComplete(true); nest.NoteFocusComplete(true);
        nest.NoteTreat(new DateTime(2026,9,20,15,0,0),true);
        nest.NoteHotApp("VS Code",true); nest.NoteHotApp("VS Code",true);
        nest.NoteSessionEnd(new MoodSnapshot { Mood="Curious",Affection=60 },true);
        if(nest.FavoritePerch!="code") throw new Exception("favorite perch not learned");
        if(nest.FocusStreak!=3) throw new Exception("focus streak failed");
        string greet=nest.ContinuityGreeting(false,true);
        if(string.IsNullOrEmpty(greet) || greet.IndexOf("streak",StringComparison.OrdinalIgnoreCase)<0)
          throw new Exception("continuity greeting missing streak");
        using(var mood=new MoodBrain(true)) {
          double before=mood.Snapshot.Affection;
          nest.ApplyOnLaunch(mood,true);
          if(mood.Snapshot.Affection<=before) throw new Exception("launch mood nudge failed");
        }
        nest.Clear();
        var reloaded=new NestMemory(file);
        if(!string.IsNullOrEmpty(reloaded.FavoritePerch) || reloaded.FocusStreak!=0)
          throw new Exception("forget nest memory failed");
        nest=new NestMemory(null);
        nest.NotePerch("chrome",false);
        nest.Tick(100,false);
        if(nest.FavoritePerch!=null) throw new Exception("disabled journal still formed nest habits");
      } finally {
        foreach(string path in Directory.GetFiles(folder)) File.Delete(path);
        Directory.Delete(folder);
      }
    }
  }
}
