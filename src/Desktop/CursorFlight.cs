using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace CorbiePet {
  // Positions are WPF desktop coordinates, not physical cursor pixels.
  public sealed class CursorFlightMotion {
    public const double MaxSpeed=520,Acceleration=1800;
    public Vector Velocity { get; private set; }
    public double Elapsed { get; private set; }
    public int Side { get; private set; }
    public CursorFlightMotion() { Side=-1; }
    public void Begin(Point position,Point pointer,double width) { Hold(); Side=pointer.X>=position.X+width/2?-1:1; }
    public void Hold() { Velocity=new Vector(); Elapsed=0; }
    static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
    static Point Keep(Point point,Rect limits) { return new Point(Math.Max(limits.Left,Math.Min(limits.Right,point.X)),Math.Max(limits.Top,Math.Min(limits.Bottom,point.Y))); }
    public Point Target(Point pointer,Rect limits,double width,double height) {
      double x=pointer.X+Side*135-width/2;
      double other=pointer.X-Side*135-width/2;
      // Keep one side until the edge requires a switch, avoiding nervous circling.
      if((x<limits.Left || x>limits.Right) && other>=limits.Left && other<=limits.Right) { Side=-Side; x=other; }
      return Keep(new Point(x,pointer.Y-height/2+28),limits);
    }
    public Point Step(Point position,Point pointer,Rect limits,double width,double height,double dt) {
      if(limits.IsEmpty || !Finite(position.X) || !Finite(position.Y) || !Finite(pointer.X) || !Finite(pointer.Y)
          || !Finite(dt) || dt<=0 || !Finite(width) || !Finite(height) || width<=0 || height<=0) { Hold(); return position; }
      dt=Math.Min(.1,dt);
      position=Keep(position,limits);
      var target=Target(pointer,limits,width,height); var delta=target-position;
      double distance=delta.Length;
      if(distance<=1.5 && Velocity.Length<12) { Velocity=new Vector(); Elapsed+=dt; return position; }
      Vector desired=distance<.001?new Vector():delta*(Math.Min(MaxSpeed,distance*4)/distance);
      Vector change=desired-Velocity;
      if(change.Length>Acceleration*dt) change*=Acceleration*dt/change.Length;
      Velocity+=change;
      var next=Keep(position+Velocity*dt,limits);
      if((next.X<=limits.Left && Velocity.X<0) || (next.X>=limits.Right && Velocity.X>0)) Velocity=new Vector(0,Velocity.Y);
      if((next.Y<=limits.Top && Velocity.Y<0) || (next.Y>=limits.Bottom && Velocity.Y>0)) Velocity=new Vector(Velocity.X,0);
      Elapsed+=dt; return next;
    }
    public static void Check() {
      var f=new CursorFlightMotion(); var limits=new Rect(-900,-400,1500,1000);
      Point p=new Point(-800,350),pointer=new Point(400,-100); f.Begin(p,pointer,180);
      double start=(f.Target(pointer,limits,180,180)-p).Length;
      for(int i=0;i<240;i++) {
        var old=p; var v=f.Velocity; p=f.Step(p,pointer,limits,180,180,1.0/60);
        if(!limits.Contains(p) || (p-old).Length>MaxSpeed/60+.001 || (f.Velocity-v).Length>Acceleration/60+.001) throw new Exception("Cursor flight escaped bounds, speed or acceleration limits.");
      }
      if((f.Target(pointer,limits,180,180)-p).Length>2 || start<500) throw new Exception("Cursor flight did not approach and settle beside the pointer.");
      var settled=p;
      for(int i=0;i<120;i++) p=f.Step(p,new Point(pointer.X+(i%2==0?.2:-.2),pointer.Y),limits,180,180,1.0/60);
      if((p-settled).Length>2 || f.Elapsed<5) throw new Exception("Stationary cursor did not retain a steady hover.");
      pointer=new Point(-850,0);
      for(int i=0;i<300;i++) { var old=p; p=f.Step(p,pointer,limits,180,180,.033); if(!limits.Contains(p) || (p-old).Length>MaxSpeed*.033+.001) throw new Exception("Cursor reversal jumped."); }
      if((f.Target(pointer,limits,180,180)-p).Length>2 || f.Side!=1) throw new Exception("Cursor flight did not reverse or switch clear of a screen edge.");
      foreach(var edge in new [] {new Point(-5000,-5000),new Point(5000,5000)}) for(int i=0;i<200;i++) { p=f.Step(p,edge,limits,180,180,.066); if(!limits.Contains(p)) throw new Exception("Offscreen pointer escaped the display."); }
      var before=p; p=f.Step(p,pointer,limits,180,180,double.NaN);
      if(p!=before || f.Velocity.Length!=0 || f.Elapsed!=0) throw new Exception("Invalid cursor sample did not hold safely.");
      p=f.Step(p,new Point(double.NaN,0),limits,180,180,.033); if(p!=before) throw new Exception("Invalid pointer moved the bird.");
      f.Step(p,pointer,limits,180,180,.033); f.Hold(); if(f.Velocity.Length!=0 || f.Elapsed!=0) throw new Exception("Hold retained flight momentum.");
    }
  }

  public partial class Pet {
    readonly CursorFlightMotion cursorFlight=new CursorFlightMotion();
    bool cursorFollowEnabled,cursorFollowingActive;
    CheckBox cursorFollowToggle;
    TextBlock cursorFollowStatus;
    MenuItem cursorFollowItem;
    Forms.ToolStripMenuItem trayCursorFollow;

    string CursorFollowBlock() {
      if(paused)return "paused";
      if(shuttingDown)return "saying goodnight";
      if(octopusMode)return "octopus form stays grounded";
      if(isNightSleep)return "sleeping";
      if(fullscreenOrPresentation)return "fullscreen or presentation";
      if(!testing && (!IsVisible || WindowState==WindowState.Minimized))return "bird is hidden";
      if(cinemaPicker!=null)return "drawing cinema walls";
      if(dragging || isFlung || isDragHovering)return "being moved";
      if(ContextMenu.IsOpen || tray.ContextMenuStrip.Visible)return "menu open";
      if(ViewerMailBusy || looking || micRequest!=null || sageRequest!=null || (lessonNotebook!=null && lessonNotebook.IsRecording))return "conversation or screen look";
      if(focusSprint.Active || breakCoach.OnBreak || breakCoach.Due)return "focus or break time";
      if(peckingArchive || notificationEdge.HuntActive || eyeRestCoach.GlideActive || eyeRestCoach.PesterBossActive
          || mischiefChaseTimer>0 || missionPoseTimer>0 || peckingCulpritTimer>0 || thermalAlertTimer>0 || offendedTimer>0)return "another activity";
      return null;
    }
    void SetCursorFollow(bool enabled) {
      if(refreshingPanel || shuttingDown)return;
      if(enabled && ViewerMailBusy)CancelViewerMail(false);
      if(enabled)StopFlockShow();
      if(enabled)StopTikTokTake();
      // A session-only performance choice; ordinary movement preferences stay saved.
      CancelDesktopForage(); ResetPerchIntent(); CancelBirdFlight(false);
      StopWalking(); ResetIdle(); EndShimejiInteraction(); attentionBid.Cancel();
      cursorGame.Step(.033,0,0,false,false); scritches.Stop();
      if(scritchHeart!=null)scritchHeart.Visibility=Visibility.Hidden;
      hop=0; nervesStartleTimer=0; nervesSample=false;
      cursorFollowEnabled=enabled; cursorFollowingActive=false; cursorFlight.Hold();
      RefreshCursorFollow(); UpdateStatus();
    }
    void StopCursorFollow() { if(cursorFollowEnabled)SetCursorFollow(false); }
    void HoldCursorFollow() {
      if(!cursorFollowingActive)return;
      cursorFollowingActive=false; cursorFlight.Hold();
      if(!paused)ResetIdle();
    }
    void BuildCursorFollowControls(StackPanel body) {
      cursorFollowToggle=Toggle("Follow cursor in flight",SetCursorFollow);
      body.Children.Add(Section("Follow cursor",cursorFollowToggle,cursorFollowStatus=Status(),
        new TextBlock { Text="A little airborne shadow: smooth pursuit, outward wing flaps and a steady hover beside your pointer. Works without fly nerves, wandering or music. Stays on his current display and inside cinema walls. Pauses for sleep, menus, conversation, focus and breaks. Off on startup; dragging, changing form or bringing him home stops it.",Foreground=MutedText,TextWrapping=TextWrapping.Wrap }));
      RefreshCursorFollow();
    }
    void RefreshCursorFollow() {
      bool prior=refreshingPanel; refreshingPanel=true;
      try {
        if(cursorFollowToggle!=null)cursorFollowToggle.IsChecked=cursorFollowEnabled;
        if(cursorFollowItem!=null)cursorFollowItem.IsChecked=cursorFollowEnabled;
        if(trayCursorFollow!=null)trayCursorFollow.Checked=cursorFollowEnabled;
      } finally { refreshingPanel=prior; }
      if(cursorFollowStatus!=null) {
        string block=CursorFollowBlock();
        cursorFollowStatus.Text=!cursorFollowEnabled?"Off · ready for a flying visit":block!=null?"Waiting · "+block:cursorFollowingActive?"Following · flapping and hovering beside the cursor":"On · move your cursor to lead him";
      }
    }
    bool StepCursorFollow(double dt) {
      if(!cursorFollowEnabled)return false;
      if(CursorFollowBlock()!=null) { HoldCursorFollow(); return false; }
      var source=PresentationSource.FromVisual(this); var limits=FlightBounds();
      if(source==null || source.CompositionTarget==null || limits.IsEmpty) { HoldCursorFollow(); return false; }
      // Existing cinema limits already include margins; add a small margin on a display.
      if(!CinemaWallsActive && limits.Width>32 && limits.Height>32)limits.Inflate(-16,-16);
      var physical=Forms.Cursor.Position;
      Point pointer=source.CompositionTarget.TransformFromDevice.Transform(new Point(physical.X,physical.Y));
      StepCursorFlight(dt,pointer,limits); return true;
    }
    void StepCursorFlight(double dt,Point pointer,Rect limits) {
      if(!cursorFollowingActive) { cursorFlight.Begin(new Point(Left,Top),pointer,Width); cursorFollowingActive=true; }
      var next=cursorFlight.Step(new Point(Left,Top),pointer,limits,Width,Height,dt); Left=next.X; Top=next.Y;
      double vx=cursorFlight.Velocity.X;
      if(Math.Abs(vx)>25)direction=vx>0?1:-1;
      else if(Math.Abs(pointer.X-(Left+Width/2))>30)direction=pointer.X>Left+Width/2?1:-1;
      facing.ScaleX=direction;
      double lift=Math.Min(1,cursorFlight.Elapsed/.25),beat=Math.Sin(cursorFlight.Elapsed*24);
      cursorTilt.Angle=cursorLean.X=danceTilt.Angle=0;
      motion.X=0; motion.Y=beat*1.8*lift; bodyRest.X=bodyRest.Y=0;
      squish.ScaleX=squish.ScaleY=1; headTilt.Angle=-5*lift;
      wingStretch.Angle=(28-26*beat)*lift;
      leftFoot.X=rightFoot.X=0; leftFoot.Y=rightFoot.Y=-7*lift;
      eye.Height=11; glint.Visibility=Visibility.Visible;
      StepArticulation(dt);
    }
    void CheckCursorFlight(string preview) {
      CursorFlightMotion.Check();
      paused=false; fullscreenOrPresentation=false; isNightSleep=false; octopusMode=false;
      SetNerves(false); flightsEnabled=false; wandering=false; autoPerch=true;
      bool perch=autoPerch,wander=wandering;
      SetCursorFollow(true);
      if(!cursorFollowEnabled || CursorFollowBlock()!=null || NervesAllowed(false) || CanInitiate())throw new Exception("Manual follow needs ambient nerves or allows competing antics.");
      if(autoPerch!=perch || wandering!=wander || flightsEnabled)throw new Exception("Follow changed ordinary movement preferences.");
      Left=100;Top=200;var limits=new Rect(0,0,750,500);var pointer=new Point(700,160);
      double minWing=100,maxWing=-100;
      for(int i=0;i<220;i++) { StepCursorFlight(.033,pointer,limits); if(i>150) { minWing=Math.Min(minWing,wingStretch.Angle);maxWing=Math.Max(maxWing,wingStretch.Angle); } if(!limits.Contains(new Point(Left,Top)))throw new Exception("Pet follow escaped walls."); }
      if(maxWing-minWing<40 || leftFoot.Y!=-7 || ArticulationState()!="flight")throw new Exception("Hover stopped flapping, dropped feet or lost flight articulation.");
      var settled=new Point(Left,Top);for(int i=0;i<90;i++)StepCursorFlight(.033,pointer,limits);
      if((new Point(Left,Top)-settled).Length>2)throw new Exception("Hover moved the window around a stationary target.");
      paused=true; if(CursorFollowBlock()==null || StepCursorFollow(.033))throw new Exception("Pause allowed cursor flight.");
      if(new Point(Left,Top)!=settled || cursorFlight.Velocity.Length!=0)throw new Exception("Pause moved follow or retained momentum.");paused=false;
      isNightSleep=true;if(CursorFollowBlock()==null)throw new Exception("Sleep allowed cursor flight.");isNightSleep=false;
      fullscreenOrPresentation=true;if(CursorFollowBlock()==null)throw new Exception("Fullscreen allowed cursor flight.");fullscreenOrPresentation=false;
      sageRequest=new System.Threading.CancellationTokenSource();if(CursorFollowBlock()==null)throw new Exception("Thinking allowed cursor flight.");sageRequest.Dispose();sageRequest=null;
      companion.Quiet=true;wasDancing=true;if(CursorFollowBlock()!=null)throw new Exception("Quiet or music blocked explicit performance mode.");companion.Quiet=false;wasDancing=false;
      var body=new StackPanel();BuildCursorFollowControls(body);RefreshCursorFollow();
      if(cursorFollowToggle.IsChecked!=true || cursorFollowItem.IsChecked!=true || !trayCursorFollow.Checked
          || !ContextMenu.Items.Contains(cursorFollowItem) || !tray.ContextMenuStrip.Items.Contains(trayCursorFollow))throw new Exception("Follow toggles disagree or are missing from a menu.");
      if(!string.IsNullOrEmpty(preview))RenderCursorFlightPreview(preview);
      Home();if(cursorFollowEnabled || cursorFollowingActive || cursorFollowToggle.IsChecked==true || cursorFollowItem.IsChecked==true || trayCursorFollow.Checked)throw new Exception("Home left follow enabled.");
      SetCursorFollow(true);SwitchForm();if(cursorFollowEnabled)throw new Exception("Changing form left follow enabled.");SwitchForm();
      SetCursorFollow(true);StopCursorFollow();if(cursorFollowEnabled || wingStretch.Angle!=0 || leftFoot.Y!=0 || autoPerch)throw new Exception("Stopping did not fold wings, plant feet or preserve Home's perch setting.");
    }
    void RenderCursorFlightPreview(string file) {
      var sheet=new System.Windows.Media.DrawingVisual();
      using(var draw=sheet.RenderOpen()) {
        draw.DrawRectangle(new SolidColorBrush(Color.FromRgb(16,23,31)),null,new Rect(0,0,960,265));
        var title=new FormattedText("CORBIE / FOLLOW CURSOR — actual vector flight poses",System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),17,Brushes.AntiqueWhite);
        draw.DrawText(title,new Point(24,18));
        for(int i=0;i<5;i++) {
          cursorFlight.Hold();cursorFollowingActive=false;Left=100;Top=200;
          int count=12+i*3;
          for(int frame=0;frame<count;frame++)StepCursorFlight(.033,new Point(700,160),new Rect(0,0,750,500));
          scene.Measure(new Size(180,180));scene.Arrange(new Rect(0,0,180,180));scene.UpdateLayout();
          var image=new System.Windows.Media.Imaging.RenderTargetBitmap(180,180,96,96,PixelFormats.Pbgra32);image.Render(scene);
          draw.DrawImage(image,new Rect(12+i*190,54,180,180));
          var label=new FormattedText(i==4?"continuous hover":"wing beat "+(i+1),System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),12,Brushes.LightSlateGray);
          draw.DrawText(label,new Point(50+i*190,238));
        }
      }
      var output=new System.Windows.Media.Imaging.RenderTargetBitmap(960,265,96,96,PixelFormats.Pbgra32);output.Render(sheet);
      var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(output));using(var stream=System.IO.File.Create(file))encoder.Save(stream);
    }
  }
}
