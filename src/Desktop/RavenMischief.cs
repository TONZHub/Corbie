using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace CorbiePet {
  public enum MischiefKind { None, Chase, HideEdge, DropFeather, FleeDialog, InspectIcon }

  public sealed class MischiefCue {
    public MischiefKind Kind { get; set; }
    public double TargetX { get; set; }
    public double TargetY { get; set; }
    public string Remark { get; set; }
  }

  // Harmless raven tricks. Geometry and class names only - never reads titles.
  public sealed class RavenMischief {
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left,Top,Right,Bottom; }

    readonly Random random=new Random();
    double cooldown=8;
    double featherTimer;
    public bool Enabled { get; set; }

    public MischiefCue Step(double dt, double petLeft, double petTop, double cursorX, double cursorY, bool allowed) {
      cooldown=Math.Max(0,cooldown-dt);
      featherTimer=Math.Max(0,featherTimer-dt);
      if(!Enabled || !allowed) return null;
      // Flee dialog-like windows near Corbie (class only).
      var flee=FindNearbyDialog(petLeft,petTop);
      if(flee!=null) {
        cooldown=Math.Max(cooldown,40);
        return flee;
      }
      if(cooldown>0) return null;
      int roll=random.Next(100);
      if(roll<28) {
        cooldown=18+random.Next(20);
        return new MischiefCue {
          Kind=MischiefKind.Chase,
          TargetX=cursorX, TargetY=cursorY,
          Remark="Cursor acquired. This is a professional chase."
        };
      }
      if(roll<48) {
        cooldown=30+random.Next(25);
        double edgeX=petLeft<400?12:System.Windows.SystemParameters.WorkArea.Width-180;
        return new MischiefCue {
          Kind=MischiefKind.HideEdge,
          TargetX=edgeX, TargetY=Math.Max(40,petTop),
          Remark="Hiding behind the window edge. You saw nothing."
        };
      }
      if(roll<68 && featherTimer<=0) {
        featherTimer=50;
        cooldown=22;
        return new MischiefCue {
          Kind=MischiefKind.DropFeather,
          TargetX=petLeft+40, TargetY=petTop+90,
          Remark="Dramatic feather drop. Critical lore event."
        };
      }
      if(roll<82) {
        cooldown=35;
        return new MischiefCue {
          Kind=MischiefKind.InspectIcon,
          TargetX=petLeft, TargetY=petTop,
          Remark="Inspecting a desktop icon with academic severity."
        };
      }
      cooldown=12;
      return null;
    }
    MischiefCue FindNearbyDialog(double petLeft, double petTop) {
      IntPtr found=IntPtr.Zero; RECT foundRect=new RECT();
      EnumWindows((h,l)=> {
        if(!IsWindowVisible(h)) return true;
        var sb=new StringBuilder(64);
        if(GetClassName(h,sb,sb.Capacity)<=0) return true;
        string cls=sb.ToString();
        // Standard dialog class and a few toast-ish shells - never GetWindowText.
        if(cls!="#32770" && cls.IndexOf("Dialog",StringComparison.OrdinalIgnoreCase)<0
           && cls.IndexOf("Bubble",StringComparison.OrdinalIgnoreCase)<0) return true;
        RECT r; if(!GetWindowRect(h,out r)) return true;
        int w=r.Right-r.Left, hh=r.Bottom-r.Top;
        if(w<120 || hh<60 || w>900 || hh>700) return true;
        double cx=(r.Left+r.Right)/2.0, cy=(r.Top+r.Bottom)/2.0;
        double dx=cx-(petLeft+80), dy=cy-(petTop+60);
        if(dx*dx+dy*dy>220*220) return true;
        found=h; foundRect=r; return false;
      },IntPtr.Zero);
      if(found==IntPtr.Zero) return null;
      double fleeX=petLeft<(foundRect.Left+foundRect.Right)/2.0 ? Math.Max(8,petLeft-140) : petLeft+140;
      return new MischiefCue {
        Kind=MischiefKind.FleeDialog,
        TargetX=fleeX, TargetY=petTop,
        Remark="Error dialog energy detected. Tactical retreat!"
      };
    }
    public static void Check() {
      var m=new RavenMischief { Enabled=false };
      if(m.Step(1,100,100,200,200,true)!=null) throw new Exception("RavenMischief: spoke while disabled");
      m.Enabled=true;
      if(m.Step(1,100,100,200,200,false)!=null) throw new Exception("RavenMischief: spoke while disallowed");
      // Force a cue attempt by stepping past initial cooldown via many ticks while allowed.
      MischiefCue cue=null;
      for(int i=0;i<80 && cue==null;i++) cue=m.Step(1,100,100,200,200,true);
      if(cue!=null && cue.Kind==MischiefKind.None) throw new Exception("RavenMischief: empty kind");
      if(string.IsNullOrEmpty(((MischiefKind)1).ToString())) throw new Exception("RavenMischief: enum missing");
    }
  }
}
