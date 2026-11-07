using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace CorbiePet {
  // A bounded, decorative performance. Positions are local WPF coordinates.
  public static class FlockChoreography {
    public const int Count=11;
    public const double Duration=14;
    public struct Pose { public Point Center; public double Scale,Opacity,Direction,Tilt; }
    static double Ease(double x) { x=Math.Max(0,Math.Min(1,x));return x*x*(3-2*x); }
    static Point Mix(Point a,Point b,double t) {return a+(b-a)*t;}
    public static Pose Sample(int index,double time,Size area,Point anchor) {
      double phase=index*Math.PI*2/Count,angle=time*.95+phase;
      double rx=Math.Min(320,area.Width*.27),ry=Math.Min(155,area.Height*.22);
      Point center=new Point(Math.Max(rx+65,Math.Min(area.Width-rx-65,anchor.X)),Math.Max(ry+70,Math.Min(area.Height-ry-70,anchor.Y)));
      Point orbit=new Point(center.X+Math.Cos(angle)*rx,center.Y+Math.Sin(angle)*ry);
      Point entry=new Point(index%2==0?-100:area.Width+100,area.Height*(.12+.7*index/(Count-1)));
      Point exit=new Point(index%2==0?area.Width+130:-130,-100-index*22);
      Point position=time<3?Mix(entry,orbit,Ease(time/3)):time>10?Mix(orbit,exit,Ease((time-10)/4)):orbit;
      double dx=time<2?orbit.X-entry.X:time>11?exit.X-orbit.X:-Math.Sin(angle)*rx;
      return new Pose { Center=position,Scale=.43+.15*(Math.Sin(angle)+1)/2,Direction=dx>=0?1:-1,
        Tilt=Math.Cos(angle)*9,Opacity=Math.Max(0,Math.Min(1,Math.Min(time/.8,(Duration-time)/1.2)))*(.76+.24*(Math.Sin(angle)+1)/2) };
    }
  }

  public sealed class FlockStage : FrameworkElement {
    readonly IList<BitmapSource> frames;
    public double Elapsed {get;set;}
    public Point Anchor {get;set;}
    public FlockStage(IList<BitmapSource> frames) {this.frames=frames;IsHitTestVisible=false;ClipToBounds=true;}
    protected override void OnRender(DrawingContext dc) {
      base.OnRender(dc);if(frames.Count==0)return;
      for(int i=0;i<FlockChoreography.Count;i++) {
        var pose=FlockChoreography.Sample(i,Elapsed,RenderSize,Anchor);
        int frame=(int)((Elapsed*3.8+i*.19)*frames.Count)%frames.Count;
        double side=180*pose.Scale;
        dc.PushOpacity(pose.Opacity);
        dc.PushTransform(new TranslateTransform(pose.Center.X,pose.Center.Y));
        dc.PushTransform(new RotateTransform(pose.Tilt));
        dc.PushTransform(new ScaleTransform(pose.Direction,1));
        dc.DrawImage(frames[frame],new Rect(-side/2,-side/2,side,side));
        dc.Pop();dc.Pop();dc.Pop();dc.Pop();
      }
    }
  }

  public sealed class FlockWindow : Window {
    const int ExStyle=-20,Transparent=0x20,ToolWindow=0x80,NoActivate=0x08000000;
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd,int index);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hwnd,int index,int value);
    readonly DispatcherTimer clock=new DispatcherTimer(DispatcherPriority.Background);
    readonly Stopwatch elapsed=new Stopwatch();
    readonly Func<bool> blocked;
    readonly Action<bool> finished;
    readonly FlockStage stage;
    bool completed;
    public FlockWindow(Window owner,Rect bounds,Point anchor,IList<BitmapSource> frames,Func<bool> blocked,Action<bool> finished) {
      Owner=owner;this.blocked=blocked;this.finished=finished;
      Title="Corbie's flock";Left=bounds.Left;Top=bounds.Top;Width=bounds.Width;Height=bounds.Height;
      WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;
      ShowInTaskbar=false;ShowActivated=false;Focusable=false;IsHitTestVisible=false;Topmost=true;
      stage=new FlockStage(frames) {Anchor=anchor};Content=stage;
      SourceInitialized+=(s,e)=> {
        var hwnd=new WindowInteropHelper(this).Handle;
        SetWindowLong(hwnd,ExStyle,GetWindowLong(hwnd,ExStyle)|Transparent|ToolWindow|NoActivate);
        HwndSource.FromHwnd(hwnd).AddHook(HitTest);
      };
      clock.Interval=TimeSpan.FromMilliseconds(33);clock.Tick+=(s,e)=>Advance(elapsed.Elapsed.TotalSeconds);
      Loaded+=(s,e)=> {elapsed.Start();clock.Start();};
      Closed+=(s,e)=> {clock.Stop();elapsed.Stop();finished(completed);};
    }
    static IntPtr HitTest(IntPtr hwnd,int message,IntPtr wp,IntPtr lp,ref bool handled) {
      if(message==0x84) {handled=true;return new IntPtr(-1);} // HTTRANSPARENT
      if(message==0x21) {handled=true;return new IntPtr(3);} // MA_NOACTIVATE
      return IntPtr.Zero;
    }
    internal void Advance(double time) {
      if(blocked()) {Close();return;}
      if(time>=FlockChoreography.Duration) {completed=true;Close();return;}
      stage.Elapsed=Math.Max(0,time);stage.InvalidateVisual();
    }
    internal bool NativeClickThrough {
      get {int style=GetWindowLong(new WindowInteropHelper(this).Handle,ExStyle);return (style&(Transparent|ToolWindow|NoActivate))==(Transparent|ToolWindow|NoActivate);}
    }
    internal bool ClockRunning {get {return clock.IsEnabled;}}
  }

  public partial class Pet {
    FlockWindow flockWindow;
    double flockPoseTime;
    bool FlockActive {get {return flockWindow!=null;}}
    bool FlockBlocked() {
      return paused || shuttingDown || octopusMode || isNightSleep || !IsVisible || WindowState==WindowState.Minimized
        || fullscreenOrPresentation || dragging || isFlung || looking || discoveryLooking || sageRequest!=null || cinemaPicker!=null
        || focusSprint.Active || breakCoach.OnBreak || breakCoach.Due || peckingArchive || isDragHovering
        || (lessonNotebook!=null && lessonNotebook.IsRecording);
    }
    List<BitmapSource> MakeFlockFrames() {
      var frames=new List<BitmapSource>();ResetIdle();
      try {
        for(int i=0;i<12;i++) {
          double phase=i*Math.PI*2/12;
          motion.X=motion.Y=bodyRest.X=bodyRest.Y=0;headTilt.Angle=-5;wingStretch.Angle=28-26*Math.Sin(phase);
          leftFoot.Y=rightFoot.Y=-7;eye.Height=11;glint.Visibility=Visibility.Visible;
          BlendArticulation("flight",i/12.0,.1);
          bird.Measure(new Size(180,180));bird.Arrange(new Rect(0,0,180,180));bird.UpdateLayout();
          var frame=new RenderTargetBitmap(180,180,96,96,PixelFormats.Pbgra32);frame.Render(bird);frame.Freeze();frames.Add(frame);
        }
      } finally {ResetIdle();}
      return frames;
    }
    Rect FlockBounds() {
      var source=PresentationSource.FromVisual(this);if(source==null || source.CompositionTarget==null)return Rect.Empty;
      var physical=Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea;
      var matrix=source.CompositionTarget.TransformFromDevice;
      var bounds=new Rect(matrix.Transform(new Point(physical.Left,physical.Top)),matrix.Transform(new Point(physical.Right,physical.Bottom)));
      if(CinemaWallsActive) {var walls=CinemaLimits;bounds.Intersect(new Rect(walls.Left,walls.Top,walls.Width+Width,walls.Height+Height));}
      return bounds;
    }
    void ToggleFlockShow() {
      if(ViewerMailBusy)CancelViewerMail(false);
      if(FlockActive) {StopFlockShow();return;}
      if(FlockBlocked()) {companion.SaySilent("The flock can wait until I'm awake, unpaused and free of another task. Even chaos needs an appointment.");return;}
      StopTikTokTake();
      var bounds=FlockBounds();if(bounds.IsEmpty || bounds.Width<300 || bounds.Height<240) {companion.SaySilent("Give us a little more room for the aerial nonsense.");return;}
      try {
        EndShowAndTell();StopCursorFollow();CancelDesktopForage();ResetPerchIntent();CancelBirdFlight(false);EndShimejiInteraction();
        StopWalking();ResetIdle();attentionBid.Cancel();scritches.Stop();ResetScreenNerves();hop=0;flockPoseTime=0;
        var frames=MakeFlockFrames();
        var window=new FlockWindow(this,bounds,new Point(Left+100-bounds.Left,Top+90-bounds.Top),frames,FlockBlocked,complete=> {
          flockWindow=null;ResetIdle();
          if(complete && !shuttingDown && !paused && !companion.Quiet)companion.Say("No, you can't keep all of them. That's my job.");
        });
        flockWindow=window;window.Show();
        if(!companion.Quiet)companion.Say("You invited an audience. I brought witnesses.");
      } catch {StopFlockShow();companion.SaySilent("The flock missed its cue. Typical bloody relatives.");}
    }
    void StopFlockShow() {var window=flockWindow;flockWindow=null;if(window!=null)window.Close();}
    bool StepFlockShow(double dt) {
      if(!FlockActive)return false;
      if(FlockBlocked()) {StopFlockShow();return false;}
      flockPoseTime+=dt;ticks+=dt;companion.Tick(dt,false,false,true);
      PoseRestBreathing(mood.Snapshot);headTilt.Angle=-8+Math.Sin(flockPoseTime*2.5)*8;
      wingStretch.Angle=22+Math.Sin(flockPoseTime*5)*17;cursorTilt.Angle=cursorLean.X=cursorLean.Y=0;
      eye.Height=flockPoseTime%3.5>3.3?2:11;glint.Visibility=eye.Height==2?Visibility.Hidden:Visibility.Visible;
      BlendArticulation("talk",flockPoseTime/2.4,dt);return true;
    }
    void BuildFlockControls(StackPanel body) {
      body.Children.Add(Section("A little theatrical nonsense",Actions(ActionButton("Summon / dismiss the flock",ToggleFlockShow)),
        new TextBlock {Text="Eleven little cousins swoop in, wheel around Corbie and scatter after 14 seconds. Run it again whenever you fancy. Click the action again to dismiss them. Pausing, bringing him home, dragging, hiding or closing him ends the show. Works offline and stays inside cinema walls.",Foreground=MutedText,TextWrapping=TextWrapping.Wrap}));
    }
    void CheckFlockShow(string preview) {
      var frames=MakeFlockFrames();if(frames.Count!=12 || !frames[0].IsFrozen)throw new Exception("Flock artwork was not cached/frozen.");
      var size=new Size(1000,600);var anchor=new Point(500,300);
      for(double t=0;t<=14;t+=.1)for(int i=0;i<FlockChoreography.Count;i++) {
        var p=FlockChoreography.Sample(i,t,size,anchor);
        if(double.IsNaN(p.Center.X) || double.IsNaN(p.Center.Y) || p.Opacity<0 || p.Opacity>1 || p.Scale<.4 || p.Scale>.6)throw new Exception("Invalid flock pose.");
        if(t>=3 && t<=10 && !new Rect(0,0,1000,600).Contains(p.Center))throw new Exception("Orbit escaped the stage.");
      }
      if(FlockChoreography.Sample(0,0,size,anchor).Opacity!=0 || FlockChoreography.Sample(0,14,size,anchor).Opacity!=0)throw new Exception("Flock did not fade at endpoints.");
      var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen()) {
        dc.DrawRectangle(ColorBrush("#F6ECD9"),null,new Rect(0,0,1000,600));
        var stage=new FlockStage(frames) {Anchor=anchor,Elapsed=6};stage.Measure(size);stage.Arrange(new Rect(size));stage.UpdateLayout();
        var image=new RenderTargetBitmap(1000,600,96,96,PixelFormats.Pbgra32);image.Render(stage);dc.DrawImage(image,new Rect(size));
        dc.DrawImage(frames[3],new Rect(410,235,180,180));
      }
      if(!string.IsNullOrEmpty(preview)) {var bmp=new RenderTargetBitmap(1000,600,96,96,PixelFormats.Pbgra32);bmp.Render(drawing);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using(var file=File.Create(preview))png.Save(file);}
      bool block=false,finished=false,natural=false;
      var window=new FlockWindow(this,new Rect(0,0,500,300),new Point(250,150),frames,()=>block,done=> {finished=true;natural=done;}) {Opacity=0};
      window.Show();if(!window.NativeClickThrough || window.ShowActivated || window.ShowInTaskbar || window.IsActive)throw new Exception("Flock could capture desktop input/focus.");
      window.Advance(6);block=true;window.Advance(6.1);
      if(!finished || natural || window.IsVisible || window.ClockRunning)throw new Exception("Interrupted flock leaked its window/timer.");
      block=false;finished=false;
      window=new FlockWindow(this,new Rect(0,0,500,300),new Point(250,150),frames,()=>block,done=> {finished=true;natural=done;}) {Opacity=0};
      window.Show();window.Advance(14);if(!finished || !natural || window.ClockRunning)throw new Exception("Flock didn't finish automatically.");
      fullscreenOrPresentation=false;paused=false;isNightSleep=false;
      ToggleFlockShow();if(!FlockActive)throw new Exception("Flock action didn't start.");flockWindow.Opacity=0;
      double x=Left,y=Top;StepFlockShow(.1);if(Left!=x || Top!=y || CanInitiate())throw new Exception("Performance moved Corbie or allowed competing antics.");
      ToggleFlockShow();if(FlockActive)throw new Exception("Repeat action didn't dismiss flock.");
      ToggleFlockShow();flockWindow.Opacity=0;TogglePause();if(FlockActive)throw new Exception("Pause kept flock alive.");TogglePause();
      ToggleFlockShow();flockWindow.Opacity=0;Home();if(FlockActive)throw new Exception("Home kept flock alive.");
      ToggleFlockShow();flockWindow.Opacity=0;Hide();if(FlockActive)throw new Exception("Hidden Corbie left the flock on screen.");Show();
      ToggleFlockShow();flockWindow.Opacity=0;SetNightSleep(true);if(FlockActive)throw new Exception("Sleep kept flock alive.");SetNightSleep(false);
      ToggleFlockShow();flockWindow.Opacity=0;SetCursorFollow(true);if(FlockActive)throw new Exception("Cursor flight kept the flock alive.");StopCursorFollow();
    }
  }
}
