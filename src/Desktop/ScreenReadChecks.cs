using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace CorbiePet {
  public static class ScreenReadChecks {
    public static async Task Run() {
      int started=0;
      using(var cancelled=new CancellationTokenSource()) {
        cancelled.Cancel();bool stopped=false;
        try {await VisionBudget.Run(t=> {started++;return Task.FromResult("bad");},TimeSpan.FromSeconds(1),cancelled.Token).ConfigureAwait(false);}catch(OperationCanceledException) {stopped=true;}
        if(!stopped || started!=0)throw new Exception("Cancelled reader started a worker.");
      }
      var ignoredCancellation=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
      var elapsed=System.Diagnostics.Stopwatch.StartNew();bool timedOut=false;
      try {await VisionBudget.Run(t=>ignoredCancellation.Task,TimeSpan.FromMilliseconds(80),CancellationToken.None).ConfigureAwait(false);}catch(TimeoutException) {timedOut=true;}
      if(!timedOut || elapsed.Elapsed>TimeSpan.FromSeconds(3))throw new Exception("Uncooperative worker held the caller beyond its budget.");
      ignoredCancellation.SetException(new InvalidOperationException("Late worker fault must be observed, never published."));
      var pending=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
      using(var cancel=new CancellationTokenSource()) {
        var work=VisionBudget.Run(t=>pending.Task,TimeSpan.FromMinutes(1),cancel.Token);cancel.Cancel();bool stopped=false;
        try {await work.ConfigureAwait(false);}catch(OperationCanceledException) {stopped=true;}
        if(!stopped)throw new Exception("Explicit cancel waited for an uncooperative worker.");
      }
      pending.SetResult("Late success");
      string result=await VisionBudget.Run(t=>Task.FromResult("Fresh result"),TimeSpan.FromSeconds(1),CancellationToken.None).ConfigureAwait(false);
      if(result!="Fresh result")throw new Exception("Ordinary reader result was lost.");
      bool failed=false;
      try {await VisionBudget.Run<string>(t=> {throw new InvalidOperationException("Model unavailable");},TimeSpan.FromSeconds(1),CancellationToken.None).ConfigureAwait(false);}catch(InvalidOperationException) {failed=true;}
      if(!failed)throw new Exception("Reader swallowed a real model error.");
    }
    internal static void Pump(Task work) {
      var frame=new DispatcherFrame();var clock=System.Diagnostics.Stopwatch.StartNew();
      var poll=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(10)};
      poll.Tick+=(s,e)=> {if(work.IsCompleted || clock.Elapsed>TimeSpan.FromSeconds(5))frame.Continue=false;};
      poll.Start();try {Dispatcher.PushFrame(frame);}finally {poll.Stop();}
      if(!work.IsCompleted)throw new Exception("UI reader failed to release within the check's five-second limit.");
      work.GetAwaiter().GetResult();
    }
  }
  public partial class Pet {
    void CheckScreenReadLifecycle() {
      roost.Data.SkyRoostEnabled=false;isNightSleep=false;fullscreenOrPresentation=false;fullscreenCheckCooldown=60;companion.SetVoice(false);
      ambientVisionTimer.Stop();Show();
      screenReadTestLimit=TimeSpan.FromSeconds(3);
      var slow=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);screenReadTestWorker=t=>slow.Task;
      string original=lastVisionContext;DateTime originalDate=lastVisionLook;
      var read=LookAtScreen();
      if(!looking || visionRequest==null || seeingLight.Visibility!=Visibility.Visible)throw new Exception("Screen read did not enter its owned looking state.");
      double x=Left,y=Top;var toe=leftFootVisual.TransformToAncestor(scene).Transform(new Point(90,147));
      double before=headTilt.Angle;ticks=.2;PoseScreenThinking(.1);double after=headTilt.Angle;
      ticks=4.05;PoseScreenThinking(.1);
      if(before==after || eye.Height!=2 || Left!=x || Top!=y || (leftFootVisual.TransformToAncestor(scene).Transform(new Point(90,147))-toe).Length>.001)throw new Exception("Looking pose froze, failed to blink, moved the window or floated its feet.");
      CancelLooking();ScreenReadChecks.Pump(read);
      if(looking || visionRequest!=null || Opacity!=1 || seeingLight.Visibility!=Visibility.Hidden || lastVisionContext!=original || lastVisionLook!=originalDate)throw new Exception("Cancel failed to clear looking, or published unfinished evidence.");
      companion.Dismiss();
      var overdue=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);screenReadTestWorker=t=>overdue.Task;screenReadTestLimit=TimeSpan.FromMilliseconds(100);
      ScreenReadChecks.Pump(LookAtScreen(true));
      if(looking || visionRequest!=null || !Convert.ToString(ambientVisionStatus.Header).Contains("timed out"))throw new Exception("Timeout left a stuck looking state or unclear status.");
      screenReadTestWorker=t=>Task.FromResult("Fresh screenshot comment.");screenReadTestLimit=TimeSpan.FromSeconds(1);
      ScreenReadChecks.Pump(LookAtScreen());
      if(lastVisionContext!="Fresh screenshot comment." || looking || !Convert.ToString(ambientVisionStatus.Header).Contains("finished"))throw new Exception("A successful retry didn't finish cleanly.");
      overdue.SetResult("Stale timed-out comment.");slow.SetResult("Stale cancelled comment.");ScreenReadChecks.Pump(Task.Delay(50));
      if(lastVisionContext!="Fresh screenshot comment.")throw new Exception("A late result replaced newer screen evidence.");
      companion.Dismiss();screenReadTestWorker=t=> {throw new InvalidOperationException("Synthetic local model failure");};ScreenReadChecks.Pump(LookAtScreen());
      if(looking || visionRequest!=null || lastVisionContext!="Fresh screenshot comment." || !Convert.ToString(ambientVisionStatus.Header).Contains("error"))throw new Exception("Failure did not release reader state or preserved stale evidence incorrectly.");
      companion.Dismiss();screenReadTestWorker=t=>new TaskCompletionSource<string>().Task;
      var interrupted=LookAtScreen();TogglePause();ScreenReadChecks.Pump(interrupted);
      if(looking || visionRequest!=null || seeingLight.Visibility!=Visibility.Hidden)throw new Exception("Pause left an active screen reader.");TogglePause();
      screenReadTestWorker=null;companion.Dismiss();
    }
  }
}
