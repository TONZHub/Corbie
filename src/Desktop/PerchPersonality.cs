using System;
using System.Collections.Generic;

namespace CorbiePet {
  public enum PerchKind { Other, Music, Browser, Art, Code, School, Game }

  public sealed class PerchMood {
    public PerchKind Kind { get; set; }
    public string Label { get; set; }
    public double HopScale { get; set; }
    public string IdleRemark { get; set; }
  }

  // Distinct roost behavior by app family. Process name only - never window text.
  public static class PerchPersonality {
    static readonly string[] Music = { "spotify","musicbee","itunes","applemusic","foobar2000","vlc","groove" };
    static readonly string[] Browser = { "chrome","msedge","firefox","brave","opera","vivaldi","iexplore" };
    static readonly string[] Art = { "photoshop","illustrator","clipstudio","krita","blender","figma","aseprite","paint","mspaint","affinity" };
    static readonly string[] Code = { "code","devenv","cursor","rider","idea64","pycharm","webstorm","windsurf","notepad++","sublime_text","windowsTerminal","WindowsTerminal","wt","cmd","powershell","pwsh" };
    static readonly string[] School = { "blackboard","teams","zoom","chrome_app","WINWORD","EXCEL","POWERPNT","outlook" };
    static readonly string[] Game = { "steam","GameOverlayUI","-Win64-Shipping","UnityCrashHandler","EpicGamesLauncher","Battle.net","Origin","EaDesktop","GalaxyClient" };

    public static PerchKind Classify(string processName) {
      if(string.IsNullOrWhiteSpace(processName)) return PerchKind.Other;
      string name=processName.Trim();
      if(Match(name,Music)) return PerchKind.Music;
      if(Match(name,Art)) return PerchKind.Art;
      if(Match(name,Code)) return PerchKind.Code;
      if(Match(name,School)) return PerchKind.School;
      if(Match(name,Game)) return PerchKind.Game;
      if(Match(name,Browser)) return PerchKind.Browser;
      return PerchKind.Other;
    }
    static bool Match(string name, string[] keys) {
      foreach(var key in keys) {
        if(name.Equals(key,StringComparison.OrdinalIgnoreCase)) return true;
        if(name.IndexOf(key,StringComparison.OrdinalIgnoreCase)>=0) return true;
      }
      return false;
    }
    public static PerchMood MoodFor(string processName, bool octopus) {
      var kind=Classify(processName);
      var mood=new PerchMood { Kind=kind, HopScale=1.0, Label=kind.ToString() };
      switch(kind) {
        case PerchKind.Music:
          mood.HopScale=1.25;
          mood.IdleRemark=octopus?"The beat's colonised every arm. Your playlist, however, is bland.":"This perch has a soundtrack. Mine's better. Not telling you what it is.";
          break;
        case PerchKind.Browser:
          mood.HopScale=1.05;
          mood.IdleRemark=octopus?"Tabs bloom like polyps. Close a few, you scruffbag!":"Browser nest. Seventeen tabs? SEVENTEEN? Bloody hell.";
          break;
        case PerchKind.Art:
          mood.HopScale=0.9;
          mood.IdleRemark=octopus?"Ink and pixels. Mine would be better. Carry on, I suppose.":"Art perch. That colour choice is half-baked. Keep going, I'll tell you when it's edible.";
          break;
        case PerchKind.Code:
          mood.HopScale=0.85;
          mood.IdleRemark=octopus?"Compile vibes detected. Eight arms braced for errors.":"Coding perch. I'm the rubber duck, and this duck is FURIOUS.";
          break;
        case PerchKind.School:
          mood.HopScale=0.8;
          mood.IdleRemark=octopus?"Academic waters. Swim faster, you dozy haddock.":"School perch. That deadline is burning. Can you smell it? Shift your arse!";
          break;
        case PerchKind.Game:
          mood.HopScale=1.15;
          mood.IdleRemark=octopus?"Game waters. Eight arms and not one can fix your aim.":"Game perch. Emotional support raven, reporting. Try not to embarrass us both.";
          break;
        default:
          mood.HopScale=1.0;
          mood.IdleRemark=null;
          break;
      }
      return mood;
    }
    public static void Check() {
      if(Classify("Spotify")!=PerchKind.Music) throw new Exception("PerchPersonality: Spotify");
      if(Classify("chrome")!=PerchKind.Browser) throw new Exception("PerchPersonality: chrome");
      if(Classify("Code")!=PerchKind.Code) throw new Exception("PerchPersonality: Code");
      if(Classify("photoshop")!=PerchKind.Art) throw new Exception("PerchPersonality: photoshop");
      if(Classify("steam")!=PerchKind.Game) throw new Exception("PerchPersonality: steam");
      var mood=MoodFor("devenv",false);
      if(mood.Kind!=PerchKind.Code || string.IsNullOrEmpty(mood.IdleRemark)) throw new Exception("PerchPersonality: mood");
    }
  }
}
