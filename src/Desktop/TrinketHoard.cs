using System;
using System.Collections.Generic;
using System.Linq;

namespace CorbiePet {
  public sealed class TrinketItem {
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Rarity { get; set; } // "Common", "Rare", "Legendary"
    public string ColorHex { get; set; }
    public DateTime AcquiredUtc { get; set; }
    public override string ToString() {
      return "[" + Rarity + "] " + Name + " · " + Description;
    }
  }

  public static class TrinketCatalog {
    public static readonly List<TrinketItem> MasterPool = new List<TrinketItem> {
      new TrinketItem { Id="paperclip", Name="Bent Paperclip", Rarity="Common", ColorHex="#C0C0C0", Description="A classic of desktop scavenging. Slightly chewed on the outer bend." },
      new TrinketItem { Id="amber_led", Name="Glowing Amber LED", Rarity="Rare", ColorHex="#FFBF00", Description="Pried from a discarded router. It still retains an ethereal warm gleam." },
      new TrinketItem { Id="ram_pin", Name="DDR3 Gold Contact Pin", Rarity="Rare", ColorHex="#FFD700", Description="Pure gold plating on copper. Corbie insists this constitutes serious wealth." },
      new TrinketItem { Id="esc_key", Name="Broken 'Esc' Keycap", Rarity="Common", ColorHex="#3A3A3A", Description="Now there is truly no escape from your deadlines." },
      new TrinketItem { Id="sea_glass", Name="Tumbled Sea Glass", Rarity="Rare", ColorHex="#48D1CC", Description="Smoothed by ocean tides. Cool to the touch and slightly translucent." },
      new TrinketItem { Id="silver_foil", Name="Crushed Silver Foil", Rarity="Common", ColorHex="#E6E6FA", Description="Lightweight, crinkly, and 100% certified corvid treasure." },
      new TrinketItem { Id="brass_nut", Name="Miniature Brass Hex Nut", Rarity="Common", ColorHex="#D4AF37", Description="Precision-threaded scrap metal. Fits snugly inside a raven's talon." },
      new TrinketItem { Id="usb_plug", Name="Rubber USB-A Dust Cap", Rarity="Common", ColorHex="#1E90FF", Description="Liberated from a dusty front panel port you haven't plugged into since 2021." },
      new TrinketItem { Id="purple_d20", Name="Translucent Purple D20", Rarity="Rare", ColorHex="#9932CC", Description="Rolled a natural 20 on scavenging. Natural 1 on your attention span." },
      new TrinketItem { Id="holo_sticker", Name="Hologram License Scrap", Rarity="Rare", ColorHex="#00FA9A", Description="Authentic 2004 software certificate. Shimmers with vintage elegance." },
      new TrinketItem { Id="coil_spring", Name="Tiny Copper Spring", Rarity="Common", ColorHex="#B87333", Description="Bounces erratically when pecked. An exquisite distraction." },
      new TrinketItem { Id="obsidian_shard", Name="Obsidian Beak Shard", Rarity="Legendary", ColorHex="#1A1829", Description="Pitch-black volcanic glass. Brooding, razor-sharp, and aristocratic." },
      new TrinketItem { Id="bio_pearl", Name="Bioluminescent Pearl", Rarity="Legendary", ColorHex="#7FFFD4", Description="Prized cephalopod artifact. Glows with eerie oceanic luminescence." },
      new TrinketItem { Id="floppy_spring", Name="Floppy Shutter Spring", Rarity="Common", ColorHex="#708090", Description="Archaeological relic from the ancient 3.5-inch magnetic era." },
      new TrinketItem { Id="gold_bit", Name="Golden Screwdriver Bit", Rarity="Rare", ColorHex="#DAA520", Description="Phillips #1. An exquisite implement of mechanical conquest." },
      new TrinketItem { Id="sapphire_resistor", Name="Sapphire Precision Resistor", Rarity="Legendary", ColorHex="#4169E1", Description="Color-coded bands gleaming like royal gemstones. Resistance is futile." },
      new TrinketItem { Id="play_badge", Name="Steam Achievement Shard", Rarity="Common", ColorHex="#1B2838", Description="Chipped off a long game night. Still faintly warm from the GPU." },
      new TrinketItem { Id="play_pixel", Name="Loot Pixel", Rarity="Rare", ColorHex="#66C0F4", Description="One stubborn glowing pixel from a victory screen. Hoarded on principle." },
      new TrinketItem { Id="play_stick", Name="Thumbstick Cap", Rarity="Rare", ColorHex="#2A475E", Description="Fallen from a controller during a clutch moment. Slightly sticky." },
      new TrinketItem { Id="play_midnight", Name="Midnight Session Token", Rarity="Legendary", ColorHex="#171A21", Description="Proof of a deep game night. The Nest remembers. Your sleep schedule does too." },
      new TrinketItem { Id="play_ship", Name="Build Confetti Flake", Rarity="Rare", ColorHex="#FFD700", Description="Shed during a ship ritual. Smells faintly of compile success." }
    };

    public static TrinketItem RollTrinket(IEnumerable<string> existingIds, Random rand) {
      if (rand == null) rand = new Random();
      var owned = new HashSet<string>(existingIds ?? Enumerable.Empty<string>());

      // Filter unowned first
      var unowned = MasterPool.Where(x => !owned.Contains(x.Id)).ToList();
      var candidatePool = unowned.Count > 0 ? unowned : MasterPool;

      // Weighted by rarity: Common (60), Rare (30), Legendary (10)
      int roll = rand.Next(100);
      string targetRarity;
      if (roll < 10) targetRarity = "Legendary";
      else if (roll < 40) targetRarity = "Rare";
      else targetRarity = "Common";

      var rarityMatches = candidatePool.Where(x => x.Rarity == targetRarity).ToList();
      TrinketItem picked = null;
      if (rarityMatches.Count > 0) {
        picked = rarityMatches[rand.Next(rarityMatches.Count)];
      } else {
        picked = candidatePool[rand.Next(candidatePool.Count)];
      }

      return new TrinketItem {
        Id = picked.Id,
        Name = picked.Name,
        Description = picked.Description,
        Rarity = picked.Rarity,
        ColorHex = picked.ColorHex,
        AcquiredUtc = DateTime.UtcNow
      };
    }

    public static string InspectRemark(TrinketItem item, bool octopusMode) {
      if (item == null) return "My hoard is EMPTY. Not one shiny. My heart is in pieces. Bring me something that gleams, PLEASE!";
      if (octopusMode) {
        return "Behold my oceanic prize: '" + item.Name + "' (" + item.Rarity + ")! " + item.Description + " Isn't it GORGEOUS?!";
      }
      return "Admire this: '" + item.Name + "' (" + item.Rarity + "). " + item.Description + " Look at it! LOOK at it! Stunning! You cannot have it.";
    }


    public static TrinketItem RollPlayTrinket(IEnumerable<string> existingIds, Random rand, string biasId) {
      if (rand == null) rand = new Random();
      var owned = new HashSet<string>(existingIds ?? Enumerable.Empty<string>());
      if (!string.IsNullOrEmpty(biasId)) {
        var bias = MasterPool.FirstOrDefault(x => x.Id == biasId);
        if (bias != null && !owned.Contains(bias.Id) && rand.Next(100) < 72) {
          return new TrinketItem {
            Id=bias.Id, Name=bias.Name, Description=bias.Description, Rarity=bias.Rarity,
            ColorHex=bias.ColorHex, AcquiredUtc=DateTime.UtcNow
          };
        }
      }
      var playPool = MasterPool.Where(x => x.Id != null && x.Id.StartsWith("play_")).ToList();
      var unowned = playPool.Where(x => !owned.Contains(x.Id)).ToList();
      var pool = unowned.Count > 0 ? unowned : (playPool.Count > 0 ? playPool : MasterPool);
      var picked = pool[rand.Next(pool.Count)];
      return new TrinketItem {
        Id=picked.Id, Name=picked.Name, Description=picked.Description, Rarity=picked.Rarity,
        ColorHex=picked.ColorHex, AcquiredUtc=DateTime.UtcNow
      };
    }

    public static void SelfCheck() {
      if (MasterPool.Count < 20) throw new Exception("TrinketCatalog: Master pool incomplete");
      var rand = new Random(42);
      var item1 = RollTrinket(new string[0], rand);
      if (item1 == null || string.IsNullOrEmpty(item1.Name)) throw new Exception("TrinketCatalog: Roll failed");
      var owned = new HashSet<string>(MasterPool.Select(x => x.Id));
      var duplicateRoll = RollTrinket(owned, rand);
      if (duplicateRoll == null) throw new Exception("TrinketCatalog: Roll should fall back gracefully when all owned");
      var remark = InspectRemark(item1, false);
      if (!remark.Contains(item1.Name)) throw new Exception("TrinketCatalog: Remark missing item name");
      var play = RollPlayTrinket(new string[0], rand, "play_badge");
      if (play == null || play.Id == null || !play.Id.StartsWith("play_")) throw new Exception("TrinketCatalog: play roll failed");
    }
  }
}
