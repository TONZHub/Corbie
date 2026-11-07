using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CorbiePet {
  public partial class Pet {
    Window corbiePanel;
    DispatcherTimer panelRefreshTimer;
    bool refreshingPanel;
    bool nestHiddenWithPet;
    TabControl panelTabs;
    StackPanel panelNav;
    readonly List<Button> panelNavButtons=new List<Button>();
    CheckBox panelHandsFree,panelVoice,panelQuiet,panelAmbient,panelTemperature,panelCpu,panelDance,panelTaste,panelPerch,panelWander,panelTaskbar;
    CheckBox panelExtractFolder,panelReveal,panelConfirmPreview,panelWarnHoard;
    CheckBox panelBreakEnabled,panelDetox;
    ComboBox panelTone;
    ComboBox panelVoiceModel;
    readonly List<string> panelVoiceIds=new List<string>();
    ComboBox panelWorkMinutes,panelBreakMinutes;
    TextBlock panelMicStatus,panelScreenStatus,panelTemperatureStatus,panelCpuStatus,panelMusicStatus,panelMoodStatus,panelBreakStatus;
    ProgressBar panelEnergy,panelCuriosity,panelAffection,panelIrritation,panelHunger;
    ProgressBar panelBreakProgress;
    Button panelPause;

    // Nest palette: charcoal bark, warm lining, moss, amber and ink.
    static readonly Brush PanelBackground=new SolidColorBrush(Color.FromRgb(0x2A,0x1F,0x16));
    static readonly Brush CardBackground=new SolidColorBrush(Color.FromRgb(0x3D,0x2E,0x22));
    static readonly Brush LiningBrush=new SolidColorBrush(Color.FromRgb(0xF6,0xEC,0xD9));
    static readonly Brush TwigBrush=new SolidColorBrush(Color.FromRgb(0x6B,0x4A,0x2E));
    static readonly Brush MossBrush=new SolidColorBrush(Color.FromRgb(0x62,0x7D,0x4A));
    static readonly Brush Accent=new SolidColorBrush(Color.FromRgb(0xD2,0x98,0x42));
    static readonly Brush PanelText=new SolidColorBrush(Color.FromRgb(0x36,0x29,0x1E));
    static readonly Brush MutedText=new SolidColorBrush(Color.FromRgb(0x6E,0x5A,0x43));
    static readonly Brush HeaderMutedText=new SolidColorBrush(Color.FromRgb(0xD3,0xC0,0xA2));
    static readonly Brush EggBrush=new SolidColorBrush(Color.FromRgb(0xF5,0xF0,0xE1));
    static readonly Brush InkBrush=new SolidColorBrush(Color.FromRgb(0x35,0x29,0x20));
    static readonly Brush CardBorderBrush=new SolidColorBrush(Color.FromRgb(0xD1,0xB9,0x91));

    public void OpenCorbiePanel() {
      if(shuttingDown || farewellSaid) return;
      try {
        nestHiddenWithPet=false;
        if(corbiePanel!=null) {
          if(!corbiePanel.IsVisible) corbiePanel.Show();
          if(corbiePanel.WindowState==WindowState.Minimized) corbiePanel.WindowState=WindowState.Normal;
          corbiePanel.Topmost=true; corbiePanel.Activate();
          if(panelRefreshTimer!=null) panelRefreshTimer.Start();
          OnNestReshown();
          return;
        }
        corbiePanel=BuildCorbiePanel();
        panelRefreshTimer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(400) };
        panelRefreshTimer.Tick+=(s,e)=> { try { RefreshCorbiePanel(); } catch { } try { StepTrinketJoy(0.4); } catch { } };
        panelRefreshTimer.Start();
        corbiePanel.Closed+=(s,e)=> { CancelViewerMail();if(panelRefreshTimer!=null) { panelRefreshTimer.Stop(); panelRefreshTimer=null; } corbiePanel=null; nestHiddenWithPet=false; };
        try { RefreshCorbiePanel(); } catch(Exception ex) {
          if(companion!=null) companion.SaySilent("Nest refresh hiccup: "+ex.Message);
        }
        corbiePanel.Show(); corbiePanel.Topmost=true; corbiePanel.Activate();
      } catch(Exception ex) {
        try {
          if(panelRefreshTimer!=null) { panelRefreshTimer.Stop(); panelRefreshTimer=null; }
          if(corbiePanel!=null) { var broken=corbiePanel; corbiePanel=null; broken.Close(); }
        } catch { }
        if(companion!=null) companion.Say("Nest stuck shut: "+ex.Message);
      }
    }
    bool OpenNestPage(string name) {
      OpenCorbiePanel();
      if(corbiePanel==null || panelTabs==null) return false;
      foreach(TabItem tab in panelTabs.Items) {
        if(!string.Equals(Convert.ToString(tab.Header),name,StringComparison.Ordinal)) continue;
        panelTabs.SelectedItem=tab; return true;
      }
      return false;
    }
    void SyncNestPetVisibility(bool visible) {
      if(corbiePanel==null) return;
      if(!visible && corbiePanel.IsVisible) {
        nestHiddenWithPet=true; corbiePanel.Hide();
        if(panelRefreshTimer!=null) panelRefreshTimer.Stop();
      } else if(visible && nestHiddenWithPet && !shuttingDown && !farewellSaid) {
        nestHiddenWithPet=false; corbiePanel.Show();
        if(panelRefreshTimer!=null) panelRefreshTimer.Start();
      }
    }
    void CloseCorbiePanel() {
      nestHiddenWithPet=false;
      if(panelRefreshTimer!=null) { panelRefreshTimer.Stop(); panelRefreshTimer=null; }
      if(corbiePanel!=null) corbiePanel.Close();
    }
    Window BuildCorbiePanel() {
      var window=new Window {
        Title="Corbie's Nest",Icon=AppIcon.WindowImage,Width=860,Height=760,MinWidth=700,MinHeight=560,
        Background=NestBackground(),WindowStartupLocation=WindowStartupLocation.CenterScreen,
        ResizeMode=ResizeMode.CanResize,ShowInTaskbar=true,Topmost=true
      };
      ApplyNestResources(window);
      // Avoid Owner=transparent pet; owned Nest can fail to surface on some DPI/session setups.
      var root=new Grid();
      root.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
      root.RowDefinitions.Add(new RowDefinition());
      var headerShell=new Border {
        Background=new SolidColorBrush(Color.FromRgb(0x31,0x24,0x19)),
        BorderBrush=Accent, BorderThickness=new Thickness(0,0,0,2),
        Padding=new Thickness(24,15,24,14),
        Child=BuildNestHeader()
      };
      root.Children.Add(headerShell);
      var nestBody=new Grid { Margin=new Thickness(14,12,14,14) };
      nestBody.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(178) });
      nestBody.ColumnDefinitions.Add(new ColumnDefinition());
      Grid.SetRow(nestBody,1);
      var navShell=new Border {
        Background=new SolidColorBrush(Color.FromRgb(0x31,0x25,0x1B)),
        BorderBrush=new SolidColorBrush(Color.FromRgb(0x55,0x3C,0x28)),BorderThickness=new Thickness(1),
        CornerRadius=new CornerRadius(22,8,8,22),Padding=new Thickness(10,14,10,12),
        Effect=new DropShadowEffect { BlurRadius=14,ShadowDepth=2,Opacity=0.24,Color=Colors.Black }
      };
      var navGrid=new Grid();
      navGrid.RowDefinitions.Add(new RowDefinition()); navGrid.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
      panelNav=new StackPanel(); panelNavButtons.Clear();
      navGrid.Children.Add(new ScrollViewer { Content=panelNav,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled });
      var navFooter=new Border { Background=new SolidColorBrush(Color.FromRgb(0x3B,0x2D,0x20)),CornerRadius=new CornerRadius(10),Padding=new Thickness(10,8,10,8),Margin=new Thickness(0,12,0,0) };
      navFooter.Child=new TextBlock { Text="14 nooks - 1 curious bird",Foreground=HeaderMutedText,FontSize=10,TextAlignment=TextAlignment.Center };
      Grid.SetRow(navFooter,1); navGrid.Children.Add(navFooter); navShell.Child=navGrid;
      nestBody.Children.Add(navShell);
      var contentShell=new Border {
        Background=LiningBrush,BorderBrush=CardBorderBrush,BorderThickness=new Thickness(1),
        CornerRadius=new CornerRadius(8,22,22,8),Margin=new Thickness(10,0,0,0),Padding=new Thickness(2),
        Effect=new DropShadowEffect { BlurRadius=16,ShadowDepth=2,Opacity=0.18,Color=Colors.Black }
      };
      Grid.SetColumn(contentShell,1);
      panelTabs=new TabControl {
        Background=LiningBrush, BorderThickness=new Thickness(0),Padding=new Thickness(0)
      };
      var hiddenTabs=new Style(typeof(TabItem));
      hiddenTabs.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Collapsed));
      panelTabs.ItemContainerStyle=hiddenTabs;
      // SelectionChanged bubbles up from inner lists/combos; only react to the Nest tab strip itself.
      panelTabs.SelectionChanged+=(s,e)=> { if(e.OriginalSource!=panelTabs) return; UpdateNestNavigation(); OnNestTabMaybeHoard(); };
      contentShell.Child=panelTabs; nestBody.Children.Add(contentShell); root.Children.Add(nestBody);
      BuildCompanionTab(); BuildTikTokTab(); BuildAwarenessTab(); BuildBrowserTab(); BuildMusicTab(); BuildSchoolTab(); BuildSecurityTab(); BuildBreaksTab(); BuildBehaviorTab(); BuildFilesTab(); BuildRoostTab(); BuildLoreTab(); BuildMiningTab(); BuildDiscoveriesTab();
      panelTabs.SelectedIndex=0; UpdateNestNavigation();
      window.Content=root; return window;
    }
    Brush NestBackground() {
      return new LinearGradientBrush(
        Color.FromRgb(0x21,0x18,0x12),Color.FromRgb(0x3C,0x2B,0x1D),new Point(0,0),new Point(1,1));
    }
    void ApplyNestResources(Window window) {
      var textBox=new Style(typeof(TextBox));
      textBox.Setters.Add(new Setter(Control.BackgroundProperty,EggBrush));
      textBox.Setters.Add(new Setter(Control.ForegroundProperty,InkBrush));
      textBox.Setters.Add(new Setter(Control.BorderBrushProperty,CardBorderBrush));
      textBox.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(1)));
      textBox.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(9,7,9,7)));
      window.Resources.Add(typeof(TextBox),textBox);
      var combo=new Style(typeof(ComboBox));
      combo.Setters.Add(new Setter(Control.BackgroundProperty,EggBrush));
      combo.Setters.Add(new Setter(Control.ForegroundProperty,InkBrush));
      combo.Setters.Add(new Setter(Control.BorderBrushProperty,CardBorderBrush));
      combo.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(8,5,8,5)));
      combo.Setters.Add(new Setter(FrameworkElement.MinHeightProperty,34.0));
      window.Resources.Add(typeof(ComboBox),combo);
    }
    UIElement BuildNestHeader() {
      var row=new Grid();
      row.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
      row.ColumnDefinitions.Add(new ColumnDefinition());
      row.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
      var art=BuildNestArt();
      Grid.SetColumn(art,0);
      row.Children.Add(art);
      var text=new StackPanel { Margin=new Thickness(17,2,0,0), VerticalAlignment=VerticalAlignment.Center };
      text.Children.Add(new TextBlock {
        Text="Corbie's Nest", Foreground=LiningBrush, FontSize=25, FontWeight=FontWeights.Bold
      });
      text.Children.Add(new TextBlock {
        Text="Home base for habits, tiny schemes, and shiny nonsense.",
        Foreground=HeaderMutedText, FontSize=12, Margin=new Thickness(0,3,0,0), TextWrapping=TextWrapping.Wrap
      });
      text.Children.Add(new TextBlock {
        Text="TWIGS  -  MOSS  -  ONE FURIOUS BIRD",
        Foreground=new SolidColorBrush(Color.FromRgb(0x9E,0xB5,0x79)), FontSize=10, FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,6,0,0)
      });
      Grid.SetColumn(text,1);
      row.Children.Add(text);
      var badges=new StackPanel { VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(18,0,0,0) };
      badges.Children.Add(HeaderBadge("v0.115",Accent,InkBrush));
      badges.Children.Add(HeaderBadge("LOCAL NEST",MossBrush,Brushes.White));
      var goodnight=new Button { Content="Goodnight, Corbie",Background=CardBackground,Foreground=LiningBrush,
        BorderBrush=TwigBrush,Padding=new Thickness(9,5,9,5),Margin=new Thickness(0,7,0,0) };
      goodnight.Click+=(s,e)=>RequestGoodbye(); badges.Children.Add(goodnight);
      Grid.SetColumn(badges,2); row.Children.Add(badges);
      return row;
    }
    Border HeaderBadge(string text,Brush background,Brush foreground) {
      return new Border { Background=background,CornerRadius=new CornerRadius(12),Padding=new Thickness(10,4,10,4),Margin=new Thickness(0,2,0,2),
        Child=new TextBlock { Text=text,Foreground=foreground,FontSize=9,FontWeight=FontWeights.Bold,HorizontalAlignment=HorizontalAlignment.Center } };
    }
    FrameworkElement BuildNestArt() {
      var canvas=new Canvas { Width=112, Height=78,ClipToBounds=false };
      var halo=new Ellipse { Width=76,Height=55,Fill=new SolidColorBrush(Color.FromArgb(35,0xF6,0xEC,0xD9)) };
      Canvas.SetLeft(halo,18);Canvas.SetTop(halo,11);canvas.Children.Add(halo);
      var bowl=new Path { Data=Geometry.Parse("M 8,35 Q 56,75 104,35 Q 96,72 56,76 Q 16,72 8,35 Z"),Fill=new SolidColorBrush(Color.FromRgb(0x57,0x39,0x25)),Stroke=new SolidColorBrush(Color.FromRgb(0x2A,0x1B,0x12)),StrokeThickness=2 };
      canvas.Children.Add(bowl);
      var rim=new Path { Data=Geometry.Parse("M 7,35 Q 56,16 105,35 Q 56,51 7,35 Z"),Fill=new SolidColorBrush(Color.FromRgb(0x7A,0x55,0x34)),Stroke=Accent,StrokeThickness=1.5 };
      canvas.Children.Add(rim);
      string[] woven={"M 13,43 Q 55,61 99,42","M 17,51 Q 56,67 95,50","M 24,59 Q 56,70 88,58","M 12,31 Q 38,46 56,63","M 101,31 Q 76,46 56,63"};
      foreach(string data in woven) canvas.Children.Add(new Path { Data=Geometry.Parse(data),Stroke=new SolidColorBrush(Color.FromRgb(0xA2,0x76,0x47)),StrokeThickness=2,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round });
      for(int i=0;i<3;i++) {
        var egg=new Ellipse { Width=18,Height=24,Fill=i==1?EggBrush:new SolidColorBrush(Color.FromRgb(0xDF,0xE7,0xD1)),Stroke=CardBorderBrush,StrokeThickness=1 };
        Canvas.SetLeft(egg,37+i*13);Canvas.SetTop(egg,22-Math.Abs(1-i)*2);canvas.Children.Add(egg);
        var speck=new Ellipse { Width=3,Height=3,Fill=TwigBrush,Opacity=0.55 };Canvas.SetLeft(speck,44+i*13);Canvas.SetTop(speck,29);canvas.Children.Add(speck);
      }
      var mossLeft=new Ellipse { Width=24,Height=9,Fill=MossBrush,Opacity=0.95 };Canvas.SetLeft(mossLeft,13);Canvas.SetTop(mossLeft,34);canvas.Children.Add(mossLeft);
      var mossRight=new Ellipse { Width=20,Height=8,Fill=MossBrush,Opacity=0.9 };Canvas.SetLeft(mossRight,79);Canvas.SetTop(mossRight,36);canvas.Children.Add(mossRight);
      return canvas;
    }
    StackPanel TabBody() {
      return new StackPanel { Margin=new Thickness(20,18,20,22) };
    }
    void AddTab(string name,StackPanel body) {
      var scroll=new ScrollViewer {
        VerticalScrollBarVisibility=ScrollBarVisibility.Auto, Content=body,
        Background=LiningBrush,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled
      };
      var tab=new TabItem {
        Header=name, Content=scroll
      };
      panelTabs.Items.Add(tab);
      int index=panelTabs.Items.Count-1;
      if(index==0 || name=="Behavior") panelNav.Children.Add(new TextBlock {
        Text=index==0?"EVERYDAY":"THE ROOST",Foreground=new SolidColorBrush(Color.FromRgb(0xA9,0x91,0x70)),
        FontSize=9,FontWeight=FontWeights.Bold,Margin=new Thickness(10,index==0?0:14,0,6)
      });
      var navButton=NestNavButton(name,index);
      panelNavButtons.Add(navButton); panelNav.Children.Add(navButton);
    }
    Button NestNavButton(string name,int index) {
      string initials=name.Length>1?name.Substring(0,2).ToUpperInvariant():name.ToUpperInvariant();
      var row=new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(34) }); row.ColumnDefinitions.Add(new ColumnDefinition());
      var badge=new Border { Width=28,Height=28,CornerRadius=new CornerRadius(14),Background=CardBackground,VerticalAlignment=VerticalAlignment.Center,
        Child=new TextBlock { Text=initials,Foreground=HeaderMutedText,FontSize=9,FontWeight=FontWeights.Bold,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center } };
      row.Children.Add(badge);
      var label=new TextBlock { Text=name,Foreground=HeaderMutedText,FontSize=12,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(4,0,0,0) };
      Grid.SetColumn(label,1);row.Children.Add(label);
      var button=new Button { Content=row,Tag=index,HorizontalContentAlignment=HorizontalAlignment.Stretch,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(7,6,7,6),Margin=new Thickness(0,1,0,1),Cursor=System.Windows.Input.Cursors.Hand };
      button.Click+=(s,e)=>panelTabs.SelectedIndex=index;
      button.MouseEnter+=(s,e)=> { if(panelTabs.SelectedIndex!=index) button.Background=new SolidColorBrush(Color.FromRgb(0x43,0x34,0x26)); };
      button.MouseLeave+=(s,e)=> { if(panelTabs.SelectedIndex!=index) button.Background=Brushes.Transparent; };
      return button;
    }
    void UpdateNestNavigation() {
      if(panelTabs==null) return;
      for(int i=0;i<panelNavButtons.Count;i++) {
        var button=panelNavButtons[i]; bool selected=i==panelTabs.SelectedIndex;
        button.Background=selected?new SolidColorBrush(Color.FromRgb(0xF1,0xE2,0xC7)):Brushes.Transparent;
        var row=button.Content as Grid; if(row==null) continue;
        var badge=row.Children[0] as Border; var label=row.Children[1] as TextBlock;
        if(badge!=null) badge.Background=selected?Accent:CardBackground;
        if(label!=null) { label.Foreground=selected?InkBrush:HeaderMutedText; label.FontWeight=selected?FontWeights.Bold:FontWeights.SemiBold; }
      }
    }
    Border Section(string title,params UIElement[] children) {
      var stack=new StackPanel();
      var titleRow=new Grid { Margin=new Thickness(0,0,0,11) };
      titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(5) }); titleRow.ColumnDefinitions.Add(new ColumnDefinition());
      var mark=new Border { Background=Accent,CornerRadius=new CornerRadius(3),Margin=new Thickness(0,1,0,1) };titleRow.Children.Add(mark);
      var heading=new TextBlock { Text=title,Foreground=InkBrush,FontWeight=FontWeights.Bold,FontSize=15,Margin=new Thickness(10,0,0,0) };Grid.SetColumn(heading,1);titleRow.Children.Add(heading);
      stack.Children.Add(titleRow);
      foreach(var child in children) stack.Children.Add(child);
      return new Border {
        Background=new SolidColorBrush(Color.FromRgb(0xFB,0xF4,0xE7)),
        BorderBrush=CardBorderBrush, BorderThickness=new Thickness(1),
        CornerRadius=new CornerRadius(14),Effect=new DropShadowEffect { BlurRadius=10,ShadowDepth=1,Opacity=0.13,Color=Colors.Black },
        Padding=new Thickness(16), Margin=new Thickness(0,0,0,14), Child=stack
      };
    }
    Button ActionButton(string text,Action action) {
      var button=new Button {
        Content=text, Padding=new Thickness(13,8,13,8), Margin=new Thickness(0,2,8,8),
        Background=TwigBrush, Foreground=EggBrush,
        BorderBrush=new SolidColorBrush(Color.FromRgb(0x4B,0x32,0x20)), BorderThickness=new Thickness(1), MinWidth=135,MinHeight=34,
        FontWeight=FontWeights.SemiBold,Cursor=System.Windows.Input.Cursors.Hand
      };
      button.MouseEnter+=(s,e)=>button.Background=new SolidColorBrush(Color.FromRgb(0x7D,0x58,0x35));
      button.MouseLeave+=(s,e)=>button.Background=TwigBrush;
      button.Click+=(s,e)=>action(); return button;
    }
    WrapPanel Actions(params Button[] buttons) { var wrap=new WrapPanel(); foreach(var button in buttons) wrap.Children.Add(button); return wrap; }
    CheckBox Toggle(string text,Action<bool> changed) {
      var label=new TextBlock { Text=text,Foreground=InkBrush,TextWrapping=TextWrapping.Wrap,FontSize=13,LineHeight=18 };
      var box=new CheckBox { Content=label,Foreground=InkBrush,Margin=new Thickness(0,2,0,3),Padding=new Thickness(6,6,6,6),MinHeight=32,VerticalContentAlignment=VerticalAlignment.Center };
      box.Checked+=(s,e)=> { if(!refreshingPanel) changed(true); };
      box.Unchecked+=(s,e)=> { if(!refreshingPanel) changed(false); };
      return box;
    }
    TextBlock Status() { return new TextBlock { Foreground=MutedText,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,7),FontSize=12,LineHeight=18 }; }
    FrameworkElement Meter(string name,out ProgressBar bar) {
      var row=new Grid { Margin=new Thickness(0,3,0,5) };
      row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(92) }); row.ColumnDefinitions.Add(new ColumnDefinition());
      row.Children.Add(new TextBlock { Text=name,Foreground=InkBrush,VerticalAlignment=VerticalAlignment.Center,FontSize=12,FontWeight=FontWeights.SemiBold });
      bar=new ProgressBar { Minimum=0,Maximum=100,Height=12,Background=new SolidColorBrush(Color.FromRgb(0xE2,0xD4,0xB9)),Foreground=MossBrush,BorderThickness=new Thickness(0),VerticalAlignment=VerticalAlignment.Center };
      Grid.SetColumn(bar,1); row.Children.Add(bar); return row;
    }

    void BuildCompanionTab() {
      var body=TabBody();
      panelMoodStatus=Status();
      body.Children.Add(Section("Inner life",panelMoodStatus,Meter("Energy",out panelEnergy),Meter("Curiosity",out panelCuriosity),Meter("Affection",out panelAffection),Meter("Irritation",out panelIrritation),Meter("Hunger",out panelHunger),Actions(
        ActionButton("How are you feeling?",()=>companion.Say(mood.Report(octopusMode)))
      ),new TextBlock { Text="Try moving the pointer gently back and forth over Corbie's head. Yes, he accepts scritches.",Foreground=MutedText,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,2,0,0) }));
      body.Children.Add(Section("Conversation",Actions(
        ActionButton("Talk to me",()=>companion.Talk(octopusMode)),ActionButton("Tell me a joke",companion.Joke),ActionButton("Give Corbie a treat",GiveTreat),
        ActionButton("Talk through mic",()=> { var ignored=ToggleMicTalk(); }),ActionButton("Clear mic memory",()=> { chat.Clear(); companion.Say("Conversation cleared. My opinions of you, sadly, remain intact."); })
      ),panelMicStatus=Status()));
      BuildSageControls(body);
      BuildHolidayControls(body);
      panelHandsFree=Toggle("Keep microphone conversation going",enabled=> { var ignored=SetHandsFree(enabled); });
      panelVoice=Toggle("Speak bubbles aloud",SetVoice); panelQuiet=Toggle("Quiet mode",SetQuiet);
      panelVoiceModel=new ComboBox { Margin=new Thickness(0,7,0,3),MinWidth=260 };
      panelVoiceIds.Clear();
      foreach(string id in companion.InstalledVoices) { panelVoiceIds.Add(id); panelVoiceModel.Items.Add(PiperVoice.FriendlyVoiceName(id)); }
      panelVoiceModel.SelectionChanged+=(s,e)=> { if(refreshingPanel || panelVoiceModel.SelectedIndex<0) return; SetVoiceModel(panelVoiceIds[panelVoiceModel.SelectedIndex]); };
      panelTone=new ComboBox { Margin=new Thickness(0,7,0,3),MinWidth=220 };
      panelTone.Items.Add("Bird -- inquisitive creature"); panelTone.Items.Add("Normal -- human voice"); panelTone.Items.Add("Chirpy -- high and light"); panelTone.Items.Add("Croak -- gravelly raven"); panelTone.Items.Add("Grump -- snarly angry crow");
      panelTone.SelectionChanged+=(s,e)=> { if(refreshingPanel || panelTone.SelectedIndex<0) return; SetVoiceTone((VoiceTone)panelTone.SelectedIndex); };
      body.Children.Add(Section("Voice and interruptions",panelHandsFree,panelVoice,panelQuiet,new TextBlock { Text="Neural voice",Foreground=MutedText,Margin=new Thickness(0,6,0,0) },panelVoiceModel,new TextBlock { Text="Delivery style",Foreground=MutedText,Margin=new Thickness(0,6,0,0) },panelTone,Actions(ActionButton("Preview voice",()=>companion.Say("Oh, a voice test. Lovely. Performing on command like a seal at the zoo. Happy now, are we?")),ActionButton("Install voice pack",InstallVoicePack))));
      AddTab("Companion",body);
    }
    void BuildAwarenessTab() {
      var body=TabBody();
      panelAmbient=Toggle("Ambient screen watch",SetAmbientVision);
      body.Children.Add(Section("Screen awareness",Actions(ActionButton("Look at my screen now",()=> { var ignored=LookAtScreen(); }),ActionButton("Cancel screen look",CancelLooking)),panelAmbient,panelScreenStatus=Status()));
      panelTemperature=Toggle("Watch CPU temperature",SetTemperatureWatch); panelCpu=Toggle("Watch CPU load",SetCpuWatch);
      body.Children.Add(Section("Laptop awareness",panelTemperature,panelTemperatureStatus=Status(),panelCpu,panelCpuStatus=Status()));
      AddTab("Awareness",body);
    }
    void BuildMusicTab() {
      var body=TabBody();
      panelDance=Toggle("Dance to computer audio",SetDance); panelTaste=Toggle("Learn my music and form opinions",SetMusicTaste);
      body.Children.Add(Section("Music brain",panelDance,panelTaste,panelMusicStatus=Status(),Actions(
        ActionButton("Opinion on this song",()=>companion.Say(musicBrain.Opinion())),ActionButton("I like this song",()=>companion.Say(musicBrain.TeachCurrent(true))),
        ActionButton("Not for me",()=>companion.Say(musicBrain.TeachCurrent(false))),ActionButton("Music taste summary",()=>companion.Say(musicBrain.Summary()))
      )));
      BuildBirdTunesControls(body);
      body.Children.Add(Section("Corbie's favourite tunes",new TextBlock { Text=musicBrain.FavouriteSummary(),Foreground=InkBrush,TextWrapping=TextWrapping.Wrap },
        new TextBlock { Text="With music learning on, he recognises these from Windows song information and gets more enthusiastic when dancing. Edit secret-top-ten.txt in your local Corbie folder, then restart him, to change this list.",Foreground=MutedText,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0) }));
      body.Children.Add(Section("How it works",new TextBlock { Text="The visualizer listens only to audio energy. Music memory reads track metadata from Windows and keeps a private journal of repeats, completed listens, quick skips, and your direct feedback. Patterns across the same artist and album now shape opinions of new songs, while Corbie keeps enough independent bias to disagree with you.",Foreground=MutedText,TextWrapping=TextWrapping.Wrap }));
      AddTab("Music",body);
    }
    void BuildBehaviorTab() {
      var body=TabBody();
      panelPerch=Toggle("Follow the active window",SetPerching); panelWander=Toggle("Wander around",SetWandering);
      panelTaskbar=Toggle("Show Corbie on the taskbar",SetTaskbarButton);
      panelPause=ActionButton("Pause Corbie",TogglePause);
      body.Children.Add(Section("Movement and presence",panelPerch,panelWander,panelTaskbar,Actions(panelPause,ActionButton("Bring Corbie home",Home))));
      BuildNervesSection(body);
      BuildScreenNervesControls(body);
      BuildCursorFollowControls(body);
      BuildFlightControls(body);
      BuildPerchIntentControls(body);
      BuildDesktopForageControls(body);
      BuildHabitatControls(body);
      BuildWeatherControls(body);
      AddTab("Behavior",body);
    }
    void SetTaskbarButton(bool enabled) {
      ShowInTaskbar=enabled;
      if(roost!=null) { roost.Data.TaskbarButton=enabled; roost.Save(); }
    }
    void BuildBreaksTab() {
      var body=TabBody();
      panelBreakEnabled=Toggle("Enable active-use break reminders",breakCoach.SetEnabled);
      panelDetox=Toggle("Detox mode -- make the break card difficult to ignore",breakCoach.SetDetoxMode);
      panelWorkMinutes=new ComboBox { Margin=new Thickness(0,5,0,8),MinWidth=180 };
      foreach(int value in new [] { 30,45,55,60,90 }) panelWorkMinutes.Items.Add(value+" minutes of active use");
      panelWorkMinutes.SelectionChanged+=(s,e)=> { if(!refreshingPanel && panelWorkMinutes.SelectedIndex>=0) breakCoach.SetWorkMinutes(new [] { 30,45,55,60,90 }[panelWorkMinutes.SelectedIndex]); };
      panelBreakMinutes=new ComboBox { Margin=new Thickness(0,5,0,8),MinWidth=180 };
      foreach(int value in new [] { 3,5,10,15 }) panelBreakMinutes.Items.Add(value+" minute break");
      panelBreakMinutes.SelectionChanged+=(s,e)=> { if(!refreshingPanel && panelBreakMinutes.SelectedIndex>=0) breakCoach.SetBreakMinutes(new [] { 3,5,10,15 }[panelBreakMinutes.SelectedIndex]); };
      panelBreakStatus=Status(); panelBreakProgress=new ProgressBar { Minimum=0,Maximum=100,Height=12,Background=new SolidColorBrush(Color.FromRgb(29,28,45)),Foreground=Accent,BorderThickness=new Thickness(0),Margin=new Thickness(0,4,0,12) };
      body.Children.Add(Section("Screen detox",panelBreakEnabled,panelDetox,new TextBlock { Text="Remind me after",Foreground=MutedText,Margin=new Thickness(0,7,0,0) },panelWorkMinutes,new TextBlock { Text="Required break",Foreground=MutedText },panelBreakMinutes,panelBreakStatus,panelBreakProgress,Actions(
        ActionButton("Start break now",BeginBreakNow),ActionButton("20-20-20 Eye Rest",StartEyeRestGlide),ActionButton("Snooze 10 minutes",()=>breakCoach.Snooze(10)),ActionButton("Reset work session",breakCoach.ResetSession)
      )));
      body.Children.Add(Section("What counts",new TextBlock { Text="Only active keyboard and mouse time builds the session. A full break away from the computer resets it automatically. Short pauses do not erase your progress. Detox mode enlarges the break card, but always leaves a snooze and an exit.",Foreground=MutedText,TextWrapping=TextWrapping.Wrap }));
      AddTab("Breaks",body);
    }
    void BuildFilesTab() {
      var body=TabBody();
      panelExtractFolder=Toggle("Extract archives into their own folder",SetExtractToFolder);
      panelReveal=Toggle("Reveal extracted folder",SetRevealFolder);
      panelConfirmPreview=Toggle("Confirm preview before opening",SetConfirmPreview);
      panelWarnHoard=Toggle("Warn before sending files to the Recycle Bin",SetWarnBeforeHoarding);
      body.Children.Add(Section("Peck open archives",panelExtractFolder,panelReveal,panelConfirmPreview,Actions(ActionButton("Choose an archive",PromptPeckOpenArchive))));
      body.Children.Add(Section("Corbie's hoard",panelWarnHoard,Actions(ActionButton("Check hoard stash",()=>companion.StashRemark(octopusMode)))));
      BuildForageSection(body);
      AddTab("Files",body);
    }
    void RefreshCorbiePanel() {
      RefreshMining();
      RefreshNerves();
      RefreshWeather();
      RefreshDesktopForage();
      if(corbiePanel==null) return;
      refreshingPanel=true;
      try {
        panelHandsFree.IsChecked=handsFreeEnabled; panelVoice.IsChecked=companion.VoiceEnabled; panelQuiet.IsChecked=companion.Quiet;
        panelTone.SelectedIndex=(int)companion.VoiceTone; panelVoiceModel.SelectedIndex=panelVoiceIds.FindIndex(x=>string.Equals(x,companion.VoiceId,StringComparison.OrdinalIgnoreCase)); panelMicStatus.Text=Convert.ToString(micStatus.Header);
        var inner=mood.Snapshot; panelMoodStatus.Text="Mood: "+inner.Mood+(inner.HeatStress>=50?" - feeling the heat":"")+" · hunger "+Math.Round(inner.Hunger)+"%"; panelEnergy.Value=inner.Energy; panelCuriosity.Value=inner.Curiosity; panelAffection.Value=inner.Affection; panelIrritation.Value=inner.Irritation; panelHunger.Value=inner.Hunger;
        panelAmbient.IsChecked=ambientVisionEnabled; panelScreenStatus.Text=Convert.ToString(ambientVisionStatus.Header);
        panelTemperature.IsChecked=temperatureEnabled; panelTemperatureStatus.Text=Convert.ToString(temperatureStatus.Header);
        panelCpu.IsChecked=cpuEnabled; panelCpuStatus.Text=Convert.ToString(cpuStatus.Header);
        panelDance.IsChecked=danceEnabled; panelTaste.IsChecked=musicTasteEnabled; panelMusicStatus.Text=Convert.ToString(musicTasteStatus.Header);
        panelPerch.IsChecked=autoPerch; panelWander.IsChecked=wandering; panelTaskbar.IsChecked=ShowInTaskbar; panelPause.Content=paused?"Resume Corbie":"Pause Corbie";
        panelBreakEnabled.IsChecked=breakCoach.Enabled; panelDetox.IsChecked=breakCoach.DetoxMode; panelBreakStatus.Text=breakCoach.Status; panelBreakProgress.Value=breakCoach.Progress*100;
        panelWorkMinutes.SelectedIndex=Array.IndexOf(new [] { 30,45,55,60,90 },breakCoach.WorkMinutes); panelBreakMinutes.SelectedIndex=Array.IndexOf(new [] { 3,5,10,15 },breakCoach.BreakMinutes);
        panelExtractFolder.IsChecked=extractToFolder; panelReveal.IsChecked=revealExtractedFolder; panelConfirmPreview.IsChecked=confirmPreview; panelWarnHoard.IsChecked=RecycleBinHoard.WarnBeforeHoarding;
        RefreshSchoolPanel();
        UpdateSecurityPanel();
        RefreshRoostPanel();
        RefreshSageControls();
        RefreshHolidays();
        RefreshDiscoveries();
        RefreshCursorFollow();
        RefreshFlightStatus();
        RefreshTikTok();
      } finally { refreshingPanel=false; }
    }
    void CheckControlPanel() {
      OpenCorbiePanel();
      if(corbiePanel==null || panelTabs==null || panelTabs.Items.Count!=14 || birdStatus==null || artHoard==null || browserTaskBox==null || browserStatus==null || browserStartButton==null || browserResumeButton==null || browserStopButton==null || miningStatus==null || panelEnergy==null || panelIrritation==null || panelMoodStatus==null || panelBreakProgress==null || panelSchoolList==null || panelSecurityStatus==null || panelVirusTotalKey==null || panelVirusTotalStatus==null || journalList==null || forageList==null || focusTask==null || chronicleList==null || territoryMapList==null) throw new Exception("Corbie's Nest did not build all control sections.");
      corbiePanel.Close();
    }
    void CheckNestFlow() {
      forageSuggestions.Add(new ForageItem { Name="example.exe",Reason="test suggestion",Bytes=1024 });
      ShowDownloadsSuggestions();
      if(corbiePanel==null || !corbiePanel.IsVisible || Convert.ToString(((TabItem)panelTabs.SelectedItem).Header)!="Files"
        || forageList.Items.Count!=1) throw new Exception("Downloads notice did not open the Files page with its suggestions.");
      var panel=corbiePanel;
      SyncNestPetVisibility(false);
      if(panel.IsVisible || panelRefreshTimer.IsEnabled) throw new Exception("Nest stayed visible or ticking while the pet was hidden.");
      SyncNestPetVisibility(true);
      if(!panel.IsVisible || !panelRefreshTimer.IsEnabled) throw new Exception("Nest did not restore with the pet.");
      RequestGoodbye();
      if(panel.IsVisible || corbiePanel!=null || panelRefreshTimer!=null) throw new Exception("Goodnight left the Nest open.");
      OpenCorbiePanel();
      if(corbiePanel!=null) throw new Exception("The Nest reopened after Corbie closed.");
    }
    void InstallVoicePack() {
      string script=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Setup-Piper.ps1");
      if(!System.IO.File.Exists(script)) { companion.Say("The voice installer has wandered off. Setup-Piper.ps1 should be beside me."); return; }
      try {
        companion.Say("Opening the voice installer. When it says the pack is ready, restart me and choose Northern in the Nest.");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
          FileName="powershell.exe",
          Arguments="-NoProfile -ExecutionPolicy Bypass -File \""+script+"\"",
          WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory,
          UseShellExecute=true
        });
      } catch(Exception ex) { companion.Say("I couldn't open the voice installer: "+ex.Message); }
    }
  }
}
