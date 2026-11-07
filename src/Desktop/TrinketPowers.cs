using System;
using System.Collections.Generic;
using System.Linq;

namespace CorbiePet {
  public sealed class TrinketPowerMods {
    public double HopScale { get; set; }
    public bool SoftMuteTolerant { get; set; }
    public string ChronicleFlavor { get; set; }
    public string Summary { get; set; }
  }

  // Lightweight passive mods from hoard + currently worn shiny.
  public static class TrinketPowers {
    public static TrinketPowerMods From(IEnumerable<TrinketItem> trinkets, string wearingId) {
      var list=(trinkets ?? Enumerable.Empty<TrinketItem>()).Where(t=>t!=null).ToList();
      int rare=list.Count(t=>string.Equals(t.Rarity,"Rare",StringComparison.OrdinalIgnoreCase));
      int legendary=list.Count(t=>string.Equals(t.Rarity,"Legendary",StringComparison.OrdinalIgnoreCase));
      int play=list.Count(t=>t.Id!=null && t.Id.StartsWith("play_",StringComparison.OrdinalIgnoreCase));
      TrinketItem worn=null;
      if(!string.IsNullOrEmpty(wearingId))
        worn=list.FirstOrDefault(t=>string.Equals(t.Id,wearingId,StringComparison.OrdinalIgnoreCase));

      double hop=1.0 + Math.Min(0.22, rare*0.02 + legendary*0.05 + play*0.015);
      if(worn!=null) {
        if(string.Equals(worn.Rarity,"Legendary",StringComparison.OrdinalIgnoreCase)) hop+=0.08;
        else if(string.Equals(worn.Rarity,"Rare",StringComparison.OrdinalIgnoreCase)) hop+=0.04;
        else hop+=0.02;
      }

      bool tolerant=legendary>=1 || (worn!=null && string.Equals(worn.Rarity,"Legendary",StringComparison.OrdinalIgnoreCase))
        || (worn!=null && worn.Id!=null && worn.Id.StartsWith("play_",StringComparison.OrdinalIgnoreCase));

      string flavor="plain Nest ink";
      if(legendary>0) flavor="legendary glitter in the margins";
      else if(play>0) flavor="game-night scrap between the lines";
      else if(rare>0) flavor="rare sparkle on the page";
      if(worn!=null) flavor="wearing "+worn.Name+" - "+flavor;

      string summary="Hop x"+hop.ToString("0.00")+(tolerant?" - soft-mute tolerant":"")+" - chronicle: "+flavor;
      return new TrinketPowerMods {
        HopScale=hop,
        SoftMuteTolerant=tolerant,
        ChronicleFlavor=flavor,
        Summary=summary
      };
    }

    public static void Check() {
      var empty=From(null, null);
      if(Math.Abs(empty.HopScale-1.0)>0.001) throw new Exception("TrinketPowers: empty hop");
      var mods=From(new[] {
        new TrinketItem { Id="bio_pearl", Name="Bioluminescent Pearl", Rarity="Legendary", ColorHex="#7FFFD4" },
        new TrinketItem { Id="play_badge", Name="Steam Achievement Shard", Rarity="Common", ColorHex="#1B2838" }
      }, "bio_pearl");
      if(mods.HopScale<=1.05) throw new Exception("TrinketPowers: hop weak");
      if(!mods.SoftMuteTolerant) throw new Exception("TrinketPowers: tolerant");
      if(mods.ChronicleFlavor.IndexOf("Pearl",StringComparison.Ordinal)<0) throw new Exception("TrinketPowers: flavor");
    }
  }
}
