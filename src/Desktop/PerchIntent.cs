using System;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Web.Script.Serialization;
using Forms=System.Windows.Forms;

namespace CorbiePet {
  public enum PerchChoice { None, Investigate, Shelter, Home, Preen, Doze }
  public sealed class QuietPerchProfile {
    public string Key { get; set; }
    public double[] Quiet { get; set; }
  }
  // Learned comfort is five normalized positions on a route, not saved window
  // identities or content. Motion and choices remain designed pet behavior.
  public sealed class PerchIntentBrain {
    public readonly List<QuietPerchProfile> Profiles=new List<QuietPerchProfile>();
    double busy,quiet,interest,cooldown=8,settleCooldown,lastOffset;
    public double Offset { get { return lastOffset; } }
    public double QuietSeconds { get { return quiet; } }
    public void Reset() { busy=quiet=interest=lastOffset=0; cooldown=8; settleCooldown=0; }
    public QuietPerchProfile Profile(string key) {
      foreach(var p in Profiles) if(p.Key==key) return p;
      if(Profiles.Count>=8) Profiles.RemoveAt(0);
      var result=new QuietPerchProfile { Key=key,Quiet=new double[5] }; Profiles.Add(result); return result;
    }
    public static double Unit(double x) { return Math.Max(0,Math.Min(1,x)); }
    public double Favorite(string key,double current,bool away) {
      var p=Profile(key); int best=-1; double score=0;
      for(int i=0;i<5;i++) {
        double position=i/4.0;
        if(away && Math.Abs(position-current)<0.18) continue;
        if(p.Quiet[i]>=30 && p.Quiet[i]>score) { score=p.Quiet[i]; best=i; }
      }
      return best<0?-1:best/4.0;
    }
    public PerchChoice Step(double dt,bool fresh,ScreenMotionSignal signal,double attention,double position,string key) {
      dt=Math.Max(0,Math.Min(0.1,dt)); cooldown=Math.Max(0,cooldown-dt); settleCooldown=Math.Max(0,settleCooldown-dt);
      if(!fresh || signal==null) { busy=quiet=interest=0; return PerchChoice.None; }
      if(signal.Strength>0) lastOffset=signal.X;
      bool active=signal.Busy || signal.Strength>0;
      if(active) { quiet=0; busy=signal.Busy?busy+dt:Math.Max(0,busy-dt); interest=!signal.Busy && attention>0.025?interest+dt:0; }
      else {
        busy=interest=0; quiet+=dt;
        // Only unoccupied, freshly observed quiet time makes a spot comfortable.
        if(quiet>=2) { var p=Profile(key); int bin=(int)Math.Round(Unit(position)*4); p.Quiet[bin]=Math.Min(600,p.Quiet[bin]+dt); }
      }
      if(cooldown<=0 && busy>=2.5) { cooldown=45; busy=interest=0; return PerchChoice.Shelter; }
      if(cooldown<=0 && interest>=0.8 && Math.Abs(lastOffset)>50) { cooldown=40; interest=0; return PerchChoice.Investigate; }
      if(cooldown<=0 && quiet>=45 && Favorite(key,position,true)>=0) { cooldown=65; quiet=0; return PerchChoice.Home; }
      if(settleCooldown<=0 && quiet>=30) { settleCooldown=60; return PerchChoice.Doze; }
      if(settleCooldown<=0 && quiet>=12) { settleCooldown=18; return PerchChoice.Preen; }
      return PerchChoice.None;
    }
    public static double Target(PerchChoice choice,double current,double min,double max,double offset,double favorite) {
      double proposed=current;
      if(choice==PerchChoice.Investigate) proposed+=Math.Sign(offset)*Math.Min(110,Math.Abs(offset)*0.5);
      else if(favorite>=0) proposed=min+Unit(favorite)*(max-min);
      else if(choice==PerchChoice.Shelter) proposed=offset>=0?min:max;
      // Never turn a quiet-perch decision into a trip across the desktop.
      return Math.Max(min,Math.Min(max,Math.Max(current-240,Math.Min(current+240,proposed))));
    }
    public static void Check() {
      var b=new PerchIntentBrain(); var motion=new ScreenMotionSignal { Strength=0.12,X=180 };
      PerchChoice choice=PerchChoice.None;
      for(int i=0;i<95;i++) { var c=b.Step(0.1,true,motion,0.08,0.5,"test"); if(c!=PerchChoice.None) choice=c; }
      if(choice!=PerchChoice.Investigate) throw new Exception("No deliberate curious approach.");
      for(int i=0;i<100;i++) if(b.Step(0.1,true,motion,0.08,0.5,"test")!=PerchChoice.None) throw new Exception("Approach ignored cooldown.");
      b.Reset(); choice=PerchChoice.None;
      for(int i=0;i<95;i++) { var c=b.Step(0.1,true,new ScreenMotionSignal { Busy=true },0,0.5,"test"); if(c!=PerchChoice.None) choice=c; }
      if(choice!=PerchChoice.Shelter) throw new Exception("Busy scene never prompted shelter.");
      b.Reset(); bool preen=false,doze=false;
      for(int i=0;i<420;i++) { var c=b.Step(0.1,true,new ScreenMotionSignal(),0,0.25,"quiet"); preen|=c==PerchChoice.Preen; doze|=c==PerchChoice.Doze; }
      if(!preen || !doze || b.Favorite("quiet",0.8,true)!=0.25) throw new Exception("Quiet comfort/settling failed.");
      b.Reset(); bool home=false;
      for(int i=0;i<470;i++) home|=b.Step(0.1,true,new ScreenMotionSignal(),0,0.8,"quiet")==PerchChoice.Home;
      if(!home) throw new Exception("Favorite perch was not revisited.");
      b.Reset(); for(int i=0;i<600;i++) if(b.Step(0.1,false,motion,1,0,"missing")!=PerchChoice.None) throw new Exception("Missing capture invented a decision.");
      if(b.Profile("missing").Quiet[0]!=0) throw new Exception("Missing capture trained comfort.");
      if(Target(PerchChoice.Investigate,100,0,500,180,-1)!=190 || Target(PerchChoice.Shelter,300,0,800,100,-1)!=60 || Target(PerchChoice.Home,100,0,800,0,1)!=340) throw new Exception("Intent route limits failed.");
      for(int i=0;i<15;i++) b.Profile("bounded"+i); if(b.Profiles.Count!=8) throw new Exception("Unbounded perch memory.");
    }
  }
  public partial class Pet {
    readonly PerchIntentBrain perchIntent=new PerchIntentBrain();
    bool perchIntentEnabled=true,intentWalking,intentFlying;
    bool nervesCursorActive;
    PerchChoice intentChoice;
    double intentTarget,intentLook,intentRest,intentSaveWait;
    string intentRoute,intentError;
    CheckBox intentToggle;
    TextBlock intentStatus;
    string IntentPath { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"corbie-quiet-perches.json"); } }
    string IntentPreference { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"corbie-perch-intent-enabled.txt"); } }
    void InitializePerchIntent() {
      if(testing) return;
      try {
        perchIntentEnabled=!File.Exists(IntentPreference) || File.ReadAllText(IntentPreference).Trim()!="disabled";
        if(!File.Exists(IntentPath)) return;
        if(new FileInfo(IntentPath).Length>16384) throw new Exception("Perch memory too large.");
        var loaded=new JavaScriptSerializer().Deserialize<List<QuietPerchProfile>>(File.ReadAllText(IntentPath));
        if(loaded!=null) foreach(var p in loaded) {
          if(perchIntent.Profiles.Count>=8) break;
          if(p==null || string.IsNullOrEmpty(p.Key) || p.Key.Length>100 || p.Quiet==null || p.Quiet.Length!=5) continue;
          for(int i=0;i<5;i++) p.Quiet[i]=double.IsNaN(p.Quiet[i]) || double.IsInfinity(p.Quiet[i])?0:Math.Max(0,Math.Min(600,p.Quiet[i]));
          perchIntent.Profiles.Add(p);
        }
      } catch { intentError="Perch memory could not be read; starting fresh."; }
    }
    void SaveQuietPerches() {
      if(testing) return;
      try { File.WriteAllText(IntentPath,new JavaScriptSerializer().Serialize(perchIntent.Profiles)); intentError=null; }
      catch { intentError="Couldn't save favourite perches."; }
    }
    void ResetPerchIntent() {
      if(intentWalking) StopWalking();
      if(intentFlying) CancelBirdFlight(true);
      if(intentRest>0) ResetIdle();
      intentWalking=intentFlying=false; intentChoice=PerchChoice.None; intentLook=intentRest=0; intentRoute=null; perchIntent.Reset();
    }
    bool IntentAllowed(bool dancing) {
      return perchIntentEnabled && wandering && !octopusMode && NervesAllowed(dancing) && !HabitatHasInput() && !ShimejiHasMotionInput()
        && !scritches.Engaged && !scritches.Celebrating && !isDragHovering && NerveWariness<=0.3 && nervesStartleTimer<=0 && hop<=0
        && peckingCulpritTimer<=0 && thermalAlertTimer<=0 && offendedTimer<=0 && mischiefChaseTimer<=0 && missionPoseTimer<=0 && trinketInspectTimer<=0;
    }
    string IntentRouteKey() {
      var monitor=Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
      return monitor.DeviceName+":"+(autoPerch?"window":"desktop")+":"+perchWindow+":"+Math.Round(routeMin)+":"+Math.Round(routeMax)+":"+Math.Round(Top);
    }
    string QuietPerchKey() {
      return Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle).DeviceName+(autoPerch?":window":":desktop");
    }
    bool StepPerchIntent(double dt,bool dancing) {
      if(!IntentAllowed(dancing)) { ResetPerchIntent(); return false; }
      UpdateWalkRoute(); string route=IntentRouteKey();
      if(!intentFlying && intentRoute!=null && route!=intentRoute) ResetPerchIntent();
      intentRoute=route;
      if(intentFlying) {
        if(birdFlight.Active) return true;
        intentFlying=false; intentChoice=PerchChoice.None; intentRest=6; return true;
      }
      if(intentWalking) {
        if(!walking) { ResetPerchIntent(); return false; }
        double old=Left,delta=intentTarget-Left;
        direction=delta>=0?1:-1; facing.ScaleX=direction;
        double step=Math.Min(Math.Abs(delta),42*dt*mood.MovementSpeed*weatherDrive.Activity);
        Left=Math.Max(routeMin,Math.Min(routeMax,Left+direction*step));
        if(autoPerch) perchOffset+=Left-old;
        stride+=dt*11*mood.MovementSpeed;
        if(Math.Abs(intentTarget-Left)<1) { StopWalking(); ResetIdle(); intentWalking=false; intentChoice=PerchChoice.None; intentRest=6; }
        return true;
      }
      if(birdFlight.Active || walking) { perchIntent.Reset(); return false; }
      if(intentLook>0) {
        StepIdle(dt);
        intentLook=Math.Max(0,intentLook-dt);
        if(intentLook<=0) StartIntentMove();
        return true;
      }
      ScreenMotionSignal signal=null;
      bool fresh=screenNervesEnabled && screenNervesObserving && !nervesCursorActive && screenMotionSense.Peek(animationClock.Elapsed.TotalSeconds,out signal);
      if(!fresh) signal=null;
      if(intentRest>0) {
        if(!fresh || signal.Busy || signal.Strength>0) { intentRest=0; ResetIdle(); }
        else { intentRest=Math.Max(0,intentRest-dt); StepIdle(dt); return true; }
      }
      if(idle!=Idle.Quiet && idle!=Idle.Curious) return false;
      double width=routeMax-routeMin; if(width<10) return false;
      string key=QuietPerchKey();
      var choice=perchIntent.Step(dt,fresh,signal,flyNerves.Attention,(Left-routeMin)/width,key);
      if(fresh && !signal.Busy && signal.Strength==0) { intentSaveWait+=dt; if(intentSaveWait>=60) { SaveQuietPerches(); intentSaveWait=0; } }
      // Fresh local observation owns this quiet interval, rather than a random
      // walk resetting quiet time before it can ever become a favourite spot.
      if(choice==PerchChoice.None) return fresh;
      if(choice==PerchChoice.Preen || choice==PerchChoice.Doze) {
        StopWalking(); idle=lastIdle=choice==PerchChoice.Preen?Idle.Preen:Idle.Sleepy; idleTime=0; idleDuration=choice==PerchChoice.Preen?3.6:4;
        intentRest=idleDuration; return true;
      }
      double favorite=choice==PerchChoice.Investigate?-1:perchIntent.Favorite(key,(Left-routeMin)/width,true);
      intentTarget=PerchIntentBrain.Target(choice,Left,routeMin,routeMax,perchIntent.Offset,favorite);
      if(Math.Abs(intentTarget-Left)<25) { intentRest=5; return true; }
      intentChoice=choice; StopWalking(); ResetIdle();
      idle=lastIdle=Idle.Curious; idleTime=0; idleDuration=1.8; intentLook=1.8;
      direction=intentTarget>=Left?1:-1; facing.ScaleX=direction;
      return true;
    }
    void StartIntentMove() {
      ResetIdle();
      if(Math.Abs(intentTarget-Left)>=90 && TryBirdFlight(intentTarget,Top,perchOffset+intentTarget-Left,FlightBounds())) { intentFlying=true; return; }
      intentWalking=true; walking=true;
    }
    void SetPerchIntent(bool enabled) {
      if(refreshingPanel) return;
      if(!testing) try { File.WriteAllText(IntentPreference,enabled?"enabled":"disabled"); } catch { intentError="Couldn't save perch choices setting."; RefreshPerchIntent(); return; }
      perchIntentEnabled=enabled; ResetPerchIntent(); RefreshPerchIntent();
    }
    void BuildPerchIntentControls(StackPanel body) {
      intentToggle=Toggle("Curious approaches, quiet perches and settling",SetPerchIntent);
      body.Children.Add(Section("Perches with purpose",intentToggle,intentStatus=Status(),Actions(ActionButton("Forget favourite perches",()=>{ ResetPerchIntent(); perchIntent.Profiles.Clear(); SaveQuietPerches(); RefreshPerchIntent(); })),
        new TextBlock { Text="Requires the fly nervous system, nearby screen senses and wandering. He looks before approaching motion, tries another part of his current perch when things stay busy (preferring remembered quiet spots), and preens or briefly droops his eyes after calm watching. Choices rest for 40–65 seconds; trips stay within 240 pixels along the current perch. Flights fall back to walking. Only small position preferences are saved locally, never screen images or window contents.",TextWrapping=TextWrapping.Wrap,Foreground=MutedText }));
      RefreshPerchIntent();
    }
    void RefreshPerchIntent() {
      if(intentStatus==null) return;
      bool prior=refreshingPanel; refreshingPanel=true; try { intentToggle.IsChecked=perchIntentEnabled; } finally { refreshingPanel=prior; }
      intentStatus.Text=intentError??(!perchIntentEnabled?"Perch choices off":!nervesEnabled || !screenNervesEnabled?"Enable fly nerves and nearby screen senses":!wandering?"Enable wandering for perch choices":!IntentAllowed(wasDancing)?"Resting · other activity takes priority":intentChoice==PerchChoice.Investigate?"Curious · looking, then approaching":intentChoice==PerchChoice.Shelter?"Trying a calmer perch":intentChoice==PerchChoice.Home?"Returning toward a favourite spot":intentRest>0?"Settling comfortably":"Watching · learning quiet spots");
    }
    void CheckPerchIntent() {
      PerchIntentBrain.Check(); SetNerves(true); screenNervesEnabled=true; wandering=true;
      if(!IntentAllowed(false)) throw new Exception("Intent unexpectedly blocked.");
      paused=true; if(IntentAllowed(false)) throw new Exception("Intent bypassed pause."); paused=false;
      isNightSleep=true; if(IntentAllowed(false)) throw new Exception("Intent bypassed sleep."); isNightSleep=false;
      if(IntentAllowed(true)) throw new Exception("Intent bypassed music.");
      octopusMode=true; if(IntentAllowed(false)) throw new Exception("Octopus got bird routine."); octopusMode=false;
      wandering=false; if(IntentAllowed(false)) throw new Exception("Intent ignored wandering switch."); wandering=true;
      autoPerch=true; routeMin=100; routeMax=500; Left=250; intentTarget=310; intentWalking=walking=true;
      for(int i=0;i<150 && intentWalking;i++) StepPerchIntent(0.033,false);
      if(intentWalking || walking || Math.Abs(Left-310)>1 || Math.Abs(perchOffset-60)>1) throw new Exception("Bounded approach failed perch bookkeeping.");
      intentWalking=walking=true; paused=true; StepPerchIntent(0.033,false); if(walking || intentWalking) throw new Exception("Blocked intent did not release walking."); paused=false;
      ResetPerchIntent(); SetPerchIntent(false); if(IntentAllowed(false)) throw new Exception("Disabled intent still allowed.");
      SetNerves(false); CheckControlPanel(); if(intentStatus==null) throw new Exception("Missing perch choice controls.");
    }
  }
}
