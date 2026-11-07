using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CorbiePet {
  public static class TikTokDirectorChecks {
    static void Reject(Action action) {try {action();}catch(ArgumentException) {return;}throw new Exception("Invalid scene was accepted.");}
    public static void Run() {
      var source=new List<TikTokCue> {
        new TikTokCue {Gesture=TikTokGesture.Wave,Line="First line",Seconds=.5},
        new TikTokCue {Gesture=TikTokGesture.Idle,Line="",Seconds=1},
        new TikTokCue {Gesture=TikTokGesture.Bow,Line="Last line",Seconds=.5}};
      var run=new TikTokSceneRun(source,true);source[0].Line="Edited draft";
      if(run.Current.Line!="First line")throw new Exception("Running scene shared the editable draft.");
      int lines=0,index=-2;double cueAge=0;var heard=new List<string>();
      for(int tick=0;tick<500 && run.Active;tick++) {
        if(run.Index!=index) {index=run.Index;cueAge=0;}
        bool voiced=run.Started && !string.IsNullOrEmpty(run.Current.Line);
        bool busy=voiced && cueAge<3,speaking=voiced && cueAge>=1 && cueAge<3;
        int before=run.Index;
        bool say=run.Step(.1,false,busy,speaking);
        if(tick<29 && run.Started)throw new Exception("Director ignored its camera countdown.");
        if(busy && before>=0 && before!=run.Index)throw new Exception("Next cue interrupted ongoing speech.");
        if(say) {lines++;heard.Add(run.Current.Line);}
        if(busy && !speaking && run.VisualStarted)throw new Exception("Gesture started during voice preparation.");
        cueAge+=.1;
      }
      if(run.Active || run.Error!=null || lines!=2 || heard[0]!="First line" || heard[1]!="Last line")throw new Exception("Scene didn't complete its ordered, exactly-once lines.");
      var mic=new TikTokSceneRun(source,false);for(int i=0;i<62;i++)mic.Step(.1,true,false,false);
      if(mic.Active || mic.Error==null || mic.Started)throw new Exception("Director didn't bound microphone shutdown.");
      var voice=new TikTokSceneRun(source,false);voice.Step(.1,false,false,false);for(int i=0;i<405;i++)voice.Step(.1,false,true,false);
      if(voice.Active || voice.Error==null)throw new Exception("Director didn't bound a stuck voice.");
      var longScene=new List<TikTokCue>();for(int i=0;i<12;i++)longScene.Add(new TikTokCue {Gesture=TikTokGesture.Idle,Line="",Seconds=10});
      var bounded=new TikTokSceneRun(longScene,true);for(int i=0;i<1210;i++)bounded.Step(.1,false,false,false);
      if(bounded.Active || bounded.Error==null)throw new Exception("Director didn't enforce its total duration limit.");
      var invalidTime=new TikTokSceneRun(source,false);foreach(double dt in new [] {double.NaN,double.PositiveInfinity,-1.0,0})invalidTime.Step(dt,false,false,false);
      if(invalidTime.Started)throw new Exception("Invalid frame times started a scene.");
      Reject(()=>TikTokDirector.Snapshot(new List<TikTokCue>()));
      Reject(()=>TikTokDirector.Snapshot(new [] {new TikTokCue {Gesture=(TikTokGesture)99,Seconds=1}}));
      Reject(()=>TikTokDirector.Snapshot(new [] {new TikTokCue {Gesture=TikTokGesture.Bow,Seconds=double.NaN}}));
      Reject(()=>TikTokDirector.Snapshot(new [] {new TikTokCue {Gesture=TikTokGesture.Bow,Seconds=1,Line=new string('x',281)}}));
      string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".director-check-"+Guid.NewGuid().ToString("N")+".json");
      try {
        var data=new TikTokDirectorData {Version=1,Draft=TikTokDirector.Demo(),LastTake=TikTokDirector.Bow(),LastCountdown=true};
        TikTokDirector.Write(file,data);data.Draft[0].Line="Changed";TikTokDirector.Write(file,data);
        var loaded=TikTokDirector.Read(file);
        if(loaded.Draft[0].Line!="Changed" || loaded.LastTake[0].Line!=TikTokDirector.BowLine || !loaded.LastCountdown)throw new Exception("Saved draft/last take/countdown didn't survive replacement and reload.");
        File.WriteAllText(file,"{\"Version\":2}");bool rejected=false;try {TikTokDirector.Read(file);}catch(InvalidDataException) {rejected=true;}
        if(!rejected)throw new Exception("Unknown stored format wasn't rejected.");
      } finally {if(File.Exists(file))File.Delete(file);}
    }
  }

  public partial class Pet {
    void CheckDirector(string panelPreview,string posePreview) {
      TikTokDirectorChecks.Run();OpenNestPage("TikTok");
      if(directorCues==null || directorGesture.Items.Count!=7 || directorData.Draft.Count!=5)throw new Exception("Director editor didn't build its gestures and demo.");
      tiktokHideNest=false;tiktokCue=false;companion.SetVoice(false,false);fullscreenOrPresentation=false;paused=false;isNightSleep=false;
      int count=directorData.Draft.Count;
      directorGesture.SelectedIndex=(int)TikTokGesture.Bow;directorLine.Text="A custom bird line.";directorHold.Value=3.5;ApplyDirectorCue(false);
      if(directorData.Draft.Count!=count+1 || directorData.Draft[directorCues.SelectedIndex].Line!="A custom bird line.")throw new Exception("Adding a custom cue failed.");
      int added=directorCues.SelectedIndex;MoveDirectorCue(1);if(directorCues.SelectedIndex!=added+1)throw new Exception("Cue move didn't update selection/order.");
      directorLine.Text="Edited bird line.";ApplyDirectorCue(true);if(directorData.Draft[directorCues.SelectedIndex].Line!="Edited bird line.")throw new Exception("Editing selected cue failed.");
      RemoveDirectorCue();if(directorData.Draft.Count!=count)throw new Exception("Remove cue failed.");
      SetDirectorDraft(TikTokDirector.Bow());RunDirectorDraft();
      if(!TikTokActive || directorRun==null || CanInitiate())throw new Exception("Director didn't reserve the performance state.");
      directorData.Draft[0].Line="Changed after recording began";
      if(directorRun.Current.Line!=TikTokDirector.BowLine || directorData.LastTake[0].Line!=TikTokDirector.BowLine)throw new Exception("Draft editing changed the running or repeat take.");
      double left=Left,top=Top;
      for(int i=0;i<65;i++)StepTikTokTake(.1);
      if(TikTokActive || Left!=left || Top!=top)throw new Exception("Bow didn't finish on its perch.");
      tiktokCue=true;RepeatDirectorTake();
      if(directorRun==null || directorRun.Current.Line!=TikTokDirector.BowLine)throw new Exception("Repeat used the edited draft.");
      StepTikTokTake(.1);if(!directorRun.Started)throw new Exception("Repeat didn't retain the original no-countdown setting.");StopTikTokTake();tiktokCue=false;
      var foot=leftFootVisual.TransformToAncestor(scene).Transform(new Point(90,147));
      foreach(TikTokGesture gesture in Enum.GetValues(typeof(TikTokGesture)))for(int i=0;i<50;i++) {
        PoseDirectedCue(gesture,i*.1,.033);
        if((leftFootVisual.TransformToAncestor(scene).Transform(new Point(90,147))-foot).Length>.001)throw new Exception("A scene gesture moved the planted foot.");
      }
      bird.Opacity=1;ResetIdle();
      var exit=new [] {new TikTokCue {Gesture=TikTokGesture.Exit,Line="",Seconds=5}};
      StartDirectorTake(exit,false);for(int i=0;i<25;i++)StepTikTokTake(.1);
      if(bird.Opacity>.1 || !TikTokActive)throw new Exception("Stage exit didn't fade the artwork.");
      TogglePause();if(TikTokActive || bird.Opacity!=1)throw new Exception("Pause didn't restore an exited bird.");TogglePause();
      StartDirectorTake(exit,false);for(int i=0;i<25;i++)StepTikTokTake(.1);Home();if(TikTokActive || bird.Opacity!=1)throw new Exception("Home didn't restore an exited bird.");
      StartDirectorTake(exit,false);for(int i=0;i<25;i++)StepTikTokTake(.1);Hide();if(TikTokActive || bird.Opacity!=1)throw new Exception("Hiding left a stale scene or transparent bird.");Show();
      StartDirectorTake(exit,false);for(int i=0;i<60;i++)StepTikTokTake(.1);if(TikTokActive || bird.Opacity!=1)throw new Exception("Scene completion didn't restore the exited bird.");
      SetDirectorDraft(TikTokDirector.Demo());
      if(!string.IsNullOrEmpty(posePreview))RenderDirectorPoses(posePreview);
      if(!string.IsNullOrEmpty(panelPreview)) {
        OpenNestPage("TikTok");corbiePanel.UpdateLayout();RenderDirectorPanel(panelPreview);
        var scroll=((TabItem)panelTabs.SelectedItem).Content as ScrollViewer;
        scroll.ScrollToVerticalOffset(530);corbiePanel.UpdateLayout();
        RenderDirectorPanel(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(panelPreview)),Path.GetFileNameWithoutExtension(panelPreview)+"-editor.png"));
        scroll.ScrollToTop();
      }
      StartDirectorTake(exit,false);for(int i=0;i<25;i++)StepTikTokTake(.1);Close();
      if(TikTokActive || bird.Opacity!=1)throw new Exception("Closing Corbie left the scene running.");
    }
    void RenderDirectorPanel(string path) {
      var bmp=new RenderTargetBitmap((int)corbiePanel.ActualWidth,(int)corbiePanel.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(corbiePanel);
      var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using(var file=File.Create(path))png.Save(file);
    }
    void RenderDirectorPoses(string path) {
      var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen()) {
        dc.DrawRectangle(ColorBrush("#F6ECD9"),null,new Rect(0,0,1260,360));
        for(int row=0;row<2;row++)for(int column=0;column<7;column++) {
          double time=column==(int)TikTokGesture.Recoil?(row==0?.12:.24):(row==0?.7:1.5);
          ResetIdle();facing.ScaleX=row==0?1:-1;PoseDirectedCue((TikTokGesture)column,time,.033);
          scene.Measure(new Size(180,180));scene.Arrange(new Rect(0,0,180,180));scene.UpdateLayout();
          var image=new RenderTargetBitmap(180,180,96,96,PixelFormats.Pbgra32);image.Render(scene);dc.DrawImage(image,new Rect(column*180,row*180,180,180));
        }
      }
      var bmp=new RenderTargetBitmap(1260,360,96,96,PixelFormats.Pbgra32);bmp.Render(drawing);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using(var file=File.Create(path))png.Save(file);
      facing.ScaleX=direction;bird.Opacity=1;ResetIdle();
    }
  }
}
