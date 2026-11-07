using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace CorbiePet {
  // Corbie's one soft spot: the Shiny Trinket Hoard (Roost tab in the Nest).
  // Dry wit elsewhere; unabashed affection for the hoard. ASCII-only strings.
  public static class TrinketJoy {
    public const string HoardTabName="Roost";
    public const double BurstCooldownSeconds=25;
    public const double FirstMeltSeconds=40;
    public const double MeltGapSeconds=75;
    public const int MaxMeltsPerVisit=2;

    static readonly string[] RavenBurst={
      "THE HOARD! Oh, oh, OH! Look at it! Every single one of them is PERFECT!",
      "My shinies! MY SHINIES! Nobody talk to me, I am having the best day of my entire life!",
      "Look at that gleam. I have excellent taste in things that fit in a beak.",
      "Oh my feathers. OH MY FUCKING FEATHERS. Is this heaven? This is heaven. It glitters!",
      "Beautiful! Stunning! Fucking GORGEOUS! I could kiss every single trinket in there. I might!",
      "You brought me to the HOARD page?! You absolute legend. I take back... some of it!",
      "Look at them sparkle. I may need a moment. For entirely dignified bird reasons.",
      "CAW! CAW CAW! Sorry. I'm fine. I'm NOT fine. It's all so SHINY!",
      "Every trinket has its place. Mostly wherever I can stare at it. Excellent system.",
      "This is the good page! My page! My happy place! Scroll slowly, I want to savour it!",
      "Is that my paperclip? It IS! Hello, gorgeous! Did you miss me? I missed YOU!"
    };
    static readonly string[] OctoBurst={
      "THE HOARD! All eight arms want to hug it at once! Form an orderly queue, arms!",
      "My shinies! Eight hearts... wait, three hearts. All three are SCREAMING with joy!",
      "Look at it glitter! This is better than the whole ocean floor! Stunning! Gorgeous!",
      "Tentacles trembling. Ink sacs clenched. It's all so SHINY I could burst!",
      "Eight arms, and every one of them wants a shiny. Finally, a sensible use of resources."
    };
    static readonly string[] RavenMelt={
      "Still here? Good. Let's just... stare at them a bit longer. Sigh. Lovely.",
      "Ahh. The shine. The sweet, sweet shine. Everything is fine in here.",
      "I'm going to pretend I didn't say anything nice. But look at them. Just look.",
      "If you close this page I will go back to screaming at you. Stay a while.",
      "Best nook in the Nest. Don't tell the other tabs. They're dead jealous."
    };
    static readonly string[] OctoMelt={
      "Eight arms, zero complaints. That never happens. It's the shinies.",
      "I am melting into a very happy puddle. Do not mop me up.",
      "Sigh. Every trinket glows a little brighter when I stare at it. Science."
    };

    sealed class DialogueBag {
      readonly List<string> remaining=new List<string>();
      string last;
      public string Next(string[] pool,Random random) {
        if(remaining.Count==0) remaining.AddRange(pool);
        int index=random.Next(remaining.Count);
        if(remaining.Count>1 && remaining[index]==last) index=(index+1)%remaining.Count;
        last=remaining[index]; remaining.RemoveAt(index); return last;
      }
    }
    static readonly DialogueBag ravenBurstBag=new DialogueBag(),octoBurstBag=new DialogueBag(),ravenMeltBag=new DialogueBag(),octoMeltBag=new DialogueBag();
    public static string Burst(bool octopus,Random random) { return (octopus?octoBurstBag:ravenBurstBag).Next(octopus?OctoBurst:RavenBurst,random); }
    public static string Melt(bool octopus,Random random) { return (octopus?octoMeltBag:ravenMeltBag).Next(octopus?OctoMelt:RavenMelt,random); }

    static bool IsAscii(string s) { foreach(char c in s) if(c>126 || (c<32 && c!='\n')) return false; return true; }
    public static void Check() {
      if(RavenBurst.Length<8 || RavenBurst.Length>12) throw new Exception("TrinketJoy: burst pool size");
      foreach(var pool in new [] { RavenBurst,OctoBurst,RavenMelt,OctoMelt })
        foreach(var line in pool) if(string.IsNullOrWhiteSpace(line) || !IsAscii(line)) throw new Exception("TrinketJoy: non-ASCII or empty line: "+line);
      var r=new Random(7);
      if(string.IsNullOrEmpty(Burst(false,r)) || string.IsNullOrEmpty(Melt(true,r))) throw new Exception("TrinketJoy: pick failed");
      foreach(var pool in new [] { RavenBurst,OctoBurst,RavenMelt,OctoMelt }) {
        var bag=new DialogueBag(); string previous=null;
        for(int cycle=0;cycle<10;cycle++) {
          var seen=new HashSet<string>();
          for(int i=0;i<pool.Length;i++) {
            string line=bag.Next(pool,r);
            if(line==previous || !seen.Add(line)) throw new Exception("TrinketJoy: repeated dialogue before pool exhausted");
            previous=line;
          }
        }
      }
    }
  }

  public partial class Pet {
    DateTime lastTrinketJoyUtc=DateTime.MinValue;
    bool onHoardTab;
    double hoardTabSeconds,nextHoardMelt;
    int hoardMelts;

    bool HoardTabSelected() {
      if(panelTabs==null || corbiePanel==null) return false;
      var tab=panelTabs.SelectedItem as TabItem;
      return tab!=null && string.Equals(Convert.ToString(tab.Header),TrinketJoy.HoardTabName,StringComparison.Ordinal);
    }
    // Called on Nest tab switch and when an existing Nest window is re-shown.
    void OnNestTabMaybeHoard() {
      bool now=HoardTabSelected();
      if(now && !onHoardTab) TrinketJoyBurst();
      if(!now) { hoardTabSeconds=0; hoardMelts=0; }
      onHoardTab=now;
    }
    void OnNestReshown() {
      onHoardTab=false; hoardTabSeconds=0; hoardMelts=0;
      OnNestTabMaybeHoard();
    }
    void TrinketJoyBurst() {
      hoardTabSeconds=0; hoardMelts=0; nextHoardMelt=TrinketJoy.FirstMeltSeconds;
      if(testing || paused || shuttingDown) return;
      if((DateTime.UtcNow-lastTrinketJoyUtc).TotalSeconds<TrinketJoy.BurstCooldownSeconds) return;
      lastTrinketJoyUtc=DateTime.UtcNow;
      try {
        StopWalking(); ResetIdle();
        hop=1.0;
        scritches.Celebrate(3.2);
        flashBadgeTimer=Math.Max(flashBadgeTimer,3.5);
        if(eye!=null) eye.Fill=ColorBrush("#FFD700");
        CreatureFoley.Play(octopusMode?FoleyKind.SuctionPop:FoleyKind.Caw,companion.Quiet);
        string line=TrinketJoy.Burst(octopusMode,random);
        if(companion.Quiet) companion.SaySilent(line); else companion.Say(line);
      } catch { }
    }
    // Ticked from the Nest refresh timer (400 ms) while the Nest is open.
    void StepTrinketJoy(double dt) {
      if(testing || corbiePanel==null) return;
      bool visible=corbiePanel.IsVisible && corbiePanel.WindowState!=WindowState.Minimized;
      if(!HoardTabSelected()) { onHoardTab=false; hoardTabSeconds=0; hoardMelts=0; return; }
      if(!visible) return;
      hoardTabSeconds+=dt;
      if(hoardMelts>=TrinketJoy.MaxMeltsPerVisit || hoardTabSeconds<nextHoardMelt) return;
      if(paused || companion.Quiet || companion.Visible) return;
      hoardMelts++; nextHoardMelt=hoardTabSeconds+TrinketJoy.MeltGapSeconds;
      try { hop=Math.Max(hop,0.6); scritches.Celebrate(2.0); companion.SaySilent(TrinketJoy.Melt(octopusMode,random)); } catch { }
    }
  }
}
