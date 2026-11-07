using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Media.Imaging;

namespace CorbiePet {
  public sealed class BirdSighting {
    public string Species { get; set; }
    public string Code { get; set; }
    public string Place { get; set; }
    public string Observed { get; set; }
    public string Checklist { get; set; }
    public int? Count { get; set; }
    public override string ToString() { return Species+" · "+Place+" · "+Observed; }
  }
  public sealed class BirdReport {
    public string Region { get; set; }
    public DateTime FetchedUtc { get; set; }
    public List<BirdSighting> Sightings { get; set; }
  }
  public sealed class HoardArt {
    public int Id { get; set; }
    public string Title { get; set; }
    public string Artist { get; set; }
    public string ArtistName { get; set; }
    public string Date { get; set; }
    public string ImageId { get; set; }
    public bool PublicDomain { get; set; }
    public bool Favourite { get; set; }
    public string Opinion { get; set; }
    public bool Looked { get; set; }
    public DateTime FoundUtc { get; set; }
    public string Page { get { return "https://www.artic.edu/artworks/"+Id.ToString(CultureInfo.InvariantCulture); } }
    public override string ToString() { return (Favourite?"★ ":"")+Title+" — "+ArtistName; }
  }
  public sealed class ArtFind {
    public HoardArt Art;
    public byte[] Image;
    public BitmapSource Preview;
  }
  public sealed class DiscoveryState {
    public string Region { get; set; }
    public bool BirdDaily { get; set; }
    public bool ArtDaily { get; set; }
    public string DailyReceipt { get; set; }
    public DateTime BirdAttemptUtc { get; set; }
    public DateTime ArtAttemptUtc { get; set; }
    public List<HoardArt> Art { get; set; }
    public List<int> Seen { get; set; }
    public int ArtSearches { get; set; }
  }
  // Independent of desktop screenshots: only explicit region codes and museum
  // catalogue searches leave the PC. Keys are never part of the shared HTTP client.
  public sealed class DiscoveryStore {
    public const string County="US-OH-087",Ohio="US-OH";
    public const int MaxArt=60;
    readonly string folder;
    readonly object sync=new object();
    readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
    readonly Func<HttpMessageHandler> factory;
    readonly Func<DateTime> clock;
    readonly JavaScriptSerializer json=new JavaScriptSerializer { MaxJsonLength=524288,RecursionLimit=20 };
    readonly Dictionary<string,List<HoardArt>> catalogues=new Dictionary<string,List<HoardArt>>();
    readonly Random random=new Random();
    DiscoveryState state=new DiscoveryState { Region=County,Art=new List<HoardArt>(),Seen=new List<int>() };
    BirdReport birds;
    string key;
    int generation;
    public DiscoveryStore(bool memoryOnly=false,string directory=null,Func<HttpMessageHandler> handler=null,Func<DateTime> utcNow=null) {
      folder=memoryOnly?null:directory??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Corbie","discoveries");
      factory=handler??(()=>new HttpClientHandler { AllowAutoRedirect=false,UseProxy=true });clock=utcNow??(()=>DateTime.UtcNow);
      if(folder==null)return;
      try { var loaded=json.Deserialize<DiscoveryState>(Read("settings.json",524288));if(loaded!=null && loaded.Art!=null && loaded.Art.Count<=MaxArt && loaded.Seen!=null && loaded.Seen.Count<=120) { loaded.Region=ValidRegion(loaded.Region)?loaded.Region:County;loaded.Art=loaded.Art.Select(CleanArt).Where(a=>a!=null).GroupBy(a=>a.Id).Select(g=>g.First()).ToList();loaded.Seen=loaded.Seen.Where(n=>n>0).Distinct().ToList();state=loaded; } }catch { }
      try { key=Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(Read("ebird.key",4096)),null,DataProtectionScope.CurrentUser));if(!ValidKey(key))key=null; }catch { }
      try { var report=json.Deserialize<BirdReport>(Read("birds.json",262144));if(report!=null && ValidRegion(report.Region) && report.Sightings!=null && report.Sightings.Count<=100) { report.Sightings=report.Sightings.Select(CleanBird).Where(b=>b!=null).ToList();birds=report; } }catch { }
    }
    string Read(string name,int max) { string path=Path.Combine(folder,name);if(new FileInfo(path).Length>max)throw new InvalidDataException();return File.ReadAllText(path); }
    void Write(string name,byte[] data) {
      if(folder==null)return;Directory.CreateDirectory(folder);
      string path=Path.Combine(folder,name),temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
      try { File.WriteAllBytes(temp,data);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path); }finally { if(File.Exists(temp))File.Delete(temp); }
    }
    void WriteJson(string name,object data) { Write(name,Encoding.UTF8.GetBytes(json.Serialize(data))); }
    void Save() { WriteJson("settings.json",state); }
    public static string Clean(string value,int max) { value=Regex.Replace(value??"",@"[\p{Cc}\p{Cf}]+"," ").Trim();return value.Length<=max?value:value.Substring(0,max); }
    public static bool ValidRegion(string region) { return region==County || region==Ohio; }
    public static string RegionName(string region) { return region==Ohio?"Ohio":"Lawrence County, Ohio"; }
    static bool ValidKey(string value) { return value!=null && Regex.IsMatch(value,@"\A[A-Za-z0-9_-]{8,256}\z"); }
    public string Region { get { lock(sync)return state.Region; } }
    public bool Configured { get { lock(sync)return !string.IsNullOrEmpty(key); } }
    public bool BirdDaily { get { lock(sync)return state.BirdDaily; } }
    public bool ArtDaily { get { lock(sync)return state.ArtDaily; } }
    public List<HoardArt> Hoard { get { lock(sync)return state.Art.Select(CloneArt).ToList(); } }
    static HoardArt CloneArt(HoardArt a) { return new HoardArt { Id=a.Id,Title=a.Title,Artist=a.Artist,ArtistName=a.ArtistName,Date=a.Date,ImageId=a.ImageId,PublicDomain=a.PublicDomain,Favourite=a.Favourite,Opinion=a.Opinion,Looked=a.Looked,FoundUtc=a.FoundUtc }; }
    public static HoardArt CopyArt(HoardArt a) { return CleanArt(a); }
    public void SetRegion(string value) {
      if(!ValidRegion(value))throw new ArgumentException("Choose Lawrence County or Ohio from the region picker.");
      lock(sync) { string old=state.Region;state.Region=value;try { Save(); }catch {state.Region=old;throw;}generation++;birds=null;if(folder!=null && File.Exists(Path.Combine(folder,"birds.json")))File.Delete(Path.Combine(folder,"birds.json")); }
    }
    public void SaveKey(string value) {
      value=(value??"").Trim();if(!ValidKey(value))throw new ArgumentException("Paste only your personal eBird API key.");
      lock(sync) { Write("ebird.key",Encoding.UTF8.GetBytes(Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value),null,DataProtectionScope.CurrentUser))));key=value;generation++;birds=null; }
    }
    public void ForgetKey() {
      lock(sync) { if(folder!=null && File.Exists(Path.Combine(folder,"ebird.key")))File.Delete(Path.Combine(folder,"ebird.key"));key=null;generation++;birds=null;state.BirdDaily=false;Save();if(folder!=null && File.Exists(Path.Combine(folder,"birds.json")))File.Delete(Path.Combine(folder,"birds.json")); }
    }
    public void SetDaily(bool bird,bool art) {
      lock(sync) { bool oldBird=state.BirdDaily,oldArt=state.ArtDaily;state.BirdDaily=bird && Configured;state.ArtDaily=art;try { Save(); }catch {state.BirdDaily=oldBird;state.ArtDaily=oldArt;throw;} }
    }
    public bool NeedsDaily(DateTime local) { lock(sync)return (state.ArtDaily || state.BirdDaily && Configured) && state.DailyReceipt!=local.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture); }
    public bool ReserveDaily(DateTime local) {
      lock(sync) { if(!NeedsDaily(local))return false;string old=state.DailyReceipt;state.DailyReceipt=local.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);try { Save();return true; }catch {state.DailyReceipt=old;throw;} }
    }
    public BirdReport CachedBirds() {
      lock(sync)return Configured && birds!=null && birds.Region==state.Region && birds.FetchedUtc<=clock().AddMinutes(2) && clock()-birds.FetchedUtc<TimeSpan.FromHours(1)?birds:null;
    }
    public static string BirdPage(BirdSighting bird) { return bird!=null && Regex.IsMatch(bird.Checklist??"",@"\AS[0-9]{1,20}\z")?"https://ebird.org/checklist/"+bird.Checklist:null; }
    static BirdSighting CleanBird(BirdSighting b) {
      DateTime observed;
      if(b==null || string.IsNullOrWhiteSpace(b.Species) || !Regex.IsMatch(b.Code??"",@"\A[a-z0-9]{1,25}\z") || !DateTime.TryParseExact(b.Observed,new[]{"yyyy-MM-dd HH:mm","yyyy-MM-dd"},CultureInfo.InvariantCulture,DateTimeStyles.None,out observed))return null;
      return new BirdSighting { Species=Clean(b.Species,100),Code=b.Code,Place=Clean(b.Place,160),Observed=b.Observed,Checklist=Regex.IsMatch(b.Checklist??"",@"\AS[0-9]{1,20}\z")?b.Checklist:null,Count=b.Count.HasValue && b.Count>=0 && b.Count<=10000000?b.Count:null };
    }
    sealed class ApiBird { public string comName { get; set; } public string speciesCode { get; set; } public string locName { get; set; } public string obsDt { get; set; } public string subId { get; set; } public int? howMany { get; set; } public bool? obsValid { get; set; } public bool? obsReviewed { get; set; } }
    public static List<BirdSighting> ParseBirds(string raw) {
      var items=new JavaScriptSerializer { MaxJsonLength=262144,RecursionLimit=16 }.Deserialize<ApiBird[]>(raw);
      if(items==null || items.Length>100)throw new InvalidDataException("eBird returned an invalid observation list.");
      return items.Where(b=>b!=null && b.obsValid==true).Select(b=>CleanBird(new BirdSighting { Species=b.comName,Code=b.speciesCode,Place=b.locName,Observed=b.obsDt,Checklist=b.subId,Count=b.howMany })).Where(b=>b!=null).GroupBy(b=>b.Code).Select(g=>g.First()).ToList();
    }
    public async Task<BirdReport> FetchBirds(CancellationToken token) {
      token.ThrowIfCancellationRequested();await gate.WaitAsync(token).ConfigureAwait(false);
      try {
        string region,apiKey;int version;
        lock(sync) {
          var cached=CachedBirds();if(cached!=null)return cached;
          if(!Configured)throw new InvalidOperationException("Save your eBird key in Nest > Discoveries first.");
          if(clock()-state.BirdAttemptUtc<TimeSpan.FromMinutes(5))throw new InvalidOperationException("Wait five minutes before retrying eBird.");
          state.BirdAttemptUtc=clock();Save();region=state.Region;apiKey=key;version=generation;
        }
        var data=await Get("https://api.ebird.org/v2/data/obs/"+region+"/recent?back=7&maxResults=100&includeProvisional=false&fmt=json",262144,token,apiKey).ConfigureAwait(false);
        var report=new BirdReport { Region=region,FetchedUtc=clock(),Sightings=ParseBirds(Encoding.UTF8.GetString(data)) };
        // eBird timestamps lack a time zone. Compare dates in the selected Ohio
        // region, not the PC's potentially different time zone.
        DateTime ohioDate=TimeZoneInfo.ConvertTimeFromUtc(clock(),TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")).Date;
        report.Sightings=report.Sightings.Where(b=>DateTime.ParseExact(b.Observed.Substring(0,10),"yyyy-MM-dd",CultureInfo.InvariantCulture)>=ohioDate.AddDays(-8) && DateTime.ParseExact(b.Observed.Substring(0,10),"yyyy-MM-dd",CultureInfo.InvariantCulture)<=ohioDate.AddDays(1)).ToList();
        lock(sync) { token.ThrowIfCancellationRequested();if(version!=generation)throw new OperationCanceledException();WriteJson("birds.json",report);birds=report; }
        return report;
      }finally { gate.Release(); }
    }
    async Task<byte[]> Get(string url,int max,CancellationToken token,string apiKey=null) {
      // The .NET Framework compiler targets legacy defaults on some machines.
      // Keep OS defaults when available; otherwise use modern TLS for HTTPS.
      if((int)System.Net.ServicePointManager.SecurityProtocol!=0)System.Net.ServicePointManager.SecurityProtocol=System.Net.SecurityProtocolType.Tls12;
      var uri=new Uri(url);
      if(uri.Scheme!="https" || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo) || !(uri.Host=="api.ebird.org" || uri.Host=="api.artic.edu" || uri.Host=="www.artic.edu") || apiKey!=null && uri.Host!="api.ebird.org")throw new InvalidOperationException("Unexpected discovery destination.");
      using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)) {
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        using(var client=new HttpClient(factory()) { Timeout=Timeout.InfiniteTimeSpan })
        using(var request=new HttpRequestMessage(HttpMethod.Get,uri)) {
          request.Headers.TryAddWithoutValidation("User-Agent","CorbiePet/0.106 (personal desktop companion)");
          if(apiKey!=null)request.Headers.TryAddWithoutValidation("X-eBirdApiToken",apiKey);
          using(var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,deadline.Token).ConfigureAwait(false)) {
            if((int)response.StatusCode==401 || (int)response.StatusCode==403)throw new InvalidOperationException(apiKey!=null?"eBird rejected that key. Check your personal API key.":"The museum is not accepting this request right now.");
            if((int)response.StatusCode==429)throw new InvalidOperationException("The provider is limiting requests. Let this connection rest before retrying.");
            if(!response.IsSuccessStatusCode)throw new InvalidOperationException("The discovery provider did not return a result (HTTP "+(int)response.StatusCode+").");
            if(response.Content.Headers.ContentLength>max)throw new InvalidDataException("The discovery response is too large.");
            using(var stream=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))using(var body=new MemoryStream()) {
              byte[] buffer=new byte[8192];int n;
              while((n=await stream.ReadAsync(buffer,0,buffer.Length,deadline.Token).ConfigureAwait(false))>0) { if(body.Length+n>max)throw new InvalidDataException("The discovery response is too large.");body.Write(buffer,0,n); }
              deadline.Token.ThrowIfCancellationRequested();return body.ToArray();
            }
          }
        }
      }
    }
    sealed class ArtResponse { public ApiArt[] data { get; set; } }
    sealed class ApiArt { public int id { get; set; } public string title { get; set; } public string artist_display { get; set; } public string artist_title { get; set; } public string date_display { get; set; } public string image_id { get; set; } public bool is_public_domain { get; set; } }
    static HoardArt CleanArt(HoardArt a) {
      if(a==null || a.Id<=0 || !a.PublicDomain || string.IsNullOrWhiteSpace(a.Title) || !Regex.IsMatch(a.ImageId??"",@"\A[a-fA-F0-9-]{20,100}\z"))return null;
      return new HoardArt { Id=a.Id,PublicDomain=true,Title=Clean(a.Title,180),Artist=Clean(a.Artist,300),ArtistName=Clean(a.ArtistName,160),Date=Clean(a.Date,100),ImageId=a.ImageId,Favourite=a.Favourite,Opinion=Clean(a.Opinion,600),Looked=a.Looked,FoundUtc=a.FoundUtc };
    }
    public static List<HoardArt> ParseArt(string raw) {
      var response=new JavaScriptSerializer { MaxJsonLength=524288,RecursionLimit=16 }.Deserialize<ArtResponse>(raw);
      if(response==null || response.data==null || response.data.Length>100)throw new InvalidDataException("The museum returned an invalid catalogue.");
      return response.data.Where(a=>a!=null).Select(a=>CleanArt(new HoardArt { Id=a.id,Title=a.title,Artist=a.artist_display,ArtistName=a.artist_title,Date=a.date_display,ImageId=a.image_id,PublicDomain=a.is_public_domain })).Where(a=>a!=null).GroupBy(a=>a.Id).Select(g=>g.First()).ToList();
    }
    static int Interest(HoardArt a) {
      string t=a.Title.ToLowerInvariant();
      if(Regex.IsMatch(t,@"\b(bird|birds|crow|crows|raven|ravens|magpie|feather|feathers)\b"))return 100;
      if(Regex.IsMatch(t,@"\b(gold|silver|jewel|jewels|necklace|coin|coins|brooch)\b"))return 85;
      if(Regex.IsMatch(t,@"\b(forest|wood|woods|woodland|moon|night|tree|trees)\b"))return 70;
      return 30;
    }
    public static string CatalogueOpinion(HoardArt a) {
      int interest=Interest(a);
      string aside=interest==100?"Excellent subject matter. Finally, someone understands the brief.":interest==85?"The catalogue mentions something shiny. Naturally, I am conducting further research.":interest==70?"That title sounds like somewhere a bird could brood comfortably.":"I rather like the sound of this one. The Nest could use a little culture.";
      return "From the catalogue: "+a.Title+". "+aside;
    }
    public async Task<ArtFind> FindArt(CancellationToken token,string projectQuery=null,Func<HoardArt,bool> projectMatch=null) {
      token.ThrowIfCancellationRequested();await gate.WaitAsync(token).ConfigureAwait(false);
      try {
        string query;List<HoardArt> candidates;
        lock(sync) {
          if(clock()-state.ArtAttemptUtc<TimeSpan.FromSeconds(10))throw new InvalidOperationException("Give me ten seconds between art expeditions.");
          state.ArtAttemptUtc=clock();Save();
          string[] themes={"bird","forest","gold","still life","moon","landscape"};
          var loved=state.Art.Where(a=>a.Favourite && !string.IsNullOrWhiteSpace(a.ArtistName)).ToList();
          int expedition=state.ArtSearches & int.MaxValue;
          query=string.IsNullOrWhiteSpace(projectQuery)?(loved.Count>0 && expedition%3==2?loved[random.Next(loved.Count)].ArtistName:themes[expedition%themes.Length]):Clean(projectQuery,160);
          state.ArtSearches=expedition==int.MaxValue?0:expedition+1;Save();
          catalogues.TryGetValue(query,out candidates);
        }
        if(candidates==null) {
          string url="https://api.artic.edu/api/v1/artworks/search?q="+Uri.EscapeDataString(query)+"&query%5Bterm%5D%5Bis_public_domain%5D=true&limit=50&fields=id,title,artist_display,artist_title,date_display,image_id,is_public_domain";
          candidates=ParseArt(Encoding.UTF8.GetString(await Get(url,524288,token).ConfigureAwait(false)));
          lock(sync) { if(catalogues.Count>=12)catalogues.Clear();catalogues[query]=candidates; }
        }
        HoardArt chosen;
        lock(sync) {
          var fresh=candidates.Where(a=>!state.Seen.Contains(a.Id) && !state.Art.Any(k=>k.Id==a.Id) && (projectMatch==null || projectMatch(a))).ToList();
          if(fresh.Count==0)throw new InvalidOperationException("No fresh pieces on that shelf. I'll try another shelf on the next expedition.");
          chosen=CloneArt(fresh.OrderByDescending(a=>Interest(a)+(state.Art.Any(k=>k.Favourite && k.ArtistName==a.ArtistName)?35:0)+random.Next(10)).First());
        }
        byte[] image=await Get(ImageUrl(chosen),3145728,token).ConfigureAwait(false);
        BitmapSource preview=Decode(image);chosen.Opinion=CatalogueOpinion(chosen);chosen.FoundUtc=clock();
        lock(sync) { token.ThrowIfCancellationRequested();state.Seen.Add(chosen.Id);while(state.Seen.Count>120)state.Seen.RemoveAt(0);Save(); }
        return new ArtFind { Art=chosen,Image=image,Preview=preview };
      }finally { gate.Release(); }
    }
    public static string ImageUrl(HoardArt a) {
      if(CleanArt(a)==null)throw new InvalidDataException("Invalid public-domain artwork.");
      return "https://www.artic.edu/iiif/2/"+a.ImageId+"/full/843,/0/default.jpg";
    }
    public static BitmapSource Decode(byte[] bytes) {
      if(bytes==null || bytes.Length==0 || bytes.Length>3145728)throw new InvalidDataException("Invalid artwork image size.");
      using(var stream=new MemoryStream(bytes,false)) {
        var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);
        var frame=decoder.Frames[0];if(frame.PixelWidth<=0 || frame.PixelHeight<=0 || (long)frame.PixelWidth*frame.PixelHeight>12000000)throw new InvalidDataException("Artwork image dimensions are too large.");
        stream.Position=0;var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=640;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();return bitmap;
      }
    }
    public void Keep(ArtFind find) {
      if(find==null)throw new ArgumentException("Find a piece of art first.");var art=CleanArt(find.Art);if(art==null)throw new InvalidDataException("This artwork cannot be kept.");
      Decode(find.Image);
      lock(sync) {
        if(state.Art.Any(a=>a.Id==art.Id))return;
        if(state.Art.Count>=MaxArt)throw new InvalidOperationException("The art hoard holds 60 pieces. Put one back before keeping another.");
        Write(art.Id.ToString(CultureInfo.InvariantCulture)+".jpg",find.Image);state.Art.Add(art);
        try { Save(); }catch {state.Art.Remove(art);if(folder!=null)File.Delete(ImagePath(art.Id));throw;}
      }
    }
    string ImagePath(int id) { return Path.Combine(folder,id.ToString(CultureInfo.InvariantCulture)+".jpg"); }
    public byte[] SavedImage(int id) {
      lock(sync) {
        if(folder==null || !state.Art.Any(a=>a.Id==id))return null;
        try { string path=ImagePath(id);if(new FileInfo(path).Length>3145728)return null;return File.ReadAllBytes(path); }catch {return null;}
      }
    }
    public void Favourite(int id) {
      lock(sync) { var art=state.Art.FirstOrDefault(a=>a.Id==id);if(art==null)return;bool old=art.Favourite;art.Favourite=!old;try { Save(); }catch {art.Favourite=old;throw;} }
    }
    public void RememberOpinion(int id,string opinion) {
      lock(sync) { var art=state.Art.FirstOrDefault(a=>a.Id==id);if(art==null)return;string old=art.Opinion;bool looked=art.Looked;art.Opinion=Clean(opinion,600);art.Looked=true;try { Save(); }catch {art.Opinion=old;art.Looked=looked;throw;} }
    }
    public void Remove(int id) {
      lock(sync) {
        int index=state.Art.FindIndex(a=>a.Id==id);if(index<0)return;var old=state.Art[index];state.Art.RemoveAt(index);try { Save(); }catch {state.Art.Insert(index,old);throw;}
        if(folder!=null && File.Exists(ImagePath(id)))File.Delete(ImagePath(id));
      }
    }
    public static string BirdRemark(BirdReport report) {
      if(report==null)return "No recent eBird report loaded. I won't invent neighbours.";
      var bird=report.Sightings.OrderByDescending(b=>Regex.IsMatch(b.Species,@"\b(crow|raven|magpie)",RegexOptions.IgnoreCase)).FirstOrDefault();
      if(bird==null)return "No published sightings came back for "+RegionName(report.Region)+" in the last seven days. That doesn't mean there are no birds.";
      return "eBird reports "+bird.Species+" at "+bird.Place+" in "+RegionName(report.Region)+", observed "+bird.Observed+". "+(Regex.IsMatch(bird.Species,@"\b(crow|raven|magpie)",RegexOptions.IgnoreCase)?"Relations, possibly. I accept no responsibility for their behaviour.":"Another feathered local. I shall reserve judgement until we've been introduced.");
    }
    public static int RequestKind(string text) {
      int project=CuriosityBook.RequestKind(text);if(project>0)return project;
      string value=SageAdvice.Normalize(text).Replace("'","");
      if(new[]{"whats the bird gossip","what is the bird gossip","bird gossip","check local birds","what birds are nearby","what birds have been seen nearby"}.Contains(value))return 1;
      if(new[]{"find me some art","find some art","bring me some art","show me some art"}.Contains(value))return 2;
      if(new[]{"show me your art hoard","open art hoard","open discoveries"}.Contains(value))return 3;
      return 0;
    }
  }
}
