using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Windows.Media.Control;

namespace CorbiePet {
  public sealed class NowPlaying {
    public string Source,Title,Artist,Album,Key;
    public bool Playing;
    public TimeSpan Position,Duration;
  }
  public sealed class MusicUpdate {
    public NowPlaying Track;
    public string Reaction;
    public bool Changed;
    public bool FavouriteReaction;
  }
  public sealed class TrackMemory {
    public string Key { get; set; }
    public string Title { get; set; }
    public string Artist { get; set; }
    public string Album { get; set; }
    public int Starts { get; set; }
    public int Completed { get; set; }
    public int FastSkips { get; set; }
    public int Repeats { get; set; }
    public int Intrinsic { get; set; }
    public int UserAffinity { get; set; }
    public double HeardSeconds { get; set; }
    public DateTime LastHeardUtc { get; set; }
  }
  sealed class MusicMemoryFile {
    public int Version { get; set; }
    public List<TrackMemory> Tracks { get; set; }
  }

  // Reads Windows' system media session. Spotify desktop and its web player
  // both publish track metadata here without giving Corbie account access.
  public sealed class MusicBrain : IDisposable {
    readonly bool testing;
    readonly string memoryPath;
    readonly List<TrackMemory> memories=new List<TrackMemory>();
    GlobalSystemMediaTransportControlsSessionManager manager;
    NowPlaying current;
    TrackMemory currentMemory;
    DateTime lastPollUtc;
    double heardThisPlay;
    TimeSpan maxPosition;
    bool disposed;
    readonly string[] favourites;
    readonly Dictionary<string,DateTime> favouriteNotices=new Dictionary<string,DateTime>();
    string currentFavourite;
    bool favouritePending;
    int favouriteRemarkIndex;
    public NowPlaying Current { get { return current; } }
    public string CurrentFavourite { get { return currentFavourite; } }
    public string FavouriteSummary() { return string.Join("\n",favourites.Select((track,i)=>(i+1)+". "+track)); }
    public int RememberedTracks { get { return memories.Count; } }
    public double CurrentFeeling { get { return currentMemory==null?0:Math.Max(currentFavourite==null?-1:.75,Math.Min(1,Score(currentMemory)/4)); } }

    public MusicBrain(bool testing=false) {
      this.testing=testing;
      favourites=testing?(string[])SecretPlaylist.DefaultFavourites.Clone():SecretPlaylist.Load();
      string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Corbie");
      memoryPath=Path.Combine(folder,"music-memory.json");
      if(!testing) Load();
    }
    public async Task<MusicUpdate> Poll(CancellationToken token) {
      if(disposed) return new MusicUpdate();
      if(manager==null) manager=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(token);
      var sessions=manager.GetSessions();
      GlobalSystemMediaTransportControlsSession chosen=null;
      foreach(var session in sessions) {
        if(session.SourceAppUserModelId.IndexOf("spotify",StringComparison.OrdinalIgnoreCase)>=0) { chosen=session; break; }
      }
      if(chosen==null) chosen=manager.GetCurrentSession();
      if(chosen==null) return Observe(null,DateTime.UtcNow);
      var props=await chosen.TryGetMediaPropertiesAsync().AsTask(token);
      if(props==null || string.IsNullOrWhiteSpace(props.Title)) return Observe(null,DateTime.UtcNow);
      var playback=chosen.GetPlaybackInfo(); var timeline=chosen.GetTimelineProperties();
      var track=new NowPlaying {
        Source=chosen.SourceAppUserModelId,Title=props.Title.Trim(),Artist=(props.Artist??"").Trim(),Album=(props.AlbumTitle??"").Trim(),
        Playing=playback!=null && playback.PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
        Position=timeline==null?TimeSpan.Zero:timeline.Position,Duration=timeline==null?TimeSpan.Zero:timeline.EndTime
      };
      track.Key=MakeKey(track.Artist,track.Title);
      return Observe(track,DateTime.UtcNow);
    }
    internal MusicUpdate Observe(NowPlaying next,DateTime now) {
      var update=new MusicUpdate { Track=next };
      if(current!=null && next!=null && current.Key==next.Key) {
        double elapsed=Math.Max(0,Math.Min(15,(now-lastPollUtc).TotalSeconds));
        if(current.Playing) heardThisPlay+=elapsed;
        bool restarted=maxPosition>TimeSpan.FromSeconds(35) && next.Position+TimeSpan.FromSeconds(20)<maxPosition;
        current=next; lastPollUtc=now; if(next.Position>maxPosition) maxPosition=next.Position;
        if(restarted) {
          FinishCurrent(false); currentMemory.Repeats++; currentMemory.Starts++; heardThisPlay=0; maxPosition=next.Position; Save();
          update.Changed=true; update.Reaction="Again? Fine. \""+next.Title+"\" has earned another flight around the room.";
          favouritePending=true;
        }
        RecogniseFavourite(update,now);
        return update;
      }
      string previousReaction=FinishCurrent(true);
      current=next; lastPollUtc=now; heardThisPlay=0; maxPosition=next==null?TimeSpan.Zero:next.Position; currentMemory=null;
      currentFavourite=null; favouritePending=true;
      if(next==null) { Save(); return update; }
      currentMemory=Find(next.Key);
      bool known=currentMemory!=null;
      if(!known) {
        currentMemory=new TrackMemory { Key=next.Key,Title=next.Title,Artist=next.Artist,Album=next.Album,Intrinsic=Intrinsic(next.Key) };
        memories.Add(currentMemory);
      }
      currentMemory.Title=next.Title; currentMemory.Artist=next.Artist; currentMemory.Album=next.Album;
      currentMemory.Starts++; currentMemory.LastHeardUtc=now; update.Changed=true;
      if(!string.IsNullOrEmpty(previousReaction)) update.Reaction=previousReaction;
      else if(known && currentMemory.Starts==3) update.Reaction="Oh, I know this one. \""+next.Title+"\" has officially become part of the nest.";
      else if(known && currentMemory.Starts>3 && currentMemory.Starts%5==0) update.Reaction="Back to \""+next.Title+"\" again. At this point it pays rent.";
      RecogniseFavourite(update,now);
      Save(); return update;
    }
    void RecogniseFavourite(MusicUpdate update,DateTime now) {
      currentFavourite=SecretPlaylist.Match(current,favourites);
      if(currentFavourite==null || !current.Playing || !favouritePending) return;
      favouritePending=false;
      DateTime last;
      if(favouriteNotices.TryGetValue(currentFavourite,out last) && (now-last).TotalMinutes<5) return;
      favouriteNotices[currentFavourite]=now;
      string title=currentFavourite.Substring(0,currentFavourite.IndexOf(" - ",StringComparison.Ordinal));
      string[] remarks={
        "Oh, bloody hell. \"{0}\". One of mine. Leave this one on.",
        "\"{0}\"? Finally, a decision I can support. Yes, it's a favourite. Don't make a thing of it.",
        "That's \"{0}\". My feet have opinions about this one. Rather enthusiastic opinions.",
        "Fine. I love \"{0}\". There. You got a straight answer. Enjoy it."
      };
      update.Reaction=string.Format(remarks[(favouriteRemarkIndex++)%remarks.Length],title);
      update.FavouriteReaction=true;
    }
    string FinishCurrent(bool changingTrack) {
      if(currentMemory==null || current==null) return null;
      currentMemory.HeardSeconds+=heardThisPlay; currentMemory.LastHeardUtc=DateTime.UtcNow;
      double duration=current.Duration.TotalSeconds;
      bool completed=duration>30 && (maxPosition.TotalSeconds>=duration*0.72 || heardThisPlay>=Math.Min(150,duration*0.72));
      bool skipped=changingTrack && duration>60 && heardThisPlay<18 && maxPosition.TotalSeconds<35;
      if(completed) currentMemory.Completed++;
      if(skipped) { currentMemory.FastSkips++; Save(); return "That was a ruthless skip. \""+current.Title+"\" barely got its shoes off."; }
      return null;
    }
    TrackMemory Find(string key) { return memories.FirstOrDefault(x=>x.Key==key); }
    static int Clamp(int value,int low,int high) { return Math.Max(low,Math.Min(high,value)); }
    static bool Same(string a,string b) { return string.Equals((a??"").Trim(),(b??"").Trim(),StringComparison.OrdinalIgnoreCase); }
    double ArtistEvidence(TrackMemory track) {
      if(track==null || string.IsNullOrWhiteSpace(track.Artist)) return 0;
      var related=memories.Where(x=>x!=track && Same(x.Artist,track.Artist)).ToList();
      if(related.Count==0) return 0;
      double evidence=related.Sum(x=>x.Completed*0.22+x.Repeats*0.42-x.FastSkips*0.48+x.UserAffinity*0.7);
      return Math.Max(-2.5,Math.Min(2.5,evidence/Math.Sqrt(related.Count)));
    }
    double AlbumEvidence(TrackMemory track) {
      if(track==null || string.IsNullOrWhiteSpace(track.Album)) return 0;
      var related=memories.Where(x=>x!=track && Same(x.Album,track.Album) && Same(x.Artist,track.Artist)).ToList();
      if(related.Count==0) return 0;
      double evidence=related.Sum(x=>x.Completed*0.12+x.Repeats*0.25-x.FastSkips*0.3+x.UserAffinity*0.35);
      return Math.Max(-1.25,Math.Min(1.25,evidence/Math.Sqrt(related.Count)));
    }
    double Score(TrackMemory track) {
      if(track==null) return 0;
      double favourite=SecretPlaylist.Match(new NowPlaying { Title=track.Title,Artist=track.Artist },favourites)==null?0:4;
      return favourite+track.Intrinsic*0.55+track.Completed*0.55+track.Repeats*0.9-track.FastSkips+track.UserAffinity*1.4+ArtistEvidence(track)+AlbumEvidence(track);
    }
    int Confidence(TrackMemory track) {
      return track==null?0:track.Starts+track.Completed+track.Repeats+track.FastSkips+Math.Abs(track.UserAffinity)*2;
    }
    public string TeachCurrent(bool liked) {
      if(current==null || currentMemory==null) return "Play something first. I cannot file a musical opinion under 'ominous silence.'";
      currentMemory.UserAffinity=Clamp(currentMemory.UserAffinity+(liked?1:-1),-2,2);
      Save();
      string name="\""+current.Title+"\"";
      if(liked) return "Noted: you like "+name+". I will consider your testimony, though your taste remains under investigation.";
      return "Noted: "+name+" is not welcome in your nest. A harsh ruling. I respect it.";
    }
    public string Opinion() {
      if(current==null || currentMemory==null) return "Nothing's playing. Even I can't judge a song that refuses to exist.";
      double score=Score(currentMemory);
      double artistEvidence=ArtistEvidence(currentMemory);
      string name="\""+current.Title+"\""+(string.IsNullOrEmpty(current.Artist)?"":" by "+current.Artist);
      if(currentFavourite!=null) return name+" is one of my favourites. Yes, mine. I'll admit it when the song deserves it.";
      if(Confidence(currentMemory)<=1 && artistEvidence>=1.2) return name+" is new, but "+current.Artist+" has a respectable record in this nest. Cautious optimism.";
      if(Confidence(currentMemory)<=1 && artistEvidence<=-1.2) return name+" is new, and "+current.Artist+" already owes me an apology. Prove me wrong.";
      if(Confidence(currentMemory)<=1) return name+" is new to the nest. I'm withholding judgment until it proves it has feathers.";
      if(score>=3.25) return name+" has earned my approval. Annoyingly good. Do not become smug about it.";
      if(score<=-2.25) return name+" and I have artistic differences. Mine are that it should stop.";
      if(currentMemory.FastSkips>currentMemory.Completed) return name+" keeps getting hurled out the window before it settles in. Suspicious.";
      if(currentMemory.UserAffinity>0 && score<1.5) return "You clearly like "+name+". I remain unconvinced, but I have entered your objection into the ledger.";
      if(currentMemory.UserAffinity<0 && score>0.5) return "You keep objecting to "+name+". I think it has something, so this may become a domestic dispute.";
      return name+" is still under review. It has potential, which is more than I say for most things on this machine.";
    }
    public string Summary() {
      if(memories.Count==0) return "My music ledger is empty. Play something and let me develop unreasonable opinions.";
      var favorite=memories.OrderByDescending(Score).ThenByDescending(x=>x.HeardSeconds).First();
      var artists=memories.Where(x=>!string.IsNullOrWhiteSpace(x.Artist)).GroupBy(x=>x.Artist,StringComparer.OrdinalIgnoreCase).ToList();
      var favoriteArtist=artists.OrderByDescending(g=>g.Sum(x=>x.Completed*0.7+x.Repeats*1.1-x.FastSkips+x.UserAffinity*1.5)+g.Average(x=>x.Intrinsic)*0.2).FirstOrDefault();
      int quickSkips=memories.Sum(x=>x.FastSkips),taught=memories.Count(x=>x.UserAffinity!=0);
      string result="I've logged "+memories.Count+" tracks across "+artists.Count+" artists. Current nest favorite: \""+favorite.Title+"\""+(string.IsNullOrEmpty(favorite.Artist)?"":" by "+favorite.Artist)+".";
      if(favoriteArtist!=null && favoriteArtist.Count()>1) result+=" "+favoriteArtist.Key+" has the strongest overall record.";
      if(taught>0) result+=" You've personally weighed in on "+taught+" of them.";
      if(quickSkips>0) result+=" I have also witnessed "+quickSkips+" merciless quick skip"+(quickSkips==1?"":"s")+".";
      return result;
    }
    void Load() {
      try {
        if(!File.Exists(memoryPath)) return;
        var data=new JavaScriptSerializer().Deserialize<MusicMemoryFile>(File.ReadAllText(memoryPath));
        if(data!=null && data.Tracks!=null) memories.AddRange(data.Tracks.Where(x=>x!=null && !string.IsNullOrEmpty(x.Key)).Take(5000));
      } catch { }
    }
    void Save() {
      if(testing) return;
      try {
        Directory.CreateDirectory(Path.GetDirectoryName(memoryPath));
        string temp=memoryPath+".tmp";
        File.WriteAllText(temp,new JavaScriptSerializer().Serialize(new MusicMemoryFile { Version=2,Tracks=memories }));
        if(File.Exists(memoryPath)) File.Replace(temp,memoryPath,null); else File.Move(temp,memoryPath);
      } catch { }
    }
    static string MakeKey(string artist,string title) { return ((artist??"")+"\n"+(title??"")).Trim().ToLowerInvariant(); }
    static int Intrinsic(string value) { unchecked { uint hash=2166136261; foreach(char c in value) { hash^=c; hash*=16777619; } return (int)(hash%5)-2; } }
    public void Dispose() { if(disposed) return; FinishCurrent(false); Save(); disposed=true; manager=null; }
    public static void Check() {
      SecretPlaylist.Check();
      foreach(string track in SecretPlaylist.AddedFavourites) {
        using(var brain=new MusicBrain(true)) {
          int split=track.IndexOf(" - ",StringComparison.Ordinal);
          string title=track.Substring(0,split),artist=track.Substring(split+3);
          var update=brain.Observe(new NowPlaying { Title=title,Artist=artist,Key=MakeKey(artist,title),Playing=true },DateTime.UtcNow);
          if(!update.FavouriteReaction || !update.Reaction.Contains(title) || brain.CurrentFeeling<.75 || !brain.Opinion().Contains("one of my favourites") || !brain.FavouriteSummary().Contains(track))
            throw new Exception("New favourite did not reach recognition, opinion and Nest: "+track);
        }
      }
      using(var brain=new MusicBrain(true)) {
        DateTime t=DateTime.UtcNow;
        Func<string,string,bool,NowPlaying> song=(title,artist,playing)=>new NowPlaying { Title=title,Artist=artist,Key=MakeKey(artist,title),Playing=playing,Duration=TimeSpan.FromMinutes(3) };
        var first=brain.Observe(song("Blackbird - Remastered 2009","The Beatles",false),t);
        if(first.FavouriteReaction || brain.CurrentFavourite==null) throw new Exception("Paused favourite did not wait for playback.");
        var started=brain.Observe(song("Blackbird - Remastered 2009","The Beatles",true),t.AddSeconds(4));
        if(!started.FavouriteReaction || !started.Reaction.Contains("Blackbird") || brain.CurrentFeeling<.75 || !brain.Opinion().Contains("one of my favourites")) throw new Exception("Favourite was not recognised or enjoyed on first play.");
        if(brain.Observe(song("Blackbird - Remastered 2009","The Beatles",true),t.AddSeconds(8)).FavouriteReaction) throw new Exception("Favourite repeated on every poll.");
        brain.Observe(song("Blackbird - Remastered 2009","The Beatles",false),t.AddSeconds(12));
        if(brain.Observe(song("Blackbird - Remastered 2009","The Beatles",true),t.AddSeconds(16)).FavouriteReaction) throw new Exception("Pause/resume repeated the favourite greeting.");
        brain.Observe(song("Other","Unknown",true),t.AddSeconds(20));
        if(brain.CurrentFavourite!=null) throw new Exception("Previous favourite leaked to the next track.");
        if(brain.Observe(song("Blackbird - Remastered 2009","The Beatles",true),t.AddSeconds(24)).FavouriteReaction) throw new Exception("Rapid track switching bypassed the favourite cooldown.");
        brain.Observe(song("Other","Unknown",true),t.AddMinutes(6));
        var returnVisit=brain.Observe(song("Blackbird - Remastered 2009","The Beatles",true),t.AddMinutes(6).AddSeconds(4));
        if(!returnVisit.FavouriteReaction || returnVisit.Reaction==started.Reaction) throw new Exception("Favourite return never refreshed its wording.");
        brain.Observe(null,t.AddMinutes(7));
        if(brain.CurrentFavourite!=null || brain.CurrentFeeling!=0) throw new Exception("Missing metadata kept a favourite active.");
        if(!brain.FavouriteSummary().Contains("Blackbird") || !brain.FavouriteSummary().Contains("Material Girl")) throw new Exception("Nest favourite list is missing tracks.");
      }
      using(var brain=new MusicBrain(true)) {
        DateTime t=DateTime.UtcNow;
        var song=new NowPlaying { Title="Shiny Test",Artist="Test Birds",Key=MakeKey("Test Birds","Shiny Test"),Playing=true,Duration=TimeSpan.FromMinutes(3),Position=TimeSpan.FromSeconds(1) };
        brain.Observe(song,t); brain.Observe(song,t.AddSeconds(8));
        var next=new NowPlaying { Title="Other Test",Artist="Test Birds",Key=MakeKey("Test Birds","Other Test"),Playing=true,Duration=TimeSpan.FromMinutes(3) };
        var update=brain.Observe(next,t.AddSeconds(10));
        if(string.IsNullOrEmpty(update.Reaction) || brain.RememberedTracks!=2) throw new Exception("Music memory did not detect a fast skip.");
        string taught=brain.TeachCurrent(true);
        if(brain.currentMemory.UserAffinity!=1 || string.IsNullOrEmpty(taught)) throw new Exception("Music preference teaching failed.");
        if(brain.CurrentFeeling<-1 || brain.CurrentFeeling>1) throw new Exception("Music feeling escaped its expected range.");
        brain.TeachCurrent(false);
        if(brain.currentMemory.UserAffinity!=0) throw new Exception("Music preference teaching did not balance opposite feedback.");
        if(string.IsNullOrEmpty(brain.Opinion()) || string.IsNullOrEmpty(brain.Summary())) throw new Exception("Music opinions failed.");
      }
    }
  }
}
