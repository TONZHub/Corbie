using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ellipse=System.Windows.Shapes.Ellipse;
using System.Windows.Interop;
using Drawing=System.Drawing;
using Forms=System.Windows.Forms;

namespace CorbiePet {
  public sealed class ScreenMotionSignal {
    public double Horizontal,Vertical,Strength,X,Seconds;
    public bool Busy;
  }
  // Coarse, bounded translation matching. This front end is a designed adapter
  // for the imported circuit, not an emulation of a fly retina or text reader.
  public sealed class ScreenMotionSense {
    byte[] previous;
    string scope;
    double lastSeconds,budget,restUntil;
    ScreenMotionSignal latest;
    public void Reset() { previous=null; scope=null; latest=null; budget=restUntil=lastSeconds=0; }
    public ScreenMotionSignal Observe(byte[] pixels,string view,double seconds) {
      if(pixels==null || pixels.Length!=64*36 || double.IsNaN(seconds) || double.IsInfinity(seconds)) throw new ArgumentException("Expected a 64 by 36 screen image.");
      var signal=new ScreenMotionSignal { Seconds=seconds };
      double age=seconds-lastSeconds;
      if(previous==null || view!=scope || age<0.2 || age>0.7) { previous=(byte[])pixels.Clone(); scope=view; lastSeconds=seconds; latest=signal; budget=0; return signal; }
      double mean=0; for(int i=0;i<pixels.Length;i++) mean+=pixels[i]-previous[i]; mean/=pixels.Length;
      int changed=0,minX=64,minY=36,maxX=0,maxY=0; double centerX=0;
      for(int y=0;y<36;y++) for(int x=0;x<64;x++) {
        int i=y*64+x; if(Math.Abs(pixels[i]-previous[i]-mean)<=18) continue;
        changed++; centerX+=x+0.5; minX=Math.Min(minX,x); maxX=Math.Max(maxX,x); minY=Math.Min(minY,y); maxY=Math.Max(maxY,y);
      }
      double ratio=changed/(64.0*36);
      bool broad=Math.Abs(mean)>25 || ratio>0.3;
      if(seconds<restUntil) signal.Busy=true;
      else if(broad) { signal.Busy=true; budget+=age; }
      else if(changed>=8) {
        minX=Math.Max(3,minX-3); maxX=Math.Min(60,maxX+3); minY=Math.Max(3,minY-3); maxY=Math.Min(32,maxY+3);
        double baseline=0,best=double.MaxValue; int bestX=0,bestY=0;
        for(int dy=-2;dy<=2;dy++) for(int dx=-2;dx<=2;dx++) {
          double error=0;
          for(int y=minY;y<=maxY;y++) for(int x=minX;x<=maxX;x++) error+=Math.Abs(pixels[y*64+x]-previous[(y-dy)*64+x-dx]-mean);
          if(dx==0 && dy==0) baseline=error;
          if(error<best-0.001 || Math.Abs(error-best)<0.001 && dx*dx+dy*dy<bestX*bestX+bestY*bestY) { best=error; bestX=dx; bestY=dy; }
        }
        if(baseline>0 && best<baseline*0.7 && (bestX!=0 || bestY!=0)) {
          signal.Horizontal=bestX/2.0; signal.Vertical=bestY/2.0;
          signal.Strength=Math.Min(0.18,ratio*6); signal.X=(centerX/changed/64-0.5)*640; budget+=age;
        } else budget=Math.Max(0,budget-age);
      } else budget=Math.Max(0,budget-age*2);
      if(budget>=3) { restUntil=seconds+8; budget=0; signal.Horizontal=signal.Vertical=signal.Strength=0; signal.Busy=true; }
      previous=(byte[])pixels.Clone(); scope=view; lastSeconds=seconds; latest=signal; return signal;
    }
    public bool Read(double seconds,out ScreenMotionSignal signal) {
      signal=latest; return latest!=null && seconds>=latest.Seconds && seconds-latest.Seconds<=0.6 && !latest.Busy && latest.Strength>0;
    }
    public bool Peek(double seconds,out ScreenMotionSignal signal) {
      signal=latest; return latest!=null && seconds>=latest.Seconds && seconds-latest.Seconds<=0.6;
    }
    public bool Resting(double seconds) { return seconds<restUntil || latest!=null && latest.Busy; }
    static byte[] Square(int x,int y) { var a=new byte[64*36]; for(int yy=y;yy<y+6;yy++) for(int xx=x;xx<x+6;xx++) a[yy*64+xx]=220; return a; }
    public static void Check() {
      var sense=new ScreenMotionSense();
      if(sense.Observe(Square(14,12),"one",1).Strength!=0) throw new Exception("Initial screen created motion.");
      var move=sense.Observe(Square(16,12),"one",1.35);
      if(move.Horizontal<=0 || move.Vertical!=0 || move.Strength<=0 || move.Strength>0.18) throw new Exception("Coarse screen translation failed.");
      if(sense.Observe(Square(16,12),"one",1.7).Strength!=0) throw new Exception("Still screen created motion.");
      if(sense.Observe(Square(18,12),"changed scope",2.05).Strength!=0) throw new Exception("Camera movement created false motion.");
      var flash=new byte[64*36]; for(int i=0;i<flash.Length;i++) flash[i]=255;
      if(sense.Observe(flash,"changed scope",2.4).Strength!=0) throw new Exception("Scene cut entered brain.");
      ScreenMotionSignal read; if(sense.Read(3.2,out read)) throw new Exception("Stale screen retained input.");
      sense.Reset(); bool rested=false;
      for(int i=0;i<30;i++) { var s=sense.Observe(Square(12+i%2*2,12),"video",10+i*0.35); if(i>8 && s.Busy && s.Strength==0) rested=true; }
      if(!rested) throw new Exception("Sustained screen motion never rested.");
      sense.Reset(); if(sense.Read(30,out read)) throw new Exception("Reset retained screen pixels.");
    }
  }
  public static class NerveScreenCapture {
    public static byte[] Capture(Drawing.Rectangle bounds,Drawing.Rectangle[] masks) {
      if(bounds.Width<1 || bounds.Height<1 || bounds.Width>640 || bounds.Height>360) throw new ArgumentException("Invalid local field of view.");
      using(var image=new Drawing.Bitmap(bounds.Width,bounds.Height,Drawing.Imaging.PixelFormat.Format24bppRgb)) {
        using(var graphics=Drawing.Graphics.FromImage(image)) graphics.CopyFromScreen(bounds.Location,Drawing.Point.Empty,bounds.Size);
        return MaskAndReduce(image,bounds,masks);
      }
    }
    public static void CheckLive() {
      var area=Forms.Screen.PrimaryScreen.Bounds;
      int width=Math.Min(640,area.Width),height=Math.Min(360,area.Height);
      var bounds=new Drawing.Rectangle(area.Left+(area.Width-width)/2,area.Top+(area.Height-height)/2,width,height);
      var pixels=Capture(bounds,new Drawing.Rectangle[0]);
      if(pixels==null || pixels.Length!=2304) throw new Exception("Native capture did not produce the local retina.");
      Array.Clear(pixels,0,pixels.Length);
    }
    public static byte[] MaskAndReduce(Drawing.Bitmap image,Drawing.Rectangle bounds,Drawing.Rectangle[] masks) {
      using(var graphics=Drawing.Graphics.FromImage(image)) foreach(var rectangle in masks) {
        var overlap=Drawing.Rectangle.Intersect(bounds,rectangle);
        if(overlap.Width>0 && overlap.Height>0) graphics.FillRectangle(Drawing.Brushes.Black,overlap.X-bounds.X,overlap.Y-bounds.Y,overlap.Width,overlap.Height);
      }
      return ScreenVision.DownscaleToThumbnail(image,64,36);
    }
    public static void Check() {
      using(var image=new Drawing.Bitmap(640,360)) {
        using(var graphics=Drawing.Graphics.FromImage(image)) graphics.Clear(Drawing.Color.White);
        var pixels=MaskAndReduce(image,new Drawing.Rectangle(100,200,640,360),new[]{new Drawing.Rectangle(100,200,320,360)});
        if(pixels.Length!=2304 || pixels[10*64+10]!=0 || pixels[10*64+50]<240) throw new Exception("Pet/panel masking failed.");
      }
    }
  }
  public partial class Pet {
    readonly ScreenMotionSense screenMotionSense=new ScreenMotionSense();
    bool screenNervesEnabled,screenNervesCapturing,screenNervesObserving;
    int screenNervesGeneration;
    double screenNervesWait;
    double screenNervesLastCapture=-1;
    string screenNervesError;
    CheckBox screenNervesToggle;
    TextBlock screenNervesStatus;
    Ellipse screenNervesLight;
    string ScreenNervesPreference { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"corbie-screen-nerves-enabled.txt"); } }
    void InitializeScreenNerves() {
      if(!testing) try { screenNervesEnabled=File.Exists(ScreenNervesPreference) && File.ReadAllText(ScreenNervesPreference).Trim()=="enabled"; } catch { screenNervesError="Screen sensing setting could not be read."; }
      screenNervesLight=new Ellipse { Width=6,Height=6,Fill=Brushes.Turquoise,Opacity=0.8,Visibility=Visibility.Hidden,ToolTip="Local screen motion senses active. No text reading." };
      Canvas.SetLeft(screenNervesLight,145); Canvas.SetTop(screenNervesLight,71); scene.Children.Add(screenNervesLight);
    }
    void ResetScreenNerves() { screenNervesGeneration++; screenMotionSense.Reset(); screenNervesLastCapture=-1; screenNervesObserving=false; if(screenNervesLight!=null) screenNervesLight.Visibility=Visibility.Hidden; }
    void SetScreenNerves(bool enabled) {
      if(refreshingPanel) return;
      if(!testing) try { File.WriteAllText(ScreenNervesPreference,enabled?"enabled":"disabled"); } catch { screenNervesError="Couldn't save screen sensing setting."; RefreshScreenNerves(); return; }
      screenNervesEnabled=enabled; ResetScreenNerves(); ResetPerchIntent(); screenNervesError=null; screenNervesWait=0; RefreshScreenNerves();
    }
    bool ScreenNervesAllowed() {
      return screenNervesEnabled && NervesAllowed(wasDancing) && !walking && !birdFlight.Active && hop<=0 && nervesStartleTimer<=0 && !shimejiInteracting && !HabitatHasInput() && !ShimejiHasMotionInput();
    }
    bool ScreenNervesView(out Drawing.Rectangle bounds,out Drawing.Rectangle[] masks,out string scope) {
      bounds=Drawing.Rectangle.Empty; masks=null; scope=null;
      IntPtr handle=new WindowInteropHelper(this).Handle; var source=PresentationSource.FromVisual(this); if(handle==IntPtr.Zero || source==null) return false;
      var head=scene.PointToScreen(new Point(112,70)); var monitor=Forms.Screen.FromHandle(handle).Bounds;
      int width=Math.Min(640,monitor.Width),height=Math.Min(360,monitor.Height);
      int x=Math.Max(monitor.Left,Math.Min(monitor.Right-width,(int)head.X-width/2));
      int y=Math.Max(monitor.Top,Math.Min(monitor.Bottom-height,(int)head.Y-height/2));
      bounds=new Drawing.Rectangle(x,y,width,height); var hidden=new List<Drawing.Rectangle>();
      var handles=new[]{handle,companion.Handle,corbiePanel==null?IntPtr.Zero:new WindowInteropHelper(corbiePanel).Handle};
      scope=bounds.ToString();
      foreach(var window in handles) {
        NativeRect rect; if(window==IntPtr.Zero || !IsWindowVisible(window) || !GetWindowRect(window,out rect)) continue;
        var mask=new Drawing.Rectangle(rect.Left-8,rect.Top-8,rect.Right-rect.Left+16,rect.Bottom-rect.Top+16); hidden.Add(mask); scope+=mask.ToString();
      }
      masks=hidden.ToArray(); return true;
    }
    void StepScreenNervesCapture(double dt) {
      bool allowed=ScreenNervesAllowed(); screenNervesObserving=allowed;
      bool fresh=screenNervesLastCapture>=0 && animationClock.Elapsed.TotalSeconds-screenNervesLastCapture<1.1;
      if(screenNervesLight!=null) {
        screenNervesLight.Visibility=allowed?Visibility.Visible:Visibility.Hidden;
        screenNervesLight.Fill=fresh?Brushes.Turquoise:Brushes.DarkGoldenrod;
        screenNervesLight.ToolTip=fresh?"Local screen motion senses active. No text reading.":"Waiting for local screen pixels.";
      }
      if(!allowed) { screenMotionSense.Reset(); screenNervesLastCapture=-1; screenNervesGeneration++; return; }
      screenNervesWait-=dt;
      if(testing || screenNervesCapturing || screenNervesWait>0) return;
      Drawing.Rectangle bounds; Drawing.Rectangle[] masks; string scope;
      if(!ScreenNervesView(out bounds,out masks,out scope)) return;
      screenNervesWait=0.35; var work=CaptureScreenNerves(bounds,masks,scope,screenNervesGeneration);
    }
    async Task CaptureScreenNerves(Drawing.Rectangle bounds,Drawing.Rectangle[] masks,string scope,int generation) {
      screenNervesCapturing=true;
      try {
        byte[] pixels=await Task.Run(()=>NerveScreenCapture.Capture(bounds,masks));
        Drawing.Rectangle current; Drawing.Rectangle[] currentMasks; string currentScope;
        if(!shuttingDown && generation==screenNervesGeneration && ScreenNervesAllowed() && ScreenNervesView(out current,out currentMasks,out currentScope) && currentScope==scope) {
          screenNervesLastCapture=animationClock.Elapsed.TotalSeconds;
          screenMotionSense.Observe(pixels,scope,screenNervesLastCapture); screenNervesError=null;
        }
      } catch { if(generation==screenNervesGeneration) { screenNervesError="Local screen capture unavailable. Retrying shortly."; screenNervesWait=10; screenMotionSense.Reset(); } }
      finally { screenNervesCapturing=false; }
    }
    bool TryScreenNervesInput(double seconds,out ScreenMotionSignal signal) {
      signal=null; return nervesMayReact && screenNervesEnabled && ScreenNervesAllowed() && screenMotionSense.Read(seconds,out signal);
    }
    void BuildScreenNervesControls(StackPanel body) {
      screenNervesToggle=Toggle("See nearby screen motion (local, low detail)",SetScreenNerves);
      body.Children.Add(Section("Little screen senses",screenNervesToggle,screenNervesStatus=Status(),
        new TextBlock { Text="Off by default. With the fly nervous system on, a small region around his perch becomes a 64 × 36 grayscale image about three times a second. A turquoise dot means sensing is active. Motion encourages curiosity, never screen-driven startle hops. Broad flashes and scene cuts are ignored; sustained motion gets an eight-second rest. Corbie, his speech bubble and the Nest are masked. Images stay briefly in memory: no saving, text reading or model/cloud requests. Habitat, nearby mascots, rest and conversation take priority.",TextWrapping=TextWrapping.Wrap,Foreground=MutedText }));
      RefreshScreenNerves();
    }
    void RefreshScreenNerves() {
      if(screenNervesStatus==null) return;
      bool prior=refreshingPanel; refreshingPanel=true; try { screenNervesToggle.IsChecked=screenNervesEnabled; } finally { refreshingPanel=prior; }
      ScreenMotionSignal signal;
      screenNervesStatus.Text=!screenNervesEnabled?"Screen motion senses off":!nervesEnabled?"Enable the fly nervous system first":screenNervesError!=null?screenNervesError:!screenNervesObserving?"Resting · no screen sampling":screenNervesLastCapture<0 || animationClock.Elapsed.TotalSeconds-screenNervesLastCapture>=1.1?"Waiting for local screen pixels":screenMotionSense.Resting(animationClock.Elapsed.TotalSeconds)?"Busy scene · taking a sensory rest":screenMotionSense.Read(animationClock.Elapsed.TotalSeconds,out signal)?"Noticed local screen motion":"Watching quietly · local pixels only";
    }
    void CheckScreenNerves() {
      ScreenMotionSense.Check(); NerveScreenCapture.Check(); SetNerves(true); SetScreenNerves(true); nervesMayReact=true;
      var now=animationClock.Elapsed.TotalSeconds;
      var a=new byte[2304]; var b=new byte[2304];
      for(int y=12;y<18;y++) for(int x=14;x<20;x++) { a[y*64+x]=220; b[y*64+x+2]=220; }
      screenMotionSense.Observe(a,"fixture",now-0.35); screenMotionSense.Observe(b,"fixture",now);
      ScreenMotionSignal signal;
      if(!TryScreenNervesInput(now,out signal)) throw new Exception("Desktop screen adapter rejected fresh pixels.");
      double peak=0; for(int i=0;i<10;i++) { flyNerves.Step(0.033,signal.Horizontal,signal.Vertical,signal.Strength); peak=Math.Max(peak,flyNerves.Attention); }
      if(peak<0.025 || hop>0 || cursorStartle.Wariness!=0) throw new Exception("Screen motion failed gentle neural response or created a scare.");
      paused=true; if(TryScreenNervesInput(now,out signal)) throw new Exception("Screen bypassed pause."); paused=false;
      isNightSleep=true; if(TryScreenNervesInput(now,out signal)) throw new Exception("Screen bypassed sleep."); isNightSleep=false;
      walking=true; if(TryScreenNervesInput(now,out signal)) throw new Exception("Screen sensed Corbie's own walking."); walking=false;
      if(TryScreenNervesInput(now+1,out signal)) throw new Exception("Screen used stale pixels.");
      ResetNerveAttention(); if(TryScreenNervesInput(now,out signal)) throw new Exception("Attention reset retained screen image.");
      SetScreenNerves(false); if(TryScreenNervesInput(now,out signal)) throw new Exception("Disabled screen senses retained input.");
      SetNerves(false); CheckControlPanel(); if(screenNervesStatus==null) throw new Exception("Missing screen sensory controls.");
    }
  }
}
