using System;
using System.IO;
using System.Web.Script.Serialization;

namespace CorbiePet {
  public enum BreakSignal { None, Due, Completed }
  public sealed class BreakUpdate {
    public BreakSignal Signal { get; set; }
    public string Message { get; set; }
  }
  sealed class BreakStateFile {
    public int Version { get; set; }
    public bool Enabled { get; set; }
    public bool DetoxMode { get; set; }
    public int WorkMinutes { get; set; }
    public int BreakMinutes { get; set; }
    public double ActiveSeconds { get; set; }
    public double NextReminderSeconds { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
  }

  public sealed class BreakCoach : IDisposable {
    readonly bool testing;
    readonly string path;
    double activeSeconds,nextReminderSeconds,saveSeconds;
    DateTime breakEndsUtc;
    bool due,onBreak,disposed;
    public bool Enabled { get; private set; }
    public bool DetoxMode { get; private set; }
    public int WorkMinutes { get; private set; }
    public int BreakMinutes { get; private set; }
    public bool Due { get { return due; } }
    public bool OnBreak { get { return onBreak; } }
    public double Progress { get { return Math.Max(0,Math.Min(1,activeSeconds/(WorkMinutes*60.0))); } }
    public TimeSpan BreakRemaining { get { return !onBreak?TimeSpan.Zero:(breakEndsUtc>DateTime.UtcNow?breakEndsUtc-DateTime.UtcNow:TimeSpan.Zero); } }
    public string Status {
      get {
        if(!Enabled) return "Break coach is off";
        if(onBreak) return "Break in progress · "+Format(BreakRemaining)+" remaining";
        if(due) return "Break due now · Corbie is judging your posture";
        return Format(TimeSpan.FromSeconds(Math.Max(0,WorkMinutes*60-activeSeconds)))+" of active time until the next break";
      }
    }
    public BreakCoach(bool testing=false) {
      this.testing=testing; WorkMinutes=55; BreakMinutes=5; Enabled=true;
      path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Corbie","breaks.json");
      if(!testing) Load();
      if(nextReminderSeconds<=0) nextReminderSeconds=WorkMinutes*60;
    }
    public BreakUpdate Step(double seconds,double idleSeconds,bool paused) {
      var update=new BreakUpdate();
      if(disposed || !Enabled || paused) return update;
      if(onBreak) {
        if(DateTime.UtcNow>=breakEndsUtc) return Complete("Break complete. Your spine and this bird are both marginally less offended.");
        return update;
      }
      if(idleSeconds>=BreakMinutes*60 && activeSeconds>=300) return Complete("You actually stepped away. Excellent. I was beginning to think the chair had claimed you.");
      if(idleSeconds<90) activeSeconds+=Math.Max(0,Math.Min(2,seconds));
      saveSeconds+=seconds; if(saveSeconds>=60) Save();
      if(!due && activeSeconds>=nextReminderSeconds) {
        due=true; Save(); update.Signal=BreakSignal.Due;
        update.Message=DetoxMode
          ? "Detox break. Off the machine for "+BreakMinutes+" minutes. Yes, I mean now. Your tabs will survive without you."
          : "You've been at this for a while. Stretch, drink something, and look at an object that isn't made of pixels.";
      }
      return update;
    }
    public void SetEnabled(bool value) { Enabled=value; if(!value) { due=false; onBreak=false; } else if(nextReminderSeconds<=activeSeconds) nextReminderSeconds=activeSeconds+WorkMinutes*60; Save(); }
    public void SetDetoxMode(bool value) { DetoxMode=value; Save(); }
    public void SetWorkMinutes(int value) { WorkMinutes=Math.Max(15,Math.Min(180,value)); if(!due) nextReminderSeconds=activeSeconds+WorkMinutes*60; Save(); }
    public void SetBreakMinutes(int value) { BreakMinutes=Math.Max(1,Math.Min(30,value)); Save(); }
    public void StartBreak() { if(!Enabled) Enabled=true; due=false; onBreak=true; breakEndsUtc=DateTime.UtcNow.AddMinutes(BreakMinutes); Save(); }
    public void Snooze(int minutes) { due=false; onBreak=false; nextReminderSeconds=activeSeconds+Math.Max(1,minutes)*60; Save(); }
    public BreakUpdate FinishEarly() { return Complete("Break logged. Short, but better than becoming permanently chair-shaped."); }
    public void ResetSession() { due=false; onBreak=false; activeSeconds=0; nextReminderSeconds=WorkMinutes*60; Save(); }
    BreakUpdate Complete(string message) {
      bool hadSession=onBreak || due || activeSeconds>=300;
      onBreak=false; due=false; activeSeconds=0; nextReminderSeconds=WorkMinutes*60; Save();
      return new BreakUpdate { Signal=hadSession?BreakSignal.Completed:BreakSignal.None,Message=message };
    }
    static string Format(TimeSpan value) {
      if(value.TotalMinutes>=1) return Math.Ceiling(value.TotalMinutes)+"m";
      return Math.Max(0,Math.Ceiling(value.TotalSeconds))+"s";
    }
    void Load() {
      try {
        if(!File.Exists(path)) return;
        var data=new JavaScriptSerializer().Deserialize<BreakStateFile>(File.ReadAllText(path)); if(data==null) return;
        Enabled=data.Enabled; DetoxMode=data.DetoxMode; WorkMinutes=data.WorkMinutes<=0?55:data.WorkMinutes; BreakMinutes=data.BreakMinutes<=0?5:data.BreakMinutes;
        activeSeconds=Math.Max(0,data.ActiveSeconds); nextReminderSeconds=Math.Max(0,data.NextReminderSeconds);
        if((DateTime.UtcNow-data.LastUpdatedUtc).TotalMinutes>=BreakMinutes) { activeSeconds=0; nextReminderSeconds=WorkMinutes*60; }
      } catch { }
    }
    void Save() {
      saveSeconds=0; if(testing) return;
      try {
        Directory.CreateDirectory(Path.GetDirectoryName(path)); string temp=path+".tmp";
        var data=new BreakStateFile { Version=1,Enabled=Enabled,DetoxMode=DetoxMode,WorkMinutes=WorkMinutes,BreakMinutes=BreakMinutes,ActiveSeconds=activeSeconds,NextReminderSeconds=nextReminderSeconds,LastUpdatedUtc=DateTime.UtcNow };
        File.WriteAllText(temp,new JavaScriptSerializer().Serialize(data)); if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
      } catch { }
    }
    public void Dispose() { if(disposed) return; Save(); disposed=true; }
    public static void Check() {
      using(var coach=new BreakCoach(true)) {
        coach.SetWorkMinutes(15); BreakUpdate due=new BreakUpdate(); for(int i=0;i<451;i++) { var step=coach.Step(2,0,false); if(step.Signal==BreakSignal.Due) due=step; }
        if(due.Signal!=BreakSignal.Due || !coach.Due) throw new Exception("Break coach did not detect a due break.");
        coach.Snooze(10); if(coach.Due) throw new Exception("Break snooze failed.");
        coach.StartBreak(); if(!coach.OnBreak || coach.BreakRemaining<=TimeSpan.Zero) throw new Exception("Manual break did not start.");
        var done=coach.FinishEarly(); if(done.Signal!=BreakSignal.Completed || coach.OnBreak || coach.Progress!=0) throw new Exception("Break completion failed.");
      }
    }
  }
}
