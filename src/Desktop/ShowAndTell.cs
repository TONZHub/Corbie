using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CorbiePet {
  public partial class Pet {
    Border showTellFrame;
    Image showTellImage;
    readonly RotateTransform showTellTilt=new RotateTransform(0,24,30);
    readonly ScaleTransform showTellMirror=new ScaleTransform(1,1,24,30);
    double showTellElapsed;
    bool showTellActive;
    bool showTellFromMic;
    bool ShowTellActive {get {return showTellActive;}}
    void BuildShowAndTell() {
      showTellImage=new Image {Stretch=Stretch.Uniform,Margin=new Thickness(2)};
      var transforms=new TransformGroup();transforms.Children.Add(showTellMirror);transforms.Children.Add(showTellTilt);
      showTellFrame=new Border {Width=48,Height=60,CornerRadius=new CornerRadius(2),BorderThickness=new Thickness(3),BorderBrush=ColorBrush("#B48B4B"),Background=ColorBrush("#F5ECD9"),Child=showTellImage,
        Visibility=Visibility.Hidden,IsHitTestVisible=false,RenderTransform=transforms,Effect=new System.Windows.Media.Effects.DropShadowEffect {BlurRadius=5,ShadowDepth=2,Opacity=.35}};
      Canvas.SetLeft(showTellFrame,128);Canvas.SetTop(showTellFrame,86);Panel.SetZIndex(showTellFrame,100);scene.Children.Add(showTellFrame);
    }
    bool PresentationBlocked(bool allowMic=false) {
      return paused || shuttingDown || !IsVisible || WindowState==WindowState.Minimized || octopusMode || isNightSleep
        || roost!=null && roost.Data.SkyRoostEnabled && SkyWeather.GetPhase(DateTime.Now.Hour)==CircadianPhase.DeepNight
        || ViewerMailBusy || fullscreenOrPresentation || dragging || isFlung || looking || discoveryLooking || sageRequest!=null || micRequest!=null && !allowMic
        || cursorFollowEnabled || birdFlight.Active || focusSprint.Active || breakCoach.OnBreak || breakCoach.Due
        || peckingArchive || isDragHovering || notificationEdge.HuntActive || eyeRestCoach.GlideActive || eyeRestCoach.PesterBossActive || shimejiInteracting || scritches.Engaged;
    }
    bool BeginShowAndTell(BitmapSource preview,bool fromMic=false) {
      if(preview==null || showTellFrame==null || PresentationBlocked(fromMic))return false;
      StopTikTokTake();
      EndShowAndTell();StopWalking();ResetIdle();hop=0;perchArrivalTime=0;
      attentionBid.Cancel();showTellElapsed=0;showTellActive=true;showTellFromMic=fromMic;showTellImage.Source=preview;showTellFrame.Visibility=Visibility.Visible;showTellFrame.Opacity=0;
      return true;
    }
    void EndShowAndTell() {
      bool wasActive=showTellActive;showTellActive=false;showTellFromMic=false;showTellElapsed=0;
      if(showTellFrame!=null) {showTellFrame.Visibility=Visibility.Hidden;showTellImage.Source=null;}
      if(wasActive) {hop=0;ResetIdle();}
    }
    bool StepShowAndTell(double dt) {
      if(!showTellActive)return false;
      if(PresentationBlocked(showTellFromMic) || ContextMenu.IsOpen || tray.ContextMenuStrip.Visible) {EndShowAndTell();return false;}
      showTellElapsed+=Math.Max(0,Math.Min(.1,dt));
      if(showTellElapsed>=7) {EndShowAndTell();return false;}
      ticks+=dt;
      companion.Tick(dt,false,octopusMode,true);
      PoseShowAndTell(showTellElapsed,dt);return true;
    }
    void PoseShowAndTell(double elapsed,double dt) {
      double entrance=Math.Min(1,elapsed/.75);entrance=entrance*entrance*(3-2*entrance);
      double exit=Math.Min(1,(7-elapsed)/.5);
      showTellFrame.Opacity=Math.Max(0,Math.Min(entrance,exit));
      Canvas.SetLeft(showTellFrame,128);Canvas.SetTop(showTellFrame,86+(1-entrance)*12+Math.Sin(elapsed*2)*.8);
      // The scene mirrors with Corbie. Counter-mirror the picture so text/art
      // stays the right way round while the frame changes sides with the bird.
      showTellMirror.ScaleX=facing.ScaleX;showTellTilt.Angle=(-5+Math.Sin(elapsed*1.7)*2)*facing.ScaleX;
      PoseRestBreathing(mood.Snapshot);
      motion.Y=elapsed<.6?-Math.Sin(elapsed/.6*Math.PI)*9:0;
      headTilt.Angle=entrance*(7+Math.Sin(elapsed*1.5)*7);
      wingStretch.Angle=-entrance*48;cursorTilt.Angle=0;cursorLean.X=cursorLean.Y=0;
      eye.Height=elapsed%3.6>3.4?2:11;glint.Visibility=eye.Height==2?Visibility.Hidden:Visibility.Visible;
      BlendArticulation("idle",elapsed/2.4,dt);
      ToolTip="Corbie is showing you a find. The project scrapbook is in Nest > Discoveries.";
    }
    void RenderShowTellPreview(BitmapSource source,string path) {
      var drawing=new DrawingVisual();
      using(var dc=drawing.RenderOpen()) {
        dc.DrawRectangle(ColorBrush("#F5ECD9"),null,new Rect(0,0,720,180));
        for(int i=0;i<4;i++) {
          ResetIdle();showTellImage.Source=source;showTellFrame.Visibility=Visibility.Visible;facing.ScaleX=i<2?1:-1;PoseShowAndTell(i%2==0?.3:2.2,.033);
          scene.Measure(new Size(180,180));scene.Arrange(new Rect(0,0,180,180));scene.UpdateLayout();
          var frame=new RenderTargetBitmap(180,180,96,96,PixelFormats.Pbgra32);frame.Render(scene);dc.DrawImage(frame,new Rect(i*180,0,180,180));
        }
      }
      var sheet=new RenderTargetBitmap(720,180,96,96,PixelFormats.Pbgra32);sheet.Render(drawing);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(sheet));using(var stream=File.Create(path))encoder.Save(stream);
      showTellActive=true;EndShowAndTell();facing.ScaleX=direction;
    }
  }
}
