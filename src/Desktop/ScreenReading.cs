using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace CorbiePet {
  // Stop waiting even when a native worker ignores cancellation. Late workers
  // may finish, but their result is never returned to the cancelled caller.
  public static class VisionBudget {
    public static async Task<T> Run<T>(Func<CancellationToken,Task<T>> work,TimeSpan limit,CancellationToken token) {
      token.ThrowIfCancellationRequested();
      using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)) {
        deadline.CancelAfter(limit);
        var stopped=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using(deadline.Token.Register(()=>stopped.TrySetResult(true))) {
          Task<T> pending=work(deadline.Token);
          var winner=await Task.WhenAny(pending,stopped.Task).ConfigureAwait(false);
          if(winner!=pending || deadline.IsCancellationRequested) {
            var observation=pending.ContinueWith(t=> {var observed=t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted|TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
            token.ThrowIfCancellationRequested();throw new TimeoutException("The local screen reader took too long.");
          }
          token.ThrowIfCancellationRequested();return await pending.ConfigureAwait(false);
        }
      }
    }
  }
  public partial class Pet {
    // Test-only worker: never used by a normal pet, and never captures a desktop.
    Func<CancellationToken,Task<string>> screenReadTestWorker;
    TimeSpan screenReadTestLimit=TimeSpan.FromSeconds(60);
    async Task<string> ReadScreenForLook(CancellationToken token,bool ambient,CancellationTokenSource owner,Action<DateTime> captured) {
      if(testing && screenReadTestWorker!=null)return await screenReadTestWorker(token);
      if(!ambient) {
        SetAmbientStatus("Screen reader: checking the local model...");
        await ScreenVision.VerifyLocalModel(token);token.ThrowIfCancellationRequested();
      }
      var activeHwnd=GetActiveWindowHandle();var petHwnd=new System.Windows.Interop.WindowInteropHelper(this).Handle;
      companion.Dismiss();SetAmbientStatus("Screen reader: capturing the active window...");Opacity=0;
      byte[] png;
      try {
        await Task.Delay(180,token);
        // Screen copying, scaling and PNG encoding can be expensive on a large
        // display. None of that work belongs on the WPF animation thread.
        png=await VisionBudget.Run(t=>Task.Run(()=> {t.ThrowIfCancellationRequested();var image=ScreenVision.CaptureScreen(petHwnd,activeHwnd);t.ThrowIfCancellationRequested();return image;}),TimeSpan.FromSeconds(4),token);
      }finally {if(!shuttingDown && visionRequest==owner)Opacity=1;}
      token.ThrowIfCancellationRequested();captured(DateTime.Now);
      SetAmbientStatus("Screen reader: thinking locally (Cancel looking stops this)...");
      return await ScreenVision.Describe(png,octopusMode,token,ambient?lastVisionContext:null);
    }
    void PoseScreenThinking(double dt) {
      ticks+=dt;hop=0;
      PoseRestBreathing(mood.Snapshot);
      leftFoot.X=leftFoot.Y=rightFoot.X=rightFoot.Y=0;
      headTilt.Angle=3+Math.Sin(ticks*1.3)*2.5;
      wingStretch.Angle=-4+Math.Sin(ticks*.9)*1.5;
      bool blink=ticks%4.3>4.1;
      eye.Height=octoEyeLeft.Height=octoEyeRight.Height=blink?2:11;glint.Visibility=blink?Visibility.Hidden:Visibility.Visible;
      BlendArticulation("idle",ticks/2.4,dt);
      if(octopusMode) {squish.ScaleX=1+Math.Sin(ticks*1.4)*.015;squish.ScaleY=1-Math.Sin(ticks*1.4)*.015;}
      companion.Tick(dt,false,octopusMode,true);
    }
  }
}
