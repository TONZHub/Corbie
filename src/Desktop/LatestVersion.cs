using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Forms=System.Windows.Forms;

namespace CorbiePet {
  public sealed class CorbieRelease {
    public string Path {get;private set;}
    public Version Version {get;private set;}
    public CorbieRelease(string path,Version version) {Path=path;Version=version;}
  }
  public sealed class VersionHandoff : IDisposable {
    readonly EventWaitHandle ready,cancel,approved;
    readonly Process child;
    bool committed;
    internal VersionHandoff(string executable,Process parent,string probeReport=null) {
      string channel=@"Local\CorbieUpgrade_"+Guid.NewGuid().ToString("N");
      ready=new EventWaitHandle(false,EventResetMode.ManualReset,channel);
      cancel=new EventWaitHandle(false,EventResetMode.ManualReset,channel+"_Cancel");
      approved=new EventWaitHandle(false,EventResetMode.ManualReset,channel+"_Approved");
      try {
        string args="--version-handoff "+parent.Id+" "+parent.StartTime.ToUniversalTime().Ticks+" "+channel;
        if(probeReport!=null)args+=" --handoff-probe-report \""+probeReport+"\"";
        child=Process.Start(new ProcessStartInfo {FileName=executable,Arguments=args,WorkingDirectory=System.IO.Path.GetDirectoryName(executable),UseShellExecute=false});
        if(child==null)throw new InvalidOperationException("The new build did not start.");
      } catch {ready.Dispose();cancel.Dispose();approved.Dispose();throw;}
    }
    public Task<bool> WaitUntilReady() {return Task.Run(()=>ready.WaitOne(TimeSpan.FromSeconds(5)) && !child.HasExited);}
    public void Approve() {approved.Set();}
    public void Commit() {committed=true;}
    internal void Cancel() {cancel.Set();}
    internal bool WaitForChildExit(int milliseconds) {return child.WaitForExit(milliseconds);}
    public void Dispose() {if(!committed)cancel.Set();ready.Dispose();cancel.Dispose();approved.Dispose();child.Dispose();}
  }
  public static class LatestVersion {
    static readonly Regex ReleaseName=new Regex(@"^Corbie-v([0-9]+)\.([0-9]+)(?:\.([0-9]+))?\.exe$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    public static Version Parse(string file) {
      var match=ReleaseName.Match(Path.GetFileName(file));if(!match.Success)return null;
      int major,minor,patch=0;
      if(!int.TryParse(match.Groups[1].Value,out major) || !int.TryParse(match.Groups[2].Value,out minor) || match.Groups[3].Success && !int.TryParse(match.Groups[3].Value,out patch))return null;
      return new Version(major,minor,patch);
    }
    public static CorbieRelease Find(string directory) {
      CorbieRelease best=null;
      foreach(string file in Directory.EnumerateFiles(directory,"Corbie-v*.exe",SearchOption.TopDirectoryOnly)) {
        Version version=Parse(file);if(version==null || best!=null && version.CompareTo(best.Version)<=0)continue;
        try {if(new FileInfo(file).Length==0)continue;AssemblyName.GetAssemblyName(file);}catch(Exception) {continue;}
        best=new CorbieRelease(Path.GetFullPath(file),version);
      }
      return best;
    }
    public static VersionHandoff Begin(string executable,Process parent,string probeReport=null) {return new VersionHandoff(executable,parent,probeReport);}
    public static bool WaitForParent(string pidText,string ticksText,string channel) {
      int pid;long ticks;
      if(!int.TryParse(pidText,out pid) || pid<=0 || !long.TryParse(ticksText,out ticks) || ticks<=0
        || !Regex.IsMatch(channel,@"^Local\\CorbieUpgrade_[a-f0-9]{32}$",RegexOptions.CultureInvariant))return false;
      try {
        using(var parent=Process.GetProcessById(pid))
        using(var ready=EventWaitHandle.OpenExisting(channel))
        using(var cancel=EventWaitHandle.OpenExisting(channel+"_Cancel"))
        using(var approved=EventWaitHandle.OpenExisting(channel+"_Approved")) {
          if(parent.Id==Process.GetCurrentProcess().Id || parent.StartTime.ToUniversalTime().Ticks!=ticks)return false;
          string parentFile=parent.MainModule.FileName,current=Process.GetCurrentProcess().MainModule.FileName;
          if(Parse(parentFile)==null || !string.Equals(Path.GetDirectoryName(parentFile),Path.GetDirectoryName(current),StringComparison.OrdinalIgnoreCase))return false;
          ready.Set();var wait=Stopwatch.StartNew();
          while(wait.Elapsed.TotalSeconds<20) {
            if(cancel.WaitOne(0))return false;
            if(parent.WaitForExit(100))return approved.WaitOne(0) && !cancel.WaitOne(0);
          }
        }
      } catch(Exception) {return false;}
      return false;
    }
  }

  public partial class Pet {
    bool versionSwitching;
    MenuItem latestVersionItem;
    Forms.ToolStripMenuItem trayLatestVersion;
    async void LaunchLatestVersion() {
      if(versionSwitching || shuttingDown || farewellSaid)return;
      if(lessonNotebook!=null && lessonNotebook.IsRecording) {companion.SaySilent("Finish the lesson recording first. I'm not leaving your notes halfway through.");return;}
      try {
        string current=Process.GetCurrentProcess().MainModule.FileName;
        var latest=LatestVersion.Find(AppDomain.CurrentDomain.BaseDirectory);
        if(latest==null) {companion.SaySilent("I couldn't find a runnable Corbie build beside me.");return;}
        Version running=LatestVersion.Parse(current);
        if(string.Equals(latest.Path,current,StringComparison.OrdinalIgnoreCase) || running!=null && latest.Version.CompareTo(running)<=0) {companion.SaySilent("Already wearing the newest feathers: "+Path.GetFileName(current)+".");return;}
        versionSwitching=true;latestVersionItem.IsEnabled=trayLatestVersion.Enabled=false;
        companion.SaySilent("Trying on the newest feathers: "+Path.GetFileName(latest.Path)+"...");
        using(var parent=Process.GetCurrentProcess())
        using(var handoff=LatestVersion.Begin(latest.Path,parent)) {
          if(!await handoff.WaitUntilReady())throw new InvalidOperationException("The newer build didn't confirm it was ready. This copy is still here.");
          if(shuttingDown || farewellSaid)return;
          if(lessonNotebook!=null && lessonNotebook.IsRecording)throw new InvalidOperationException("Finish the lesson recording, then switch versions.");
          handoff.Approve();
          Close();
          if(shuttingDown)handoff.Commit();
          else throw new InvalidOperationException("Closing was cancelled. This copy is still here.");
        }
      } catch(Exception ex) {if(!shuttingDown)companion.SaySilent("Couldn't switch versions: "+ex.Message);}
      finally {versionSwitching=false;if(!shuttingDown)latestVersionItem.IsEnabled=trayLatestVersion.Enabled=true;}
    }
    void CheckLatestVersionMenu() {
      if(latestVersionItem==null || !ContextMenu.Items.Contains(latestVersionItem) || trayLatestVersion==null || !tray.ContextMenuStrip.Items.Contains(trayLatestVersion)
        || TrayItemKeepsMenuOpen("Launch latest version"))throw new Exception("Latest-version entry was missing from the live menus or kept the tray open.");
    }
  }
}
