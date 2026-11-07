using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CorbiePet {
  public partial class Pet {
    readonly DispatcherTimer securityTimer=new DispatcherTimer { Interval=TimeSpan.FromMinutes(5) };
    SecurityScout.Health? lastSecurityHealth;
    TextBlock panelSecurityStatus,panelScanStatus;
    Button panelQuickScan;
    bool securityScanRunning;
    readonly VirusTotalLookup virusTotal=new VirusTotalLookup();
    PasswordBox panelVirusTotalKey;
    TextBlock panelVirusTotalKeyStatus,panelVirusTotalStatus;
    Button panelVirusTotalLookup,panelVirusTotalCancel,panelVirusTotalReport;
    CancellationTokenSource virusTotalRequest;
    VirusTotalReport lastVirusTotalReport;

    void StartSecurityWatch() {
      securityTimer.Tick+=(s,e)=>RefreshSecurityHealth();
      securityTimer.Start();
      RefreshSecurityHealth();
    }
    void RefreshSecurityHealth() {
      var health=SecurityScout.ReadHealth();
      bool newWarning=SecurityScout.NeedsUserAttention(health) && (!lastSecurityHealth.HasValue || lastSecurityHealth.Value!=health);
      lastSecurityHealth=health;
      UpdateSecurityPanel();
      if(newWarning && !shuttingDown && !testing && (lessonNotebook==null || !lessonNotebook.IsRecording))
        companion.SaySilent("Security lookout: "+SecurityScout.Describe(health));
    }
    void UpdateSecurityPanel() {
      if(panelSecurityStatus!=null)
        panelSecurityStatus.Text=lastSecurityHealth.HasValue?SecurityScout.Describe(lastSecurityHealth.Value):"Checking Windows antivirus status...";
    }
    async void StartQuickScan() {
      if(securityScanRunning) return;
      securityScanRunning=true;
      if(panelQuickScan!=null) panelQuickScan.IsEnabled=false;
      if(panelScanStatus!=null) panelScanStatus.Text="Requesting Windows administrator approval for a Defender quick scan...";
      try {
        await SecurityScout.RunDefenderQuickScan();
        if(panelScanStatus!=null) panelScanStatus.Text="Defender's quick-scan command completed. Review results and protection history in Windows Security.";
      } catch(Win32Exception ex) {
        if(panelScanStatus!=null) panelScanStatus.Text=ex.NativeErrorCode==1223?"Quick scan was canceled at the Windows approval prompt.":"Defender quick scan could not start: "+ex.Message;
      } catch(Exception ex) {
        if(panelScanStatus!=null) panelScanStatus.Text="Defender quick scan failed: "+ex.Message;
      } finally {
        securityScanRunning=false;
        if(panelQuickScan!=null) panelQuickScan.IsEnabled=true;
        RefreshSecurityHealth();
      }
    }
    void OpenSecuritySettings() {
      try { SecurityScout.OpenWindowsSecurity(); }
      catch(Exception ex) { if(panelScanStatus!=null) panelScanStatus.Text="Windows Security could not open: "+ex.Message; }
    }
    void SaveVirusTotalKey() {
      try {
        virusTotal.SaveKey(panelVirusTotalKey.Password);
        panelVirusTotalKey.Clear();
        lastVirusTotalReport=null;
        panelVirusTotalReport.IsEnabled=false;
        panelVirusTotalKeyStatus.Text="API key saved for this Windows account.";
        panelVirusTotalStatus.Text="Choose a file to check its SHA-256 hash.";
      } catch(Exception ex) { panelVirusTotalStatus.Text="Could not save API key: "+ex.Message; }
    }
    void ForgetVirusTotalKey() {
      try {
        if(virusTotalRequest!=null) virusTotalRequest.Cancel();
        virusTotal.ForgetKey();
        panelVirusTotalKey.Clear();
        lastVirusTotalReport=null;
        panelVirusTotalReport.IsEnabled=false;
        panelVirusTotalKeyStatus.Text="No VirusTotal API key saved.";
        panelVirusTotalStatus.Text="Saved key removed.";
      } catch(Exception ex) { panelVirusTotalStatus.Text="Could not remove API key: "+ex.Message; }
    }
    async void CheckSelectedFileWithVirusTotal() {
      if(virusTotalRequest!=null) return;
      if(!virusTotal.Configured) { panelVirusTotalStatus.Text="Save your VirusTotal API key first."; return; }
      var picker=new Microsoft.Win32.OpenFileDialog { Title="Choose one file for a VirusTotal hash lookup",CheckFileExists=true,Multiselect=false,Filter="All files|*.*" };
      if(picker.ShowDialog(corbiePanel)!=true) return;
      var request=new CancellationTokenSource();virusTotalRequest=request;lastVirusTotalReport=null;
      panelVirusTotalLookup.IsEnabled=false;panelVirusTotalCancel.IsEnabled=true;panelVirusTotalReport.IsEnabled=false;
      panelVirusTotalStatus.Text="Hashing the selected file on this PC, then looking up only its SHA-256 hash...";
      try {
        var report=await virusTotal.CheckFile(picker.FileName,request.Token);
        request.Token.ThrowIfCancellationRequested();
        lastVirusTotalReport=report;
        panelVirusTotalStatus.Text=report.Summary+"\nSHA-256: "+report.Sha256;
        panelVirusTotalReport.IsEnabled=report.Known;
        if(!shuttingDown) {
          if(report.Known && (report.Malicious>0 || report.Suspicious>0))
            companion.SaySilent("VirusTotal flagged this hash in "+(report.Malicious+report.Suspicious)+" engine result"+(report.Malicious+report.Suspicious==1?"":"s")+". Let's inspect the report before running it.");
          else if(!report.Known)
            companion.SaySilent("No VirusTotal report for that hash. Unknown means unknown, not deliciously safe.");
        }
      } catch(OperationCanceledException) { panelVirusTotalStatus.Text="VirusTotal lookup canceled. No file was uploaded."; }
      catch(Exception ex) { panelVirusTotalStatus.Text="VirusTotal lookup failed: "+ex.Message; }
      finally {
        if(virusTotalRequest==request) virusTotalRequest=null;
        request.Dispose();
        panelVirusTotalLookup.IsEnabled=true;panelVirusTotalCancel.IsEnabled=false;
      }
    }
    void OpenVirusTotalReport() {
      if(lastVirusTotalReport==null || !lastVirusTotalReport.Known) return;
      try { Process.Start(new ProcessStartInfo(lastVirusTotalReport.Url) { UseShellExecute=true }); }
      catch(Exception ex) { panelVirusTotalStatus.Text="Could not open VirusTotal report: "+ex.Message; }
    }
    void BuildSecurityTab() {
      var body=TabBody();
      panelSecurityStatus=Status();
      body.Children.Add(Section("Protection lookout",
        new TextBlock { Text="Corbie reads Windows Security's antivirus health every five minutes and alerts you if protection starts needing attention. Windows remains in charge of malware detection.",Foreground=MutedText,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8) },
        panelSecurityStatus,
        Actions(ActionButton("Refresh protection status",RefreshSecurityHealth),ActionButton("Open Windows Security",OpenSecuritySettings))));
      panelScanStatus=Status();
      panelQuickScan=ActionButton("Run Defender quick scan",StartQuickScan);
      body.Children.Add(Section("On-demand scan",
        new TextBlock { Text="Starts Microsoft's quick scan after Windows asks for administrator approval. Defender handles any detection; review its results in Windows Security.",Foreground=MutedText,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8) },
        Actions(panelQuickScan),panelScanStatus));
      panelVirusTotalKey=new PasswordBox { MinWidth=280,Margin=new Thickness(0,5,0,8),Padding=new Thickness(7),ToolTip="Your personal VirusTotal API key" };
      panelVirusTotalKeyStatus=Status();
      panelVirusTotalKeyStatus.Text=virusTotal.Configured?"API key saved for this Windows account.":"No VirusTotal API key saved.";
      panelVirusTotalStatus=Status();
      panelVirusTotalStatus.Text="Choose a file to check its SHA-256 hash. The file stays on this PC.";
      panelVirusTotalLookup=ActionButton("Check a file hash",CheckSelectedFileWithVirusTotal);
      panelVirusTotalCancel=ActionButton("Cancel lookup",()=> { if(virusTotalRequest!=null) virusTotalRequest.Cancel(); });
      panelVirusTotalCancel.IsEnabled=false;
      panelVirusTotalReport=ActionButton("Open VirusTotal report",OpenVirusTotalReport);
      panelVirusTotalReport.IsEnabled=false;
      body.Children.Add(Section("VirusTotal reputation",
        new TextBlock { Text="Optional cloud lookup. Corbie sends the file's SHA-256 hash and your API key to VirusTotal over HTTPS. He never uploads the file, though a hash can identify a known file. Unknown or unflagged is not proof of safety.",Foreground=MutedText,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8) },
        panelVirusTotalKey,Actions(ActionButton("Save API key",SaveVirusTotalKey),ActionButton("Forget key",ForgetVirusTotalKey)),
        panelVirusTotalKeyStatus,Actions(panelVirusTotalLookup,panelVirusTotalCancel,panelVirusTotalReport),panelVirusTotalStatus));
      AddTab("Security",body);
    }
  }
}
