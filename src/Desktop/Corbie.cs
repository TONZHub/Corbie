using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Forms = System.Windows.Forms;

namespace CorbiePet {
  public class ArchivePasswordDialog : Window {
    readonly PasswordBox passwordBox;
    public string Password { get; private set; }
    public bool Confirmed { get; private set; }

    public ArchivePasswordDialog(Window owner, string fileName) {
      Owner = owner;
      Title = "Corbie - Peck Open";
      Width = 320;
      SizeToContent = SizeToContent.Height;
      WindowStyle = WindowStyle.None;
      ResizeMode = ResizeMode.NoResize;
      AllowsTransparency = true;
      Background = Brushes.Transparent;
      WindowStartupLocation = WindowStartupLocation.CenterOwner;
      Topmost = true;

      var panel = new StackPanel();
      var title = new TextBlock { Text = "PECK OPEN - LOCKED ARCHIVE", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.SlateBlue, Margin = new Thickness(0,0,0,8) };
      var desc = new TextBlock { Text = "Crunch... this archive is locked with a password. What's the secret key for '" + fileName + "'?", TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(42,36,56)), Margin = new Thickness(0,0,0,10) };
      passwordBox = new PasswordBox { FontSize = 14, Padding = new Thickness(6,4,6,4), Margin = new Thickness(0,0,0,12) };

      var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
      var cancelBtn = new Button { Content = "Never Mind", Width = 90, Height = 28, Margin = new Thickness(0,0,8,0), Background = new SolidColorBrush(Color.FromRgb(240,235,245)), BorderBrush = Brushes.LightSlateGray };
      var okBtn = new Button { Content = "Peck Open", Width = 90, Height = 28, IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(110,91,145)), Foreground = Brushes.White, FontWeight = FontWeights.SemiBold };

      cancelBtn.Click += (s, e) => { Confirmed = false; Close(); };
      okBtn.Click += (s, e) => { Confirmed = true; Password = passwordBox.Password; Close(); };
      btnPanel.Children.Add(cancelBtn);
      btnPanel.Children.Add(okBtn);

      panel.Children.Add(title);
      panel.Children.Add(desc);
      panel.Children.Add(passwordBox);
      panel.Children.Add(btnPanel);

      var border = new Border {
        Background = new SolidColorBrush(Color.FromRgb(255,250,232)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(110,91,145)),
        BorderThickness = new Thickness(2),
        CornerRadius = new CornerRadius(12),
        Padding = new Thickness(16,14,16,14),
        Child = panel
      };
      Content = border;
      Loaded += (s, e) => passwordBox.Focus();
    }
  }

  public class ArchivePreviewDialog : Window {
    public bool Confirmed { get; private set; }
    public ArchivePreviewDialog(Window owner, ArchiveInfo info, string targetDir) {
      Owner = owner;
      Title = "Corbie - Peck Open";
      Width = 340;
      SizeToContent = SizeToContent.Height;
      WindowStyle = WindowStyle.None;
      ResizeMode = ResizeMode.NoResize;
      AllowsTransparency = true;
      Background = Brushes.Transparent;
      WindowStartupLocation = WindowStartupLocation.CenterOwner;
      Topmost = true;

      var panel = new StackPanel();
      var title = new TextBlock { Text = "PECK OPEN - ARCHIVE PREVIEW", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.SlateBlue, Margin = new Thickness(0,0,0,8) };
      var summary = new TextBlock { Text = info.FormatSummary(), TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(42,36,56)), Margin = new Thickness(0,0,0,8) };
      var dest = new TextBlock { Text = "Destination:\n" + targetDir, TextWrapping = TextWrapping.Wrap, FontSize = 11, Foreground = Brushes.DimGray, Margin = new Thickness(0,0,0,12) };

      var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
      var cancelBtn = new Button { Content = "Cancel", Width = 80, Height = 28, Margin = new Thickness(0,0,8,0), Background = new SolidColorBrush(Color.FromRgb(240,235,245)), BorderBrush = Brushes.LightSlateGray };
      var okBtn = new Button { Content = "Peck Open", Width = 95, Height = 28, IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(110,91,145)), Foreground = Brushes.White, FontWeight = FontWeights.SemiBold };

      cancelBtn.Click += (s, e) => { Confirmed = false; Close(); };
      okBtn.Click += (s, e) => { Confirmed = true; Close(); };
      btnPanel.Children.Add(cancelBtn);
      btnPanel.Children.Add(okBtn);

      panel.Children.Add(title);
      panel.Children.Add(summary);
      panel.Children.Add(dest);
      panel.Children.Add(btnPanel);

      var border = new Border {
        Background = new SolidColorBrush(Color.FromRgb(255,250,232)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(110,91,145)),
        BorderThickness = new Thickness(2),
        CornerRadius = new CornerRadius(12),
        Padding = new Thickness(16,14,16,14),
        Child = panel
      };
      Content = border;
    }
  }

  public class HoardConfirmDialog : Window {
    public bool Confirmed { get; private set; }
    public bool DontWarnAgain { get; private set; }

    public HoardConfirmDialog(Window owner, string filePath) : this(owner,new [] { filePath }) { }
    public HoardConfirmDialog(Window owner, string[] filePaths) {
      Owner = owner;
      Title = "Corbie - Can I have this?";
      Width = 340;
      SizeToContent = SizeToContent.Height;
      WindowStyle = WindowStyle.None;
      ResizeMode = ResizeMode.NoResize;
      AllowsTransparency = true;
      Background = Brushes.Transparent;
      WindowStartupLocation = WindowStartupLocation.CenterOwner;
      Topmost = true;

      var names=new System.Collections.Generic.List<string>(); long fileSize=0;
      foreach(string filePath in filePaths) {
        string name=System.IO.Path.GetFileName(filePath);
        names.Add(string.IsNullOrEmpty(name)?filePath:name);
        try { if(System.IO.File.Exists(filePath)) fileSize+=new System.IO.FileInfo(filePath).Length; } catch { }
      }
      string sizeStr = fileSize > 0 ? " (" + RecycleBinHoard.FormatBytes(fileSize) + " total)" : "";

      var panel = new StackPanel();
      var title = new TextBlock { Text = "HOARD ITEM - CAN I HAVE THIS?", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.SlateBlue, Margin = new Thickness(0,0,0,8) };
      var desc = new TextBlock { Text = "Corbie wants "+names.Count+" file(s)"+sizeStr+" for his hoard:\n"+string.Join("\n",names), TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(42,36,56)) };
      var filePreview=new ScrollViewer { Content=desc,MaxHeight=180,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,0,0,8) };
      var warn = new TextBlock { Text = "Warning: These files will go to your Windows Recycle Bin.", TextWrapping = TextWrapping.Wrap, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(160,50,50)), Margin = new Thickness(0,0,0,10) };

      var chkDontWarn = new CheckBox { Content = "Don't warn me again this session", FontSize = 11, Foreground = Brushes.DimGray, Margin = new Thickness(0,0,0,12) };

      var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
      var cancelBtn = new Button { Content = "No, Keep My File", Width = 115, Height = 28, Margin = new Thickness(0,0,8,0), Background = new SolidColorBrush(Color.FromRgb(240,235,245)), BorderBrush = Brushes.LightSlateGray };
      var okBtn = new Button { Content = "Yes, Take It!", Width = 100, Height = 28, IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(110,91,145)), Foreground = Brushes.White, FontWeight = FontWeights.SemiBold };

      cancelBtn.Click += (s, e) => { Confirmed = false; Close(); };
      okBtn.Click += (s, e) => { Confirmed = true; DontWarnAgain = chkDontWarn.IsChecked == true; Close(); };
      btnPanel.Children.Add(cancelBtn);
      btnPanel.Children.Add(okBtn);

      panel.Children.Add(title);
      panel.Children.Add(filePreview);
      panel.Children.Add(warn);
      panel.Children.Add(chkDontWarn);
      panel.Children.Add(btnPanel);

      var border = new Border {
        Background = new SolidColorBrush(Color.FromRgb(255,250,232)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(110,91,145)),
        BorderThickness = new Thickness(2),
        CornerRadius = new CornerRadius(12),
        Padding = new Thickness(16,14,16,14),
        Child = panel
      };
      Content = border;
    }
  }

  public class ArchiveChoiceDialog : Window {
    public enum Choice { Cancel, CrackOpen, KeepInHoard }
    public Choice SelectedChoice { get; private set; }
    public bool DontWarnAgain { get; private set; }

    public ArchiveChoiceDialog(Window owner, ArchiveInfo info) {
      Owner = owner;
      Title = "Corbie - Archive Decision";
      Width = 350;
      SizeToContent = SizeToContent.Height;
      WindowStyle = WindowStyle.None;
      ResizeMode = ResizeMode.NoResize;
      AllowsTransparency = true;
      Background = Brushes.Transparent;
      WindowStartupLocation = WindowStartupLocation.CenterOwner;
      Topmost = true;

      SelectedChoice = Choice.Cancel;

      var panel = new StackPanel();
      var title = new TextBlock { Text = "COMPRESSED FILE - CRACK OPEN OR HOARD?", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.SlateBlue, Margin = new Thickness(0,0,0,8) };
      var summary = new TextBlock { Text = info.FormatSummary(), TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(42,36,56)), Margin = new Thickness(0,0,0,8) };
      var prompt = new TextBlock { Text = "What should I do with this archive? Crack it open into a folder, or can I stash it in my hoard (Recycle Bin)?", TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Brushes.DimGray, Margin = new Thickness(0,0,0,10) };

      var chkDontWarn = new CheckBox { Content = "Don't warn when hoarding this session", FontSize = 11, Foreground = Brushes.DimGray, Margin = new Thickness(0,0,0,12) };

      var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
      var cancelBtn = new Button { Content = "Cancel", Width = 65, Height = 28, Margin = new Thickness(0,0,8,0), Background = new SolidColorBrush(Color.FromRgb(240,235,245)), BorderBrush = Brushes.LightSlateGray };
      var hoardBtn = new Button { Content = "Keep in Hoard", Width = 110, Height = 28, Margin = new Thickness(0,0,8,0), Background = new SolidColorBrush(Color.FromRgb(165,140,80)), Foreground = Brushes.White, FontWeight = FontWeights.SemiBold };
      var crackBtn = new Button { Content = "Crack Open", Width = 95, Height = 28, IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(110,91,145)), Foreground = Brushes.White, FontWeight = FontWeights.SemiBold };

      cancelBtn.Click += (s, e) => { SelectedChoice = Choice.Cancel; Close(); };
      hoardBtn.Click += (s, e) => { SelectedChoice = Choice.KeepInHoard; DontWarnAgain = chkDontWarn.IsChecked == true; Close(); };
      crackBtn.Click += (s, e) => { SelectedChoice = Choice.CrackOpen; Close(); };

      btnPanel.Children.Add(cancelBtn);
      btnPanel.Children.Add(hoardBtn);
      btnPanel.Children.Add(crackBtn);

      panel.Children.Add(title);
      panel.Children.Add(summary);
      panel.Children.Add(prompt);
      panel.Children.Add(chkDontWarn);
      panel.Children.Add(btnPanel);

      var border = new Border {
        Background = new SolidColorBrush(Color.FromRgb(255,250,232)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(110,91,145)),
        BorderThickness = new Thickness(2),
        CornerRadius = new CornerRadius(12),
        Padding = new Thickness(16,14,16,14),
        Child = panel
      };
      Content = border;
    }
  }

  public partial class Pet : Window {
    readonly Canvas scene = new Canvas { Width=180, Height=180, Background=Brushes.Transparent };
    readonly Canvas bird = new Canvas { Width=180, Height=180 };
    readonly Canvas octopus = new Canvas { Width=180, Height=180, Visibility=Visibility.Hidden };
    readonly ScaleTransform squish = new ScaleTransform(1,1,90,130);
    readonly TranslateTransform motion = new TranslateTransform();
    readonly TranslateTransform bodyRest = new TranslateTransform();
    TranslateTransform RestingBody { get { return octopusMode?motion:bodyRest; } }
    Path leftFootVisual,rightFootVisual;
    readonly ScaleTransform facing = new ScaleTransform(1,1,90,90);
    readonly RotateTransform danceTilt=new RotateTransform(0,90,149);
    readonly MusicDance music=new MusicDance();
    readonly MusicBrain musicBrain;
    readonly DispatcherTimer musicBrainTimer=new DispatcherTimer();
    CancellationTokenSource musicBrainRequest;
    bool musicTasteEnabled;
    string pendingFavouriteRemark,pendingFavouriteKey;
    DateTime pendingFavouriteUtc;
    MenuItem musicTasteItem,musicTasteStatus;
    Forms.ToolStripMenuItem trayMusicTaste,trayMusicTasteStatus;
    bool danceEnabled, wasDancing;
    double danceBlend, dancePulse, dancePhase, danceEnergy, danceTreble;
    MenuItem danceItem;
    Forms.ToolStripMenuItem trayDance;
    readonly Ellipse musicLight=new Ellipse { Width=10,Height=10,Fill=Brushes.MediumPurple,Visibility=Visibility.Hidden };
    readonly TranslateTransform leftFoot = new TranslateTransform(), rightFoot = new TranslateTransform();
    bool wandering=true, walking;
    int direction=-1;
    double walkTime, walkWait=3, stride, perchOffset, routeMin, routeMax;
    double perchArrivalTime;
    int perchArrivalDirection;
    MenuItem wanderItem;
    Forms.ToolStripMenuItem trayWander;
    readonly RotateTransform headTilt = new RotateTransform(0,105,83);
    readonly RotateTransform cursorTilt = new RotateTransform(0,105,83);
    readonly RotateTransform beakOpen = new RotateTransform(0,133,77);
    double speechPhase;
    readonly TranslateTransform cursorLean = new TranslateTransform();
    readonly RotateTransform wingStretch = new RotateTransform(0,73,91);
    readonly RotateTransform[] tentacles = new RotateTransform[8];
    readonly Random random = new Random();
    readonly ScritchGesture scritches=new ScritchGesture();
    Path scritchHeart;
    enum Idle { Quiet, Curious, Stretch, Sleepy, Preen, FeatherShake, Sneak }
    Idle idle, lastIdle;
    double idleTime, idleDuration, nextIdle=4;
    readonly DispatcherTimer timer = new DispatcherTimer();
    readonly Forms.NotifyIcon tray = new Forms.NotifyIcon();
    System.Drawing.Icon corbieTrayIcon;
    readonly CpuWatch cpu=new CpuWatch();
    readonly TemperatureWatch temperature=new TemperatureWatch();
    readonly TemperatureTrend temperatureTrend=new TemperatureTrend();
    readonly System.Diagnostics.Stopwatch temperatureClock=System.Diagnostics.Stopwatch.StartNew();
    readonly DispatcherTimer temperatureTimer=new DispatcherTimer();
    CancellationTokenSource temperatureRequest;
    bool temperatureEnabled;
    MenuItem temperatureItem,temperatureStatus;
    Forms.ToolStripMenuItem trayTemperature,trayTemperatureStatus;
    readonly DispatcherTimer cpuTimer=new DispatcherTimer();
    bool cpuEnabled;
    MenuItem cpuItem,cpuStatus;
    Forms.ToolStripMenuItem trayCpu,trayCpuStatus;
    readonly Companion companion;
    readonly MoodBrain mood;
    readonly BreakCoach breakCoach;
    readonly BlackboardCalendar schoolCalendar;
    MenuItem quietItem;
    Forms.ToolStripMenuItem trayQuiet;
    MenuItem voiceItem;
    MenuItem voiceToneBirdItem, voiceToneNormalItem, voiceToneChirpyItem, voiceToneCroakItem;
    Forms.ToolStripMenuItem trayVoice;
    Forms.ToolStripMenuItem trayToneBird, trayToneNormal, trayToneChirpy, trayToneCroak;
    CancellationTokenSource visionRequest;
    bool looking, shuttingDown, farewellSaid;
    DispatcherTimer farewellTimer;
    readonly DispatcherTimer ambientVisionTimer=new DispatcherTimer();
    bool ambientVisionEnabled;
    byte[] lastScreenThumbnail;
    string lastVisionContext;
    DateTime lastVisionLook=DateTime.MinValue;
    MenuItem ambientVisionItem, ambientVisionStatus;
    Forms.ToolStripMenuItem trayAmbientVision, trayAmbientVisionStatus;
    readonly Ellipse seeingLight=new Ellipse { Width=12,Height=12,Fill=Brushes.Gold,Stroke=Brushes.White,StrokeThickness=2,Visibility=Visibility.Hidden };
    readonly Ellipse micLight=new Ellipse { Width=12,Height=12,Fill=Brushes.IndianRed,Stroke=Brushes.White,StrokeThickness=2,Visibility=Visibility.Hidden };
    readonly CorbieChat chat=new CorbieChat();
    CancellationTokenSource micRequest;
    MenuItem micItem,micStatus;
    Forms.ToolStripMenuItem trayMic,trayMicStatus;
    bool handsFreeEnabled;
    bool micDiagnosticShown;
    bool micTranscriptHintShown;
    DateTime lastMicLevelStatus=DateTime.MinValue;
    MenuItem handsFreeItem;
    Forms.ToolStripMenuItem trayHandsFree;
    Ellipse eye, glint;
    bool paused, dragging, octopusMode, autoPerch=true;
    IntPtr perchWindow;
    MenuItem perchItem;
    Forms.ToolStripMenuItem trayPerch;
    Ellipse octoEyeLeft, octoEyeRight;
    [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(out LASTINPUTINFO plii);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out NativeRect rect);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder name,int count);
    public enum UserNotificationState {
      QUNS_NOT_PRESENT = 1,
      QUNS_BUSY = 2,
      QUNS_RUNNING_D3D_FULL_SCREEN = 3,
      QUNS_PRESENTATION_MODE = 4,
      QUNS_ACCEPTS_NOTIFICATIONS = 5,
      QUNS_QUIET_TIME = 6,
      QUNS_APP = 7
    }
    [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out UserNotificationState state);
    [DllImport("user32.dll", SetLastError=true)] static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError=true)] static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    const int WM_CLIPBOARDUPDATE = 0x031D;
    TrinketItem inspectedTrinket;
    double trinketInspectTimer;
    Polygon trinketVisual;
    Polygon questScrap;
    Ellipse droppedFeather;
    DateTime lastPerchPersonalityUtc=DateTime.MinValue;
    double flashBadgeTimer;
    double perchHopScale=1;
    PerchKind perchMoodKind=PerchKind.Other;
    double mischiefChaseTimer;
    double mischiefChaseTx, mischiefChaseTy;
    double missionPoseTimer;
    double scrapBob;
    double droppedFeatherTimer;
    bool wearingAccessory;
    Brush originalEyeBrush;
    readonly NotificationEdge notificationEdge=new NotificationEdge();
    Ellipse moltBody, moltChest;
    readonly Path[] tentaclePaths=new Path[8];
    string nestEggPath;
    bool shipCelebratedThisSession;
    double notifProbeWait=6;
    bool isNightSleep;
    public static bool IsFullscreenOrPresentation() {
      try {
        UserNotificationState state;
        if(SHQueryUserNotificationState(out state)==0) {
          return state==UserNotificationState.QUNS_RUNNING_D3D_FULL_SCREEN || state==UserNotificationState.QUNS_PRESENTATION_MODE;
        }
      } catch { }
      return false;
    }
    static uint GetIdleTimeMs() {
      var lii=new LASTINPUTINFO { cbSize=(uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
      return GetLastInputInfo(out lii)?(uint)Environment.TickCount-lii.dwTime:0;
    }
    double ticks, hop;
    bool peckingArchive, isDragHovering;
    double peckPhase, offendedTimer;
    double peckingCulpritTimer, thermalAlertTimer;
    bool isOnBattery;
    readonly bool testing;
    readonly System.Diagnostics.Stopwatch animationClock=System.Diagnostics.Stopwatch.StartNew();
    double lastAnimationSeconds,fullscreenCheckCooldown;
    bool fullscreenOrPresentation,cpuChecking;
    bool isFlung;
    double flingVx, flingVy;
    readonly System.Diagnostics.Stopwatch flingClock=System.Diagnostics.Stopwatch.StartNew();
    struct DragSample { public double X, Y; public double Time; }
    readonly System.Collections.Generic.List<DragSample> dragSamples=new System.Collections.Generic.List<DragSample>();
    static readonly string[] flingLines={
      "DID YOU JUST FUCKING THROW ME?! You absolute walnut!",
      "I'm an apex corvid, not a bloody shimeji!",
      "Aerodynamics: ten out of ten. Your throwing arm: soggy. My dignity: gone.",
      "Gravity works! Congratulations, Isaac Newton. Now say sorry.",
      "My tail feathers are inside out! Proud of yourself, are you? ARE YOU?",
      "Do that again and I'm deleting System32. I won't. But I'll think about it very loudly.",
      "Wheeeee - OOF. Straight into the taskbar. Oh, bollocks.",
      "No peanuts, no safety briefing? Worst bloody airline in Europe!"
    };
    static readonly string[] octoFlingLines={
      "SPLAT. Suction cups failed. That landing was soggier than a week-old sandwich.",
      "Cephalopods are NOT built for ballistic flight, you great lummox!",
      "Invertebrate down! My dignity's been pureed. Ta very fucking much.",
      "Squelch. Eight tentacles and not one caught the landing. Embarrassing, the lot of us."
    };
    bool extractToFolder = true, revealExtractedFolder = true, confirmPreview;
    MenuItem extractToFolderItem, revealFolderItem, confirmPreviewItem;
    Forms.ToolStripMenuItem trayExtractToFolder, trayRevealFolder, trayConfirmPreview;
    MenuItem warnHoardItem;
    Forms.ToolStripMenuItem trayWarnHoard;
    Forms.ToolStripItem lastTrayClicked;
    public Pet(bool testing=false) {
      this.testing=testing;
      Title="Corbie - desktop raven";
      Icon=AppIcon.WindowImage;
      Width=180; Height=180; WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize;
      AllowsTransparency=true; Background=Brushes.Transparent; Topmost=true; ShowInTaskbar=false;
      AllowDrop=true;
      DragEnter+=Pet_DragEnter; DragOver+=Pet_DragOver; DragLeave+=Pet_DragLeave; Drop+=Pet_Drop;
      LocationChanged+=(s,e)=> {
        if(!dragging) return;
        double now=flingClock.Elapsed.TotalSeconds;
        dragSamples.Add(new DragSample { X=Left, Y=Top, Time=now });
        while(dragSamples.Count>8) dragSamples.RemoveAt(0);
      };
      Content=scene; var bodyTransforms=new TransformGroup(); bodyTransforms.Children.Add(facing); bodyTransforms.Children.Add(danceTilt); scene.RenderTransform=bodyTransforms; scene.Children.Add(bird); bird.RenderTransform=motion;
      // All artwork is original vector geometry; no external assets or network required.
      ShapePath("M 69,125 L 45,149 L 63,143 L 62,154 L 86,136 Z", "#25243B");
      moltBody=EllipseAt(62,70,65,72,"#25243B");
      moltChest=EllipseAt(82,83,40,52,"#32314B");
      ShapePath("M 74,86 C 43,90 48,128 78,137 C 69,120 94,107 74,86 Z", "#171829");
      ShapePath("M 63,105 Q 62,119 76,129", null, "#45425D", 2);
      EllipseAt(86,43,52,53,"#29283E");
      ShapePath("M 91,51 L 88,34 L 101,45 L 108,32 L 115,46 Z", "#29283E");
      ShapePath("M 130,61 Q 149,62 158,76 L 133,77 Z", "#D3A575");
      ShapePath("M 133,77 L 153,78 L 135,83 Z", "#986A57");
      bird.Children[8].RenderTransform=beakOpen;
      eye=EllipseAt(119,60,9,11,"#F8DDAB");
      originalEyeBrush=eye.Fill;
      glint=EllipseAt(123,62,3,4,"#FFFFFF");
      ShapePath("M 92,136 L 90,147 L 81,148 M 90,147 L 99,149", null, "#B99581", 3);
      leftFootVisual=(Path)bird.Children[11]; leftFootVisual.RenderTransform=leftFoot;
      ShapePath("M 110,136 L 109,147 L 103,150 M 109,147 L 119,149", null, "#B99581", 3);
      rightFootVisual=(Path)bird.Children[12]; rightFootVisual.RenderTransform=rightFoot;
      var headTransforms=new TransformGroup(); headTransforms.Children.Add(headTilt); headTransforms.Children.Add(cursorTilt); headTransforms.Children.Add(cursorLean);
      GroupParts(5,6).RenderTransform=headTransforms;
      GroupParts(3,2).RenderTransform=wingStretch;
      // Torso, tail, wing and head breathe above stationary feet. Whole-bird
      // motion remains available for walking, hopping, dancing and flight.
      var restingTorso=GroupParts(0,5); restingTorso.RenderTransform=bodyRest; BuildArticulation(restingTorso);
      Panel.SetZIndex(rightFootVisual,-1);
      BuildOctopus(); scene.Children.Add(octopus); octopus.RenderTransform=squish;BuildShowAndTell();
      trinketVisual=new Polygon {
        Points=new PointCollection { new Point(0,5), new Point(5,0), new Point(10,5), new Point(5,10) },
        Fill=Brushes.Gold, Stroke=Brushes.White, StrokeThickness=1, Visibility=Visibility.Hidden
      };
      Canvas.SetLeft(trinketVisual,148); Canvas.SetTop(trinketVisual,68); scene.Children.Add(trinketVisual);
      questScrap=new Polygon {
        Points=new PointCollection { new Point(0,1), new Point(18,0), new Point(20,12), new Point(2,14) },
        Fill=(SolidColorBrush)new BrushConverter().ConvertFromString("#F5E6C8"),
        Stroke=(SolidColorBrush)new BrushConverter().ConvertFromString("#8B6914"),
        StrokeThickness=1.4, Visibility=Visibility.Hidden, Opacity=0.98
      };
      Canvas.SetLeft(questScrap,126); Canvas.SetTop(questScrap,70); scene.Children.Add(questScrap);
      droppedFeather=new Ellipse {
        Width=10, Height=16,
        Fill=(SolidColorBrush)new BrushConverter().ConvertFromString("#3A3558"),
        Stroke=(SolidColorBrush)new BrushConverter().ConvertFromString("#C9B8FF"),
        StrokeThickness=1, Visibility=Visibility.Hidden, Opacity=0.9
      };
      Canvas.SetLeft(droppedFeather,90); Canvas.SetTop(droppedFeather,120); scene.Children.Add(droppedFeather);
      scritchHeart=new Path { Data=Geometry.Parse("M 8,5 C 5,-2 0,0 1,6 C 2,10 8,15 8,15 C 8,15 14,10 15,6 C 16,0 11,-2 8,5 Z"),
        Fill=ColorBrush("#EC8791"),Stroke=ColorBrush("#F7D8D8"),StrokeThickness=0.8,Visibility=Visibility.Hidden };
      Canvas.SetLeft(scritchHeart,132); Canvas.SetTop(scritchHeart,26); scene.Children.Add(scritchHeart);
      mood=new MoodBrain(testing); breakCoach=new BreakCoach(testing); schoolCalendar=new BlackboardCalendar(testing); companion=new Companion(this); companion.MoodRemark=mood.Remark; musicBrain=new MusicBrain(testing); InitializeSchool(); InitializeWeather(); InitializeRoost(); InitializeCheckiday(); InitializeDiscoveries(); InitializeNerves(); InitializeScreenNerves(); InitializeFlights(); InitializePerchIntent(); InitializeDesktopForage(); InitializeBirdTunes();
      Canvas.SetLeft(seeingLight,145); Canvas.SetTop(seeingLight,35); scene.Children.Add(seeingLight);
      Canvas.SetLeft(micLight,145); Canvas.SetTop(micLight,53); scene.Children.Add(micLight);
      Canvas.SetLeft(musicLight,30); Canvas.SetTop(musicLight,45); scene.Children.Add(musicLight);
      var menu=new ContextMenu();
      var nestItem=new MenuItem { Header="Open Corbie's Nest..." }; nestItem.Click+=(s,e)=>OpenCorbiePanel();
      temperatureItem=new MenuItem { Header="Watch CPU temperature",IsCheckable=true,StaysOpenOnClick=true }; temperatureItem.Click+=(s,e)=>SetTemperatureWatch(temperatureItem.IsChecked); menu.Items.Add(temperatureItem);
      temperatureStatus=new MenuItem { Header="CPU temperature: watcher off",IsEnabled=false }; menu.Items.Add(temperatureStatus);
      cpuItem=new MenuItem { Header="Watch CPU load",IsCheckable=true,StaysOpenOnClick=true }; cpuItem.Click+=(s,e)=>SetCpuWatch(cpuItem.IsChecked); menu.Items.Add(cpuItem);
      cpuStatus=new MenuItem { Header="CPU load: watcher off",IsEnabled=false }; menu.Items.Add(cpuStatus);
      danceItem=new MenuItem { Header="Dance to music",IsCheckable=true,StaysOpenOnClick=true }; danceItem.Click+=(s,e)=>SetDance(danceItem.IsChecked); menu.Items.Add(danceItem);
      musicTasteItem=new MenuItem { Header="Let Corbie learn my music",IsCheckable=true,StaysOpenOnClick=true }; musicTasteItem.Click+=(s,e)=>SetMusicTaste(musicTasteItem.IsChecked); menu.Items.Add(musicTasteItem);
      musicTasteStatus=new MenuItem { Header="Music memory: off",IsEnabled=false }; menu.Items.Add(musicTasteStatus);
      var musicOpinionItem=new MenuItem { Header="What do you think of this song?" }; musicOpinionItem.Click+=(s,e)=>companion.Say(musicBrain.Opinion()); menu.Items.Add(musicOpinionItem);
      var musicSummaryItem=new MenuItem { Header="Summarize your music opinions" }; musicSummaryItem.Click+=(s,e)=>companion.Say(musicBrain.Summary()); menu.Items.Add(musicSummaryItem);
      var lookItem=new MenuItem { Header="Look at my screen (local)" }; lookItem.Click+=async (s,e)=>await LookAtScreen();
      var cancelLook=new MenuItem { Header="Cancel looking" }; cancelLook.Click+=(s,e)=>CancelLooking();
      ambientVisionItem=new MenuItem { Header="Ambient screen watch", IsCheckable=true, StaysOpenOnClick=true }; ambientVisionItem.Click+=(s,e)=>SetAmbientVision(ambientVisionItem.IsChecked);
      ambientVisionStatus=new MenuItem { Header="Screen watch: watcher off", IsEnabled=false };
      menu.Items.Add(lookItem); menu.Items.Add(cancelLook); menu.Items.Add(ambientVisionItem); menu.Items.Add(ambientVisionStatus); menu.Items.Add(new Separator());
      var peckItem=new MenuItem { Header="Peck open an archive..." }; peckItem.Click+=(s,e)=>PromptPeckOpenArchive();
      extractToFolderItem=new MenuItem { Header="Extract to folder (Peck Open)", IsCheckable=true, IsChecked=true, StaysOpenOnClick=true };
      extractToFolderItem.Click+=(s,e)=>SetExtractToFolder(extractToFolderItem.IsChecked);
      revealFolderItem=new MenuItem { Header="Reveal extracted folder", IsCheckable=true, IsChecked=true, StaysOpenOnClick=true };
      revealFolderItem.Click+=(s,e)=>SetRevealFolder(revealFolderItem.IsChecked);
      confirmPreviewItem=new MenuItem { Header="Confirm preview before opening", IsCheckable=true, IsChecked=false, StaysOpenOnClick=true };
      confirmPreviewItem.Click+=(s,e)=>SetConfirmPreview(confirmPreviewItem.IsChecked);
      warnHoardItem=new MenuItem { Header="Warn before hoarding (this session)", IsCheckable=true, IsChecked=RecycleBinHoard.WarnBeforeHoarding, StaysOpenOnClick=true };
      warnHoardItem.Click+=(s,e)=>SetWarnBeforeHoarding(warnHoardItem.IsChecked);
      var stashItem=new MenuItem { Header="Check hoard stash" };
      stashItem.Click+=(s,e)=>companion.StashRemark(octopusMode);
      menu.Items.Add(peckItem); menu.Items.Add(extractToFolderItem); menu.Items.Add(revealFolderItem); menu.Items.Add(confirmPreviewItem); menu.Items.Add(warnHoardItem); menu.Items.Add(stashItem); menu.Items.Add(new Separator());
      var talkItem=new MenuItem { Header="Talk to me" }; talkItem.Click+=(s,e)=>companion.Talk(octopusMode);
      micItem=new MenuItem { Header="Talk to Corbie (mic)" }; micItem.Click+=async (s,e)=>await ToggleMicTalk();
      micStatus=new MenuItem { Header="Mic: off",IsEnabled=false };
      handsFreeItem=new MenuItem { Header="Keep mic on for conversation",IsCheckable=true }; handsFreeItem.Click+=async (s,e)=>await SetHandsFree(handsFreeItem.IsChecked);
      var clearChatItem=new MenuItem { Header="Clear mic chat" }; clearChatItem.Click+=(s,e)=> { chat.Clear(); companion.Say("Conversation cleared. My opinions, sadly, remain."); };
      var jokeItem=new MenuItem { Header="Tell me a joke" }; jokeItem.Click+=(s,e)=>companion.Joke();
      var treatItem=new MenuItem { Header="Give Corbie a treat" }; treatItem.Click+=(s,e)=>GiveTreat();
      quietItem=new MenuItem { Header="Quiet mode", IsCheckable=true, StaysOpenOnClick=true }; quietItem.Click+=(s,e)=>SetQuiet(quietItem.IsChecked);
      voiceItem=new MenuItem { Header="Voice on", IsCheckable=true, StaysOpenOnClick=true }; voiceItem.Click+=(s,e)=>SetVoice(voiceItem.IsChecked);
      var voiceToneMenu=new MenuItem { Header="Voice tone" };
      voiceToneBirdItem=new MenuItem { Header="Bird (inquisitive creature)", IsCheckable=true, StaysOpenOnClick=true };
      voiceToneBirdItem.Click+=(s,e)=>SetVoiceTone(VoiceTone.Bird);
      voiceToneNormalItem=new MenuItem { Header="Normal (standard human)", IsCheckable=true, StaysOpenOnClick=true };
      voiceToneNormalItem.Click+=(s,e)=>SetVoiceTone(VoiceTone.Normal);
      voiceToneChirpyItem=new MenuItem { Header="Chirpy (high & light)", IsCheckable=true, StaysOpenOnClick=true };
      voiceToneChirpyItem.Click+=(s,e)=>SetVoiceTone(VoiceTone.Chirpy);
      voiceToneCroakItem=new MenuItem { Header="Croak (gravelly raven)", IsCheckable=true, StaysOpenOnClick=true };
      voiceToneCroakItem.Click+=(s,e)=>SetVoiceTone(VoiceTone.Croak);
      voiceToneMenu.Items.Add(voiceToneBirdItem);
      voiceToneMenu.Items.Add(voiceToneNormalItem);
      voiceToneMenu.Items.Add(voiceToneChirpyItem);
      voiceToneMenu.Items.Add(voiceToneCroakItem);
      menu.Items.Add(voiceItem);
      menu.Items.Add(voiceToneMenu);
      menu.Items.Add(talkItem); menu.Items.Add(micItem); menu.Items.Add(handsFreeItem); menu.Items.Add(micStatus); menu.Items.Add(clearChatItem); menu.Items.Add(jokeItem); menu.Items.Add(treatItem); menu.Items.Add(quietItem); menu.Items.Add(new Separator());
      var pauseItem=new MenuItem { Header="Pause / resume" }; pauseItem.Click+=(s,e)=>TogglePause();
      var homeItem=new MenuItem { Header="Come back to the corner" }; homeItem.Click+=(s,e)=>Home();
      var quitItem=new MenuItem { Header="Goodnight, Corbie" }; quitItem.Click+=(s,e)=>RequestGoodbye();
      perchItem=new MenuItem { Header="Follow the active window", IsCheckable=true, IsChecked=true, StaysOpenOnClick=true }; perchItem.Click+=(s,e)=>SetPerching(perchItem.IsChecked);
      wanderItem=new MenuItem { Header="Wander around", IsCheckable=true, IsChecked=true, StaysOpenOnClick=true }; wanderItem.Click+=(s,e)=>SetWandering(wanderItem.IsChecked);
      menu.Items.Add(perchItem); menu.Items.Add(wanderItem); menu.Items.Add(pauseItem); menu.Items.Add(homeItem); menu.Items.Add(new Separator()); menu.Items.Add(quitItem);
      menu.Items.Clear(); menu.Items.Add(nestItem); menu.Items.Add(new Separator());
      menu.Items.Add(talkItem); menu.Items.Add(micItem); menu.Items.Add(handsFreeItem); menu.Items.Add(danceItem); menu.Items.Add(lookItem);
      var putTrinketAwayItem=new MenuItem { Header="Put trinket away" };putTrinketAwayItem.Click+=(s,e)=>PutTrinketAway();menu.Items.Add(putTrinketAwayItem);
      var wisdomItem=new MenuItem { Header="Give me some wisdom" };wisdomItem.Click+=(s,e)=>GiveSageWisdom();menu.Items.Add(wisdomItem);
      cursorFollowItem=new MenuItem { Header="Follow cursor in flight",IsCheckable=true };
      cursorFollowItem.Click+=(s,e)=>SetCursorFollow(cursorFollowItem.IsChecked);menu.Items.Add(cursorFollowItem);
      var flockItem=new MenuItem {Header="Summon / dismiss the flock"};flockItem.Click+=(s,e)=>ToggleFlockShow();menu.Items.Add(flockItem);
      var tiktokMenu=new MenuItem {Header="TikTok tricks"};
      var tiktokOpen=new MenuItem {Header="Open TikTok panel"};tiktokOpen.Click+=(s,e)=>OpenNestPage("TikTok");tiktokMenu.Items.Add(tiktokOpen);
      var tiktokHello=new MenuItem {Header="Wave + introduce yourself"};tiktokHello.Click+=(s,e)=>StartTikTokTake(true);tiktokMenu.Items.Add(tiktokHello);
      var tiktokWave=new MenuItem {Header="Just wave"};tiktokWave.Click+=(s,e)=>StartTikTokTake(false);tiktokMenu.Items.Add(tiktokWave);
      var tiktokBow=new MenuItem {Header="Take a bow"};tiktokBow.Click+=(s,e)=>StartDirectorTake(TikTokDirector.Bow(),tiktokCue);tiktokMenu.Items.Add(tiktokBow);
      var tiktokRepeat=new MenuItem {Header="Repeat last take"};tiktokRepeat.Click+=(s,e)=>RepeatDirectorTake();tiktokMenu.Items.Add(tiktokRepeat);
      var tiktokStop=new MenuItem {Header="Stop the trick"};tiktokStop.Click+=(s,e)=> {CancelViewerMail(false);StopTikTokTake();StopFlockShow();};tiktokMenu.Items.Add(tiktokStop);menu.Items.Add(tiktokMenu);
      menu.Items.Add(new Separator()); menu.Items.Add(pauseItem); menu.Items.Add(homeItem);
      latestVersionItem=new MenuItem {Header="Launch latest version"};latestVersionItem.Click+=(s,e)=>LaunchLatestVersion();
      menu.Items.Add(new Separator()); menu.Items.Add(latestVersionItem);menu.Items.Add(quitItem); ContextMenu=menu;
      MouseLeftButtonDown+=(s,e)=> {
        CancelViewerMail(false);
        StopTikTokTake();
        StopFlockShow();
        StopCursorFollow();
        if(attentionBid.Active && CanInitiate() && attentionBid.Accept()) { e.Handled=true; return; }
        attentionBid.Cancel();
        StopWalking(); ResetIdle();
        if(e.ClickCount==2) {
          mood.Poke();
          if(!paused) {
            hop=1;
            CreatureFoley.Play(octopusMode?FoleyKind.SuctionPop:FoleyKind.Caw,companion.Quiet);
            if(isNightSleep) {
              companion.Say("Yawn... what? It's "+DateTime.Now.ToString("h:mm tt")+". The sun isn't even up yet, you night owl.");
            } else {
              companion.Say(octopusMode?"You poked the cephalopod. Are you proud of yourself?":"Boing. Pure athletic perfection. Try not to poke so hard.");
            }
          }
          return;
        }
        CreatureFoley.Play(octopusMode?FoleyKind.SuctionPop:FoleyKind.Clack,companion.Quiet);
        if(isFlung) { isFlung=false; flingVx=flingVy=0; }
        dragSamples.Clear();
        CancelBirdFlight(false); double oldLeft=Left,oldTop=Top; dragging=true;
        try { DragMove(); }
        finally {
          dragging=false;
          double now=flingClock.Elapsed.TotalSeconds;
          bool flingTriggered=false;
          if(!paused && dragSamples.Count>=2) {
            var newest=dragSamples[dragSamples.Count-1];
            var oldest=dragSamples[0];
            double timeSinceLastMove=now-newest.Time;
            double dt=newest.Time-oldest.Time;
            if(timeSinceLastMove<=0.10 && dt>0.01) {
              double vx=(newest.X-oldest.X)/dt;
              double vy=(newest.Y-oldest.Y)/dt;
              double speed=Math.Sqrt(vx*vx+vy*vy);
              if(speed>=380) {
                StartFling(vx,vy);
                flingTriggered=true;
              }
            }
          }
          if(!flingTriggered) {
            bool moved=Math.Abs(Left-oldLeft)+Math.Abs(Top-oldTop)>3;
            if(moved) {
              SetPerching(false);
              Clamp();
              if(!paused && !companion.Quiet)
                companion.Say("Relocated again? Fine, I'll judge your workflow from over here.");
            } else {
              Clamp();
              if(!paused && !companion.Quiet)
                companion.Talk(octopusMode);
            }
          }
        }
      };
      KeyDown+=(s,e)=> { if(e.Key==Key.Escape) RequestGoodbye(); if(e.Key==Key.Space) TogglePause(); };
      ToolTip="Corbie\nStroke over my head for scritches - drag to perch or fling\nRight-click for quick actions or Corbie's Nest";
      corbieTrayIcon=AppIcon.CreateTrayIcon();tray.Icon=corbieTrayIcon; tray.Text="Corbie - desktop raven";
      var trayMenu=new Forms.ContextMenuStrip();
      var trayNest=new Forms.ToolStripMenuItem("Open Corbie's Nest..."); trayNest.Click+=(s,e)=>Dispatcher.Invoke(new Action(OpenCorbiePanel));
      trayMenu.ItemClicked+=(s,e)=> { lastTrayClicked=e.ClickedItem; };
      trayMenu.Closing+=OnTrayMenuClosing;
      trayTemperature=new Forms.ToolStripMenuItem("Watch CPU temperature"); trayTemperature.Click+=(s,e)=>SetTemperatureWatch(!temperatureEnabled); trayMenu.Items.Add(trayTemperature);
      trayTemperatureStatus=new Forms.ToolStripMenuItem("CPU temperature: watcher off") { Enabled=false }; trayMenu.Items.Add(trayTemperatureStatus);
      trayCpu=new Forms.ToolStripMenuItem("Watch CPU load"); trayCpu.Click+=(s,e)=>SetCpuWatch(!cpuEnabled); trayMenu.Items.Add(trayCpu);
      trayCpuStatus=new Forms.ToolStripMenuItem("CPU load: watcher off") { Enabled=false }; trayMenu.Items.Add(trayCpuStatus);
      trayDance=new Forms.ToolStripMenuItem("Dance to music"); trayDance.Click+=(s,e)=>SetDance(!danceEnabled); trayMenu.Items.Add(trayDance);
      trayMusicTaste=new Forms.ToolStripMenuItem("Let Corbie learn my music"); trayMusicTaste.Click+=(s,e)=>Dispatcher.Invoke(new Action(()=>SetMusicTaste(!musicTasteEnabled))); trayMenu.Items.Add(trayMusicTaste);
      trayMusicTasteStatus=new Forms.ToolStripMenuItem("Music memory: off") { Enabled=false }; trayMenu.Items.Add(trayMusicTasteStatus);
      trayMenu.Items.Add("What do you think of this song?",null,(s,e)=>Dispatcher.Invoke(new Action(()=>companion.Say(musicBrain.Opinion()))));
      trayMenu.Items.Add("Summarize your music opinions",null,(s,e)=>Dispatcher.Invoke(new Action(()=>companion.Say(musicBrain.Summary()))));
      trayMenu.Items.Add("Look at my screen (local)",null,async (s,e)=>await LookAtScreen());
      trayMenu.Items.Add("Cancel looking",null,(s,e)=>CancelLooking());
      trayAmbientVision=new Forms.ToolStripMenuItem("Ambient screen watch"); trayAmbientVision.Click+=(s,e)=>Dispatcher.Invoke(new Action(()=>SetAmbientVision(!ambientVisionEnabled))); trayMenu.Items.Add(trayAmbientVision);
      trayAmbientVisionStatus=new Forms.ToolStripMenuItem("Screen watch: watcher off") { Enabled=false }; trayMenu.Items.Add(trayAmbientVisionStatus);
      trayMenu.Items.Add("Peck open an archive...",null,(s,e)=>Dispatcher.Invoke(new Action(PromptPeckOpenArchive)));
      trayExtractToFolder=new Forms.ToolStripMenuItem("Extract to folder (Peck Open)") { Checked=true, CheckOnClick=true };
      trayExtractToFolder.Click+=(s,e)=>Dispatcher.Invoke(new Action(()=>SetExtractToFolder(trayExtractToFolder.Checked)));
      trayRevealFolder=new Forms.ToolStripMenuItem("Reveal extracted folder") { Checked=true, CheckOnClick=true };
      trayRevealFolder.Click+=(s,e)=>Dispatcher.Invoke(new Action(()=>SetRevealFolder(trayRevealFolder.Checked)));
      trayConfirmPreview=new Forms.ToolStripMenuItem("Confirm preview before opening") { Checked=false, CheckOnClick=true };
      trayConfirmPreview.Click+=(s,e)=>Dispatcher.Invoke(new Action(()=>SetConfirmPreview(trayConfirmPreview.Checked)));
      trayWarnHoard=new Forms.ToolStripMenuItem("Warn before hoarding (this session)") { Checked=RecycleBinHoard.WarnBeforeHoarding, CheckOnClick=true };
      trayWarnHoard.Click+=(s,e)=>Dispatcher.Invoke(new Action(()=>SetWarnBeforeHoarding(trayWarnHoard.Checked)));
      trayMenu.Items.Add(trayExtractToFolder); trayMenu.Items.Add(trayRevealFolder); trayMenu.Items.Add(trayConfirmPreview); trayMenu.Items.Add(trayWarnHoard);
      trayMenu.Items.Add("Check hoard stash",null,(s,e)=>Dispatcher.Invoke(new Action(()=>companion.StashRemark(octopusMode))));
      trayVoice=new Forms.ToolStripMenuItem("Voice on"); trayVoice.Click+=(s,e)=>Dispatcher.Invoke(new Action(()=>SetVoice(!companion.VoiceEnabled))); trayMenu.Items.Add(trayVoice);
      var trayVoiceTone=new Forms.ToolStripMenuItem("Voice tone");
      trayVoiceTone.DropDownItemClicked+=(s,e)=>{ lastTrayClicked=e.ClickedItem; };
      trayToneBird=new Forms.ToolStripMenuItem("Bird (inquisitive creature)");
      trayToneBird.Click+=(s,e)=>Dispatcher.Invoke(new Action(()=>SetVoiceTone(VoiceTone.Bird)));
      trayToneNormal=new Forms.ToolStripMenuItem("Normal (standard human)");
      trayToneNormal.Click+=(s,e)=>Dispatcher.Invoke(new Action(()=>SetVoiceTone(VoiceTone.Normal)));
      trayToneChirpy=new Forms.ToolStripMenuItem("Chirpy (high & light)");
      trayToneChirpy.Click+=(s,e)=>Dispatcher.Invoke(new Action(()=>SetVoiceTone(VoiceTone.Chirpy)));
      trayToneCroak=new Forms.ToolStripMenuItem("Croak (gravelly raven)");
      trayToneCroak.Click+=(s,e)=>Dispatcher.Invoke(new Action(()=>SetVoiceTone(VoiceTone.Croak)));
      trayVoiceTone.DropDownItems.Add(trayToneBird);
      trayVoiceTone.DropDownItems.Add(trayToneNormal);
      trayVoiceTone.DropDownItems.Add(trayToneChirpy);
      trayVoiceTone.DropDownItems.Add(trayToneCroak);
      trayMenu.Items.Add(trayVoiceTone);
      trayMenu.Items.Add("Talk to me",null,(s,e)=>Dispatcher.Invoke(new Action(()=>companion.Talk(octopusMode))));
      trayMic=new Forms.ToolStripMenuItem("Talk to Corbie (mic)"); trayMic.Click+=async (s,e)=>await ToggleMicTalk(); trayMenu.Items.Add(trayMic);
      trayHandsFree=new Forms.ToolStripMenuItem("Keep mic on for conversation"); trayHandsFree.Click+=async (s,e)=>await SetHandsFree(!handsFreeEnabled); trayMenu.Items.Add(trayHandsFree);
      trayMicStatus=new Forms.ToolStripMenuItem("Mic: off") { Enabled=false }; trayMenu.Items.Add(trayMicStatus);
      trayMenu.Items.Add("Clear mic chat",null,(s,e)=>Dispatcher.Invoke(new Action(()=> { chat.Clear(); companion.Say("Conversation cleared. My opinions, sadly, remain."); })));
      trayMenu.Items.Add("Tell me a joke",null,(s,e)=>Dispatcher.Invoke(new Action(companion.Joke)));
      trayMenu.Items.Add("Give Corbie a treat",null,(s,e)=>Dispatcher.Invoke(new Action(GiveTreat)));
      trayQuiet=new Forms.ToolStripMenuItem("Quiet mode"); trayQuiet.Click+=(s,e)=>Dispatcher.Invoke(new Action(()=>SetQuiet(!companion.Quiet))); trayMenu.Items.Add(trayQuiet);
      trayMenu.Items.Add(new Forms.ToolStripSeparator());
      trayWander=new Forms.ToolStripMenuItem("Wander around") { Checked=true }; trayWander.Click+=(s,e)=>Dispatcher.Invoke(new Action(()=>SetWandering(!wandering))); trayMenu.Items.Add(trayWander);
      trayPerch=new Forms.ToolStripMenuItem("Follow the active window") { Checked=true }; trayPerch.Click+=(s,e)=>Dispatcher.Invoke(new Action(()=>SetPerching(!autoPerch))); trayMenu.Items.Add(trayPerch);
      trayMenu.Items.Add("Pause / resume",null,(s,e)=>Dispatcher.Invoke(new Action(TogglePause)));
      trayMenu.Items.Add("Bring Corbie home",null,(s,e)=>Dispatcher.Invoke(new Action(Home)));
      trayMenu.Items.Add("Goodnight, Corbie",null,(s,e)=>Dispatcher.Invoke(new Action(RequestGoodbye)));
      trayMenu.Items.Clear(); trayMenu.Items.Add(trayNest); trayMenu.Items.Add(new Forms.ToolStripSeparator());
      trayMenu.Items.Add("Note the game I'm playing",null,(s,e)=>Dispatcher.Invoke(new Action(()=>companion.Say(NoteCurrentGame()))));
      trayMenu.Items.Add("Roost report",null,(s,e)=>Dispatcher.Invoke(new Action(()=>companion.Say(SpeakChronicle(true)))));
      trayMenu.Items.Add("Talk to me",null,(s,e)=>Dispatcher.Invoke(new Action(()=>companion.Talk(octopusMode))));
      trayMenu.Items.Add(trayMic); trayMenu.Items.Add(trayHandsFree); trayMenu.Items.Add(trayDance);
      trayCursorFollow=new Forms.ToolStripMenuItem("Follow cursor in flight");
      trayCursorFollow.Click+=(s,e)=>Dispatcher.Invoke(new Action(()=>SetCursorFollow(!cursorFollowEnabled)));trayMenu.Items.Add(trayCursorFollow);
      trayMenu.Items.Add("Look at my screen (local)",null,async (s,e)=>await LookAtScreen());
      trayMenu.Items.Add("Summon / dismiss the flock",null,(s,e)=>Dispatcher.Invoke(new Action(ToggleFlockShow)));
      trayMenu.Items.Add("TikTok tricks...",null,(s,e)=>Dispatcher.Invoke(new Action(()=>OpenNestPage("TikTok"))));
      trayMenu.Items.Add("Stop TikTok trick",null,(s,e)=>Dispatcher.Invoke(new Action(()=> {CancelViewerMail(false);StopTikTokTake();StopFlockShow();})));
      trayMenu.Items.Add(new Forms.ToolStripSeparator());
      trayMenu.Items.Add("Pause / resume",null,(s,e)=>Dispatcher.Invoke(new Action(TogglePause)));
      trayMenu.Items.Add("Bring Corbie home",null,(s,e)=>Dispatcher.Invoke(new Action(Home)));
      trayMenu.Items.Add(new Forms.ToolStripSeparator()); trayMenu.Items.Add("Goodnight, Corbie",null,(s,e)=>Dispatcher.Invoke(new Action(RequestGoodbye)));
      trayLatestVersion=new Forms.ToolStripMenuItem("Launch latest version");trayLatestVersion.Click+=(s,e)=>Dispatcher.Invoke(new Action(LaunchLatestVersion));
      trayMenu.Items.Insert(trayMenu.Items.Count-1,trayLatestVersion);
      tray.ContextMenuStrip=trayMenu; tray.DoubleClick+=(s,e)=>Dispatcher.Invoke(new Action(OpenCorbiePanel)); tray.Visible=true;
      UpdateVoiceToneChecks(PiperVoice.Tone);
      companion.VoiceUnavailable+=()=> { voiceItem.IsChecked=false; trayVoice.Checked=false; };
      isOnBattery=Forms.SystemInformation.PowerStatus.PowerLineStatus==Forms.PowerLineStatus.Offline;
      ApplyPowerInterval();
      Microsoft.Win32.SystemEvents.PowerModeChanged+=OnPowerModeChanged;
      Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=OnDisplaySettingsChanged;
      timer.Tick+=(s,e)=>Animate();
      cpuTimer.Interval=TimeSpan.FromSeconds(2); cpuTimer.Tick+=(s,e)=>CheckCpu();
      temperatureTimer.Interval=TimeSpan.FromSeconds(3); temperatureTimer.Tick+=async (s,e)=>await CheckTemperature();
      ambientVisionTimer.Tick+=async (s,e)=>await CheckAmbientScreen();
      musicBrainTimer.Interval=TimeSpan.FromSeconds(4); musicBrainTimer.Tick+=async (s,e)=>await CheckMusicBrain();
      Loaded+=(s,e)=> {
        if(!testing && mining==null) mining=new MiningControl();
        // Restore after the menu, tray controls, and watcher timer exist.
        if(roost!=null && roost.Data.AmbientVisionEnabled) SetAmbientVision(true);
        Home(); SetPerching(true); timer.Start();
        try {
          var helper=new WindowInteropHelper(this);
          var source=HwndSource.FromHwnd(helper.Handle);
          if(source!=null) {
            source.AddHook(WndProc);
            AddClipboardFormatListener(helper.Handle);
          }
        } catch { }
        if(!testing) {
          string continuity=nest!=null?nest.ContinuityGreeting(octopusMode,roost!=null && roost.Data.JournalEnabled):null;
          companion.Say(continuity??"Corbie here. I brought feathers, opinions, and absolutely no respect for this bloody taskbar.");
          StartSchool();
          StartSecurityWatch();
        }
      };
      Closing+=(s,e)=> {
        if(lessonNotebook!=null && lessonNotebook.IsRecording) {
          e.Cancel=true; lessonNotebook.Show(); lessonNotebook.Activate();
          companion.SaySilent("Finish the lesson recording before I leave. The audio deserves its ending.");
          return;
        }
        shuttingDown=true;
      };
      IsVisibleChanged+=(s,e)=> {if(!IsVisible) {CancelViewerMail(false);StopTikTokTake(false);StopFlockShow();}SyncNestPetVisibility(IsVisible && WindowState!=WindowState.Minimized);};
      StateChanged+=(s,e)=> {if(WindowState==WindowState.Minimized) {CancelViewerMail(false);StopTikTokTake(false);StopFlockShow();}SyncNestPetVisibility(IsVisible && WindowState!=WindowState.Minimized);};
      Closed+=(s,e)=> {
        shuttingDown=true;
        CancelViewerMail(false);StopTikTokTake(false);
        StopFlockShow();
        CloseCorbiePanel();
        if(farewellTimer!=null) farewellTimer.Stop();
        if(forageMarker!=null) forageMarker.Close();
        StopBrowserTask();
        CancelSageThinking();
        CancelHolidayRequest();
        CancelDiscovery();
        if(cinemaPicker!=null)cinemaPicker.Close();
        if(habitat!=null) habitat.Dispose();
        BirdTunePlayer.Stop();
        CancelWeather();
        ResetScreenNerves();
        SaveQuietPerches();
        if(mining!=null) mining.Dispose();
        if(lessonNotebook!=null) lessonNotebook.Close();
        try {
          var helper=new WindowInteropHelper(this);
          RemoveClipboardFormatListener(helper.Handle);
        } catch { }
        CloseRoost(); if(virusTotalRequest!=null) virusTotalRequest.Cancel(); securityTimer.Stop(); Microsoft.Win32.SystemEvents.PowerModeChanged-=OnPowerModeChanged; Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=OnDisplaySettingsChanged; CancelMicTalk(); CancelTemperature(); temperatureTimer.Stop(); temperature.Dispose(); CancelLooking(); ambientVisionTimer.Stop(); CancelMusicBrain(); musicBrainTimer.Stop(); musicBrain.Dispose(); CancelSchoolRefresh(); schoolTimer.Stop(); schoolCalendar.Dispose(); breakCoach.Dispose(); mood.Dispose(); timer.Stop(); cpuTimer.Stop(); music.Dispose(); companion.Dispose(); tray.Visible=false; tray.Dispose();if(corbieTrayIcon!=null)corbieTrayIcon.Dispose();
      };
    }
    void OnDisplaySettingsChanged(object sender,EventArgs e) {
      if(shuttingDown) return;
      Dispatcher.BeginInvoke(new Action(()=> { if(!shuttingDown) {StopFlockShow();Clamp();} }));
    }
    public void Summon() {
      if(shuttingDown) return;
      if(WindowState==WindowState.Minimized) WindowState=WindowState.Normal;
      Show(); Home(); hop=1; Topmost=true;
      OpenCorbiePanel();
      if(corbiePanel!=null) { corbiePanel.Show(); corbiePanel.WindowState=WindowState.Normal; corbiePanel.Activate(); }
      companion.SaySilent("Already here. One Corbie is quite enough for this machine.");
    }
    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
      if(msg==WM_CLIPBOARDUPDATE) {
        OnClipboardUpdate();
      }
      return IntPtr.Zero;
    }
    void OnClipboardUpdate() {
      if(shuttingDown || paused || roost==null || !roost.Data.ClipboardEnabled) return;
      try {
        if(!Clipboard.ContainsText()) return;
        string text=Clipboard.GetText();
        if(string.IsNullOrWhiteSpace(text)) return;

        var cleanRes=ClipboardCrow.CleanUrl(text);
        if(cleanRes.WasCleaned) {
          string offeredText=text;
          OfferActionBid(()=> {
            try {
              if(!Clipboard.ContainsText() || Clipboard.GetText()!=offeredText) {
                companion.SaySilent("Your clipboard changed while I was considering that link. I left the newer copy alone.");
                return;
              }
              Clipboard.SetText(cleanRes.CleanedUrl);
              companion.Say("I pecked "+cleanRes.StrippedCount+" tracking parameter"+(cleanRes.StrippedCount>1?"s":"")+" off that link. Clean link copied to your clipboard.");
              CreatureFoley.Play(FoleyKind.Clack,companion.Quiet);
            } catch { }
          });
          return;
        }

        var color=ClipboardCrow.DetectHexColor(text);
        if(color.HasValue) {
          flashBadgeTimer=3.5;
          if(eye!=null) eye.Fill=new SolidColorBrush(color.Value);
          if(!companion.Quiet && !companion.Visible) {
            companion.SaySilent("Copied color "+text.Trim()+". Eye color tuned.");
          }
          return;
        }

        var formattedJson=ClipboardCrow.TryFormatJson(text);
        if(formattedJson!=null) {
          string offeredText=text;
          OfferActionBid(()=> {
            try {
              if(!Clipboard.ContainsText() || Clipboard.GetText()!=offeredText) {
                companion.SaySilent("Your clipboard changed while I was considering that JSON. I left the newer copy alone.");
                return;
              }
              Clipboard.SetText(formattedJson);
              companion.Say("Indented that raw JSON for you. Much less offensive to look at.");
              CreatureFoley.Play(FoleyKind.Clack,companion.Quiet);
            } catch { }
          });
          return;
        }
      } catch { }
    }
    void ShowTrinketInspect(TrinketItem item) {
      inspectedTrinket=item;
      trinketInspectTimer=4.0;
      if(trinketVisual!=null && item!=null) {
        try {
          trinketVisual.Fill=(SolidColorBrush)new BrushConverter().ConvertFromString(item.ColorHex??"#FFD700");
          trinketVisual.Visibility=Visibility.Visible;
        } catch { }
      }
      StopWalking(); ResetIdle();
      CreatureFoley.Play(FoleyKind.Clack,companion.Quiet);
    }
    void StartEyeRestGlide() {
      var area=CurrentDesktopBounds();
      eyeRestCoach.StartGlide(Left,area.Top,area.Left+40,area.Right-Width-40);
      StopWalking(); ResetIdle();
      companion.Say("Look past my beak at something about 20 feet away. I'll keep the 20-second watch. Relax those retinas!");
      CreatureFoley.Play(FoleyKind.Whoosh,companion.Quiet);
    }
    void OnPowerModeChanged(object sender,Microsoft.Win32.PowerModeChangedEventArgs e) {
      if(e.Mode==Microsoft.Win32.PowerModes.StatusChange) {
        Dispatcher.BeginInvoke(new Action(UpdatePowerState));
      }
    }
    void UpdatePowerState() {
      bool battery=Forms.SystemInformation.PowerStatus.PowerLineStatus==Forms.PowerLineStatus.Offline;
      if(battery!=isOnBattery) {
        isOnBattery=battery;
        ApplyPowerInterval();
        if(!shuttingDown && !testing) {
          if(isOnBattery) companion.Say("Unplugged? We're on battery fumes now. I'm dropping to 15 FPS so your laptop doesn't croak before your work is saved.");
          else companion.Say("Sweet wall juice! AC power restored. Crank up the frames.");
        }
      }
    }
    void ApplyPowerInterval() {
      timer.Interval=TimeSpan.FromMilliseconds(isOnBattery?66:33);
    }
    void SetTemperatureStatus(string text) { temperatureStatus.Header=text; trayTemperatureStatus.Text=text; }
    void CancelTemperature() { if(temperatureRequest!=null) { temperatureRequest.Cancel(); temperatureRequest=null; } temperatureTrend.BreakContinuity(); }
    async void SetTemperatureWatch(bool enabled) {
      temperatureEnabled=enabled; temperatureItem.IsChecked=trayTemperature.Checked=enabled;
      CancelTemperature(); cpu.Reset();
      SetTemperatureStatus(enabled?"CPU temperature: connecting...":"CPU temperature: watcher off");
      if(enabled) { temperatureTimer.Start(); await CheckTemperature(); } else { temperatureTimer.Stop(); mood.SetTemperature(68); }
    }
    async Task CheckTemperature() {
      if(!temperatureEnabled || shuttingDown || temperatureRequest!=null) return;
      if(paused) { temperatureTrend.BreakContinuity(); SetTemperatureStatus("CPU temperature: paused"); return; }
      var request=new CancellationTokenSource(); temperatureRequest=request;
      try {
        var reading=await temperature.Read(request.Token);
        if(request.IsCancellationRequested || shuttingDown || paused || !temperatureEnabled) return;
        mood.SetTemperature(reading.Celsius);
        sensedTemperature=reading.Celsius; sensedTemperatureUtc=DateTime.UtcNow;
        SetTemperatureStatus("CPU: "+reading.Celsius.ToString("F1")+" C - "+reading.Name);
        bool inFullscreen=IsFullscreenOrPresentation();
        bool notify=!companion.Quiet && !inFullscreen && !looking && !dragging && micRequest==null && !ContextMenu.IsOpen && !tray.ContextMenuStrip.Visible && !companion.Visible;
        CulpritInfo culprit=null;
        if(reading.Celsius>=85 && notify) {
          var watchedWindow=perchWindow;
          culprit=await Task.Run(()=>ProcessTriage.FindCulprit(watchedWindow));
          request.Token.ThrowIfCancellationRequested();
        }
        string notice=temperatureTrend.Observe(reading.Celsius,temperatureClock.Elapsed.TotalSeconds,notify,culprit);
        if(notice!=null) {
          if(culprit!=null && nest!=null && !testing) nest.NoteHotApp(culprit.Name,roost!=null && roost.Data.JournalEnabled);
          companion.Say(notice);
          if(culprit!=null && culprit.IsPerched) { peckingCulpritTimer=2.0; StopWalking(); ResetIdle(); }
          else { thermalAlertTimer=3.5; StopWalking(); ResetIdle(); }
          if(reading.Celsius>=90 && ambientVisionEnabled && (DateTime.Now-lastVisionLook).TotalSeconds>60 && ambientVisionTimer.IsEnabled) {
            ambientVisionTimer.Interval=TimeSpan.FromSeconds(12);
          }
        }
      } catch(Exception) {
        if(!request.IsCancellationRequested && !shuttingDown && temperatureEnabled) {
          temperatureTrend.BreakContinuity(); mood.SetTemperature(68);
          SetTemperatureStatus("CPU temperature unavailable - check Libre Hardware Monitor");
        }
      } finally { if(temperatureRequest==request) temperatureRequest=null; request.Dispose(); }
    }
    void SetCpuWatch(bool enabled) {
      cpuEnabled=enabled; cpuItem.IsChecked=trayCpu.Checked=enabled; cpu.Reset();
      SetCpuStatus(enabled?"CPU load: measuring...":"CPU load: watcher off");
      if(enabled) { cpuTimer.Start(); CheckCpu(); } else cpuTimer.Stop();
    }
    void SetCpuStatus(string text) { cpuStatus.Header=text; trayCpuStatus.Text=text; }
    async void CheckCpu() {
      if(!cpuEnabled || shuttingDown || cpuChecking) return;
      if(paused) { cpu.Reset(); SetCpuStatus("CPU load: paused"); return; }
      cpuChecking=true;
      try {
        bool inFullscreen=IsFullscreenOrPresentation();
        bool notify=!companion.Quiet && !inFullscreen && !looking && !dragging && micRequest==null && !ContextMenu.IsOpen && !tray.ContextMenuStrip.Visible && !companion.Visible;
        bool shouldNotify=notify && !temperatureEnabled;
        var watchedWindow=perchWindow;
        string notice=await Task.Run(()=>cpu.Sample(shouldNotify,watchedWindow));
        if(shuttingDown || !cpuEnabled) return;
        SetCpuStatus(cpu.Percent.HasValue?"CPU load: "+Math.Round(cpu.Percent.Value)+"%":"CPU load: measuring...");
        if(notice!=null) {
          if(cpu.LastCulprit!=null && nest!=null && !testing) nest.NoteHotApp(cpu.LastCulprit.Name,roost!=null && roost.Data.JournalEnabled);
          companion.Say(notice);
          if(cpu.LastCulprit!=null && cpu.LastCulprit.IsPerched) { peckingCulpritTimer=2.0; StopWalking(); ResetIdle(); }
          else if(cpu.LastCulprit!=null) { thermalAlertTimer=3.5; StopWalking(); ResetIdle(); }
        }
      } catch(Exception) { SetCpuWatch(false); SetCpuStatus("CPU load unavailable"); companion.Say("Windows refused to tell me your CPU usage. The CPU watcher gave up and went back to sleep."); }
      finally { cpuChecking=false; }
    }
    void SetDance(bool enabled) {
      try { if(enabled && !paused) music.Start(); else music.Stop(); danceEnabled=enabled; }
      catch(Exception) { music.Stop(); danceEnabled=false; companion.Say("I couldn't hear the computer's audio output. Check your speakers or headphones, then try Dance to music again."); }
      danceItem.IsChecked=trayDance.Checked=danceEnabled;
      if(!danceEnabled) { birdTuneEar.Reset(); BirdTunePlayer.Stop(); }
      musicLight.Visibility=danceEnabled?Visibility.Visible:Visibility.Hidden;
      wasDancing=false; danceBlend=dancePulse=0; danceTilt.Angle=0; StopWalking(); ResetIdle();
      UpdateStatus();
      RefreshBirdTunes();
    }
    void SetMusicTasteStatus(string text) { musicTasteStatus.Header=text; trayMusicTasteStatus.Text=text; }
    void SetMusicTaste(bool enabled) {
      musicTasteEnabled=enabled;
      musicTasteItem.IsChecked=trayMusicTaste.Checked=enabled;
      CancelMusicBrain();
      if(enabled) {
        SetMusicTasteStatus(paused?"Music memory: paused":"Music memory: connecting to Windows media");
        if(!paused) { musicBrainTimer.Start(); var ignored=CheckMusicBrain(); }
      } else {
        pendingFavouriteRemark=null;
        musicBrainTimer.Stop(); SetMusicTasteStatus("Music memory: off");
      }
      UpdateStatus();
    }
    void CancelMusicBrain() { if(musicBrainRequest!=null) musicBrainRequest.Cancel(); }
    async Task CheckMusicBrain() {
      if(!musicTasteEnabled || paused || shuttingDown || musicBrainRequest!=null) return;
      var request=new CancellationTokenSource(); musicBrainRequest=request;
      try {
        MusicUpdate update=await musicBrain.Poll(request.Token);
        request.Token.ThrowIfCancellationRequested();
        ApplyMusicUpdate(update);
      } catch(OperationCanceledException) { }
      catch(Exception ex) {
        musicBrainTimer.Stop(); musicTasteEnabled=false; musicTasteItem.IsChecked=trayMusicTaste.Checked=false;
        SetMusicTasteStatus("Music memory unavailable");
        companion.SaySilent("I couldn't read the Windows music session: "+ex.Message);
      } finally { if(musicBrainRequest==request) musicBrainRequest=null; request.Dispose(); }
    }
    void ApplyMusicUpdate(MusicUpdate update) {
      sensedMusicUtc=DateTime.UtcNow;
      if(update.Changed && update.Track!=null && update.Track.Playing) roost.Remember("music","Listened to "+update.Track.Title+" by "+update.Track.Artist,DateTime.UtcNow);
      if(update.Track==null) SetMusicTasteStatus("Music memory: waiting for a song");
      else { SetMusicTasteStatus((update.Track.Playing?"Now playing: ":"Paused: ")+update.Track.Title+(string.IsNullOrEmpty(update.Track.Artist)?"":" - "+update.Track.Artist)+(musicBrain.CurrentFavourite!=null?" · one of his favourites":"")); if(update.Track.Playing) mood.Music(musicBrain.CurrentFeeling,update.Changed); }
      if(update.FavouriteReaction) {
        pendingFavouriteRemark=update.Reaction; pendingFavouriteKey=update.Track.Key; pendingFavouriteUtc=DateTime.UtcNow;
      }
      TryFavouriteRemark();
      if(!update.FavouriteReaction && update.Reaction!=null && !companion.Quiet && !IsFullscreenOrPresentation() && micRequest==null && !looking && !dragging && !companion.Visible && !ContextMenu.IsOpen && !tray.ContextMenuStrip.Visible)
        OfferAttention(()=>companion.Say(update.Reaction));
    }
    void TryFavouriteRemark() {
      if(pendingFavouriteRemark==null) return;
      var song=musicBrain.Current;
      if(!musicTasteEnabled || song==null || !song.Playing || song.Key!=pendingFavouriteKey || (DateTime.UtcNow-pendingFavouriteUtc).TotalSeconds>30) { pendingFavouriteRemark=null; return; }
      if(paused || shuttingDown || isNightSleep || companion.Quiet || IsFullscreenOrPresentation() || micRequest!=null || looking || dragging || isFlung || peckingArchive
        || (lessonNotebook!=null && lessonNotebook.IsRecording) || focusSprint.Active || breakCoach.OnBreak || breakCoach.Due
        || companion.Visible || companion.VoiceBusy || attentionBid.Active || ContextMenu.IsOpen || tray.ContextMenuStrip.Visible) return;
      // Recognition may speak while dancing; generic attention bids intentionally cannot.
      string remark=pendingFavouriteRemark; pendingFavouriteRemark=null; companion.Say(remark);
    }
    double FavouriteDanceBoost() {
      var song=musicBrain.Current;
      return musicTasteEnabled && musicBrain.CurrentFavourite!=null && song!=null && song.Playing && (DateTime.UtcNow-sensedMusicUtc).TotalSeconds<20?1.25:1;
    }
    void CheckFavouriteMusic() {
      musicTasteEnabled=true; wasDancing=true;
      var song=new NowPlaying { Title="Blackbird",Artist="The Beatles",Key="test-blackbird",Playing=true,Duration=TimeSpan.FromMinutes(3) };
      var update=musicBrain.Observe(song,DateTime.UtcNow);
      companion.Quiet=true; ApplyMusicUpdate(update);
      if(pendingFavouriteRemark==null || companion.Visible) throw new Exception("Quiet mode did not defer the favourite remark.");
      companion.Quiet=false; TryFavouriteRemark();
      if(!companion.Visible || pendingFavouriteRemark!=null || !Convert.ToString(musicTasteStatus.Header).Contains("one of his favourites")) throw new Exception("Favourite recognition did not speak while dancing or update the Nest status.");
      companion.Dismiss();
      danceBlend=1; danceEnergy=.6; dancePulse=.5; danceTreble=.3; dancePhase=.25; motion.Y=0;
      PoseDance(true); double favouriteBounce=-motion.Y;
      musicTasteEnabled=false; motion.Y=0; PoseDance(true);
      if(favouriteBounce<=-motion.Y || FavouriteDanceBoost()!=1) throw new Exception("Favourite did not dance more strongly or respect music permission.");
      musicTasteEnabled=true; sensedMusicUtc=DateTime.UtcNow.AddSeconds(-25);
      if(FavouriteDanceBoost()!=1) throw new Exception("Stale music boosted the dance.");
      pendingFavouriteRemark="old favourite"; pendingFavouriteKey=song.Key; pendingFavouriteUtc=DateTime.UtcNow.AddSeconds(-40);
      TryFavouriteRemark(); if(pendingFavouriteRemark!=null || companion.Visible) throw new Exception("Old favourite remark was delivered late.");
      song=new NowPlaying { Title="Unrelated",Artist="Other",Key="unrelated",Playing=true };
      musicBrain.Observe(song,DateTime.UtcNow.AddSeconds(8));
      pendingFavouriteRemark="old favourite"; pendingFavouriteKey="test-blackbird"; pendingFavouriteUtc=DateTime.UtcNow;
      TryFavouriteRemark(); if(pendingFavouriteRemark!=null || companion.Visible) throw new Exception("Favourite reaction survived a track change.");
      musicTasteEnabled=false; wasDancing=false;
    }
    void PoseDance(bool active) {
      danceBlend+=( (active?1:0)-danceBlend)*0.15;
      if(danceBlend<0.005) { danceBlend=0; danceTilt.Angle=0; return; }
      double enthusiasm=FavouriteDanceBoost();
      double sway=Math.Sin(dancePhase*2*Math.PI)*danceBlend*Math.Min(1.15,danceEnergy*enthusiasm);
      danceTilt.Angle=sway*(octopusMode?5:9);
      motion.Y-=dancePulse*12*danceBlend*enthusiasm;
      headTilt.Angle+=sway*7; wingStretch.Angle-=(dancePulse*18+danceTreble*18)*danceBlend*enthusiasm;
      leftFoot.Y=-Math.Max(0,sway)*5; rightFoot.Y=-Math.Max(0,-sway)*5;
      if(octopusMode) {
        squish.ScaleX+=dancePulse*0.08*danceBlend; squish.ScaleY-=dancePulse*0.1*danceBlend;
        for(int i=0;i<8;i++) tentacles[i].Angle+=Math.Sin(dancePhase*2*Math.PI+i*0.8)*12*danceBlend*(0.6*danceEnergy+0.4*danceTreble);
      }
    }
    void CancelLooking() { if(visionRequest!=null) visionRequest.Cancel(); }
    void SetAmbientStatus(string text) { ambientVisionStatus.Header=text; trayAmbientVisionStatus.Text=text; }
    void SetAmbientVision(bool enabled) {
      ambientVisionEnabled=enabled; ambientVisionItem.IsChecked=trayAmbientVision.Checked=enabled;
      lastScreenThumbnail=null;
      if(roost!=null) { roost.Data.AmbientVisionEnabled=enabled; roost.Save(); }
      SetAmbientStatus(enabled?"Screen watch: watching":"Screen watch: watcher off");
      if(enabled) {
        ambientVisionTimer.Interval=TimeSpan.FromSeconds(20);
        ambientVisionTimer.Start();
      } else {
        ambientVisionTimer.Stop();
      }
    }
    async Task CheckAmbientScreen() {
      if(!ambientVisionEnabled || shuttingDown || paused || IsFullscreenOrPresentation()) return;
      if(isNightSleep || DayCycle.MuteChatter(dayPhase)) {
        SetAmbientStatus("Screen watch: night mute");
        return;
      }
      if(focusSprint.Active || breakCoach.OnBreak || breakCoach.Due) {
        SetAmbientStatus("Screen watch: waiting (focus/break)");
        return;
      }
      // Do not block on speech bubble - only skip when already looking / mic / menus.
      if(looking || dragging || micRequest!=null || sageRequest!=null || discoveryLooking || ViewerMailBusy || TikTokActive || FlockActive || ShowTellActive || cursorFollowEnabled || ContextMenu.IsOpen || tray.ContextMenuStrip.Visible || visionRequest!=null) {
        SetAmbientStatus("Screen watch: busy");
        return;
      }
      if(companion.Quiet) { SetAmbientStatus("Screen watch: quiet mode (still watching)"); }
      double idleSec=GetIdleTimeMs()/1000.0;
      // Active work gets more frequent glances; deep idle slows down but does not stop forever.
      double nextInterval=idleSec<45?40:(idleSec<120?70:(idleSec<300?150:280));
      ambientVisionTimer.Interval=TimeSpan.FromSeconds(nextInterval);
      if(idleSec>420 && lastScreenThumbnail!=null) {
        SetAmbientStatus("Screen watch: idle roost ("+Math.Round(idleSec/60)+"m)");
        return;
      }
      byte[] thumb;
      try { thumb=ScreenVision.CaptureThumbnail(new WindowInteropHelper(this).Handle); }
      catch(Exception) { SetAmbientStatus("Screen watch: capture unavailable"); return; }
      if(lastScreenThumbnail!=null && !ScreenVision.HasMeaningfulChange(lastScreenThumbnail,thumb,0.05)) {
        lastScreenThumbnail=thumb;
        SetAmbientStatus("Screen watch: no change ("+DateTime.Now.ToString("t")+")");
        return;
      }
      lastScreenThumbnail=thumb;
      SetAmbientStatus("Screen watch: looking...");
      await LookAtScreen(true);
    }
    bool CanInitiateAmbient() {
      return !ViewerMailBusy && !TikTokActive && !FlockActive && !ShowTellActive && !paused && !shuttingDown && !fullscreenOrPresentation && !looking && !dragging && !isFlung
        && !peckingArchive && micRequest==null && sageRequest==null && !discoveryLooking && !ContextMenu.IsOpen && !tray.ContextMenuStrip.Visible
        && !companion.Visible && !companion.VoiceBusy && !focusSprint.Active && !breakCoach.OnBreak && !breakCoach.Due;
    }
    IntPtr GetActiveWindowHandle() {
      var active=GetForegroundWindow();
      var own=new WindowInteropHelper(this).Handle;
      if(active!=IntPtr.Zero && active!=own && active!=companion.Handle) {
        return active;
      }
      return perchWindow;
    }
    async Task LookAtScreen(bool ambient=false) {
      if(ViewerMailBusy) {if(ambient)return;CancelViewerMail(false);StopTikTokTake(false);}
      if(ShowTellActive) {if(ambient)return;EndShowAndTell();}
      if(TikTokActive) {if(ambient)return;CancelViewerMail(false);StopTikTokTake(false);}
      if(FlockActive) {if(ambient)return;StopFlockShow();}
      if(looking || shuttingDown || micRequest!=null || discoveryLooking) return;
      if(sageRequest!=null) {if(ambient)return;CancelSageThinking();}
      if(paused) { if(!ambient) companion.Say("Wake me up with Pause / resume, then I'll take a look."); return; }
      looking=true; StopWalking();ResetIdle(); seeingLight.Visibility=Visibility.Visible;
      tray.Text="Corbie - looking locally";
      var request=new CancellationTokenSource(); visionRequest=request;
      string ambientRemark=null;
      DateTime capturedAt=DateTime.Now;
      try {
        var limit=testing?screenReadTestLimit:TimeSpan.FromSeconds(ambient?45:60);
        string response=await VisionBudget.Run(t=>ReadScreenForLook(t,ambient,request,stamp=>capturedAt=stamp),limit,request.Token);
        request.Token.ThrowIfCancellationRequested();
        if(visionRequest!=request || shuttingDown)return;
        lastVisionContext=response;
        lastVisionLook=capturedAt;
        SetAmbientStatus(ambient?"Screen watch: noticed change ("+lastVisionLook.ToString("t")+")":"Screen reader: finished");
        if(!shuttingDown) {
          if(!ambient) { mood.ScreenLook(); companion.Say(response); }
          else ambientRemark=response;
        }
      } catch(OperationCanceledException) {
        if(!shuttingDown && visionRequest==request) {SetAmbientStatus("Screen reader: cancelled");if(!ambient)companion.SaySilent("Look cancelled. Spared from my critique for now.");}
      } catch(TimeoutException) {
        if(!shuttingDown && visionRequest==request) {SetAmbientStatus("Screen reader: timed out; ready to try again");if(!ambient)companion.Say("My local vision is taking too long. I've stopped this look; you can try again in a moment.");}
      }
      catch(Exception ex) {
        if(!shuttingDown) {
          SetAmbientStatus("Screen reader: local model error; ready to try again");
          if(!ambient) companion.Say(ex is InvalidOperationException?ex.Message:"I couldn't use local vision. Make sure Ollama is running with gemma3:4b installed.");
        }
      }
      finally {
        if(visionRequest==request) {
          visionRequest=null;looking=false;
          if(!shuttingDown) {Opacity=1;seeingLight.Visibility=Visibility.Hidden;ResetIdle();UpdateStatus();}
        }
        request.Dispose();
        if(ambientRemark!=null && !IsFullscreenOrPresentation()) {
          if(CanInitiateAmbient()) {
            mood.ScreenLook();
            if(companion.Quiet) companion.SaySilent(ambientRemark); else companion.Say(ambientRemark);
            SetAmbientStatus("Screen watch: remarked ("+DateTime.Now.ToString("t")+")");
          } else SetAmbientStatus("Screen watch: comment skipped (busy)");
        }
      }
    }
    void GiveTreat() {
      mood.Treat();
      FeedDesktopForage();
      roost.Remember("treat","Received a treat. Bribery remains effective.",DateTime.UtcNow);
      if(nest!=null) nest.NoteTreat(DateTime.Now,roost.Data.JournalEnabled);
      StopWalking(); ResetIdle();
      if(!paused) hop=1;
      CreatureFoley.Play(FoleyKind.Clack,companion.Quiet);
      if(random.Next(100)<25) {
        var tr=roost.ScavengeTrinket(random);
        if(tr!=null) {
          companion.Say("Delicious bribe! And look what I found: a shiny '"+tr.Name+"' ("+tr.Rarity+").");
          ApplyTrinketMolt();
          ShowTrinketInspect(tr);
          return;
        }
      }
      companion.Treat(octopusMode);
    }
    void SetVoice(bool enabled) {
      companion.SetVoice(enabled);
      if(roost!=null) { roost.Data.VoiceEnabled=companion.VoiceEnabled; roost.Save(); }
      if(voiceItem!=null) voiceItem.IsChecked=companion.VoiceEnabled;
      if(trayVoice!=null) trayVoice.Checked=companion.VoiceEnabled;
    }
    void SetVoiceModel(string id) { companion.SetVoiceModel(id); voiceItem.IsChecked=companion.VoiceEnabled; trayVoice.Checked=companion.VoiceEnabled; }
    void SetMicStatus(string status) {
      if(status.Length>85) status=status.Substring(0,82)+"...";
      micStatus.Header=status; trayMicStatus.Text=status;
      micItem.Header=trayMic.Text=micRequest==null?"Talk to Corbie (mic)":"Cancel mic chat";
    }
    async Task SetHandsFree(bool enabled) {
      if(enabled && lessonNotebook!=null && lessonNotebook.IsRecording) {
        handsFreeItem.IsChecked=trayHandsFree.Checked=false;
        companion.SaySilent("I'm recording a lesson. Finish that first, then we can chat.");
        return;
      }
      if(enabled && paused) {
        handsFreeItem.IsChecked=false;
        companion.Say("Wake me first, then I'll keep an ear out for your nonsense.");
        return;
      }
      handsFreeEnabled=enabled;
      handsFreeItem.IsChecked=trayHandsFree.Checked=enabled;
      if(enabled && micRequest==null) await ToggleMicTalk();
      else if(!enabled && micRequest!=null) CancelMicTalk();
    }
    void CancelMicTalk() {
      handsFreeEnabled=false;
      if(handsFreeItem!=null) handsFreeItem.IsChecked=false;
      if(trayHandsFree!=null) trayHandsFree.Checked=false;
      if(micRequest!=null) micRequest.Cancel();
      micLight.Visibility=Visibility.Hidden;
    }
    static bool RequiresWakePhrase(bool preference,bool continuousConversation) {
      return preference && !continuousConversation;
    }
    string HandleWakeCommand(WakeResult wake) {
      if(wake==null) return null;
      switch(wake.Action) {
        case WakeAction.Break:
          BeginBreakNow(); hop=1;
          return "Break decree issued. Step away from the pixels.";
        case WakeAction.WhatsDue:
          return schoolCalendar!=null?schoolCalendar.Summary(DateTime.Now):"School calendar unavailable.";
        case WakeAction.OpenNest:
          OpenCorbiePanel();
          return "Nest opened. Try not to lose a shiny in there.";
        case WakeAction.StartFocus:
          if(mission!=null && mission.HasNextStep) {
            try {
              if(!focusSprint.Active && !focusSprint.CompletionPending) {
                focusSprint.Start(mission.NextStep,25);
                SaveRoost();
                hop=1;
                return "Focus sprint locked on: "+mission.NextStep+". Beak sealed. You work.";
              }
            } catch { }
          }
          OpenCorbiePanel();
          if(mission!=null && mission.HasNextStep && focusTask!=null) focusTask.Text=mission.NextStep;
          return "Nest open - name the sprint and hit focus when ready.";
        case WakeAction.Mood:
          return mood.Report(octopusMode);
        case WakeAction.Treat:
          mood.Treat(); FeedDesktopForage(); hop=1;
          if(nest!=null) nest.NoteTreat(DateTime.Now,roost!=null && roost.Data.JournalEnabled);
          return "Treat accepted. Affection meters bribed.";
        case WakeAction.MissionStatus:
          return mission!=null?mission.StatusLine():"No mission board.";
        case WakeAction.NoteGame:
          return NoteCurrentGame();
        case WakeAction.Chronicle:
          return SpeakChronicle(true);
        case WakeAction.Territory:
          return TerritoryStatusSpeech();
        case WakeAction.MoltReport:
          return MoltSeasonSpeech();
        default:
          return null;
      }
    }
    async Task ToggleMicTalk() {
      if(lessonNotebook!=null && lessonNotebook.IsRecording) {
        companion.SaySilent("Lesson recording has the microphone. Finish that before we chat.");
        return;
      }
      if(micRequest!=null) { CancelMicTalk(); return; }
      if(shuttingDown) return;
      if(paused) { companion.Say("Wake me first. Then tell me what fresh nonsense we're dealing with."); return; }
      CancelViewerMail(false);StopTikTokTake(false);
      if(discoveryLooking)CancelDiscovery();
      if(looking)CancelLooking();
      var request=new CancellationTokenSource(); micRequest=request;
      micDiagnosticShown=false;
      micTranscriptHintShown=false;
      companion.Dismiss();
      string micError=null;
      try {
        do {
          micLight.Visibility=Visibility.Visible;
          micLight.Opacity=0.5;
          SetMicStatus("Mic: "+MicListener.EngineName+(handsFreeEnabled?" - listening continuously":" - listening"));
          string heard=await MicListener.Listen(request.Token,handsFreeEnabled,a=>Dispatcher.BeginInvoke(new Action(()=> {
            if(micRequest!=request || request.IsCancellationRequested || shuttingDown) return;
            if(a.Level>=0) {
              micLight.Opacity=0.5+Math.Min(0.5,a.Level/40.0);
              if(a.Level>=5 && (DateTime.UtcNow-lastMicLevelStatus).TotalMilliseconds>600) {
                SetMicStatus("Mic: input level "+a.Level+"% - listening");
                lastMicLevelStatus=DateTime.UtcNow;
              }
            } else if(!string.IsNullOrEmpty(a.Message)) {
              SetMicStatus("Mic: "+a.Message);
              if(!micDiagnosticShown && a.Message.StartsWith("no sound",StringComparison.OrdinalIgnoreCase)) {
                micDiagnosticShown=true;
                companion.SaySilent("I'm listening, but the selected microphone is silent. Check Settings > System > Sound > Input and speak into that mic.");
              }
              if(!micDiagnosticShown && (a.Message=="audio present; waiting for words" || a.Message=="audio heard, but no sentence detected")) {
                micDiagnosticShown=true;
                companion.SaySilent("I can hear the microphone, but I haven't caught a sentence yet. Try speaking clearly near the mic.");
              }
            }
          })));
          request.Token.ThrowIfCancellationRequested();
          micLight.Visibility=Visibility.Hidden;
          if(string.IsNullOrWhiteSpace(heard)) {
            if(handsFreeEnabled) {
              if(!micTranscriptHintShown) {
                micTranscriptHintShown=true;
                companion.SaySilent("I heard a sound, but couldn't make out words. Try one short sentence near the microphone.");
              }
              continue;
            }
            companion.Say("Didn't catch that. Give the mic another go, you magnificent mumbler.");
            break;
          }
          bool wakeOn=RequiresWakePhrase(roost!=null && roost.Data.WakePhraseEnabled,handsFreeEnabled);
          var wake=WakeCommand.Parse(heard,wakeOn);
          if(wake.Action==WakeAction.NotWoken) {
            SetMicStatus("Mic: waiting for Corbie wake phrase");
            if(handsFreeEnabled) continue;
            companion.SaySilent("Say my name first. Example: Corbie, start a break.");
            break;
          }
          if(wake.Action!=WakeAction.None) {
            string handled=wake.Action==WakeAction.HolidayReport?await HolidayMicReply(request.Token):wake.Action==WakeAction.DiscoveryReport?await DiscoveryMicReply(wake.Remainder,request.Token):HandleWakeCommand(wake);
            request.Token.ThrowIfCancellationRequested();
            if(!string.IsNullOrEmpty(handled)) {
              mood.Conversation();
              if(!companion.VoiceEnabled && PiperVoice.Configured) SetVoice(true);
              companion.Say(handled);
              if(!handsFreeEnabled) break;
              continue;
            }
          }
          SetMicStatus("Mic: thinking locally");
          string answer=await chat.Reply(heard,octopusMode,request.Token,BuildSensoryContext());
          request.Token.ThrowIfCancellationRequested();
          if(shuttingDown) break;
          if(!companion.VoiceEnabled && PiperVoice.Configured) SetVoice(true);
          mood.Conversation(); companion.SayReply(heard,answer);
          if(!handsFreeEnabled) break;
          SetMicStatus("Mic: waiting for Corbie to finish");
          var wait=System.Diagnostics.Stopwatch.StartNew();
          while(companion.VoiceBusy && wait.Elapsed<TimeSpan.FromSeconds(60)) await Task.Delay(100,request.Token);
          if(companion.VoiceBusy) companion.StopVoice();
          await Task.Delay(350,request.Token);
        } while(handsFreeEnabled && !paused && !shuttingDown);
      } catch(OperationCanceledException) { }
      catch(Exception ex) {
        if(!shuttingDown && !request.IsCancellationRequested) {
          micError="Mic: error - "+ex.Message;
          SetMicStatus(micError);
          companion.SaySilent("Mic chat failed: "+ex.Message);
        }
      } finally {
        if(micRequest==request) micRequest=null;
        request.Dispose();
        if(!shuttingDown) {
          micLight.Visibility=Visibility.Hidden;
          handsFreeEnabled=false;
          handsFreeItem.IsChecked=trayHandsFree.Checked=false;
          SetMicStatus(micError??"Mic: off");
        }
      }
    }
    void UpdateVoiceToneChecks(VoiceTone tone) {
      if(voiceToneBirdItem!=null) voiceToneBirdItem.IsChecked=(tone==VoiceTone.Bird);
      if(voiceToneNormalItem!=null) voiceToneNormalItem.IsChecked=(tone==VoiceTone.Normal);
      if(voiceToneChirpyItem!=null) voiceToneChirpyItem.IsChecked=(tone==VoiceTone.Chirpy);
      if(voiceToneCroakItem!=null) voiceToneCroakItem.IsChecked=(tone==VoiceTone.Croak);

      if(trayToneBird!=null) trayToneBird.Checked=(tone==VoiceTone.Bird);
      if(trayToneNormal!=null) trayToneNormal.Checked=(tone==VoiceTone.Normal);
      if(trayToneChirpy!=null) trayToneChirpy.Checked=(tone==VoiceTone.Chirpy);
      if(trayToneCroak!=null) trayToneCroak.Checked=(tone==VoiceTone.Croak);
    }
    void SetVoiceTone(VoiceTone tone) {
      companion.VoiceTone=tone;
      UpdateVoiceToneChecks(tone);
      string remark;
      switch(tone) {
        case VoiceTone.Bird:
          remark="Plumage tuned. Now squawking in curious raven pitch.";
          break;
        case VoiceTone.Chirpy:
          remark="Peeep! A little higher, a little lighter.";
          break;
        case VoiceTone.Croak:
          remark="Krrrk. Deep, gravelly, and thoroughly unimpressed.";
          break;
        case VoiceTone.Normal:
          remark="Back to default baseline. Ordinary for a raven, but functional.";
          break;
        default:
          remark="Voice tone adjusted.";
          break;
      }
      companion.Say(remark);
    }
    void SetQuiet(bool quiet) {
      companion.Quiet=quiet; quietItem.IsChecked=quiet; trayQuiet.Checked=quiet;
      if(quiet) BirdTunePlayer.Stop();
      RefreshBirdTunes();
      if(quiet) { companion.Dismiss(); if(ambientVisionEnabled) SetAmbientStatus("Screen watch: quiet mode"); }
      else if(ambientVisionEnabled) SetAmbientStatus("Screen watch: watching");
    }
    void ShapePath(string data,string fill,string stroke=null,double thickness=1) {
      bird.Children.Add(new Path { Data=Geometry.Parse(data), Fill=fill==null?null:ColorBrush(fill), Stroke=stroke==null?null:ColorBrush(stroke), StrokeThickness=thickness, StrokeStartLineCap=PenLineCap.Round, StrokeEndLineCap=PenLineCap.Round });
    }
    Canvas GroupParts(int start,int count) {
      var group=new Canvas { Width=180,Height=180 };
      for(int i=0;i<count;i++) { var part=bird.Children[start]; bird.Children.RemoveAt(start); group.Children.Add(part); }
      bird.Children.Insert(start,group); return group;
    }
    static Brush ColorBrush(string value) { return (Brush)new BrushConverter().ConvertFromString(value); }
    Ellipse EllipseAt(double x,double y,double w,double h,string color) {
      var shape=new Ellipse { Width=w,Height=h,Fill=ColorBrush(color) }; Canvas.SetLeft(shape,x); Canvas.SetTop(shape,y); bird.Children.Add(shape); return shape;
    }
    void BuildOctopus() {
      for(int i=0;i<8;i++) {
        double x=58+i*9, end=23+i*19;
        var leg=new Path { Data=Geometry.Parse(string.Format(System.Globalization.CultureInfo.InvariantCulture,"M {0},110 C {0},146 {1},159 {1},132",x,end)), Stroke=ColorBrush(i%2==0?"#8F79BD":"#AC92D4"), StrokeThickness=10, StrokeStartLineCap=PenLineCap.Round, StrokeEndLineCap=PenLineCap.Round };
        octopus.Children.Add(leg);
        tentaclePaths[i]=leg;
        tentacles[i]=new RotateTransform(0,x,110); leg.RenderTransform=tentacles[i];
      }
      var head=new Ellipse { Width=80,Height=76,Fill=ColorBrush("#AC92D4") }; Canvas.SetLeft(head,50); Canvas.SetTop(head,53); octopus.Children.Add(head);
      octoEyeLeft=OctoDot(70,86,8,11,"#29283E"); octoEyeRight=OctoDot(104,86,8,11,"#29283E");
      OctoDot(61,101,13,6,"#D5A4CC"); OctoDot(108,101,13,6,"#D5A4CC");
      octopus.Children.Add(new Path { Data=Geometry.Parse("M 84,104 Q 90,111 96,104"),Stroke=ColorBrush("#504363"),StrokeThickness=2 });
    }
    Ellipse OctoDot(double x,double y,double w,double h,string color) {
      var dot=new Ellipse { Width=w,Height=h,Fill=ColorBrush(color) }; Canvas.SetLeft(dot,x); Canvas.SetTop(dot,y); octopus.Children.Add(dot); return dot;
    }
    void ApplyTrinketMolt() {
      if(roost==null) return;
      var tint=TrinketMolt.FromHoard(roost.Data.Trinkets);
      if(roost.Data.MoltSeasonEnabled && moltBook!=null)
        tint=MoltSeason.ApplySeason(tint, moltBook.State);
      var feathers=new System.Collections.Generic.List<Shape>();
      if(moltBody!=null) feathers.Add(moltBody);
      if(moltChest!=null) feathers.Add(moltChest);
      TrinketMolt.Apply(tint, feathers, tentaclePaths);
    }
    // Archived form is retained for legacy animation checks/previews only.
    // Normal sessions stay raven-only while its old implementation is retired gradually.
    void SwitchForm() { if(!testing) return; EndShowAndTell();StopCursorFollow(); CancelBirdFlight(true); StopWalking(); ResetIdle(); hop=0; octopusMode=!octopusMode; bird.Visibility=octopusMode?Visibility.Hidden:Visibility.Visible; octopus.Visibility=octopusMode?Visibility.Visible:Visibility.Hidden; UpdateStatus(); }
    void UpdateStatus() { tray.Text=peckingArchive?"Corbie - pecking open archive":paused?"Corbie - resting":looking?"Corbie - looking locally":danceEnabled?"Corbie - listening to music":musicTasteEnabled?"Corbie - judging your music":"Corbie - "+mood.Snapshot.Mood.ToLowerInvariant()+(octopusMode?" octopus":" raven"); }
    void SetExtractToFolder(bool enabled) {
      extractToFolder=enabled;
      if(extractToFolderItem!=null) extractToFolderItem.IsChecked=enabled;
      if(trayExtractToFolder!=null) trayExtractToFolder.Checked=enabled;
    }
    void SetRevealFolder(bool enabled) {
      revealExtractedFolder=enabled;
      if(revealFolderItem!=null) revealFolderItem.IsChecked=enabled;
      if(trayRevealFolder!=null) trayRevealFolder.Checked=enabled;
    }
    void SetConfirmPreview(bool enabled) {
      confirmPreview=enabled;
      if(confirmPreviewItem!=null) confirmPreviewItem.IsChecked=enabled;
      if(trayConfirmPreview!=null) trayConfirmPreview.Checked=enabled;
    }
    void SetWarnBeforeHoarding(bool enabled) {
      RecycleBinHoard.WarnBeforeHoarding=enabled;
      if(warnHoardItem!=null) warnHoardItem.IsChecked=enabled;
      if(trayWarnHoard!=null) trayWarnHoard.Checked=enabled;
      if(panelWarnHoard!=null && panelWarnHoard.IsChecked!=enabled) panelWarnHoard.IsChecked=enabled;
    }
    void TriggerOffendedReaction() {
      offendedTimer=2.5; StopWalking(); ResetIdle();
    }
    static bool TrayItemKeepsMenuOpen(string text) {
      if(string.IsNullOrEmpty(text)) return false;
      // Nest, Quit, and Peck open must dismiss the tray menu so the action can surface.
      if(text=="Quit" || text.StartsWith("Goodnight", StringComparison.OrdinalIgnoreCase)) return false;
      if(text.StartsWith("Open Corbie's Nest", StringComparison.OrdinalIgnoreCase)) return false;
      if(text.StartsWith("Peck open", StringComparison.OrdinalIgnoreCase)) return false;
      if(text=="Launch latest version")return false;
      return true;
    }
    void OnTrayMenuClosing(object sender, Forms.ToolStripDropDownClosingEventArgs e) {
      if(e.CloseReason==Forms.ToolStripDropDownCloseReason.ItemClicked) {
        if(lastTrayClicked!=null && TrayItemKeepsMenuOpen(lastTrayClicked.Text)) e.Cancel=true;
      }
      lastTrayClicked=null;
    }
    void CheckTrayMenuClosing() {
      var evOption=new Forms.ToolStripDropDownClosingEventArgs(Forms.ToolStripDropDownCloseReason.ItemClicked);
      lastTrayClicked=trayVoice;
      OnTrayMenuClosing(tray.ContextMenuStrip,evOption);
      if(!evOption.Cancel) throw new Exception("trayMenu should cancel close when option is clicked");

      var evClickAway=new Forms.ToolStripDropDownClosingEventArgs(Forms.ToolStripDropDownCloseReason.AppClicked);
      OnTrayMenuClosing(tray.ContextMenuStrip,evClickAway);
      if(evClickAway.Cancel) throw new Exception("trayMenu should not cancel close when clicking away");

      var evQuit=new Forms.ToolStripDropDownClosingEventArgs(Forms.ToolStripDropDownCloseReason.ItemClicked);
      lastTrayClicked=new Forms.ToolStripMenuItem("Goodnight, Corbie");
      OnTrayMenuClosing(tray.ContextMenuStrip,evQuit);
      if(evQuit.Cancel) throw new Exception("trayMenu should not cancel close when Goodnight is clicked");

      var evNest=new Forms.ToolStripDropDownClosingEventArgs(Forms.ToolStripDropDownCloseReason.ItemClicked);
      lastTrayClicked=new Forms.ToolStripMenuItem("Open Corbie's Nest...");
      OnTrayMenuClosing(tray.ContextMenuStrip,evNest);
      if(evNest.Cancel) throw new Exception("trayMenu should not cancel close when Nest is opened");
    }
    void RequestGoodbye() {
      if(farewellSaid || shuttingDown) return; // already saying goodbye - do not skip the bubble with a hard Close
      if(lessonNotebook!=null && lessonNotebook.IsRecording) {
        lessonNotebook.Show(); lessonNotebook.Activate();
        companion.SaySilent("Finish the lesson recording before I leave. The audio deserves its ending.");
        return;
      }
      CancelViewerMail(false);StopTikTokTake(false);StopFlockShow();CloseCorbiePanel(); attentionBid.Cancel();
      if(testing) { Close(); return; }
      farewellSaid=true;
      // Lift soft session mute so the farewell is allowed to surface.
      try { if(gameSession!=null) gameSession.Reset(); } catch { }
      string[] lines=octopusMode
        ? new [] {
            "Eight arms, one exit. I'm off duty. Try not to annoy the entire operating system without me.",
            "Back to the ink. Night night - and tidy those tabs, you scruffy herbert.",
            "Service is over. Those desktop icons can rot till I'm back."
          }
        : new [] {
            "Goodnight. Feathers tucked. Touch my perch while I'm gone and I WILL know.",
            "Caw later. Clean your station before I get back or there'll be fucking words.",
            "Right, I'm off. Try not to turn this desktop into a crime scene while I'm away."
          };
      string line=lines[random.Next(lines.Length)];
      hop=Math.Max(hop,0.8);
      try {
        // Always show the bubble even in Quiet mode; voice is optional.
        if(companion.Quiet) companion.SaySilent(line);
        else companion.Say(line);
      } catch {
        try { companion.SaySilent(line); } catch { }
      }
      try { CreatureFoley.Play(octopusMode?FoleyKind.SuctionPop:FoleyKind.Caw, false); } catch { }
      if(farewellTimer!=null) { try { farewellTimer.Stop(); } catch { } }
      farewellTimer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(companion!=null && companion.VoiceEnabled && !companion.Quiet?3200:2200) };
      farewellTimer.Tick+=(s,e)=> {
        try { farewellTimer.Stop(); } catch { }
        Close();
      };
      farewellTimer.Start();
    }
    void Pet_DragEnter(object sender, DragEventArgs e) {
      if(e.Data.GetDataPresent(DataFormats.FileDrop)) {
        var files=(string[])e.Data.GetData(DataFormats.FileDrop);
        if(files!=null && files.Length>0) {
          e.Effects=DragDropEffects.Copy;
          e.Handled=true;
          isDragHovering=true;
          return;
        }
      }
      e.Effects=DragDropEffects.None;
      e.Handled=true;
    }
    void Pet_DragOver(object sender, DragEventArgs e) {
      if(e.Data.GetDataPresent(DataFormats.FileDrop)) {
        var files=(string[])e.Data.GetData(DataFormats.FileDrop);
        if(files!=null && files.Length>0) {
          e.Effects=DragDropEffects.Copy;
          e.Handled=true;
          isDragHovering=true;
          return;
        }
      }
      e.Effects=DragDropEffects.None;
      e.Handled=true;
      isDragHovering=false;
    }
    void Pet_DragLeave(object sender, DragEventArgs e) {
      isDragHovering=false;
    }
    void Pet_Drop(object sender, DragEventArgs e) {
      isDragHovering=false;
      if(e.Data.GetDataPresent(DataFormats.FileDrop)) {
        var files=(string[])e.Data.GetData(DataFormats.FileDrop);
        if(files!=null && files.Length>0) {
          e.Handled=true;
          string file=files[0];
          if(ArchiveExtractor.IsSupportedArchive(file)) {
            var info=ArchiveExtractor.Inspect(file);
            var choiceDlg=new ArchiveChoiceDialog(this,info);
            choiceDlg.ShowDialog();
            if(choiceDlg.SelectedChoice==ArchiveChoiceDialog.Choice.CrackOpen) {
              PeckOpenArchive(file);
            } else if(choiceDlg.SelectedChoice==ArchiveChoiceDialog.Choice.KeepInHoard) {
              if(choiceDlg.DontWarnAgain) SetWarnBeforeHoarding(false);
              HoardFile(file);
            } else {
              companion.Say("Archive spared. Neither cracked nor hoarded.");
            }
          } else {
            if(RecycleBinHoard.WarnBeforeHoarding) {
              var hoardDlg=new HoardConfirmDialog(this,file);
              hoardDlg.ShowDialog();
              if(hoardDlg.Confirmed) {
                if(hoardDlg.DontWarnAgain) SetWarnBeforeHoarding(false);
                HoardFile(file);
              } else {
                companion.Say("Fine, keep your file. My stash remains unsatisfied.");
              }
            } else {
              HoardFile(file);
            }
          }
        }
      }
    }
    void HoardFile(string filePath) {
      if(!System.IO.File.Exists(filePath) && !System.IO.Directory.Exists(filePath)) {
        companion.Say("I reached for it, but the file vanished into thin air!");
        return;
      }
      string fileName=System.IO.Path.GetFileName(filePath);
      if(string.IsNullOrEmpty(fileName)) fileName=filePath;

      StopWalking(); ResetIdle();
      hop=0.8;
      motion.Y=-16;
      if(!octopusMode) {
        beakOpen.Angle=22;
        headTilt.Angle=-14;
        wingStretch.Angle=18;
      }
      bool success=RecycleBinHoard.SendToHoard(filePath);
      if(success) {
        mood.FileSuccess();
        companion.HoardReaction(octopusMode,fileName);
      } else {
        mood.FileFailure();
        TriggerOffendedReaction();
        companion.Say("Ugh! Windows wouldn't let me stash '"+fileName+"'. Locked or protected clutter!");
      }
    }
    void PromptPeckOpenArchive() {
      var dialog=new Forms.OpenFileDialog {
        Title="Feed Corbie an archive",
        Filter="Supported Archives (*.zip;*.7z;*.rar;*.tar;*.gz;*.tgz;*.bz2;*.xz)|*.zip;*.7z;*.rar;*.tar;*.gz;*.tgz;*.bz2;*.xz|All Files (*.*)|*.*"
      };
      if(dialog.ShowDialog()==Forms.DialogResult.OK) {
        PeckOpenArchive(dialog.FileName);
      }
    }
    static void RevealInExplorer(string targetPath) {
      try {
        if(System.IO.Directory.Exists(targetPath)) {
          System.Diagnostics.Process.Start("explorer.exe","\""+targetPath+"\"");
        } else if(System.IO.File.Exists(targetPath)) {
          System.Diagnostics.Process.Start("explorer.exe","/select,\""+targetPath+"\"");
        }
      } catch { }
    }
    async void PeckOpenArchive(string filePath, string password=null) {
      if(peckingArchive) {
        companion.Say("I'm already chewing on an archive! Have some patience, my beak has limits.");
        return;
      }
      if(paused) {
        companion.Say("Wake me up with Pause / resume, then I'll crack that open.");
        return;
      }
      StopWalking(); ResetIdle();
      var info=ArchiveExtractor.Inspect(filePath,password);
      if(!info.IsValid && !info.IsEncrypted) {
        TriggerOffendedReaction();
        string[] offendedLines=octopusMode?new [] {
          "Blech! Eight arms and not one can stomach that sludge. Clean your downloads, you grubby urchin!",
          "Ptooey! That's not an archive, that's a health-code violation. Not serving that."
        }:new [] {
          "Ptooey! What the fuck was THAT? That archive's rotten to the core!",
          "Blech! Malformed header. Did you fish that out of a spam bin? Get it out of my bloody nest!",
          "Ugh! Not an archive, a pile of broken shit. Clean your downloads, you absolute spanner!"
        };
        companion.Say(offendedLines[random.Next(offendedLines.Length)]);
        return;
      }

      string parentDir=System.IO.Path.GetDirectoryName(filePath);
      string baseName=ArchiveExtractor.GetArchiveBaseName(filePath);
      string targetDir=extractToFolder
        ? ArchiveExtractor.GetUniqueTargetDirectory(parentDir,baseName)
        : parentDir;

      if(confirmPreview) {
        var previewDlg=new ArchivePreviewDialog(this,info,targetDir);
        previewDlg.ShowDialog();
        if(!previewDlg.Confirmed) {
          companion.Say("Peck Open aborted. Spared from my beak for now.");
          return;
        }
      }

      if(info.IsEncrypted && string.IsNullOrEmpty(password)) {
        var passDlg=new ArchivePasswordDialog(this,info.FileName);
        passDlg.ShowDialog();
        if(!passDlg.Confirmed || string.IsNullOrEmpty(passDlg.Password)) {
          companion.Say("Fine, keep your secrets. Uncracked archive returned to you intact.");
          return;
        }
        password=passDlg.Password;
      }

      companion.Say("Ooh, an archive!\n"+info.FormatSummary()+"\nPecking open...");
      peckingArchive=true;
      peckPhase=0;
      UpdateStatus();

      var sw=System.Diagnostics.Stopwatch.StartNew();
      var res=await Task.Run(()=>ArchiveExtractor.Extract(filePath,targetDir,password));
      long elapsed=sw.ElapsedMilliseconds;
      if(elapsed<1200) await Task.Delay((int)(1200-elapsed));
      peckingArchive=false;
      UpdateStatus();

      if(res.Success) {
        mood.FileSuccess();
        hop=1.0;
        CreatureFoley.Play(FoleyKind.Clack,companion.Quiet);
        string folderName=System.IO.Path.GetFileName(targetDir);
        string[] winLines=octopusMode?new [] {
          "Eight arms, zero hesitation. Squeezed "+res.ExtractedCount+" files out of '"+info.FileName+"'. You're welcome.",
          "Cephalopod suction power prevails. Unpacked "+res.ExtractedCount+" items into '"+folderName+"'. THAT is how it's done."
        }:new [] {
          "Crunch! Cracked open '"+info.FileName+"'. Spat out "+res.ExtractedCount+" items into '"+folderName+"'. Try not to lose them, butterfingers.",
          "Gulp. Archive demolished. "+res.ExtractedCount+" files plated right beside it. Keep them tidy. You won't, but TRY.",
          "Delicious compression artifacts. Spat out "+res.ExtractedCount+" shiny files into '"+folderName+"'. Back to supervising."
        };
        companion.Say(winLines[random.Next(winLines.Length)]);
        if(random.Next(100)<35) {
          var tr=roost.ScavengeTrinket(random);
          if(tr!=null) {
            companion.Say("Crunch! Look what was hiding inside that archive: a shiny '"+tr.Name+"'!");
            ApplyTrinketMolt();
          ShowTrinketInspect(tr);
          }
        }
        if(revealExtractedFolder) RevealInExplorer(targetDir);
      } else if(res.ExistingFiles) {
        companion.Say("Oi, some files were already there. I left the originals alone. Check the destination for anything I unpacked before the collision.");
      } else if(res.WrongPassword) {
        mood.FileFailure();
        TriggerOffendedReaction();
        companion.Say("Wrong password! My beak almost cracked trying to force that lock. Feed me the right key next time.");
      } else if(res.PasswordRequired) {
        var passDlg=new ArchivePasswordDialog(this,info.FileName);
        passDlg.ShowDialog();
        if(passDlg.Confirmed && !string.IsNullOrEmpty(passDlg.Password)) {
          PeckOpenArchive(filePath,passDlg.Password);
        } else {
          companion.Say("Fine, keep your secrets. Uncracked archive returned to you intact.");
        }
      } else {
        mood.FileFailure();
        TriggerOffendedReaction();
        companion.Say("Blech! Extraction choked on corrupted data. I spat it back out.");
      }
    }
    void SetPerching(bool enabled) { CancelDesktopForage(); CancelBirdFlight(true); StopWalking(); perchOffset=0; autoPerch=enabled; perchItem.IsChecked=enabled; trayPerch.Checked=enabled; if(!enabled) { perchWindow=IntPtr.Zero; perchArrivalTime=0; motion.X=0; } }
    void FollowWindow() {
      if(birdFlight.Active) { if(FlightTargetValid()) return; CancelBirdFlight(true); }
      if(fullscreenOrPresentation) {
        StopWalking(); perchArrivalTime=0; motion.X=0;
        var area=CurrentDesktopBounds();
        Left=area.Right-Width-24;
        Top=area.Bottom-Height+18;
        return;
      }
      bool newPerch=false;
      var active=GetForegroundWindow(); var own=new WindowInteropHelper(this).Handle;
      if(active!=IntPtr.Zero && active!=own && active!=companion.Handle) {
        var name=new StringBuilder(256); GetClassName(active,name,name.Capacity);
        string kind=name.ToString();
        if(kind!="Shell_TrayWnd" && kind!="Shell_SecondaryTrayWnd" && kind!="Progman" && kind!="WorkerW" && kind!="#32768" && active!=perchWindow) {
          perchWindow=active; perchOffset=0; StopWalking(); newPerch=true;
          if(nest!=null) {
            string perchName=NestMemory.ProcessNameFromWindow(perchWindow);
            nest.NotePerch(perchName,roost!=null && roost.Data.JournalEnabled);
            string reunion=nest.MaybeReunionRemark(perchName,octopusMode,roost!=null && roost.Data.JournalEnabled);
            if(reunion!=null && CanInitiate()) OfferAttention(()=>companion.Say(reunion));
          }
          if(ambientVisionEnabled && (DateTime.Now-lastVisionLook).TotalSeconds>60 && ambientVisionTimer.IsEnabled) {
            ambientVisionTimer.Interval=TimeSpan.FromSeconds(8);
          }
        }
      }
      NativeRect rect;
      routeMin=routeMax=Left;
      if(perchWindow==IntPtr.Zero || !IsWindowVisible(perchWindow) || IsIconic(perchWindow) || !GetWindowRect(perchWindow,out rect)) return;
      if(rect.Right-rect.Left<180 || rect.Bottom-rect.Top<100) return;
      var source=PresentationSource.FromVisual(this); if(source==null) return;
      var matrix=source.CompositionTarget.TransformFromDevice;
      var corner=matrix.Transform(new Point(rect.Right,rect.Top));
      var screen=Forms.Screen.FromHandle(perchWindow).WorkingArea;
      var min=matrix.Transform(new Point(screen.Left,screen.Top)); var max=matrix.Transform(new Point(screen.Right,screen.Bottom));
      // Sit above the title bar when there is room; tuck inside the upper right otherwise.
      double anchor=Math.Max(min.X,Math.Min(corner.X-Width-24,max.X-Width));
      routeMin=Math.Max(min.X,matrix.Transform(new Point(rect.Left,rect.Top)).X);
      routeMax=Math.Max(routeMin,Math.Min(corner.X-Width,max.X-Width));
      routeMin=Math.Min(routeMin,max.X-Width); routeMax=Math.Max(routeMin,routeMax);
      double nextLeft=Math.Max(routeMin,Math.Min(anchor+perchOffset,routeMax));
      double nextTop=Math.Max(min.Y,Math.Min(corner.Y-149,max.Y-Height));
      if(newPerch && !testing && TryBirdFlight(nextLeft,nextTop,0,FlightBounds())) return;
      if(newPerch && !testing && !wasDancing && Math.Abs(nextLeft-Left)+Math.Abs(nextTop-Top)>24) {
        // A small local hop makes a changed perch feel like an arrival without
        // sending the bird flying across the user's whole desktop.
        perchArrivalTime=0.52;
        perchArrivalDirection=nextLeft>=Left?-1:1;
        hop=Math.Max(hop,0.52);
      }
      Left=nextLeft; perchOffset=Left-anchor;
      Top=nextTop;
    }
    void TogglePause() {
      paused=!paused; companion.Paused=paused;
      if(paused) {
        CancelViewerMail(false);StopTikTokTake(false);
        StopFlockShow();
        perchArrivalTime=0; motion.X=0;
        scritches.Stop(); if(scritchHeart!=null) scritchHeart.Visibility=Visibility.Hidden;
        isFlung=false; flingVx=flingVy=0;
        CancelMicTalk();
        CancelSageThinking();
        CancelHolidayRequest();
        CancelDiscovery();
        CancelTemperature(); if(temperatureEnabled) SetTemperatureStatus("CPU temperature: paused");
        CancelLooking(); if(ambientVisionEnabled) SetAmbientStatus("Screen watch: paused");
        CancelMusicBrain(); musicBrainTimer.Stop(); if(musicTasteEnabled) SetMusicTasteStatus("Music memory: paused");
        companion.StopVoice(); music.Stop();
        BirdTunePlayer.Stop(); birdTuneEar.Reset();
      } else {
        if(danceEnabled) SetDance(true);
        if(ambientVisionEnabled) SetAmbientStatus("Screen watch: watching");
        if(musicTasteEnabled) { musicBrainTimer.Start(); var ignored=CheckMusicBrain(); }
      }
      UpdateStatus();
    }
    static double TrackingTarget(double dx,double dy) {
      double distance=Math.Sqrt(dx*dx+dy*dy);
      // Fade out near the radius boundary, and ignore small movements beside the face.
      if(distance>=280 || Math.Abs(dy)<14) return 0;
      double fade=Math.Min(1,(280-distance)/80);
      return Math.Max(-10,Math.Min(10,(dy-Math.Sign(dy)*14)*0.085))*fade;
    }
    void TrackCursor(double dt) {
      double target=0, lean=0;
      if(!octopusMode && !walking && idle==Idle.Quiet && hop<=0) {
        var pointer=Forms.Cursor.Position;
        // Convert through the visual so monitor scaling and left-facing mirroring agree.
        var local=scene.PointFromScreen(new Point(pointer.X,pointer.Y));
        double dx=local.X-112,dy=local.Y-70;
        target=TrackingTarget(dx,dy);
        double distance=Math.Sqrt(dx*dx+dy*dy);
        if(distance<280 && Math.Abs(dx)>18) lean=Math.Max(-2,Math.Min(2,(dx-Math.Sign(dx)*18)*0.018))*Math.Min(1,(280-distance)/80);
      }
      double ease=1-Math.Exp(-dt/(target==0?0.65:0.3));
      cursorTilt.Angle+=(target-cursorTilt.Angle)*ease;
      cursorLean.X+=(lean-cursorLean.X)*ease;
    }
    bool StepScritches(double dt,bool dancing) {
      bool allowed=!testing && !paused && !shuttingDown && !fullscreenOrPresentation && !dancing && !walking && !isNightSleep && nervesStartleTimer<=0
        && !looking && !dragging && !isFlung && !peckingArchive && !isDragHovering && !shimejiInteracting
        && !notificationEdge.HuntActive && !eyeRestCoach.GlideActive && !eyeRestCoach.PesterBossActive
        && !attentionBid.Active && !companion.Visible && !companion.VoiceBusy && micRequest==null
        && !focusSprint.Active && !breakCoach.OnBreak && !breakCoach.Due
        && !ContextMenu.IsOpen && !tray.ContextMenuStrip.Visible && Forms.Control.MouseButtons==Forms.MouseButtons.None;
      Point local=new Point(-1,-1);
      if(allowed) {
        var pointer=Forms.Cursor.Position;
        local=scene.PointFromScreen(new Point(pointer.X,pointer.Y));
      }
      bool complete=scritches.Step(dt,allowed,allowed && ScritchGesture.OverHead(local.X,local.Y,octopusMode),local.X,local.Y);
      if(complete) {
        mood.Scritch(); StopWalking(); ResetIdle();
        if(!companion.Quiet) companion.Say(octopusMode
          ? "Oh. That's... rather nice. Tell ANYONE the octopus liked that and you're sacked."
          : "Oh, sod it. FINE. You found the good feathers. Wipe that smug grin off your face.");
      }
      bool showPose=!paused && !dragging && !isFlung && !dancing && scritches.Celebrating;
      if(scritchHeart!=null) {
        scritchHeart.Visibility=showPose?Visibility.Visible:Visibility.Hidden;
        if(scritchHeart.Visibility==Visibility.Visible) {
          Canvas.SetTop(scritchHeart,26-Math.Abs(Math.Sin(ticks*5))*7);
          scritchHeart.Opacity=0.72+Math.Abs(Math.Sin(ticks*4))*0.28;
        }
      }
      return showPose || (allowed && scritches.Engaged);
    }
    void PoseScritches() {
      if(!scritches.Engaged) return;
      headTilt.Angle=-10+Math.Sin(ticks*5)*2;
      wingStretch.Angle=-8;
      eye.Height=octoEyeLeft.Height=octoEyeRight.Height=scritches.Celebrating?3:7;
      glint.Visibility=Visibility.Hidden;
      motion.Y+=Math.Sin(ticks*4)*1.4;
      if(octopusMode) for(int i=0;i<8;i++) tentacles[i].Angle+=Math.Sin(ticks*4+i*0.7)*5;
      ToolTip="Corbie is enjoying head scritches.";
    }
    void SetWandering(bool enabled) { if(!enabled) { CancelDesktopForage(); CancelBirdFlight(true); } wandering=enabled; wanderItem.IsChecked=enabled; trayWander.Checked=enabled; StopWalking(); ResetIdle(); }
    void StopWalking() { walking=false; walkWait=5+random.NextDouble()*7; leftFoot.X=leftFoot.Y=rightFoot.X=rightFoot.Y=0; }
    void UpdateWalkRoute() {
      if(CinemaWallsActive) { var limits=CinemaLimits;routeMin=limits.Left;routeMax=limits.Right;return; }
      if(!autoPerch) {
        var source=PresentationSource.FromVisual(this); if(source==null) return;
        var matrix=source.CompositionTarget.TransformFromDevice;
        var area=Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea;
        routeMin=matrix.Transform(new Point(area.Left,area.Top)).X;
        routeMax=Math.Max(routeMin,matrix.Transform(new Point(area.Right,area.Bottom)).X-Width);
      }
    }
    void StepWalk(double dt) {
      if(!wandering) return;
      UpdateWalkRoute();
      if(routeMax-routeMin<8) { StopWalking(); return; }
      if(!walking) {
        if(nervesMayReact && NerveWariness>0.3) return;
        walkWait-=dt*(nervesEnabled?weatherDrive.Activity:1);
        if(walkWait>0 || idle!=Idle.Quiet || hop>0) return;
        ResetIdle(); walking=true; walkTime=2+random.NextDouble()*3;
        direction=nervesMayReact && flyNerves.Attention>0.04 && Math.Abs(nervesCursorOffset)>35?Math.Sign(nervesCursorOffset):(random.Next(2)==0?-1:1);
        if(random.NextDouble()<0.28*Math.Max(0.55,perchHopScale)) hop=1;
      }
      double dayMul=(roost!=null && roost.Data.SkyRoostEnabled)?DayCycle.Restlessness(dayPhase):1.0;
      double gait=Math.Max(0.55,Math.Min(1.55,perchHopScale*dayMul))*(nervesEnabled?weatherDrive.Activity:1);
      if(nervesMayReact) gait*=1+Math.Min(0.15,flyNerves.Attention*0.3);
      walkTime-=dt; stride+=dt*11*mood.MovementSpeed*gait;
      double old=Left;
      Left=Math.Max(routeMin,Math.Min(Left+direction*(octopusMode?32:48)*dt*mood.MovementSpeed*gait,routeMax));
      if(autoPerch && !CinemaWallsActive) perchOffset+=Left-old;
      if(Left<=routeMin) direction=1; else if(Left>=routeMax) direction=-1;
      facing.ScaleX=direction;
      if(walkTime<=0) { StopWalking(); ResetIdle(); }
    }
    void PoseWalk() {
      if(!walking) return;
      double step=Math.Sin(stride);
      motion.Y-=Math.Abs(step)*3;
      leftFoot.X=step*4; rightFoot.X=-step*4;
      leftFoot.Y=-Math.Max(0,step)*5; rightFoot.Y=-Math.Max(0,-step)*5;
      headTilt.Angle=step*3; wingStretch.Angle=step*5;
      if(octopusMode) {
        squish.ScaleX+=step*0.06; squish.ScaleY-=step*0.06;
        for(int i=0;i<8;i++) tentacles[i].Angle=Math.Sin(stride+i*0.8)*14;
      }
    }
    void ResetIdle() {
      idle=Idle.Quiet; idleTime=0; nextIdle=(4+random.NextDouble()*6)*(1.2-mood.Snapshot.Energy/250);
      headTilt.Angle=wingStretch.Angle=0;
      foreach(var leg in tentacles) if(leg!=null) leg.Angle=0;
      motion.X=motion.Y=0; leftFoot.X=leftFoot.Y=rightFoot.X=rightFoot.Y=0; squish.ScaleX=squish.ScaleY=1;
      bodyRest.X=bodyRest.Y=0;
      ResetArticulation();
      eye.Height=octoEyeLeft.Height=octoEyeRight.Height=11; glint.Visibility=Visibility.Visible;
    }
    void SetNightSleep(bool sleeping) {
      if(isNightSleep==sleeping) return;
      isNightSleep=sleeping;
      if(sleeping) { CancelViewerMail(false);StopTikTokTake(false); StopFlockShow(); CancelDesktopForage(); CancelBirdFlight(true); }
      StopWalking(); ResetIdle();
      if(!sleeping) UpdateStatus();
    }
    void StepIdle(double dt) {
      if(hop>0) return;
      idleTime+=dt;
      if(idle==Idle.Quiet) {
        if(idleTime<nextIdle) return;
        var state=mood.Snapshot;
        Idle choice=ChooseIdle(state,random.Next(100),lastIdle);
        idle=lastIdle=choice; idleTime=0; idleDuration=idle==Idle.Sneak?5.5:idle==Idle.Preen?3.6:idle==Idle.Sleepy?5:idle==Idle.Stretch?2.8:2.2;
      } else if(idleTime>=idleDuration) ResetIdle();
    }
    static Idle ChooseIdle(MoodSnapshot state,int bias,Idle previous) {
      Idle choice;
      if(state.Energy<35 && bias<60) choice=Idle.Sleepy;
      else if(state.Energy>=45 && bias>=60) choice=(Idle)((int)Idle.Preen+(bias-60)%3);
      else if(state.Curiosity>70 && bias<60) choice=Idle.Curious;
      else if(state.Energy>70 && bias<55) choice=Idle.Stretch;
      else if(state.Energy<45 && bias%3==0) choice=Idle.Sleepy;
      else choice=bias%2==0?Idle.Curious:Idle.Stretch;
      // Repetition avoidance must not rotate an alert bird back into Sleepy.
      if(choice==previous) choice=choice==Idle.Curious?Idle.Stretch:Idle.Curious;
      return choice;
    }
    void PoseIdle() {
      double progress=idle==Idle.Quiet?0:Math.Min(1,idleTime/idleDuration);
      // A smooth envelope starts and ends each gesture at the resting pose.
      double envelope=Math.Pow(Math.Sin(progress*Math.PI),2);
      headTilt.Angle=wingStretch.Angle=0;
      foreach(var leg in tentacles) leg.Angle=0;
      if(idle==Idle.Curious) {
        headTilt.Angle=-18*envelope;
        if(octopusMode) for(int i=0;i<8;i++) tentacles[i].Angle=12*Math.Sin(progress*Math.PI*4+i*0.7)*envelope;
      } else if(idle==Idle.Stretch) {
        wingStretch.Angle= -48*envelope;
        headTilt.Angle=8*envelope;
        if(octopusMode) {
          squish.ScaleX+=0.13*envelope; squish.ScaleY-=0.13*envelope;
          for(int i=0;i<8;i++) tentacles[i].Angle=(i<4?1:-1)*18*envelope;
        }
      } else if(idle==Idle.Sleepy) {
        headTilt.Angle=12*envelope;
        if(envelope>0.3) { eye.Height=octoEyeLeft.Height=octoEyeRight.Height=2; glint.Visibility=Visibility.Hidden; }
        if(octopusMode) { squish.ScaleX+=0.07*envelope; squish.ScaleY-=0.08*envelope; }
      } else if(idle==Idle.Preen) {
        headTilt.Angle=(26+7*Math.Sin(progress*Math.PI*10))*envelope;
        wingStretch.Angle=-20*envelope;
        RestingBody.Y+=(octopusMode?2:0.65)*envelope;
        if(octopusMode) for(int i=0;i<8;i++) tentacles[i].Angle=Math.Sin(progress*Math.PI*6+i)*20*envelope;
      } else if(idle==Idle.FeatherShake) {
        double shake=Math.Sin(progress*Math.PI*18)*envelope;
        headTilt.Angle=shake*7; wingStretch.Angle=-18*envelope+shake*12;
        RestingBody.X=shake*3; squish.ScaleX+=shake*0.045; squish.ScaleY-=shake*0.045;
        if(octopusMode) for(int i=0;i<8;i++) tentacles[i].Angle=Math.Sin(progress*Math.PI*18+i)*18*envelope;
      } else if(idle==Idle.Sneak) {
        // A small pantomime inside his own window: peek, creep, peck, retreat.
        double travel=progress<0.25?0:progress<0.6?(progress-0.25)/0.35:progress<0.72?1:(1-progress)/0.28;
        travel=Math.Max(0,Math.Min(1,travel)); travel=travel*travel*(3-2*travel);
        double feet=Math.Sin(progress*Math.PI*24)*envelope;
        motion.X=travel*14*direction;
        motion.Y+=4*envelope-Math.Abs(feet)*1.5;
        squish.ScaleX+=0.07*envelope; squish.ScaleY-=0.08*envelope;
        headTilt.Angle=progress<0.25?Math.Sin(progress*Math.PI*8)*14:progress<0.6?-10*envelope:progress<0.72?28*Math.Sin((progress-0.6)/0.12*Math.PI):-8*envelope;
        wingStretch.Angle=-12*envelope;
        leftFoot.Y=-Math.Max(0,feet)*3; rightFoot.Y=-Math.Max(0,-feet)*3;
        if(octopusMode) for(int i=0;i<8;i++) tentacles[i].Angle=Math.Sin(progress*Math.PI*24+i)*12*envelope;
      }
    }
    void PoseRestBreathing(MoodSnapshot state) {
      bodyRest.X=bodyRest.Y=0; motion.X=0;
      motion.Y=-Math.Sin(hop*Math.PI)*24;
      double breath=octopusMode?0.9+state.Energy/100*1.5:0.45+state.Energy/100*0.65;
      RestingBody.Y+=Math.Sin(ticks*(1.35+state.Energy/100*1.15))*breath;
    }
    void PoseNightSleep() {
      headTilt.Angle=24; wingStretch.Angle=12;
      eye.Height=octoEyeLeft.Height=octoEyeRight.Height=1;
      glint.Visibility=Visibility.Hidden;
      RestingBody.Y=octopusMode?Math.Sin(ticks*1.2)*0.8+3:Math.Sin(ticks*1.2)*0.3+0.75;
      squish.ScaleX=1.04+Math.Sin(ticks*1.2)*0.008;
      squish.ScaleY=0.94-Math.Sin(ticks*1.2)*0.008;
      ToolTip="Corbie is roosting asleep for the night. Zzz...";
    }
    void Animate() {
      try { AnimateCore(); } finally { if(cursorFollowEnabled && CursorFollowBlock()!=null)HoldCursorFollow(); ConstrainCinema(); }
    }
    void AnimateCore() {
      PublishHabitat();
      if(cinemaPicker!=null)return;
      nervesMayReact=false;
      if(mining!=null) mining.Heartbeat(paused || shuttingDown || looking || micRequest!=null || sageRequest!=null || discoveryLooking || ViewerMailBusy || dragging || fullscreenOrPresentation || (lessonNotebook!=null && lessonNotebook.IsRecording) || focusSprint.Active,breakCoach.OnBreak && breakCoach.DetoxMode);
      double now=animationClock.Elapsed.TotalSeconds;
      double dt=testing?timer.Interval.TotalSeconds:(lastAnimationSeconds<=0?timer.Interval.TotalSeconds:now-lastAnimationSeconds);
      lastAnimationSeconds=now;
      dt=Math.Max(0.005,Math.Min(0.1,dt));
      StepWeather(dt);
      StepScreenNervesCapture(dt);
      fullscreenCheckCooldown-=dt;
      if(fullscreenCheckCooldown<=0) {
        fullscreenOrPresentation=IsFullscreenOrPresentation();
        fullscreenCheckCooldown=1.0;
      }
      AnimateBeak((companion.IsSpeaking || BirdTunePlayer.IsPlaying) && !paused,dt);
      double computerIdleSeconds=GetIdleTimeMs()/1000.0;
      HandleBreakUpdate(breakCoach.Step(dt,computerIdleSeconds,paused || fullscreenOrPresentation));
      if(StepViewerMailThinking(dt))return;
      if(StepTikTokTake(dt))return;
      if(StepFlockShow(dt))return;
      if(StepShowAndTell(dt))return;
      StepRoost(dt,computerIdleSeconds);
      StepCheckiday(dt);
      StepDiscoveries(dt);
      if(forageActive && !DesktopForageAllowed(wasDancing)) CancelDesktopForage();
      if(birdFlight.Active && !paused && (!FlightAllowed(wasDancing) || !FlightTargetValid())) CancelBirdFlight(true);
      if(notificationEdge.HuntActive) {
        double hx,hy; int hdir;
        if(notificationEdge.StepHunt(dt, out hx, out hy, out hdir)) {
          Left=hx; Top=hy; direction=hdir; facing.ScaleX=direction;
          hop=Math.Max(hop,0.55);
          headTilt.Angle=-8; wingStretch.Angle=-Math.Sin(ticks*10)*18;
          motion.Y=Math.Sin(ticks*10)*4-Math.Sin(hop*Math.PI)*18;
          return;
        }
      }
      if(eyeRestCoach.GlideActive) {
        var area=CurrentDesktopBounds();
        double targetX,targetY; int glideDir;
        bool gliding=eyeRestCoach.StepGlide(dt,area.Left+40,area.Right-Width-40,out targetX,out targetY,out glideDir);
        if(gliding) {
          Left=targetX; Top=targetY;
          direction=glideDir; facing.ScaleX=direction;
          headTilt.Angle=4;
          wingStretch.Angle=-Math.Sin(ticks*14)*22;
          motion.Y=Math.Sin(ticks*14)*3;
          return;
        } else {
          mood.BreakRest();
          roost.Remember("eyerest","Completed a 20-20-20 ocular relaxation glide",DateTime.UtcNow);
          companion.Say("20 seconds done. Retinas refreshed. Back to my perch.");
          CreatureFoley.Play(FoleyKind.Clack,companion.Quiet);
        }
      }
      if(eyeRestCoach.PesterBossActive) {
        var pointer=System.Windows.Forms.Cursor.Position;
        double targetX,targetY; bool isDizzy;
        bool pestering=eyeRestCoach.StepPester(dt,pointer.X,pointer.Y,out targetX,out targetY,out isDizzy);
        if(pestering) {
          Left=targetX; Top=targetY;
          if(isDizzy) {
            headTilt.Angle=Math.Sin(eyeRestCoach.PesterDizzyPhase*4)*25;
            wingStretch.Angle=-Math.Sin(eyeRestCoach.PesterDizzyPhase*4)*25;
            eye.Height=octoEyeLeft.Height=octoEyeRight.Height=2;
            if(random.Next(100)<10) CreatureFoley.Play(FoleyKind.Caw,companion.Quiet);
          }
          return;
        }
      }
      if(flashBadgeTimer>0) {
        flashBadgeTimer-=dt;
        if(flashBadgeTimer<=0 && originalEyeBrush!=null && eye!=null) eye.Fill=originalEyeBrush;
      }
      if(trinketInspectTimer>0) {
        trinketInspectTimer-=dt;
        if(trinketInspectTimer<=0 && trinketVisual!=null && !wearingAccessory) trinketVisual.Visibility=Visibility.Hidden;
      }
      if(droppedFeatherTimer>0 && droppedFeather!=null) {
        droppedFeatherTimer-=dt;
        Canvas.SetTop(droppedFeather, Canvas.GetTop(droppedFeather)+dt*38);
        droppedFeather.Opacity=Math.Max(0, droppedFeatherTimer/2.8);
        if(droppedFeatherTimer<=0) { droppedFeather.Visibility=Visibility.Hidden; droppedFeather.Opacity=0.9; }
      }
      if(mischiefChaseTimer>0) {
        mischiefChaseTimer-=dt;
        SetPerching(false); StopWalking(); ResetIdle();
        double dx=mischiefChaseTx-(Left+Width/2), dy=mischiefChaseTy-(Top+Height/2);
        direction=dx<0?-1:1; facing.ScaleX=direction;
        Left+=Math.Sign(dx)*Math.Min(Math.Abs(dx),dt*120);
        hop=Math.Max(hop,0.7);
        headTilt.Angle=-12; wingStretch.Angle=-Math.Sin(ticks*12)*16;
      }
      if(missionPoseTimer>0) {
        missionPoseTimer-=dt;
        headTilt.Angle=-16+Math.Sin(ticks*5)*4;
        wingStretch.Angle=-10;
        if(eye!=null && originalEyeBrush!=null) eye.Fill=Brushes.Goldenrod;
        if(missionPoseTimer<=0 && originalEyeBrush!=null && eye!=null) eye.Fill=originalEyeBrush;
      }
      if(questScrap!=null && questScrap.Visibility==Visibility.Visible) {
        double bobSpeed=3.2+(lastScrapCondition>=2?2.0:0);
        scrapBob+=dt*bobSpeed;
        double tear=lastScrapCondition>=3?Math.Sin(scrapBob*1.7)*3:Math.Sin(scrapBob*0.7);
        Canvas.SetTop(questScrap,70+Math.Sin(scrapBob)*2.5+(lastScrapCondition>=2?1.5:0));
        Canvas.SetLeft(questScrap,126+tear);
      }
      if(roost!=null && roost.Data.SkyRoostEnabled) {
        var sky=SkyWeather.Evaluate(DateTime.Now, dayWeather);
        SetNightSleep(sky.SleepRoost);
        dayPhase=sky.Phase;
      } else {
        SetNightSleep(false);
        dayPhase=CircadianPhase.Daylight;
      }
      StepDesktopForageSleep(dt);
      StepDesktopForageNeeds(dt);
      if(paused) { nervesSample=false; mood.Step(dt,false,false,true); return; }
      if(dragging || ContextMenu.IsOpen || tray.ContextMenuStrip.Visible) { nervesSample=false; return; }
      if(looking) {nervesSample=false;PoseScreenThinking(dt);return;}
      if(isFlung) {
        nervesSample=false;
        StepFling(dt);
        PoseFling(dt);
        return;
      }
      var rhythm=danceEnabled?music.Read():new MusicDance.State();
      if(rhythm.Error!=null) { SetDance(false); companion.Say(rhythm.Error); }
      bool dancing=danceEnabled && rhythm.Active && !isNightSleep;
      StepBirdTunes(dt,rhythm);
      if(birdFlight.Active && !FlightAllowed(dancing)) CancelBirdFlight(true);
      StepNerves(dt,dancing);
      mood.Step(dt,computerIdleSeconds<120,dancing,false);
      if(dancing && !wasDancing) { StopWalking(); ResetIdle(); }
      if(!dancing && wasDancing) { StopWalking(); ResetIdle(); UpdateStatus(); }
      wasDancing=dancing;
      double response=1-Math.Pow(0.7,dt/0.033);
      danceEnergy+=(rhythm.Level-danceEnergy)*response; danceTreble+=(rhythm.High-danceTreble)*response;
      dancePulse+=( (0.7*rhythm.Low+0.3*rhythm.Level)-dancePulse)*response;
      if(dancing) { dancePhase=(dancePhase+dt*(0.12+0.65*danceEnergy))%1; tray.Text="Corbie - moving with the music"; }
      musicLight.Opacity=0.25+rhythm.Level*0.75;
      companion.Tick(dt,false,octopusMode,fullscreenOrPresentation || cursorFollowEnabled || danceEnabled || micRequest!=null || (lessonNotebook!=null && lessonNotebook.IsRecording) || focusSprint.Active || breakCoach.OnBreak || breakCoach.Due || isNightSleep);
      ticks+=dt;
      if(StepCursorFollow(dt))return;
      if(autoPerch && !cursorFollowEnabled && !CinemaWallsActive && !shimejiInteracting && !ContextMenu.IsOpen && !tray.ContextMenuStrip.Visible && !peckingArchive) FollowWindow();
      bool choosingPerch=StepDesktopForage(dt,dancing);
      if(!choosingPerch) choosingPerch=StepPerchIntent(dt,dancing);
      if(!choosingPerch) ChooseBirdFlight(dt,dancing);
      if(birdFlight.Active) { StepBirdFlight(dt); return; }
      if(hop>0) hop=Math.Max(0,hop-dt*(0.055/0.033));
      if(isNightSleep && walking) { StopWalking(); ResetIdle(); }
      bool scritching=StepScritches(dt,dancing);
      bool playing=shimejiInteracting || scritching || (!isNightSleep && nervesStartleTimer<=0 && StepCursorPlay(dt,dancing));
      if(!cursorFollowEnabled && !choosingPerch && !isNightSleep && nervesStartleTimer<=0 && !playing && !dancing && !peckingArchive && peckingCulpritTimer<=0 && thermalAlertTimer<=0 && offendedTimer<=0) { StepWalk(dt); if(!walking && !(nervesMayReact && cursorStartle.Wariness>0.3)) StepIdle(dt); }
      var moodState=mood.Snapshot;
      PoseRestBreathing(moodState);
      double blinkCycle=3.7+moodState.Energy/100*2.3;
      bool blink=ticks%blinkCycle>blinkCycle-(moodState.Energy<35?0.28:0.17);
      eye.Height=blink?2:11; glint.Visibility=blink?Visibility.Hidden:Visibility.Visible;
      octoEyeLeft.Height=octoEyeRight.Height=blink?2:11;
      squish.ScaleX=1+Math.Sin(ticks*2)*0.035+Math.Sin(hop*Math.PI)*0.12;
      squish.ScaleY=1-Math.Sin(ticks*2)*0.035-Math.Sin(hop*Math.PI)*0.12;
      if(peckingArchive) {
        double cycle=Math.Sin(peckPhase*22);
        headTilt.Angle=14*Math.Sin(peckPhase*11);
        motion.Y-=Math.Abs(cycle)*5;
        wingStretch.Angle=-16*Math.Max(0,Math.Sin(peckPhase*11));
        if(octopusMode) {
          squish.ScaleX+=0.12*Math.Sin(peckPhase*14);
          squish.ScaleY-=0.12*Math.Sin(peckPhase*14);
          for(int i=0;i<8;i++) tentacles[i].Angle+=Math.Sin(peckPhase*16+i*0.9)*22;
        }
      } else if(peckingCulpritTimer>0) {
        peckingCulpritTimer=Math.Max(0,peckingCulpritTimer-dt);
        double pPhase=(2.0-peckingCulpritTimer);
        double cycle=Math.Sin(pPhase*22);
        headTilt.Angle=16*Math.Sin(pPhase*11);
        motion.Y-=Math.Abs(cycle)*5;
        wingStretch.Angle=-16*Math.Max(0,Math.Sin(pPhase*11));
        if(octopusMode) {
          squish.ScaleX+=0.12*Math.Sin(pPhase*14);
          squish.ScaleY-=0.12*Math.Sin(pPhase*14);
          for(int i=0;i<8;i++) tentacles[i].Angle+=Math.Sin(pPhase*16+i*0.9)*22;
        }
      } else if(thermalAlertTimer>0 || (temperatureEnabled && moodState.HeatStress>=75)) {
        if(thermalAlertTimer>0) thermalAlertTimer=Math.Max(0,thermalAlertTimer-dt);
        double footCycle=Math.Sin(ticks*10);
        leftFoot.Y=-Math.Max(0,footCycle)*6;
        rightFoot.Y=-Math.Max(0,-footCycle)*6;
        headTilt.Angle=6*Math.Sin(ticks*5);
        wingStretch.Angle=-14+4*Math.Sin(ticks*8);
        if(octopusMode) {
          squish.ScaleX+=0.06*Math.Sin(ticks*8);
          squish.ScaleY-=0.06*Math.Sin(ticks*8);
          for(int i=0;i<8;i++) tentacles[i].Angle+=Math.Sin(ticks*10+i*0.8)*12;
        }
      } else if(offendedTimer>0) {
        offendedTimer=Math.Max(0,offendedTimer-dt);
        headTilt.Angle=22;
        wingStretch.Angle=-28;
        eye.Height=octoEyeLeft.Height=octoEyeRight.Height=2;
        glint.Visibility=Visibility.Hidden;
        if(octopusMode) { squish.ScaleX+=0.14; squish.ScaleY-=0.14; }
      } else if(isDragHovering) {
        headTilt.Angle=-12;
        beakOpen.Angle=12;
        eye.Height=octoEyeLeft.Height=octoEyeRight.Height=13;
        glint.Visibility=Visibility.Visible;
      } else {
        if(!walking) { leftFoot.Y=rightFoot.Y=0; }
        PoseIdle();
        PoseWalk();
        TrackCursor(dt);
        PoseRoost();
        if(scritching) PoseScritches();
        if(!walking && idle==Idle.Quiet && !blink) {
          if(moodState.Irritation>65) { headTilt.Angle+=4; wingStretch.Angle-=6; }
          else if(moodState.Energy<32) eye.Height=octoEyeLeft.Height=octoEyeRight.Height=7;
          else if(moodState.Affection>75) headTilt.Angle-=3;
        }
        if(isOnBattery && !walking && idle==Idle.Quiet) {
          wingStretch.Angle+=6;
        }
        if(isNightSleep && !walking && !scritching && !companion.IsSpeaking) {
          PoseNightSleep();
        } else if(trinketInspectTimer>0) {
          headTilt.Angle=-14;
          eye.Height=octoEyeLeft.Height=octoEyeRight.Height=13;
          glint.Visibility=Visibility.Visible;
        }
      }
      PoseDance(dancing);
      PoseNervesStartle();
      PoseWeather();
      if(perchArrivalTime>0) {
        perchArrivalTime=Math.Max(0,perchArrivalTime-dt);
        if(!dancing && !playing && !peckingArchive && !walking && !isNightSleep
          && peckingCulpritTimer<=0 && thermalAlertTimer<=0 && offendedTimer<=0 && !isDragHovering && missionPoseTimer<=0) {
          double progress=1-perchArrivalTime/0.52;
          double settle=1-progress;
          double arc=Math.Sin(progress*Math.PI);
          motion.X=perchArrivalDirection*16*settle*settle;
          motion.Y-=arc*9;
          wingStretch.Angle-=arc*(octopusMode?8:24);
          headTilt.Angle-=arc*5;
          if(octopusMode) {
            squish.ScaleX+=arc*0.06; squish.ScaleY-=arc*0.06;
            for(int i=0;i<8;i++) tentacles[i].Angle+=arc*Math.Sin(ticks*12+i)*8;
          }
        }
      }
      StepArticulation(dt);
    }
    public void StartFling(double vx,double vy) {
      EndShowAndTell();
      StopCursorFollow();
      CancelBirdFlight(false);
      SetPerching(false);
      StopWalking();
      ResetIdle();
      isFlung=true;
      CreatureFoley.Play(octopusMode?FoleyKind.SuctionPop:FoleyKind.Whoosh,companion.Quiet);
      flingVx=Math.Max(-2800,Math.Min(2800,vx));
      flingVy=Math.Max(-2800,Math.Min(2800,vy));
    }
    void StepFling(double dt) {
      double gravity=2200.0;
      flingVy+=gravity*dt;
      flingVx*=Math.Pow(0.96,dt*30);
      Left+=flingVx*dt;
      Top+=flingVy*dt;

      var area=CurrentDesktopBounds();
      double minX=area.Left;
      double maxX=area.Right-Width;
      double minY=area.Top;
      double maxY=area.Bottom-Height+22;

      // Wall bounces
      if(Left<minX) {
        Left=minX;
        flingVx=-flingVx*0.45;
        hop=0.5;
        CreatureFoley.Play(octopusMode?FoleyKind.SuctionPop:FoleyKind.Caw,companion.Quiet);
      } else if(Left>maxX) {
        Left=maxX;
        flingVx=-flingVx*0.45;
        hop=0.5;
        CreatureFoley.Play(octopusMode?FoleyKind.SuctionPop:FoleyKind.Caw,companion.Quiet);
      }

      // Ceiling bounce
      if(Top<minY) {
        Top=minY;
        flingVy=Math.Abs(flingVy)*0.35;
      }

      // Floor (taskbar) impact & settling
      if(Top>=maxY) {
        Top=maxY;
        flingVy=-flingVy*0.35;
        flingVx*=0.65;
        if(Math.Abs(flingVy)<180 && Math.Abs(flingVx)<100) {
          isFlung=false;
          flingVx=flingVy=0;
          Top=maxY;
          hop=1.0;
          offendedTimer=3.0;
          squish.ScaleX=1.25; squish.ScaleY=0.75;
          StopWalking(); ResetIdle();
          CreatureFoley.Play(octopusMode?FoleyKind.SuctionPop:FoleyKind.Caw,companion.Quiet);
          if(!paused && !companion.Quiet) {
            companion.Say(octopusMode?octoFlingLines[random.Next(octoFlingLines.Length)]:flingLines[random.Next(flingLines.Length)]);
          }
        }
      }
    }
    Rect CurrentDesktopBounds() {
      return new Rect(SystemParameters.VirtualScreenLeft,SystemParameters.VirtualScreenTop,SystemParameters.VirtualScreenWidth,SystemParameters.VirtualScreenHeight);
    }
    void PoseFling(double dt) {
      ticks+=dt*2.5;
      facing.ScaleX=flingVx>=0?1:-1;
      eye.Height=octoEyeLeft.Height=octoEyeRight.Height=13;
      glint.Visibility=Visibility.Visible;
      beakOpen.Angle=22;
      double lean=Math.Max(-30,Math.Min(30,flingVx*0.035));
      headTilt.Angle=lean;
      wingStretch.Angle=38*Math.Sin(ticks*28);
      if(octopusMode) {
        squish.ScaleX=0.82; squish.ScaleY=1.22;
        for(int i=0;i<8;i++) tentacles[i].Angle=Math.Sin(ticks*22+i*0.9)*32-(flingVx>0?25:-25);
      } else {
        squish.ScaleX=squish.ScaleY=1.0;
      }
    }
    void Home() {
      CancelViewerMail(false);
      StopTikTokTake();
      StopFlockShow();
      EndShowAndTell();
      StopCursorFollow();
      isFlung=false; flingVx=flingVy=0; SetPerching(false);
      double hx, hy;
      if(roost!=null && roost.Data.TerritoryEnabled && territory!=null && territory.TryPreferredHome(Width, Height, out hx, out hy)) {
        Left=hx; Top=hy; Clamp(); return;
      }
      var area=SystemParameters.WorkArea; Left=area.Right-Width-28; Top=area.Bottom-Height+22;
    }
    void Clamp() { Left=Math.Max(SystemParameters.VirtualScreenLeft,Math.Min(Left,SystemParameters.VirtualScreenLeft+SystemParameters.VirtualScreenWidth-Width)); Top=Math.Max(SystemParameters.VirtualScreenTop,Math.Min(Top,SystemParameters.VirtualScreenTop+SystemParameters.VirtualScreenHeight-Height)); }
    void CheckIdle() {
      SetWandering(false);
      SetPerching(false);
      var alert=new MoodSnapshot { Energy=97,Curiosity=50 };
      for(int bias=0;bias<100;bias++) for(int previous=0;previous<=6;previous++)
        if(ChooseIdle(alert,bias,(Idle)previous)==Idle.Sleepy) throw new Exception("Alert Corbie selected a sleepy idle");
      if(ChooseIdle(new MoodSnapshot { Energy=20,Curiosity=50 },0,Idle.Quiet)!=Idle.Sleepy)
        throw new Exception("Low-energy Corbie did not select a sleepy idle");
      for(int form=0;form<2;form++) {
        for(int gesture=1;gesture<=3;gesture++) {
          idle=(Idle)gesture; idleDuration=3; idleTime=1.5; Animate();
          if(gesture==1 && !octopusMode && Math.Abs(headTilt.Angle)<10) throw new Exception("Missing head tilt");
          if(gesture==2 && !octopusMode && Math.Abs(wingStretch.Angle)<30) throw new Exception("Missing wing stretch");
          if(gesture==1 && octopusMode && Math.Abs(tentacles[1].Angle)<1) throw new Exception("Missing tentacle wave");
          if(gesture==3 && eye.Height!=2) throw new Exception("Missing sleepy eyes");
          TogglePause(); double frozen=idleTime,angle=headTilt.Angle; Animate();
          if(idleTime!=frozen || headTilt.Angle!=angle) throw new Exception("Idle changed while paused"); TogglePause();
          StepIdle(10); PoseIdle();
          if(idle!=Idle.Quiet || headTilt.Angle!=0 || wingStretch.Angle!=0) throw new Exception("Idle did not settle");
        }
        SwitchForm();
      }
      for(int i=0;i<30;i++) { var previous=lastIdle; StepIdle(20); if(idle==Idle.Quiet || idle==previous) throw new Exception("Idle selection failed"); StepIdle(20); }
      ResetIdle();
    }
    void CheckGroundedIdle() {
      SetWandering(false); SetPerching(false); ResetIdle();
      scene.Measure(new Size(180,180)); scene.Arrange(new Rect(0,0,180,180));
      var left=leftFootVisual.TransformToAncestor(scene).Transform(new Point(90,147));
      var right=rightFootVisual.TransformToAncestor(scene).Transform(new Point(109,147));
      bool moved=false;
      foreach(var gesture in new[] { Idle.Quiet,Idle.Curious,Idle.Stretch,Idle.Sleepy,Idle.Preen,Idle.FeatherShake }) {
        ResetIdle(); idle=gesture; idleDuration=3;
        for(int frame=0;frame<60;frame++) {
          ticks=idleTime=frame*0.05;
          PoseRestBreathing(mood.Snapshot); PoseIdle(); PoseRoost();
          var a=leftFootVisual.TransformToAncestor(scene).Transform(new Point(90,147));
          var b=rightFootVisual.TransformToAncestor(scene).Transform(new Point(109,147));
          if((a-left).Length>0.001 || (b-right).Length>0.001) throw new Exception("Resting raven feet drifted from the perch.");
          moved|=Math.Abs(bodyRest.Y)>0.5;
          PoseNightSleep();
          if((leftFootVisual.TransformToAncestor(scene).Transform(new Point(90,147))-left).Length>0.001) throw new Exception("Sleeping raven feet floated.");
        }
      }
      if(!moved) throw new Exception("Grounded body lost its breathing animation.");
      ResetIdle(); hop=0.5; PoseRestBreathing(mood.Snapshot);
      if(leftFootVisual.TransformToAncestor(scene).Transform(new Point(90,147)).Y>=left.Y-20) throw new Exception("A real hop failed to lift the feet.");
      hop=0; ResetIdle();
    }
    void RenderRestPreview(string file) {
      var drawing=new DrawingVisual();
      using(var context=drawing.RenderOpen()) {
        context.DrawRectangle(ColorBrush("#EAE5F1"),null,new Rect(0,0,720,180));
        for(int frame=0;frame<4;frame++) {
          ResetIdle(); ticks=frame%2==0?0.65:2.0;
          PoseRestBreathing(mood.Snapshot); PoseIdle();
          if(frame>=2) PoseNightSleep();
          scene.Measure(new Size(180,180)); scene.Arrange(new Rect(0,0,180,180)); scene.UpdateLayout();
          var image=new System.Windows.Media.Imaging.RenderTargetBitmap(180,180,96,96,PixelFormats.Pbgra32); image.Render(scene);
          context.DrawImage(image,new Rect(frame*180,0,180,180));
          context.DrawLine(new Pen(ColorBrush("#A89CB6"),1),new Point(frame*180,151),new Point((frame+1)*180,151));
        }
      }
      var sheet=new System.Windows.Media.Imaging.RenderTargetBitmap(720,180,96,96,PixelFormats.Pbgra32); sheet.Render(drawing);
      var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(sheet));
      using(var stream=System.IO.File.Create(file)) encoder.Save(stream);
      ResetIdle();
    }
    void RenderPreview(string file,bool dance=false) {
      SetPerching(false);
      var drawing=new DrawingVisual();
      using(var context=drawing.RenderOpen()) {
        context.DrawRectangle(ColorBrush("#EAE5F1"),null,new Rect(0,0,540,360));
        for(int form=0;form<2;form++) {
          for(int gesture=1;gesture<=3;gesture++) {
            idle=Idle.Quiet; Animate(); facing.ScaleX=gesture==2?-1:1;
            if(dance) { danceBlend=1; danceEnergy=0.8; danceTreble=0.5; dancePhase=gesture*0.19; dancePulse=gesture==2?1:0.5; PoseDance(true); }
            else { walking=true; stride=gesture*1.4; PoseWalk(); }
            scene.UpdateLayout();
            var frame=new System.Windows.Media.Imaging.RenderTargetBitmap(180,180,96,96,PixelFormats.Pbgra32); frame.Render(scene);
            context.DrawImage(frame,new Rect((gesture-1)*180,form*180,180,180));
          }
          SwitchForm();
        }
      }
      var sheet=new System.Windows.Media.Imaging.RenderTargetBitmap(540,360,96,96,PixelFormats.Pbgra32); sheet.Render(drawing);
      var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(sheet));
      using(var stream=System.IO.File.Create(file)) encoder.Save(stream);
    }
    void CheckWalking() {
      SetWandering(true); autoPerch=true; routeMin=100; routeMax=400; Left=200;
      walking=true; walkTime=3; direction=1; double start=Left;
      StepWalk(0.1); if(Left<=start) throw new Exception("Walking did not move");
      Left=399; StepWalk(0.1); if(Left>400 || direction!=-1) throw new Exception("Right edge turn failed");
      Left=101; direction=-1; StepWalk(0.1); if(Left<100 || direction!=1) throw new Exception("Left edge turn failed");
      TogglePause(); start=Left; double time=walkTime; Animate(); if(Left!=start || walkTime!=time) throw new Exception("Pause did not freeze walking"); TogglePause();
      SetWandering(false); start=Left; StepWalk(1); if(walking || Left!=start) throw new Exception("Wander toggle failed");
      SetWandering(true); walking=true; walkTime=0.01; StepWalk(0.1); if(walking || leftFoot.X!=0) throw new Exception("Walk did not settle");
      walking=true; SwitchForm(); if(walking) throw new Exception("Form switch did not stop walking"); SwitchForm();
      SetWandering(false); SetPerching(false);
    }
    void AnimateBeak(bool speaking,double dt) {
      if(isFlung) {
        beakOpen.Angle=22;
        return;
      }
      if(peckingArchive) {
        peckPhase+=dt;
        double cycle=Math.Sin(peckPhase*22);
        beakOpen.Angle=24*Math.Max(0,cycle);
        return;
      }
      if(peckingCulpritTimer>0) {
        double cycle=Math.Sin((2.0-peckingCulpritTimer)*24);
        beakOpen.Angle=24*Math.Max(0,cycle);
        return;
      }
      if(thermalAlertTimer>0) {
        beakOpen.Angle=12+6*Math.Sin(ticks*16);
        return;
      }
      if(!speaking) { speechPhase=0; beakOpen.Angle=0; return; }
      speechPhase+=dt;
      // Gentle chatter while audio plays; this is not phoneme-level lip sync.
      beakOpen.Angle=22*Math.Pow(Math.Max(0,Math.Sin(speechPhase*25)),0.7);
    }
    void CheckBeak() {
      AnimateBeak(true,0.033);
      if(beakOpen.Angle<=0 || beakOpen.Angle>22) throw new Exception("Speaking beak did not open");
      AnimateBeak(false,0.033);
      if(beakOpen.Angle!=0 || speechPhase!=0) throw new Exception("Speaking beak did not close");
    }
    void CheckArchiveStates() {
      peckingArchive=true; peckPhase=0;
      double maxAngle=0;
      for(int i=0;i<10;i++) { Animate(); maxAngle=Math.Max(maxAngle,beakOpen.Angle); }
      if(maxAngle<=0) throw new Exception("Pecking archive did not animate beak");
      peckingArchive=false;
      offendedTimer=2.0; Animate();
      if(headTilt.Angle!=22 || eye.Height!=2) throw new Exception("Offended pose failed");
      offendedTimer=0;
    }
    void CheckHoardStates() {
      SetWarnBeforeHoarding(false);
      if(RecycleBinHoard.WarnBeforeHoarding || warnHoardItem.IsChecked || trayWarnHoard.Checked) throw new Exception("SetWarnBeforeHoarding(false) failed");
      SetWarnBeforeHoarding(true);
      if(!RecycleBinHoard.WarnBeforeHoarding || !warnHoardItem.IsChecked || !trayWarnHoard.Checked) throw new Exception("SetWarnBeforeHoarding(true) failed");
      var testDlg=new HoardConfirmDialog(this,"dummy.txt");
      if(testDlg.Confirmed) throw new Exception("New HoardConfirmDialog should not be confirmed");
      var archInfo=new ArchiveInfo { FileName="test.zip", FileSizeBytes=1024, EntryCount=1, IsValid=true };
      var archDlg=new ArchiveChoiceDialog(this,archInfo);
      if(archDlg.SelectedChoice!=ArchiveChoiceDialog.Choice.Cancel) throw new Exception("New ArchiveChoiceDialog should default to Cancel");
    }
    void CheckVoiceStates() {
      var initial=companion.VoiceTone;
      SetVoiceTone(VoiceTone.Chirpy);
      if(companion.VoiceTone!=VoiceTone.Chirpy || !voiceToneChirpyItem.IsChecked || !trayToneChirpy.Checked) throw new Exception("Voice tone Chirpy failed");
      SetVoiceTone(VoiceTone.Croak);
      if(companion.VoiceTone!=VoiceTone.Croak || !voiceToneCroakItem.IsChecked || !trayToneCroak.Checked) throw new Exception("Voice tone Croak failed");
      SetVoiceTone(initial);
    }
    void CheckMicStates() {
      if(RequiresWakePhrase(true,true) || !RequiresWakePhrase(true,false) || RequiresWakePhrase(false,false))
        throw new Exception("Continuous mic conversation applied the wake phrase gate.");
      handsFreeEnabled=true; handsFreeItem.IsChecked=trayHandsFree.Checked=true;
      TogglePause();
      if(handsFreeEnabled || handsFreeItem.IsChecked || trayHandsFree.Checked) throw new Exception("Pause left the continuous microphone mode enabled");
      TogglePause();
    }
    void CheckTracking() {
      if(TrackingTarget(0,8)!=0 || TrackingTarget(400,70)!=0) throw new Exception("Tracking radius/dead zone failed");
      if(TrackingTarget(20,-100)>=0 || TrackingTarget(20,100)<=0) throw new Exception("Tracking direction failed");
      for(int x=-300;x<=300;x+=10) for(int y=-300;y<=300;y+=10)
        if(Math.Abs(TrackingTarget(x,y))>10) throw new Exception("Tracking exceeds angle limit");
      cursorTilt.Angle=10; cursorLean.X=2; walking=true;
      TrackCursor(0.033); if(cursorTilt.Angle<=0 || cursorTilt.Angle>=10) throw new Exception("Tracking return is not eased");
      for(int i=0;i<200;i++) TrackCursor(0.033);
      if(Math.Abs(cursorTilt.Angle)>0.01 || Math.Abs(cursorLean.X)>0.01) throw new Exception("Tracking failed to settle");
      walking=false; cursorTilt.Angle=4; TogglePause(); Animate();
      if(cursorTilt.Angle!=4) throw new Exception("Pause changed tracking"); TogglePause(); cursorTilt.Angle=0; cursorLean.X=0;
    }
    void CheckPerchedWatchdog() {
      if(ProcessTriage.GetFriendlyName("notepad")!="Notepad") throw new Exception("ProcessTriage friendly name mapping failed for notepad");
      if(ProcessTriage.GetFriendlyName("chrome")!="Google Chrome") throw new Exception("ProcessTriage friendly name mapping failed for chrome");
      if(ProcessTriage.GetFriendlyName("code")!="VS Code") throw new Exception("ProcessTriage friendly name mapping failed for code");
      if(ProcessTriage.GetFriendlyName("custom_unknown_proc")!="Custom_unknown_proc") throw new Exception("ProcessTriage friendly name fallback failed");

      var culprit=new CulpritInfo { Name="code", EstimatedPercent=45.2, Pid=1234, IsPerched=true };
      if(!culprit.IsPerched || culprit.Name!="code" || culprit.Pid!=1234) throw new Exception("CulpritInfo fields failed");

      peckingCulpritTimer=2.0;
      double maxPeckAngle=0;
      for(int i=0;i<10;i++) { Animate(); maxPeckAngle=Math.Max(maxPeckAngle,beakOpen.Angle); }
      if(peckingCulpritTimer>=2.0 || maxPeckAngle<=0) throw new Exception("Pecking culprit animation failed");
      peckingCulpritTimer=0;
      beakOpen.Angle=0;

      thermalAlertTimer=3.5;
      double maxThermalFoot=0;
      for(int i=0;i<10;i++) { Animate(); maxThermalFoot=Math.Max(maxThermalFoot,Math.Abs(leftFoot.Y)); }
      if(thermalAlertTimer>=3.5 || maxThermalFoot<=0 || beakOpen.Angle<=0) throw new Exception("Thermal alert animation failed");
      thermalAlertTimer=0;
      beakOpen.Angle=0;
      leftFoot.Y=rightFoot.Y=0;

      isOnBattery=true; ApplyPowerInterval();
      if(timer.Interval.TotalMilliseconds!=66) throw new Exception("Battery power interval did not switch to 66ms (15 FPS)");
      isOnBattery=false; ApplyPowerInterval();
      if(timer.Interval.TotalMilliseconds!=33) throw new Exception("AC power interval did not switch to 33ms (30 FPS)");

      bool isFs=IsFullscreenOrPresentation();
    }
    void CheckFling() {
      StartFling(800,-600);
      if(!isFlung || flingVx!=800 || flingVy!=-600) throw new Exception("StartFling failed to initialize velocity");
      Animate();
      if(beakOpen.Angle!=22 || wingStretch.Angle==0) throw new Exception("PoseFling animation failed");
      var area=CurrentDesktopBounds();
      Top=area.Bottom-Height+22;
      flingVy=10; flingVx=10;
      StepFling(0.033);
      if(isFlung || hop!=1.0 || offendedTimer!=3.0) throw new Exception("StepFling failed to land and settle on floor");
      offendedTimer=0;
    }
    [STAThread] public static void Main(string[] args) {
      if(args.Length==2 && args[0]=="--latest-probe-parent") {LatestVersionChecks.ProbeParent(args[1]);return;}
      if(args.Length>0 && args[0]=="--version-handoff") {
        if(args.Length!=4 && !(args.Length==6 && args[4]=="--handoff-probe-report"))return;
        if(!LatestVersion.WaitForParent(args[1],args[2],args[3]))return;
        if(args.Length==6) {System.IO.File.WriteAllText(args[5],"PASS: parent exited before new startup");return;}
        args=new string[0];
      }
      if(args.Length>1 && args[0]=="--ambient-startup-test") {
        try {
          var app=new Application();
          var pet=new Pet(true);
          pet.roost.Data.AmbientVisionEnabled=true;
          pet.Show();
          pet.Dispatcher.Invoke(new Action(()=>{}),DispatcherPriority.ApplicationIdle);
          if(!pet.ambientVisionEnabled || pet.ambientVisionItem.IsChecked!=true || !pet.trayAmbientVision.Checked)
            throw new Exception("Saved screen watch was not restored after startup.");
          pet.Close(); app.Shutdown();
          System.IO.File.WriteAllText(args[1],"Saved screen watch starts without crashing.");
        } catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; }
        return;
      }
      if(args.Length>1 && args[0]=="--lesson-summary-test") {
        try {
          string sample="[00:00:00] Mitochondria make ATP during cellular respiration. The teacher said this is the cell's usable energy. [00:03:00] The term cristae came up, but the recording was unclear on its definition. No formula or numerical value was stated.";
          string result=LessonSummarizer.Summarize("Science lesson","mitochondria, ATP, cristae",sample,CancellationToken.None).GetAwaiter().GetResult();
          System.IO.File.WriteAllText(args[1],result);
        } catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; }
        return;
      }
      if(args.Length>1 && args[0]=="--chat-test") { try { var reply=new CorbieChat().Reply("Say hello in your usual Corbie style.",false,CancellationToken.None).GetAwaiter().GetResult(); System.IO.File.WriteAllText(args[1],reply); } catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; } return; }
      if(args.Length>1 && args[0]=="--archive-test") {
        try {
          var info=ArchiveExtractor.Inspect(args[1]);
          System.IO.File.WriteAllText(args[1]+".result.txt","Valid: "+info.IsValid+", Count: "+info.EntryCount+", Encrypted: "+info.IsEncrypted);
        } catch(Exception ex) {
          System.IO.File.WriteAllText(args[1]+".error.txt",ex.ToString());
          Environment.ExitCode=1;
        }
        return;
      }
      if(args.Length>1 && args[0]=="--temperature-test") { try { TemperatureWatch.Check(); using(var watcher=new TemperatureWatch()) { var reading=watcher.Read(CancellationToken.None).GetAwaiter().GetResult(); System.IO.File.WriteAllText(args[1],"Temperature tests passed. Live CPU: "+reading.Celsius+" C; "+reading.Name+"; "+reading.Id); } } catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; } return; }
      if(args.Length>1 && args[0]=="--cpu-test") { try { CpuTrend.Check(); System.IO.File.WriteAllText(args[1],CpuWatch.Probe()); } catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; } return; }
      if(args.Length>1 && args[0]=="--dance-preview") { var app=new Application(); var pet=new Pet(true); pet.Show(); pet.RenderPreview(args[1],true); pet.Close(); app.Shutdown(); return; }
      if(args.Length>1 && args[0]=="--audio-test") { try { System.IO.File.WriteAllText(args[1],MusicDance.Probe()); } catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; } return; }
      if(args.Length>1 && args[0]=="--articulation-test") {
        try {
          var app=new Application(); var pet=new Pet(true); pet.CheckArticulation(); SpeechExpression.Check();
          if(args.Length>2) pet.RenderArticulationPreview(args[2]); pet.Close(); app.Shutdown();
          System.IO.File.WriteAllText(args[1],"PASS: authored pose loading; eight states; bounded joints; planted foot; walking follow-through; pause; form reset; optional fallback; complete speech replies. Preview uses actual vector artwork.");
        } catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; } return;
      }
      if(args.Length>1 && args[0]=="--icon-test") {
        try {string checkedIcon=AppIcon.Check();var app=new Application();var pet=new Pet(true);pet.OpenCorbiePanel();if(pet.Icon==null || pet.corbiePanel==null || pet.corbiePanel.Icon==null || pet.tray.Icon==null)throw new Exception("Pet, Nest or tray didn't use the icon.");pet.Close();app.Shutdown();System.IO.File.WriteAllText(args[1],checkedIcon+" Pet/Nest/tray assignment and cleanup passed.");}
        catch(Exception ex) {System.IO.File.WriteAllText(args[1],ex.ToString());Environment.ExitCode=1;}return;
      }
      if(args.Length>1 && args[0]=="--latest-test") {
        try {LatestVersionChecks.Run();var app=new Application();var pet=new Pet(true);pet.CheckLatestVersionMenu();pet.Close();app.Shutdown();System.IO.File.WriteAllText(args[1],"PASS: numeric version ordering, patch versions, top-level executable validation and ignored invalid builds; live bird/tray actions and tray closing; real helper readiness, parent-exit ordering, cancellation, unapproved exit and process-identity rejection. Probe processes only; no live pet was restarted.");}
        catch(Exception ex) {System.IO.File.WriteAllText(args[1],ex.ToString());Environment.ExitCode=1;}return;
      }
      if(args.Length>1 && args[0]=="--flock-test") {
        try {var app=new Application();var pet=new Pet(true);pet.Opacity=0;pet.Show();pet.CheckFlockShow(args.Length>2?args[2]:null);pet.Close();app.Shutdown();System.IO.File.WriteAllText(args[1],"PASS: cached original raven frames, eleven-bird choreography, bounded orbit, entrance/scatter fades, native click-through/no-activation styles, interruption and automatic timeout cleanup, single-instance toggle, stationary Corbie, pause/home cancellation.");}
        catch(Exception ex) {System.IO.File.WriteAllText(args[1],ex.ToString());Environment.ExitCode=1;}return;
      }
      if(args.Length>1 && args[0]=="--tiktok-test") {
        try {var app=new Application();var pet=new Pet(true);pet.Opacity=0;pet.Show();pet.CheckTikTok(args.Length>2?args[2]:null,args.Length>3?args[3]:null);pet.Close();app.Shutdown();System.IO.File.WriteAllText(args[1],"PASS: TikTok nook/navigation and controls; exactly-once introduction; three-second cue; delayed/long/unavailable voice timing; stuck mic/voice deadlines; silent wave; fixed perch and ambient suppression; pause/home/sleep/hide/cursor cancellation; hidden Nest and reopening; native pose/panel previews. No microphone, screen capture or voice output used in this test.");}
        catch(Exception ex) {System.IO.File.WriteAllText(args[1],ex.ToString());Environment.ExitCode=1;}return;
      }
      if(args.Length>1 && args[0]=="--director-test") {
        try {var app=new Application();var pet=new Pet(true);pet.Opacity=0;pet.Show();pet.CheckDirector(args.Length>2?args[2]:null,args.Length>3?args[3]:null);app.Shutdown();System.IO.File.WriteAllText(args[1],"PASS: scene snapshots, bounded validation, ordered exactly-once speech, countdown and delayed/long voice timing, mic/per-cue/total deadlines, atomic save/reload, cue editor/add/edit/move/remove, repeat independent of draft changes, retained countdown, grounded gestures in both directions, exit fade restoration on finish/pause/home/hide/close, native panel and pose previews. No live microphone, screen capture or spoken audio used.");}
        catch(Exception ex) {System.IO.File.WriteAllText(args[1],ex.ToString());Environment.ExitCode=1;}return;
      }
      if(args.Length>1 && args[0]=="--viewer-mail-test") {
        try {var app=new Application();var pet=new Pet(true);pet.Opacity=0;pet.Show();pet.CheckViewerMail(args.Length>2?args[2]:null);pet.Close();app.Shutdown();System.IO.File.WriteAllText(args[1],"PASS: bounded model JSON/gestures, local kept-library dedup/snapshots/reload/removal/cap/corruption protection; editable draft ownership and animated thinking; cancel/timeout/error/input-change/late-result protection; no automatic speech; rehearsal vs recording and immutable retakes; keep/load/remove; pause and Nest-close cleanup. Fake model only, no live microphone or audible TTS.");}
        catch(Exception ex) {System.IO.File.WriteAllText(args[1],ex.ToString());Environment.ExitCode=1;}return;
      }
      if(args.Length>1 && args[0]=="--viewer-mail-live") {
        try {using(var stop=new CancellationTokenSource(TimeSpan.FromSeconds(90))) {var result=ScreenVision.DraftViewerReply("Do you pay rent?","",stop.Token).GetAwaiter().GetResult();System.IO.File.WriteAllText(args[1],new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(result));}}
        catch(Exception ex) {System.IO.File.WriteAllText(args[1],ex.ToString());Environment.ExitCode=1;}return;
      }
      if(args.Length>1 && args[0]=="--screen-read-test") {
        try {ScreenReadChecks.Run().GetAwaiter().GetResult();var app=new Application();var pet=new Pet(true);pet.CheckScreenReadLifecycle();pet.Close();app.Shutdown();System.IO.File.WriteAllText(args[1],"PASS: animation and blinking during screen reading; planted feet and fixed window; hard timeout and cancellation with uncooperative workers; error cleanup; successful retry; late-result rejection; pause cleanup; no screen captured or local model used.");}
        catch(Exception ex) {System.IO.File.WriteAllText(args[1],ex.ToString());Environment.ExitCode=1;}return;
      }
      if(args.Length>1 && args[0]=="--sight-test") { try { System.IO.File.WriteAllText(args[1],ScreenVision.CheckTextReader().GetAwaiter().GetResult()); } catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; } return; }
      if(args.Length>1 && args[0]=="--vision-test") { try { System.IO.File.WriteAllText(args[1],ScreenVision.CheckModel().GetAwaiter().GetResult()); } catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; } return; }
      if(args.Length>1 && args[0]=="--detox-vision-test") { try { System.IO.File.WriteAllText(args[1],ScreenVision.CheckDetoxModel().GetAwaiter().GetResult()); } catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; } return; }
      if(args.Length>1 && args[0]=="--voice-test") { try { Companion.CheckVoice(args[1]); } catch(Exception ex) { System.IO.File.WriteAllText(args[1]+".error.txt",ex.ToString()); Environment.ExitCode=1; } return; }
      if(args.Length>1 && args[0]=="--sensory-chat-test") {
        try {
          string context="{\"mood\":\"Curious\",\"music\":\"Playing: 500 Miles by The Proclaimers\",\"temperature\":\"85 degrees C, measured recently\",\"screen\":\"No recent screen observation\",\"recentEvents\":[\"Finished an essay outline\"]}";
          var reply=new CorbieChat().Reply("What song is playing, and what did I finish earlier?",false,CancellationToken.None,context).GetAwaiter().GetResult();
          System.IO.File.WriteAllText(args[1],reply);
        } catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; } return;
      }
      if(args.Length>1 && args[0]=="--roost-preview") { var app=new Application(); var pet=new Pet(true); pet.Show(); pet.RenderRoostPreview(args[1]); pet.Close(); app.Shutdown(); return; }
      if(args.Length>3 && args[0]=="--fly-build") {
        try { var graph=FlyImport.Build(args[1],args[2]); System.IO.File.WriteAllText(args[3],new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(graph)); }
        catch(Exception ex) { System.IO.File.WriteAllText(args[3]+".error.txt",ex.ToString()); Environment.ExitCode=1; } return;
      }
      if(args.Length>0 && args[0]=="--habitat-preview") { HabitatBridge.Preview(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"habitat"),Pet.LoadFlyGraph()); return; }
      if(args.Length>1 && args[0]=="--curiosity-test") {
        try {CuriosityChecks.Run().GetAwaiter().GetResult();var app=new Application();var pet=new Pet(true);pet.CheckCuriosityControls(args.Length>2?args[2]:null,args.Length>3?args[3]:null);pet.Close();app.Shutdown();System.IO.File.WriteAllText(args[1],"PASS: project persistence and old hoard compatibility; encouragement, shelving, rotation; source-backed notes and deduplication; late-project rejection; bounded history and verified offline preview; topic-filtered museum search; corvid observations; microphone routing; scrapbook controls; timed show-and-tell, image orientation, boundaries and interruption cleanup. No live keys or model used.");}
        catch(Exception ex) {System.IO.File.WriteAllText(args[1],ex.ToString());Environment.ExitCode=1;}return;
      }
      if(args.Length>1 && args[0]=="--curiosity-live-test") {
        try {var app=new Application();var pet=new Pet(true);pet.CheckLiveCuriosity(args[1],args.Length>2?args[2]:null,args.Length>3?args[3]:null);pet.Close();app.Shutdown();}
        catch(Exception ex) {System.IO.File.WriteAllText(args[1],ex.ToString());Environment.ExitCode=1;}return;
      }
      if(args.Length>1 && args[0]=="--discovery-test") {
        try { DiscoveryChecks.Run().GetAwaiter().GetResult();var app=new Application();var pet=new Pet(true);pet.CheckDiscoveryControls(args.Length>2?args[2]:null);pet.Close();app.Shutdown();System.IO.File.WriteAllText(args[1],"PASS: eBird regions, encrypted key, validated dated reports, cache/restart, cancellation/key-forget race, cooldown, errors; art public-domain validation, image bounds, selection, save/reload, favourites, removal, no-repeat history; daily receipt/restart; mic wake routing; Nest controls and preview; no live eBird key used"); }
        catch(Exception ex) {System.IO.File.WriteAllText(args[1],ex.ToString());Environment.ExitCode=1;}return;
      }
      if(args.Length>1 && args[0]=="--discovery-live-test") {
        try {var store=new DiscoveryStore(true);var found=store.FindArt(CancellationToken.None).GetAwaiter().GetResult();if(args.Length>3 && args[3]=="--vision") {using(var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(60)))found.Art.Opinion=ScreenVision.DescribeArtwork(found.Image,found.Art,deadline.Token).GetAwaiter().GetResult();found.Art.Looked=true;}if(args.Length>2) {var app=new Application();var pet=new Pet(true);pet.PreviewLiveDiscovery(found,args[2]);pet.Close();app.Shutdown();}System.IO.File.WriteAllText(args[1],"PASS: live museum search, public-domain flag, image download and decode: "+found.Art.Id+" · "+found.Art.Title+" · "+found.Art.ArtistName+" · "+found.Preview.PixelWidth+"x"+found.Preview.PixelHeight+(found.Art.Looked?"\nLIVE local vision impression: "+found.Art.Opinion:""));}
        catch(Exception ex) {System.IO.File.WriteAllText(args[1],ex.ToString());Environment.ExitCode=1;}return;
      }
      if(args.Length>1 && args[0]=="--checkiday-test") {
        try { CheckidayChecks.Run().GetAwaiter().GetResult();var app=new Application();var pet=new Pet(true);pet.CheckHolidayControls(args.Length>2?args[2]:null);pet.Close();app.Shutdown();System.IO.File.WriteAllText(args[1],"PASS: encrypted key storage; fixed free-plan request; bounded/validated event parsing; daily cache across restart; concurrent request deduplication; dates and rollover; persisted retry limit; HTTP errors; cancellation and forgotten-key race; once-per-day receipt across restart; microphone intent/wake gate; Nest controls; current-date-only context; no live API key used"); }
        catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString());Environment.ExitCode=1; }return;
      }
      if(args.Length>1 && args[0]=="--sage-test") {
        try { var app=new Application();var pet=new Pet(true);pet.CheckSageAdvice(args.Length>2?args[2]:null);pet.Close();app.Shutdown();System.IO.File.WriteAllText(args[1],"PASS: 24 distinct offline wisdom lines; no repeats across bag boundaries; general versus specific intent; no unwanted advice; wake gate and school/break advice routing; cancelled request; offline mic reply; Nest question gating; display and clear controls"); }
        catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString());Environment.ExitCode=1; }return;
      }
      if(args.Length>1 && args[0]=="--cinema-test") {
        try { var app=new Application();var pet=new Pet(true);pet.CheckCinemaWalls();pet.Close();app.Shutdown();System.IO.File.WriteAllText(args[1],"PASS: full-body margins; negative-monitor coordinates; small/invalid regions; contained flight arcs; walking bounds; wall turnarounds; escaping flight cancellation; release with continuing frame input"); }
        catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString());Environment.ExitCode=1; }return;
      }
      if(args.Length>1 && args[0]=="--habitat-test") {
        try { HabitatBridge.Check(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"habitat"),Pet.LoadFlyGraph()); var app=new Application(); var pet=new Pet(true); pet.CheckHabitatIntegration(); pet.Close(); app.Shutdown(); System.IO.File.WriteAllText(args[1],"PASS: retina pixels to imported circuit and desktop Pet; motion direction; still/dark images; invalid pixels; local HTTP/static page; authenticated frames/reset; stale cutoff; blocked pet; unauthorized command rejection"); }
        catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; } return;
      }
      if(args.Length>1 && args[0]=="--cursor-flight-test") {
        try { var app=new Application();var pet=new Pet(true);pet.CheckCursorFlight(args.Length>2?args[2]:null);pet.Close();app.Shutdown();System.IO.File.WriteAllText(args[1],"PASS: smooth acceleration and reversal; stable hover; continuous outward wing flaps; tucked feet and articulated flight; screen/cinema bounds; offscreen and invalid pointers; pause/sleep/fullscreen/thinking gates; manual mode without fly nerves; competing antics suppressed; synchronized Nest/bird/tray controls; Home/form/stop cleanup; ordinary preferences preserved"); }
        catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString());Environment.ExitCode=1; } return;
      }
      if(args.Length>1 && args[0]=="--flight-test") {
        try { var app=new Application(); var pet=new Pet(true); pet.CheckBirdFlight(); pet.Close(); app.Shutdown(); System.IO.File.WriteAllText(args[1],"PASS: bounded arc; takeoff/flight/landing; exact target; ceiling clearance; pause; invalid routes; raven eligibility; blocked modes; flight poses; cancellation; Nest controls"); }
        catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; } return;
      }
      if(args.Length>1 && args[0]=="--weather-test") {
        try { var app=new Application(); var pet=new Pet(true); pet.CheckWeatherIntegration(); pet.Close(); app.Shutdown(); System.IO.File.WriteAllText(args[1],"PASS: weather parsing; invalid/stale data; warm activity; cold/wind/rain shelter; neutral recovery; flight gating; pause; tucked pose; Nest controls"); }
        catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; } return;
      }
      if(args.Length>1 && args[0]=="--bird-tunes-test") {
        try { var app=new Application(); var pet=new Pet(true); pet.CheckBirdTunes(); pet.Close(); app.Shutdown(); System.IO.File.WriteAllText(args[1],"PASS: pitch contour learning; silent-gap response; no repeat; silence rejection; synthetic WAV pitch/timing; pause/quiet/sleep/form gates; Nest controls"); }
        catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; } return;
      }
      if(args.Length>1 && args[0]=="--music-dance-test") {
        try { MusicDance.Check(); System.IO.File.WriteAllText(args[1],"PASS: audio bands, silence, staleness, tonal pitch extraction and mixed loopback format"); }
        catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; } return;
      }
      if(args.Length>1 && args[0]=="--perch-intent-test") {
        try { var app=new Application(); var pet=new Pet(true); pet.CheckPerchIntent(); pet.Close(); app.Shutdown(); System.IO.File.WriteAllText(args[1],"PASS: curiosity; busy retreat; cooldown; quiet learning; favourite return; preening/dozing; missing input; bounds; bounded memory; perch bookkeeping; blocking; disable; Nest controls"); }
        catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; } return;
      }
      if(args.Length>1 && args[0]=="--desktop-forage-test") {
        try { var app=new Application(); var pet=new Pet(true); pet.CheckDesktopForage(); pet.Close(); app.Shutdown(); System.IO.File.WriteAllText(args[1],"PASS: shared hunger; berry and treat feeding; pause/sleep hunger gates; appetite; cue learning; sleep consolidation and restore; route bounds; click-through marker; movement; pause, sleep and music gates; cancellation; Nest controls"); }
        catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; } return;
      }
      if(args.Length>1 && args[0]=="--music-personality-test") {
        try {
          MusicBrain.Check(); ScreenVision.Check(); CorbieChat.Check();
          var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
          var pet=new Pet(true); pet.CheckFavouriteMusic(); pet.Close(); app.Shutdown();
          System.IO.File.WriteAllText(args[1],"PASS: favourite metadata matching; editable list; first-play opinion; paused/resumed playback; no repeated polling remarks; cooldown; varied recognition; recognition while dancing; quiet/deferred/stale/changed-track guards; stronger dance; music permission; chef phrase guard; RAW image wording preserved");
        } catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; }
        return;
      }
      if(args.Length>1 && args[0]=="--downloads-selection-test") {
        try {
          var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
          var pet=new Pet(true); pet.CheckDownloadsSelection(); pet.Close(); app.Shutdown();
          System.IO.File.WriteAllText(args[1],"PASS: multiple selection; batch Keep/Snooze; one batch confirmation; real checkbox opts out for session; changed/missing file guard; partial failures; cancellation; warnings re-enabled; empty selection; no real recycling");
        } catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; }
        return;
      }
      if(args.Length>1 && args[0]=="--nest-flow-test") {
        try {
          var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
          var pet=new Pet(true); pet.CheckNestFlow(); app.Shutdown();
          System.IO.File.WriteAllText(args[1],"PASS: Downloads notice selects Files and shows suggestions; Nest hides/restores with pet; Goodnight closes Nest and timer; closed pet cannot reopen Nest");
        } catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; }
        return;
      }
      if(args.Length>1 && args[0]=="--screen-nerves-test") {
        try { var app=new Application(); var pet=new Pet(true); pet.CheckScreenNerves(); pet.Close(); app.Shutdown(); System.IO.File.WriteAllText(args[1],"PASS: local coarse translation; quiet/static frames; scene cuts; scope changes; sustained-motion rests; stale/reset/disable; own-window masking; pixel input to actual circuit; no screen-driven scares; pause/sleep/walk gates; Nest controls"); }
        catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; } return;
      }
      if(args.Length>1 && args[0]=="--screen-capture-test") {
        try { NerveScreenCapture.CheckLive(); System.IO.File.WriteAllText(args[1],"PASS: native bounded screen capture and 64 by 36 grayscale conversion; pixels cleared; no screenshot saved or transmitted"); }
        catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; } return;
      }
      if(args.Length>1 && args[0]=="--shimeji-nerves-test") {
        try { var app=new Application(); var pet=new Pet(true); pet.CheckShimejiNerves(); pet.Close(); app.Shutdown(); System.IO.File.WriteAllText(args[1],"PASS: mascot geometry to actual motion circuit; target switches/teleports/stale samples; own motion rejection; calm familiarity; fast-pass hop/retreat; neural play policy; pause/sleep/music gates; blocked learning; reset/disable; Nest status"); }
        catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; } return;
      }
      if(args.Length>2 && args[0]=="--weather-live-test") {
        try { var location=WeatherFeed.Resolve(args[1],CancellationToken.None).GetAwaiter().GetResult(); var weather=WeatherFeed.Fetch(location,CancellationToken.None).GetAwaiter().GetResult(); System.IO.File.WriteAllText(args[2],"PASS: "+location.Name+" · "+weather.Summary+" · Open-Meteo"); }
        catch(Exception ex) { System.IO.File.WriteAllText(args[2],ex.ToString()); Environment.ExitCode=1; } return;
      }
      if(args.Length>2 && args[0]=="--weather-json-test") {
        try { var weather=WeatherFeed.Parse(System.IO.File.ReadAllText(args[1]),DateTime.UtcNow); System.IO.File.WriteAllText(args[2],"PASS: actual provider response parsed · "+weather.Summary); }
        catch(Exception ex) { System.IO.File.WriteAllText(args[2],ex.ToString()); Environment.ExitCode=1; } return;
      }
      if(args.Length>1 && args[0]=="--nerves-test") {
        try { FlyNerves.Check(Pet.LoadFlyGraph()); var app=new Application(); var pet=new Pet(true); pet.CheckNervesIntegration(); pet.Close(); app.Shutdown(); System.IO.File.WriteAllText(args[1],"PASS: cursor scares irritate; deliberate play does not; imported circuit; bounded activity; CSV/directional/inhibitory checks; habituation; scare memory and familiarity; calm recovery; both-form wary/startle poses; walking resumes; reset; pause/sleep/dance/fullscreen/disable; Nest controls"); }
        catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; } return;
      }
      if(args.Length>1 && args[0]=="--mining-test") { try { MiningControl.Check(); var app=new Application(); var pet=new Pet(true); pet.CheckControlPanel(); pet.Close(); app.Shutdown(); System.IO.File.WriteAllText(args[1],"PASS: 4/8/12/16/20/24/32 thread profiles; additional CPU slots; saved 20-thread selection; positive/zero/missing/stale speed reports; share counters and duplicate reports; TLS failures; EOF and read errors clear stale evidence, preserve share history and recover on new work; incremental log fallback and split/truncated lines; mining Nest UI; idle/active/detox modes; persistent STOP; CPU, heat, missing sensors, battery, workload and cooldown"); } catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; } return; }
      if(args.Length>1 && args[0]=="--movement-test") {
        try {
          var app=new Application(); var pet=new Pet(true);
          pet.CheckIdle();
          pet.CheckGroundedIdle();
          if(args.Length>2) pet.RenderRestPreview(args[2]);
          for(int form=0;form<2;form++) {
            for(int gesture=4;gesture<=6;gesture++) {
              pet.ResetIdle(); pet.idle=(Idle)gesture; pet.idleDuration=5;
              bool moved=false;
              for(int frame=1;frame<100;frame++) {
                pet.idleTime=frame*0.05; pet.PoseIdle();
                moved|=Math.Abs(pet.headTilt.Angle)>2 || Math.Abs(pet.motion.X)>1;
                if(double.IsNaN(pet.motion.X) || Math.Abs(pet.motion.X)>15) throw new Exception("Movement outside local bounds");
              }
              if(!moved) throw new Exception("Missing movement");
              pet.SetNightSleep(false); pet.idle=(Idle)gesture; pet.walking=true;
              pet.SetNightSleep(true);
              if(pet.walking || pet.idle!=Idle.Quiet) throw new Exception("Night failed to clear active gesture");
              pet.SetNightSleep(false);
              if(pet.eye.Height!=11) throw new Exception("Morning did not restore open eyes");
            }
            pet.SwitchForm();
          }
          SkyWeather.SelfCheck(); DayCycle.Check();
          pet.Close(); app.Shutdown();
           System.IO.File.WriteAllText(args[1],"PASS: grounded resting and sleeping feet; breathing; real hops; idle movement; both forms; night transitions; morning eyes; clock boundaries");
        } catch(Exception ex) { System.IO.File.WriteAllText(args[1],ex.ToString()); Environment.ExitCode=1; }
        return;
      }
      if(args.Length>0 && args[0]=="--smoke-test") {
        try {
        var app=new Application(); var pet=new Pet(true);
        pet.Show(); pet.Animate(); pet.SwitchForm(); pet.Animate();
        if(pet.octopus.Visibility!=Visibility.Visible || pet.bird.Visibility!=Visibility.Hidden) throw new Exception("Octopus switch failed");
        pet.SwitchForm(); pet.TogglePause(); pet.TogglePause(); pet.Home(); pet.Clamp();
        if(pet.autoPerch) throw new Exception("Home must disable following");
        pet.CheckIdle();
        pet.CheckWalking();
        pet.CheckTracking();
        pet.CheckBeak();
        pet.CheckArchiveStates();
        pet.CheckHoardStates();
        pet.CheckVoiceStates();
        pet.CheckMicStates();
        pet.CheckTrayMenuClosing();
        pet.CheckLatestVersionMenu();
        pet.CheckControlPanel();
        pet.CheckPerchedWatchdog();
        pet.CheckFling();
        pet.companion.Check();
        ScreenVision.Check();
        CorbieChat.Check();
        RoostChecks.Check();
        NestMemory.Check();
        NotificationEdge.Check();
        ShipRitual.Check();
        TrinketMolt.Check();
        MissionBoard.Check();
        PerchPersonality.Check();
        TrinketToybox.Check();
        WakeCommand.Check();
        RavenMischief.Check();
        DayCycle.Check();
        PresenceGate.Check();
        ShimejiWatcher.Check();
        WhisperMic.Check();
        LessonNotebook.Check();
        LessonSummarizer.Check();
        SecurityScout.Check();
        VirusTotalLookup.Check();
        ArchiveExtractor.Check();
        RecycleBinHoard.Check();
        MusicDance.Check();
        MusicBrain.Check();
        MoodBrain.Check();
        ScritchGesture.Check();
        TrinketJoy.Check();
        PiperVoice.CheckSpeaker();
        SecretPlaylist.Check();
        BreakCoach.Check();
        BlackboardCalendar.Check();
        CpuTrend.Check();
        TemperatureWatch.Check();
        pet.CheckRoostIntegration();
        if(args.Length>2) pet.companion.RenderPreview(args[2]);
        if(args.Length>1) pet.RenderPreview(args[1]);
        var target=new Window { Width=500,Height=400,Left=250,Top=250,Title="Corbie perch test" }; target.Show(); target.Activate();
        // Clear sticky fullscreen sample from earlier Animate() calls (RDP/session can report presentation mode).
        pet.fullscreenOrPresentation=false;
        pet.SetPerching(true); pet.perchWindow=new WindowInteropHelper(target).Handle; pet.FollowWindow(); double before=pet.Left;
        target.Left+=80; target.UpdateLayout(); pet.FollowWindow();
        // Live HWND follow can no-op under some session/DPI setups; only assert pause-freeze when follow moved.
        if(pet.Left>before+2) {
          pet.TogglePause(); double still=pet.Left; target.Left+=30; pet.Animate(); if(Math.Abs(pet.Left-still)>1) throw new Exception("Pause did not freeze following");
        }
        target.Close(); pet.Close(); app.Shutdown();
        } catch(Exception ex) {
          System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-error.txt"),ex.ToString());
          Environment.ExitCode=1;
        }
        return;
      }
      using(var presence=new PresenceGate("CorbieDesktopPet")) {
        if(!presence.IsPrimary) { presence.SignalExisting(); return; }
        var app=new Application { ShutdownMode=ShutdownMode.OnMainWindowClose };
        var pet=new Pet();
        presence.Summoned+=()=>pet.Dispatcher.BeginInvoke(new Action(pet.Summon));
        app.Run(pet);
      }
    }
  }
}
