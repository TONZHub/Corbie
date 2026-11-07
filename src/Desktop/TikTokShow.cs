using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CorbiePet {
  public sealed class TikTokRoutine {
    public const string Introduction="Alright, TikTok. I'm Corbie: desktop raven, collector of shiny things, and this human's unpaid supervisor. I flap, I hoard, and I have opinions. Lovely to meet you. Keep your shiny things where I can see them.";
    public bool Active {get;private set;}
    public bool Started {get;private set;}
    public bool VisualStarted {get;private set;}
    public bool TimedOut {get;private set;}
    public bool Greeting {get;private set;}
    public double Elapsed {get;private set;}
    public double PoseSeconds {get;private set;}
    readonly double cue;
    public TikTokRoutine(bool greeting,bool countdown) {Greeting=greeting;cue=countdown?3:0;Active=true;}
    // Returns true exactly once, when the caller should deliver the greeting.
    public bool Step(double dt,bool micBusy,bool voiceBusy,bool speaking) {
      if(!Active)return false;
      dt=Math.Max(0,Math.Min(.1,dt));Elapsed+=dt;
      if(Elapsed>=45 || !Started && micBusy && Elapsed>=6) {TimedOut=true;Active=false;return false;}
      if(!Started) {
        if(Elapsed<cue || micBusy)return false;
        Started=true;return Greeting;
      }
      if(!VisualStarted && (!Greeting || speaking || !voiceBusy))VisualStarted=true;
      if(VisualStarted)PoseSeconds+=dt;
      if(VisualStarted && PoseSeconds>=(Greeting?8:5.5) && !voiceBusy)Active=false;
      return false;
    }
    public string Status {
      get {return !Active?(TimedOut?"Take stopped: the mic or voice took too long.":"Take complete. Ready for another."):
        !Started?Elapsed<cue?"Camera cue: "+Math.Max(1,(int)Math.Ceiling(cue-Elapsed))+"...":"Waiting for the microphone to release...":
        !VisualStarted?"Preparing Corbie's voice...":Greeting?"Greeting TikTok · waving hello":"Waving hello · silent rehearsal";}
    }
    public static void Check() {
      var intro=new TikTokRoutine(true,true);int speeches=0;
      for(int i=0;i<29;i++)if(intro.Step(.1,false,false,false))speeches++;
      if(intro.Started || speeches!=0)throw new Exception("Greeting ignored the camera cue.");
      for(int i=0;i<3;i++)if(intro.Step(.1,false,true,false))speeches++;
      if(speeches!=1 || intro.VisualStarted)throw new Exception("Greeting duplicated or waved before voice preparation.");
      for(int i=0;i<250;i++)if(intro.Step(.1,false,true,true))speeches++;
      if(!intro.Active || intro.PoseSeconds<20)throw new Exception("Long speech lost its performance state.");
      intro.Step(.1,false,false,false);if(intro.Active || speeches!=1)throw new Exception("Greeting didn't finish after audio.");
      var silent=new TikTokRoutine(false,false);for(int i=0;i<58;i++)if(silent.Step(.1,false,false,false))throw new Exception("Silent wave requested speech.");
      if(silent.Active)throw new Exception("Silent wave didn't finish.");
      var mic=new TikTokRoutine(true,false);for(int i=0;i<62;i++)mic.Step(.1,true,false,false);
      if(mic.Active || !mic.TimedOut || mic.Started)throw new Exception("Stuck microphone didn't stop before speaking.");
      var stuckVoice=new TikTokRoutine(true,false);stuckVoice.Step(.1,false,false,false);for(int i=0;i<451;i++)stuckVoice.Step(.1,false,true,false);
      if(stuckVoice.Active || !stuckVoice.TimedOut)throw new Exception("Stuck voice didn't time out.");
      var noVoice=new TikTokRoutine(true,false);for(int i=0;i<84;i++)noVoice.Step(.1,false,false,false);
      if(noVoice.Active)throw new Exception("Text-only introduction didn't finish.");
    }
  }

  public partial class Pet {
    TikTokRoutine tiktokRoutine;
    readonly Stopwatch tiktokClock=new Stopwatch();
    bool tiktokHideNest=true,tiktokCue=true,tiktokResumeMic,tiktokSpoke;
    int tiktokGeneration;
    string tiktokLastStatus="Ready for a take.";
    TextBlock tiktokStatus,tiktokVoiceStatus;
    CheckBox tiktokVoiceToggle;
    Button tiktokIntroButton,tiktokWaveButton,tiktokStopButton;
    bool TikTokActive {get {return tiktokRoutine!=null && tiktokRoutine.Active || directorRun!=null && directorRun.Active;}}
    bool TikTokBlocked() {
      return paused || shuttingDown || farewellSaid || octopusMode || isNightSleep || !IsVisible || WindowState==WindowState.Minimized
        || fullscreenOrPresentation || dragging || isFlung || looking || discoveryLooking || ViewerMailBusy || sageRequest!=null || cinemaPicker!=null
        || focusSprint.Active || breakCoach.OnBreak || breakCoach.Due || peckingArchive || isDragHovering
        || (lessonNotebook!=null && lessonNotebook.IsRecording);
    }
    void BuildTikTokTab() {
      var body=TabBody();
      body.Children.Add(Section("Corbie, meet TikTok",new TextBlock {
        Text="A little stage for a bird with far too much confidence. Choose a trick, roll your recording, and let him be insufferably charming.",Foreground=InkBrush,TextWrapping=TextWrapping.Wrap}));
      BuildViewerMailControls(body);BuildDirectorControls(body);
      tiktokIntroButton=ActionButton("Wave + introduce yourself",()=>StartTikTokTake(true));
      tiktokWaveButton=ActionButton("Just wave",()=>StartTikTokTake(false));
      tiktokStopButton=ActionButton("Stop the trick",()=> {CancelViewerMail(false);StopTikTokTake();StopFlockShow();});
      body.Children.Add(Section("Quick takes",Actions(tiktokIntroButton,tiktokWaveButton,ActionButton("Take a bow",()=>StartDirectorTake(TikTokDirector.Bow(),tiktokCue)),tiktokStopButton),tiktokStatus=Status(),
        new TextBlock {Text=TikTokRoutine.Introduction,Foreground=InkBrush,FontStyle=FontStyles.Italic,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)}));
      var hide=Toggle("Hide the Nest when a trick starts",value=>tiktokHideNest=value);hide.IsChecked=tiktokHideNest;
      var cue=Toggle("Three-second camera cue before each take",value=>tiktokCue=value);cue.IsChecked=tiktokCue;
      tiktokVoiceToggle=Toggle("Use Corbie's voice",SetVoice);
      body.Children.Add(Section("Before you roll",hide,cue,tiktokVoiceToggle,tiktokVoiceStatus=Status(),new TextBlock {
        Text="Uses your installed Corbie voice and his talking beak. With voice off, lines appear in his speech bubble. Continuous mic chat pauses for the take and resumes afterwards. Reopen the Nest by double-clicking his tray icon. Repeat last take keeps its original cues and camera countdown. These are local performances; recording is up to you.",Foreground=MutedText,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)}));
      body.Children.Add(Section("Bring the backup dancers",Actions(ActionButton("Summon / dismiss the flock",()=> {
        if(FlockActive) {StopFlockShow();return;}
        CancelViewerMail(false);StopTikTokTake();ToggleFlockShow();if(FlockActive)HideNestForTikTok();
      })),new TextBlock {Text="Eleven little cousins swoop in, circle and scatter over 14 seconds. This trick starts immediately. The bird and tray menus still have the flock action too.",Foreground=MutedText,TextWrapping=TextWrapping.Wrap}));
      AddTab("TikTok",body);RefreshTikTok();
    }
    void HideNestForTikTok() {
      if(!tiktokHideNest || corbiePanel==null)return;
      nestHiddenWithPet=false;corbiePanel.Hide();if(panelRefreshTimer!=null)panelRefreshTimer.Stop();
    }
    void StartTikTokTake(bool greeting,bool remember=true,bool hideNest=true) {
      if(ViewerMailBusy)CancelViewerMail(false);
      if(TikTokBlocked()) {tiktokLastStatus="Wake and unpause Corbie, then finish his current task or break before starting a take.";RefreshTikTok();companion.SaySilent(tiktokLastStatus);return;}
      bool resume=tiktokResumeMic || handsFreeEnabled;StopTikTokTake(false);StopFlockShow();EndShowAndTell();
      StopCursorFollow();CancelDesktopForage();ResetPerchIntent();CancelBirdFlight(false);EndShimejiInteraction();
      StopWalking();ResetIdle();attentionBid.Cancel();scritches.Stop();ResetScreenNerves();BirdTunePlayer.Stop();
      if(scritchHeart!=null)scritchHeart.Visibility=Visibility.Hidden;
      companion.Dismiss();if(micRequest!=null || handsFreeEnabled)CancelMicTalk();
      tiktokResumeMic=resume;tiktokSpoke=false;hop=0;perchArrivalTime=0;
      tiktokRoutine=new TikTokRoutine(greeting,tiktokCue);tiktokClock.Restart();
      if(remember)RememberDirectorTake(new [] {new TikTokCue {Gesture=TikTokGesture.Wave,Line=greeting?TikTokRoutine.Introduction:"",Seconds=greeting?8:5.5}},tiktokCue);
      tiktokLastStatus=tiktokRoutine.Status;RefreshTikTok();if(hideNest)HideNestForTikTok();
    }
    void StopTikTokTake(bool resumeMic=true) {
      bool existed=tiktokRoutine!=null || directorRun!=null,resume=tiktokResumeMic;
      tiktokRoutine=null;directorRun=null;directorLastCue=-1;tiktokResumeMic=false;tiktokClock.Stop();int generation=++tiktokGeneration;
      bird.Opacity=1;
      if(tiktokSpoke && companion!=null)companion.Dismiss();tiktokSpoke=false;
      if(existed) {ResetIdle();tiktokLastStatus="Ready for another take.";RefreshTikTok();}
      if(resumeMic && resume && !testing) {var pending=ResumeTikTokMic(generation);}
    }
    async Task ResumeTikTokMic(int generation) {
      try {
        // Wait for the cancelled old listening loop's finally block before a restart.
        for(int i=0;i<50 && micRequest!=null;i++)await Task.Delay(100);
        if(generation!=tiktokGeneration || TikTokActive || micRequest!=null || handsFreeEnabled || TikTokBlocked())return;
        await SetHandsFree(true);
      } catch(Exception) {if(!shuttingDown)SetMicStatus("Mic: resume from the Nest when ready");}
    }
    bool StepTikTokTake(double dt) {
      if(directorRun!=null)return StepDirectedTake(dt);
      if(!TikTokActive)return false;
      if(TikTokBlocked()) {StopTikTokTake();return false;}
      bool speak=tiktokRoutine.Step(dt,micRequest!=null,companion.VoiceBusy,companion.IsSpeaking);
      if(speak) {tiktokSpoke=true;companion.Say(TikTokRoutine.Introduction);}
      if(!tiktokRoutine.Active || tiktokClock.Elapsed.TotalSeconds>=45) {
        string status=tiktokClock.Elapsed.TotalSeconds>=45?"Take stopped: the voice took too long.":tiktokRoutine.Status;
        StopTikTokTake();tiktokLastStatus=status;RefreshTikTok();return false;
      }
      ticks+=dt;companion.Tick(dt,false,false,true);
      PoseTikTokWave(tiktokRoutine.VisualStarted?tiktokRoutine.PoseSeconds:-1,dt);
      tiktokLastStatus=tiktokRoutine.Status;RefreshTikTok();return true;
    }
    void PoseTikTokWave(double time,double dt) {
      PoseRestBreathing(mood.Snapshot);motion.X=motion.Y=0;
      cursorTilt.Angle=cursorLean.X=cursorLean.Y=danceTilt.Angle=0;
      double lift=time<0?0:Math.Min(1,time/.65)*Math.Min(1,Math.Max(0,(5.5-time)/.65));
      lift=lift*lift*(3-2*lift);
      wingStretch.Angle=lift*(126+Math.Sin(Math.Max(0,time)*8)*17);
      headTilt.Angle=-4+Math.Sin(Math.Max(0,time)*2.1)*3;
      eye.Height=time>=0 && time%3.4>3.22?2:11;glint.Visibility=eye.Height==2?Visibility.Hidden:Visibility.Visible;
      BlendArticulation("talk",Math.Max(0,time)/2.4,dt);
      ToolTip="Corbie is waving hello to TikTok. Stop the trick from the TikTok menu or Nest panel.";
    }
    void RefreshTikTok() {
      if(tiktokStatus==null)return;
      tiktokStatus.Text=TikTokActive?directorRun!=null?directorRun.Status:tiktokRoutine.Status:tiktokLastStatus;
      tiktokVoiceStatus.Text=companion.VoiceEnabled?"Voice ready · "+PiperVoice.FriendlyVoiceName(companion.VoiceId):"Voice off · lines will use a speech bubble";
      bool previous=refreshingPanel;refreshingPanel=true;
      try {tiktokVoiceToggle.IsChecked=companion.VoiceEnabled;}finally {refreshingPanel=previous;}
      tiktokIntroButton.IsEnabled=tiktokWaveButton.IsEnabled=!TikTokActive;
      tiktokStopButton.IsEnabled=TikTokActive || FlockActive;
      RefreshDirector();
      RefreshViewerMail();
    }
    void CheckTikTok(string panelPreview,string wavePreview) {
      TikTokRoutine.Check();OpenNestPage("TikTok");
      if(panelTabs.Items.Count!=14 || Convert.ToString(((TabItem)panelTabs.SelectedItem).Header)!="TikTok" || tiktokIntroButton==null || tiktokStatus==null)throw new Exception("TikTok nook didn't build/select.");
      bool trayPage=false,trayStop=false,trayFlock=false;
      foreach(System.Windows.Forms.ToolStripItem item in tray.ContextMenuStrip.Items) {trayPage|=item.Text=="TikTok tricks...";trayStop|=item.Text=="Stop TikTok trick";trayFlock|=item.Text=="Summon / dismiss the flock";}
      if(!trayPage || !trayStop || !trayFlock)throw new Exception("TikTok/flock controls were missing from the live tray menu.");
      tiktokHideNest=false;tiktokCue=false;companion.SetVoice(false,false);fullscreenOrPresentation=false;paused=false;isNightSleep=false;
      double x=Left,y=Top;StartTikTokTake(true);StepTikTokTake(.1);
      if(!TikTokActive || !tiktokSpoke || CanInitiate() || NervesAllowed(false))throw new Exception("Greeting didn't start exactly once with ambient behavior suppressed.");
      var foot=leftFootVisual.TransformToAncestor(scene).Transform(new Point(90,147));
      for(int i=0;i<45;i++) {PoseTikTokWave(i*.1,.033);if((leftFootVisual.TransformToAncestor(scene).Transform(new Point(90,147))-foot).Length>.001)throw new Exception("Wave moved a planted foot.");}
      for(int i=0;i<100;i++)StepTikTokTake(.1);
      if(TikTokActive || Left!=x || Top!=y)throw new Exception("Text introduction didn't finish on its perch.");
      StartTikTokTake(false);StepTikTokTake(.1);if(tiktokSpoke)throw new Exception("Wave rehearsal spoke.");
      TogglePause();if(TikTokActive)throw new Exception("Pause didn't cancel the take.");TogglePause();
      StartTikTokTake(false);Home();if(TikTokActive)throw new Exception("Home didn't cancel the take.");
      StartTikTokTake(false);SetNightSleep(true);if(TikTokActive)throw new Exception("Sleep didn't cancel the take.");SetNightSleep(false);
      StartTikTokTake(false);Hide();if(TikTokActive)throw new Exception("Hidden bird kept performing.");Show();
      StartTikTokTake(false);SetCursorFollow(true);if(TikTokActive)throw new Exception("Cursor flight didn't take over.");StopCursorFollow();
      tiktokHideNest=true;corbiePanel.Show();StartTikTokTake(false);
      if(corbiePanel.IsVisible || panelRefreshTimer.IsEnabled)throw new Exception("Recording option didn't hide the Nest cleanly.");StopTikTokTake();OpenNestPage("TikTok");
      if(!corbiePanel.IsVisible || !panelRefreshTimer.IsEnabled)throw new Exception("Nest didn't reopen after a take.");
      if(!string.IsNullOrEmpty(wavePreview))RenderTikTokWave(wavePreview);
      if(!string.IsNullOrEmpty(panelPreview)) {
        corbiePanel.UpdateLayout();var bmp=new RenderTargetBitmap((int)corbiePanel.ActualWidth,(int)corbiePanel.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(corbiePanel);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using(var file=File.Create(panelPreview))png.Save(file);
      }
    }
    void RenderTikTokWave(string path) {
      var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen()) {
        dc.DrawRectangle(ColorBrush("#F6ECD9"),null,new Rect(0,0,900,180));
        for(int i=0;i<5;i++) {
          ResetIdle();facing.ScaleX=i<3?1:-1;PoseTikTokWave(i==0?0:i*.4,.033);
          scene.Measure(new Size(180,180));scene.Arrange(new Rect(0,0,180,180));scene.UpdateLayout();
          var image=new RenderTargetBitmap(180,180,96,96,PixelFormats.Pbgra32);image.Render(scene);dc.DrawImage(image,new Rect(i*180,0,180,180));
        }
      }
      var bmp=new RenderTargetBitmap(900,180,96,96,PixelFormats.Pbgra32);bmp.Render(drawing);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using(var file=File.Create(path))png.Save(file);
      facing.ScaleX=direction;ResetIdle();
    }
  }
}
