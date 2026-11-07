using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;

namespace CorbiePet {
  public partial class Pet {
    TextBox browserTaskBox;
    TextBlock browserStatus;
    Button browserStartButton;
    Button browserResumeButton;
    Button browserStopButton;
    bool browserAwaitingUser;
    string browserLastStatus="Ready. Corbie opens a separate Edge window for browser tasks.";
    Process browserAgentProcess;
    readonly object browserWriteLock=new object();
    int browserRunNumber;

    void BuildBrowserTab() {
      var body=TabBody();
      browserTaskBox=new TextBox { MinHeight=88,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,
        VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxLength=800,
        Text="Find a page about raven behavior and give me a short summary." };
      browserStatus=Status();
      browserStatus.Text=browserLastStatus;
      browserStartButton=ActionButton("Send Corbie browsing",()=> { var ignored=RunBrowserTask(browserTaskBox.Text); });
      browserResumeButton=ActionButton("Continue after I handle it",ResumeBrowserTask);
      browserResumeButton.IsEnabled=false;
      browserStopButton=ActionButton("Stop browsing",StopBrowserTask);
      browserStopButton.IsEnabled=false;
      body.Children.Add(Section("Give Corbie a browser task",
        new TextBlock { Text="Tell him what to find or do. He will use his own Edge window and ask before typing, pressing buttons, or taking other actions that may change a page.",
          Foreground=MutedText,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,9) },
        browserTaskBox,new TextBlock { Text="You can give him free rein: ‘On Temu, pick up to $100 of whatever you like. Show me your choices; don't check out.’",
          Foreground=MutedText,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,7,0,4) },
        Actions(browserStartButton,browserResumeButton,browserStopButton),browserStatus));
      body.Children.Add(Section("While he works",
        new TextBlock { Text="If he reaches a login, verification, or other roadblock, finish it in Edge and choose Continue. The browser stays open until you choose Stop browsing. Check his result before relying on it.",
          Foreground=MutedText,TextWrapping=TextWrapping.Wrap }));
      AddTab("Browser",body);
      if(browserAgentProcess!=null && !browserAgentProcess.HasExited) BrowserUi(browserLastStatus,true,browserAwaitingUser);
    }
    void BrowserUi(string message,bool running,bool awaitingUser=false) {
      browserLastStatus=message;
      browserAwaitingUser=awaitingUser;
      if(browserStatus!=null) browserStatus.Text=message;
      if(browserStartButton!=null) browserStartButton.IsEnabled=!running;
      if(browserResumeButton!=null) browserResumeButton.IsEnabled=awaitingUser;
      if(browserStopButton!=null) browserStopButton.IsEnabled=running;
    }
    static string BrowserField(Dictionary<string,object> data,string key) {
      object value; return data!=null && data.TryGetValue(key,out value) && value!=null?Convert.ToString(value):"";
    }
    void SendBrowserCommand(object command) {
      lock(browserWriteLock) {
        try {
          var process=browserAgentProcess;
          if(process!=null && !process.HasExited) {
            process.StandardInput.WriteLine(new JavaScriptSerializer().Serialize(command));
            process.StandardInput.Flush();
          }
        } catch { }
      }
    }
    async Task<string> RunBrowserTask(string task) {
      task=(task??"").Trim();
      if(task.Length==0 || task.Length>800) { BrowserUi("Give him one short browser task (up to 800 characters).",false); return "Browser task needs a description."; }
      if(browserAgentProcess!=null && !browserAgentProcess.HasExited) { BrowserUi("Corbie is already browsing. Stop that task first.",true); return "Already browsing."; }
      string root=AppDomain.CurrentDomain.BaseDirectory;
      string agent=Path.Combine(root,"browser-agent","agent.mjs");
      string node=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"nodejs","node.exe");
      if(!File.Exists(node)) node="node.exe";
      if(!File.Exists(agent) || !Directory.Exists(Path.Combine(root,"browser-agent","node_modules"))) {
        BrowserUi("Browser helper is missing. Keep the browser-agent folder beside Corbie's EXE.",false);
        return "Browser helper missing.";
      }
      int run=++browserRunNumber;
      var process=new Process();
      process.StartInfo=new ProcessStartInfo {
        FileName=node,Arguments="\""+agent+"\"",WorkingDirectory=Path.GetDirectoryName(agent),
        UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,
        StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8
      };
      try { process.Start(); }
      catch(Exception ex) { BrowserUi("Could not start the browser helper: "+ex.Message,false); process.Dispose(); return "Browser helper failed to start."; }
      browserAgentProcess=process;
      BrowserUi("Corbie is opening his browser…",true);
      SendBrowserCommand(new { type="run",task=task });
      string outcome="Browser task ended without a result.";
      var errors=process.StandardError.ReadToEndAsync();
      try {
        while(true) {
          string line=await process.StandardOutput.ReadLineAsync();
          if(line==null) break;
          Dictionary<string,object> message;
          try { message=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(line); } catch { continue; }
          string kind=BrowserField(message,"type"),detail=BrowserField(message,"message");
          if(run!=browserRunNumber || shuttingDown) continue;
          if(kind=="status") BrowserUi(detail,true);
          else if(kind=="waiting") {
            outcome=detail;
            BrowserUi(detail+"\n\nThe Edge window is still open. Handle anything needed there, then choose Continue. Stop browsing closes this task.",true,true);
            companion.SaySilent(detail);
          }
          else if(kind=="approval") {
            string id=BrowserField(message,"id");
            string action=BrowserField(message,"action");
            string parameters=BrowserField(message,"detail");
            var choice=MessageBox.Show(corbiePanel??(Window)this,
              detail+"\n\nAction: "+action+"\nDetails: "+parameters+"\n\nAllow this one action?",
              "Corbie needs your OK",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No);
            SendBrowserCommand(new { type="approve",id=id,yes=choice==MessageBoxResult.Yes });
          } else if(kind=="done" || kind=="error") {
            outcome=detail;
            BrowserUi(detail,false);
          }
        }
        await errors;
      } catch(Exception ex) { outcome="Browser task failed: "+ex.Message; }
      finally {
        if(browserAgentProcess==process) browserAgentProcess=null;
        try { process.Dispose(); } catch { }
        if(run==browserRunNumber && !shuttingDown) BrowserUi(outcome,false);
        else if(!shuttingDown) BrowserUi("Browser task stopped.",false);
      }
      if(run==browserRunNumber && !shuttingDown && !string.IsNullOrWhiteSpace(outcome)) companion.SaySilent(outcome);
      return outcome;
    }
    void ResumeBrowserTask() {
      if(!browserAwaitingUser || browserAgentProcess==null) return;
      BrowserUi("Corbie is checking the page again…",true);
      SendBrowserCommand(new { type="resume" });
    }
    void StopBrowserTask() {
      var process=browserAgentProcess;
      if(process==null) return;
      ++browserRunNumber;
      SendBrowserCommand(new { type="cancel" });
      BrowserUi("Stopping browser task…",true);
    }
  }
}
