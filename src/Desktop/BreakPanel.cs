using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace CorbiePet {
  public partial class Pet {
    Window breakPrompt;
    TextBlock breakPromptTitle,breakPromptBody,breakPromptClock;
    Button breakPromptPrimary,breakPromptSnooze,breakPromptDone;
    DispatcherTimer breakPromptTimer;
    bool closingBreakPrompt;

    void HandleBreakUpdate(BreakUpdate update) {
      if(update==null || update.Signal==BreakSignal.None) return;
      if(update.Signal==BreakSignal.Due) {
        if(companion.Quiet) companion.SaySilent(update.Message); else companion.Say(update.Message);
        OpenBreakPrompt();
      } else if(update.Signal==BreakSignal.Completed) {
        mood.BreakRest(); CloseBreakPrompt();
        if(mission!=null) mission.NoteBreakComplete();
        var earned=roost!=null?roost.ScavengeTrinket(new Random()):null;
        if(earned!=null) { ApplyTrinketMolt(); update.Message+=" I found a "+earned.Rarity.ToLowerInvariant()+" shiny for the hoard: "+earned.Name+"."; }
        if(mission!=null && mission.Active) update.Message+=" Mission break credited.";
        if(companion.Quiet) companion.SaySilent(update.Message); else companion.Say(update.Message);
      }
    }
    void BeginBreakNow() { breakCoach.StartBreak(); OpenBreakPrompt(); RefreshBreakPrompt(); }
    void OpenBreakPrompt() {
      if(breakPrompt!=null) { RefreshBreakPrompt(); breakPrompt.Activate(); return; }
      bool detox=breakCoach.DetoxMode;
      breakPrompt=new Window {
        Title="Corbie's break decree",Width=detox?680:500,Height=detox?430:330,MinWidth=440,MinHeight=300,
        WindowStyle=detox?WindowStyle.None:WindowStyle.ToolWindow,ResizeMode=ResizeMode.NoResize,
        Background=new SolidColorBrush(Color.FromRgb(24,23,40)),WindowStartupLocation=WindowStartupLocation.CenterScreen,
        Topmost=true,ShowInTaskbar=true,Owner=this
      };
      var root=new StackPanel { Margin=new Thickness(detox?42:28) };
      root.Children.Add(new TextBlock { Text=detox?"🐦‍⬛":"CORBIE",FontSize=detox?52:14,Foreground=new SolidColorBrush(Color.FromRgb(197,177,231)),HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,0,10) });
      breakPromptTitle=new TextBlock { FontSize=detox?28:23,FontWeight=FontWeights.Bold,Foreground=Brushes.White,TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap };
      breakPromptBody=new TextBlock { FontSize=14,Foreground=new SolidColorBrush(Color.FromRgb(211,203,225)),TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,14,0,10) };
      breakPromptClock=new TextBlock { FontSize=detox?38:30,FontWeight=FontWeights.Bold,Foreground=new SolidColorBrush(Color.FromRgb(164,135,207)),TextAlignment=TextAlignment.Center,Margin=new Thickness(0,5,0,18) };
      root.Children.Add(breakPromptTitle); root.Children.Add(breakPromptBody); root.Children.Add(breakPromptClock);
      var actions=new WrapPanel { HorizontalAlignment=HorizontalAlignment.Center };
      breakPromptPrimary=BreakButton("Start break",()=> {
        if(breakCoach.OnBreak) HandleBreakUpdate(breakCoach.FinishEarly()); else BeginBreakNow();
      });
      breakPromptSnooze=BreakButton("Snooze 10 minutes",()=> {
        breakCoach.Snooze(10);
        bool pesterStarted=false;
        if(roost!=null && roost.Data.PesterBossEnabled && breakCoach.DetoxMode) {
          eyeRestCoach.ConsecutiveSnoozes++;
          if(eyeRestCoach.ConsecutiveSnoozes>=3) {
            eyeRestCoach.TriggerPesterBoss();
            pesterStarted=true;
            companion.Say("Three snoozes in detox mode? Your cursor is confiscated. Step away from the machine!");
          }
        }
        CloseBreakPrompt();
        if(!pesterStarted) companion.SaySilent("Ten-minute reprieve granted. Use it wisely, chair creature.");
      });
      breakPromptDone=BreakButton("I already stepped away",()=> { breakCoach.ResetSession(); mood.BreakRest(); eyeRestCoach.DismissPesterBoss(); CloseBreakPrompt(); companion.SaySilent("Break credited. I choose to trust you, which feels reckless."); });
      var breakPromptEyeRest=BreakButton("20-20-20 Eye Rest",()=> { CloseBreakPrompt(); StartEyeRestGlide(); });
      actions.Children.Add(breakPromptPrimary); actions.Children.Add(breakPromptSnooze); actions.Children.Add(breakPromptDone); actions.Children.Add(breakPromptEyeRest); root.Children.Add(actions);
      breakPrompt.Content=new Border { Background=new SolidColorBrush(Color.FromRgb(35,33,54)),BorderBrush=new SolidColorBrush(Color.FromRgb(126,104,166)),BorderThickness=new Thickness(detox?4:2),CornerRadius=new CornerRadius(16),Padding=new Thickness(18),Child=root };
      breakPrompt.Closing+=(s,e)=> {
        if(!closingBreakPrompt && !shuttingDown && (breakCoach.Due || breakCoach.OnBreak)) breakCoach.Snooze(10);
      };
      breakPrompt.Closed+=(s,e)=> { breakPromptTimer.Stop(); breakPromptTimer=null; breakPrompt=null; closingBreakPrompt=false; };
      breakPromptTimer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(1) }; breakPromptTimer.Tick+=(s,e)=>RefreshBreakPrompt(); breakPromptTimer.Start();
      RefreshBreakPrompt(); breakPrompt.Show(); breakPrompt.Activate();
    }
    Button BreakButton(string text,Action action) {
      var button=new Button { Content=text,MinWidth=130,Padding=new Thickness(12,8,12,8),Margin=new Thickness(5),Background=new SolidColorBrush(Color.FromRgb(126,104,166)),Foreground=Brushes.White,BorderThickness=new Thickness(0) };
      button.Click+=(s,e)=>action(); return button;
    }
    void RefreshBreakPrompt() {
      if(breakPrompt==null) return;
      if(breakCoach.OnBreak) {
        breakPromptTitle.Text="BREAK IN PROGRESS";
        breakPromptBody.Text="Step away from the screen. Water, window, stretch, hallway—anything with fewer pixels.";
        var left=breakCoach.BreakRemaining; breakPromptClock.Text=((int)left.TotalMinutes).ToString("00")+":"+left.Seconds.ToString("00");
        breakPromptPrimary.Content="End break early"; breakPromptSnooze.Visibility=Visibility.Collapsed; breakPromptDone.Visibility=Visibility.Collapsed;
      } else {
        breakPromptTitle.Text=breakCoach.DetoxMode?"THE SCREEN DETOX STARTS NOW":"CORBIE PRESCRIBES A BREAK";
        breakPromptBody.Text="You've accumulated "+breakCoach.WorkMinutes+" minutes of active computer time. Take "+breakCoach.BreakMinutes+" minutes away and come back with functioning joints.";
        breakPromptClock.Text=breakCoach.BreakMinutes+" MIN"; breakPromptPrimary.Content="Start break"; breakPromptSnooze.Visibility=Visibility.Visible; breakPromptDone.Visibility=Visibility.Visible;
      }
    }
    void CloseBreakPrompt() {
      if(breakPrompt==null) return; closingBreakPrompt=true; breakPrompt.Close();
    }
  }
}
