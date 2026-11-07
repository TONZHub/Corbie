using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NAudio.Wave;

namespace CorbiePet {
  public sealed class BirdMotif {
    public double Beat;
    public double Brightness;
    public int Variation;
    public BirdNote[] Notes;
  }
  public sealed class BirdNote {
    public int Midi;
    public double Start,Duration;
  }
  // Only a short string of pitch/timing estimates is retained; raw samples,
  // track metadata and lyrics never enter this memory.
  public sealed class BirdTuneEar {
    sealed class PitchFrame { public double Time; public int Midi; public bool Pulse; }
    readonly List<PitchFrame> frames=new List<PitchFrame>();
    double clock,heardSeconds,silence,brightnessSum,lastClear=-100,previousLevel,lastPulse=-100;
    int pitchSerial,brightnessCount;
    bool pendingPulse;
    BirdMotif remembered;
    public BirdMotif Remembered { get { return remembered; } }
    public double HeardSeconds { get { return heardSeconds; } }
    public void Reset() { clock=heardSeconds=silence=brightnessSum=previousLevel=0; lastClear=lastPulse=-100; pitchSerial=brightnessCount=0; pendingPulse=false; frames.Clear(); remembered=null; }
    public bool Observe(double dt,MusicDance.State audio) {
      dt=Math.Max(0,Math.Min(0.1,dt)); clock+=dt;
      bool audible=audio.Active && audio.Level>0.04;
      if(audible) {
        heardSeconds+=dt; silence=0;
        if(audio.Level-previousLevel>.08 && audio.Level>.12 && clock-lastPulse>.18) { pendingPulse=true; lastPulse=clock; }
        previousLevel=audio.Level;
        if(audio.PitchSerial!=0 && audio.PitchSerial!=pitchSerial) {
          pitchSerial=audio.PitchSerial;
          if(audio.PitchMidi>=48 && audio.PitchMidi<=84 && audio.PitchConfidence>=.3) {
            frames.Add(new PitchFrame { Time=clock,Midi=audio.PitchMidi,Pulse=pendingPulse });
            brightnessSum+=Math.Max(0,Math.Min(1,audio.High)); brightnessCount++;
          }
          pendingPulse=false;
        }
        while(frames.Count>0 && (clock-frames[0].Time>4 || frames.Count>60)) frames.RemoveAt(0);
        if(heardSeconds>=8) { var candidate=BuildMotif(); if(candidate!=null) { remembered=candidate; lastClear=clock; } }
        return false;
      }
      previousLevel=0;
      if(heardSeconds>0) silence+=dt;
      if(silence>=2 && remembered!=null && clock-lastClear<=8) { ClearPassage(); return true; }
      if(silence>=2 && clock-lastClear>8) remembered=null;
      if(silence>=3) ClearPassage();
      return false;
    }
    void ClearPassage() { heardSeconds=silence=brightnessSum=0; brightnessCount=0; frames.Clear(); }
    BirdMotif BuildMotif() {
      var segments=new List<BirdNote>();
      if(frames.Count<6) return null;
      double start=frames[0].Time,end=start+.09; int midi=frames[0].Midi;
      for(int i=1;i<=frames.Count;i++) {
        bool same=i<frames.Count && !frames[i].Pulse && Math.Abs(frames[i].Midi-midi)<=1 && frames[i].Time-end<=.22;
        if(same) { end=frames[i].Time+.09; continue; }
        if(end-start>=.14) segments.Add(new BirdNote { Midi=midi,Start=start,Duration=end-start });
        if(i<frames.Count) { midi=frames[i].Midi; start=frames[i].Time; end=start+.09; }
      }
      if(segments.Count<3) return null;
      int first=Math.Max(0,segments.Count-6);
      var notes=new List<BirdNote>(); double origin=segments[first].Start,position=0,previousEnd=origin;
      for(int i=first;i<segments.Count;i++) {
        var piece=segments[i];
        position+=Math.Max(0,Math.Min(.22,piece.Start-previousEnd));
        double duration=Math.Max(.14,Math.Min(.55,piece.Duration));
        notes.Add(new BirdNote { Midi=piece.Midi,Start=position,Duration=duration });
        position+=duration; previousEnd=piece.Start+piece.Duration;
      }
      if(position>3.3) return null;
      return new BirdMotif { Notes=notes.ToArray(),Brightness=brightnessCount==0?.4:brightnessSum/brightnessCount,Beat=.3 };
    }
    public static void Check() {
      var ear=new BirdTuneEar(); bool ready=false;
      for(int i=0;i<120;i++) ready|=ear.Observe(.1,new MusicDance.State { Active=true,Level=.2,High=.5,PitchMidi=new[]{69,72,76}[(i/5)%3],PitchConfidence=.8,PitchSerial=i+1 });
      if(ready || ear.Remembered==null || ear.Remembered.Notes.Length<3 || ear.Remembered.Notes[0].Midi==ear.Remembered.Notes[1].Midi) throw new Exception("Bird ear did not learn a melodic contour.");
      for(int i=0;i<25;i++) ready|=ear.Observe(.1,new MusicDance.State());
      if(!ready) throw new Exception("Bird ear did not wait for a music gap.");
      ready=false; for(int i=0;i<100;i++) ready|=ear.Observe(.1,new MusicDance.State());
      if(ready) throw new Exception("Bird ear repeated the same gap.");
      ear.Reset(); if(ear.Remembered!=null) throw new Exception("Bird ear retained a reset tune.");
      for(int i=0;i<120;i++) ear.Observe(.1,new MusicDance.State { Active=true,Level=i%4==0?.5:.18,PitchMidi=69,PitchConfidence=.8,PitchSerial=i+1 });
      if(ear.Remembered==null || ear.Remembered.Notes.Length<3) throw new Exception("Repeated-note rhythm was lost.");
      ear.Reset();
      for(int i=0;i<120;i++) ear.Observe(.1,new MusicDance.State { Active=true,Level=.2 });
      if(ear.Remembered!=null) throw new Exception("Pitchless music invented a melody.");
      for(int i=0;i<25;i++) if(ear.Observe(.1,new MusicDance.State())) throw new Exception("Unclear music triggered a mimic.");
    }
  }
  public static class BirdTuneSynth {
    public static byte[] Make(BirdMotif motif) {
      if(motif==null) motif=new BirdMotif { Beat=.32,Brightness=.45,Variation=2 };
      double beat=Math.Max(.22,Math.Min(.48,motif.Beat));
      double bright=Math.Max(0,Math.Min(1,motif.Brightness));
      BirdNote[] notes=motif.Notes;
      if(notes==null || notes.Length<3 || notes.Length>6 || !Valid(notes)) {
        int variation=Math.Abs(motif.Variation)%5;
        int[] steps={0,3,5,2,7};
        notes=new BirdNote[4];
        for(int i=0;i<4;i++) notes[i]=new BirdNote { Midi=72+steps[(i+variation)%steps.Length],Start=i*beat,Duration=beat*(i==3?1.05:.78) };
      }
      double average=0; foreach(var note in notes) average+=note.Midi; average/=notes.Length;
      int octaveShift=0;
      while(average+octaveShift<71) octaveShift+=12;
      while(average+octaveShift>79) octaveShift-=12;
      int rate=22050;
      double length=Math.Min(3.5,notes[notes.Length-1].Start+notes[notes.Length-1].Duration+.15);
      var samples=new short[(int)(length*rate)];
      for(int note=0;note<notes.Length;note++) {
        double start=notes[note].Start,duration=notes[note].Duration;
        double baseHz=440*Math.Pow(2,(notes[note].Midi+octaveShift-69)/12.0);
        double phase=0;
        int first=(int)(start*rate),count=(int)(duration*rate);
        for(int i=0;i<count && first+i<samples.Length;i++) {
          double t=i/(double)rate,p=t/duration;
          double env=Math.Min(1,t/.024)*Math.Min(1,(duration-t)/.075);
          double frequency=baseHz*(1+0.008*Math.Sin(2*Math.PI*8*t)+0.012*p);
          phase+=2*Math.PI*frequency/rate;
          double trill=.84+.16*Math.Sin(2*Math.PI*14*t);
          double sound=(Math.Sin(phase)+(.19+.15*bright)*Math.Sin(2*phase)+.10*Math.Sin(3*phase))*trill*env;
          double value=samples[first+i]+sound*4700;
          samples[first+i]=(short)Math.Max(-10000,Math.Min(10000,value));
        }
      }
      return CreatureFoley.BuildWav(samples,rate);
    }
    static bool Valid(BirdNote[] notes) {
      double previous=-1;
      foreach(var note in notes) {
        if(note==null || note.Midi<48 || note.Midi>84 || double.IsNaN(note.Start) || double.IsNaN(note.Duration)
          || note.Start<previous || note.Start<0 || note.Start>3 || note.Duration<.1 || note.Duration>.6 || note.Start+note.Duration>3.35) return false;
        previous=note.Start;
      }
      return true;
    }
    public static void Check() {
      var a=Make(null); var b=Make(new BirdMotif { Beat=.4,Brightness=.8,Variation=4 });
      if(a.Length<20000 || b.Length<20000 || a.Length==b.Length || a[0]!='R' || a[1]!='I') throw new Exception("Bird tune WAV failed.");
      var melody=new BirdMotif { Notes=new[]{new BirdNote { Midi=60,Start=0,Duration=.25 },new BirdNote { Midi=64,Start=.3,Duration=.3 },new BirdNote { Midi=67,Start=.65,Duration=.3 }} };
      var mimicked=Make(melody);
      if(mimicked.Length<=44 || mimicked.Length==a.Length || mimicked.Length>3.5*22050*2+44) throw new Exception("Melodic WAV timing failed.");
      var heard=new MelodyPitch(22050); int[] picked=new int[3];
      for(int i=0;i<(mimicked.Length-44)/2;i++) {
        heard.Feed(BitConverter.ToInt16(mimicked,44+i*2)/32768.0);
        if(i==(int)(.22*22050)) picked[0]=heard.Midi;
        if(i==(int)(.57*22050)) picked[1]=heard.Midi;
        if(i==(int)(.93*22050)) picked[2]=heard.Midi;
      }
      if(Math.Abs(picked[0]-72)>1 || Math.Abs(picked[1]-76)>1 || Math.Abs(picked[2]-79)>1) throw new Exception("Synth lost the heard contour: "+string.Join(",",picked));
      int peak=0; for(int i=44;i+1<a.Length;i+=2) peak=Math.Max(peak,Math.Abs(BitConverter.ToInt16(a,i)));
      if(peak<500 || peak>10000) throw new Exception("Bird tune volume invalid.");
    }
  }
  public static class BirdTunePlayer {
    static int generation,playing;
    public static bool IsPlaying { get { return Interlocked.CompareExchange(ref playing,0,0)!=0; } }
    public static void Stop() { Interlocked.Increment(ref generation); }
    public static bool Play(byte[] wav) {
      if(wav==null || wav.Length<44 || Interlocked.CompareExchange(ref playing,1,0)!=0) return false;
      int mine=Interlocked.CompareExchange(ref generation,0,0);
      ThreadPool.QueueUserWorkItem(_=>{
        try {
          if(mine!=Interlocked.CompareExchange(ref generation,0,0)) return;
          using(var stream=new MemoryStream(wav,false))
          using(var reader=new WaveFileReader(stream))
          using(var output=new WaveOutEvent()) {
            output.Init(reader); output.Play();
            while(output.PlaybackState==PlaybackState.Playing) {
              if(mine!=Interlocked.CompareExchange(ref generation,0,0)) { output.Stop(); break; }
              Thread.Sleep(15);
            }
          }
        } catch { }
        finally { Interlocked.Exchange(ref playing,0); }
      });
      return true;
    }
  }
  public partial class Pet {
    readonly BirdTuneEar birdTuneEar=new BirdTuneEar();
    bool birdTunesEnabled;
    double birdTuneCooldown=30,birdTuneIgnore;
    CheckBox birdTunesToggle;
    TextBlock birdTunesStatus;
    string TunesPreference { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"corbie-bird-tunes-enabled.txt"); } }
    void InitializeBirdTunes() {
      if(!testing) try { birdTunesEnabled=File.Exists(TunesPreference) && File.ReadAllText(TunesPreference).Trim()=="enabled"; } catch { birdTunesEnabled=false; }
    }
    bool BirdTunesAllowed() {
      return !paused && !shuttingDown && !octopusMode && !isNightSleep && !companion.Quiet && !companion.Visible && !companion.VoiceBusy
        && micRequest==null && !looking && !dragging && !fullscreenOrPresentation && !focusSprint.Active && !breakCoach.OnBreak && !breakCoach.Due
        && !attentionBid.Active && !notificationEdge.HuntActive && !eyeRestCoach.GlideActive && !shimejiInteracting
        && (lessonNotebook==null || !lessonNotebook.IsRecording) && !ContextMenu.IsOpen && !tray.ContextMenuStrip.Visible;
    }
    void StepBirdTunes(double dt,MusicDance.State audio) {
      birdTuneCooldown=Math.Max(0,birdTuneCooldown-dt);
      if(!BirdTunesAllowed()) { if(BirdTunePlayer.IsPlaying) BirdTunePlayer.Stop(); birdTuneEar.Reset(); return; }
      // The output loopback hears Corbie too. Do not learn his own whistle.
      if(BirdTunePlayer.IsPlaying) { birdTuneEar.Reset(); birdTuneIgnore=1; return; }
      if(birdTuneIgnore>0) { birdTuneIgnore=Math.Max(0,birdTuneIgnore-dt); birdTuneEar.Reset(); return; }
      // A manually requested tune may finish even with automatic mimicry off.
      if(!birdTunesEnabled || !danceEnabled) { birdTuneEar.Reset(); return; }
      if(birdTuneEar.Observe(dt,audio) && birdTuneCooldown<=0) SingBirdTune(false);
    }
    void SingBirdTune(bool requested) {
      if(!BirdTunesAllowed()) { if(birdTunesStatus!=null) birdTunesStatus.Text="Corbie needs a quiet, available moment to sing."; return; }
      if(!requested && (!birdTunesEnabled || birdTuneCooldown>0)) return;
      if(birdTuneEar.Remembered==null) { if(birdTunesStatus!=null) birdTunesStatus.Text="No clear melody yet. Let him hear a passage with Dance on."; return; }
      if(BirdTunePlayer.Play(BirdTuneSynth.Make(birdTuneEar.Remembered))) {
        birdTuneCooldown=requested?45:180;
        if(birdTunesStatus!=null) birdTunesStatus.Text=requested?"A little tune, just for you":"Echoing a rhythm in his own way";
      }
    }
    void SingOriginalTune() {
      if(!BirdTunesAllowed()) { if(birdTunesStatus!=null) birdTunesStatus.Text="Corbie needs a quiet, available moment to sing."; return; }
      if(BirdTunePlayer.Play(BirdTuneSynth.Make(null)) && birdTunesStatus!=null) birdTunesStatus.Text="Singing his own little tune";
    }
    void SetBirdTunes(bool enabled) {
      if(refreshingPanel) return;
      if(!testing) try { File.WriteAllText(TunesPreference,enabled?"enabled":"disabled"); } catch { if(birdTunesStatus!=null) birdTunesStatus.Text="Couldn't save the little tunes setting."; return; }
      birdTunesEnabled=enabled; birdTuneEar.Reset(); if(!enabled) BirdTunePlayer.Stop(); RefreshBirdTunes();
    }
    void BuildBirdTunesControls(StackPanel body) {
      birdTunesToggle=Toggle("Let Corbie make little tunes",SetBirdTunes);
      body.Children.Add(Section("Corbie's little tunes",birdTunesToggle,birdTunesStatus=Status(),Actions(ActionButton("Mimic what he heard",()=>SingBirdTune(true)),ActionButton("Sing his own tune",SingOriginalTune)),
        new TextBlock { Text="Optional. With Dance to computer audio on, Corbie estimates a short run of notes and their timing, then whistles it back in his own voice after the music pauses. He never records or replays the song. Dense mixes may hide the melody; if he cannot hear clear notes, he stays quiet. Quiet mode, mic chat, sleep, focus and conversation silence him; automatic tunes have a three-minute rest. His own tune works without music.",TextWrapping=TextWrapping.Wrap,Foreground=MutedText }));
      RefreshBirdTunes();
    }
    void RefreshBirdTunes() {
      if(birdTunesStatus==null) return;
      bool prior=refreshingPanel; refreshingPanel=true; try { birdTunesToggle.IsChecked=birdTunesEnabled; } finally { refreshingPanel=prior; }
      birdTunesStatus.Text=!birdTunesEnabled?"Automatic tunes off":companion.Quiet?"Quiet mode · silent":!danceEnabled?"Enable Dance to computer audio to hear notes":BirdTunePlayer.IsPlaying?"Singing a little passage":birdTuneEar.Remembered!=null?"A little melody is on his mind":"Listening for clear notes";
    }
    void CheckBirdTunes() {
      BirdTuneEar.Check(); BirdTuneSynth.Check();
      birdTunesEnabled=true; paused=true; if(BirdTunesAllowed()) throw new Exception("Tune bypassed pause."); paused=false;
      octopusMode=true; if(BirdTunesAllowed()) throw new Exception("Octopus sang raven tune."); octopusMode=false;
      isNightSleep=true; if(BirdTunesAllowed()) throw new Exception("Sleeping bird sang."); isNightSleep=false;
      companion.Quiet=true; if(BirdTunesAllowed()) throw new Exception("Quiet mode sang."); companion.Quiet=false;
      CheckControlPanel(); if(birdTunesStatus==null) throw new Exception("Missing song controls.");
    }
  }
}
