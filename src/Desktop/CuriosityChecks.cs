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

namespace CorbiePet {
  public static class CuriosityChecks {
    static void Check(bool yes,string message) {if(!yes)throw new Exception(message);}
    public static ArtFind Fixture(int id=101,string artist="Test artist") {
      var a=DiscoveryStore.ParseArt(DiscoveryChecks.ArtFixture)[0];a.Id=id;a.Title="Bird's Nest (synthetic fixture "+id+")";a.Artist=a.ArtistName=artist;
      var bytes=DiscoveryChecks.ImageFixture();return new ArtFind {Art=a,Image=bytes,Preview=DiscoveryStore.Decode(bytes)};
    }
    sealed class MuseumHandler : HttpMessageHandler {
      protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken t) {
        t.ThrowIfCancellationRequested();
        if(r.Headers.Contains("X-eBirdApiToken"))throw new Exception("Project leaked bird credentials to museum.");
        if(r.RequestUri.Host=="api.artic.edu") {
          if(!r.RequestUri.Query.Contains("q=nest"))throw new Exception("Expedition ignored project query.");
          string raw=DiscoveryChecks.ArtFixture.Replace("Birds (synthetic test fixture)","Bird's Nest (synthetic test fixture)");
          return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent(raw,Encoding.UTF8,"application/json")});
        }
        if(r.RequestUri.Host!="www.artic.edu")throw new Exception("Unexpected project destination.");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content=new ByteArrayContent(DiscoveryChecks.ImageFixture())});
      }
    }
    public static async Task Run() {
      string folder=Path.Combine(Path.GetTempPath(),"corbie-curiosity-check-"+Guid.NewGuid().ToString("N"));DateTime utc=DateTime.UtcNow;
      try {
        var book=new CuriosityBook(false,folder,()=>utc,new Random(8));Check(book.Active==null && book.Summary().Contains("No current"),"Unstarted project invented evidence.");
        var p=book.Start("nests");var find=Fixture();
        Check(book.RecordArt(p.Id,find)!=null && book.RecordArt(p.Id,find)==null && book.Active.Notes.Count==1,"Duplicate art note recorded.");
        Check(book.Preview(book.Active).SequenceEqual(find.Image),"Offline scrapbook image was not saved.");
        book.RecordArt(p.Id,Fixture(102));Check(book.Active.Notes.Last().Why.Contains("Another catalogue piece by Test artist"),"Artist continuity was not grounded in an earlier find.");
        var restarted=new CuriosityBook(false,folder,()=>utc);Check(restarted.Active.Id==p.Id && restarted.Active.Notes.Count==2 && restarted.Preview(restarted.Active)!=null,"Project/evidence/preview didn't survive restart.");
        var detached=restarted.Active;detached.Notes.Clear();Check(restarted.Active.Notes.Count==2,"Public project snapshot mutated storage.");
        utc=utc.AddDays(3);book.Encourage();utc=utc.AddDays(2);Check(book.Choose(false,new HoardArt[0]).Id==p.Id,"Encouraged project rotated too early.");
        utc=utc.AddDays(3);var next=book.Choose(false,new HoardArt[0]);Check(next.Id!=p.Id && next.Topic!="corvids" && book.Projects.Any(x=>x.Id==p.Id && x.Status=="Finished"),"Project did not rotate/archive or chose unavailable bird integration.");
        Check(book.RecordArt(p.Id,Fixture(103))==null,"Late art result wrote into an archived project.");
        book.Shelve();Check(book.Active==null && book.Choose(false,new HoardArt[0],false)==null && book.Projects[0].Status=="Shelved","Shelving did not quiet automatic projects.");
        Check(new CuriosityBook(false,folder,()=>utc).Choose(false,new HoardArt[0],false)==null,"Restart lost shelving cooldown.");
        p=book.Start("corvids");var birds=new BirdReport {Region=DiscoveryStore.County,FetchedUtc=utc,Sightings=DiscoveryStore.ParseBirds(DiscoveryChecks.BirdFixture(utc))};
        Check(book.RecordBirds(p.Id,birds)==1 && book.RecordBirds(p.Id,birds)==0 && book.Active.Notes[0].Observed.EndsWith("09:30"),"Bird scrapbook lost dates or repeated sightings.");
        Check(book.Active.Notes[0].Why.Contains("haven't seen") && book.Active.Notes[0].Source=="https://ebird.org/checklist/S123456","Bird note claimed direct observation or lost source.");
        p=book.Start("nests");for(int i=0;i<14;i++)book.RecordArt(p.Id,Fixture(200+i));Check(book.Active.Notes.Count==12 && book.RecordArt(p.Id,Fixture(200))==null,"Bounded scrapbook forgot recent dedup history.");
        File.WriteAllBytes(Path.Combine(folder,p.Id+".jpg"),new byte[]{1,2,3});Check(book.Preview(book.Active)==null,"Mismatched cached image was attributed to artwork.");
        for(int i=0;i<7;i++)book.Start(i%2==0?"woodland":"curios");Check(book.Projects.Count==6 && Directory.GetFiles(folder,"*.jpg").Length==0,"Project/archive image cap failed.");
        Check(!CuriosityBook.ValidSource("https://www.artic.edu.evil.example/artworks/101") && !CuriosityBook.ValidSource("https://ebird.org/checklist/S1?redirect=evil") && !CuriosityBook.ValidSource("file:///c:/secret") && CuriosityBook.ValidSource(find.Art.Page),"Scrapbook source validation failed.");
        var store=new DiscoveryStore(true,null,()=>new MuseumHandler());var project=new CuriosityBook(true);p=project.Start("nests");
        var actual=await store.FindArt(CancellationToken.None,project.ArtQuery(p),a=>CuriosityBook.Matches(p,a)).ConfigureAwait(false);
        Check(actual.Art.Id==101 && actual.Art.Title.Contains("Nest"),"Project selected unrelated museum metadata.");
        Check(project.RecordArt(p.Id,actual)!=null && store.Hoard.Count==0,"Project evidence implicitly kept art in hoard.");
        Check(WakeCommand.Parse("Corbie what are you curious about",true).Action==WakeAction.DiscoveryReport && DiscoveryStore.RequestKind("Corbie, look for nests") ==9 && WakeCommand.Parse("shelve this project",true).Action==WakeAction.NotWoken && WakeCommand.Parse("I don't want to shelve this project",false).Action==WakeAction.None,"Project mic routing bypassed wake or conversational gates.");
        // Existing v0.106 settings are independent and remain readable.
        var oldStore=new DiscoveryStore(false,folder);oldStore.Keep(find);Check(new DiscoveryStore(false,folder).Hoard.Count==1,"Project storage interfered with existing art hoard.");
      }finally {if(Directory.Exists(folder)) {foreach(string file in Directory.GetFiles(folder))File.Delete(file);Directory.Delete(folder);}}
    }
  }
  public partial class Pet {
    void CheckCuriosityControls(string panelPreview,string posePreview) {
      var body=new StackPanel {Width=640,Background=LiningBrush};BuildDiscoveryControls(body);
      if(curiosity.Active!=null || curiosityEncourage.IsEnabled || curiosityShow.IsEnabled || curiosityShelve.IsEnabled)throw new Exception("Empty project enabled evidence controls.");
      StartCuriosity("nests");var p=curiosity.Active;var find=CuriosityChecks.Fixture();NoteCuriosityArt(p.Id,find);currentArt=find;DisplayHoardArt(find.Art);RefreshDiscoveries();
      if(curiosityNotes.Items.Count!=1 || !curiosityShow.IsEnabled || !curiositySource.IsEnabled || !curiosityDetail.Text.Contains("explicitly mentions a nest"))throw new Exception("Project evidence or source controls missing.");
      EncourageCuriosity();if(curiosity.Active.Encouragements!=1)throw new Exception("Encourage control failed.");
      roost.Data.SkyRoostEnabled=false;isNightSleep=false;fullscreenOrPresentation=false;Show();
      if(!BeginShowAndTell(find.Preview))throw new Exception("Unblocked bird couldn't present a find.");
      double x=Left,y=Top;facing.ScaleX=1;StepShowAndTell(.1);if(showTellFrame.Visibility!=Visibility.Visible || CanInitiate() || showTellMirror.ScaleX!=1)throw new Exception("Presentation did not guard other behaviour.");
      facing.ScaleX=-1;StepShowAndTell(.1);if(showTellMirror.ScaleX!=-1 || Left!=x || Top!=y || Canvas.GetLeft(showTellFrame)<0 || Canvas.GetLeft(showTellFrame)+showTellFrame.Width>180 || Canvas.GetTop(showTellFrame)+showTellFrame.Height>180)throw new Exception("Presentation moved the window, mirrored the image incorrectly, or left its bounds.");
      paused=true;StepShowAndTell(.1);if(ShowTellActive || showTellFrame.Visibility!=Visibility.Hidden || showTellImage.Source!=null)throw new Exception("Pause left a presentation visible.");paused=false;
      BeginShowAndTell(find.Preview);for(int i=0;i<72;i++)StepShowAndTell(.1);if(ShowTellActive || motion.Y!=0)throw new Exception("Presentation did not finish cleanly.");
      BeginShowAndTell(find.Preview);SwitchForm();if(ShowTellActive || showTellFrame.Visibility!=Visibility.Hidden)throw new Exception("Form change kept old presentation.");SwitchForm();
      isNightSleep=true;if(BeginShowAndTell(find.Preview))throw new Exception("Sleeping bird presented art.");isNightSleep=false;
      micRequest=new CancellationTokenSource();if(BeginShowAndTell(find.Preview) || !BeginShowAndTell(find.Preview,true) || !StepShowAndTell(.1))throw new Exception("Explicit mic show-and-tell or ordinary mic hold failed.");EndShowAndTell();micRequest.Dispose();micRequest=null;
      if(!string.IsNullOrEmpty(panelPreview))RenderDiscoveryPreview(body,panelPreview);
      if(!string.IsNullOrEmpty(posePreview))RenderShowTellPreview(find.Preview,posePreview);
      companion.Dismiss();ShelveCuriosity();if(curiosity.Active!=null || curiosityNotes.Items.Count!=1 || curiosityShelve.IsEnabled)throw new Exception("Shelving removed scrapbook or left active controls.");
    }
    void CheckLiveCuriosity(string report,string panelPreview,string posePreview) {
      var p=curiosity.Start("nests");var found=discoveries.FindArt(CancellationToken.None,curiosity.ArtQuery(p),a=>CuriosityBook.Matches(p,a)).GetAwaiter().GetResult();
      NoteCuriosityArt(p.Id,found);currentArt=found;
      var body=new StackPanel {Width=640,Background=LiningBrush};BuildDiscoveryControls(body);DisplayHoardArt(found.Art);RefreshDiscoveries();
      if(!string.IsNullOrEmpty(panelPreview))RenderDiscoveryPreview(body,panelPreview);if(!string.IsNullOrEmpty(posePreview))RenderShowTellPreview(found.Preview,posePreview);
      File.WriteAllText(report,"PASS: live project-directed museum search and source-backed scrapbook entry: "+found.Art.Title+" by "+found.Art.ArtistName+" · "+curiosity.Active.Notes[0].Source+" · "+curiosity.Active.Notes[0].Why+". Actual image rendered in four presentation poses.");
    }
  }
}
