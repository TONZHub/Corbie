using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CorbiePet {
  public sealed class CuriosityNote {
    public string Key { get; set; }
    public string Title { get; set; }
    public string Source { get; set; }
    public string Why { get; set; }
    public string Observed { get; set; }
    public DateTime RecordedUtc { get; set; }
    public HoardArt Art { get; set; }
    public override string ToString() { return RecordedUtc.ToLocalTime().ToString("MMM d")+" · "+Title; }
  }
  public sealed class CuriosityProject {
    public string Id { get; set; }
    public string Topic { get; set; }
    public string Status { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime UntilUtc { get; set; }
    public int Encouragements { get; set; }
    public List<CuriosityNote> Notes { get; set; }
    public List<string> SeenKeys { get; set; }
    public string ImageHash { get; set; }
    public int ImageArtId { get; set; }
    public string Title { get { return CuriosityBook.TopicTitle(Topic); } }
    public override string ToString() { return Title+" · "+Status; }
  }
  public sealed class CuriosityState {
    public string ActiveId { get; set; }
    public DateTime RestUntilUtc { get; set; }
    public List<CuriosityProject> Projects { get; set; }
  }
  // Evidence scrapbook, not imagined adventures. No network or model calls here.
  public sealed class CuriosityBook {
    public static readonly string[] Topics={"nests","woodland","curios","corvids"};
    readonly string folder;
    readonly Random random;
    readonly Func<DateTime> clock;
    readonly JavaScriptSerializer json=new JavaScriptSerializer { MaxJsonLength=524288,RecursionLimit=24 };
    CuriosityState state=new CuriosityState { Projects=new List<CuriosityProject>() };
    public CuriosityBook(bool memoryOnly=false,string directory=null,Func<DateTime> utcNow=null,Random rng=null) {
      folder=memoryOnly?null:directory??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Corbie","discoveries");clock=utcNow??(()=>DateTime.UtcNow);random=rng??new Random();
      if(folder==null)return;
      try {
        string path=Path.Combine(folder,"curiosity.json");if(new FileInfo(path).Length>524288)return;
        var data=json.Deserialize<CuriosityState>(File.ReadAllText(path));
        if(data==null || data.Projects==null || data.Projects.Count>6)return;
        var ids=new HashSet<string>();
        data.Projects=data.Projects.Where(p=>p!=null && ValidId(p.Id) && ids.Add(p.Id) && Topics.Contains(p.Topic) && new[]{"Exploring","Shelved","Finished"}.Contains(p.Status) && p.Notes!=null && p.Notes.Count<=12).ToList();
        foreach(var project in data.Projects) {
          project.Encouragements=Math.Max(0,Math.Min(100,project.Encouragements));
          project.Notes=project.Notes.Where(n=>n!=null && !string.IsNullOrWhiteSpace(n.Key) && ValidSource(n.Source)).Select(n=>new CuriosityNote { Key=DiscoveryStore.Clean(n.Key,180),Title=DiscoveryStore.Clean(n.Title,200),Source=n.Source,Why=DiscoveryStore.Clean(n.Why,500),Observed=DiscoveryStore.Clean(n.Observed,100),RecordedUtc=n.RecordedUtc,Art=DiscoveryStore.CopyArt(n.Art) }).GroupBy(n=>n.Key).Select(g=>g.First()).ToList();
          project.SeenKeys=(project.SeenKeys??project.Notes.Select(n=>n.Key).ToList()).Where(k=>!string.IsNullOrEmpty(k) && k.Length<=180).Distinct().Take(120).ToList();
        }
        if(!data.Projects.Any(p=>p.Id==data.ActiveId && p.Status=="Exploring"))data.ActiveId=null;
        if(data.RestUntilUtc>clock().AddDays(1))data.RestUntilUtc=clock().AddDays(1);
        state=data;
      }catch { } // An optional scrapbook cannot prevent Corbie starting.
    }
    static bool ValidId(string id) { return id!=null && Regex.IsMatch(id,@"\A[a-f0-9]{32}\z"); }
    public static bool ValidSource(string source) {
      Uri uri;if(source==null || source.Length>500 || !Uri.TryCreate(source,UriKind.Absolute,out uri) || uri.Scheme!="https" || !uri.IsDefaultPort || uri.UserInfo!="" || uri.Query!="" || uri.Fragment!="")return false;
      return uri.Host=="www.artic.edu" && Regex.IsMatch(uri.AbsolutePath,@"\A/artworks/[0-9]+\z") || uri.Host=="ebird.org" && Regex.IsMatch(uri.AbsolutePath,@"\A/checklist/S[0-9]{1,20}\z");
    }
    static CuriosityProject Clone(CuriosityProject p) {
      return p==null?null:new CuriosityProject { Id=p.Id,Topic=p.Topic,Status=p.Status,StartedUtc=p.StartedUtc,UntilUtc=p.UntilUtc,Encouragements=p.Encouragements,ImageHash=p.ImageHash,ImageArtId=p.ImageArtId,
        SeenKeys=new List<string>(p.SeenKeys),Notes=p.Notes.Select(n=>new CuriosityNote { Key=n.Key,Title=n.Title,Source=n.Source,Why=n.Why,Observed=n.Observed,RecordedUtc=n.RecordedUtc,Art=DiscoveryStore.CopyArt(n.Art) }).ToList() };
    }
    public CuriosityProject Active { get { return Clone(state.Projects.FirstOrDefault(p=>p.Id==state.ActiveId && p.Status=="Exploring")); } }
    public bool Resting {get {return Active==null && clock()<state.RestUntilUtc;}}
    public List<CuriosityProject> Projects { get { return state.Projects.AsEnumerable().Reverse().Select(Clone).ToList(); } }
    public static string TopicTitle(string topic) {
      switch(topic) {case "nests":return "Nests and their keepers";case "woodland":return "Places for a raven to brood";case "curios":return "Odd objects for the imaginary shelf";case "corvids":return "The local corvid situation";default:return "No current project";}
    }
    void Write(string name,byte[] bytes) {
      if(folder==null)return;Directory.CreateDirectory(folder);string path=Path.Combine(folder,name),temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
      try {File.WriteAllBytes(temp,bytes);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally {if(File.Exists(temp))File.Delete(temp);}
    }
    void Save() { Write("curiosity.json",Encoding.UTF8.GetBytes(json.Serialize(state))); }
    void Mutate(Action action) {
      var before=json.Serialize(state);
      try {action();Save();}catch {state=json.Deserialize<CuriosityState>(before);throw;}
    }
    public CuriosityProject Choose(bool birdsAvailable,IEnumerable<HoardArt> hoard,bool manual=true) {
      DateTime now=clock();var active=Active;
      if(active!=null && (now<active.UntilUtc || active.Notes.Count<2))return active;
      if(!manual && now<state.RestUntilUtc)return null;
      var recent=state.Projects.AsEnumerable().Reverse().Take(2).Select(p=>p.Topic).ToList();
      var options=Topics.Where(t=>(t!="corvids" || birdsAvailable) && !recent.Contains(t)).ToList();if(options.Count==0)options=Topics.Where(t=>t!="corvids" || birdsAvailable).ToList();
      string favourite=string.Join(" ",(hoard??new HoardArt[0]).Where(a=>a.Favourite).Select(a=>a.Title)).ToLowerInvariant();
      string topic=options.OrderByDescending(t=>random.Next(20)+(t=="nests" && favourite.Contains("nest")?25:t=="woodland" && Regex.IsMatch(favourite,@"forest|tree|wood")?25:0)).First();
      return Start(topic);
    }
    public CuriosityProject Start(string topic) {
      if(!Topics.Contains(topic))throw new ArgumentException("Choose one of Corbie's curiosity topics.");
      List<string> pruned=new List<string>();
      Mutate(()=> {
        var old=state.Projects.FirstOrDefault(p=>p.Id==state.ActiveId);if(old!=null)old.Status="Finished";
        var project=new CuriosityProject { Id=Guid.NewGuid().ToString("N"),Topic=topic,Status="Exploring",StartedUtc=clock(),UntilUtc=clock().AddDays(4),Notes=new List<CuriosityNote>(),SeenKeys=new List<string>() };
        state.Projects.Add(project);state.ActiveId=project.Id;state.RestUntilUtc=DateTime.MinValue;
        while(state.Projects.Count>6) {pruned.Add(state.Projects[0].Id);state.Projects.RemoveAt(0);}
      });
      foreach(string id in pruned)DeletePreview(id);return Active;
    }
    public void Encourage() {
      if(Active==null)return;
      Mutate(()=> {var p=state.Projects.First(x=>x.Id==state.ActiveId);p.Encouragements=Math.Min(100,p.Encouragements+1);p.UntilUtc=clock().AddDays(4);});
    }
    public void Shelve() {
      if(Active==null)return;
      Mutate(()=> {state.Projects.First(p=>p.Id==state.ActiveId).Status="Shelved";state.ActiveId=null;state.RestUntilUtc=clock().AddDays(1);});
    }
    public static bool Matches(CuriosityProject p,HoardArt a) {
      if(p==null || a==null || p.Topic=="corvids")return false;
      string title=a.Title??"";
      return p.Topic=="nests"?Regex.IsMatch(title,@"\bnests?\b",RegexOptions.IgnoreCase):p.Topic=="woodland"?Regex.IsMatch(title,@"\b(forest|woods?|woodland|trees?|moon|landscape)\b",RegexOptions.IgnoreCase):true;
    }
    public string ArtQuery(CuriosityProject project) {
      if(project==null || project.Topic=="corvids")return null;
      string[] queries=project.Topic=="nests"?new[]{"nest"}:project.Topic=="woodland"?new[]{"forest","tree","moon","landscape"}:new[]{"vessel","brooch","container","cup"};
      return queries[project.Notes.Count%queries.Length];
    }
    static string ArtWhy(CuriosityProject project,HoardArt art) {
      var earlier=project.Notes.Where(n=>n.Art!=null).FirstOrDefault(n=>!string.IsNullOrEmpty(art.ArtistName) && n.Art.ArtistName==art.ArtistName);
      if(earlier!=null)return "Another catalogue piece by "+art.ArtistName+", like "+earlier.Title+" in this scrapbook. I'm following that connection.";
      return project.Topic=="nests"?"The title explicitly mentions a nest. Architecture worth studying if one has feathers.":project.Topic=="woodland"?"The title fits my woodland-and-brooding study. I'm collecting places that sound suitable for a raven.":"Found while searching the museum for curious objects. A candidate for my imaginary shelf; the physical original stays at the museum.";
    }
    public CuriosityNote RecordArt(string projectId,ArtFind found) {
      var project=Active;if(project==null || project.Id!=projectId || found==null || !Matches(project,found.Art))return null;
      var art=DiscoveryStore.CopyArt(found.Art);if(art==null)return null;
      string key="art:"+art.Id;if(project.SeenKeys.Contains(key))return null;
      var note=new CuriosityNote { Key=key,Title=art.Title,Source=art.Page,Why=ArtWhy(project,art),RecordedUtc=clock(),Art=art };
      DiscoveryStore.Decode(found.Image);
      byte[] oldImage=Preview(project);
      try {
        Write(project.Id+".jpg",found.Image);
        Mutate(()=> {var p=state.Projects.First(x=>x.Id==project.Id);p.Notes.Add(note);p.SeenKeys.Add(key);Trim(p);p.ImageArtId=art.Id;p.ImageHash=Hash(found.Image);});
      }catch {if(oldImage!=null)Write(project.Id+".jpg",oldImage);else DeletePreview(project.Id);throw;}
      return note;
    }
    public int RecordBirds(string projectId,BirdReport report) {
      var project=Active;if(project==null || project.Id!=projectId || project.Topic!="corvids" || report==null)return 0;
      var notes=report.Sightings.Where(b=>Regex.IsMatch(b.Species,@"\b(crow|raven|magpie)",RegexOptions.IgnoreCase) && DiscoveryStore.BirdPage(b)!=null).Select(b=>new CuriosityNote {
        Key=DiscoveryStore.Clean("bird:"+b.Code+":"+b.Checklist+":"+b.Observed,180),Title=b.Species+" · "+b.Place,Source=DiscoveryStore.BirdPage(b),Observed=b.Observed,
        Why="A reported corvid sighting in "+DiscoveryStore.RegionName(report.Region)+". Adding this checklist to the neighbourhood study; I haven't seen this bird myself.",RecordedUtc=clock()
      }).Where(n=>!project.SeenKeys.Contains(n.Key)).GroupBy(n=>n.Key).Select(g=>g.First()).Take(3).ToList();
      if(notes.Count==0)return 0;
      Mutate(()=> {var p=state.Projects.First(x=>x.Id==project.Id);p.Notes.AddRange(notes);p.SeenKeys.AddRange(notes.Select(n=>n.Key));Trim(p);});return notes.Count;
    }
    static void Trim(CuriosityProject p) {while(p.Notes.Count>12)p.Notes.RemoveAt(0);while(p.SeenKeys.Count>120)p.SeenKeys.RemoveAt(0);}
    static string Hash(byte[] bytes) {using(var hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(bytes));}
    public byte[] Preview(CuriosityProject project) {
      if(folder==null || project==null || !ValidId(project.Id) || project.ImageArtId<=0 || string.IsNullOrEmpty(project.ImageHash))return null;
      try {string path=Path.Combine(folder,project.Id+".jpg");if(new FileInfo(path).Length>3145728)return null;var bytes=File.ReadAllBytes(path);return Hash(bytes)==project.ImageHash?bytes:null;}catch {return null;}
    }
    void DeletePreview(string id) {if(folder==null || !ValidId(id))return;try {string path=Path.Combine(folder,id+".jpg");if(File.Exists(path))File.Delete(path);}catch {}}
    public string Summary() {
      var p=Active;return p==null?"No current curiosity project. Let me choose an interest, or suggest one in Discoveries.":"I'm investigating "+p.Title+". "+p.Notes.Count+" recorded finds so far. "+(p.Notes.Count==0?"No evidence yet. A promising title is not a discovery.":"Latest: "+p.Notes.Last().Title+". "+p.Notes.Last().Why);
    }
    public static int RequestKind(string text) {
      string s=SageAdvice.Normalize(text).Replace("'","");
      if(new[]{"what are you curious about","whats your curiosity project","what is your curiosity project"}.Contains(s))return 4;
      if(new[]{"encourage this project","keep exploring this","keep investigating this"}.Contains(s))return 5;
      if(new[]{"shelve this project","shelve your project"}.Contains(s))return 6;
      if(new[]{"continue your project","continue exploring","go on an expedition"}.Contains(s))return 7;
      if(new[]{"show me your latest find","show and tell","show me what you found"}.Contains(s))return 8;
      if(s=="look for nests")return 9;if(s=="look for woodland art")return 10;if(s=="look for curious objects")return 11;if(s=="study local corvids")return 12;
      return 0;
    }
  }
}
