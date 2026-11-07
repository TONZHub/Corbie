using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CorbiePet {
  public static class CheckidayChecks {
    const string FakeKey="corbie_test_key_not_a_real_secret";
    sealed class FakeHandler : HttpMessageHandler {
      readonly Func<HttpResponseMessage> response;
      readonly Func<CancellationToken,Task> wait;
      public FakeHandler(Func<HttpResponseMessage> response,Func<CancellationToken,Task> wait=null) { this.response=response;this.wait=wait; }
      protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) {
        if(request.Method!=HttpMethod.Get || request.RequestUri.AbsoluteUri!="https://api.apilayer.com/checkiday/events?adult=false"
          || !request.Headers.Contains("apikey") || string.Join("",request.Headers.GetValues("apikey"))!=FakeKey)throw new Exception("Unexpected Checkiday destination, free-plan parameters or credentials.");
        token.ThrowIfCancellationRequested();if(wait!=null)await wait(token).ConfigureAwait(false);token.ThrowIfCancellationRequested();return response();
      }
    }
    static string Body(string date) { return "{\"date\":\""+date+"\",\"timezone\":\"America/Chicago\",\"adult\":false,\"events\":[{\"id\":\"a\",\"name\":\"World Bird Day\",\"url\":\"https://www.checkiday.com/a/world-bird-day\"},{\"id\":\"b\",\"name\":\"World Bird Day\"},{\"id\":\"c\",\"name\":\"Snack Day\",\"url\":\"https://evil.example/\"}]}"; }
    static HttpResponseMessage Response(string body,HttpStatusCode status=HttpStatusCode.OK) { return new HttpResponseMessage(status) { Content=new StringContent(body,Encoding.UTF8,"application/json") }; }
    static async Task Reject(Task action,string expected) {
      try { await action.ConfigureAwait(false); }catch(InvalidOperationException ex) { if(ex.Message.IndexOf(expected,StringComparison.OrdinalIgnoreCase)>=0)return;throw; }
      throw new Exception("Expected Checkiday rejection: "+expected);
    }
    public static async Task Run() {
      DateTime utc=new DateTime(2026,10,12,16,0,0,DateTimeKind.Utc),local=new DateTime(2026,10,12,12,0,0);
      string folder=Path.Combine(Path.GetTempPath(),"corbie-checkiday-"+Guid.NewGuid().ToString("N"));
      int calls=0;string body=Body("10/12/2026");HttpStatusCode status=HttpStatusCode.OK;
      var held=new TaskCompletionSource<bool>();
      Func<HttpMessageHandler> factory=()=>new FakeHandler(()=> { calls++;return Response(body,status); },token=>held.Task);
      try {
        var client=new CheckidayClient(false,folder,factory,()=>utc);
        if(client.Configured || client.DailyEnabled)throw new Exception("Checkiday started connected or daily chatter enabled.");
        client.SaveKey(FakeKey);
        if(File.ReadAllText(Path.Combine(folder,"checkiday.key")).Contains(FakeKey))throw new Exception("Key was saved in plaintext.");
        var reload=new CheckidayClient(false,folder,factory,()=>utc);if(!reload.Configured)throw new Exception("Encrypted key could not be restored.");
        await Reject(new CheckidayClient(true).Today(local,CancellationToken.None),"save your");
        var first=client.Today(local,CancellationToken.None);var second=client.Today(local,CancellationToken.None);
        if(first.IsCompleted || second.IsCompleted)throw new Exception("Concurrent-request fixture didn't hold the response.");
        held.SetResult(true);var results=await Task.WhenAll(first,second).ConfigureAwait(false);
        if(calls!=1 || results[0].Events.Count!=2 || results[0].Events[1].Url!=null || results[0].Date!="2026-10-12")throw new Exception("Request deduplication, duplicate-event or link validation failed.");
        reload=new CheckidayClient(false,folder,factory,()=>utc);await reload.Today(local,CancellationToken.None).ConfigureAwait(false);if(calls!=1)throw new Exception("Restart refetched today's cached list.");
        reload.SetDaily(true);if(!reload.NeedsDaily(local) || !reload.ReserveDaily(local) || reload.ReserveDaily(local))throw new Exception("Daily receipt repeated or could not reserve.");
        if(new CheckidayClient(false,folder,factory,()=>utc).NeedsDaily(local))throw new Exception("Daily receipt did not survive restart.");
        if(!CheckidayClient.Remark(results[0]).Contains("plumage"))throw new Exception("Bird preference missing.");
        var solemn=CheckidayClient.Parse("{\"date\":\"10/12/2026\",\"events\":[{\"name\":\"Cancer Awareness Day\"}]}",utc);
        if(!CheckidayClient.Remark(solemn).Contains("care"))throw new Exception("Solemn observance received a frivolous reaction.");
        foreach(string raw in new[]{"{}","{\"date\":\"not a date\",\"events\":[]}","{\"date\":\"10/12/2026\",\"adult\":true,\"events\":[]}"}) {
          bool rejected=false;try { CheckidayClient.Parse(raw,utc); }catch {rejected=true;}if(!rejected)throw new Exception("Malformed or adult-enabled list accepted.");
        }
        if(CheckidayClient.ValidUrl("https://www.checkiday.com.evil.example/a") || CheckidayClient.ValidUrl("https://user@checkiday.com/a") || CheckidayClient.ValidUrl("file:///C:/test") || CheckidayClient.ValidUrl("https://checkiday.com/a?key=test"))throw new Exception("Unsafe holiday link accepted.");
        using(var cancel=new CancellationTokenSource()) { cancel.Cancel();bool cancelled=false;try { await client.Today(local,cancel.Token).ConfigureAwait(false); }catch(OperationCanceledException) {cancelled=true;}if(!cancelled || calls!=1)throw new Exception("Cancelled cached request completed or sent HTTP."); }
        using(var cancel=new CancellationTokenSource()) {
          var waiting=new TaskCompletionSource<bool>();
          var pending=new CheckidayClient(true,null,()=>new FakeHandler(()=>Response(body),async token=> { using(token.Register(()=>waiting.TrySetCanceled()))await waiting.Task.ConfigureAwait(false); }),()=>utc);
          pending.SaveKey(FakeKey);var work=pending.Today(local,cancel.Token);cancel.Cancel();
          bool cancelled=false;try { await work.ConfigureAwait(false); }catch(OperationCanceledException) {cancelled=true;}
          if(!cancelled || pending.CachedToday(local)!=null)throw new Exception("Cancellation during HTTP produced a cached result.");
        }
        // Local midnight can lead Checkiday's default time zone. Do not label yesterday as today.
        local=local.AddDays(1);utc=utc.AddDays(1);
        if(client.CachedToday(local)!=null)throw new Exception("Yesterday remained current.");
        await Reject(client.Today(local,CancellationToken.None),"is reporting");
        await Reject(client.Today(local,CancellationToken.None),"rolled over");
        if(calls!=2)throw new Exception("Date mismatch was repeatedly fetched.");
        utc=utc.AddMinutes(66);body=Body("10/13/2026");await client.Today(local,CancellationToken.None).ConfigureAwait(false);
        if(calls!=3 || client.CachedToday(local)==null)throw new Exception("Provider-date rollover did not recover.");
        client.MarkDiscussed(local);client.ForgetKey();
        if(client.Configured || client.DailyEnabled || client.CachedToday(local)!=null || File.Exists(Path.Combine(folder,"checkiday.key")) || File.Exists(Path.Combine(folder,"checkiday-cache.json")))throw new Exception("Forgetting retained credentials, cache or enabled daily chatter.");
        client.SaveKey(FakeKey);local=local.AddDays(1);utc=utc.AddDays(1);status=HttpStatusCode.Unauthorized;
        await Reject(client.Today(local,CancellationToken.None),"rejected");
        await Reject(client.Today(local,CancellationToken.None),"ten minutes");
        utc=utc.AddMinutes(11);status=(HttpStatusCode)429;await Reject(client.Today(local,CancellationToken.None),"allowance");
        utc=utc.AddMinutes(11);status=HttpStatusCode.Redirect;await Reject(client.Today(local,CancellationToken.None),"http 302");
        utc=utc.AddMinutes(11);reload=new CheckidayClient(false,folder,factory,()=>utc);int previous=calls;
        await Reject(reload.Today(local,CancellationToken.None),"three");if(calls!=previous)throw new Exception("Restart bypassed retry cap.");
        // Forget the key while a response is in flight; its result must not recreate the cache.
        local=local.AddDays(1);utc=utc.AddDays(1);body=Body("10/15/2026");
        CheckidayClient raced=null;
        raced=new CheckidayClient(false,folder,()=>new FakeHandler(()=> { raced.ForgetKey();return Response(body); }),()=>utc);
        bool stale=false;try { await raced.Today(local,CancellationToken.None).ConfigureAwait(false); }catch(OperationCanceledException) {stale=true;}
        if(!stale || raced.Configured || raced.CachedToday(local)!=null || File.Exists(Path.Combine(folder,"checkiday-cache.json")))throw new Exception("Forgotten key allowed a late cache result.");
        if(WakeCommand.Parse("Corbie, what are we celebrating today?",true).Action!=WakeAction.HolidayReport || WakeCommand.Parse("what are we celebrating",true).Action!=WakeAction.NotWoken
            || WakeCommand.Parse("I need advice about a holiday",false).Action!=WakeAction.None || !CheckidayClient.IsRequest("What’s today’s holiday?") || !CheckidayClient.IsRequest("whats todays holiday") || CheckidayClient.IsRequest("I don't want to celebrate today"))throw new Exception("Holiday mic intent bypassed wake or conversational routing.");
      } finally {
        foreach(string name in new[]{"checkiday.key","checkiday-settings.json","checkiday-cache.json"}) { string file=Path.Combine(folder,name);if(File.Exists(file))File.Delete(file); }
        if(Directory.Exists(folder))Directory.Delete(folder);
      }
    }
    internal static CheckidayClient ExampleClient(DateTime local) {
      var client=new CheckidayClient(true,null,()=>new FakeHandler(()=>Response(Body(local.ToString("MM/dd/yyyy",System.Globalization.CultureInfo.InvariantCulture)))));
      client.SaveKey(FakeKey);client.Today(local,CancellationToken.None).GetAwaiter().GetResult();return client;
    }
  }

  public partial class Pet {
    void CheckHolidayControls(string preview) {
      if(checkiday.Configured || checkiday.DailyEnabled)throw new Exception("Test pet loaded live Checkiday credentials.");
      var body=new StackPanel { Width=620,Background=PanelBackground };BuildHolidayControls(body);
      if(holidayRead.IsEnabled || holidayDaily.IsChecked==true || holidayOpen.IsEnabled || holidayCancel.IsEnabled)throw new Exception("Unconfigured holiday controls enabled an action.");
      if(!HolidayContext().Contains("No verified"))throw new Exception("Missing holiday context fabricated data.");
      holidayRequest=new CancellationTokenSource();RefreshHolidays();if(holidaySave.IsEnabled || !holidayCancel.IsEnabled)throw new Exception("Pending holiday request did not guard controls.");
      CancelHolidayRequest();if(!holidayRequest.IsCancellationRequested)throw new Exception("Holiday cancel did not cancel.");holidayRequest.Dispose();holidayRequest=null;
      holidayError=null;RefreshHolidays();
      if(!string.IsNullOrEmpty(preview)) {
        body.Measure(new Size(620,double.PositiveInfinity));body.Arrange(new Rect(0,0,620,body.DesiredSize.Height));body.UpdateLayout();
        var image=new System.Windows.Media.Imaging.RenderTargetBitmap(620,(int)Math.Ceiling(body.DesiredSize.Height),96,96,System.Windows.Media.PixelFormats.Pbgra32);image.Render(body);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));using(var stream=File.Create(preview))encoder.Save(stream);
      }
      checkiday=CheckidayChecks.ExampleClient(DateTime.Now);RefreshHolidays();
      if(!holidayRead.IsEnabled || !holidayDaily.IsEnabled || holidayList.Items.Count!=2 || !holidayOpen.IsEnabled || !HolidayContext().Contains("World Bird Day") || HolidayContext().Contains("corbie_test_key"))throw new Exception("Cached holiday controls or context lost the list or exposed credentials.");
      string answer=HolidayMicReply(CancellationToken.None).GetAwaiter().GetResult();
      if(!answer.Contains("plumage"))throw new Exception("Mic holiday answer didn't use the verified list.");
      SetHolidayDaily(true);if(holidayDaily.IsChecked!=true || checkiday.NeedsDaily(DateTime.Now))throw new Exception("Manual holiday answer wasn't remembered for the daily remark.");
      companion.Quiet=true;if(CanSayDailyHoliday())throw new Exception("Quiet mode allowed a daily holiday remark.");companion.Quiet=false;
      paused=true;if(CanSayDailyHoliday())throw new Exception("Pause allowed a daily holiday remark.");paused=false;
      ForgetHolidayKey();if(checkiday.Configured || holidayList.Items.Count!=0 || holidayRead.IsEnabled || holidayOpinionBox.Text!="")throw new Exception("Forget left an active holiday UI or cached opinion.");
    }
  }
}
