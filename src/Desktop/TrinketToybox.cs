using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace CorbiePet {
  public enum ToyReaction { Neutral, Wear, Toss, Hide, Dislike, Attached }

  public sealed class ToyBond {
    public string TrinketId { get; set; }
    public string Reaction { get; set; }
    public DateTime GivenUtc { get; set; }
    public int Plays { get; set; }
  }

  public sealed class ToyboxState {
    public int Version { get; set; }
    public List<ToyBond> Bonds { get; set; }
    public string WearingId { get; set; }
    public ToyboxState() { Bonds=new List<ToyBond>(); }
  }

  // Give hoard shinies to Corbie. He wears, tosses, hides, dislikes, or attaches.
  public sealed class TrinketToybox {
    readonly string path;
    readonly bool testing;
    readonly Random random=new Random();
    ToyboxState state;
    public TrinketToybox(string path=null, bool testing=false) {
      this.path=path; this.testing=testing;
      state=new ToyboxState { Version=1 };
      if(!testing) Load();
    }
    public ToyboxState State { get { return state; } }
    public string WearingId { get { return state.WearingId; } }
    public void ClearWearing() { state.WearingId=null; Save(); }
    public bool TryStealWorn() {
      if(string.IsNullOrEmpty(state.WearingId)) return false;
      state.WearingId=null; Save(); return true;
    }

    public ToyBond BondFor(string trinketId) {
      if(string.IsNullOrEmpty(trinketId) || state.Bonds==null) return null;
      return state.Bonds.FirstOrDefault(b=>b!=null && string.Equals(b.TrinketId,trinketId,StringComparison.OrdinalIgnoreCase));
    }
    public ToyReaction Give(TrinketItem item) {
      if(item==null || string.IsNullOrEmpty(item.Id)) return ToyReaction.Neutral;
      var existing=BondFor(item.Id);
      ToyReaction reaction;
      if(existing!=null) {
        reaction=Parse(existing.Reaction);
        existing.Plays++;
        existing.GivenUtc=DateTime.UtcNow;
      } else {
        reaction=Roll(item);
        if(state.Bonds==null) state.Bonds=new List<ToyBond>();
        state.Bonds.Insert(0,new ToyBond { TrinketId=item.Id, Reaction=reaction.ToString(), GivenUtc=DateTime.UtcNow, Plays=1 });
        if(state.Bonds.Count>40) state.Bonds=state.Bonds.Take(40).ToList();
      }
      if(reaction==ToyReaction.Wear || reaction==ToyReaction.Attached) state.WearingId=item.Id;
      else if(string.Equals(state.WearingId,item.Id,StringComparison.OrdinalIgnoreCase) && reaction==ToyReaction.Dislike)
        state.WearingId=null;
      Save();
      return reaction;
    }
    public string Remark(TrinketItem item, ToyReaction reaction, bool octopus) {
      string name=item!=null?item.Name:"that shiny";
      switch(reaction) {
        case ToyReaction.Wear:
          return octopus?"Affixing '"+name+"' to my finest arm. I look STUNNING.":"Wearing '"+name+"'! Look at me! I'm GORGEOUS!";
        case ToyReaction.Toss:
          return octopus?"Wheee! '"+name+"' flung in a joyful arc! I'll fetch it back and love it forever!":"Tossed '"+name+"' up just to watch it sparkle on the way down! Magnificent!";
        case ToyReaction.Hide:
          return octopus?"'"+name+"' tucked into the ink for safekeeping. My precious!":"Hid '"+name+"' under the nest lining. My precious! MINE!";
        case ToyReaction.Dislike:
          return octopus?"'"+name+"' is too precious to wear today. Back to the hoard to sparkle in peace!":"'"+name+"' is far too gorgeous to risk wearing! Straight back to the hoard where it's safe. Swoon!";
        case ToyReaction.Attached:
          return octopus?"I have fallen HOPELESSLY in love with '"+name+"'. Do not touch!":"I'm besotted with '"+name+"'. Utterly smitten. This is my life now!";
        default:
          return octopus?"Inspected '"+name+"'. Every arm is tingling. Gorgeous!":"Look at '"+name+"'! Stunning! Absolutely stunning!";
      }
    }
    static ToyReaction Roll(TrinketItem item) {
      int seed=(item.Id??"").GetHashCode();
      var rng=new Random(seed ^ Environment.TickCount);
      int roll=rng.Next(100);
      string rarity=(item.Rarity??"").ToLowerInvariant();
      if(rarity=="legendary") {
        if(roll<45) return ToyReaction.Attached;
        if(roll<70) return ToyReaction.Wear;
        if(roll<85) return ToyReaction.Hide;
        return ToyReaction.Toss;
      }
      if(rarity=="rare") {
        if(roll<25) return ToyReaction.Wear;
        if(roll<45) return ToyReaction.Attached;
        if(roll<65) return ToyReaction.Toss;
        if(roll<80) return ToyReaction.Hide;
        if(roll<90) return ToyReaction.Dislike;
        return ToyReaction.Neutral;
      }
      if(roll<15) return ToyReaction.Wear;
      if(roll<35) return ToyReaction.Toss;
      if(roll<50) return ToyReaction.Hide;
      if(roll<65) return ToyReaction.Dislike;
      if(roll<75) return ToyReaction.Attached;
      return ToyReaction.Neutral;
    }
    static ToyReaction Parse(string value) {
      ToyReaction parsed;
      if(Enum.TryParse(value??"",true,out parsed)) return parsed;
      return ToyReaction.Neutral;
    }
    void Load() {
      try {
        if(string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        if(new FileInfo(path).Length>128*1024) return;
        var loaded=new JavaScriptSerializer().Deserialize<ToyboxState>(File.ReadAllText(path));
        if(loaded!=null) { state=loaded; if(state.Bonds==null) state.Bonds=new List<ToyBond>(); }
      } catch { }
    }
    void Save() {
      if(testing || string.IsNullOrEmpty(path)) return;
      try {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp=path+".tmp";
        File.WriteAllText(temp, new JavaScriptSerializer().Serialize(state));
        if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
      } catch { }
    }
    public static void Check() {
      var box=new TrinketToybox(null,true);
      var item=new TrinketItem { Id="paperclip", Name="Bent Paperclip", Rarity="Common", ColorHex="#C0C0C0" };
      var r=box.Give(item);
      if(box.BondFor("paperclip")==null) throw new Exception("TrinketToybox: bond missing");
      string remark=box.Remark(item,r,false);
      if(string.IsNullOrEmpty(remark) || remark.IndexOf("Paperclip",StringComparison.OrdinalIgnoreCase)<0)
        throw new Exception("TrinketToybox: remark");
    }
  }
}
