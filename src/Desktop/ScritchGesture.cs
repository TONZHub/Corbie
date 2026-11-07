using System;

namespace CorbiePet {
  // A small, deliberate back-and-forth stroke over the head. Cursor motion
  // alone never counts as petting, and one gesture earns one response.
  public sealed class ScritchGesture {
    double cooldown,pose,elapsed,lastX,lastY,travel,segment;
    int direction,reversals;
    bool tracking;

    public bool Engaged { get { return pose>0 || (tracking && travel>=6); } }
    public bool Celebrating { get { return pose>0; } }

    public bool Step(double dt,bool allowed,bool overHead,double x,double y) {
      cooldown=Math.Max(0,cooldown-dt);
      pose=Math.Max(0,pose-dt);
      if(!allowed || !overHead || cooldown>0) { ResetStroke(); return false; }
      if(!tracking) { Begin(x,y); return false; }
      elapsed+=dt;
      double dx=x-lastX,dy=y-lastY;
      lastX=x; lastY=y;
      if(elapsed>3 || Math.Abs(dx)>30 || Math.Abs(dy)>20) { Begin(x,y); return false; }
      if(Math.Abs(dx)<1.2) return false;
      int nextDirection=Math.Sign(dx);
      if(direction!=0 && direction!=nextDirection && segment>=8) { reversals++; segment=0; }
      direction=nextDirection;
      segment+=Math.Abs(dx);
      travel+=Math.Abs(dx);
      if(travel<42 || reversals<1) return false;
      cooldown=45; pose=2.4;
      ResetStroke();
      return true;
    }
    void Begin(double x,double y) {
      ResetStroke(); tracking=true; lastX=x; lastY=y;
    }
    void ResetStroke() { tracking=false; elapsed=travel=segment=0; direction=reversals=0; }
    public void Stop() { ResetStroke(); pose=0; }
    // Reuse the scritch happy pose (heart, wing stretch, squint) for other joys, e.g. the trinket hoard.
    public void Celebrate(double seconds) { pose=Math.Max(pose,Math.Max(0,seconds)); }

    public static bool OverHead(double x,double y,bool octopus) {
      if(octopus) return x>=56 && x<=124 && y>=52 && y<=82;
      return x>=88 && x<=131 && y>=35 && y<=66;
    }
    public static void Check() {
      var gesture=new ScritchGesture();
      if(gesture.Step(0.03,true,true,92,48)) throw new Exception("First scritch sample completed a gesture");
      if(gesture.Step(0.03,true,true,104,48)) throw new Exception("One-way cursor movement counted as petting");
      if(gesture.Step(0.03,true,true,116,49)) throw new Exception("One-way cursor movement counted as petting");
      if(gesture.Step(0.03,true,true,104,49)) throw new Exception("Short scritch completed early");
      if(!gesture.Step(0.03,true,true,92,48) || !gesture.Celebrating) throw new Exception("Back-and-forth scritch was missed");
      if(gesture.Step(0.03,true,true,104,48)) throw new Exception("Scritch cooldown failed");
      gesture.Stop();
      if(gesture.Celebrating || gesture.Engaged) throw new Exception("Scritch did not stop");
      if(!OverHead(105,50,false) || OverHead(30,140,false) || !OverHead(90,65,true))
        throw new Exception("Scritch head zones failed");
    }
  }
}
