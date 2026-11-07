using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CorbiePet {
  // Slow plumage / tentacle-tip tint from shiny hoard rarities.
  public static class TrinketMolt {
    public sealed class Tint {
      public Color Feather;
      public Color TentacleTip;
      public string Summary;
      public int RareCount;
      public int LegendaryCount;
    }

    public static Tint FromHoard(IEnumerable<TrinketItem> trinkets) {
      var list=(trinkets ?? Enumerable.Empty<TrinketItem>()).Where(t=>t!=null).ToList();
      int rare=list.Count(t=>string.Equals(t.Rarity,"Rare",StringComparison.OrdinalIgnoreCase));
      int legendary=list.Count(t=>string.Equals(t.Rarity,"Legendary",StringComparison.OrdinalIgnoreCase));
      int playNight=list.Count(t=>t.Id!=null && t.Id.StartsWith("play_",StringComparison.OrdinalIgnoreCase));
      Color baseFeather=Color.FromRgb(0x25,0x24,0x3B);
      Color baseTip=Color.FromRgb(0x8F,0x79,0xBD);
      if(list.Count==0) {
        return new Tint { Feather=baseFeather, TentacleTip=baseTip, Summary="No molt yet - hoard something shiny.", RareCount=0, LegendaryCount=0 };
      }
      Color accent=ParseHex(list[0].ColorHex, Color.FromRgb(0xFF,0xD7,0x00));
      double strength=Math.Min(0.48, 0.08 + rare*0.04 + legendary*0.09 + playNight*0.035 + Math.Min(8,list.Count)*0.015);
      Color feather=Blend(baseFeather, accent, strength);
      Color tip=Blend(baseTip, accent, Math.Min(0.55, strength+0.12));
      string summary;
      if(legendary>0) summary="Legendary molt - tips drink "+(list[0].Name??"treasure")+" light.";
      else if(playNight>0) summary="Game-night molt - "+playNight+" play scrap"+(playNight==1?"":"s")+" tinting the tips.";
      else if(rare>0) summary="Rare molt warming the plumage ("+rare+" rare).";
      else summary="Common sparkle on the tips ("+list.Count+" trinkets).";
      return new Tint { Feather=feather, TentacleTip=tip, Summary=summary, RareCount=rare, LegendaryCount=legendary };
    }

    public static void Apply(Tint tint, IEnumerable<Shape> featherShapes, IEnumerable<Shape> tipShapes) {
      if(tint==null) return;
      var featherBrush=new SolidColorBrush(tint.Feather); featherBrush.Freeze();
      var tipBrush=new SolidColorBrush(tint.TentacleTip); tipBrush.Freeze();
      if(featherShapes!=null) foreach(var shape in featherShapes) if(shape!=null) shape.Fill=featherBrush;
      if(tipShapes!=null) {
        foreach(var shape in tipShapes) {
          if(shape==null) continue;
          if(shape is Path && ((Path)shape).Stroke!=null) ((Path)shape).Stroke=tipBrush;
          else shape.Fill=tipBrush;
        }
      }
    }

    public static Color Blend(Color a, Color b, double t) {
      t=Math.Max(0, Math.Min(1, t));
      byte r=(byte)(a.R+(b.R-a.R)*t);
      byte g=(byte)(a.G+(b.G-a.G)*t);
      byte bl=(byte)(a.B+(b.B-a.B)*t);
      return Color.FromRgb(r,g,bl);
    }

    public static Color ParseHex(string hex, Color fallback) {
      try {
        if(string.IsNullOrWhiteSpace(hex)) return fallback;
        object converted=ColorConverter.ConvertFromString(hex.Trim());
        if(converted is Color) return (Color)converted;
      } catch { }
      return fallback;
    }

    public static void Check() {
      var empty=FromHoard(null);
      if(empty.RareCount!=0 || empty.LegendaryCount!=0) throw new Exception("TrinketMolt: empty hoard tainted");
      var rare=new TrinketItem { Id="amber_led", Name="Glowing Amber LED", Rarity="Rare", ColorHex="#FFBF00" };
      var legend=new TrinketItem { Id="bio_pearl", Name="Bioluminescent Pearl", Rarity="Legendary", ColorHex="#7FFFD4" };
      var tint=FromHoard(new[] { rare, legend });
      if(tint.RareCount!=1 || tint.LegendaryCount!=1) throw new Exception("TrinketMolt: rarity counts wrong");
      if(tint.Feather==Color.FromRgb(0x25,0x24,0x3B)) throw new Exception("TrinketMolt: feather never shifted");
      if(tint.Summary.IndexOf("Legendary", StringComparison.OrdinalIgnoreCase)<0) throw new Exception("TrinketMolt: summary weak");
      var blended=Blend(Color.FromRgb(0,0,0), Color.FromRgb(255,255,255), 0.5);
      if(blended.R!=127) throw new Exception("TrinketMolt: blend math wrong");
      var play=new TrinketItem { Id="play_midnight", Name="Midnight Session Token", Rarity="Legendary", ColorHex="#171A21" };
      var playTint=FromHoard(new[] { play });
      if(playTint.Summary.IndexOf("Legendary", StringComparison.OrdinalIgnoreCase)<0 && playTint.Summary.IndexOf("Game-night", StringComparison.OrdinalIgnoreCase)<0)
        throw new Exception("TrinketMolt: play molt summary weak");
    }
  }
}
