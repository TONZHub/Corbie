using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CorbiePet {
  public enum TikTokGesture { Idle,Wave,Bow,SideEye,Flourish,Recoil,Exit }
  public sealed class TikTokCue {
    public TikTokGesture Gesture {get;set;}
    public string Line {get;set;}
    public double Seconds {get;set;}
    public TikTokCue Copy() {return new TikTokCue {Gesture=Gesture,Line=Line??"",Seconds=Seconds};}
    public override string ToString() {
      string text=string.IsNullOrWhiteSpace(Line)?"(silent)":Line.Replace('\r',' ').Replace('\n',' ');
      return TikTokDirector.GestureName(Gesture)+" · "+Seconds.ToString("0.#",CultureInfo.InvariantCulture)+"s · "+(text.Length>68?text.Substring(0,65)+"...":text);
    }
  }
  public sealed class TikTokDirectorData {
    public int Version {get;set;}
    public List<TikTokCue> Draft {get;set;}
    public List<TikTokCue> LastTake {get;set;}
    public bool LastCountdown {get;set;}
  }
  public static class TikTokDirector {
    public const string BowLine="Thank you. I accept applause, shiny objects, and absolutely none of your constructive criticism.";
    public static string GestureName(TikTokGesture gesture) {
      switch(gesture) {case TikTokGesture.Idle:return "Pause / talk";case TikTokGesture.Wave:return "Wave hello";case TikTokGesture.Bow:return "Dramatic bow";
        case TikTokGesture.SideEye:return "Suspicious lean";case TikTokGesture.Flourish:return "Wing flourish";case TikTokGesture.Recoil:return "Offended recoil";case TikTokGesture.Exit:return "Stage exit";default:return "Unknown";}
    }
    public static List<TikTokCue> Snapshot(IList<TikTokCue> cues) {
      if(cues==null || cues.Count<1 || cues.Count>12)throw new ArgumentException("A scene needs between 1 and 12 cues.");
      var result=new List<TikTokCue>();
      foreach(var cue in cues) {
        if(cue==null || !Enum.IsDefined(typeof(TikTokGesture),cue.Gesture) || double.IsNaN(cue.Seconds) || double.IsInfinity(cue.Seconds) || cue.Seconds<.5 || cue.Seconds>10 || (cue.Line??"").Length>280)
          throw new ArgumentException("Each cue needs a known gesture, 0.5–10 seconds and at most 280 characters.");
        result.Add(cue.Copy());
      }
      return result;
    }
    public static List<TikTokCue> Bow() {return new List<TikTokCue> {new TikTokCue {Gesture=TikTokGesture.Bow,Line=BowLine,Seconds=4}};}
    public static List<TikTokCue> Demo() {return new List<TikTokCue> {
      new TikTokCue {Gesture=TikTokGesture.Wave,Line="Alright, TikTok. Corbie here. Professional raven, amateur menace.",Seconds=5.5},
      new TikTokCue {Gesture=TikTokGesture.Idle,Line="",Seconds=1.5},
      new TikTokCue {Gesture=TikTokGesture.SideEye,Line="Before anyone asks: yes, the shiny things are mine. All of them.",Seconds=4},
      new TikTokCue {Gesture=TikTokGesture.Bow,Line=BowLine,Seconds=4},
      new TikTokCue {Gesture=TikTokGesture.Exit,Line="",Seconds=3}};}
    public static TikTokDirectorData Read(string file) {
      var fallback=new TikTokDirectorData {Version=1,Draft=Demo(),LastCountdown=true};
      if(!File.Exists(file))return fallback;
      if(new FileInfo(file).Length>65536)throw new InvalidDataException("Saved scene is too large.");
      var data=new JavaScriptSerializer().Deserialize<TikTokDirectorData>(File.ReadAllText(file));
      if(data==null || data.Version!=1)throw new InvalidDataException("Unrecognised scene format.");
      data.Draft=Snapshot(data.Draft);if(data.LastTake!=null)data.LastTake=Snapshot(data.LastTake);return data;
    }
    public static void Write(string file,TikTokDirectorData data) {
      var saved=new TikTokDirectorData {Version=1,Draft=Snapshot(data.Draft),LastTake=data.LastTake==null?null:Snapshot(data.LastTake),LastCountdown=data.LastCountdown};
      Directory.CreateDirectory(Path.GetDirectoryName(file));string temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
      try {
        File.WriteAllText(temp,new JavaScriptSerializer().Serialize(saved),new System.Text.UTF8Encoding(false));
        if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);
      } finally {if(File.Exists(temp))File.Delete(temp);}
    }
  }
  public sealed class TikTokSceneRun {
    readonly List<TikTokCue> cues;
    readonly double countdown;
    double elapsed,cueElapsed;
    public bool Active {get;private set;}
    public bool Started {get;private set;}
    public bool VisualStarted {get;private set;}
    public string Error {get;private set;}
    public int Index {get;private set;}
    public double PoseSeconds {get;private set;}
    public TikTokCue Current {get {return cues[Math.Max(0,Index)].Copy();}}
    public TikTokSceneRun(IList<TikTokCue> source,bool cue) {cues=TikTokDirector.Snapshot(source);countdown=cue?3:0;Active=true;Index=-1;}
    public bool Step(double dt,bool micBusy,bool voiceBusy,bool speaking) {
      if(!Active)return false;
      if(double.IsNaN(dt) || double.IsInfinity(dt) || dt<=0)return false;
      dt=Math.Min(.1,dt);elapsed+=dt;
      if(elapsed>=120) {Fail("Scene reached its two-minute limit.");return false;}
      if(!Started) {
        if(micBusy && elapsed>=6) {Fail("The microphone didn't release in time.");return false;}
        if(elapsed<countdown || micBusy)return false;
        Started=true;Index=0;return !string.IsNullOrWhiteSpace(Current.Line);
      }
      cueElapsed+=dt;
      if(cueElapsed>=40) {Fail("A cue's voice took too long.");return false;}
      if(!VisualStarted && (string.IsNullOrWhiteSpace(Current.Line) || speaking || !voiceBusy))VisualStarted=true;
      if(VisualStarted)PoseSeconds+=dt;
      if(PoseSeconds>=Current.Seconds && !voiceBusy) {
        if(Index==cues.Count-1) {Active=false;return false;}
        Index++;PoseSeconds=cueElapsed=0;VisualStarted=false;return !string.IsNullOrWhiteSpace(Current.Line);
      }
      return false;
    }
    void Fail(string reason) {Error=reason;Active=false;}
    public string Status {get {return !Active?Error??"Scene complete. Ready for another take.":!Started?elapsed<countdown?"Camera cue: "+Math.Max(1,(int)Math.Ceiling(countdown-elapsed))+"...":"Waiting for the microphone...":
      "Cue "+(Index+1)+" / "+cues.Count+" · "+TikTokDirector.GestureName(Current.Gesture)+(VisualStarted?"":" · preparing");}}
  }

  public partial class Pet {
    TikTokDirectorData directorData;
    TikTokSceneRun directorRun;
    int directorLastCue=-1;
    string directorNote="Build a scene, then roll a take.";
    bool directorLoading;
    ListBox directorCues;
    ComboBox directorGesture;
    TextBox directorLine;
    Slider directorHold;
    TextBlock directorInfo,directorDuration;
    Button directorRunButton,directorRepeatButton;
    static string DirectorFile {get {return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Corbie","tiktok-scenes.json");}}
    void EnsureDirector() {
      if(directorData!=null)return;
      try {directorData=testing?new TikTokDirectorData {Version=1,Draft=TikTokDirector.Demo()}:TikTokDirector.Read(DirectorFile);}
      catch {directorData=new TikTokDirectorData {Version=1,Draft=TikTokDirector.Demo()};directorNote="Couldn't load the saved scene. A fresh demo is ready; saving a change replaces that file.";}
    }
    void SaveDirector() {
      if(testing)return;
      try {TikTokDirector.Write(DirectorFile,directorData);}catch {directorNote="The scene works this session, but couldn't be saved to disk.";}
    }
    void BuildDirectorControls(StackPanel body) {
      EnsureDirector();
      directorCues=new ListBox {Height=142,Background=EggBrush,Foreground=InkBrush,Margin=new Thickness(0,8,0,10),HorizontalContentAlignment=HorizontalAlignment.Stretch};
      directorCues.SelectionChanged+=(s,e)=>LoadDirectorCue();
      directorRunButton=ActionButton("Run scene",RunDirectorDraft);directorRepeatButton=ActionButton("Repeat last take",RepeatDirectorTake);
      body.Children.Add(Section("Scene director",new TextBlock {Text="Chain up to 12 cues in a scene of up to two minutes. Lines use Corbie's installed voice; an empty line makes a silent gesture or pause. Your draft and last take are saved locally.",Foreground=MutedText,TextWrapping=TextWrapping.Wrap},
        directorCues,Actions(directorRunButton,directorRepeatButton,ActionButton("Stop the trick",()=> {CancelViewerMail(false);StopTikTokTake();StopFlockShow();})),directorInfo=Status(),
        Actions(ActionButton("Load bow scene",()=>SetDirectorDraft(TikTokDirector.Bow())),ActionButton("Load demo skit",()=>SetDirectorDraft(TikTokDirector.Demo())))));
      directorGesture=new ComboBox {MinWidth=180,Margin=new Thickness(0,5,0,8)};
      foreach(TikTokGesture gesture in Enum.GetValues(typeof(TikTokGesture)))directorGesture.Items.Add(TikTokDirector.GestureName(gesture));
      directorLine=new TextBox {AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MaxLength=280,MinHeight=82,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,5,0,8)};
      directorHold=new Slider {Minimum=.5,Maximum=10,TickFrequency=.5,IsSnapToTickEnabled=true,Margin=new Thickness(0,5,0,8)};
      directorDuration=new TextBlock {Foreground=MutedText,TextWrapping=TextWrapping.Wrap};
      directorHold.ValueChanged+=(s,e)=>directorDuration.Text="Minimum pose: "+directorHold.Value.ToString("0.#",CultureInfo.InvariantCulture)+" seconds. Spoken lines finish before the next cue.";
      body.Children.Add(Section("Edit a cue",new TextBlock {Text="Select a cue to edit it, or enter a new one and choose Add cue.",Foreground=MutedText,TextWrapping=TextWrapping.Wrap},
        new TextBlock {Text="Gesture",Foreground=InkBrush,Margin=new Thickness(0,8,0,0)},directorGesture,
        new TextBlock {Text="Line · optional · up to 280 characters",Foreground=InkBrush},directorLine,directorHold,directorDuration,
        Actions(ActionButton("Add cue",()=>ApplyDirectorCue(false)),ActionButton("Update selected",()=>ApplyDirectorCue(true)),ActionButton("Move up",()=>MoveDirectorCue(-1)),ActionButton("Move down",()=>MoveDirectorCue(1)),ActionButton("Remove selected",RemoveDirectorCue))));
      RefreshDirectorList(0);
    }
    void RefreshDirectorList(int selection) {
      if(directorCues==null)return;
      directorLoading=true;try {directorCues.Items.Clear();for(int i=0;i<directorData.Draft.Count;i++)directorCues.Items.Add((i+1)+". "+directorData.Draft[i]);directorCues.SelectedIndex=Math.Max(0,Math.Min(selection,directorCues.Items.Count-1));}finally {directorLoading=false;}
      LoadDirectorCue();RefreshDirector();
    }
    void LoadDirectorCue() {
      if(directorLoading || directorGesture==null || directorCues.SelectedIndex<0)return;
      var cue=directorData.Draft[directorCues.SelectedIndex];directorGesture.SelectedIndex=(int)cue.Gesture;directorLine.Text=cue.Line;directorHold.Value=cue.Seconds;
    }
    void SetDirectorDraft(IList<TikTokCue> cues) {directorData.Draft=TikTokDirector.Snapshot(cues);directorNote="Preset loaded into the draft.";SaveDirector();RefreshDirectorList(0);}
    void ApplyDirectorCue(bool replace) {
      int index=directorCues.SelectedIndex;if(replace && index<0)return;
      if(!replace && directorData.Draft.Count>=12) {directorNote="Twelve cues is enough for one tiny production. Remove one to add another.";RefreshDirector();return;}
      var cue=new TikTokCue {Gesture=(TikTokGesture)Math.Max(0,directorGesture.SelectedIndex),Line=directorLine.Text.Trim(),Seconds=directorHold.Value};
      TikTokDirector.Snapshot(new List<TikTokCue> {cue});
      if(replace)directorData.Draft[index]=cue;else {directorData.Draft.Insert(index<0?directorData.Draft.Count:index+1,cue);index=index<0?directorData.Draft.Count-1:index+1;}
      directorNote="Draft saved. Run scene uses these cues; Repeat last take keeps the previous script.";SaveDirector();RefreshDirectorList(index);
    }
    void MoveDirectorCue(int direction) {
      int index=directorCues.SelectedIndex,next=index+direction;if(index<0 || next<0 || next>=directorData.Draft.Count)return;
      var cue=directorData.Draft[index];directorData.Draft.RemoveAt(index);directorData.Draft.Insert(next,cue);directorNote="Cue order saved.";SaveDirector();RefreshDirectorList(next);
    }
    void RemoveDirectorCue() {
      int index=directorCues.SelectedIndex;if(index<0)return;
      if(directorData.Draft.Count==1) {directorNote="Keep at least one cue; edit this one or load a preset.";RefreshDirector();return;}
      directorData.Draft.RemoveAt(index);directorNote="Cue removed from draft.";SaveDirector();RefreshDirectorList(index);
    }
    void RememberDirectorTake(IList<TikTokCue> cues,bool countdown) {
      EnsureDirector();directorData.LastTake=TikTokDirector.Snapshot(cues);directorData.LastCountdown=countdown;SaveDirector();RefreshDirector();
    }
    void RunDirectorDraft() {EnsureDirector();StartDirectorTake(directorData.Draft,tiktokCue);}
    void RepeatDirectorTake() {EnsureDirector();if(directorData.LastTake!=null)StartDirectorTake(directorData.LastTake,directorData.LastCountdown);}
    void StartDirectorTake(IList<TikTokCue> cues,bool countdown,bool remember=true,bool hideNest=true) {
      if(ViewerMailBusy)CancelViewerMail(false);
      if(TikTokBlocked()) {tiktokLastStatus="Wake and unpause Corbie, then finish his current task or break before starting a take.";RefreshTikTok();return;}
      var script=TikTokDirector.Snapshot(cues);
      StartTikTokTake(false,false,hideNest);if(!TikTokActive)return;
      tiktokRoutine=null;directorRun=new TikTokSceneRun(script,countdown);directorLastCue=-1;
      if(remember)RememberDirectorTake(script,countdown);tiktokLastStatus=directorRun.Status;RefreshTikTok();
    }
    bool StepDirectedTake(double dt) {
      if(directorRun==null)return false;
      if(TikTokBlocked()) {StopTikTokTake();return false;}
      bool speak=directorRun.Step(dt,micRequest!=null,companion.VoiceBusy,companion.IsSpeaking);
      if(!directorRun.Active || tiktokClock.Elapsed.TotalSeconds>=120) {
        string status=tiktokClock.Elapsed.TotalSeconds>=120?"Scene reached its two-minute limit.":directorRun.Status;
        StopTikTokTake();tiktokLastStatus=status;RefreshTikTok();return false;
      }
      if(directorRun.Started && directorLastCue!=directorRun.Index) {directorLastCue=directorRun.Index;companion.Dismiss();tiktokSpoke=false;ResetIdle();bird.Opacity=1;}
      if(speak) {tiktokSpoke=true;companion.Say(directorRun.Current.Line);}
      ticks+=dt;companion.Tick(dt,false,false,true);
      PoseDirectedCue(directorRun.Current.Gesture,directorRun.VisualStarted?directorRun.PoseSeconds:-1,dt);
      tiktokLastStatus=directorRun.Status;RefreshTikTok();return true;
    }
    void PoseDirectedCue(TikTokGesture gesture,double time,double dt) {
      bird.Opacity=1;
      if(gesture==TikTokGesture.Wave) {PoseTikTokWave(time,dt);return;}
      PoseRestBreathing(mood.Snapshot);motion.X=motion.Y=0;cursorTilt.Angle=cursorLean.X=cursorLean.Y=danceTilt.Angle=0;
      headTilt.Angle=wingStretch.Angle=0;eye.Height=11;glint.Visibility=Visibility.Visible;
      BlendArticulation("idle",Math.Max(0,time)/2.4,dt);
      if(time<0)return;
      double enter=Math.Min(1,time/.45);enter=enter*enter*(3-2*enter);
      switch(gesture) {
        case TikTokGesture.Bow:
          double bow=Math.Sin(Math.PI*Math.Min(1,time/3));bow=bow*bow;
          torsoJoint.Angle+=27*bow;headTilt.Angle=22*bow;wingStretch.Angle=20*bow;eye.Height=bow>.4?4:11;break;
        case TikTokGesture.SideEye:
          headTilt.Angle=-10*enter;cursorTilt.Angle=-6*enter;neckJoint.X-=3*enter;eye.Height=11-4*enter;break;
        case TikTokGesture.Flourish:
          wingStretch.Angle=enter*(119+Math.Sin(time*5)*18);headTilt.Angle=-6*enter;break;
        case TikTokGesture.Recoil:
          double recoil=Math.Exp(-time*1.3)*Math.Sin(Math.Min(Math.PI,time*6));
          torsoJoint.Angle-=15*recoil;headTilt.Angle=-18*recoil;neckJoint.X-=4*recoil;wingStretch.Angle=48*recoil;eye.Height=11+3*recoil;break;
        case TikTokGesture.Exit:
          double fade=Math.Max(0,Math.Min(1,(time-.4)/1.4));bird.Opacity=1-fade;headTilt.Angle=-12;wingStretch.Angle=22;break;
      }
      glint.Visibility=eye.Height<5?Visibility.Hidden:Visibility.Visible;
      ToolTip="Corbie is performing a scene. Stop from the TikTok panel or tray menu.";
    }
    void RefreshDirector() {
      if(directorInfo==null)return;
      directorInfo.Text=directorRun!=null?directorRun.Status:directorNote;
      directorRunButton.IsEnabled=!TikTokActive;directorRepeatButton.IsEnabled=!TikTokActive && directorData.LastTake!=null;
    }
  }
}
