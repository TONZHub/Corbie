using System;
using System.IO;
using System.Web.Script.Serialization;

namespace CorbiePet {
  public sealed class MoodSnapshot {
    public string Mood { get; set; }
    public double Energy { get; set; }
    public double Curiosity { get; set; }
    public double Affection { get; set; }
    public double Irritation { get; set; }
    public double Contentment { get; set; }
    public double HeatStress { get; set; }
    public double Hunger { get; set; }
  }

  sealed class MoodStateFile {
    public int Version { get; set; }
    public double Energy { get; set; }
    public double Curiosity { get; set; }
    public double Affection { get; set; }
    public double Irritation { get; set; }
    public double Contentment { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
  }

  // A small persistent state model shared by Corbie's otherwise separate senses.
  // Values ease rather than jumping, and offline time lets agitation settle.
  public sealed class MoodBrain : IDisposable {
    readonly bool testing;
    readonly string path;
    readonly Random random=new Random();
    double energy=62,curiosity=68,affection=55,irritation=12,contentment=60,heatStress,schoolStress,hunger=85;
    double secondsSinceSave;
    DateTime lastPokeUtc=DateTime.MinValue;
    bool disposed;

    public MoodBrain(bool testing=false) {
      this.testing=testing;
      path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Corbie","mood.json");
      if(!testing) Load();
    }
    static double Bound(double value) { return Math.Max(0,Math.Min(100,value)); }
    static double Approach(double value,double target,double amount) {
      if(value<target) return Math.Min(target,value+amount);
      return Math.Max(target,value-amount);
    }
    string MoodName() {
      if(heatStress>=70) return "Frazzled";
      if(irritation>=72) return "Irritated";
      if(energy<=24) return "Sleepy";
      if(contentment>=74 && energy>=67) return "Excited";
      if(affection>=76 && irritation<35) return "Fond";
      if(curiosity>=74) return "Curious";
      if(contentment<=35) return "Grumpy";
      if(energy<=39) return "Tired";
      if(hunger>=75) return "Hungry";
      if(contentment>=62) return "Content";
      return "Observant";
    }
    public MoodSnapshot Snapshot {
      get { return new MoodSnapshot { Mood=MoodName(),Energy=energy,Curiosity=curiosity,Affection=affection,Irritation=irritation,Contentment=contentment,HeatStress=heatStress,Hunger=hunger }; }
    }
    public double MovementSpeed { get { return 0.72+energy/100*0.62+irritation/100*0.14; } }

    public void Step(double seconds,bool userActive,bool musicActive,bool paused) {
      if(disposed || seconds<=0) return;
      if(paused || !userActive) energy=Approach(energy,64,seconds*0.0022);
      else energy=Approach(energy,18,seconds*0.00125);
      curiosity=Approach(curiosity,userActive?62:50,seconds*0.0007);
      affection=Approach(affection,55,seconds*0.00008);
      irritation=Approach(irritation,0,seconds*0.0022);
      contentment=Approach(contentment,58,seconds*0.00065);
      if(musicActive && !paused) { energy+=seconds*0.0028; contentment+=seconds*0.0024; curiosity+=seconds*0.0008; }
      if(heatStress>0) { irritation+=seconds*0.0035*(heatStress/100); contentment-=seconds*0.0025*(heatStress/100); }
      if(schoolStress>0) { curiosity+=seconds*0.0012*(schoolStress/100); irritation+=seconds*0.001*(schoolStress/100); }
      ClampAll(); secondsSinceSave+=seconds;
      if(secondsSinceSave>=120) Save();
    }
    public void Treat() { affection+=8; contentment+=9; irritation-=7; energy+=3; Changed(); }
    public void Scritch() { affection+=3; contentment+=4; irritation-=2; Changed(); }
    public void CursorScare() { irritation+=5; contentment-=2; Changed(); }
    public void CursorPlay() { curiosity+=1; contentment+=1.5; Changed(); }
    public void SetHunger(double percent) { if(!double.IsNaN(percent) && !double.IsInfinity(percent)) hunger=Bound(percent); }
    public void Poke() {
      bool rapid=(DateTime.UtcNow-lastPokeUtc).TotalSeconds<5; lastPokeUtc=DateTime.UtcNow;
      irritation+=rapid?7:3; curiosity+=1.5; affection+=rapid?-0.5:0.4; contentment-=rapid?3:0.5; Changed();
    }
    public void Conversation() { affection+=2.2; curiosity+=4; contentment+=1.5; irritation-=1; Changed(); }
    public void ScreenLook() { curiosity+=5.5; energy+=1.5; Changed(); }
    public void BreakRest() { energy+=14; contentment+=7; irritation-=12; affection+=1; Changed(); }
    public void Music(double feeling,bool newTrack) {
      feeling=Math.Max(-1,Math.Min(1,feeling));
      if(newTrack) curiosity+=1.5;
      contentment+=feeling*(newTrack?2:0.006);
      energy+=Math.Max(0,feeling)*(newTrack?1:0.003);
      irritation+=Math.Max(0,-feeling)*(newTrack?1.2:0.003);
      ClampAll();
    }
    public void FileSuccess() { curiosity+=3; affection+=1.5; contentment+=3; Changed(); }
    public void FileFailure() { irritation+=5; contentment-=4; Changed(); }
    public void SetTemperature(double celsius) {
      heatStress=Bound((celsius-68)*4.2);
    }
    public void SetSchoolPressure(int overdue,int dueSoon) { schoolStress=Bound(Math.Max(0,overdue)*18+Math.Max(0,dueSoon)*7); }
    public string Report(bool octopus) {
      var s=Snapshot;
      string meters="Energy "+Math.Round(s.Energy)+", curiosity "+Math.Round(s.Curiosity)+", affection "+Math.Round(s.Affection)+", hunger "+Math.Round(s.Hunger)+"%.";
      switch(s.Mood) {
        case "Frazzled": return (octopus?"I am warm soup, which is the wrong kind of soup.":"My feathers are thermally offended.")+" "+meters;
        case "Irritated": return (octopus?"Several arms are drafting a formal complaint. In CAPITALS.":"One more poke and your cursor goes in the fucking bin. Try me.")+" "+meters;
        case "Sleepy": return (octopus?"I could flatten into a very respectable nap.":"My eyelids have unionized.")+" "+meters;
        case "Excited": return (octopus?"All eight arms are ready for nonsense.":"I have dangerous amounts of bird energy.")+" "+meters;
        case "Fond": return "Against my better judgment, I have grown rather fond of this arrangement. "+meters;
        case "Curious": return (octopus?"Every arm wants to investigate something different.":"Something interesting is happening. I can feel it in the beak.")+" "+meters;
        case "Grumpy": return "Current mood: furious little bastard with feathers. "+meters;
        case "Hungry": return "I could use a snack. Preferably a berry, rather than one of those suspect crumbs. "+meters;
        default: return "I am "+s.Mood.ToLowerInvariant()+", watchful, and adequately bribed. "+meters;
      }
    }
    public string Remark(bool octopus) {
      switch(MoodName()) {
        case "Frazzled": return octopus?"Eight arms and no bloody air conditioning. This machine is getting too hot.":"The laptop is getting hot. My tail feathers have filed a formal complaint.";
        case "Irritated": return octopus?"Eight arms. Eight chances to slap that cursor away. Try me.":"I remember every poke. Every. Single. One. Corvid memory, you cheeky git.";
        case "Sleepy": return octopus?"I am considering becoming a decorative puddle for a while.":"If anyone asks, this is strategic eye maintenance, not a nap.";
        case "Excited": return octopus?"Something has put a wiggle in all eight arms. Catastrophic energy.":"Excellent. I have acquired zoomies and no responsible outlet.";
        case "Fond": return "You're... acceptable. Barely. Don't get emotional, it's embarrassing.";
        case "Curious": return octopus?"I need to inspect that. And that. Probably all rubbish, but I'll check.":"What are we doing? Is it interesting? It's not, is it. Can I peck it anyway?";
        case "Grumpy": return "My patience has left the building. Snacks, or piss off.";
        case "Tired": return "Still supervising. At a reduced feather-per-second rate.";
        case "Hungry": return octopus?"Several arms are checking whether a snack has appeared.":"A berry would improve this arrangement considerably.";
        default: return random.Next(2)==0?"The nest is almost civilized right now. Suspicious. What did you break?":"I am content. Ruin it with an update dialog and you're sacked.";
      }
    }
    void Changed() { ClampAll(); secondsSinceSave+=15; if(secondsSinceSave>=30) Save(); }
    void ClampAll() {
      energy=Bound(energy); curiosity=Bound(curiosity); affection=Bound(affection); irritation=Bound(irritation); contentment=Bound(contentment);
    }
    void Load() {
      try {
        if(!File.Exists(path)) return;
        var data=new JavaScriptSerializer().Deserialize<MoodStateFile>(File.ReadAllText(path));
        if(data==null) return;
        energy=Bound(data.Energy); curiosity=Bound(data.Curiosity); affection=Bound(data.Affection); irritation=Bound(data.Irritation); contentment=Bound(data.Contentment);
        double hours=Math.Max(0,Math.Min(72,(DateTime.UtcNow-data.LastUpdatedUtc).TotalHours));
        double rest=1-Math.Exp(-hours/4);
        energy+=(66-energy)*rest; curiosity+=(56-curiosity)*rest*0.7; irritation+=(0-irritation)*rest; contentment+=(60-contentment)*rest*0.8;
        affection+=(55-affection)*(1-Math.Exp(-hours/120)); ClampAll();
      } catch { }
    }
    void Save() {
      secondsSinceSave=0; if(testing) return;
      try {
        Directory.CreateDirectory(Path.GetDirectoryName(path)); string temp=path+".tmp";
        var data=new MoodStateFile { Version=1,Energy=energy,Curiosity=curiosity,Affection=affection,Irritation=irritation,Contentment=contentment,LastUpdatedUtc=DateTime.UtcNow };
        File.WriteAllText(temp,new JavaScriptSerializer().Serialize(data));
        if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
      } catch { }
    }
    public void Dispose() { if(disposed) return; Save(); disposed=true; }
    public static void Check() {
      using(var mood=new MoodBrain(true)) {
        var before=mood.Snapshot; mood.Treat(); var fed=mood.Snapshot;
        if(fed.Affection<=before.Affection || fed.Contentment<=before.Contentment) throw new Exception("Treat did not improve Corbie's mood.");
        mood.Scritch(); if(mood.Snapshot.Affection<=fed.Affection) throw new Exception("Scritches did not improve affection.");
        mood.Poke(); mood.Poke(); var poked=mood.Snapshot;
        if(poked.Irritation<=fed.Irritation) throw new Exception("Repeated poking did not irritate Corbie.");
        mood.Conversation(); mood.ScreenLook(); if(mood.Snapshot.Curiosity<=poked.Curiosity) throw new Exception("Awareness did not increase curiosity.");
        double happy=mood.Snapshot.Contentment; mood.Music(1,true); if(mood.Snapshot.Contentment<=happy) throw new Exception("Liked music did not improve mood.");
        double tired=mood.Snapshot.Energy; mood.BreakRest(); if(mood.Snapshot.Energy<=tired) throw new Exception("A completed break did not restore energy.");
        mood.SetTemperature(92); mood.Step(60,true,false,false); if(mood.Snapshot.HeatStress<50) throw new Exception("Heat did not affect Corbie's state.");
        if(string.IsNullOrEmpty(mood.Report(false)) || string.IsNullOrEmpty(mood.Remark(true))) throw new Exception("Mood text is unavailable.");
      }
    }
  }
}
