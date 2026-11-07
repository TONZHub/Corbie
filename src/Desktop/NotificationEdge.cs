using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace CorbiePet {
  // Detects toast / badge windows by class name and geometry only.
  // Never reads window titles, notification text, or toast payloads.
  public sealed class NotificationSignal {
    public bool Found;
    public double TargetX;
    public double TargetY;
    public string ClassHint;
  }

  public sealed class NotificationEdge {
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);
    [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left, Top, Right, Bottom; }

    static readonly string[] ToastClassHints = {
      "Windows.UI.Core.CoreWindow",
      "Windows.UI.Notifications",
      "ToastWindow",
      "NotificationCenterWindow",
      "Xaml_WindowedPopupClass"
    };

    double cooldown;
    double huntTime;
    double huntFromX, huntFromY, huntToX, huntToY;
    public bool HuntActive { get { return huntTime>0; } }

    public void Tick(double dt) {
      if(dt<=0) return;
      cooldown=Math.Max(0,cooldown-dt);
      if(huntTime>0) huntTime=Math.Max(0,huntTime-dt);
    }

    public NotificationSignal Probe(double workLeft, double workTop, double workRight, double workBottom, Random rand) {
      var hits=new List<NotificationSignal>();
      EnumWindows((hWnd, lParam)=> {
        if(!IsWindowVisible(hWnd)) return true;
        var name=new StringBuilder(256);
        if(GetClassName(hWnd,name,name.Capacity)<=0) return true;
        string cls=name.ToString();
        if(!LooksLikeToastClass(cls)) return true;
        NativeRect rect;
        if(!GetWindowRect(hWnd,out rect)) return true;
        int width=rect.Right-rect.Left, height=rect.Bottom-rect.Top;
        if(width<80 || height<40 || width>720 || height>520) return true;
        // Toast / badge zone: near the bottom edge of the work area (taskbar-adjacent).
        double midY=(rect.Top+rect.Bottom)*0.5;
        if(midY < workBottom-280 || midY > workBottom+80) return true;
        if(rect.Right < workLeft-40 || rect.Left > workRight+40) return true;
        double targetX=Math.Max(workLeft, Math.Min(rect.Left-20.0, workRight-180));
        double targetY=Math.Max(workTop, Math.Min(workBottom-160, workBottom-HeightBias(height)));
        hits.Add(new NotificationSignal { Found=true, TargetX=targetX, TargetY=targetY, ClassHint=ShortClass(cls) });
        return true;
      }, IntPtr.Zero);
      if(hits.Count==0) return new NotificationSignal();
      if(rand==null) rand=new Random();
      return hits[rand.Next(hits.Count)];
    }

    static double HeightBias(int height) { return Math.Min(200, 120+height*0.15); }

    public bool TryBeginHunt(NotificationSignal signal, double fromX, double fromY, double cooldownSeconds) {
      if(signal==null || !signal.Found || HuntActive || cooldown>0) return false;
      huntFromX=fromX; huntFromY=fromY;
      huntToX=signal.TargetX; huntToY=signal.TargetY;
      huntTime=2.4;
      cooldown=Math.Max(20, cooldownSeconds);
      return true;
    }

    public bool StepHunt(double dt, out double x, out double y, out int direction) {
      x=huntFromX; y=huntFromY; direction=1;
      if(huntTime<=0) return false;
      double total=2.4;
      double progress=1.0-(huntTime/total);
      double ease=progress*progress*(3-2*progress);
      x=huntFromX+(huntToX-huntFromX)*ease;
      y=huntFromY+(huntToY-huntFromY)*ease;
      direction=huntToX>=huntFromX?1:-1;
      return true;
    }

    public static bool LooksLikeToastClass(string cls) {
      if(string.IsNullOrEmpty(cls)) return false;
      foreach(var hint in ToastClassHints) {
        if(cls.IndexOf(hint, StringComparison.OrdinalIgnoreCase)>=0) return true;
      }
      if(cls.IndexOf("Toast", StringComparison.OrdinalIgnoreCase)>=0) return true;
      if(cls.IndexOf("Notification", StringComparison.OrdinalIgnoreCase)>=0 &&
         cls.IndexOf("Tray", StringComparison.OrdinalIgnoreCase)<0) return true;
      return false;
    }

    static string ShortClass(string cls) {
      if(string.IsNullOrEmpty(cls)) return "edge";
      if(cls.Length<=28) return cls;
      return cls.Substring(0,25)+"...";
    }

    public static void Check() {
      if(!LooksLikeToastClass("Windows.UI.Core.CoreWindow")) throw new Exception("NotificationEdge: core window miss");
      if(!LooksLikeToastClass("Xaml_WindowedPopupClass")) throw new Exception("NotificationEdge: popup miss");
      if(LooksLikeToastClass("Shell_TrayWnd")) throw new Exception("NotificationEdge: tray false positive");
      if(LooksLikeToastClass("Chrome_WidgetWin_1")) throw new Exception("NotificationEdge: chrome false positive");
      var edge=new NotificationEdge();
      var sig=new NotificationSignal { Found=true, TargetX=400, TargetY=700 };
      if(!edge.TryBeginHunt(sig, 100, 500, 30)) throw new Exception("NotificationEdge: hunt refused");
      if(edge.TryBeginHunt(sig, 100, 500, 30)) throw new Exception("NotificationEdge: hunt ignored cooldown");
      double x,y; int dir;
      if(!edge.StepHunt(0.1, out x, out y, out dir)) throw new Exception("NotificationEdge: hunt step failed");
      if(dir!=1) throw new Exception("NotificationEdge: hunt direction wrong");
      for(int i=0;i<40;i++) edge.Tick(0.1);
      if(edge.HuntActive) throw new Exception("NotificationEdge: hunt never ended");
    }
  }
}
