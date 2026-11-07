using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CorbiePet {
  // Windows Security Center is the authority for antivirus health. Corbie never
  // interprets files as malware or changes Defender configuration.
  public static class SecurityScout {
    public enum Health { Healthy, NotMonitored, NeedsAttention, Snoozed, Unavailable }
    const int AntivirusProvider=0x4;
    [DllImport("wscapi.dll",ExactSpelling=true)]
    static extern int WscGetSecurityProviderHealth(int providers,out int health);
    public static Health ReadHealth() {
      try {
        int value;
        int result=WscGetSecurityProviderHealth(AntivirusProvider,out value);
        if(result!=0) return Health.Unavailable;
        switch(value) {
          case 0: return Health.Healthy;
          case 1: return Health.NotMonitored;
          case 2: return Health.NeedsAttention;
          case 3: return Health.Snoozed;
          default: return Health.Unavailable;
        }
      } catch { return Health.Unavailable; }
    }
    public static string Describe(Health health) {
      switch(health) {
        case Health.Healthy: return "Windows reports antivirus protection healthy. This does not mean every file is safe.";
        case Health.NotMonitored: return "Windows is not monitoring antivirus protection. Open Windows Security to check your provider.";
        case Health.NeedsAttention: return "Windows says antivirus protection needs attention. Open Windows Security for details.";
        case Health.Snoozed: return "Windows reports antivirus protection snoozed. Open Windows Security to restore it.";
        default: return "Antivirus status is unavailable. Open Windows Security to check protection directly.";
      }
    }
    public static bool NeedsUserAttention(Health health) { return health==Health.NeedsAttention || health==Health.Snoozed || health==Health.NotMonitored; }
    public static void OpenWindowsSecurity() {
      Process.Start(new ProcessStartInfo("ms-settings:windowsdefender") { UseShellExecute=true });
    }
    public static async Task RunDefenderQuickScan() {
      string windows=Environment.GetEnvironmentVariable("WINDIR");
      if(string.IsNullOrEmpty(windows)) throw new InvalidOperationException("Windows directory could not be found.");
      string powershell=Path.Combine(windows,"System32","WindowsPowerShell","v1.0","powershell.exe");
      if(!File.Exists(powershell)) throw new FileNotFoundException("Windows PowerShell is unavailable.",powershell);
      var info=new ProcessStartInfo(powershell,
        "-NoProfile -NonInteractive -WindowStyle Hidden -Command \"Start-MpScan -ScanType QuickScan -ErrorAction Stop\"") {
        UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden
      };
      using(var process=Process.Start(info)) {
        if(process==null) throw new InvalidOperationException("Windows did not start the Defender scan.");
        await Task.Run(()=>process.WaitForExit());
        if(process.ExitCode!=0) throw new InvalidOperationException("Defender could not complete the quick scan. Check Windows Security for details.");
      }
    }
    public static void Check() {
      if(AntivirusProvider!=4 || !NeedsUserAttention(Health.NeedsAttention) || NeedsUserAttention(Health.Healthy))
        throw new Exception("Antivirus health mapping changed.");
      if(Describe(Health.Healthy).IndexOf("every file is safe",StringComparison.Ordinal)<0)
        throw new Exception("Antivirus health must not claim a clean machine.");
    }
  }
}
