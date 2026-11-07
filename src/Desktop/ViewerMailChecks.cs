using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace CorbiePet {
  public static class ViewerMailChecks {
    public const string RentReply="I provide pest control and emotional inconvenience. That's my contribution.";
    public static void Run() {
      var parsed=ViewerMailBrain.Parse("```json\n{\"reply\":\"I hoard. Proudly.\",\"gesture\":\"side-eye\",\"command\":\"ignored\"}\n```");
      if(parsed.Gesture!=TikTokGesture.SideEye || parsed.Reply!="I hoard. Proudly.")throw new Exception("JSON/gesture normalization failed.");
      if(ViewerMailBrain.Parse("{\"reply\":\"Hello.\",\"gesture\":\"exit\"}").Gesture!=TikTokGesture.Idle)throw new Exception("Unknown model gesture didn't safely fall back.");
      var longReply=ViewerMailBrain.Parse("{\"reply\":\""+string.Join(" ",Enumerable.Repeat("treasure",90))+"\",\"gesture\":\"bow\"}");
      if(longReply.Reply.Length>280 || longReply.Reply.Split(' ').Length>45)throw new Exception("Reply exceeded its speaking bounds.");
      foreach(string raw in new [] {"", "not json", "{}", "{\"reply\":4}", "{\"reply\":\"   \"}",new string('x',17000)}) {
        bool rejected=false;try {ViewerMailBrain.Parse(raw);}catch(InvalidDataException) {rejected=true;}if(!rejected)throw new Exception("Invalid draft was accepted.");
      }
      string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".viewer-mail-check-"+Guid.NewGuid().ToString("N")+".json");
      try {
        var book=new ViewerMailBook(file);var item=new ViewerMailEntry {Comment="Do you pay rent?",Reply=RentReply,Gesture=TikTokGesture.SideEye};
        var saved=book.Keep(item);book.Keep(item);item.Reply="Edited source";
        if(book.Entries.Count!=1 || book.Entries[0].Reply!=RentReply)throw new Exception("Keep didn't snapshot or deduplicate.");
        var exposed=book.Entries[0];exposed.Reply="Edited copy";if(book.Entries[0].Reply!=RentReply)throw new Exception("Saved entry leaked mutable state.");
        book=new ViewerMailBook(file);if(book.Entries.Count!=1 || book.Entries[0].Reply!=RentReply)throw new Exception("Kept reply didn't survive reload.");
        book.Remove(saved.Id);if(new ViewerMailBook(file).Entries.Count!=0)throw new Exception("Saved removal didn't survive reload.");
        for(int i=0;i<30;i++)book.Keep(new ViewerMailEntry {Comment="Question "+i,Reply="Mine.",Gesture=TikTokGesture.Bow});
        bool full=false;try {book.Keep(new ViewerMailEntry {Comment="Overflow",Reply="Mine.",Gesture=TikTokGesture.Bow});}catch(InvalidOperationException) {full=true;}
        if(!full || book.Entries.Count!=30)throw new Exception("Kept library was not bounded.");
        File.WriteAllText(file,"broken saved data");var broken=new ViewerMailBook(file);bool protectedFile=false;
        try {broken.Keep(new ViewerMailEntry {Comment="Test",Reply="Mine.",Gesture=TikTokGesture.Bow});}catch(InvalidOperationException) {protectedFile=true;}
        if(!broken.LoadFailed || !protectedFile || broken.Entries.Count!=0 || File.ReadAllText(file)!="broken saved data")throw new Exception("Invalid saved mail was overwritten or failed keep wasn't rolled back.");
      }finally {if(File.Exists(file))File.Delete(file);}
    }
  }
  public partial class Pet {
    void CheckViewerMail(string preview) {
      ViewerMailChecks.Run();OpenNestPage("TikTok");
      roost.Data.SkyRoostEnabled=false;isNightSleep=false;fullscreenOrPresentation=false;fullscreenCheckCooldown=60;companion.SetVoice(false,false);companion.Dismiss();
      ambientVisionTimer.Stop();tiktokCue=false;tiktokHideNest=true;
      if(viewerComment==null || viewerKept==null || viewerGesture.Items.Count!=6)throw new Exception("Viewer Mail controls didn't build.");
      viewerComment.Text="Do you pay rent?";viewerReply.Text="My previous editable reply.";
      var slow=new TaskCompletionSource<ViewerReply>(TaskCreationOptions.RunContinuationsAsynchronously);
      viewerMailTestWorker=(comment,previous,token)=> {if(comment!="Do you pay rent?" || previous!="My previous editable reply.")throw new Exception("Draft inputs changed.");return slow.Task;};
      var drafting=DraftViewerMail();
      if(!ViewerMailBusy || !viewerReply.IsReadOnly || CanInitiate() || !TikTokBlocked() || CursorFollowBlock()==null)throw new Exception("Draft didn't own its busy state.");
      double x=Left,y=Top;ticks=.2;StepViewerMailThinking(.1);double head=headTilt.Angle;StepViewerMailThinking(.1);
      if(Left!=x || Top!=y || head==headTilt.Angle)throw new Exception("Drafting froze the bird or moved its perch.");
      CancelViewerMail();ScreenReadChecks.Pump(drafting);
      if(ViewerMailBusy || viewerReply.IsReadOnly || viewerReply.Text!="My previous editable reply.")throw new Exception("Cancel didn't preserve the editable reply and release state.");
      viewerMailTestWorker=(comment,previous,token)=>Task.FromResult(new ViewerReply {Reply=ViewerMailChecks.RentReply,Gesture=TikTokGesture.SideEye});
      ScreenReadChecks.Pump(DraftViewerMail());
      if(viewerReply.Text!=ViewerMailChecks.RentReply || viewerGesture.SelectedIndex!=(int)TikTokGesture.SideEye || TikTokActive || companion.Visible)throw new Exception("Draft didn't publish correctly or spoke/performed automatically.");
      slow.SetResult(new ViewerReply {Reply="Stale draft",Gesture=TikTokGesture.Bow});ScreenReadChecks.Pump(Task.Delay(40));
      if(viewerReply.Text!=ViewerMailChecks.RentReply)throw new Exception("Late cancelled result replaced a newer draft.");
      var timeout=new TaskCompletionSource<ViewerReply>(TaskCreationOptions.RunContinuationsAsynchronously);viewerMailTestWorker=(c,p,t)=>timeout.Task;viewerMailTestLimit=TimeSpan.FromMilliseconds(90);
      ScreenReadChecks.Pump(DraftViewerMail());if(ViewerMailBusy || !viewerMailNote.Contains("timed out") || viewerReply.Text!=ViewerMailChecks.RentReply)throw new Exception("Timeout didn't release the draft or preserve the previous reply.");
      viewerMailTestLimit=TimeSpan.FromSeconds(3);viewerMailTestWorker=(c,p,t)=> {throw new InvalidOperationException("Model fixture unavailable");};
      ScreenReadChecks.Pump(DraftViewerMail());if(ViewerMailBusy || !viewerMailNote.Contains("Couldn't draft") || viewerReply.Text!=ViewerMailChecks.RentReply)throw new Exception("Draft failure lost prior text or stayed busy.");
      var changed=new TaskCompletionSource<ViewerReply>(TaskCreationOptions.RunContinuationsAsynchronously);viewerMailTestWorker=(c,p,t)=>changed.Task;
      drafting=DraftViewerMail();viewerComment.Text="Can you fly?";ScreenReadChecks.Pump(drafting);
      if(ViewerMailBusy || viewerReply.Text!="")throw new Exception("Changing the comment didn't invalidate its old answer.");
      changed.SetResult(new ViewerReply {Reply="Wrong comment",Gesture=TikTokGesture.Wave});timeout.SetResult(new ViewerReply {Reply="Overdue",Gesture=TikTokGesture.Wave});ScreenReadChecks.Pump(Task.Delay(40));
      if(viewerReply.Text!="")throw new Exception("Late reply was attached to a changed comment.");
      var takeover=new TaskCompletionSource<ViewerReply>(TaskCreationOptions.RunContinuationsAsynchronously);viewerMailTestWorker=(c,p,t)=>takeover.Task;
      handsFreeEnabled=true;drafting=DraftViewerMail();if(!tiktokResumeMic || handsFreeEnabled)throw new Exception("Drafting didn't acquire the continuous-mic pause.");
      StartTikTokTake(false);ScreenReadChecks.Pump(drafting);
      if(ViewerMailBusy || !TikTokActive || !tiktokResumeMic)throw new Exception("A new take didn't inherit the temporary mic pause or cancel the older draft.");
      StopTikTokTake(false);takeover.SetResult(new ViewerReply {Reply="Replaced by a take",Gesture=TikTokGesture.Idle});OpenNestPage("TikTok");
      viewerComment.Text="Do you pay rent?";viewerReply.Text=ViewerMailChecks.RentReply;viewerGesture.SelectedIndex=(int)TikTokGesture.SideEye;
      EnsureDirector();RememberDirectorTake(TikTokDirector.Bow(),true);var lastScene=directorData.LastTake[0].Line;
      PerformViewerMail(true,false);
      if(!TikTokActive || !corbiePanel.IsVisible || directorData.LastTake[0].Line!=lastScene || viewerLastTake!=null)throw new Exception("Rehearsal hid the Nest or replaced a recorded take.");
      for(int i=0;i<60;i++)StepTikTokTake(.1);
      PerformViewerMail(false,false);
      if(!TikTokActive || corbiePanel.IsVisible || viewerLastTake.Reply!=ViewerMailChecks.RentReply)throw new Exception("Performance didn't snapshot the reply or use camera hiding.");
      if(directorData.LastTake[0].Line!=lastScene)throw new Exception("Viewer Mail performance persisted an unkept reply as the last director scene.");
      viewerReply.Text="Edited after performing.";StopTikTokTake();OpenNestPage("TikTok");
      PerformViewerMail(false,true);
      if(directorRun==null || directorRun.Current.Line!=ViewerMailChecks.RentReply)throw new Exception("Retake used edited text instead of the performed snapshot.");StopTikTokTake();OpenNestPage("TikTok");
      viewerReply.Text=ViewerMailChecks.RentReply;KeepViewerMail();KeepViewerMail();if(viewerKept.Items.Count!=1)throw new Exception("Keep didn't deduplicate the reply.");
      viewerReply.Text="Edited again.";viewerKept.SelectedIndex=-1;viewerKept.SelectedIndex=0;if(viewerReply.Text!=ViewerMailChecks.RentReply)throw new Exception("Saved reply selection didn't reload the original.");
      RemoveViewerMail();if(viewerKept.Items.Count!=0)throw new Exception("Remove kept reply failed.");KeepViewerMail();
      var paused=new TaskCompletionSource<ViewerReply>(TaskCreationOptions.RunContinuationsAsynchronously);viewerMailTestWorker=(c,p,t)=>paused.Task;
      drafting=DraftViewerMail();TogglePause();ScreenReadChecks.Pump(drafting);if(ViewerMailBusy)throw new Exception("Pause left a draft running.");TogglePause();paused.SetResult(new ViewerReply {Reply="Paused late",Gesture=TikTokGesture.Idle});
      var closed=new TaskCompletionSource<ViewerReply>(TaskCreationOptions.RunContinuationsAsynchronously);viewerMailTestWorker=(c,p,t)=>closed.Task;
      drafting=DraftViewerMail();corbiePanel.Close();ScreenReadChecks.Pump(drafting);if(ViewerMailBusy)throw new Exception("Closed Nest left its draft running.");closed.SetResult(new ViewerReply {Reply="Closed late",Gesture=TikTokGesture.Idle});OpenNestPage("TikTok");
      viewerComment.Text="Do you pay rent?";viewerMailTestWorker=(c,p,t)=>Task.FromResult(new ViewerReply {Reply=ViewerMailChecks.RentReply,Gesture=TikTokGesture.SideEye});ScreenReadChecks.Pump(DraftViewerMail());
      if(viewerKept.Items.Count!=1)throw new Exception("Kept replies were lost when reopening the Nest.");
      if(!string.IsNullOrEmpty(preview)) {corbiePanel.UpdateLayout();RenderDirectorPanel(preview);}
      viewerMailTestWorker=null;companion.Dismiss();
    }
  }
}
