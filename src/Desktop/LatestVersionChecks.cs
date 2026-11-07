using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace CorbiePet {
  public static class LatestVersionChecks {
    public static void ProbeParent(string channel) {
      using(var finish=EventWaitHandle.OpenExisting(channel))finish.WaitOne(TimeSpan.FromSeconds(12));
    }
    public static void Run() {
      string current=Process.GetCurrentProcess().MainModule.FileName;
      string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".latest-check-"+Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(folder);
      try {
        if(LatestVersion.Find(folder)!=null)throw new Exception("Empty directory had a release.");
        foreach(string name in new [] {"Corbie-v0.99.exe","Corbie-v0.100.exe","Corbie-v0.100.2.exe"})File.Copy(current,Path.Combine(folder,name));
        File.WriteAllText(Path.Combine(folder,"Corbie-v99.999.exe"),"not an executable");
        File.Copy(current,Path.Combine(folder,"Corbie-v99999-preview.exe"));
        var latest=LatestVersion.Find(folder);
        if(latest==null || latest.Version!=new Version(0,100,2))throw new Exception("Numeric version ordering or invalid-build filtering failed.");
        if(LatestVersion.Parse("Corbie-v0.9.exe")>=LatestVersion.Parse("Corbie-v0.100.exe") || LatestVersion.Parse("Corbie-v0.100-preview.exe")!=null || LatestVersion.Parse("other.exe")!=null)throw new Exception("Release filename parsing failed.");
      } finally {foreach(string file in Directory.GetFiles(folder))File.Delete(file);Directory.Delete(folder);}
      if(LatestVersion.WaitForParent("0","0","invalid"))throw new Exception("Invalid handoff accepted.");
      CheckHandoff(current,true);CheckHandoff(current,false);CheckUnapprovedExit(current);CheckWrongIdentity(current);
    }
    static Process Parent(string current,string channel) {
      return Process.Start(new ProcessStartInfo {FileName=current,Arguments="--latest-probe-parent "+channel,UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(current)});
    }
    static void CheckHandoff(string current,bool complete) {
      string channel=@"Local\CorbieUpgradeProbe_"+Guid.NewGuid().ToString("N");
      string report=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".handoff-probe-"+Guid.NewGuid().ToString("N")+".txt");
      using(var finish=new EventWaitHandle(false,EventResetMode.ManualReset,channel))
      using(var parent=Parent(current,channel)) {
        try {
          using(var handoff=LatestVersion.Begin(current,parent,report)) {
            if(!handoff.WaitUntilReady().GetAwaiter().GetResult())throw new Exception("Replacement helper didn't become ready.");
            if(File.Exists(report) || parent.HasExited)throw new Exception("Replacement started before parent shutdown.");
            if(complete) {handoff.Approve();finish.Set();if(!handoff.WaitForChildExit(5000) || !File.Exists(report))throw new Exception("Replacement didn't proceed after parent shutdown.");handoff.Commit();}
            else {handoff.Cancel();if(!handoff.WaitForChildExit(3000) || File.Exists(report) || parent.HasExited)throw new Exception("Cancelled handoff disrupted its parent or started a replacement.");}
          }
        } finally {finish.Set();parent.WaitForExit(3000);if(File.Exists(report))File.Delete(report);}
      }
    }
    static void CheckUnapprovedExit(string current) {
      string channel=@"Local\CorbieUpgradeProbe_"+Guid.NewGuid().ToString("N");
      string report=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".handoff-probe-"+Guid.NewGuid().ToString("N")+".txt");
      using(var finish=new EventWaitHandle(false,EventResetMode.ManualReset,channel))
      using(var parent=Parent(current,channel)) {
        try {
          using(var handoff=LatestVersion.Begin(current,parent,report)) {
            if(!handoff.WaitUntilReady().GetAwaiter().GetResult())throw new Exception("Unapproved helper wasn't ready.");
            finish.Set();if(!handoff.WaitForChildExit(3000) || File.Exists(report))throw new Exception("Parent exit without approval started a replacement.");
          }
        } finally {finish.Set();parent.WaitForExit(3000);if(File.Exists(report))File.Delete(report);}
      }
    }
    static void CheckWrongIdentity(string current) {
      string channel=@"Local\CorbieUpgrade_"+Guid.NewGuid().ToString("N");
      using(var ready=new EventWaitHandle(false,EventResetMode.ManualReset,channel))
      using(var cancel=new EventWaitHandle(false,EventResetMode.ManualReset,channel+"_Cancel"))
      using(var approved=new EventWaitHandle(false,EventResetMode.ManualReset,channel+"_Approved"))
      using(var parent=Process.GetCurrentProcess()) {
        if(LatestVersion.WaitForParent(parent.Id.ToString(),(parent.StartTime.ToUniversalTime().Ticks+1).ToString(),channel) || ready.WaitOne(0))throw new Exception("Wrong process identity was accepted.");
      }
    }
  }
}
