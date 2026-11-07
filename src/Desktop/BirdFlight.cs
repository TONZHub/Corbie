using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace CorbiePet {
  public sealed class FlightMotion {
    public bool Active { get; private set; }
    public Point From { get; private set; }
    public Point To { get; private set; }
    public Point Position { get; private set; }
    public double Elapsed { get; private set; }
    public double Travel { get; private set; }
    public double Arc { get; private set; }
    public string Phase { get { return !Active?"Perched":Elapsed<0.25?"Takeoff":Elapsed<0.25+Travel?"Flying":"Landing"; } }
    public double Progress { get { return Math.Max(0,Math.Min(1,(Elapsed-0.25)/Travel)); } }
    public bool Start(Point from,Point to,Rect limits) {
      if(Active || limits.IsEmpty || !Finite(from.X) || !Finite(from.Y) || !Finite(to.X) || !Finite(to.Y) || !limits.Contains(from) || !limits.Contains(to)) return false;
      double distance=(to-from).Length; if(distance<60 || distance>650) return false;
      From=from; To=to; Position=from; Elapsed=0; Travel=Math.Max(0.8,Math.Min(1.8,distance/350));
      Arc=Math.Max(0,Math.Min(Math.Min(70,distance*0.18),Math.Min(from.Y,to.Y)-limits.Top)); Active=true; return true;
    }
    static bool Finite(double v) { return !double.IsNaN(v) && !double.IsInfinity(v); }
    public void Step(double dt,bool paused) {
      if(!Active || paused || !Finite(dt) || dt<=0) return;
      Elapsed+=Math.Min(0.1,dt);
      double p=Progress, eased=p*p*(3-2*p);
      Position=new Point(From.X+(To.X-From.X)*eased,From.Y+(To.Y-From.Y)*eased-Arc*Math.Sin(p*Math.PI));
      if(Elapsed>=Travel+0.55) { Position=To; Active=false; }
    }
    public void Cancel() { Active=false; }
    public static void Check() {
      var f=new FlightMotion(); var bounds=new Rect(-500,-100,1000,600);
      if(!f.Start(new Point(-250,200),new Point(100,120),bounds)) throw new Exception("Valid flight rejected.");
      f.Step(0.1,false); var before=f.Position; double clock=f.Elapsed; f.Step(0.1,true);
      if(f.Position!=before || f.Elapsed!=clock) throw new Exception("Pause moved flight.");
      bool airborne=false,landing=false;
      for(int i=0;i<100 && f.Active;i++) {
        f.Step(0.033,false); airborne|=f.Phase=="Flying"; landing|=f.Phase=="Landing";
        if(!bounds.Contains(f.Position) || double.IsNaN(f.Position.X)) throw new Exception("Flight left monitor bounds.");
      }
      if(f.Active || f.Position!=new Point(100,120) || !airborne || !landing) throw new Exception("Flight phases did not complete at target.");
      if(f.Start(new Point(0,0),new Point(10,0),bounds) || f.Start(new Point(-450,0),new Point(450,0),bounds) || f.Start(new Point(0,0),new Point(900,0),bounds)) throw new Exception("Invalid flight accepted.");
      if(!f.Start(new Point(-200,-100),new Point(100,-100),bounds) || f.Arc!=0) throw new Exception("Ceiling clearance failed.");
      f.Step(double.NaN,false); if(f.Elapsed!=0) throw new Exception("Invalid timestep moved flight."); f.Cancel(); if(f.Active) throw new Exception("Flight cancel failed.");
    }
  }

  public partial class Pet {
    readonly FlightMotion birdFlight=new FlightMotion();
    bool flightsEnabled=true;
    bool flightEscapePending;
    double flightWait=25,flightTargetOffset;
    IntPtr flightWindow;
    NativeRect flightWindowRect;
    CheckBox flightsToggle;
    TextBlock flightStatus;
    string FlightPreference { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"corbie-flight-enabled.txt"); } }
    void InitializeFlights() {
      if(!testing) try { flightsEnabled=!File.Exists(FlightPreference) || File.ReadAllText(FlightPreference).Trim()!="disabled"; } catch { flightsEnabled=false; }
    }
    bool FlightAllowed(bool dancing) {
      return flightsEnabled && !cursorFollowEnabled && weatherDrive.Shelter<0.6 && !octopusMode && NervesAllowed(dancing) && !scritches.Engaged && !scritches.Celebrating
        && !isDragHovering && peckingCulpritTimer<=0 && thermalAlertTimer<=0 && offendedTimer<=0 && mischiefChaseTimer<=0 && missionPoseTimer<=0 && trinketInspectTimer<=0;
    }
    Rect FlightBounds() {
      if(CinemaWallsActive)return CinemaLimits;
      var source=PresentationSource.FromVisual(this); if(source==null) return Rect.Empty;
      var area=Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle).Bounds;
      var matrix=source.CompositionTarget.TransformFromDevice;
      var first=matrix.Transform(new Point(area.Left,area.Top)); var last=matrix.Transform(new Point(area.Right,area.Bottom));
      return new Rect(first.X,first.Y,Math.Max(0,last.X-first.X-Width),Math.Max(0,last.Y-first.Y-Height));
    }
    bool TryBirdFlight(double x,double y,double targetOffset,Rect limits) {
      if(birdFlight.Active || !FlightAllowed(wasDancing) || mood.Snapshot.Energy<40 || hop>0 || nervesStartleTimer>0) return false;
      flightWindow=autoPerch&&!CinemaWallsActive?perchWindow:IntPtr.Zero;
      if(flightWindow!=IntPtr.Zero && (!IsWindowVisible(flightWindow) || IsIconic(flightWindow) || !GetWindowRect(flightWindow,out flightWindowRect))) return false;
      if(!birdFlight.Start(new Point(Left,Top),new Point(x,y),limits)) return false;
      flightTargetOffset=targetOffset; StopWalking(); ResetIdle(); perchArrivalTime=0;
      nervesSample=false; nervesMayReact=false; flightWait=35+random.NextDouble()*25; return true;
    }
    bool FlightTargetValid() {
      if(flightWindow==IntPtr.Zero) return true;
      NativeRect rect;
      if(perchWindow!=flightWindow || !IsWindowVisible(flightWindow) || IsIconic(flightWindow) || !GetWindowRect(flightWindow,out rect)) return false;
      if(Math.Abs(rect.Left-flightWindowRect.Left)>4 || Math.Abs(rect.Top-flightWindowRect.Top)>4 || Math.Abs(rect.Right-flightWindowRect.Right)>4 || Math.Abs(rect.Bottom-flightWindowRect.Bottom)>4) return false;
      var active=GetForegroundWindow();
      return active==flightWindow || active==new System.Windows.Interop.WindowInteropHelper(this).Handle || active==companion.Handle
        || (corbiePanel!=null && active==new System.Windows.Interop.WindowInteropHelper(corbiePanel).Handle);
    }
    void CancelBirdFlight(bool restorePerch) {
      flightEscapePending=false;
      if(!birdFlight.Active) return;
      if(restorePerch) { Left=birdFlight.From.X; Top=birdFlight.From.Y; }
      birdFlight.Cancel();
      ResetIdle(); nervesSample=false; flightWait=12;
    }
    void SetFlights(bool enabled) {
      if(refreshingPanel) return;
      if(!testing) try { File.WriteAllText(FlightPreference,enabled?"enabled":"disabled"); } catch(Exception ex) { companion.SaySilent("Couldn't save flight setting: "+ex.Message); return; }
      flightsEnabled=enabled; if(!enabled) CancelBirdFlight(true); RefreshFlightStatus();
    }
    void BuildFlightControls(StackPanel body) {
      flightsToggle=Toggle("Short perch flights (raven)",SetFlights);
      body.Children.Add(Section("Little flights",flightsToggle,flightStatus=Status(),Actions(ActionButton("Try a short flight",TryShortFlight)),
        new TextBlock { Text="With the nervous system on, an energetic, curious raven occasionally flies along his perch or to a nearby active window. Flight rests during work, conversation and nighttime. Octopus form stays grounded.",TextWrapping=TextWrapping.Wrap,Foreground=MutedText }));
    }
    void RefreshFlightStatus() {
      if(flightStatus==null) return;
      bool prior=refreshingPanel; refreshingPanel=true; try { flightsToggle.IsChecked=flightsEnabled; } finally { refreshingPanel=prior; }
      flightStatus.Text=!flightsEnabled?"Flights off":!nervesEnabled?"Enable the fly nervous system to fly":octopusMode?"Octopus: staying grounded":birdFlight.Active?birdFlight.Phase:mood.Snapshot.Energy<40?"Resting: energy too low to fly":"Perched and ready";
    }
    void TryShortFlight() {
      UpdateWalkRoute(); var limits=FlightBounds();
      double target=Left+((routeMax-Left>=Left-routeMin)?1:-1)*Math.Min(220,Math.Max(routeMax-Left,Left-routeMin));
      target=Math.Max(routeMin,Math.Min(routeMax,target));
      if(!TryBirdFlight(target,Top,perchOffset+target-Left,limits) && flightStatus!=null) flightStatus.Text="Needs room on the perch, energy, and a quiet moment with the nervous system on.";
    }
    void ChooseBirdFlight(double dt,bool dancing) {
      if(birdFlight.Active) return;
      if(!FlightAllowed(dancing)) { flightEscapePending=false; return; }
      if(flightEscapePending && nervesStartleTimer<=0 && hop<=0) {
        flightEscapePending=false; UpdateWalkRoute();
        double target=Math.Max(routeMin,Math.Min(routeMax,Left+(nervesCursorOffset>=0?-160:160)));
        TryBirdFlight(target,Top,perchOffset+target-Left,FlightBounds()); return;
      }
      flightWait=Math.Max(0,flightWait-dt);
      var state=mood.Snapshot;
      if(flightWait>0 || !wandering || walking || idle!=Idle.Quiet || hop>0 || state.Energy<55 || state.Curiosity<55 || flyNerves.Attention<0.02 || cursorStartle.Wariness>0.3) return;
      TryShortFlight(); flightWait=35+random.NextDouble()*25;
    }
    void StepBirdFlight(double dt) {
      birdFlight.Step(dt,false); Left=birdFlight.Position.X; Top=birdFlight.Position.Y;
      if(!birdFlight.Active) {
        if(autoPerch && !CinemaWallsActive) perchOffset=flightTargetOffset;
        ResetIdle(); return;
      }
      direction=birdFlight.To.X>=birdFlight.From.X?1:-1; facing.ScaleX=direction;
      cursorTilt.Angle=cursorLean.X=0; danceTilt.Angle=0;
      motion.X=0; motion.Y=0; squish.ScaleX=squish.ScaleY=1; headTilt.Angle=0; wingStretch.Angle=0;
      bodyRest.X=bodyRest.Y=0;
      eye.Height=11; glint.Visibility=Visibility.Visible;
      if(birdFlight.Phase=="Takeoff") {
        double p=birdFlight.Elapsed/0.25; motion.Y=4*Math.Sin(p*Math.PI); squish.ScaleY=1-0.08*Math.Sin(p*Math.PI); wingStretch.Angle=28*p;
      } else if(birdFlight.Phase=="Flying") {
        double beat=Math.Sin(birdFlight.Elapsed*24);
        double envelope=Math.Min(1,Math.Min(birdFlight.Progress/0.12,(1-birdFlight.Progress)/0.12));
        wingStretch.Angle=28-beat*26*envelope; headTilt.Angle=-5;
        leftFoot.Y=rightFoot.Y=-7; motion.Y=Math.Sin(birdFlight.Elapsed*24)*1.5;
      } else {
        double p=Math.Max(0,Math.Min(1,(birdFlight.Elapsed-0.25-birdFlight.Travel)/0.3));
        wingStretch.Angle=28*(1-p);
        motion.Y=3*Math.Sin(p*Math.PI); leftFoot.Y=rightFoot.Y=-7*(1-p); headTilt.Angle=5*Math.Sin(p*Math.PI);
      }
      StepArticulation(dt);
    }
    void CheckBirdFlight() {
      FlightMotion.Check();
      SetNerves(true); flightsEnabled=true; paused=false; fullscreenOrPresentation=false; octopusMode=false;
      if(!FlightAllowed(false)) throw new Exception("Unblocked raven cannot fly.");
      octopusMode=true; if(FlightAllowed(false)) throw new Exception("Octopus flight allowed."); octopusMode=false;
      paused=true; if(FlightAllowed(false)) throw new Exception("Paused flight allowed."); paused=false;
      isNightSleep=true; if(FlightAllowed(false)) throw new Exception("Sleeping flight allowed."); isNightSleep=false;
      if(FlightAllowed(true)) throw new Exception("Dancing flight allowed.");
      autoPerch=false; Left=100; Top=150;
      if(!birdFlight.Start(new Point(100,150),new Point(300,150),new Rect(0,0,600,400))) throw new Exception("Pet fixture flight failed.");
      double minWing=double.PositiveInfinity,maxWing=double.NegativeInfinity;
      for(int i=0;i<100 && birdFlight.Active;i++) {
        StepBirdFlight(0.033); if(double.IsNaN(wingStretch.Angle) || double.IsNaN(motion.Y)) throw new Exception("Invalid flight pose.");
        if(birdFlight.Phase=="Flying") { minWing=Math.Min(minWing,wingStretch.Angle); maxWing=Math.Max(maxWing,wingStretch.Angle); }
      }
      if(maxWing-minWing<20) throw new Exception("Existing wing did not flap.");
      if(Left!=300 || Top!=150 || wingStretch.Angle!=0) throw new Exception("Landing did not fold wing at target.");
      birdFlight.Start(new Point(300,150),new Point(100,150),new Rect(0,0,600,400)); StepBirdFlight(0.1); CancelBirdFlight(true);
      if(Left!=300 || Top!=150 || birdFlight.Active) throw new Exception("Cancelled flight lost perch.");
      SetNerves(false); CheckControlPanel(); if(flightStatus==null) throw new Exception("Flight controls absent.");
    }
  }
}
