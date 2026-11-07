using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace CorbiePet {
  // Local personality lines. This companion does not send messages to a service.
  public sealed class Companion : IDisposable {
    readonly Window pet;
    readonly Window bubble;
    readonly TextBlock words;
    readonly TextBlock heardWords;
    readonly Random random=new Random();
    double remaining, nextRemark=40;
    double stashCooldown=1800;
    int lastLine=-1;
    int voiceGeneration;
    bool disposed;
    PiperVoice voice;
    public bool VoiceEnabled { get; private set; }
    public VoiceTone VoiceTone {
      get { return PiperVoice.Tone; }
      set { PiperVoice.SaveTone(value); }
    }
    public string VoiceId { get { return PiperVoice.VoiceId; } }
    public IList<string> InstalledVoices { get { return PiperVoice.InstalledVoices(); } }
    public bool IsSpeaking {
      get {
        if(!VoiceEnabled || Paused || voice==null) return false;
        try { return voice.IsSpeaking; }
        catch(Exception) { return false; }
      }
    }
    public bool VoiceBusy { get { return VoiceEnabled && voice!=null && voice.Busy; } }
    public event Action VoiceUnavailable;
    public bool Paused { get; set; }
    public bool Quiet { get; set; }
    public Func<bool,string> MoodRemark { get; set; }
    public Action<Action> RequestAttention { get; set; }
    public bool Visible { get { return bubble.IsVisible; } }
    public IntPtr Handle { get { return new WindowInteropHelper(bubble).Handle; } }
    public Companion(Window owner) {
      pet=owner;
      var panel=new StackPanel();
      var title=new TextBlock { Text="CORBIE", FontSize=11, FontWeight=FontWeights.Bold, Foreground=Brushes.SlateBlue, Margin=new Thickness(0,0,0,7) };
      heardWords=new TextBlock { TextWrapping=TextWrapping.Wrap,FontSize=11,Foreground=Brushes.SlateGray,Margin=new Thickness(0,0,0,7),Visibility=Visibility.Collapsed };
      words=new TextBlock { TextWrapping=TextWrapping.Wrap, FontSize=15, LineHeight=21, Foreground=new SolidColorBrush(Color.FromRgb(42,36,56)) };
      var hint=new TextBlock { Text="click to dismiss", FontSize=10, Foreground=Brushes.SlateGray, Margin=new Thickness(0,9,0,0) };
      panel.Children.Add(title); panel.Children.Add(heardWords); panel.Children.Add(words); panel.Children.Add(hint);
      var card=new Border { Background=new SolidColorBrush(Color.FromRgb(255,250,232)), BorderBrush=new SolidColorBrush(Color.FromRgb(110,91,145)), BorderThickness=new Thickness(2), CornerRadius=new CornerRadius(15,15,15,3), Padding=new Thickness(16,12,16,12), Child=panel };
      bubble=new Window { Width=264, SizeToContent=SizeToContent.Height, WindowStyle=WindowStyle.None, ResizeMode=ResizeMode.NoResize, AllowsTransparency=true, Background=Brushes.Transparent, ShowInTaskbar=false, ShowActivated=false, Focusable=false, Topmost=true, Content=card };
      bubble.SourceInitialized+=(s,e)=>HwndSource.FromHwnd(Handle).AddHook(NoActivate);
      bubble.MouseLeftButtonDown+=(s,e)=> { Dismiss(); e.Handled=true; };
      pet.LocationChanged+=(s,e)=> { if(Visible) Position(); };
    }
    IntPtr NoActivate(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled) {
      if(message==0x21) { handled=true; return new IntPtr(3); } // MA_NOACTIVATE
      return IntPtr.Zero;
    }
    public void Say(string text) { SayCore(text,true); }
    public void SaySilent(string text) { SayCore(text,false); }
    void SayCore(string text,bool speak) {
      heardWords.Visibility=Visibility.Collapsed;
      words.Text=text; remaining=Math.Max(6,Math.Min(12,text.Length*0.065)); nextRemark=45+random.NextDouble()*30;
      if(bubble.Owner==null) { try { if(pet.IsLoaded || pet.IsVisible) bubble.Owner=pet; } catch { } }
      // Measure before showing so the first visible frame is beside the pet.
      // Measure the card, not the native Window: measuring a hidden, never-shown
      // SizeToContent window can enter WPF's HWND sizing path before Show().
      ((FrameworkElement)bubble.Content).Measure(new Size(264,double.PositiveInfinity));
      Position(); bubble.Show(); bubble.UpdateLayout(); Position();
      if(speak && VoiceEnabled && !Paused && voice!=null) {
        int generation=++voiceGeneration;
        try {
          var task=voice.Speak(text);
          task.ContinueWith(t => {
            if(t.IsFaulted) {
              var observed=t.Exception;
              pet.Dispatcher.BeginInvoke(new Action(() => {
                if(disposed || generation!=voiceGeneration || !VoiceEnabled || Paused) return;
                VoiceEnabled=false;
                words.Text=text+"\n\n(Voice is unavailable. Bubbles still work.)";
                if(VoiceUnavailable!=null) VoiceUnavailable();
              }));
            }
          });
        }
        catch(Exception) {
          VoiceEnabled=false;
          words.Text=text+"\n\n(Voice is unavailable. Bubbles still work.)";
          if(VoiceUnavailable!=null) VoiceUnavailable();
        }
      }
    }
    public void SayReply(string heard,string reply) {
      Say(reply);
      heardWords.Text="You: "+(heard.Length>130?heard.Substring(0,127)+"...":heard);
      heardWords.Visibility=Visibility.Visible;
      bubble.UpdateLayout(); Position();
    }
    void Position() {
      var source=PresentationSource.FromVisual(pet); if(source==null) return;
      var area=System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(pet).Handle).WorkingArea;
      var matrix=source.CompositionTarget.TransformFromDevice;
      var min=matrix.Transform(new Point(area.Left,area.Top)); var max=matrix.Transform(new Point(area.Right,area.Bottom));
      var card=bubble.Content as FrameworkElement;
      double height=card!=null && card.DesiredSize.Height>0?card.DesiredSize.Height:bubble.ActualHeight>0?bubble.ActualHeight:150;
      double x=pet.Left-240; if(x<min.X) x=pet.Left+150;
      bubble.Left=Math.Max(min.X,Math.Min(x,max.X-264));
      bubble.Top=Math.Max(min.Y,Math.Min(pet.Top-height+55,max.Y-height));
    }
    public void Dismiss() { remaining=0; bubble.Hide(); StopVoice(); }
    public void StopVoice() { voiceGeneration++; if(voice!=null) { try { voice.Stop(); } catch(Exception) { VoiceEnabled=false; if(VoiceUnavailable!=null) VoiceUnavailable(); } } }
    public bool SetVoice(bool enabled, bool announce=true) {
      if(!enabled) { VoiceEnabled=false; StopVoice(); return true; }
      try {
        if(!PiperVoice.Configured) throw new InvalidOperationException("Piper voice is not configured. Run Setup-Piper.ps1 first.");
        if(voice==null) voice=new PiperVoice();
        VoiceEnabled=true;
        if(announce) Say("Voice on. Brilliant. Now you can hear exactly how disappointed I am. In surround sound.");
        return true;
      } catch(Exception) {
        VoiceEnabled=false; if(voice!=null) { voice.Dispose(); voice=null; }
        if(announce) {
          Say(PiperVoice.Configured
            ? "My voice is off the menu right now. Lucky you. The critiques continue in writing."
            : "Piper voice isn't set up yet. Run Setup-Piper.ps1 if you want to hear me shout at you properly.");
        }
        return false;
      }
    }
    public bool SetVoiceModel(string id) {
      bool wasEnabled=VoiceEnabled; StopVoice(); if(voice!=null) { voice.Dispose(); voice=null; }
      try {
        PiperVoice.SaveVoice(id);
        if(wasEnabled) { voice=new PiperVoice(); VoiceEnabled=true; }
        Say("Voice changed. "+PiperVoice.FriendlyVoiceName(id)+" reporting for beak duty. Try not to look too impressed."); return true;
      } catch(Exception ex) { VoiceEnabled=false; Say("I couldn't switch voices: "+ex.Message); return false; }
    }
    public void Tick(double dt,bool paused,bool octopus,bool suppressRemarks=false) {
      if(paused) return;
      if(remaining>0) { if(!(VoiceEnabled && voice!=null && voice.Busy)) remaining-=dt; if(remaining<=0) Dismiss(); }
      if(Quiet || suppressRemarks) return;
      stashCooldown-=dt;
      if(stashCooldown<=0) {
        var info=RecycleBinHoard.Query();
        if(!info.IsEmpty) {
          stashCooldown=2700+random.Next(900);
          if(RequestAttention!=null) RequestAttention(()=>StashRemark(octopus,info)); else StashRemark(octopus,info);
          return;
        } else {
          stashCooldown=1800;
        }
      }
      nextRemark-=dt;
      if(nextRemark<=0) {
        nextRemark=50+random.NextDouble()*35;
        if(RequestAttention!=null) RequestAttention(()=>Talk(octopus)); else Talk(octopus);
      }
    }
    public void Talk(bool octopus) {
      DateTime now=DateTime.Now;
      int hour=now.Hour;
      var list=new List<string>();
      var timeLines=new List<string>();

      if(octopus) {
        list.AddRange(new [] {
          "Eight arms and not one of them wants to touch this mess. The absolute fucking state of it.",
          "This corner's lovely. The rest of the desktop is somebody else's problem. I've decided.",
          "I've dissolved into soup and I'm STILL better organised than your operating system, you plank.",
          "Eight arms, and somehow a bloody loading spinner is still in charge.",
          "Eight arms, zero wages. Who signed this contract? Some gormless wally. Oh. Me.",
          "I could ink the whole screen and it'd be an improvement. Don't tempt me.",
          "Cephalopod wisdom: when it all goes pear-shaped, become soup and wait it out.",
          "Eight hands and I still won't touch that. The bloody thing has stopped responding."
        });
        if(now.DayOfWeek==DayOfWeek.Friday) timeLines.Add("Friday. All eight arms have clocked off. You should too, you cheeky sod.");
        if(hour<5 || hour>=22) timeLines.Add("Octopuses are nocturnal. You are just badly managed. Bed!");
      } else {
        list.AddRange(new [] {
          "I have feathers, opinions, and absolutely no patience for software taking the piss.",
          "I live here. Apparently that makes me the bloody IT department.",
          "These icons are scattered like a stag do at closing time. Utter shitshow.",
          "Twelve windows open and not one of them is doing a thing. Wake up, you dozy doughnut!",
          "I'm a highly evolved corvid, babysitting this bollocks. Unbelievable.",
          "One click. Such authority. Have you considered being a woodpecker?",
          "My professional opinion? Bin the lot and start again. Invoice: three almonds.",
          "Where's the fucking focus gone? Out the door with your dignity, that's where.",
          "Oh lovely, another tab. Said absolutely nobody, you plonker.",
          "I've seen tidier desktops in a raccoon's lunchbox. Get a grip, numpty.",
          "Ravens remember faces. Yours is currently making a right pig's ear of this.",
          "That click had confidence. Shame everything after it went up in smoke.",
          "I'm available for technical support. Payment upfront. No, admiration doesn't count.",
          "What the fuck is that folder called? Final_final_v2? Sort it out, you pillock!"
        });
        if(hour<5) {
          timeLines.Add("Why the hell are you awake? Even crows sleep, and we eat roadkill! Bed, now!");
          timeLines.Add("That bug'll still be there tomorrow, you knackered owl. Go to sleep!");
        } else if(hour<9) {
          timeLines.Add("Morning. Shiny trinkets and a proper brew. Chop chop!");
        } else if(hour>=12 && hour<14) {
          timeLines.Add("Lunch! Eaten anything that isn't beige? No? Bloody disgrace.");
        } else if(hour>=17 && hour<20) {
          timeLines.Add("Evening. Productivity clocked off an hour ago. You're just loitering now.");
        } else if(hour>=20) {
          timeLines.Add("Still staring at the screen? Give your eyes a rest. I'll guard the bloody tabs.");
        }

        if(now.DayOfWeek==DayOfWeek.Monday) {
          timeLines.Add("Monday. I've filed a complaint. Apparently it still has to happen.");
        } else if(now.DayOfWeek==DayOfWeek.Wednesday) {
          timeLines.Add("Wednesday. Halfway through. I have contributed feathers and opinions.");
        } else if(now.DayOfWeek==DayOfWeek.Friday) {
          timeLines.Add("Friday. Even my patience has packed up and gone home.");
        } else if(now.DayOfWeek==DayOfWeek.Saturday || now.DayOfWeek==DayOfWeek.Sunday) {
          timeLines.Add("Weekend. Go outside, touch some grass, fetch me a shiny bottlecap. Go on, hop it!");
        }
      }
      if(MoodRemark!=null && random.Next(3)==0) {
        string moodLine=MoodRemark(octopus);
        if(!string.IsNullOrWhiteSpace(moodLine)) { Say(moodLine); return; }
      }
      // Occasional smugness about his real favourites, available in the Nest.
      if(random.Next(9)==0) { Say(SecretPlaylist.Tease(random)); return; }
      // Time awareness is occasional flavor, not a timestamp on every remark.
      if(timeLines.Count>0 && random.Next(10)==0) { Say(timeLines[random.Next(timeLines.Count)]); return; }
      string[] lines=list.ToArray();
      int index; do { index=random.Next(lines.Length); } while(index==lastLine && lines.Length>1); lastLine=index; Say(lines[index]);
    }
    public void Joke() {
      DateTime now=DateTime.Now;
      string day=now.ToString("dddd");
      string[] jokes={
        "Why did I sit on the keyboard? Someone had to stop the next bloody disaster.",
        "My filing system has three categories: shiny, suspicious, and what the fuck is that.",
        "Two wings, an excellent memory, and somehow I still cannot find where I put that shiny.",
        "I asked for a raise. They offered me a bigger cursor. A bigger cursor! Tight-fisted bastards.",
        "Why do ravens love developers? Free bugs. An all-you-can-eat buffet of mistakes.",
        "I told a joke to the Recycle Bin. It said my material was rubbish. Fair. Have you seen your desktop?",
        day=="Tuesday"
          ? "What day is it? It's Tuesday. And for once, it actually IS Tuesday."
          : string.Format("What day is it? It's {0}. Definitely not Tuesday. Unlike some vision models, I own a calendar, you pair of turnips.", day),
        "I tried counting my treasures. Turns out 'mine' isn't a number. Bloody oversight."
      };
      Say(jokes[random.Next(jokes.Length)]);
    }
    public void Treat(bool octopus) { Say(octopus?"A snack. FINALLY. Eight arms applaud. Took your sweet time, didn't you?":"Oh, a treat. Bribery works. First decent thing you've served all day. Don't let it go to your head."); }
    public void StashRemark(bool octopus, RecycleBinHoard.StashInfo info=null) {
      if(info==null) info=RecycleBinHoard.Query();
      if(info.IsEmpty) {
        Say(octopus
          ? "My sunken hoard in the Recycle Bin is barren. Not a single shiny piece of digital flotsam."
          : "My stash in the Recycle Bin is completely empty. Not one scrap. An empty pantry! Shameful.");
        return;
      }
      string countStr=info.ItemCount+(info.ItemCount==1?" item":" items");
      string[] lines=octopus?new [] {
        string.Format("My sunken hoard in the Recycle Bin has grown to {0} ({1}). Empty the depths whenever you need space.", countStr, info.FormattedSize),
        string.Format("Deep down in your Recycle Bin, {0} are resting ({1}). I call it treasure; you probably call it clutter.", countStr, info.FormattedSize)
      }:new [] {
        string.Format("My stash in the Recycle Bin is up to {0} ({1}). That fridge is overflowing. Clear it out before it grows legs!", countStr, info.FormattedSize),
        string.Format("I've amassed quite a collection: {0} shiny cast-offs ({1}) in the Recycle Bin. Might be time to take out the digital trash.", countStr, info.FormattedSize),
        string.Format("There are {0} ({1}) rotting in the Recycle Bin. It smells like a walk-in freezer that died. Empty it!", countStr, info.FormattedSize)
      };
      Say(lines[random.Next(lines.Length)]);
    }
    public void HoardReaction(bool octopus, string fileName) {
      string[] lines=octopus?new [] {
        string.Format("Yoink! Eight arms pulled '{0}' straight down into my sunken hoard.", fileName),
        string.Format("Secured '{0}'. Safely stashed in the oceanic depths of the Recycle Bin.", fileName),
        string.Format("Mine! Tentacles coiled around '{0}'. You clearly didn't need it.", fileName)
      }:new [] {
        string.Format("Mine now! Tucked '{0}' safely away in my personal nest (a.k.a. the Recycle Bin).", fileName),
        string.Format("Delicious digital clutter. Added '{0}' to my treasure pile.", fileName),
        string.Format("Yoink! Into the hoard goes '{0}'. You clearly weren't using it anyway.", fileName),
        string.Format("Trinket acquired: '{0}'. If you ever want it back, you'll have to go dumpster diving in the Recycle Bin.", fileName)
      };
      Say(lines[random.Next(lines.Length)]);
    }
    public void Dispose() { disposed=true; voiceGeneration++; if(voice!=null) { voice.Dispose(); voice=null; } bubble.Close(); }
    public static void CheckVoice(string file) {
      PiperVoice.Probe(file);
    }
    public void RenderPreview(string file) {
      Say("Right. I'm Corbie. I've seen your desktop and frankly, it's a dog's dinner. Let's get cracking.");
      bubble.UpdateLayout();
      var content=(FrameworkElement)bubble.Content;
      var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth),(int)Math.Ceiling(content.ActualHeight),96,96,PixelFormats.Pbgra32);
      bitmap.Render(content);
      var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
      using(var stream=System.IO.File.Create(file)) encoder.Save(stream);
      Dismiss();
    }
    public void Check() {
      bool wasVoice=VoiceEnabled; VoiceEnabled=false; StopVoice();
      try {
        Say("Right. I'm Corbie. I've seen your desktop and frankly, it's a dog's dinner. Let's get cracking.");
        if(!Visible) throw new Exception("Speech bubble did not open");
        Tick(30,true,false); if(!Visible) throw new Exception("Pause did not freeze bubble");
        Quiet=true; Tick(30,false,false); if(Visible) throw new Exception("Speech bubble did not dismiss");
        Tick(200,false,false); if(Visible) throw new Exception("Quiet mode produced a remark");
        Talk(false); if(!Visible) throw new Exception("Requested talk failed in quiet mode");
        Talk(true); if(!Visible) throw new Exception("Requested octopus talk failed in quiet mode");
        Joke(); if(!Visible) throw new Exception("Requested joke failed in quiet mode");
        StashRemark(false); if(!Visible) throw new Exception("Requested stash remark failed in quiet mode");
        HoardReaction(false,"dummy.txt"); if(!Visible) throw new Exception("Requested hoard reaction failed in quiet mode");
        var prevTone=VoiceTone;
        VoiceTone=VoiceTone.Chirpy;
        if(VoiceTone!=VoiceTone.Chirpy) throw new Exception("VoiceTone set failed");
        VoiceTone=prevTone;
        Dismiss(); Quiet=false;
      } finally {
        VoiceEnabled=wasVoice;
      }
    }
  }
}
