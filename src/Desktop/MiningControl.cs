using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Text;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;

namespace CorbiePet {
  public enum MiningMode { Off, Idle, WhileActive }
  public sealed class MiningTelemetry {
    public string Rate { get; private set; }
    public string State { get; private set; }
    public int Accepted { get; private set; }
    public int Rejected { get; private set; }
    DateTime lastSpeedUtc,lastShareUtc;
    bool positiveSpeed;
    public void Reset() { Rate="not reported yet"; State="Starting XMRig"; Accepted=Rejected=0; lastSpeedUtc=lastShareUtc=DateTime.MinValue; positiveSpeed=false; }
    public MiningTelemetry() { Reset(); }
    public bool IsHashing(DateTime now) {
      return (positiveSpeed && (now-lastSpeedUtc).TotalSeconds<=90) || (lastShareUtc!=DateTime.MinValue && (now-lastShareUtc).TotalSeconds<=90);
    }
    public string Report(DateTime now,DateTime startedUtc) {
      string rate=lastSpeedUtc!=DateTime.MinValue && (now-lastSpeedUtc).TotalSeconds>90?"stale (last "+Rate+")":Rate;
      string wait=lastSpeedUtc==DateTime.MinValue?" | No speed report after "+Math.Max(0,(int)(now-startedUtc).TotalSeconds)+" seconds":"";
      string help=State=="Pool closed the connection; retrying"?" | Check pool settings and network filtering; extra threads cannot fix a connection failure":"";
      return State+" | Hash rate (10s): "+rate+" | Pool-accepted shares this run: "+Accepted+"; rejected: "+Rejected+wait+help;
    }
    public void Observe(string output) { Observe(output,DateTime.UtcNow); }
    public void Observe(string output,DateTime now) {
      string text=Regex.Replace(output ?? "",@"\x1b\[[0-9;]*m","");
      var speed=Regex.Match(text,@"speed\s+10s/60s/15m\s+([0-9.]+|n/a)\s+([0-9.]+|n/a)\s+([0-9.]+|n/a)\s+(H/s|kH/s|MH/s)",RegexOptions.IgnoreCase);
      if(speed.Success) {
        Rate=speed.Groups[1].Value=="n/a"?"not reported yet":speed.Groups[1].Value+" "+speed.Groups[4].Value;
        double measured;
        positiveSpeed=double.TryParse(speed.Groups[1].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out measured) && measured>0;
        if(!positiveSpeed) lastShareUtc=DateTime.MinValue;
        lastSpeedUtc=now; State=positiveSpeed?"Hashing":"Waiting for a positive speed report";
      }
      string lower=text.ToLowerInvariant();
      var share=Regex.Match(lower,@"\b(accepted|rejected)\s+\((\d+)/(\d+)\)");
      if(share.Success) {
        int accepted,rejected;
        if(int.TryParse(share.Groups[2].Value,out accepted)) Accepted=Math.Max(Accepted,accepted);
        if(int.TryParse(share.Groups[3].Value,out rejected)) Rejected=Math.Max(Rejected,rejected);
        if(share.Groups[1].Value=="accepted") { State="Pool accepted a share"; lastShareUtc=now; }
        else State="Pool rejected a share";
      }
      else if(lower.Contains("new job from")) State="Pool connected; waiting for speed report";
      else if(lower.Contains("paused")) { State="XMRig paused"; Rate="paused"; positiveSpeed=false; lastShareUtc=DateTime.MinValue; }
      else if(lower.Contains("resume")) { State="XMRig resumed; waiting for speed report"; Rate="not reported yet"; positiveSpeed=false; lastSpeedUtc=lastShareUtc=DateTime.MinValue; }
      else if(lower.Contains("init dataset")) State="Preparing mining memory";
      else if(lower.Contains("ready") && lower.Contains("threads")) State="CPU threads ready; waiting for speed report";
      else if(lower.Contains("read error") || lower.Contains("end of file")) { State="Pool closed the connection; retrying"; Rate="not reported yet"; positiveSpeed=false; lastSpeedUtc=lastShareUtc=DateTime.MinValue; }
      else if(lower.Contains("no active pools") || lower.Contains("connect error") || lower.Contains("connection refused") || lower.Contains("dns error") || lower.Contains("failed to resolve") || lower.Contains("tls error") || lower.Contains("ssl error")) { State="Pool connection unavailable; retrying"; Rate="not reported yet"; positiveSpeed=false; lastSpeedUtc=lastShareUtc=DateTime.MinValue; }
      else if(lower.Contains("invalid address") || lower.Contains("invalid login") || lower.Contains("unauthenticated")) { State="Pool rejected payout/login settings"; Rate="not reported yet"; positiveSpeed=false; lastSpeedUtc=lastShareUtc=DateTime.MinValue; }
      else if(lower.Contains("failed to allocate")) { State="XMRig could not allocate mining memory"; Rate="not reported yet"; positiveSpeed=false; lastSpeedUtc=lastShareUtc=DateTime.MinValue; }
    }
  }
  public static class MiningPolicy {
    public static bool Select(MiningMode mode,bool automaticDetox,bool detoxActive,out bool requireIdle) {
      requireIdle=mode==MiningMode.Idle && !(automaticDetox && detoxActive);
      return mode!=MiningMode.Off || (automaticDetox && detoxActive);
    }
  }
  public sealed class MiningGate {
    double settled;
    DateTime retryAfter;
    public bool Evaluate(DateTime now,double? cpu,double? temperature,bool ac,double idle,bool blocked,bool running,out string reason) {
      return Evaluate(now,cpu,temperature,ac,idle,blocked,running,true,out reason);
    }
    public bool Evaluate(DateTime now,double? cpu,double? temperature,bool ac,double idle,bool blocked,bool running,bool requireIdle,out string reason) {
      reason=!ac?"Waiting for mains power":blocked?"Paused for Corbie or your workload":requireIdle && idle<300?"Waiting for five minutes idle":
        !cpu.HasValue || double.IsNaN(cpu.Value) || cpu.Value<0 || cpu.Value>100?"Waiting for CPU readings":
        !temperature.HasValue || double.IsNaN(temperature.Value) || temperature.Value<0 || temperature.Value>120?"Waiting for live CPU temperature":
        cpu.Value>=75?"Paused: CPU at or above 75%":temperature.Value>=(running?80:70)?"Paused: CPU too warm":
        !running && cpu.Value>=60?"Waiting for CPU below 60%":null;
      if(reason!=null) { settled=0; if(running) retryAfter=now.AddSeconds(120); return false; }
      if(running) return true;
      if(now<retryAfter) { reason="Cooling down before restart"; settled=0; return false; }
      settled+=2;
      if(settled<30) { reason="Checking conditions for 30 seconds"; return false; }
      return true;
    }
    public void Reset() { settled=0; retryAfter=DateTime.MinValue; }
  }

  // Closing the job handle terminates only the miner started by this controller,
  // including if Corbie crashes. Existing independently started miners are untouched.
  public sealed class MiningJob : IDisposable {
    [StructLayout(LayoutKind.Sequential)] struct Basic { public long ProcessTime,JobTime; public uint Flags; public UIntPtr Min,Max; public uint Active; public UIntPtr Affinity; public uint Priority,Scheduling; }
    [StructLayout(LayoutKind.Sequential)] struct Io { public ulong ReadOps,WriteOps,OtherOps,ReadBytes,WriteBytes,OtherBytes; }
    [StructLayout(LayoutKind.Sequential)] struct Limits { public Basic Basic; public Io Io; public UIntPtr ProcessMemory,JobMemory,PeakProcess,PeakJob; }
    [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr CreateJobObject(IntPtr attrs,string name);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetInformationJobObject(IntPtr job,int info,ref Limits limits,int size);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    IntPtr handle;
    public MiningJob() {
      handle=CreateJobObject(IntPtr.Zero,null);
      var limits=new Limits(); limits.Basic.Flags=0x2000;
      if(handle==IntPtr.Zero || !SetInformationJobObject(handle,9,ref limits,Marshal.SizeOf(typeof(Limits)))) { Dispose(); throw new InvalidOperationException("Windows could not contain the miner. Mining stays off."); }
    }
    public void Attach(Process process) { if(!AssignProcessToJobObject(handle,process.Handle)) throw new InvalidOperationException("Windows could not attach the miner to Corbie. Mining stays off."); }
    public void Dispose() { if(handle!=IntPtr.Zero) { CloseHandle(handle); handle=IntPtr.Zero; } }
  }

  public sealed class MiningControl : IDisposable {
    [StructLayout(LayoutKind.Sequential)] struct LastInput { public uint Size,Tick; }
    [StructLayout(LayoutKind.Sequential)] struct Power { public byte AC,Flags,Percent,Reserved; public uint Life,FullLife; }
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LastInput input);
    [DllImport("kernel32.dll")] static extern bool GetSystemPowerStatus(out Power power);
    readonly object sync=new object();
    readonly CpuWatch cpu=new CpuWatch();
    readonly TemperatureWatch temperature=new TemperatureWatch();
    readonly CancellationTokenSource cancel=new CancellationTokenSource();
    readonly MiningGate gate=new MiningGate();
    readonly MiningTelemetry telemetry=new MiningTelemetry();
    System.Threading.Timer timer;
    Process process;
    MiningJob job;
    int checking;
    bool enabled,disposed;
    MiningMode mode;
    bool autoDetox;
    bool? runningRequiresIdle;
    volatile bool detoxActive;
    volatile bool blocked=true;
    long heartbeat;
    volatile string status="Mining off";
    string executable,config;
    readonly string root;
    int threads=4;
    string poolHost,payoutWallet;
    string logPath;
    long logOffset;
    string partialLog="";
    DateTime startedUtc;
    bool poolTls;
    public string Status { get { return status; } }
    public string LiveReport { get { lock(sync) return process==null?"XMRig stopped":"XMRig running (PID "+process.Id+") | "+telemetry.Report(DateTime.UtcNow,startedUtc); } }
    public string LogPath { get { return logPath; } }
    public bool Enabled { get { lock(sync) return enabled; } }
    public bool AutoDetox { get { lock(sync) return autoDetox; } }
    public int Threads { get { lock(sync) return threads; } }
    public string Executable { get { return executable; } }
    public MiningControl(string settingsRoot=null,bool testing=false) {
      root=settingsRoot ?? AppDomain.CurrentDomain.BaseDirectory;
      logPath=Path.Combine(root,"corbie-mining.log");
      executable=Path.Combine(root,"mining-tools","xmrig-6.26.0","xmrig.exe");
      config=Path.Combine(root,"corbie-mining.json");
      try { int saved=int.Parse(File.ReadAllText(Path.Combine(root,"corbie-mining-threads.txt"))); ThreadProfile(saved,Environment.ProcessorCount); threads=saved; } catch { }
      try { string saved=File.ReadAllText(Path.Combine(root,"corbie-mining-executable.txt")); if(File.Exists(saved) && Path.GetFileName(saved).Equals("xmrig.exe",StringComparison.OrdinalIgnoreCase)) executable=saved; } catch { }
      try {
        if(File.ReadAllText(Path.Combine(root,"corbie-mining-detox.txt")).Trim()=="enabled") {
          PrepareConfiguration(); autoDetox=enabled=true; status="Automatic detox mining armed; waiting for a Corbie detox break";
        }
      } catch { }
      if(!testing) timer=new System.Threading.Timer(_=> { var ignored=Tick(); },null,2000,2000);
    }
    public void Heartbeat(bool busy,bool detox=false) { blocked=busy; detoxActive=detox; Interlocked.Exchange(ref heartbeat,DateTime.UtcNow.Ticks); }
    public static object Config(string pool,string wallet,bool tls) {
      return Config(pool,wallet,tls,true);
    }
    public static object Config(string pool,string wallet,bool tls,bool requireIdle) {
      return Config(pool,wallet,tls,requireIdle,4);
    }
    public static int[] ThreadProfile(int count,int processors) {
      if((count!=4 && count!=8 && count!=12 && count!=16 && count!=20 && count!=24 && count!=32) || count>processors) throw new ArgumentException("Choose a supported thread count for this computer.");
      var profile=new int[count];
      int firstPass=(processors+1)/2;
      for(int i=0;i<count;i++) profile[i]=i<firstPass?i*2:(i-firstPass)*2+1;
      return profile;
    }
    public static object Config(string pool,string wallet,bool tls,bool requireIdle,int count) {
      var profile=ThreadProfile(count,Environment.ProcessorCount);
      Uri endpoint;
      if(string.IsNullOrWhiteSpace(pool) || pool.Trim()!=pool || pool.IndexOfAny(new [] {'/','\\','"','\r','\n',' '})>=0 ||
         !Uri.TryCreate("tcp://"+pool,UriKind.Absolute,out endpoint) || endpoint.Port<=0 || endpoint.Port>65535 || pool.IndexOf("donate",StringComparison.OrdinalIgnoreCase)>=0)
        throw new ArgumentException("Enter the pool's host:port, from its Monero mining instructions.");
      if(!Regex.IsMatch(wallet ?? "",@"\A[48][1-9A-HJ-NP-Za-km-z]{94}\z")) throw new ArgumentException("Enter your public Monero receiving address (95 characters). Never enter a seed phrase or private key.");
      return new Dictionary<string,object> {
        {"autosave",false},{"background",false},{"watch",false},{"colors",false},{"title",false},{"dmi",true},
        {"donate-level",1},{"http",new { enabled=false }},
        {"randomx",new Dictionary<string,object> {{"init",2},{"wrmsr",false},{"rdmsr",false},{"mode","fast"}}},
        {"cpu",new Dictionary<string,object> {{"enabled",true},{"priority",0},{"yield",true},{"rx",profile}}},
        {"cuda",new {enabled=false}},{"opencl",new {enabled=false}},
        {"pools",new [] {new { algo="rx/0",coin="monero",url=pool,user=wallet,pass="x",tls=tls,keepalive=true,enabled=true }}},
        {"pause-on-battery",true},{"pause-on-active",requireIdle?(object)300:false},{"print-time",30}
      };
    }
    public void Configure(string exe,string pool,string wallet,bool tls) {
      lock(sync) {
        if(disposed) throw new ObjectDisposedException("MiningControl");
        if(enabled) throw new InvalidOperationException("Stop mining before changing setup.");
        string full=Path.GetFullPath(exe);
        if(!File.Exists(full) || !string.Equals(Path.GetFileName(full),"xmrig.exe",StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choose the downloaded xmrig.exe.");
        var body=Config(pool,wallet,tls,true,threads);
        File.WriteAllText(config,new JavaScriptSerializer().Serialize(body));
        File.WriteAllText(Path.Combine(root,"corbie-mining-executable.txt"),full);
        executable=full; status="Setup saved. Mining off; enable when ready.";
      }
    }
    void PrepareConfiguration() {
      if(!File.Exists(executable) || !File.Exists(config)) throw new InvalidOperationException("Save the XMRig, pool and payout setup first.");
      var body=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(config));
      var pools=(IList)body["pools"]; var pool=(Dictionary<string,object>)pools[0];
      string host=Convert.ToString(pool["url"]),wallet=Convert.ToString(pool["user"]); bool tls=Convert.ToBoolean(pool["tls"]);
      Config(host,wallet,tls,true,threads); poolHost=host; payoutWallet=wallet; poolTls=tls;
    }
    public void SetThreads(int count) {
      lock(sync) {
        if(disposed) throw new ObjectDisposedException("MiningControl");
        ThreadProfile(count,Environment.ProcessorCount);
        if(count==threads) return;
        File.WriteAllText(Path.Combine(root,"corbie-mining-threads.txt"),count.ToString());
        threads=count; StopProcess(); gate.Reset();
        status=enabled?"Thread count changed to "+threads+"; rechecking mining conditions":"Mining off; "+threads+" threads selected";
      }
    }
    void SaveDetoxPreference() {
      File.WriteAllText(Path.Combine(root,"corbie-mining-detox.txt"),autoDetox?"enabled":"disabled");
    }
    public void SetAutoDetox(bool value) {
      lock(sync) {
        if(disposed) throw new ObjectDisposedException("MiningControl");
        if(value) PrepareConfiguration();
        bool previous=autoDetox; autoDetox=value;
        try { SaveDetoxPreference(); } catch { autoDetox=previous; throw; }
        enabled=mode!=MiningMode.Off || autoDetox;
        StopProcess(); gate.Reset();
        status=value?"Automatic detox mining armed; waiting for a Corbie detox break":"Automatic detox mining off";
      }
    }
    public void Enable() { Enable(MiningMode.Idle); }
    public void Enable(MiningMode requestedMode) {
      lock(sync) {
        if(disposed) throw new ObjectDisposedException("MiningControl");
        if(requestedMode!=MiningMode.Idle && requestedMode!=MiningMode.WhileActive) throw new ArgumentException("Choose idle or while-active mining.");
        PrepareConfiguration(); StopProcess(); mode=requestedMode;
        enabled=true; gate.Reset(); cpu.Reset(); status="Enabled: "+(mode==MiningMode.Idle?"idle":"while-active")+" mining; checking conditions";
      }
    }
    public void Stop() { lock(sync) {
      enabled=false; mode=MiningMode.Off; autoDetox=false; StopProcess(); gate.Reset(); status="Mining off (including automatic detox)";
      try { SaveDetoxPreference(); } catch { status+=". Could not save this preference; turn it off again after restarting."; }
    } }
    void StopProcess() {
      runningRequiresIdle=null;
      telemetry.Reset();
      if(job!=null) { job.Dispose(); job=null; }
      if(process!=null) {
        try { if(!process.HasExited) process.Kill(); } catch { }
        process.Dispose(); process=null;
      }
    }
    async Task Tick() {
      if(Interlocked.Exchange(ref checking,1)!=0) return;
      try {
        lock(sync) {
          if(!enabled || disposed) return;
          bool required;
          if(!MiningPolicy.Select(mode,autoDetox,detoxActive,out required)) { StopProcess(); gate.Reset(); status="Automatic detox mining armed; waiting for a Corbie detox break"; return; }
        }
        bool another=false;
        foreach(var candidate in Process.GetProcessesByName("xmrig")) {
          try { lock(sync) { if(process==null || candidate.Id!=process.Id) another=true; } } finally { candidate.Dispose(); }
        }
        if(another) { lock(sync) { StopProcess(); gate.Reset(); status="Paused: another XMRig is already running"; } return; }
        Power power; bool ac=GetSystemPowerStatus(out power) && power.AC==1;
        var input=new LastInput { Size=(uint)Marshal.SizeOf(typeof(LastInput)) };
        double idle=GetLastInputInfo(ref input)?unchecked((uint)Environment.TickCount-input.Tick)/1000.0:0;
        bool busy=blocked || DateTime.UtcNow.Ticks-Interlocked.Read(ref heartbeat)>TimeSpan.FromSeconds(10).Ticks;
        // Stop before waiting for any sensor/network request when the user returns.
        lock(sync) {
          bool requireIdle; bool wanted=MiningPolicy.Select(mode,autoDetox,detoxActive,out requireIdle);
          if(!wanted || !ac || busy || (requireIdle && idle<300)) { StopProcess(); gate.Reset(); status=!wanted?"Waiting for a Corbie detox break":!ac?"Paused: on battery":busy?"Paused: Corbie is busy":"Waiting for five minutes idle"; return; }
        }
        cpu.Sample(false);
        double? c=null;
        try { c=(await temperature.Read(cancel.Token).ConfigureAwait(false)).Celsius; } catch { }
        lock(sync) {
          if(disposed || !enabled) return;
          ac=GetSystemPowerStatus(out power) && power.AC==1;
          idle=GetLastInputInfo(ref input)?unchecked((uint)Environment.TickCount-input.Tick)/1000.0:0;
          busy=blocked || DateTime.UtcNow.Ticks-Interlocked.Read(ref heartbeat)>TimeSpan.FromSeconds(10).Ticks;
          bool requireIdle;
          if(!MiningPolicy.Select(mode,autoDetox,detoxActive,out requireIdle)) { StopProcess(); gate.Reset(); status="Automatic detox mining armed; waiting for a Corbie detox break"; return; }
          if(process!=null && runningRequiresIdle!=requireIdle) { StopProcess(); gate.Reset(); }
          bool running=process!=null && !process.HasExited;
          if(process!=null && !running) { StopProcess(); enabled=false; status="XMRig exited. Check setup; mining disabled."; return; }
          string reason;
          if(!gate.Evaluate(DateTime.UtcNow,cpu.Percent,c,ac,idle,busy,running,requireIdle,out reason)) { StopProcess(); status=reason; return; }
          if(running) ReadMiningLog();
          if(!running) {
            File.WriteAllText(config,new JavaScriptSerializer().Serialize(Config(poolHost,payoutWallet,poolTls,requireIdle,threads)));
            string payoutArguments=" -a rx/0 -o "+poolHost+" -u "+payoutWallet+" --threads="+threads+" --cpu-priority=0 --randomx-init=2 --randomx-wrmsr=-1 --randomx-no-rdmsr --pause-on-battery --pause-on-active="+(requireIdle?"300":"0")+(poolTls?" --tls":"");
            job=new MiningJob();
            telemetry.Reset();
            try {
              if(File.Exists(logPath)) File.Copy(logPath,Path.Combine(root,"corbie-mining.previous.log"),true);
              File.WriteAllText(logPath,""); logOffset=0; partialLog=""; startedUtc=DateTime.UtcNow;
              process=Process.Start(new ProcessStartInfo(executable,"--config=\""+config+"\""+payoutArguments+" --log-file=\""+logPath+"\"") { UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(executable),RedirectStandardOutput=true,RedirectStandardError=true });
              job.Attach(process);
              var owner=process;
              process.OutputDataReceived+=(s,e)=>ObserveOutput(owner,e.Data); process.ErrorDataReceived+=(s,e)=>ObserveOutput(owner,e.Data);
              process.BeginOutputReadLine(); process.BeginErrorReadLine();
              process.PriorityClass=ProcessPriorityClass.Idle;
              runningRequiresIdle=requireIdle;
            } catch { StopProcess(); throw; }
          }
          status=(telemetry.IsHashing(DateTime.UtcNow)?"HASHING":telemetry.State+"; hashing unconfirmed")+" ("+(autoDetox && detoxActive?"detox":requireIdle?"idle":"while active")+") - "+threads+" CPU threads | CPU "+cpu.Percent.Value.ToString("0")+"% | "+c.Value.ToString("0")+" C";
        }
      } catch(Exception ex) { lock(sync) { StopProcess(); enabled=false; status="Mining stopped: "+RoostMemory.Short(ex.Message,180); } }
      finally { Interlocked.Exchange(ref checking,0); }
    }
    void ObserveOutput(Process owner,string text) {
      if(text==null) return;
      lock(sync) { if(process==owner && !disposed) telemetry.Observe(text); }
    }
    void ReadMiningLog() {
      try {
        using(var file=new FileStream(logPath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) {
          if(file.Length<logOffset) { logOffset=0; partialLog=""; }
          file.Position=logOffset;
          var bytes=new byte[(int)Math.Min(65536,file.Length-logOffset)];
          int read=file.Read(bytes,0,bytes.Length); logOffset+=read;
          string text=partialLog+Encoding.UTF8.GetString(bytes,0,read);
          string[] lines=text.Split('\n');
          for(int i=0;i<lines.Length-1;i++) if(lines[i].Length<=4096) telemetry.Observe(lines[i].TrimEnd('\r'));
          partialLog=lines[lines.Length-1]; if(partialLog.Length>4096) partialLog="";
        }
      } catch(IOException) { } catch(UnauthorizedAccessException) { }
    }
    public void Dispose() { lock(sync) { if(disposed) return; disposed=true; enabled=false; if(timer!=null) timer.Dispose(); cancel.Cancel(); StopProcess(); temperature.Dispose(); } }
    public static void Check() {
      foreach(int count in new [] {4,8,12,16,20,24,32}) {
        var profile=ThreadProfile(count,32); var unique=new HashSet<int>(profile);
        if(profile.Length!=count || unique.Count!=count || profile[count-1]>=32) throw new Exception("Invalid mining CPU affinity");
      }
      if(ThreadProfile(4,4)[3]!=3) throw new Exception("Mining affinity exceeds smaller computer");
      var stronger=ThreadProfile(20,32);
      for(int i=0;i<16;i++) if(stronger[i]!=i*2) throw new Exception("Stronger mining dropped an existing CPU slot");
      if(stronger[16]!=1 || stronger[19]!=7) throw new Exception("Extra threads did not add new CPU slots");
      bool badProfile=false; try { ThreadProfile(16,8); } catch(ArgumentException) { badProfile=true; }
      if(!badProfile) throw new Exception("Unsupported thread count accepted");
      var meter=new MiningTelemetry();
      meter.Observe("[time] net new job from example:443 diff 1000 algo rx/0");
      if(meter.Rate!="not reported yet") throw new Exception("Pool connection invented a hash rate");
      meter.Observe("[time] miner speed 10s/60s/15m 2456.7 n/a n/a H/s max 2500.0 H/s");
      if(meter.Rate!="2456.7 H/s") throw new Exception("Hash report not parsed");
      meter.Observe("net accepted (1/0) diff 1000");
      if(meter.Rate!="2456.7 H/s" || meter.State!="Pool accepted a share") throw new Exception("Accepted share lost measured rate");
      meter.Observe("net connect error: connection refused");
      if(meter.Rate!="not reported yet") throw new Exception("Stale rate after connection failure");
      meter.Observe("cpu paused");
      if(meter.Rate!="paused") throw new Exception("Pause not shown");
      meter.Reset(); if(meter.Rate!="not reported yet") throw new Exception("Old mining rate carried across restart");
      DateTime reportTime=DateTime.UtcNow;
      meter.Observe("miner speed 10s/60s/15m n/a n/a n/a H/s",reportTime);
      if(meter.IsHashing(reportTime)) throw new Exception("Missing speed was reported as hashing");
      meter.Observe("miner speed 10s/60s/15m 0.0 0.0 n/a H/s",reportTime);
      if(meter.IsHashing(reportTime)) throw new Exception("Zero speed was reported as hashing");
      meter.Observe("miner speed 10s/60s/15m 2456.7 n/a n/a H/s",reportTime);
      if(!meter.IsHashing(reportTime) || meter.IsHashing(reportTime.AddSeconds(91)) || !meter.Report(reportTime.AddSeconds(91),reportTime).Contains("stale")) throw new Exception("Measured speed freshness failed");
      meter.Observe("net accepted (2/0) diff 1000",reportTime); meter.Observe("net accepted (2/0) diff 1000",reportTime);
      meter.Observe("net rejected (2/1) diff 1000",reportTime);
      if(meter.Accepted!=2 || meter.Rejected!=1) throw new Exception("Share counters double-counted log and pipe output");
      meter.Observe("net TLS error: handshake failed",reportTime);
      if(meter.IsHashing(reportTime) || !meter.State.Contains("unavailable")) throw new Exception("TLS failure kept a fake hashing state");
      foreach(string failure in new [] {"[2026-10-04 20:06:46.221]  net      pool.hashvault.pro:443 read error: \"end of file\"", "net pool.example:443 read error: connection reset by peer", "net end of file"}) {
        meter.Observe("miner speed 10s/60s/15m 2456.7 n/a n/a H/s",reportTime);
        meter.Observe("net accepted (2/1) diff 1000",reportTime);
        meter.Observe(failure,reportTime);
        if(meter.IsHashing(reportTime) || meter.Rate!="not reported yet" || meter.State!="Pool closed the connection; retrying" || !meter.Report(reportTime,reportTime).Contains("network filtering")) throw new Exception("Pool closure kept a fake hashing state or hid recovery guidance");
        if(meter.Accepted!=2 || meter.Rejected!=1) throw new Exception("Connection failure discarded real share history");
        meter.Observe("net new job from example:443 diff 1000 algo rx/0",reportTime);
        if(meter.IsHashing(reportTime) || meter.Report(reportTime,reportTime).Contains("network filtering")) throw new Exception("Reconnection retained closure advice or invented hashing");
        meter.Observe("miner speed 10s/60s/15m 1000.0 n/a n/a H/s",reportTime);
        if(!meter.IsHashing(reportTime)) throw new Exception("Speed evidence did not recover after a pool closure");
      }
      meter.Reset(); if(!meter.Report(reportTime.AddSeconds(120),reportTime).Contains("No speed report after 120 seconds")) throw new Exception("Startup stall was hidden");
      var g=new MiningGate(); var now=DateTime.UtcNow; string reason;
      for(int i=0;i<14;i++) if(g.Evaluate(now,20,60,true,301,false,false,out reason)) throw new Exception("Mining began without settling");
      if(!g.Evaluate(now,20,60,true,301,false,false,out reason)) throw new Exception("Mining never became eligible");
      if(g.Evaluate(now,75,60,true,301,false,true,out reason)) throw new Exception("75 percent CPU allowed");
      if(g.Evaluate(now.AddSeconds(1),20,60,true,301,false,false,out reason)) throw new Exception("Missing restart cooldown");
      foreach(var test in new [] {0,1,2,3,4,5}) {
        g.Reset();
        if(g.Evaluate(now,test==0?(double?)null:20,test==1?(double?)null:test==2?80:60,test!=3,test==4?0:301,test==5,true,out reason)) throw new Exception("Mining guard missed "+test);
      }
      bool rejected=false; try { Config("donate.v2.xmrig.com:3333","YOUR_WALLET_ADDRESS",false); } catch(ArgumentException) { rejected=true; }
      if(!rejected) throw new Exception("Example donation config accepted");
      var body=(Dictionary<string,object>)Config("127.0.0.1:3333","4"+new string('1',94),false);
      if(Convert.ToBoolean(((Dictionary<string,object>)body["randomx"])["wrmsr"])) throw new Exception("Mining driver writes enabled");
      bool requireIdle;
      if(MiningPolicy.Select(MiningMode.Off,true,false,out requireIdle)) throw new Exception("Detox mining starts outside a break");
      if(!MiningPolicy.Select(MiningMode.Off,true,true,out requireIdle) || requireIdle) throw new Exception("Detox still requires idle");
      if(MiningPolicy.Select(MiningMode.Off,false,true,out requireIdle)) throw new Exception("Stop overridden by detox");
      if(!MiningPolicy.Select(MiningMode.WhileActive,false,false,out requireIdle) || requireIdle) throw new Exception("Active mode requires idle");
      if(!MiningPolicy.Select(MiningMode.Idle,true,false,out requireIdle) || !requireIdle) throw new Exception("Idle fallback lost");
      if(!MiningPolicy.Select(MiningMode.Idle,true,true,out requireIdle) || requireIdle) throw new Exception("Detox did not override idle wait");
      var activeBody=(Dictionary<string,object>)Config("127.0.0.1:3333","4"+new string('1',94),false,false);
      if(!object.Equals(activeBody["pause-on-active"],false)) throw new Exception("XMRig active pause not disabled");
      g.Reset();
      for(int i=0;i<14;i++) if(g.Evaluate(now,20,60,true,0,false,false,false,out reason)) throw new Exception("Non-idle settling was bypassed");
      if(!g.Evaluate(now,20,60,true,0,false,false,false,out reason)) throw new Exception("Non-idle mining blocked by activity");
      for(int test=0;test<5;test++) {
        g.Reset();
        if(g.Evaluate(now,test==0?75:20,test==1?(double?)null:test==2?80:60,test!=3,0,test==4,true,false,out reason)) throw new Exception("Non-idle guard missed "+test);
      }
      using(var breaks=new BreakCoach(true)) {
        breaks.SetDetoxMode(true); breaks.StartBreak();
        if(!MiningPolicy.Select(MiningMode.Off,true,breaks.OnBreak && breaks.DetoxMode,out requireIdle)) throw new Exception("Detox break did not arm mining");
        breaks.FinishEarly();
        if(MiningPolicy.Select(MiningMode.Off,true,breaks.OnBreak && breaks.DetoxMode,out requireIdle)) throw new Exception("Ended detox still mines");
      }
      string testRoot=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"mining-checks");
      Directory.CreateDirectory(testRoot);
      using(var controller=new MiningControl(testRoot,true)) {
        controller.Stop();
        controller.Configure(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"mining-tools","xmrig-6.26.0","xmrig.exe"),"127.0.0.1:3333","4"+new string('1',94),false);
        controller.SetThreads(8);
        controller.SetThreads(20);
        if(controller.Threads!=20) throw new Exception("Stronger thread count was not saved");
        File.WriteAllText(controller.LogPath,"net new job from example:443\nminer speed 10s/60s/15m 1234.0 n/a n/a H/s\nnet accepted (1/");
        controller.ReadMiningLog();
        if(controller.telemetry.Rate!="1234.0 H/s" || controller.telemetry.Accepted!=0) throw new Exception("Mining log fallback or partial-line handling failed");
        File.AppendAllText(controller.LogPath,"0) diff 1000\n"); controller.ReadMiningLog(); controller.ReadMiningLog();
        if(controller.telemetry.Accepted!=1) throw new Exception("Log tail skipped a split share line or replayed old lines");
        File.WriteAllText(controller.LogPath,"cpu paused\n"); controller.ReadMiningLog();
        if(controller.telemetry.Rate!="paused" || controller.telemetry.IsHashing(DateTime.UtcNow)) throw new Exception("Log truncation or pause handling failed");
        controller.SetAutoDetox(true);
        if(!controller.AutoDetox || !controller.Enabled) throw new Exception("Detox option did not arm controller");
        using(var restored=new MiningControl(testRoot,true)) {
          if(!restored.AutoDetox || !restored.Enabled || restored.mode!=MiningMode.Off || restored.Threads!=20) throw new Exception("Mining preferences not restored independently of manual mode");
          restored.Enable(MiningMode.WhileActive); restored.Stop();
          if(restored.Enabled || restored.AutoDetox || restored.mode!=MiningMode.Off) throw new Exception("Stop did not clear all modes");
        }
        using(var stopped=new MiningControl(testRoot,true)) if(stopped.Enabled || stopped.AutoDetox) throw new Exception("Stopped detox resumed after restart");
        controller.Stop();
      }
    }
  }

  public partial class Pet {
    MiningControl mining;
    TextBlock miningStatus;
    TextBlock miningLiveStatus;
    TextBlock miningSetupStatus;
    TextBox miningExe,miningPool,miningWallet;
    CheckBox miningTls;
    CheckBox miningAutoDetox;
    ComboBox miningThreads;
    bool refreshingMining;
    void BuildMiningTab() {
      if(mining==null && !testing) mining=new MiningControl();
      var body=TabBody();
      miningStatus=Status();
      miningLiveStatus=Status();
      miningSetupStatus=Status();
      miningExe=new TextBox {Text=mining==null?"":mining.Executable};
      miningPool=new TextBox(); miningWallet=new TextBox();
      miningTls=new CheckBox {Content="Use TLS (match your pool's chosen port)",IsChecked=true};
      miningThreads=new ComboBox();
      foreach(int count in new [] {4,8,12,16,20,24,32}) if(count<=Environment.ProcessorCount) miningThreads.Items.Add(count);
      miningThreads.SelectedItem=mining==null?4:mining.Threads;
      miningThreads.SelectionChanged+=(s,e)=> {
        if(refreshingMining || mining==null || miningThreads.SelectedItem==null) return;
        try { mining.SetThreads((int)miningThreads.SelectedItem); miningSetupStatus.Text=""; } catch(Exception ex) { miningSetupStatus.Text=ex.Message; }
        RefreshMining();
      };
      try {
        var data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"corbie-mining.json")));
        var pools=(IList)data["pools"]; var pool=(Dictionary<string,object>)pools[0];
        miningPool.Text=Convert.ToString(pool["url"]); miningWallet.Text=Convert.ToString(pool["user"]); miningTls.IsChecked=Convert.ToBoolean(pool["tls"]);
      } catch { }
      body.Children.Add(Section("Mining setup",new TextBlock {Text="Monero / XMRig. Manual modes start off each launch. Automatic detox mining remembers your choice. XMRig contributes 1% of mining time to its developers.",TextWrapping=TextWrapping.Wrap},
        new TextBlock {Text="XMRig executable"},miningExe,new TextBlock {Text="Pool host:port"},miningPool,new TextBlock {Text="Public Monero receiving address"},miningWallet,miningTls,
        Actions(ActionButton("Choose XMRig",()=> { var dialog=new Microsoft.Win32.OpenFileDialog {Filter="XMRig|xmrig.exe"}; if(dialog.ShowDialog()==true) miningExe.Text=dialog.FileName; }),
          ActionButton("Save setup",()=> { try { mining.Configure(miningExe.Text,miningPool.Text.Trim(),miningWallet.Text.Trim(),miningTls.IsChecked==true); miningSetupStatus.Text="Setup saved."; } catch(Exception ex) { miningSetupStatus.Text=ex.Message; } })),miningSetupStatus));
      miningAutoDetox=Toggle("Automatically mine during Corbie's detox breaks",value=> {
        if(refreshingMining) return;
        try { mining.SetAutoDetox(value); miningSetupStatus.Text=""; } catch(Exception ex) { miningSetupStatus.Text=ex.Message; }
        RefreshMining();
      });
      body.Children.Add(Section("Mining modes",new TextBlock {Text="Idle mode waits five minutes. Mine while active can run while you use the computer. Both check CPU below 60% and temperature below 70 C for 30 seconds before starting; stop at 75% CPU or 80 C, on battery, without temperature readings, or while Corbie processes voice/screen work. Low-priority threads; two-minute cooldown after load/heat stops. Requires Libre Hardware Monitor's local web server.",TextWrapping=TextWrapping.Wrap},
        new TextBlock {Text="Mining threads: 4 gentle; 8 / 12 / 16 / 20 / 24 / 32 stronger. Higher counts may hit your CPU stop sooner."},miningThreads,
        miningStatus,miningLiveStatus,Actions(ActionButton("Enable idle mining",()=> { try { mining.Enable(); miningSetupStatus.Text=""; miningStatus.Text=mining.Status; } catch(Exception ex) { miningSetupStatus.Text=ex.Message; } }),
          ActionButton("Mine while active",()=> { try { mining.Enable(MiningMode.WhileActive); miningSetupStatus.Text=""; miningStatus.Text=mining.Status; } catch(Exception ex) { miningSetupStatus.Text=ex.Message; } }),
          ActionButton("STOP mining",()=> { if(mining!=null) mining.Stop(); miningStatus.Text="Mining off"; }),
          ActionButton("Open mining log",()=> { if(mining==null || !File.Exists(mining.LogPath)) { miningSetupStatus.Text="No log yet. It is created when XMRig starts."; return; } try { Process.Start(new ProcessStartInfo("notepad.exe","\""+mining.LogPath+"\"") { UseShellExecute=true }); } catch(Exception ex) { miningSetupStatus.Text=ex.Message; } }))));
      body.Children.Add(Section("What proves it is working",new TextBlock {Text="A recent positive hash rate proves local hashing. Pool-accepted shares prove the pool received work. A running XMRig process alone proves neither. Your pool's unpaid balance is separate from your wallet balance, which changes after a payout.",TextWrapping=TextWrapping.Wrap}));
      body.Children.Add(Section("Detox mining",miningAutoDetox,new TextBlock {Text="Uses the Breaks nook's Detox mode plus an active break timer. No five-minute idle wait. When detox ends, mining stops unless you enabled a manual mode. STOP disables all modes and the automatic option.",TextWrapping=TextWrapping.Wrap}));
      AddTab("Mining",body);
      RefreshMining();
    }
    void RefreshMining() {
      if(mining==null || miningStatus==null) return;
      miningStatus.Text=mining.Status;
      if(miningLiveStatus!=null) miningLiveStatus.Text=mining.LiveReport;
      refreshingMining=true;
      try { if(miningAutoDetox!=null) miningAutoDetox.IsChecked=mining.AutoDetox; if(miningThreads!=null) miningThreads.SelectedItem=mining.Threads; } finally { refreshingMining=false; }
    }
  }
}
