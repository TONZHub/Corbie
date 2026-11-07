using System;
using System.Windows;
using System.Windows.Controls;

namespace CorbiePet {
  // Window geometry is a synthetic visual input, not recognition of sprite art.
  // Absolute DIP motion avoids treating Corbie's own approach as mascot motion.
  public sealed class MascotMotion {
    public Point Before,After,Head;
    public double Age,Horizontal,Vertical,Proximity,Seconds;
    public bool Fresh;
  }
  public sealed class MascotMotionSense {
    MascotMotion latest;
    long identity;
    public void Reset() { latest=null; identity=0; }
    static bool Finite(double n) { return !double.IsNaN(n) && !double.IsInfinity(n); }
    public MascotMotion Observe(long id,Point point,Point head,double seconds) {
      if(id==0 || !Finite(point.X) || !Finite(point.Y) || !Finite(head.X) || !Finite(head.Y) || !Finite(seconds)) { Reset(); return null; }
      double age=latest==null?0:seconds-latest.Seconds;
      Point before=latest==null?point:latest.After;
      double length=(point-before).Length;
      bool fresh=latest!=null && id==identity && age>=0.04 && age<=0.15 && length<=220;
      double proximity=Math.Max(0,1-(point-head).Length/450);
      latest=new MascotMotion { Before=before,After=point,Head=head,Age=age,Fresh=fresh,Seconds=seconds,Proximity=proximity,
        Horizontal=fresh?(point.X-before.X)/age/200:0,Vertical=fresh?(point.Y-before.Y)/age/200:0 };
      identity=id; return latest;
    }
    public bool Read(double seconds,out MascotMotion sense) { sense=latest; return sense!=null && seconds>=sense.Seconds && seconds-sense.Seconds<0.25; }
    public static int ChoosePlay(double attention,double wary,double familiarity,double shelter,bool firstMeet,int variation) {
      if(shelter>0.6) return 0;
      if(wary>0.3) return 3;
      if(firstMeet || attention<0.025) return 0;
      if(familiarity<0.35) return 0;
      // Play variation remains a pet-design choice, gated by neural attention
      // and familiarity rather than a random unconditional pursuit.
      int choice=variation%5;
      return choice==0?2:choice==1?4:choice==2?7:choice==3?(familiarity>0.65 && attention>0.06?6:4):(attention>0.05?5:0);
    }
    public static void Check() {
      var sensor=new MascotMotionSense(); var head=new Point(0,0);
      if(sensor.Observe(1,new Point(-40,0),head,1).Fresh) throw new Exception("First mascot sample created motion.");
      var m=sensor.Observe(1,new Point(40,0),head,1.1);
      if(!m.Fresh || m.Horizontal<=0) throw new Exception("Mascot direction missing.");
      if(sensor.Observe(2,new Point(60,0),head,1.2).Fresh) throw new Exception("Target switch created false motion.");
      if(sensor.Observe(2,new Point(600,0),head,1.3).Fresh) throw new Exception("Mascot teleport created false motion.");
      if(sensor.Observe(2,new Point(590,0),head,2).Fresh) throw new Exception("Stale pair created motion.");
      MascotMotion read; if(sensor.Read(2.3,out read)) throw new Exception("Stale mascot retained input.");
      sensor.Observe(1,new Point(0,0),head,3); m=sensor.Observe(1,new Point(0,0),new Point(80,0),3.1);
      if(m.Horizontal!=0 || m.Vertical!=0) throw new Exception("Corbie's own motion became mascot motion.");
      if(ChoosePlay(0.5,0.5,0.8,0,false,0)!=3 || ChoosePlay(0,0,0.8,0,false,0)!=0 || ChoosePlay(0.5,0,0,0,false,0)!=0 || ChoosePlay(0.5,0,0.8,0.8,false,0)!=0 || ChoosePlay(0.5,0,0.8,0,false,0)!=2) throw new Exception("Neural social policy failed.");
    }
  }
  public partial class Pet {
    readonly MascotMotionSense shimejiMotion=new MascotMotionSense();
    readonly CursorPassReflex shimejiStartle=new CursorPassReflex();
    double shimejiSenseWait,shimejiConsumedSeconds=-1;
    double shimejiComfort;
    TextBlock shimejiNerveStatus;
    bool ShimejiBrainLinked { get { return nervesEnabled && flyNerves!=null && roost!=null && roost.Data.ShimejiEnabled; } }
    double NerveWariness { get { return Math.Max(cursorStartle.Wariness,shimejiStartle.Wariness); } }
    double ShimejiFamiliarity { get { return Math.Max(shimejiComfort,shimejiStartle.Familiarity); } }
    void RelaxShimejiNerves(double dt) { shimejiStartle.Relax(dt); shimejiComfort*=Math.Exp(-dt/300); }
    void ResetShimejiNerves() { shimejiMotion.Reset(); shimejiStartle.Reset(); shimejiComfort=0; shimejiSenseWait=0; shimejiConsumedSeconds=-1; }
    bool HabitatHasInput() { HabitatSense s; return habitat!=null && habitat.Read(out s); }
    bool ShimejiHasMotionInput() { MascotMotion s; return ShimejiBrainLinked && shimejiMotion.Read(animationClock.Elapsed.TotalSeconds,out s) && s.Proximity>0; }
    void SampleShimejiMotion(double dt) {
      if(!ShimejiBrainLinked || !NervesAllowed(wasDancing,true) || birdFlight.Active || HabitatHasInput()) { shimejiMotion.Reset(); return; }
      shimejiSenseWait-=dt; if(shimejiSenseWait>0) return; shimejiSenseWait=0.085;
      double x,y; long identity;
      var source=PresentationSource.FromVisual(this);
      if(source==null || !shimejiWatcher.SampleTarget(out x,out y,out identity)) { shimejiMotion.Reset(); return; }
      var matrix=source.CompositionTarget.TransformFromDevice;
      var point=matrix.Transform(new Point(x,y));
      var head=matrix.Transform(scene.PointToScreen(new Point(112,70)));
      shimejiMotion.Observe(identity,point,head,animationClock.Elapsed.TotalSeconds);
      Point local=PointFromScreen(new Point(x,y)); shimejiTargetX=Left+local.X; shimejiTargetY=Top+local.Y;
    }
    bool StepShimejiNerves(double dt,double seconds) {
      MascotMotion sample;
      if(!nervesMayReact || !ShimejiBrainLinked || !shimejiMotion.Read(seconds,out sample) || sample.Proximity<=0) { RelaxShimejiNerves(dt); return false; }
      bool newFrame=sample.Seconds!=shimejiConsumedSeconds;
      bool fresh=sample.Fresh && seconds-sample.Seconds<0.15;
      shimejiComfort*=Math.Exp(-dt/300);
      double speed=sample.Age>0?(sample.After-sample.Before).Length/sample.Age:0;
      if(newFrame && fresh && sample.Proximity>0.35 && speed>=20 && speed<600 && shimejiStartle.Wariness<0.3 && hop<=0)
        shimejiComfort+=(1-shimejiComfort)*Math.Min(0.15,sample.Age)/12;
      flyNerves.Step(dt,fresh?sample.Horizontal:0,fresh?sample.Vertical:0,sample.Proximity*weatherDrive.Sensitivity);
      nervesCursorOffset=sample.After.X-sample.Head.X;
      bool startled=shimejiStartle.Step(dt,!scritches.Engaged && !scritches.Celebrating && hop<=0 && peckingCulpritTimer<=0 && thermalAlertTimer<=0 && offendedTimer<=0 && mischiefChaseTimer<=0 && perchArrivalTime<=0,
        newFrame && fresh,sample.Age,sample.Before,sample.After,sample.Head);
      shimejiConsumedSeconds=sample.Seconds;
      ApplyNerveReaction(dt,startled,shimejiStartle.Wariness);
      if(startled) { shimejiPlayMode=3; shimejiPlayTimer=2.5; flightEscapePending=false; }
      nervesSample=false; return true;
    }
    bool ShimejiBrainEngaged() {
      MascotMotion sample;
      return ShimejiBrainLinked && shimejiMotion.Read(animationClock.Elapsed.TotalSeconds,out sample) && sample.Proximity>0 && (flyNerves.Attention>0.025 || shimejiStartle.Wariness>0.05 || ShimejiFamiliarity>0.35);
    }
    void RefreshShimejiNerves() {
      if(shimejiNerveStatus==null) return;
      shimejiNerveStatus.Text=ShimejiBrainLinked?"Brain-linked mascot motion · watchfulness "+Math.Round(shimejiStartle.Wariness*100)+"% · familiarity "+Math.Round(ShimejiFamiliarity*100)+"%":!roost.Data.ShimejiEnabled?"Shimeji reactions off":"Enable the fly nervous system to link reactions. Otherwise the original play routines remain available.";
    }
    void CheckShimejiNerves() {
      MascotMotionSense.Check(); ShimejiWatcher.Check();
      SetNerves(true); roost.Data.ShimejiEnabled=true; nervesMayReact=true; nervesCooldown=0;
      double time=1,peak=0;
      for(int i=0;i<12;i++) {
        shimejiMotion.Observe(1,new Point(-100+i*12,30),new Point(0,0),time);
        if(!StepShimejiNerves(0.1,time)) throw new Exception("Mascot did not reach desktop neural path.");
        peak=Math.Max(peak,flyNerves.Attention); time+=0.1;
      }
      if(peak<0.025) throw new Exception("Mascot motion did not excite fly circuit.");
      // Exercise the real frame dispatcher while social play is active.
      shimejiInteracting=true; shimejiMotion.Reset();
      double frameNow=animationClock.Elapsed.TotalSeconds;
      shimejiMotion.Observe(1,new Point(-30,20),new Point(0,0),frameNow-0.1);
      shimejiMotion.Observe(1,new Point(30,20),new Point(0,0),frameNow);
      StepNerves(0.033,false);
      if(!nervesMayReact || shimejiConsumedSeconds!=frameNow || nervesSample) throw new Exception("Social frame dispatcher failed to route mascot input.");
      shimejiInteracting=false;
      ResetNerveAttention(); nervesMayReact=true; hop=0;
      for(int i=0;i<150;i++) {
        double x=50*Math.Sin(i*0.15); shimejiMotion.Observe(1,new Point(x,20),new Point(0,0),20+i*0.1);
        StepShimejiNerves(0.1,20+i*0.1);
      }
      if(ShimejiFamiliarity<0.35 || shimejiStartle.Wariness>0.05) throw new Exception("Calm mascot encounters did not build familiarity.");
      ResetShimejiNerves(); ResetNerveAttention(); hop=0; nervesMayReact=true;
      shimejiMotion.Observe(1,new Point(-40,0),new Point(0,0),10);
      StepShimejiNerves(0.1,10);
      shimejiMotion.Observe(1,new Point(40,0),new Point(0,0),10.1);
      StepShimejiNerves(0.1,10.1);
      if(hop!=1 || shimejiStartle.Wariness<0.5 || shimejiPlayMode!=3) throw new Exception("Fast mascot pass did not startle and select retreat.");
      if(StepShimejiNerves(0.1,11)) throw new Exception("Stale mascot retained desktop input.");
      shimejiInteracting=true; if(!NervesAllowed(false,true) || NervesAllowed(false)) throw new Exception("Social neural gate incorrect.");
      paused=true; if(NervesAllowed(false,true)) throw new Exception("Mascot bypassed pause."); paused=false;
      isNightSleep=true; if(NervesAllowed(false,true)) throw new Exception("Mascot bypassed sleep."); isNightSleep=false;
      if(NervesAllowed(true,true)) throw new Exception("Mascot bypassed music.");
      nervesMayReact=false; shimejiMotion.Observe(1,new Point(-40,0),new Point(0,0),30); shimejiMotion.Observe(1,new Point(40,0),new Point(0,0),30.1);
      double familiar=ShimejiFamiliarity;
      if(StepShimejiNerves(0.1,30.1) || ShimejiFamiliarity>familiar) throw new Exception("Blocked mascot motion trained memory.");
      ResetNerveAttention(); if(shimejiStartle.Wariness!=0 || ShimejiFamiliarity!=0) throw new Exception("Reset retained mascot memory.");
      EndShimejiInteraction(); SetNerves(false);
      if(ShimejiBrainLinked) throw new Exception("Disabled brain retained mascot link.");
      CheckControlPanel(); if(shimejiNerveStatus==null) throw new Exception("Missing mascot neural status.");
    }
  }
}
