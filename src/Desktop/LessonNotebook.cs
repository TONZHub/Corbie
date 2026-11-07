using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NAudio.Wave;

namespace CorbiePet {
  // A local classroom notebook. Audio is never discarded when transcription fails.
  public sealed class LessonNotebook : Window {
    sealed class LessonEntry {
      public string Folder;
      public string Title;
      public override string ToString() { return Title; }
    }
    public static readonly string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Corbie","Lessons");
    readonly ListBox lessons=new ListBox { MinWidth=190,Margin=new Thickness(0,0,14,0) };
    readonly TextBox title=new TextBox { MaxLength=160,Margin=new Thickness(0,0,0,8) };
    readonly TextBox vocabulary=new TextBox { MaxLength=500,Margin=new Thickness(0,0,0,8),ToolTip="Teacher names and science terms for local Whisper" };
    readonly TextBox transcript=new TextBox { AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MinHeight=180,Margin=new Thickness(0,0,0,8) };
    readonly TextBox notes=new TextBox { AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MinHeight=160,Margin=new Thickness(0,0,0,8) };
    readonly TextBlock status=new TextBlock { TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0),Foreground=Brushes.SaddleBrown };
    readonly Button record=new Button { Content="Record lesson",MinWidth=120 };
    readonly Button pause=new Button { Content="Pause",MinWidth=80 };
    readonly Button finish=new Button { Content="Finish recording",MinWidth=120 };
    readonly Button transcribe=new Button { Content="Transcribe locally",MinWidth=130 };
    readonly Button cancelTranscribe=new Button { Content="Cancel transcription",MinWidth=130 };
    readonly Button summarize=new Button { Content="Summarize locally",MinWidth=130 };
    readonly Button cancelSummary=new Button { Content="Cancel summary",MinWidth=110 };
    readonly DispatcherTimer saveTimer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(650) };
    readonly DispatcherTimer clock=new DispatcherTimer { Interval=TimeSpan.FromSeconds(1) };
    readonly object audioGate=new object();
    readonly string storageRoot;
    readonly Func<bool> micBusy;
    readonly Action recordingStarted;
    WaveInEvent input;
    WaveFileWriter writer;
    TaskCompletionSource<bool> stopped;
    CancellationTokenSource transcriptionRequest;
    CancellationTokenSource summaryRequest;
    LessonEntry current;
    bool loading, recording, paused, closingAfterFinish;
    DateTime recordedAt;
    TimeSpan elapsedBeforePause;
    public bool IsRecording { get { return recording; } }
    public LessonNotebook(Func<bool> micBusy,Action recordingStarted,string storageRoot=null) {
      this.micBusy=micBusy;this.recordingStarted=recordingStarted;this.storageRoot=storageRoot??Root;
      Title="Corbie's lesson notebook"; Width=920; Height=760; MinWidth=700; MinHeight=570;
      Background=new SolidColorBrush(Color.FromRgb(0xF6,0xEC,0xD9));
      WindowStartupLocation=WindowStartupLocation.CenterScreen; ShowInTaskbar=true;
      Directory.CreateDirectory(this.storageRoot);
      var root=new Grid { Margin=new Thickness(18) };
      root.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(210) });
      root.ColumnDefinitions.Add(new ColumnDefinition());
      var left=new DockPanel();
      var newLesson=Button("New lesson",()=>NewLesson());
      DockPanel.SetDock(newLesson,Dock.Top);left.Children.Add(newLesson);
      left.Children.Add(lessons);root.Children.Add(left);
      var right=new StackPanel();
      right.Children.Add(Heading("LESSON NOTEBOOK"));
      right.Children.Add(new TextBlock { Text="Original audio and notes stay on this PC. Check scientific words and numbers against the recording.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12),Foreground=Brushes.DimGray });
      right.Children.Add(Label("Lesson title"));right.Children.Add(title);
      right.Children.Add(Label("Names and science terms"));right.Children.Add(vocabulary);
      var controls=new WrapPanel { Margin=new Thickness(0,0,0,5) };
      foreach(var button in new [] {record,pause,finish,transcribe,cancelTranscribe,summarize,cancelSummary}) { button.Margin=new Thickness(0,0,7,7); button.Padding=new Thickness(8,6,8,6); controls.Children.Add(button); }
      right.Children.Add(controls);
      right.Children.Add(Label("Editable transcript"));right.Children.Add(transcript);
      right.Children.Add(Label("Study notes"));right.Children.Add(notes);
      var fileControls=new WrapPanel();
      fileControls.Children.Add(Button("Play recording",PlayRecording));
      fileControls.Children.Add(Button("Open lesson folder",OpenFolder));
      right.Children.Add(fileControls);right.Children.Add(status);
      var scroll=new ScrollViewer { Content=right,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
      Grid.SetColumn(scroll,1);root.Children.Add(scroll);Content=root;
      lessons.SelectionChanged+=(s,e)=> { if(!recording && transcriptionRequest==null && summaryRequest==null) LoadSelected(); };
      title.TextChanged+=(s,e)=>QueueSave();
      vocabulary.TextChanged+=(s,e)=>QueueSave();
      transcript.TextChanged+=(s,e)=>QueueSave();
      notes.TextChanged+=(s,e)=>QueueSave();
      saveTimer.Tick+=(s,e)=> { saveTimer.Stop(); SaveCurrent(); };
      clock.Tick+=(s,e)=> { if(recording) status.Text=(paused?"Paused":"Recording")+" · "+Duration(); };
      record.Click+=(s,e)=>StartRecording();
      pause.Click+=(s,e)=>TogglePause();
      finish.Click+=async (s,e)=>await FinishRecording();
      transcribe.Click+=async (s,e)=>await TranscribeRecording();
      cancelTranscribe.Click+=(s,e)=> { if(transcriptionRequest!=null) transcriptionRequest.Cancel(); };
      summarize.Click+=async (s,e)=>await SummarizeLesson();
      cancelSummary.Click+=(s,e)=> { if(summaryRequest!=null) summaryRequest.Cancel(); };
      Closing+=async (s,e)=> {
        if(recording && !closingAfterFinish) { e.Cancel=true; closingAfterFinish=true; await FinishRecording(); Close(); return; }
        saveTimer.Stop(); SaveCurrent(); if(transcriptionRequest!=null) transcriptionRequest.Cancel(); if(summaryRequest!=null) summaryRequest.Cancel();
      };
      RefreshLessons(); UpdateButtons();
    }
    static TextBlock Heading(string value) { return new TextBlock { Text=value,FontSize=19,FontWeight=FontWeights.Bold,Foreground=Brushes.SaddleBrown,Margin=new Thickness(0,0,0,8) }; }
    static TextBlock Label(string value) { return new TextBlock { Text=value,FontSize=12,FontWeight=FontWeights.Bold,Foreground=Brushes.SaddleBrown,Margin=new Thickness(0,0,0,4) }; }
    static Button Button(string label,Action action) {
      var button=new Button { Content=label,Padding=new Thickness(9,6,9,6),Margin=new Thickness(0,0,8,8),Background=Brushes.SaddleBrown,Foreground=Brushes.White };
      button.Click+=(s,e)=>action();return button;
    }
    string PathFor(string name) { return current==null?null:Path.Combine(current.Folder,name); }
    void RefreshLessons(string select=null) {
      lessons.Items.Clear();
      foreach(string folder in Directory.GetDirectories(storageRoot).OrderByDescending(x=>x)) {
        string titlePath=Path.Combine(folder,"title.txt");
        try { lessons.Items.Add(new LessonEntry { Folder=folder,Title=File.Exists(titlePath)?File.ReadAllText(titlePath).Trim():Path.GetFileName(folder) }); }
        catch(IOException) { }
      }
      if(select!=null) lessons.SelectedItem=lessons.Items.Cast<LessonEntry>().FirstOrDefault(x=>x.Folder==select);
      else if(lessons.Items.Count>0) lessons.SelectedIndex=0;
    }
    void NewLesson() {
      if(recording || transcriptionRequest!=null || summaryRequest!=null) { status.Text="Finish the current lesson task first."; return; }
      try {
        SaveCurrent();
        string folder=Path.Combine(storageRoot,DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder,"title.txt"),"Untitled lesson",Encoding.UTF8);
        RefreshLessons(folder);title.Focus();title.SelectAll();
        status.Text="New lesson ready. Record a class or type notes.";
      } catch(Exception ex) { status.Text="Could not create lesson: "+ex.Message; }
    }
    void LoadSelected() {
      SaveCurrent();
      current=lessons.SelectedItem as LessonEntry; loading=true;
      string loadError=null;
      try {
        title.Text=current==null?"":current.Title;
        vocabulary.Text=current==null?"":Read(PathFor("vocabulary.txt"));
        transcript.Text=current==null?"":Read(PathFor("transcript.txt"));
        notes.Text=current==null?"":Read(PathFor("notes.txt"));
      } catch(Exception ex) { loadError="Could not load lesson: "+ex.Message; }
      finally { loading=false; }
      status.Text=loadError??(current==null?"Create a lesson to begin.":File.Exists(PathFor("audio.wav"))?"Recording saved. You can play or transcribe it.":"No recording yet.");
      UpdateButtons();
    }
    static string Read(string path) { return path!=null && File.Exists(path)?File.ReadAllText(path):""; }
    void QueueSave() { if(!loading && current!=null) { saveTimer.Stop();saveTimer.Start(); } }
    void SaveCurrent() {
      if(current==null || loading) return;
      try {
        string value=string.IsNullOrWhiteSpace(title.Text)?"Untitled lesson":title.Text.Trim();
        Write(PathFor("title.txt"),value);Write(PathFor("vocabulary.txt"),vocabulary.Text);Write(PathFor("transcript.txt"),transcript.Text);Write(PathFor("notes.txt"),notes.Text);
        current.Title=value;lessons.Items.Refresh();
      } catch(Exception ex) { status.Text="Could not save notes: "+ex.Message; }
    }
    static void Write(string path,string value) {
      string temp=path+".tmp";
      File.WriteAllText(temp,value??"",Encoding.UTF8);
      if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
    }
    void UpdateButtons() {
      bool selected=current!=null, hasAudio=selected && File.Exists(PathFor("audio.wav"));
      record.IsEnabled=selected && !recording && !hasAudio && transcriptionRequest==null && summaryRequest==null;
      pause.IsEnabled=recording;pause.Content=paused?"Resume":"Pause";
      finish.IsEnabled=recording;
      transcribe.IsEnabled=hasAudio && !recording && transcriptionRequest==null && summaryRequest==null && WhisperMic.Configured;
      cancelTranscribe.IsEnabled=transcriptionRequest!=null;
      summarize.IsEnabled=selected && !string.IsNullOrWhiteSpace(transcript.Text) && !recording && transcriptionRequest==null && summaryRequest==null;
      cancelSummary.IsEnabled=summaryRequest!=null;
      lessons.IsEnabled=!recording && transcriptionRequest==null && summaryRequest==null;
      transcript.IsReadOnly=summaryRequest!=null;
      notes.IsReadOnly=summaryRequest!=null;
    }
    string Duration() { return (elapsedBeforePause+(paused?TimeSpan.Zero:DateTime.Now-recordedAt)).ToString(@"hh\:mm\:ss"); }
    void StartRecording() {
      if(current==null) { status.Text="Create a lesson first."; return; }
      if(micBusy!=null && micBusy()) { status.Text="Turn off Corbie's mic chat before recording a lesson."; return; }
      if(File.Exists(PathFor("audio.wav"))) { status.Text="This lesson already has audio. Make a new lesson for another recording."; return; }
      try {
        SaveCurrent();
        if(recordingStarted!=null) recordingStarted();
        var device=new WaveInEvent { DeviceNumber=0,WaveFormat=new WaveFormat(16000,16,1),BufferMilliseconds=100,NumberOfBuffers=3 };
        var sink=new WaveFileWriter(PathFor("audio.wav"),device.WaveFormat);
        input=device;writer=sink;stopped=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        device.DataAvailable+=(s,e)=> { lock(audioGate) { if(recording && !paused && writer!=null) writer.Write(e.Buffer,0,e.BytesRecorded); } };
        device.RecordingStopped+=(s,e)=> { if(e.Exception!=null) stopped.TrySetException(e.Exception); else stopped.TrySetResult(true); };
        device.StartRecording();
        recording=true;paused=false;elapsedBeforePause=TimeSpan.Zero;recordedAt=DateTime.Now;clock.Start();
        status.Text="Recording · 00:00:00";UpdateButtons();
      } catch(Exception ex) {
        lock(audioGate) { recording=false;if(writer!=null) writer.Dispose();writer=null; }
        if(input!=null) input.Dispose();input=null;
        try { if(File.Exists(PathFor("audio.wav"))) File.Delete(PathFor("audio.wav")); } catch { }
        status.Text="Microphone could not start: "+ex.Message;UpdateButtons();
      }
    }
    void TogglePause() {
      if(!recording) return;
      if(paused) { recordedAt=DateTime.Now;paused=false; }
      else { elapsedBeforePause+=DateTime.Now-recordedAt;paused=true; }
      status.Text=(paused?"Paused":"Recording")+" · "+Duration();UpdateButtons();
    }
    async Task FinishRecording() {
      if(!recording) return;
      clock.Stop();
      lock(audioGate) { recording=false; }
      try {
        input.StopRecording();
        await stopped.Task;
        status.Text="Recording saved. Play it back, then transcribe when ready.";
      } catch(Exception ex) { status.Text="Recording stopped with an error; check the saved audio: "+ex.Message; }
      finally {
        lock(audioGate) { if(writer!=null) writer.Dispose();writer=null; }
        if(input!=null) input.Dispose();input=null;stopped=null;paused=false;UpdateButtons();
      }
    }
    async Task TranscribeRecording() {
      if(current==null || recording || transcriptionRequest!=null || summaryRequest!=null) return;
      string path=PathFor("audio.wav");
      if(!File.Exists(path)) { status.Text="Record a lesson first."; return; }
      if(!string.IsNullOrWhiteSpace(transcript.Text)) {
        var choice=MessageBox.Show(this,"Replace the current transcript with a new local transcription? Your existing edits will be lost. The recording and study notes stay saved.","Transcribe lesson again",MessageBoxButton.YesNo,MessageBoxImage.Question);
        if(choice!=MessageBoxResult.Yes) return;
      }
      SaveCurrent();
      var request=new CancellationTokenSource();transcriptionRequest=request;UpdateButtons();
      status.Text="Transcribing on this PC. The audio is safe if this takes a while or fails.";
      try {
        string result=await WhisperMic.TranscribeFile(path,vocabulary.Text,request.Token,(part,total)=>Dispatcher.BeginInvoke(new Action(()=> { if(transcriptionRequest==request) status.Text="Transcribing locally · part "+part+" of "+total+". Original audio is safe."; })));
        if(!string.IsNullOrWhiteSpace(result)) {
          transcript.Text=result;
          SaveCurrent();status.Text="Draft transcript saved. Please check science terms, numbers, and units against the audio.";
        } else status.Text="Whisper found no speech. The original recording is still saved.";
      } catch(OperationCanceledException) { status.Text="Transcription canceled. The recording is still saved."; }
      catch(Exception ex) { status.Text="Transcription failed: "+ex.Message+" The recording is still saved."; }
      finally { transcriptionRequest=null;request.Dispose();UpdateButtons(); }
    }
    async Task SummarizeLesson() {
      if(current==null || recording || transcriptionRequest!=null || summaryRequest!=null) return;
      if(string.IsNullOrWhiteSpace(transcript.Text)) { status.Text="Transcribe or type a lesson before summarizing it."; return; }
      if(!string.IsNullOrWhiteSpace(notes.Text)) {
        var choice=MessageBox.Show(this,"Replace your current study notes with a new summary? Your existing notes will be overwritten. The transcript and recording will stay saved.","Summarize lesson",MessageBoxButton.YesNo,MessageBoxImage.Question);
        if(choice!=MessageBoxResult.Yes) return;
      }
      SaveCurrent();
      string source=transcript.Text,lessonTitle=title.Text,terms=vocabulary.Text;
      var request=new CancellationTokenSource();summaryRequest=request;UpdateButtons();
      status.Text="Summarizing on this PC. Your transcript and audio are safe.";
      try {
        string result=await LessonSummarizer.Summarize(lessonTitle,terms,source,request.Token,message=>Dispatcher.BeginInvoke(new Action(()=> {
          if(summaryRequest==request && !request.IsCancellationRequested) status.Text=message;
        })));
        request.Token.ThrowIfCancellationRequested();
        notes.Text=result;
        SaveCurrent();
        status.Text="Draft summary saved. Check it against the transcript and recording, especially scientific terms, numbers, and units.";
      } catch(OperationCanceledException) { status.Text="Summary canceled. Your previous notes are unchanged."; }
      catch(Exception ex) { status.Text="Could not summarize locally: "+ex.Message+" Your previous notes are unchanged."; }
      finally { summaryRequest=null;request.Dispose();UpdateButtons(); }
    }
    void PlayRecording() {
      if(current==null || !File.Exists(PathFor("audio.wav"))) { status.Text="No recording for this lesson yet."; return; }
      try { System.Diagnostics.Process.Start(PathFor("audio.wav")); }
      catch(Exception ex) { status.Text="Could not open recording: "+ex.Message; }
    }
    void OpenFolder() {
      if(current==null) { status.Text="Create a lesson first."; return; }
      SaveCurrent();
      try { System.Diagnostics.Process.Start("explorer.exe",current.Folder); }
      catch(Exception ex) { status.Text="Could not open lesson folder: "+ex.Message; }
    }
    public static void Check() {
      if(!Root.EndsWith(Path.Combine("Corbie","Lessons"),StringComparison.OrdinalIgnoreCase)) throw new Exception("Lesson storage path changed.");
      if(new WaveFormat(16000,16,1).AverageBytesPerSecond!=32000) throw new Exception("Lesson WAV format changed.");
      string testRoot=Path.Combine(Path.GetTempPath(),"corbie-lessons-check-"+Guid.NewGuid().ToString("N"));
      try {
        var notebook=new LessonNotebook(()=>false,()=>{},testRoot);
        notebook.NewLesson();
        notebook.title.Text="Science lesson";
        notebook.vocabulary.Text="Aschlew Mengitsu, mitochondria";
        notebook.notes.Text="Check the formula.";
        notebook.SaveCurrent();
        string folder=notebook.current.Folder;
        notebook.Close();
        if(Read(Path.Combine(folder,"title.txt"))!="Science lesson" || Read(Path.Combine(folder,"vocabulary.txt")).IndexOf("mitochondria",StringComparison.Ordinal)<0 || Read(Path.Combine(folder,"notes.txt"))!="Check the formula.")
          throw new Exception("Lesson notebook did not persist edits.");
      } finally { if(Directory.Exists(testRoot)) Directory.Delete(testRoot,true); }
    }
  }
}
