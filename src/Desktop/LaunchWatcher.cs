using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace CorbiePet {
  public sealed class SteamGameInfo {
    public int AppId { get; set; }
    public string Name { get; set; }
    public bool IsPlaying { get { return AppId>0 && !string.IsNullOrEmpty(Name); } }
  }

  public sealed class LaunchCue {
    public string ProcessName { get; set; }
    public string DisplayName { get; set; }
    public bool IsSteamGame { get; set; }
    public int SteamAppId { get; set; }
    public string Remark { get; set; }
    public string JournalText { get; set; }
    public string JournalKind { get; set; }
  }

  // Notices newly started user apps (process snapshot delta). Steam game titles
  // come from RunningAppID + local appmanifest - never chat, friends, or window text.
  public sealed class LaunchWatcher {
    static readonly HashSet<string> Ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
      "Idle","System","Registry","smss","csrss","wininit","services","lsass","svchost","fontdrvhost",
      "dwm","explorer","ShellExperienceHost","StartMenuExperienceHost","SearchHost","SearchApp",
      "RuntimeBroker","ApplicationFrameHost","SystemSettings","SecurityHealthService","MsMpEng",
      "NisSrv","smartscreen","conhost","dllhost","taskhostw","sihost","ctfmon","TextInputHost",
      "LockApp","LogonUI","winlogon","WmiPrvSE","spoolsv","audiodg","CompPkgSrv","backgroundTaskHost",
      "UserOOBEBroker","WidgetService","Widgets","PhoneExperienceHost","CrossDeviceResume",
      "steamwebhelper","GameOverlayUI","SteamService","steamerrorreporter","streaming_client",
      "Corbie","powershell","pwsh","cmd","WindowsTerminal","OpenConsole","conhost",
      "Cursor","Code","devenv","msedge","chrome","firefox","brave","SearchIndexer"
    };

    HashSet<string> known;
    readonly HashSet<string> reacted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    double probeWait;
    DateTime lastReactUtc = DateTime.MinValue;
    int lastSteamAppId;
    DateTime lastSteamNoteUtc = DateTime.MinValue;

    public void Reset() {
      known=null;
      reacted.Clear();
      probeWait=0;
      lastReactUtc=DateTime.MinValue;
      lastSteamAppId=0;
      lastSteamNoteUtc=DateTime.MinValue;
    }

    public LaunchCue Step(double dt, bool enabled, bool octopus, Random rand) {
      if(!enabled) { known=null; return null; }
      probeWait-=dt;
      if(probeWait>0) return null;
      probeWait=2.4;

      HashSet<string> now = Snapshot();
      if(known==null) { known=now; PeekSteamBootstrap(); return null; }

      LaunchCue steamCue = MaybeSteamSessionNote(octopus, rand);
      var fresh = now.Where(p => !known.Contains(p) && !Ignore.Contains(p) && !IsNoisy(p)).ToList();
      known=now;

      if(steamCue!=null) return steamCue;
      if(fresh.Count==0) return null;
      if((DateTime.UtcNow-lastReactUtc).TotalSeconds<70) return null;

      string pick = PreferInteresting(fresh);
      if(string.IsNullOrEmpty(pick) || reacted.Contains(pick)) return null;
      reacted.Add(pick);
      if(reacted.Count>80) {
        foreach(var drop in reacted.Take(reacted.Count-60).ToList()) reacted.Remove(drop);
      }
      lastReactUtc=DateTime.UtcNow;

      var steam = TryReadSteamGame();
      bool steamish = steam!=null && steam.IsPlaying && !IsSteamClient(pick);
      string display = steamish ? steam.Name : ProcessTriage.GetFriendlyName(pick);
      display = RoostMemory.Short(display,80);
      bool journal=ShouldJournalLaunch(pick,steamish,rand);
      return new LaunchCue {
        ProcessName=pick,
        DisplayName=display,
        IsSteamGame=steamish,
        SteamAppId=steamish?steam.AppId:0,
        Remark=PickRemark(display, steamish, octopus, rand, pick),
        JournalKind=journal?(steamish?"steam":"launch"):null,
        JournalText=journal?(steamish?("Playing on Steam: "+display):("Opened "+display)):null
      };
    }

    // Manual tray / wake: journal whatever Steam says is running, else foreground app.
    public LaunchCue CaptureNow(bool octopus, Random rand, IntPtr foregroundHwnd) {
      var steam = TryReadSteamGame();
      if(steam!=null && steam.IsPlaying) {
        lastSteamAppId=steam.AppId;
        lastSteamNoteUtc=DateTime.UtcNow;
        string name=RoostMemory.Short(steam.Name,80);
        return new LaunchCue {
          ProcessName="steam",
          DisplayName=name,
          IsSteamGame=true,
          SteamAppId=steam.AppId,
          Remark=PickRemark(name,true,octopus,rand,"steam"),
          JournalKind="steam",
          JournalText="Playing on Steam: "+name
        };
      }
      string proc = NestMemory.ProcessNameFromWindow(foregroundHwnd);
      if(string.IsNullOrEmpty(proc) || Ignore.Contains(proc)) {
        return new LaunchCue {
          ProcessName=null,
          DisplayName=null,
          Remark=octopus?"No game on the sensors. Eight arms, zero titles.":"No game showing. Steam quiet, or the title hid from me.",
          JournalKind=null,
          JournalText=null
        };
      }
      string display=RoostMemory.Short(ProcessTriage.GetFriendlyName(proc),80);
      return new LaunchCue {
        ProcessName=proc,
        DisplayName=display,
        IsSteamGame=false,
        Remark=PickRemark(display,false,octopus,rand,proc),
        JournalKind="launch",
        JournalText="Opened "+display
      };
    }

    LaunchCue MaybeSteamSessionNote(bool octopus, Random rand) {
      var steam = TryReadSteamGame();
      if(steam==null || !steam.IsPlaying) {
        if(steam==null || steam.AppId==0) lastSteamAppId=0;
        return null;
      }
      if(steam.AppId==lastSteamAppId) return null;
      if((DateTime.UtcNow-lastSteamNoteUtc).TotalMinutes<8 && lastSteamAppId!=0) return null;
      lastSteamAppId=steam.AppId;
      lastSteamNoteUtc=DateTime.UtcNow;
      lastReactUtc=DateTime.UtcNow;
      string name=RoostMemory.Short(steam.Name,80);
      reacted.Add("steam:"+steam.AppId);
      return new LaunchCue {
        ProcessName="steam",
        DisplayName=name,
        IsSteamGame=true,
        SteamAppId=steam.AppId,
        Remark=PickRemark(name,true,octopus,rand,"steam"),
        JournalKind="steam",
        JournalText="Playing on Steam: "+name
      };
    }

    void PeekSteamBootstrap() {
      var steam=TryReadSteamGame();
      if(steam!=null && steam.IsPlaying) lastSteamAppId=steam.AppId;
    }

    static HashSet<string> Snapshot() {
      var set=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      try {
        foreach(var p in Process.GetProcesses()) {
          try {
            string n=p.ProcessName;
            if(!string.IsNullOrEmpty(n)) set.Add(n);
          } catch { }
          finally { try { p.Dispose(); } catch { } }
        }
      } catch { }
      return set;
    }

    static bool IsNoisy(string name) {
      if(string.IsNullOrEmpty(name)) return true;
      if(name.StartsWith("Corbie",StringComparison.OrdinalIgnoreCase)) return true;
      if(name.EndsWith("Host",StringComparison.OrdinalIgnoreCase)) return true;
      if(name.EndsWith("Service",StringComparison.OrdinalIgnoreCase)) return true;
      if(name.IndexOf("Update",StringComparison.OrdinalIgnoreCase)>=0) return true;
      if(name.IndexOf("Helper",StringComparison.OrdinalIgnoreCase)>=0) return true;
      if(name.IndexOf("Crash",StringComparison.OrdinalIgnoreCase)>=0) return true;
      return false;
    }

    static bool IsSteamClient(string name) {
      return string.Equals(name,"steam",StringComparison.OrdinalIgnoreCase)
          || string.Equals(name,"steamwebhelper",StringComparison.OrdinalIgnoreCase);
    }

    static string PreferInteresting(List<string> names) {
      foreach(var n in names) {
        if(n.IndexOf("Win64-Shipping",StringComparison.OrdinalIgnoreCase)>=0) return n;
        if(n.IndexOf("Game",StringComparison.OrdinalIgnoreCase)>=0) return n;
      }
      return names[0];
    }

    static string PickRemark(string display, bool steamGame, bool octopus, Random rand, string processName) {
      if(steamGame) {
        string[] steamBird = {
          "Steam lit up. "+display+". Noted. Don't pretend this is a quick one, you cheeky bastard.",
          "Oh, "+display+". Of course. Logging it before you fib about how long you played.",
          display+" on Steam. Work's been binned, then? Marvellous."
        };
        string[] steamOct = {
          "Steam pulse. "+display+". Eight arms, eight eye-rolls. Logged.",
          display+" detected. Session annexed. Don't come crying to me at 3 a.m.",
          "Game boot: "+display+". Eight arms, one opinion: you should be working."
        };
        var steamPool=octopus?steamOct:steamBird;
        return steamPool[rand.Next(steamPool.Length)];
      }
      var kind=PerchPersonality.Classify(processName??"");
      string[] bird;
      string[] oct;
      switch(kind) {
        case PerchKind.Game:
          bird=new[]{ display+" boots up. Go on then, try not to be shit at it.", "Game-shaped exe: "+display+". I'll watch. I'll judge. Loudly.", display+" entered the arena. My expectations are on the floor, next to your crumbs." };
          oct=new[]{ display+" spawned. Eight arms bracing for your dreadful aim.", "Matchmaking with "+display+". God help your poor sodding teammates.", display+" is live. Emotional-support cephalopod, unpaid, unimpressed." };
          break;
        case PerchKind.Browser:
          bird=new[]{ display+" - oh wonderful, MORE tabs. Just what this mess needed.", "Browser stir: "+display+". Forty tabs and not one of them is work. Cheeky.", display+" opened. Doomscrolling again? Put the fucking mouse DOWN." };
          oct=new[]{ display+" tab-polyps spreading like mould on bread.", "Browser bloom: "+display+". Eight arms can't close that many tabs.", display+" arrived. Seventeen tabs incoming. Pray for me." };
          break;
        case PerchKind.Code:
          bird=new[]{ display+" - right. Let's see how badly you can undercook this code.", "Code perch online: "+display+". I will NOT judge silently.", display+" opened. That last build was raw as my patience. Do better." };
          oct=new[]{ display+" IDE pulse. Eight arms ready to slap your bugs.", "Code waters: "+display+". Mind the stack traces, smart-arse.", display+" opened. I can already smell something burning." };
          break;
        case PerchKind.Art:
          bird=new[]{ display+" - art tool. Go on, Picasso. Impress me. You won't.", "Canvas energy: "+display+". Don't overwork it like last time, you fusspot.", display+" opened. The bird art critic has arrived. Brace yourself." };
          oct=new[]{ display+" ink detected. My ink is better.", "Art spawn: "+display+". That palette had better not be beige.", display+" - pixels under strict review." };
          break;
        case PerchKind.Music:
          // Music apps sometimes prompt a nudge toward his real favourites.
          if(rand.Next(2)==0) return display+" opened. "+SecretPlaylist.Tease(rand);
          bird=new[]{ display+" - music. Let's hear it. I reserve the right to have unreasonable opinions.", "Music nest: "+display+". Put something good on. I've got standards.", display+" queued. Ears, you have my sympathy." };
          oct=new[]{ display+" beat colonised the desk. The tentacles will decide.", "Audio bloom: "+display+". Eight ears, zero enthusiasm.", display+" - groove levels: tragic." };
          break;
        case PerchKind.School:
          bird=new[]{ display+" - school stuff. One small step. I'll supervise and complain.", "Academic exe: "+display+". Sit up straight. Chop chop!", display+" opened. I brought feathers and moral support. Mostly feathers." };
          oct=new[]{ display+" in academic waters. Sink or swim, sunshine.", "School pulse: "+display+". Get it done.", display+" - swim faster. The deadline's circling." };
          break;
        default:
          bird=new[]{ display+" just hatched. What the fuck is that?", "New exe on the roost: "+display+". Who the hell invited THAT?", display+" opened. I saw that. I'm judging it." };
          oct=new[]{ display+" spawned. Catalogued. Disapproved.", "Process bloom: "+display+". Nobody ordered that.", display+" arrived. Tentacles took notes. Harsh ones." };
          break;
      }
      var pool2=octopus?oct:bird;
      return pool2[rand.Next(pool2.Length)];
    }

    static bool ShouldJournalLaunch(string processName, bool steamGame, Random rand) {
      if(steamGame) return true;
      var kind=PerchPersonality.Classify(processName??"");
      if(kind==PerchKind.Game) return rand.Next(100)<70;
      if(kind==PerchKind.Code || kind==PerchKind.Art) return rand.Next(100)<35;
      if(kind==PerchKind.Other) return rand.Next(100)<18;
      return rand.Next(100)<25;
    }

    public static SteamGameInfo TryReadSteamGame() {
      try {
        using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")) {
          if(key==null) return new SteamGameInfo();
          object raw=key.GetValue("RunningAppID");
          int appId=0;
          if(raw is int) appId=(int)raw;
          else if(raw!=null) int.TryParse(raw.ToString(),out appId);
          if(appId<=0) return new SteamGameInfo();
          // Skip Steamworks redistributables helper
          if(appId==228980) return new SteamGameInfo { AppId=0 };
          string steamPath = key.GetValue("SteamPath") as string;
          string name = ResolveSteamName(steamPath, appId);
          if(string.IsNullOrEmpty(name)) name="Steam app "+appId;
          return new SteamGameInfo { AppId=appId, Name=name };
        }
      } catch {
        return new SteamGameInfo();
      }
    }

    static string ResolveSteamName(string steamPath, int appId) {
      if(string.IsNullOrEmpty(steamPath) || appId<=0) return null;
      try {
        string file=Path.Combine(steamPath.Replace('/','\\'),"steamapps","appmanifest_"+appId+".acf");
        if(!File.Exists(file)) {
          // Other library folders
          string libraries=Path.Combine(steamPath.Replace('/','\\'),"steamapps","libraryfolders.vdf");
          if(File.Exists(libraries)) {
            foreach(Match m in Regex.Matches(File.ReadAllText(libraries),@"""path""\s+""([^""]+)""")) {
              string lib=m.Groups[1].Value.Replace(@"\\",@"\");
              string alt=Path.Combine(lib,"steamapps","appmanifest_"+appId+".acf");
              if(File.Exists(alt)) { file=alt; break; }
            }
          }
        }
        if(!File.Exists(file)) return null;
        string text=File.ReadAllText(file);
        var nameMatch=Regex.Match(text,@"""name""\s+""([^""]+)""");
        if(!nameMatch.Success) return null;
        return nameMatch.Groups[1].Value.Trim();
      } catch { return null; }
    }

    public static void Check() {
      if(Ignore.Contains("svchost")==false) throw new Exception("LaunchWatcher: ignore set");
      if(IsNoisy("SomethingHost")) { /* ok */ } else throw new Exception("LaunchWatcher: host filter");
      if(IsNoisy("FooUpdateBar")==false) throw new Exception("LaunchWatcher: update filter");
      var w=new LaunchWatcher();
      var c=w.Step(3,true,false,new Random(1));
      if(c!=null) throw new Exception("LaunchWatcher: first poll must seed only");
      c=w.Step(3,false,false,new Random(1));
      if(c!=null) throw new Exception("LaunchWatcher: disabled still fired");
      // Capture with no hwnd should still return a cue object
      var cap=w.CaptureNow(false,new Random(2),IntPtr.Zero);
      if(cap==null || string.IsNullOrEmpty(cap.Remark)) throw new Exception("LaunchWatcher: capture empty");
      string remark=PickRemark("Test Game",true,false,new Random(3),"steam");
      if(remark.IndexOf("Test Game",StringComparison.Ordinal)<0) throw new Exception("LaunchWatcher: remark");
      // Manifest resolver tolerates missing Steam
      var steam=TryReadSteamGame();
      if(steam==null) throw new Exception("LaunchWatcher: steam null");
    }
  }
}
