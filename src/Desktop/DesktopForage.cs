using System;
using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Ellipse=System.Windows.Shapes.Ellipse;

namespace CorbiePet {
  // A small designed reward/avoidance adapter. It does not pretend that the
  // imported visual-motion circuit is an olfactory or mushroom-body model.
  public sealed class DesktopForageBrain {
    public double Appetite { get; private set; }
    public double SourAversion { get; private set; }
    public double DurableSourAversion { get; private set; }
    public int SweetFound { get; private set; }
    public int SourTried { get; private set; }
    double sleepSeconds;
    public DesktopForageBrain() { Appetite=0.85; SourAversion=DurableSourAversion=0.15; }
    public void Step(double dt) { if(dt>0 && !double.IsNaN(dt)) Appetite=Math.Min(1,Appetite+Math.Min(dt,1)/900); }
    public bool Investigate(bool sweet) { return Appetite>=0.22 && (sweet || SourAversion<0.62); }
    public void Feed() { Appetite=Math.Max(0,Appetite-0.48); }
    public void Taste(bool sweet) {
      if(sweet) { SweetFound++; Feed(); }
      else { SourTried++; SourAversion=Math.Min(1,SourAversion+0.28); Appetite=Math.Max(0,Appetite-0.1); }
    }
    public bool Sleep(double dt) {
      if(dt<=0 || double.IsNaN(dt) || double.IsInfinity(dt)) return false;
      sleepSeconds+=Math.Min(dt,1);
      if(sleepSeconds<60 || SourTried==0) return false;
      double learned=Math.Min(0.85,0.15+0.24*SourTried);
      if(learned<=DurableSourAversion+0.001) return false;
      DurableSourAversion=learned; SourAversion=Math.Max(SourAversion,learned); return true;
    }
    public void Wake() { sleepSeconds=0; }
    public void Restore(double remembered) {
      if(double.IsNaN(remembered) || double.IsInfinity(remembered)) return;
      DurableSourAversion=Math.Max(0.15,Math.Min(0.85,remembered));
      SourAversion=Math.Max(SourAversion,DurableSourAversion);
    }
    public void Forget() { SourAversion=DurableSourAversion=0.15; SourTried=0; sleepSeconds=0; }
    public static void Check() {
      var brain=new DesktopForageBrain();
      if(!brain.Investigate(true) || !brain.Investigate(false)) throw new Exception("New forager refused both cues.");
      brain.Taste(false); brain.Taste(false);
      if(brain.Investigate(false) || !brain.Investigate(true) || brain.SourTried!=2) throw new Exception("Sour cue did not become avoidable.");
      for(int i=0;i<59;i++) if(brain.Sleep(1)) throw new Exception("Sleep lesson consolidated before the rest interval.");
      if(!brain.Sleep(1) || brain.DurableSourAversion<0.62 || brain.Sleep(1)) throw new Exception("Sleep did not consolidate one bounded lesson.");
      var nextMorning=new DesktopForageBrain(); nextMorning.Restore(brain.DurableSourAversion);
      if(nextMorning.Investigate(false) || nextMorning.SourTried!=0) throw new Exception("Saved lesson did not survive a fresh brain.");
      nextMorning.Forget(); if(!nextMorning.Investigate(false) || nextMorning.DurableSourAversion!=0.15) throw new Exception("Forage lesson could not be forgotten.");
      brain.Taste(true); brain.Taste(true);
      if(brain.Investigate(true) || brain.SweetFound!=2) throw new Exception("Satiety did not stop foraging.");
      for(int i=0;i<900;i++) brain.Step(1);
      if(!brain.Investigate(true) || brain.Appetite>1) throw new Exception("Appetite did not recover within bounds.");
    }
  }

  sealed class ForageMarker : Window {
    const int GWL_EXSTYLE=-20,WS_EX_TRANSPARENT=0x20,WS_EX_NOACTIVATE=0x08000000;
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")] static extern int GetWindowLong(IntPtr handle,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongW")] static extern int SetWindowLong(IntPtr handle,int index,int value);
    readonly Ellipse fruit;
    public ForageMarker() {
      Width=30; Height=30; WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize;
      AllowsTransparency=true; Background=Brushes.Transparent; Topmost=true; ShowInTaskbar=false;
      ShowActivated=false; Focusable=false; IsHitTestVisible=false;
      var canvas=new Canvas { Width=30,Height=30,IsHitTestVisible=false };
      fruit=new Ellipse { Width=17,Height=14,Stroke=Brushes.White,StrokeThickness=1.3 };
      Canvas.SetLeft(fruit,7); Canvas.SetTop(fruit,11); canvas.Children.Add(fruit);
      var leaf=new Ellipse { Width=8,Height=4,Fill=new SolidColorBrush(Color.FromRgb(94,171,106)) };
      Canvas.SetLeft(leaf,14); Canvas.SetTop(leaf,7); canvas.Children.Add(leaf);
      Content=canvas;
      SourceInitialized+=(s,e)=> {
        var handle=new WindowInteropHelper(this).Handle;
        SetWindowLong(handle,GWL_EXSTYLE,GetWindowLong(handle,GWL_EXSTYLE)|WS_EX_TRANSPARENT|WS_EX_NOACTIVATE);
      };
    }
    public void SetKind(bool sweet) {
      fruit.Fill=new SolidColorBrush(sweet?Color.FromRgb(225,78,108):Color.FromRgb(139,123,83));
      fruit.Stroke=sweet?Brushes.LightPink:Brushes.LightGray;
    }
    public static void Check() {
      var marker=new ForageMarker();
      try {
        marker.SetKind(true); marker.SetKind(false);
        var handle=new WindowInteropHelper(marker).EnsureHandle();
        int style=GetWindowLong(handle,GWL_EXSTYLE);
        if((style&WS_EX_TRANSPARENT)==0 || (style&WS_EX_NOACTIVATE)==0 || marker.ShowInTaskbar) throw new Exception("Desktop cue might intercept clicks or focus.");
      } finally { marker.Close(); }
    }
  }

  public partial class Pet {
    readonly DesktopForageBrain desktopForageBrain=new DesktopForageBrain();
    ForageMarker forageMarker;
    Ellipse forageDream;
    CheckBox desktopForageToggle;
    TextBlock desktopForageStatus;
    bool desktopForageEnabled=true,forageActive,forageWalking,forageFlying,forageSweet;
    double forageCooldown=65,forageLook,forageTargetLeft;
    string forageRoute,forageMessage="Waiting for a quiet moment";
    string DesktopForagePreference { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"corbie-desktop-forage-enabled.txt"); } }
    string DesktopForageMemory { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"corbie-forage-memory.txt"); } }
    void InitializeDesktopForage() {
      forageDream=new Ellipse { Width=9,Height=9,Fill=new SolidColorBrush(Color.FromRgb(225,102,139)),Stroke=Brushes.White,
        StrokeThickness=1,Opacity=0.6,Visibility=Visibility.Hidden,IsHitTestVisible=false };
      Canvas.SetLeft(forageDream,145); Canvas.SetTop(forageDream,21); scene.Children.Add(forageDream);
      if(testing) return;
      try { desktopForageEnabled=!File.Exists(DesktopForagePreference) || File.ReadAllText(DesktopForagePreference).Trim()!="disabled"; }
      catch { desktopForageEnabled=false; }
      try {
        if(File.Exists(DesktopForageMemory) && new FileInfo(DesktopForageMemory).Length<=64) {
          double remembered;
          if(double.TryParse(File.ReadAllText(DesktopForageMemory),NumberStyles.Float,CultureInfo.InvariantCulture,out remembered)) desktopForageBrain.Restore(remembered);
        }
      } catch { }
    }
    void StepDesktopForageNeeds(double dt) {
      if(!paused && !isNightSleep) desktopForageBrain.Step(dt);
      mood.SetHunger(desktopForageBrain.Appetite*100);
    }
    void FeedDesktopForage() {
      desktopForageBrain.Feed(); mood.SetHunger(desktopForageBrain.Appetite*100); RefreshDesktopForage();
    }
    void SaveDesktopForageMemory() {
      if(testing) return;
      try { File.WriteAllText(DesktopForageMemory,desktopForageBrain.DurableSourAversion.ToString("R",CultureInfo.InvariantCulture)); }
      catch { forageMessage="Forage lesson could not be saved"; }
    }
    void StepDesktopForageSleep(double dt) {
      if(desktopForageEnabled && isNightSleep && !paused) {
        if(desktopForageBrain.Sleep(dt)) { SaveDesktopForageMemory(); RefreshDesktopForage(); }
        if(forageDream!=null && (desktopForageBrain.SweetFound+desktopForageBrain.SourTried>0 || desktopForageBrain.DurableSourAversion>0.15)) {
          forageDream.Visibility=Visibility.Visible;
          forageDream.Opacity=0.35+0.3*(0.5+0.5*Math.Sin(animationClock.Elapsed.TotalSeconds*1.4));
          Canvas.SetTop(forageDream,21+2*Math.Sin(animationClock.Elapsed.TotalSeconds*1.1));
        }
      } else {
        desktopForageBrain.Wake();
        if(forageDream!=null) forageDream.Visibility=Visibility.Hidden;
      }
    }
    bool DesktopForageAllowed(bool dancing) {
      return desktopForageEnabled && wandering && !octopusMode && weatherDrive.Shelter<0.6 && NervesAllowed(dancing) && !HabitatHasInput() && !ShimejiHasMotionInput()
        && !scritches.Engaged && !scritches.Celebrating && !isDragHovering && NerveWariness<=0.3 && nervesStartleTimer<=0 && hop<=0
        && peckingCulpritTimer<=0 && thermalAlertTimer<=0 && offendedTimer<=0 && mischiefChaseTimer<=0 && missionPoseTimer<=0 && trinketInspectTimer<=0;
    }
    void HideForageMarker() { if(forageMarker!=null) forageMarker.Hide(); }
    void CancelDesktopForage() {
      if(forageFlying) CancelBirdFlight(true);
      if(forageWalking) StopWalking();
      forageActive=forageWalking=forageFlying=false; forageLook=0; forageRoute=null;
      forageMessage="Resting · another activity took priority";
      HideForageMarker(); RefreshDesktopForage();
    }
    bool ScatterDesktopForage(bool sweet,bool manual) {
      if(!DesktopForageAllowed(wasDancing) || forageActive || birdFlight.Active || walking || intentWalking || intentFlying || idle!=Idle.Quiet) {
        if(manual) { forageMessage="Need a quiet, awake raven with fly nerves and wandering enabled"; RefreshDesktopForage(); }
        return false;
      }
      UpdateWalkRoute();
      double leftRoom=Left-routeMin,rightRoom=routeMax-Left;
      if(Math.Max(leftRoom,rightRoom)<80) { if(manual) { forageMessage="No room along this perch"; RefreshDesktopForage(); } return false; }
      double side=rightRoom>=leftRoom?1:-1;
      double distance=Math.Min(150+random.NextDouble()*70,side>0?rightRoom:leftRoom);
      forageTargetLeft=Math.Max(routeMin,Math.Min(routeMax,Left+side*distance));
      forageRoute=IntentRouteKey(); forageSweet=sweet; forageActive=true; forageLook=1.1;
      forageMessage=sweet?"Spotted a berry · sniffing":"Spotted a dubious crumb · sniffing";
      if(!testing) {
        if(forageMarker==null) forageMarker=new ForageMarker();
        forageMarker.SetKind(sweet);
        forageMarker.Left=forageTargetLeft+117; forageMarker.Top=Top+127;
        forageMarker.Show();
      }
      direction=side>0?1:-1; facing.ScaleX=direction;
      idle=lastIdle=Idle.Curious; idleTime=0; idleDuration=1.1;
      RefreshDesktopForage(); return true;
    }
    void FinishDesktopForage(bool tasted) {
      bool sweet=forageSweet;
      if(tasted) desktopForageBrain.Taste(sweet);
      mood.SetHunger(desktopForageBrain.Appetite*100);
      forageMessage=!tasted?"Ignored that crumb":sweet?"Berry found · appetite easing":"Dubious crumb · learning to avoid it";
      forageCooldown=tasted?105+random.NextDouble()*45:70;
      forageActive=forageWalking=forageFlying=false; forageLook=0; forageRoute=null;
      HideForageMarker(); StopWalking(); ResetIdle();
      if(tasted) {
        if(sweet) mood.Music(0.4,true);
        else mood.Music(-0.25,true);
        hop=0.5;
        CreatureFoley.Play(FoleyKind.Clack,companion.Quiet);
      }
      RefreshDesktopForage();
    }
    bool StepDesktopForage(double dt,bool dancing) {
      if(!DesktopForageAllowed(dancing)) { if(forageActive) CancelDesktopForage(); return false; }
      if(!forageActive) {
        forageCooldown=Math.Max(0,forageCooldown-dt);
        if(forageCooldown<=0 && desktopForageBrain.Appetite>=0.38 && !birdFlight.Active && !walking && idle==Idle.Quiet) {
          bool sweet=random.NextDouble()<0.78;
          if(ScatterDesktopForage(sweet,false)) return true;
          forageCooldown=15;
        }
        return false;
      }
      UpdateWalkRoute();
      if(!forageFlying && IntentRouteKey()!=forageRoute) { CancelDesktopForage(); return false; }
      if(forageFlying) {
        if(birdFlight.Active) return true;
        if(Math.Abs(Left-forageTargetLeft)<3) FinishDesktopForage(true);
        else CancelDesktopForage();
        return true;
      }
      if(forageWalking) {
        if(!walking) { CancelDesktopForage(); return false; }
        double delta=forageTargetLeft-Left,old=Left;
        direction=delta>=0?1:-1; facing.ScaleX=direction;
        Left=Math.Max(routeMin,Math.Min(routeMax,Left+direction*Math.Min(Math.Abs(delta),46*dt*mood.MovementSpeed*weatherDrive.Activity)));
        if(autoPerch) perchOffset+=Left-old;
        stride+=dt*11*mood.MovementSpeed;
        if(Math.Abs(forageTargetLeft-Left)<1) FinishDesktopForage(true);
        return true;
      }
      forageLook=Math.Max(0,forageLook-dt); StepIdle(dt);
      if(forageLook>0) return true;
      if(!desktopForageBrain.Investigate(forageSweet)) { FinishDesktopForage(false); return true; }
      ResetIdle();
      if(Math.Abs(forageTargetLeft-Left)>=90 && TryBirdFlight(forageTargetLeft,Top,perchOffset+forageTargetLeft-Left,FlightBounds())) {
        forageFlying=true; forageMessage="Flying toward the scent";
      } else { forageWalking=walking=true; forageMessage="Walking toward the scent"; }
      RefreshDesktopForage(); return true;
    }
    void SetDesktopForage(bool enabled) {
      if(refreshingPanel) return;
      if(!testing) try { File.WriteAllText(DesktopForagePreference,enabled?"enabled":"disabled"); }
      catch { forageMessage="Couldn't save desktop foraging setting"; RefreshDesktopForage(); return; }
      desktopForageEnabled=enabled;
      if(!enabled) CancelDesktopForage();
      else forageCooldown=Math.Min(forageCooldown,25);
      RefreshDesktopForage();
    }
    void BuildDesktopForageControls(StackPanel body) {
      desktopForageToggle=Toggle("Let Corbie forage on the desktop",SetDesktopForage);
      body.Children.Add(Section("Desktop foraging",desktopForageToggle,desktopForageStatus=Status(),Actions(
        ActionButton("Scatter a berry",()=>ScatterDesktopForage(true,true)),
        ActionButton("Scatter a dubious crumb",()=>ScatterDesktopForage(false,true)),
        ActionButton("Forget forage lessons",()=>{ desktopForageBrain.Forget(); SaveDesktopForageMemory(); forageMessage="Forage lessons forgotten"; RefreshDesktopForage(); })),
        new TextBlock { Text="Corbie's own tiny food cues appear on his current desktop perch. He sniffs, walks or makes an eligible short flight, grows less hungry after berries, and learns to reject dubious crumbs. After a quiet minute of night sleep, a learned crumb aversion becomes a small saved preference for tomorrow. A tiny berry dream appears above his head. He never reads, moves or eats your files. Fly nerves and wandering are required; other interactions take priority.",TextWrapping=TextWrapping.Wrap,Foreground=MutedText }));
      RefreshDesktopForage();
    }
    void RefreshDesktopForage() {
      if(desktopForageStatus==null) return;
      bool prior=refreshingPanel; refreshingPanel=true; try { desktopForageToggle.IsChecked=desktopForageEnabled; } finally { refreshingPanel=prior; }
      desktopForageStatus.Text=!desktopForageEnabled?"Foraging off":!nervesEnabled?"Enable the fly nervous system":!wandering?"Enable wandering":forageActive?forageMessage:
        forageMessage+" · hunger "+Math.Round(desktopForageBrain.Appetite*100)+"% · sour aversion "+Math.Round(desktopForageBrain.SourAversion*100)+"%"+
        (desktopForageBrain.DurableSourAversion>0.15?" · remembered after sleep":"");
    }
    void CheckDesktopForage() {
      DesktopForageBrain.Check(); ForageMarker.Check(); SetNerves(true); desktopForageEnabled=true; wandering=true; paused=false; octopusMode=false;
      if(!DesktopForageAllowed(false)) throw new Exception("Desktop foraging unexpectedly blocked.");
      paused=true; if(DesktopForageAllowed(false)) throw new Exception("Foraging bypassed pause."); paused=false;
      isNightSleep=true; if(DesktopForageAllowed(false)) throw new Exception("Foraging bypassed sleep."); isNightSleep=false;
      if(DesktopForageAllowed(true)) throw new Exception("Foraging interrupted music.");
      octopusMode=true; if(DesktopForageAllowed(false)) throw new Exception("Octopus got raven forage."); octopusMode=false;
      autoPerch=false; routeMin=100; routeMax=600; Left=250; Top=150; idle=Idle.Quiet;
      if(!ScatterDesktopForage(true,true) || !forageActive) throw new Exception("Manual desktop cue was not placed.");
      if(forageTargetLeft<routeMin || forageTargetLeft>routeMax || Math.Abs(forageTargetLeft-Left)>240) throw new Exception("Forage target left the route.");
      for(int i=0;i<350 && forageActive;i++) StepDesktopForage(0.033,false);
      if(forageActive || desktopForageBrain.SweetFound!=1) throw new Exception("Corbie did not reach and learn from the berry.");
      if(Math.Abs(mood.Snapshot.Hunger-desktopForageBrain.Appetite*100)>0.001 || mood.Snapshot.Hunger>=85) throw new Exception("Berry did not feed the shared hunger meter.");
      hop=0; idle=Idle.Quiet;
      if(!ScatterDesktopForage(false,true)) throw new Exception("Second cue was not placed.");
      paused=true; if(StepDesktopForage(0.033,false) || forageActive) throw new Exception("Blocked foraging did not cancel."); paused=false;
      hop=0; idle=Idle.Quiet;
      if(!ScatterDesktopForage(false,true)) throw new Exception("Flight-abort fixture cue was not placed.");
      forageFlying=true; birdFlight.Cancel();
      StepDesktopForage(0.033,false);
      if(forageActive || desktopForageBrain.SourTried!=0) throw new Exception("Cancelled flight falsely tasted a cue.");
      desktopForageBrain.Taste(false); desktopForageBrain.Taste(false);
      isNightSleep=true;
      for(int i=0;i<59;i++) StepDesktopForageSleep(1);
      if(desktopForageBrain.DurableSourAversion>0.15) throw new Exception("Dream lesson saved too early.");
      StepDesktopForageSleep(1);
      if(desktopForageBrain.DurableSourAversion<0.62 || forageDream.Visibility!=Visibility.Visible) throw new Exception("Dream lesson or visual did not appear.");
      isNightSleep=false; StepDesktopForageSleep(1);
      if(forageDream.Visibility!=Visibility.Hidden) throw new Exception("Dream berry stayed visible after waking.");
      SetDesktopForage(false); if(DesktopForageAllowed(false)) throw new Exception("Disabled foraging still allowed.");
      isNightSleep=true; StepDesktopForageSleep(1);
      if(forageDream.Visibility!=Visibility.Hidden) throw new Exception("Disabled foraging still dreamed.");
      isNightSleep=false; StepDesktopForageSleep(1);
      double hungry=desktopForageBrain.Appetite;
      paused=true; StepDesktopForageNeeds(1); paused=false;
      if(desktopForageBrain.Appetite!=hungry) throw new Exception("Hunger advanced while paused.");
      isNightSleep=true; StepDesktopForageNeeds(1); isNightSleep=false;
      if(desktopForageBrain.Appetite!=hungry) throw new Exception("Hunger advanced while sleeping.");
      StepDesktopForageNeeds(1);
      if(desktopForageBrain.Appetite<=hungry) throw new Exception("Awake hunger did not recover.");
      int berries=desktopForageBrain.SweetFound;
      FeedDesktopForage();
      if(mood.Snapshot.Hunger>=hungry*100 || desktopForageBrain.SweetFound!=berries) throw new Exception("Treat feeding changed taste history or failed to reduce hunger.");
      SetNerves(false); CheckControlPanel(); if(desktopForageStatus==null) throw new Exception("Missing desktop foraging controls.");
      if(panelHunger==null || panelHunger.Value!=mood.Snapshot.Hunger) throw new Exception("Nest hunger meter does not match actual hunger.");
    }
  }
}
