using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading;

namespace CorbiePet {
  sealed class SchoolRow {
    public SchoolEvent Event { get; set; }
    public string Label { get; set; }
    public override string ToString() { return Label; }
  }

  public partial class Pet {
    LessonNotebook lessonNotebook;
    void OpenLessonNotebook() {
      try {
        if(lessonNotebook!=null) {
          lessonNotebook.Show();
          if(lessonNotebook.WindowState==WindowState.Minimized) lessonNotebook.WindowState=WindowState.Normal;
          lessonNotebook.Activate();
          return;
        }
        lessonNotebook=new LessonNotebook(()=>micRequest!=null,()=>companion.StopVoice());
        lessonNotebook.Closed+=(s,e)=>lessonNotebook=null;
        lessonNotebook.Show();
        lessonNotebook.Activate();
      } catch(Exception ex) {
        companion.SaySilent("Lesson notebook could not open: "+ex.Message);
      }
    }
    PasswordBox panelBlackboardUrl;
    CheckBox panelSchoolReminders;
    TextBlock panelSchoolStatus;
    ListBox panelSchoolList;
    int panelSchoolRevision=-1;
    readonly DispatcherTimer schoolTimer=new DispatcherTimer();
    CancellationTokenSource schoolRequest;
    DateTime lastSchoolRemark=DateTime.MinValue;

    void InitializeSchool() {
      schoolTimer.Interval=TimeSpan.FromMinutes(15);
      schoolTimer.Tick+=async (s,e)=>await RefreshSchool(false);
    }
    void StartSchool() {
      if(!schoolCalendar.Configured) return; schoolTimer.Start(); var ignored=RefreshSchool(false);
    }
    void CancelSchoolRefresh() { if(schoolRequest!=null) schoolRequest.Cancel(); }
    async Task RefreshSchool(bool announce) {
      if(!schoolCalendar.Configured) { if(panelSchoolStatus!=null) panelSchoolStatus.Text="Paste Blackboard's Share Calendar link to connect."; return; }
      if(schoolRequest!=null) return; var request=new CancellationTokenSource(); schoolRequest=request;
      try {
        if(panelSchoolStatus!=null) panelSchoolStatus.Text="Refreshing Blackboard calendar...";
        await schoolCalendar.Refresh(request.Token); request.Token.ThrowIfCancellationRequested();
        RefreshSchoolPanel(); UpdateSchoolPressure();
        string reminder=schoolCalendar.Reminders?schoolCalendar.Reminder(DateTime.Now):null;
        if(!string.IsNullOrEmpty(reminder) && (announce || (!focusSprint.Active && (DateTime.Now-lastSchoolRemark).TotalHours>=3))) {
          lastSchoolRemark=DateTime.Now; if(companion.Quiet) companion.SaySilent(reminder); else companion.Say(reminder);
        } else if(announce) companion.SaySilent(schoolCalendar.Summary(DateTime.Now));
      } catch(OperationCanceledException) { }
      catch(Exception ex) { RefreshSchoolPanel(); if(announce || lastSchoolRemark==DateTime.MinValue) companion.SaySilent("Blackboard calendar failed to refresh: "+ex.Message); }
      finally { if(schoolRequest==request) schoolRequest=null; request.Dispose(); }
    }
    void UpdateSchoolPressure() { mood.SetSchoolPressure(schoolCalendar.Overdue(DateTime.Now).Count,schoolCalendar.DueSoon(DateTime.Now).Count); }

    void BuildSchoolTab() {
      var body=TabBody();
      panelBlackboardUrl=new PasswordBox { MinWidth=300,Margin=new Thickness(0,5,0,8),Padding=new Thickness(7),ToolTip="Blackboard Calendar ??? Calendar Settings ??? three dots ??? Share Calendar" };
      panelSchoolReminders=Toggle("Let Corbie remind me about school deadlines",schoolCalendar.SetReminders);
      panelSchoolStatus=Status();
      body.Children.Add(Section("Connect Blackboard",new TextBlock { Text="Paste the private Share Calendar link here. It stays on this PC and is encrypted for your Windows account.",Foreground=MutedText,TextWrapping=TextWrapping.Wrap },panelBlackboardUrl,Actions(
        ActionButton("Save and connect",()=> { var ignored=ConnectBlackboard(); }),ActionButton("Refresh now",()=> { var ignored=RefreshSchool(true); }),ActionButton("Disconnect",DisconnectBlackboard)
      ),panelSchoolReminders,panelSchoolStatus));
      panelSchoolList=new ListBox { MinHeight=240,Background=EggBrush,Foreground=PanelText,BorderBrush=CardBorderBrush,BorderThickness=new Thickness(1),Padding=new Thickness(7) };
      body.Children.Add(Section("Deadline triage",panelSchoolList,Actions(
        ActionButton("Open in Blackboard",OpenSelectedSchoolItem),ActionButton("Mark done locally",MarkSelectedSchoolItemDone),
        ActionButton("Start mission from selected",()=> { StartMissionSchool(); })
      ),new TextBlock { Text="The calendar cannot see whether you submitted something. Mark completed work done here so Corbie stops counting it as outstanding. Missions turn a deadline into a quest scrap with one next step.",Foreground=MutedText,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,0) }));
      body.Children.Add(Section("Lesson notes",new TextBlock { Text="Record a class with Corbie, keep its audio on this PC, and turn it into an editable draft transcript with local Whisper. Your notes save alongside the recording.",Foreground=MutedText,TextWrapping=TextWrapping.Wrap },Actions(ActionButton("Open lesson notebook",OpenLessonNotebook))));
      BuildFocusSection(body);
      AddTab("School",body);
    }
    async Task ConnectBlackboard() {
      try {
        schoolCalendar.Configure(panelBlackboardUrl.Password); panelBlackboardUrl.Password=""; schoolTimer.Start();
        await RefreshSchool(true);
      } catch(Exception ex) { panelSchoolStatus.Text=ex.Message; companion.SaySilent("Blackboard connection failed: "+ex.Message); }
    }
    void DisconnectBlackboard() {
      CancelSchoolRefresh(); schoolTimer.Stop(); schoolCalendar.Disconnect(); mood.SetSchoolPressure(0,0); panelSchoolRevision=-1; RefreshSchoolPanel();
      companion.SaySilent("Blackboard calendar disconnected. Academic haunting suspended.");
    }
    void OpenSelectedSchoolItem() {
      var row=panelSchoolList.SelectedItem as SchoolRow;
      if(row==null) { companion.SaySilent("Select a school item first. I cannot peck a conceptual link."); return; }
      Uri uri; if(string.IsNullOrEmpty(row.Event.Url) || !Uri.TryCreate(row.Event.Url,UriKind.Absolute,out uri) || (uri.Scheme!="https" && uri.Scheme!="http")) {
        companion.SaySilent("That calendar item did not include a Blackboard link. Open it from Blackboard's Calendar or Activity page."); return;
      }
      try { Process.Start(uri.AbsoluteUri); } catch { companion.SaySilent("Windows refused to open that Blackboard link."); }
    }
    void MarkSelectedSchoolItemDone() {
      var row=panelSchoolList.SelectedItem as SchoolRow;
      if(row==null) { companion.SaySilent("Select the thing you conquered first."); return; }
      schoolCalendar.MarkDone(row.Event); panelSchoolRevision=-1; RefreshSchoolPanel(); UpdateSchoolPressure();
      companion.SaySilent("Marked \""+row.Event.Title+"\" done locally. One less academic ghost rattling the windows.");
    }
    void RefreshSchoolPanel() {
      if(panelSchoolStatus==null) return;
      panelSchoolReminders.IsChecked=schoolCalendar.Reminders;
      panelSchoolStatus.Text=schoolCalendar.Summary(DateTime.Now);
      if(panelSchoolRevision==schoolCalendar.Revision) return;
      panelSchoolRevision=schoolCalendar.Revision; panelSchoolList.Items.Clear(); DateTime now=DateTime.Now;
      foreach(var item in schoolCalendar.Visible(now)) {
        string when=item.Due<now?"OVERDUE ?? "+BlackboardCalendar.FriendlyDue(item.Due,now):BlackboardCalendar.FriendlyDue(item.Due,now);
        string course=string.IsNullOrWhiteSpace(item.Course)?"":" ?? "+item.Course;
        panelSchoolList.Items.Add(new SchoolRow { Event=item,Label="["+when+"] "+item.Title+course });
      }
      if(panelSchoolList.Items.Count==0 && schoolCalendar.Configured && schoolCalendar.LastRefresh!=DateTime.MinValue) panelSchoolList.Items.Add(new ListBoxItem { Content="Nothing outstanding in the visible calendar window.",IsEnabled=false });
    }
  }
}
