using System;

namespace CorbiePet {
  public enum WakeAction {
    None, NotWoken, Break, WhatsDue, OpenNest, StartFocus, Mood, Treat, MissionStatus, NoteGame, Chronicle, Territory, MoltReport, HolidayReport, DiscoveryReport
  }

  public sealed class WakeResult {
    public WakeAction Action { get; set; }
    public string Remainder { get; set; }
    public string Reply { get; set; }
  }

  // Local wake gate: ignore mic chatter until "Corbie" is heard, then map short commands.
  public static class WakeCommand {
    public static bool MentionsName(string heard) {
      if(string.IsNullOrWhiteSpace(heard)) return false;
      return heard.IndexOf("corbie",StringComparison.OrdinalIgnoreCase)>=0
          || heard.IndexOf("corby",StringComparison.OrdinalIgnoreCase)>=0;
    }
    public static WakeResult Parse(string heard, bool wakeRequired) {
      var result=new WakeResult { Action=WakeAction.None, Remainder=heard };
      if(string.IsNullOrWhiteSpace(heard)) return result;
      string text=heard.Trim();
      if(wakeRequired && !MentionsName(text)) {
        result.Action=WakeAction.NotWoken;
        return result;
      }
      text=StripName(text);
      result.Remainder=text;
      // Advice about "school", "a break", or "my mood" is conversation, not
      // permission to invoke the similarly named quick command.
      if(SageAdvice.IsRequest(text))return result;
      if(CheckidayClient.IsRequest(text)) { result.Action=WakeAction.HolidayReport;return result; }
      if(DiscoveryStore.RequestKind(text)>0) {result.Action=WakeAction.DiscoveryReport;return result;}
      string lower=text.ToLowerInvariant();
      if(ContainsAny(lower,"start a break","take a break","begin a break","break time","start break"))
        result.Action=WakeAction.Break;
      else if(ContainsAny(lower,"what's due","whats due","what is due","due soon","school","blackboard"))
        result.Action=WakeAction.WhatsDue;
      else if(ContainsAny(lower,"open nest","open the nest","open panel","open corbie"))
        result.Action=WakeAction.OpenNest;
      else if(ContainsAny(lower,"start focus","focus sprint","start a focus","start sprint"))
        result.Action=WakeAction.StartFocus;
      else if(ContainsAny(lower,"how are you","how's your mood","how is your mood","mood","how do you feel"))
        result.Action=WakeAction.Mood;
      else if(ContainsAny(lower,"treat","feed you","have a treat"))
        result.Action=WakeAction.Treat;
      else if(ContainsAny(lower,"mission","quest","what's my mission","whats my mission"))
        result.Action=WakeAction.MissionStatus;
      else if(ContainsAny(lower,"note this game","note the game","note my game","what am i playing","what am I playing","journal this game","log this game","note the steam","steam game"))
        result.Action=WakeAction.NoteGame;
      else if(ContainsAny(lower,"chronicle","roost report","weekly report","flight log","flight chronicle"))
        result.Action=WakeAction.Chronicle;
      else if(ContainsAny(lower,"territory","roost map","where is home","home roost"))
        result.Action=WakeAction.Territory;
      else if(ContainsAny(lower,"molt","molting","plumage","feather season"))
        result.Action=WakeAction.MoltReport;
      else
        result.Action=WakeAction.None;
      return result;
    }
    static string StripName(string text) {
      text=System.Text.RegularExpressions.Regex.Replace(text,@"\bcorbie\b"," ",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
      text=System.Text.RegularExpressions.Regex.Replace(text,@"\bcorby\b"," ",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
      return text.Replace("  "," ").Trim().Trim(',','.','!','?',':',';');
    }
    static bool ContainsAny(string hay, params string[] needles) {
      foreach(var n in needles) if(hay.IndexOf(n,StringComparison.Ordinal)>=0) return true;
      return false;
    }
    public static void Check() {
      if(MentionsName("hey there") ) throw new Exception("WakeCommand: false wake");
      if(!MentionsName("Corbie, start a break")) throw new Exception("WakeCommand: miss wake");
      var r=Parse("Corbie start a break",true);
      if(r.Action!=WakeAction.Break) throw new Exception("WakeCommand: break");
      r=Parse("please start a break",true);
      if(r.Action!=WakeAction.NotWoken) throw new Exception("WakeCommand: gate");
      r=Parse("Corbie what's due",true);
      if(r.Action!=WakeAction.WhatsDue) throw new Exception("WakeCommand: due");
      r=Parse("hey Corbie open nest",true);
      if(r.Action!=WakeAction.OpenNest) throw new Exception("WakeCommand: nest");
      r=Parse("Corbie note this game",true);
      if(r.Action!=WakeAction.NoteGame) throw new Exception("WakeCommand: note game");
      r=Parse("Corbie roost report",true);
      if(r.Action!=WakeAction.Chronicle) throw new Exception("WakeCommand: chronicle");
    }
  }
}
