using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CorbiePet {
  public static class DiscoveryChecks {
    const string FakeKey="corbie_ebird_test_key_not_real";
    sealed class FakeHandler : HttpMessageHandler {
      readonly Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> reply;
      public FakeHandler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> reply) {this.reply=reply;}
      protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) {token.ThrowIfCancellationRequested();return reply(request,token);}
    }
    static HttpResponseMessage Body(string body,HttpStatusCode status=HttpStatusCode.OK) {return new HttpResponseMessage(status) {Content=new StringContent(body,Encoding.UTF8,"application/json")};}
    static void Check(bool value,string error) {if(!value)throw new Exception(error);}
    static async Task Reject(Func<Task> action,string message) {bool denied=false;try {await action().ConfigureAwait(false);}catch(OperationCanceledException) {denied=true;}catch(InvalidOperationException) {denied=true;}catch(InvalidDataException) {denied=true;}Check(denied,message);}
    public static byte[] ImageFixture() {
      var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2,2,96,96,PixelFormats.Bgra32,null,new byte[]{80,140,190,255,80,140,190,255,80,140,190,255,80,140,190,255},8)));
      using(var stream=new MemoryStream()) {encoder.Save(stream);return stream.ToArray();}
    }
    public static string BirdFixture(DateTime date) {return "[{\"comName\":\"American Crow\",\"speciesCode\":\"amecro\",\"locName\":\"Test park\",\"obsDt\":\""+date.ToString("yyyy-MM-dd")+" 09:30\",\"subId\":\"S123456\",\"howMany\":3,\"obsValid\":true},{\"comName\":\"Unreviewed bird\",\"speciesCode\":\"test\",\"obsDt\":\""+date.ToString("yyyy-MM-dd")+"\",\"obsValid\":false},{\"comName\":\"Unsafe checklist\",\"speciesCode\":\"amecro\",\"obsDt\":\"bad date\",\"obsValid\":true}]";}
    public static string ArtFixture {get {return "{\"data\":[{\"id\":101,\"title\":\"Birds (synthetic test fixture)\",\"artist_display\":\"Test artist\",\"artist_title\":\"Test artist\",\"date_display\":\"1900\",\"image_id\":\"01234567-0123-0123-0123-012345678901\",\"is_public_domain\":true},{\"id\":102,\"title\":\"Gold (synthetic test fixture)\",\"artist_title\":\"Test artist\",\"image_id\":\"01234567-0123-0123-0123-012345678902\",\"is_public_domain\":true},{\"id\":103,\"title\":\"Copyrighted\",\"image_id\":\"01234567-0123-0123-0123-012345678903\",\"is_public_domain\":false},{\"id\":104,\"title\":\"Unsafe image\",\"image_id\":\"../../outside\",\"is_public_domain\":true}]}";}}
    public static async Task Run() {
      string folder=Path.Combine(Path.GetTempPath(),"corbie-discovery-check-"+Guid.NewGuid().ToString("N"));
      DateTime utc=DateTime.UtcNow;int birdCalls=0,artCalls=0,imageCalls=0;byte[] image=ImageFixture();
      Func<HttpMessageHandler> handler=()=>new FakeHandler((request,token)=> {
        string host=request.RequestUri.Host;
        Check(request.Method==HttpMethod.Get && request.Headers.Contains("User-Agent"),"Wrong request method or missing user agent.");
        if(host=="api.ebird.org") {
          birdCalls++;Check(request.Headers.Contains("X-eBirdApiToken") && string.Join("",request.Headers.GetValues("X-eBirdApiToken"))==FakeKey,"eBird header incorrect.");
          Check(request.RequestUri.AbsoluteUri=="https://api.ebird.org/v2/data/obs/US-OH-087/recent?back=7&maxResults=100&includeProvisional=false&fmt=json" || request.RequestUri.AbsoluteUri=="https://api.ebird.org/v2/data/obs/US-OH/recent?back=7&maxResults=100&includeProvisional=false&fmt=json","Unexpected bird endpoint or region.");
          return Task.FromResult(Body(BirdFixture(utc.ToLocalTime())));
        }
        Check(!request.Headers.Contains("X-eBirdApiToken"),"eBird key leaked to the museum.");
        if(host=="api.artic.edu") {artCalls++;Check(request.RequestUri.Query.Contains("is_public_domain") && request.RequestUri.Query.Contains("fields="),"Art search omitted public-domain filter or bounded fields.");return Task.FromResult(Body(ArtFixture));}
        Check(host=="www.artic.edu" && request.RequestUri.AbsolutePath.EndsWith("/full/843,/0/default.jpg"),"Unexpected image host or size.");imageCalls++;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content=new ByteArrayContent(image)});
      });
      try {
        var store=new DiscoveryStore(false,folder,handler,()=>utc);
        Check(!store.Configured && !store.BirdDaily && !store.ArtDaily && store.Region==DiscoveryStore.County,"Incorrect opt-in defaults or county.");
        bool invalid=false;try {store.SetRegion("Lawrence County");}catch(ArgumentException) {invalid=true;}Check(invalid,"Free-text region was accepted.");
        invalid=false;try {store.SaveKey("bad\r\nheader");}catch(ArgumentException) {invalid=true;}Check(invalid,"Invalid key accepted.");
        store.SaveKey(FakeKey);Check(!File.ReadAllText(Path.Combine(folder,"ebird.key")).Contains(FakeKey),"Key persisted in plain text.");
        var first=await store.FetchBirds(CancellationToken.None).ConfigureAwait(false);Check(birdCalls==1 && first.Sightings.Count==1 && first.Sightings[0].Count==3,"Bird parser failed validation.");
        Check(DiscoveryStore.BirdRemark(first).Contains("eBird reports") && DiscoveryStore.BirdRemark(first).Contains("observed") && DiscoveryStore.BirdPage(first.Sightings[0])=="https://ebird.org/checklist/S123456","Gossip lost provenance or dates.");
        var cached=await store.FetchBirds(CancellationToken.None).ConfigureAwait(false);Check(birdCalls==1 && cached==first,"Bird cache was bypassed.");
        var reload=new DiscoveryStore(false,folder,handler,()=>utc);Check(reload.Configured && reload.CachedBirds()!=null,"Encrypted key/report did not survive restart.");
        var found=await store.FindArt(CancellationToken.None).ConfigureAwait(false);Check(found.Art.Id==101 && found.Preview.IsFrozen && found.Art.Opinion.StartsWith("From the catalogue:"),"Art preference, decode or honest fallback failed.");
        store.Keep(found);store.Favourite(found.Art.Id);store.RememberOpinion(found.Art.Id,"A test visual impression.");
        reload=new DiscoveryStore(false,folder,handler,()=>utc);Check(reload.Hoard.Count==1 && reload.Hoard[0].Favourite && reload.Hoard[0].Looked && reload.SavedImage(found.Art.Id).SequenceEqual(image),"Art/image/favourite/impression did not survive restart.");
        await Reject(()=>store.FindArt(CancellationToken.None),"Fast repeated expedition ignored cooldown.");
        utc=utc.AddMinutes(1);var next=await store.FindArt(CancellationToken.None).ConfigureAwait(false);Check(next.Art.Id!=found.Art.Id && next.Art.Id==102,"Art expedition repeated or returned unsafe art.");
        var nextReload=new DiscoveryStore(false,folder,handler,()=>utc);utc=utc.AddMinutes(1);await Reject(()=>nextReload.FindArt(CancellationToken.None),"Restart lost no-repeat history.");
        reload.SetDaily(true,true);Check(reload.ReserveDaily(utc.ToLocalTime()) && !reload.ReserveDaily(utc.ToLocalTime()),"Daily limit failed.");
        Check(!new DiscoveryStore(false,folder,handler,()=>utc).NeedsDaily(utc.ToLocalTime()),"Daily receipt did not survive restart.");
        reload.Remove(found.Art.Id);Check(reload.Hoard.Count==0 && !File.Exists(Path.Combine(folder,"101.jpg")),"Put back left saved metadata or image.");
        store.SetRegion(DiscoveryStore.Ohio);Check(store.CachedBirds()==null,"Region change kept old sightings current.");
        utc=utc.AddMinutes(10);await store.FetchBirds(CancellationToken.None).ConfigureAwait(false);Check(birdCalls==2,"Ohio request missing.");
        var pending=new TaskCompletionSource<bool>();var entered=new TaskCompletionSource<bool>();
        var race=new DiscoveryStore(true,null,()=>new FakeHandler(async (request,token)=> {entered.SetResult(true);await pending.Task.ConfigureAwait(false);return Body(BirdFixture(utc.ToLocalTime()));}),()=>utc);race.SaveKey(FakeKey);
        var work=race.FetchBirds(CancellationToken.None);await entered.Task.ConfigureAwait(false);race.ForgetKey();pending.SetResult(true);await Reject(()=>work,"Forgotten key allowed late report.");Check(race.CachedBirds()==null,"Forgotten key retained report.");
        using(var cancel=new CancellationTokenSource()) {cancel.Cancel();int before=birdCalls;await Reject(()=>reload.FetchBirds(cancel.Token),"Cancelled request proceeded.");Check(before==birdCalls,"Cancelled request made HTTP call.");}
        var failing=new DiscoveryStore(true,null,()=>new FakeHandler((r,t)=>Task.FromResult(Body("secret provider error",HttpStatusCode.Unauthorized))),()=>utc);failing.SaveKey(FakeKey);
        await Reject(()=>failing.FetchBirds(CancellationToken.None),"HTTP 401 accepted.");await Reject(()=>failing.FetchBirds(CancellationToken.None),"Failed request did not cool down.");
        var large=new DiscoveryStore(true,null,()=>new FakeHandler((r,t)=>Task.FromResult(Body(new string('x',262145)))),()=>utc);large.SaveKey(FakeKey);await Reject(()=>large.FetchBirds(CancellationToken.None),"Oversized response accepted.");
        Check(DiscoveryStore.ParseArt(ArtFixture).Count==2 && !DiscoveryStore.ValidRegion("US-OH-087/../../evil"),"Unsafe metadata accepted.");
        Check(DiscoveryStore.BirdPage(new BirdSighting {Checklist="S1/../../evil"})==null,"Unvalidated checklist link.");
        invalid=false;try {DiscoveryStore.Decode(new byte[3145729]);}catch(InvalidDataException) {invalid=true;}Check(invalid,"Unbounded image accepted.");
        Check(WakeCommand.Parse("Corbie, what's the bird gossip?",true).Action==WakeAction.DiscoveryReport && WakeCommand.Parse("find me some art",true).Action==WakeAction.NotWoken && WakeCommand.Parse("I need advice about birds",false).Action==WakeAction.None,"Discovery intent bypassed wake/conversation gates.");
        Check(artCalls>0 && imageCalls==2,"Image fetch count unexpected.");
      }finally {
        // Only this test's unique directory and the files it created are removed.
        if(Directory.Exists(folder)) {foreach(string file in Directory.GetFiles(folder))File.Delete(file);Directory.Delete(folder);}
      }
    }
  }
  public partial class Pet {
    void CheckDiscoveryControls(string preview) {
      if(discoveries.Configured || discoveries.ArtDaily || discoveries.BirdDaily || discoveries.Hoard.Count>0)throw new Exception("Test pet loaded live discoveries.");
      var body=new StackPanel {Width=640,Background=LiningBrush};BuildDiscoveryControls(body);
      if(birdFetch.IsEnabled || birdDaily.IsChecked==true || artDaily.IsChecked==true || artKeep.IsEnabled || artOpen.IsEnabled || birdRegion.IsEditable)throw new Exception("Discovery defaults incorrectly enabled actions.");
      if(!DiscoveryContext().Contains("No current eBird"))throw new Exception("Missing bird data fabricated context.");
      var art=DiscoveryStore.ParseArt(DiscoveryChecks.ArtFixture)[0];currentArt=new ArtFind {Art=art,Image=DiscoveryChecks.ImageFixture(),Preview=DiscoveryStore.Decode(DiscoveryChecks.ImageFixture())};DisplayHoardArt(art);RefreshDiscoveries();
      if(!artKeep.IsEnabled || !artOpen.IsEnabled || artLove.IsEnabled || !artCaption.Text.Contains("synthetic test fixture"))throw new Exception("Found art actions incorrect.");
      discoveries.Keep(currentArt);RefreshDiscoveries();if(artKeep.IsEnabled || !artLove.IsEnabled || !artRemove.IsEnabled)throw new Exception("Kept art actions incorrect.");
      LoveHoardArt();if(!selectedArt.Favourite || !DiscoveryContext().Contains("Birds (synthetic test fixture)"))throw new Exception("Favourite not reflected in context.");
      discoveryRequest=new CancellationTokenSource();RefreshDiscoveries();if(artFind.IsEnabled || !discoveryCancel.IsEnabled || birdRegion.IsEnabled)throw new Exception("Busy discovery actions incorrect.");CancelDiscovery();if(!discoveryRequest.IsCancellationRequested)throw new Exception("Discovery cancel failed.");discoveryRequest.Dispose();discoveryRequest=null;
      companion.Quiet=true;if(CanOfferDiscovery())throw new Exception("Quiet allowed unsolicited discovery.");companion.Quiet=false;paused=true;if(CanOfferDiscovery())throw new Exception("Pause allowed unsolicited discovery.");paused=false;
      RemoveHoardArt();if(artImage.Source!=null || discoveries.Hoard.Count!=0 || selectedArt!=null)throw new Exception("Put back didn't clear preview.");
      if(!string.IsNullOrEmpty(preview))RenderDiscoveryPreview(body,preview);
    }
    void RenderDiscoveryPreview(StackPanel body,string path) {
      body.Measure(new Size(640,double.PositiveInfinity));body.Arrange(new Rect(0,0,640,body.DesiredSize.Height));body.UpdateLayout();
      var bitmap=new RenderTargetBitmap(640,(int)Math.Ceiling(body.DesiredSize.Height),96,96,PixelFormats.Pbgra32);bitmap.Render(body);
      var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))encoder.Save(file);
    }
    void PreviewLiveDiscovery(ArtFind found,string path) {
      var body=new StackPanel {Width=640,Background=LiningBrush};BuildDiscoveryControls(body);currentArt=found;DisplayHoardArt(found.Art);RefreshDiscoveries();RenderDiscoveryPreview(body,path);
    }
  }
}
