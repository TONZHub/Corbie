using System;
using System.Collections.Generic;

namespace CorbiePet {
  public sealed class GameSessionEvent {
    public bool Started { get; set; }
    public bool Ended { get; set; }
    public string GameName { get; set; }
    public int AppId { get; set; }
    public int Minutes { get; set; }
    public string Remark { get; set; }
    public string JournalText { get; set; }
    public bool AwardTrinket { get; set; }
    public string TrinketBiasId { get; set; } // preferred play trinket id, or null for normal roll
  }

  // Steam session companion: settle, soft-mute casual chatter, Nest note length on quit.
  public sealed class GameSessionBuddy {
    int activeAppId;
    string activeName;
    DateTime startedUtc=DateTime.MinValue;
    double probeWait;
    bool softMute;
    bool awardedThisSession;

    public bool SoftMute { get { return softMute; } }
    public bool Active { get { return activeAppId>0; } }
    public string ActiveGame { get { return activeName; } }

    public void Reset() {
      activeAppId=0; activeName=null; startedUtc=DateTime.MinValue;
      probeWait=0; softMute=false; awardedThisSession=false;
    }

    public GameSessionEvent Step(double dt, bool enabled, bool octopus, Random rand) {
      if(!enabled) {
        if(Active) return ForceEnd(octopus, rand, false);
        softMute=false; return null;
      }
      probeWait-=dt;
      if(probeWait>0) return null;
      probeWait=3.0;

      var steam=LaunchWatcher.TryReadSteamGame();
      if(steam!=null && steam.IsPlaying) {
        if(steam.AppId==activeAppId) return null;
        // Switch or first start
        GameSessionEvent ended=null;
        if(activeAppId>0) ended=BuildEnd(octopus, rand, true);
        Begin(steam.AppId, steam.Name);
        var start=BuildStart(octopus, rand);
        if(ended!=null) {
          // Prefer announcing the new game; still return end stats via journal on start path
          start.JournalText=(ended.JournalText!=null?ended.JournalText+" / ":"")+start.JournalText;
          start.AwardTrinket=ended.AwardTrinket;
          start.TrinketBiasId=ended.TrinketBiasId;
          start.Minutes=ended.Minutes;
          start.Ended=true; // prior session closed
        }
        return start;
      }

      if(activeAppId>0) return BuildEnd(octopus, rand, true);
      softMute=false;
      return null;
    }

    void Begin(int appId, string name) {
      activeAppId=appId;
      activeName=RoostMemory.Short(name??("Steam app "+appId),80);
      startedUtc=DateTime.UtcNow;
      softMute=true;
      awardedThisSession=false;
    }

    GameSessionEvent ForceEnd(bool octopus, Random rand, bool award) {
      if(activeAppId<=0) { softMute=false; return null; }
      return BuildEnd(octopus, rand, award);
    }

    GameSessionEvent BuildStart(bool octopus, Random rand) {
      string name=activeName??"a game";
      string[] bird={
        "Settling in for "+name+". Fine. I'll shut my beak. For now.",
        name+" session open. I'll sit here quietly and judge your every move.",
        "Game night: "+name+". Nest on quiet watch. Don't embarrass us."
      };
      string[] oct={
        "Anchoring near "+name+". Chatter muted. Judgement NOT muted.",
        name+" underway. Tentacles in spectator mode. Unimpressed so far.",
        "Session locked: "+name+". Soft mute engaged. You're welcome."
      };
      var pool=octopus?oct:bird;
      return new GameSessionEvent {
        Started=true,
        GameName=name,
        AppId=activeAppId,
        Remark=pool[rand.Next(pool.Length)],
        JournalText="Started Steam session: "+name
      };
    }

    GameSessionEvent BuildEnd(bool octopus, Random rand, bool mayAward) {
      int minutes=0;
      if(startedUtc!=DateTime.MinValue)
        minutes=(int)Math.Max(0, Math.Round((DateTime.UtcNow-startedUtc).TotalMinutes));
      string name=activeName??"a game";
      int app=activeAppId;
      activeAppId=0; activeName=null; startedUtc=DateTime.MinValue; softMute=false;

      string[] bird={
        name+" closed. About "+minutes+" min on the clock. Logged. Don't lie about it.",
        "Session over - "+name+", roughly "+minutes+" minutes. Not totally rubbish.",
        name+" logged out. "+minutes+" min scratched into the Nest. Back to work, you skiving wanker."
      };
      string[] oct={
        name+" ended. "+minutes+" minutes annexed to the journal. Shameful.",
        "Session dissolved: "+name+" ("+minutes+" min). Catalogued. Judged.",
        "Game over pulse. "+name+", "+minutes+" min. Soft mute lifted. Brace yourself."
      };
      var pool=octopus?oct:bird;
      bool award=mayAward && !awardedThisSession && minutes>=20;
      string bias=null;
      if(award) {
        awardedThisSession=true;
        // Longer sessions bias rarer play loot
        if(minutes>=90) bias="play_midnight";
        else if(minutes>=45) bias="play_pixel";
        else bias="play_badge";
      }
      return new GameSessionEvent {
        Ended=true,
        GameName=name,
        AppId=app,
        Minutes=minutes,
        Remark=pool[rand.Next(pool.Length)],
        JournalText="Steam session ended: "+name+" (~"+minutes+" min)",
        AwardTrinket=award,
        TrinketBiasId=bias
      };
    }

    public static void Check() {
      var b=new GameSessionBuddy();
      if(b.SoftMute) throw new Exception("GameSession: mute on reset");
      var ev=b.Step(4,false,false,new Random(1));
      if(ev!=null) throw new Exception("GameSession: disabled fired");
      // Simulate begin/end without live Steam by reflection-free path:
      b.Begin(42,"Fixture Quest");
      if(!b.SoftMute || !b.Active) throw new Exception("GameSession: begin failed");
      var end=b.BuildEnd(false,new Random(2),true);
      // minutes may be 0 if clock didn't advance - still must clear
      if(b.Active || b.SoftMute) throw new Exception("GameSession: end did not clear");
      if(end==null || string.IsNullOrEmpty(end.JournalText) || !end.Ended)
        throw new Exception("GameSession: end payload");
      if(end.JournalText.IndexOf("Fixture Quest",StringComparison.Ordinal)<0)
        throw new Exception("GameSession: name missing");
    }
  }
}
