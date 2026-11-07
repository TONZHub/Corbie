using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CorbiePet {
  public enum ShimejiChange { None, Arrived, Multiplied, Departed }

  public sealed class ShimejiObservation {
    public bool Present;
    public int ProcessCount;
    public int VisibleSprites;
    public bool HasTarget;
    public double TargetX;
    public double TargetY;
    public long TargetId;
    public ShimejiChange Change;
  }

  // Process identity and window geometry only. No titles, screenshots, files,
  // command lines, or Shimeji internals are read.
  public sealed class ShimejiWatcher {
    delegate bool EnumWindowsProc(IntPtr hWnd,IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left,Top,Right,Bottom; }
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc callback,IntPtr lParam);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd,out NativeRect rect);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd,out uint processId);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr hWnd,System.Text.StringBuilder className,int maxCount);

    bool known;
    int previousCount;
    IntPtr targetWindow;
    uint targetPid;

    // Discovery scans stay infrequent. Between scans, sample only the known
    // mascot window so motion can reach the brain without rescanning processes.
    public bool SampleTarget(out double x,out double y,out long identity) {
      x=y=0; identity=0;
      if(targetWindow==IntPtr.Zero) return false;
      uint pid; NativeRect rect;
      GetWindowThreadProcessId(targetWindow,out pid);
      if(pid!=targetPid || !IsWindowVisible(targetWindow) || !GetWindowRect(targetWindow,out rect)) { targetWindow=IntPtr.Zero; return false; }
      int width=rect.Right-rect.Left,height=rect.Bottom-rect.Top;
      if(width<8 || height<8 || width>700 || height>700) return false;
      x=(rect.Left+rect.Right)/2.0; y=(rect.Top+rect.Bottom)/2.0; identity=targetWindow.ToInt64(); return true;
    }

    public static bool IsShimejiProcessName(string value) {
      string name=(value??"").Trim().ToLowerInvariant();
      if(name.EndsWith(".exe")) name=name.Substring(0,name.Length-4);
      name=name.Replace("-","").Replace("_","").Replace(" ","");
      return name.StartsWith("shimeji",StringComparison.Ordinal);
    }

    public static bool IsJavaProcessName(string value) {
      string name=(value??"").Trim().ToLowerInvariant();
      if(name.EndsWith(".exe")) name=name.Substring(0,name.Length-4);
      return name=="java" || name=="javaw";
    }

    public static bool IsJavaMascotWindow(string processName,string className,int width,int height) {
      if(!IsJavaProcessName(processName) || width<8 || height<8 || width>700 || height>700) return false;
      string kind=(className??"").Trim();
      return kind.StartsWith("SunAwt",StringComparison.OrdinalIgnoreCase) ||
        kind.IndexOf("AwtWindow",StringComparison.OrdinalIgnoreCase)>=0;
    }

    public ShimejiObservation Poll(double fromX,double fromY) {
      var namedPids=new HashSet<uint>();
      var javaPids=new HashSet<uint>();
      foreach(var process in Process.GetProcesses()) {
        try {
          if(IsShimejiProcessName(process.ProcessName)) namedPids.Add((uint)process.Id);
          else if(IsJavaProcessName(process.ProcessName)) javaPids.Add((uint)process.Id);
        } catch { }
        finally { process.Dispose(); }
      }

      int sprites=0; bool target=false; double targetX=0,targetY=0,best=double.MaxValue;
      IntPtr nearest=IntPtr.Zero; uint nearestPid=0;
      var matchedPids=new HashSet<uint>(namedPids);
      if(namedPids.Count>0 || javaPids.Count>0) {
        EnumWindows((window,lParam)=> {
          try {
            uint pid; GetWindowThreadProcessId(window,out pid);
            NativeRect rect;
            bool named=namedPids.Contains(pid),java=javaPids.Contains(pid);
            if((!named && !java) || !IsWindowVisible(window) || !GetWindowRect(window,out rect)) return true;
            int width=rect.Right-rect.Left,height=rect.Bottom-rect.Top;
            if(width<8 || height<8 || width>700 || height>700) return true;
            if(java) {
              var className=new System.Text.StringBuilder(128);
              GetClassName(window,className,className.Capacity);
              if(!IsJavaMascotWindow("javaw",className.ToString(),width,height)) return true;
            }
            matchedPids.Add(pid);
            sprites++;
            double x=(rect.Left+rect.Right)/2.0,y=(rect.Top+rect.Bottom)/2.0;
            double distance=(x-fromX)*(x-fromX)+(y-fromY)*(y-fromY);
            if(distance<best) { best=distance; targetX=x; targetY=y; target=true; nearest=window; nearestPid=pid; }
          } catch { }
          return true;
        },IntPtr.Zero);
      }
      int processes=matchedPids.Count;
      targetWindow=nearest; targetPid=nearestPid;
      var observed=Observe(processes>0,processes,sprites,target,targetX,targetY); observed.TargetId=nearest.ToInt64(); return observed;
    }

    public ShimejiObservation Observe(bool present,int processes,int sprites,bool target=false,double targetX=0,double targetY=0) {
      int count=Math.Max(processes,sprites); ShimejiChange change=ShimejiChange.None;
      if(present && !known) change=ShimejiChange.Arrived;
      else if(present && known && count>previousCount) change=ShimejiChange.Multiplied;
      else if(!present && known) change=ShimejiChange.Departed;
      known=present; previousCount=present?Math.Max(1,count):0;
      return new ShimejiObservation { Present=present,ProcessCount=processes,VisibleSprites=sprites,HasTarget=target,TargetX=targetX,TargetY=targetY,Change=change };
    }

    public void Reset() { known=false; previousCount=0; targetWindow=IntPtr.Zero; targetPid=0; }

    public static void Check() {
      if(!IsShimejiProcessName("shimeji.exe") || !IsShimejiProcessName("Shimeji-ee") || IsShimejiProcessName("javaw"))
        throw new Exception("Shimeji process-name detection failed");
      if(!IsJavaMascotWindow("javaw","SunAwtWindow",128,128) ||
        IsJavaMascotWindow("javaw","SunAwtFrame",900,700) ||
        IsJavaMascotWindow("notepad","SunAwtWindow",128,128))
        throw new Exception("Java Shimeji window detection failed");
      var watcher=new ShimejiWatcher();
      if(watcher.Observe(false,0,0).Change!=ShimejiChange.None) throw new Exception("Absent Shimeji produced an event");
      var arrived=watcher.Observe(true,1,1,true,40,50);
      if(arrived.Change!=ShimejiChange.Arrived || !arrived.HasTarget) throw new Exception("Shimeji arrival failed");
      if(watcher.Observe(true,1,2).Change!=ShimejiChange.Multiplied) throw new Exception("Shimeji multiplication failed");
      if(watcher.Observe(true,1,2).Change!=ShimejiChange.None) throw new Exception("Stable Shimeji repeated an event");
      if(watcher.Observe(false,0,0).Change!=ShimejiChange.Departed) throw new Exception("Shimeji departure failed");
    }
  }
}
