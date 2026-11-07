using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;

namespace CorbiePet {
  // Default favourites and their editable local copy. Keep the legacy filename.
  // Updates append new favourites once, preserving the user's existing entries.
  // Favourites are real preferences used by the music brain, not just idle jokes.
  public static class SecretPlaylist {
    static readonly string[] OriginalFavourites={
      "Shiny - Jemaine Clement (Moana)",
      "Blackbird - The Beatles",
      "Free Bird - Lynyrd Skynyrd",
      "Three Little Birds - Bob Marley & The Wailers",
      "Paint It Black - The Rolling Stones",
      "Rockin' Robin - Bobby Day",
      "Diamonds Are a Girl's Best Friend - Marilyn Monroe",
      "Fly Like an Eagle - Steve Miller Band",
      "Bird on the Wire - Leonard Cohen",
      "Material Girl - Madonna"
    };
    public static readonly string[] AddedFavourites={
      "I'm Not Like Everybody Else - The Kinks",
      "God's Away on Business - Tom Waits",
      "Red Right Hand - Nick Cave & The Bad Seeds",
      "Feel Good Inc. - Gorillaz",
      "Bird Song - Florence + The Machine"
    };
    public static readonly string[] DefaultFavourites=OriginalFavourites.Concat(AddedFavourites).ToArray();
    const string ExpansionMarker="# Corbie favourites update v0.94";
    static readonly string[] Teases={
      "My favourites are in the Nest. That is a list, not permission to judge me.",
      "Put one of my favourites on. I may briefly become tolerable.",
      "I have a favourites list. Yes, some of it is bird-themed. Mind your bloody business.",
      "I've got impeccable taste in music. You'll just have to take my word for it, won't you?",
      "There is a playlist in my head. The Nest has the paperwork.",
      "Hum? Me? I wasn't humming. Mind your own fucking business.",
      "I can name my favourites. Admitting I enjoy myself is the difficult bit.",
      "Your taste is under investigation. Mine is available for inspection in the Nest.",
      "Somewhere in my skull a certain song is on loop. Smug? Me? Always."
    };
    public const string FileName="secret-top-ten.txt";
    public static string DefaultPath {
      get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Corbie",FileName); }
    }
    public static string Tease(Random random) { return Teases[random.Next(Teases.Length)]; }
    public static string[] Load(string path=null) {
      try {
        path=path??DefaultPath;
        if(File.Exists(path) && new FileInfo(path).Length<=65536) {
          var lines=File.ReadAllLines(path);
          var tracks=lines.Select(x=>Regex.Replace(x.Trim(),@"^\d+[.)]\s*","")).
            Where(x=>x.Length<=300 && x.IndexOf(" - ",StringComparison.Ordinal)>0 && !x.StartsWith("#")).Take(50).ToArray();
          if(tracks.Length>0) {
            if(!lines.Any(x=>x.Trim()==ExpansionMarker)) {
              var added=AddedFavourites.Where(x=>!tracks.Any(t=>Normalize(t)==Normalize(x))).ToArray();
              var suffix=new StringBuilder("\r\n"+ExpansionMarker+"\r\n");
              for(int i=0;i<added.Length;i++) suffix.Append((tracks.Length+i+1)+". "+added[i]+"\r\n");
              // Additive migration only. If saving fails, recognition still gets the new tracks.
              try { File.AppendAllText(path,suffix.ToString(),new UTF8Encoding(false)); } catch { }
              return tracks.Concat(added).ToArray();
            }
            return tracks;
          }
        }
      } catch { }
      return (string[])DefaultFavourites.Clone();
    }
    static string Normalize(string text) {
      return Regex.Replace((text??"").ToLowerInvariant(),@"[^\p{L}\p{N}]+"," ").Trim();
    }
    static string CleanTitle(string text) {
      return Regex.Replace(text??"",@"\s*(?:[\(\[][\s\S]*(?:remaster|official|lyrics?|audio|video|live|version|soundtrack|from|hd|4k)[\s\S]*[\)\]]|[-\u2013\u2014]\s*(?:\d{4}\s*)?(?:remaster(?:ed)?|official|lyrics?|audio|video|live|version|soundtrack|from)\b.*)$","",RegexOptions.IgnoreCase).Trim();
    }
    static bool SameArtist(string actual,string expected) {
      actual=Normalize(Regex.Replace(actual??"",@"\([^)]*\)",""));
      expected=Normalize(Regex.Replace(expected??"",@"\([^)]*\)",""));
      actual=Regex.Replace(actual,@"\band\b\s*","").Trim();
      expected=Regex.Replace(expected,@"\band\b\s*","").Trim();
      if(expected=="bob marley the wailers" && actual=="bob marley") return true;
      return actual.Length>0 && (actual==expected || (" "+actual+" ").Contains(" "+expected+" "));
    }
    public static string Match(NowPlaying song,string[] tracks=null) {
      if(song==null || string.IsNullOrWhiteSpace(song.Title)) return null;
      string title=CleanTitle(song.Title);
      foreach(string track in tracks??DefaultFavourites) {
        int split=track.IndexOf(" - ",StringComparison.Ordinal); if(split<=0) continue;
        string name=track.Substring(0,split),artist=track.Substring(split+3);
        if(Normalize(title)==Normalize(name) && SameArtist(song.Artist,artist)) return track;
        // Video titles often carry both artist and title; the artist field may be a channel name.
        int divider=title.IndexOf(" - ",StringComparison.Ordinal);
        if(divider>0) {
          string left=title.Substring(0,divider),right=title.Substring(divider+3);
          if((SameArtist(left,artist) && Normalize(right)==Normalize(name)) ||
             (SameArtist(right,artist) && Normalize(left)==Normalize(name))) return track;
        }
      }
      return null;
    }
    public static string FileText() {
      var sb=new StringBuilder();
      sb.Append("CORBIE'S FAVOURITES. Yes, you can inspect the list. Try not to look smug.\r\n");
      sb.Append("Tell anyone and I'll peck your cursor into next week.\r\n\r\n");
      sb.Append(ExpansionMarker+"\r\n");
      for(int i=0;i<DefaultFavourites.Length;i++) sb.Append((i+1)+". "+DefaultFavourites[i]+"\r\n");
      return sb.ToString();
    }
    // Creates the file only if it is missing. Returns true when a new file was written.
    public static bool EnsureFile(string path=null) {
      try {
        path=path??DefaultPath;
        if(File.Exists(path)) return false;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path,FileText(),new UTF8Encoding(false));
        return true;
      } catch { return false; }
    }
    static bool IsAscii(string s) { foreach(char c in s) if(c>126 || (c<32 && c!='\r' && c!='\n')) return false; return true; }
    public static void Check() {
      if(DefaultFavourites.Length!=15) throw new Exception("SecretPlaylist: need fifteen favourites");
      if(Teases.Length<8) throw new Exception("SecretPlaylist: tease pool too small");
      foreach(var t in Teases) {
        if(!IsAscii(t)) throw new Exception("SecretPlaylist: non-ASCII tease");
        foreach(var track in DefaultFavourites) {
          string title=track.Substring(0,track.IndexOf(" - ",StringComparison.Ordinal));
          if(t.IndexOf(title,StringComparison.OrdinalIgnoreCase)>=0 && title!="Shiny") throw new Exception("SecretPlaylist: tease leaks a track: "+title);
        }
      }
      if(!IsAscii(FileText())) throw new Exception("SecretPlaylist: non-ASCII file text");
      if(Match(new NowPlaying { Title="Blackbird - Remastered 2009",Artist="The Beatles" })==null ||
         Match(new NowPlaying { Title="The Beatles - Blackbird (Official Audio)",Artist="Music channel" })==null ||
         Match(new NowPlaying { Title="Shiny - From \"Moana\"/Soundtrack Version",Artist="Jemaine Clement" })==null ||
         Match(new NowPlaying { Title="Three Little Birds",Artist="Bob Marley" })==null) throw new Exception("Favourite metadata variants not recognised.");
      if(Match(new NowPlaying { Title="Blackbird",Artist="Some cover band" })!=null ||
         Match(new NowPlaying { Title="Blackbird",Artist="" })!=null ||
         Match(new NowPlaying { Title="Blackbird tutorial",Artist="The Beatles" })!=null ||
         Match(new NowPlaying { Title="Shiny Test",Artist="Test Birds" })!=null) throw new Exception("Favourite matching accepted an unrelated track.");
      string folder=Path.Combine(Path.GetTempPath(),"Corbie-top10-"+Guid.NewGuid().ToString("N"));
      try {
        string file=Path.Combine(folder,FileName);
        if(!EnsureFile(file) || !File.Exists(file)) throw new Exception("SecretPlaylist: file not created");
        File.WriteAllText(file,"edited by Zoe");
        if(EnsureFile(file) || File.ReadAllText(file)!="edited by Zoe") throw new Exception("SecretPlaylist: existing file overwritten");
        File.WriteAllText(file,"CORBIE'S TOP TEN\r\n1. Test Song - Test Artist\r\n");
        var edited=Load(file);
        if(edited.Length!=6 || Match(new NowPlaying { Title="Test Song",Artist="Test Artist" },edited)==null ||
          Match(new NowPlaying { Title="Blackbird",Artist="The Beatles" },edited)!=null) throw new Exception("Edited favourite list was ignored.");
        string migrated=File.ReadAllText(file);
        if(!migrated.Contains(ExpansionMarker) || Load(file).Length!=6 || File.ReadAllText(file)!=migrated) throw new Exception("Favourite expansion repeated or was not saved.");
        foreach(string track in AddedFavourites) {
          int split=track.IndexOf(" - ",StringComparison.Ordinal);
          if(Match(new NowPlaying { Title=track.Substring(0,split),Artist=track.Substring(split+3) },edited)==null) throw new Exception("New favourite was not recognised: "+track);
        }
        File.WriteAllText(file,ExpansionMarker+"\r\n1. Test Song - Test Artist\r\n");
        if(Load(file).Length!=1) throw new Exception("Later user edits were overridden by the expansion.");
        File.WriteAllText(file,string.Join("\r\n",OriginalFavourites));
        if(Load(file).Length!=15 || !File.ReadAllText(file).StartsWith(OriginalFavourites[0])) throw new Exception("Original top ten was not extended without replacement.");
        if(Match(new NowPlaying { Title="Bird Song",Artist="Florence and the Machine" })==null ||
           Match(new NowPlaying { Title="Red Right Hand",Artist="Nick Cave and the Bad Seeds" })==null) throw new Exception("Artist conjunction variants were not recognised.");
      } finally {
        try {
          string file=Path.Combine(folder,FileName);
          if(File.Exists(file)) File.Delete(file);
          if(Directory.Exists(folder)) Directory.Delete(folder);
        } catch { }
      }
    }
  }
}
