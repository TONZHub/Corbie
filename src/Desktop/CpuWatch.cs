using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CorbiePet {
  public sealed class CulpritInfo {
    public int Pid;
    public string Name;
    public double EstimatedPercent;
    public bool IsPerched;
  }

  public static class ProcessTriage {
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    public static string GetFriendlyName(string processName) {
      if(string.IsNullOrEmpty(processName)) return "Unknown Task";
      string lower=processName.ToLowerInvariant();
      switch(lower) {
        case "code": return "VS Code";
        case "devenv": return "Visual Studio";
        case "chrome": return "Google Chrome";
        case "msedge": return "Microsoft Edge";
        case "firefox": return "Firefox";
        case "brave": return "Brave Browser";
        case "node": return "Node.js";
        case "python":
        case "python3":
        case "pythonw": return "Python";
        case "dotnet": return ".NET runtime";
        case "cargo":
        case "rustc": return "Rust compiler";
        case "javac":
        case "java": return "Java";
        case "blender": return "Blender";
        case "unity":
        case "unityeditor": return "Unity";
        case "slack": return "Slack";
        case "discord": return "Discord";
        case "spotify": return "Spotify";
        case "explorer": return "Windows Explorer";
        case "devswarm": return "DevSwarm";
        case "corbie": return "Corbie";
        default:
          if(processName.Length>1) return char.ToUpperInvariant(processName[0])+processName.Substring(1);
          return processName;
      }
    }

    public static CulpritInfo FindCulprit(IntPtr perchWindow, int sampleMs = 100) {
      uint perchedPid=0;
      if(perchWindow!=IntPtr.Zero) {
        GetWindowThreadProcessId(perchWindow, out perchedPid);
      }
      try {
        var startSnap = new Dictionary<int, TimeSpan>();
        var procList = Process.GetProcesses();
        foreach(var p in procList) {
          try { startSnap[p.Id] = p.TotalProcessorTime; }
          catch { }
          finally { p.Dispose(); }
        }
        System.Threading.Thread.Sleep(sampleMs);
        int topPid = -1;
        double maxDeltaMs = -1;
        string topProcName = null;
        var endList = Process.GetProcesses();
        foreach(var p in endList) {
          try {
            TimeSpan prev;
            if(startSnap.TryGetValue(p.Id, out prev)) {
              double deltaMs = (p.TotalProcessorTime - prev).TotalMilliseconds;
              if(deltaMs > maxDeltaMs) {
                maxDeltaMs = deltaMs;
                topPid = p.Id;
                topProcName = p.ProcessName;
              }
            }
          }
          catch { }
          finally { p.Dispose(); }
        }
        if(topPid > 0 && maxDeltaMs > 10) {
          double estPercent = Math.Min(100.0, (maxDeltaMs / (sampleMs * Environment.ProcessorCount)) * 100.0);
          bool isPerched = (perchedPid != 0 && topPid == (int)perchedPid);
          return new CulpritInfo {
            Pid = topPid,
            Name = GetFriendlyName(topProcName),
            EstimatedPercent = estPercent,
            IsPerched = isPerched
          };
        }
      } catch { }
      return null;
    }

    public static void Check() {
      if(GetFriendlyName("devenv")!="Visual Studio") throw new Exception("devenv mapping failed");
      if(GetFriendlyName("code")!="VS Code") throw new Exception("code mapping failed");
      if(GetFriendlyName("chrome")!="Google Chrome") throw new Exception("chrome mapping failed");
      if(GetFriendlyName("node")!="Node.js") throw new Exception("node mapping failed");
      if(GetFriendlyName("customtool")!="Customtool") throw new Exception("custom tool capitalization failed");
      var info=new CulpritInfo { Pid=1234, Name="VS Code", EstimatedPercent=92.5, IsPerched=true };
      if(info.Pid!=1234 || info.Name!="VS Code" || !info.IsPerched) throw new Exception("CulpritInfo failed");
      uint pid;
      GetWindowThreadProcessId(IntPtr.Zero, out pid);
    }
  }

  // Workload reminders, not temperature or fan-speed measurements.
  public sealed class CpuTrend {
    double highSeconds,lowSeconds,elapsed,lastNotice=-1000;
    bool warned;
    public void Reset() { highSeconds=lowSeconds=elapsed=0; lastNotice=-1000; warned=false; }
    public string Observe(double percent,double seconds,bool notify) {
      return Observe(percent,seconds,notify,null);
    }
    public string Observe(double percent,double seconds,bool notify,CulpritInfo culprit) {
      if(seconds<=0 || seconds>5 || double.IsNaN(percent)) { highSeconds=lowSeconds=0; return null; }
      elapsed+=seconds;
      highSeconds=percent>=80?highSeconds+seconds:0;
      lowSeconds=percent<=35?lowSeconds+seconds:0;
      if(!warned && highSeconds>=45 && notify && elapsed-lastNotice>=600) {
        warned=true; lastNotice=elapsed;
        if(culprit!=null) {
          if(culprit.IsPerched) {
            return "Hey! "+culprit.Name+" is devouring your CPU right beneath my claws! Boost your fans before this window melts my perch.";
          } else {
            return "Your CPU has been sweating above 80% for 45s. My perch feels warm, but it's not this window—'"+culprit.Name+"' is runaway in the background!";
          }
        }
        return "Your CPU has been sweating above 80% for 45 seconds. Boost your fans before your processor decides to unionize. (I can't read your current fan mode.)";
      }
      if(warned && lowSeconds>=120) {
        warned=false;
        if(notify) return "CPU load has dropped below 35% for two minutes. You can probably turn the jet turbines down to a quieter mode now.";
      }
      return null;
    }
    public static void Check() {
      var trend=new CpuTrend();
      for(int i=0;i<10;i++) if(trend.Observe(95,2,true)!=null) throw new Exception("Brief CPU spike triggered a reminder");
      trend.Observe(20,2,true);
      for(int i=0;i<22;i++) if(trend.Observe(90,2,true)!=null) throw new Exception("CPU reminder was early");
      if(trend.Observe(90,2,true)==null) throw new Exception("Sustained CPU load was missed");
      for(int i=0;i<400;i++) if(trend.Observe(90,2,true)!=null) throw new Exception("CPU reminder repeated during same workload");
      for(int i=0;i<59;i++) if(trend.Observe(20,2,true)!=null) throw new Exception("Settled-load reminder was early");
      if(trend.Observe(20,2,true)==null) throw new Exception("Settled workload was missed");
      trend.Reset(); for(int i=0;i<30;i++) if(trend.Observe(95,2,false)!=null) throw new Exception("Quiet mode produced a CPU reminder");
      if(trend.Observe(95,2,true)==null) throw new Exception("CPU reminder did not resume");
      trend.Reset(); trend.Observe(95,40,true); if(trend.Observe(95,2,true)!=null) throw new Exception("Sleep gap counted as sustained load");
      trend.Reset(); for(int i=0;i<23;i++) trend.Observe(90,2,true);
      for(int i=0;i<60;i++) trend.Observe(20,2,true);
      for(int i=0;i<23;i++) if(trend.Observe(90,2,true)!=null) throw new Exception("CPU reminder cooldown was ignored");

      // Test culprit awareness
      trend.Reset();
      for(int i=0;i<22;i++) trend.Observe(90,2,true);
      var perchedCulprit=new CulpritInfo { Pid=100, Name="VS Code", IsPerched=true };
      var perchedNotice=trend.Observe(90,2,true,perchedCulprit);
      if(perchedNotice==null || !perchedNotice.Contains("VS Code") || !perchedNotice.Contains("beneath my claws")) throw new Exception("Perched culprit reminder failed");

      trend.Reset();
      for(int i=0;i<22;i++) trend.Observe(90,2,true);
      var bgCulprit=new CulpritInfo { Pid=200, Name="Node.js", IsPerched=false };
      var bgNotice=trend.Observe(90,2,true,bgCulprit);
      if(bgNotice==null || !bgNotice.Contains("Node.js") || !bgNotice.Contains("background")) throw new Exception("Background culprit reminder failed");

      ProcessTriage.Check();
    }
  }
  public sealed class CpuWatch {
    [StructLayout(LayoutKind.Sequential)] struct Stamp { public uint Low,High; public ulong Value { get { return ((ulong)High<<32)|Low; } } }
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetSystemTimes(out Stamp idle,out Stamp kernel,out Stamp user);
    readonly Stopwatch clock=Stopwatch.StartNew();
    readonly CpuTrend trend=new CpuTrend();
    ulong idleBefore,totalBefore;
    double before;
    bool primed;
    public double? Percent { get; private set; }
    public CulpritInfo LastCulprit { get; private set; }
    public void Reset() { primed=false; Percent=null; LastCulprit=null; trend.Reset(); }
    public string Sample(bool notify, IntPtr perchWindow = default(IntPtr)) {
      Stamp idle,kernel,user;
      if(!GetSystemTimes(out idle,out kernel,out user)) throw new InvalidOperationException("Windows CPU usage is unavailable.");
      ulong total=kernel.Value+user.Value; double now=clock.Elapsed.TotalSeconds;
      if(!primed) { primed=true; idleBefore=idle.Value; totalBefore=total; before=now; return null; }
      double seconds=now-before;
      ulong delta=total>=totalBefore?total-totalBefore:0;
      ulong idleDelta=idle.Value>=idleBefore?idle.Value-idleBefore:0;
      idleBefore=idle.Value; totalBefore=total; before=now;
      if(delta==0 || seconds>5) { Percent=null; LastCulprit=null; trend.Observe(0,6,false); return null; }
      Percent=Math.Max(0,Math.Min(100,100.0*(1-(double)idleDelta/delta)));
      CulpritInfo culprit=null;
      if(Percent.Value>=75 && notify) {
        culprit=ProcessTriage.FindCulprit(perchWindow);
      }
      LastCulprit=culprit;
      return trend.Observe(Percent.Value,seconds,notify,culprit);
    }
    public static string Probe() {
      var watcher=new CpuWatch(); watcher.Sample(false); System.Threading.Thread.Sleep(1100); watcher.Sample(false);
      if(!watcher.Percent.HasValue) throw new Exception("No CPU usage sample");
      return "CPU usage read successfully: "+Math.Round(watcher.Percent.Value)+"%.";
    }
  }
}
