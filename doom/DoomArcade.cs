using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace CorbieArcade {
  public sealed class Choice { public string Wad { get; set; } public string Profile { get; set; } }
  public sealed class GameFile {
    public string Path,Name,Key;
    public bool Doom2,Freedoom;
    public string Engine="crispy";
    public string[] Mods=new string[0];
  }
  public static class GameFiles {
    public static GameFile Read(string path) { return ReadWad(path,false); }
    public static GameFile ReadMod(string path) { return ReadWad(path,true); }
    static GameFile ReadWad(string path,bool mod) {
      if(string.IsNullOrWhiteSpace(path) || path.StartsWith(@"\\") || path.StartsWith("//") || !System.IO.Path.IsPathRooted(path) || !string.Equals(System.IO.Path.GetExtension(path),".wad",StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("Choose a local Doom game WAD file.");
      using(var stream=File.OpenRead(path)) using(var reader=new BinaryReader(stream)) {
        if(stream.Length<12 || stream.Length>512L*1024*1024)throw new InvalidDataException("That game file is empty, incomplete, or larger than 512 MB.");
        if(Encoding.ASCII.GetString(reader.ReadBytes(4))!=(mod?"PWAD":"IWAD"))throw new InvalidDataException(mod?"This add-on must be a PWAD file.":"Choose a base game IWAD. Use Austrian Avian for the bird mod.");
        int count=reader.ReadInt32(),offset=reader.ReadInt32();
        if(count<1 || count>300000 || offset<12 || (long)offset+16L*count>stream.Length)throw new InvalidDataException("The game file has an invalid WAD directory.");
        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        stream.Position=offset;
        for(int i=0;i<count;i++) {
          int start=reader.ReadInt32(),size=reader.ReadInt32();
          string name=Encoding.ASCII.GetString(reader.ReadBytes(8)).TrimEnd('\0');
          if(start<0 || size<0 || (long)start+size>stream.Length)throw new InvalidDataException("The game file contains an invalid lump.");
          names.Add(name);
        }
        if(!mod && (!(names.Contains("E1M1")||names.Contains("MAP01")) || !new[]{"PLAYPAL","COLORMAP","PNAMES","TEXTURE1","THINGS","LINEDEFS"}.All(names.Contains)))
          throw new InvalidDataException("That does not look like a complete classic Doom base game.");
        stream.Position=0;string key;
        using(var hash=SHA256.Create())key=BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").Substring(0,16).ToLowerInvariant();
        return new GameFile { Path=System.IO.Path.GetFullPath(path),Name=System.IO.Path.GetFileName(path),Key=key,Doom2=names.Contains("MAP01"),Freedoom=names.Contains("FREEDOOM") };
      }
    }
    public static GameFile Avian(string root,string basePath) {
      if(string.IsNullOrWhiteSpace(basePath)) {
        basePath=System.IO.Path.Combine(root,"games","doom2.wad");
        if(!File.Exists(basePath))basePath=System.IO.Path.Combine(root,"games","freedoom2.wad");
      }
      var game=Read(basePath);
      if(!game.Doom2)throw new InvalidDataException("Austrian Avian needs Doom II or Freedoom Phase 2 as its base game.");
      var mod=ReadMod(System.IO.Path.Combine(root,"games","AUAVIAN.wad"));
      if(!mod.Doom2)throw new InvalidDataException("AUAVIAN.wad is missing its first level. Check the mod download.");
      var mods=new List<GameFile>{mod};
      string wide=System.IO.Path.Combine(root,"games","AAA_WS.wad");
      if(File.Exists(wide))mods.Add(ReadMod(wide));
      string identity="woof-avian-v1:"+game.Key+":"+string.Join(":",mods.Select(m=>m.Key).ToArray());
      using(var hash=SHA256.Create())game.Key=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-","").Substring(0,16).ToLowerInvariant();
      game.Name="Austrian Avian Association";game.Engine="woof";game.Mods=mods.Select(m=>m.Path).ToArray();return game;
    }
    public static string EnginePath(GameFile game,string root) {
      return System.IO.Path.Combine(root,game.Engine=="woof"?"woof-runtime":"runtime",game.Engine=="woof"?"woof.exe":"crispy-doom.exe");
    }
    public static string Quote(string value) {
      // Windows command-line quoting for ProcessStartInfo with UseShellExecute=false.
      if(value.IndexOf('\0')>=0 || value.IndexOf('"')>=0 || value.IndexOf('\r')>=0 || value.IndexOf('\n')>=0)throw new InvalidDataException("Invalid game path.");
      return "\""+value.TrimEnd('\\')+"\"";
    }
    public static string Arguments(GameFile game,string root) {
      string state=System.IO.Path.Combine(root,"state",game.Key);
      if(game.Engine=="woof")return "-iwad "+Quote(game.Path)+" -file "+string.Join(" ",game.Mods.Select(Quote).ToArray())+
        " -window -nograbmouse -nogui -noautoload -complevel mbf -config "+Quote(System.IO.Path.Combine(state,"woof.cfg"))+" -save "+Quote(System.IO.Path.Combine(state,"saves"));
      return "-iwad "+Quote(game.Path)+" -window -geometry 800x600 -nograbmouse -nogui -config "+Quote(System.IO.Path.Combine(state,"doom.cfg"))+
        " -extraconfig "+Quote(System.IO.Path.Combine(state,"crispy.cfg"))+" -savedir "+Quote(System.IO.Path.Combine(state,"saves"));
    }
    public static void Prepare(GameFile game,string root) {
      string state=System.IO.Path.Combine(root,"state",game.Key);Directory.CreateDirectory(System.IO.Path.Combine(state,"saves"));
      if(game.Engine=="woof") {
        string config=System.IO.Path.Combine(state,"woof.cfg");
        if(!File.Exists(config))File.WriteAllText(config,"fullscreen 0\nwindow_width 800\nwindow_height 600\ngrabmouse 0\n",Encoding.ASCII);
        return;
      }
      string extra=System.IO.Path.Combine(state,"crispy.cfg");
      if(!File.Exists(extra))File.WriteAllText(extra,"fullscreen 0\nscreen_width 800\nscreen_height 600\ngrabmouse 0\n",Encoding.ASCII);
    }
    public static GameFile Selected(string root) {
      string config=System.IO.Path.Combine(root,"choice.json");
      if(File.Exists(config)) {
        var info=new FileInfo(config);if(info.Length>10000)throw new InvalidDataException("Corbie's game choice file is damaged. Choose a game again.");
        var choice=new JavaScriptSerializer().Deserialize<Choice>(File.ReadAllText(config));
        if(choice!=null && choice.Profile=="avian")return Avian(root,choice.Wad);
        if(choice!=null && !string.IsNullOrWhiteSpace(choice.Wad))return Read(choice.Wad);
      }
      string starter=System.IO.Path.Combine(root,"games","freedoom1.wad");
      return File.Exists(starter)?Read(starter):null;
    }
    public static void Choose(GameFile game,string root) {
      Directory.CreateDirectory(root);
      File.WriteAllText(System.IO.Path.Combine(root,"choice.json"),new JavaScriptSerializer().Serialize(new Choice { Wad=game.Path,Profile=game.Engine=="woof"?"avian":"classic" }));
    }
  }

  public sealed class Arcade : Form {
    readonly string root=AppDomain.CurrentDomain.BaseDirectory;
    readonly Panel screen=new Panel();
    readonly Label status=new Label(),placeholder=new Label();
    readonly Button play=new Button(),pick=new Button(),avian=new Button(),install=new Button(),pop=new Button();
    readonly Timer timer=new Timer { Interval=200 };
    Process game,setup;
    GameFile selected;
    IntPtr child;
    int oldStyle;
    bool embedded,closing;
    DateTime started;
    readonly StringBuilder logs=new StringBuilder();
    public Arcade() {
      Text="Corbie's Arcade - Doom";ClientSize=new Size(920,780);MinimumSize=new Size(640,580);
      BackColor=Color.FromArgb(20,24,35);ForeColor=Color.FromArgb(238,232,217);Font=new Font("Segoe UI",10);StartPosition=FormStartPosition.CenterScreen;
      var layout=new TableLayoutPanel { Dock=DockStyle.Fill,RowCount=5,ColumnCount=1,Padding=new Padding(18) };
      layout.RowStyles.Add(new RowStyle(SizeType.Absolute,68));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,52));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));
      var header=new Label { Dock=DockStyle.Fill,Text="CORBIE'S ARCADE\nYes. The bird runs Doom.",Font=new Font("Segoe UI",15,FontStyle.Bold),ForeColor=Color.FromArgb(233,197,140) };
      var actions=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=true,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink };
      Style(play,"Play",()=>StartGame());Style(pick,"Classic Doom...",ChooseGame);Style(avian,"Austrian Avian",ChooseAvian);Style(install,"Install starter pack",InstallStarter);Style(pop,"Separate window",PopOut);
      actions.Controls.AddRange(new Control[]{play,pick,avian,install,pop});pop.Enabled=false;
      screen.BackColor=Color.Black;screen.Dock=DockStyle.Fill;screen.Margin=new Padding(0,8,0,8);
      placeholder.Dock=DockStyle.Fill;placeholder.TextAlign=ContentAlignment.MiddleCenter;placeholder.ForeColor=Color.FromArgb(190,190,200);placeholder.Font=new Font("Segoe UI",13);
      screen.Controls.Add(placeholder);screen.Resize+=(s,e)=>ResizeGame();
      status.Dock=DockStyle.Fill;status.Padding=new Padding(0,7,0,0);
      var help=new Label { Dock=DockStyle.Fill,Text="Click the game to focus. Arrows move | Ctrl fires | Space uses | Shift runs\nEsc: game menu | F2: save | F3: load | Alt+Tab: leave the game",ForeColor=Color.FromArgb(185,190,204),Font=new Font("Segoe UI",9) };
      layout.Controls.Add(header,0,0);layout.Controls.Add(actions,0,1);layout.Controls.Add(screen,0,2);layout.Controls.Add(status,0,3);layout.Controls.Add(help,0,4);Controls.Add(layout);
      timer.Tick+=(s,e)=>Poll();timer.Start();Load+=(s,e)=>RefreshChoice();FormClosing+=ClosingArcade;
    }
    void Style(Button button,string title,Action action) {
      button.Text=title;button.AutoSize=true;button.Height=36;button.FlatStyle=FlatStyle.Flat;button.BackColor=Color.FromArgb(40,48,65);button.ForeColor=ForeColor;
      button.Padding=new Padding(10,3,10,3);button.Margin=new Padding(0,0,8,0);button.Click+=(s,e)=>action();
    }
    bool Alive(Process process) { try{return process!=null&&!process.HasExited;}catch{return false;} }
    void RefreshChoice() {
      try{selected=GameFiles.Selected(root);}catch(Exception ex){selected=null;status.Text=ex.Message;}
      bool engine=selected!=null&&File.Exists(GameFiles.EnginePath(selected,root));
      placeholder.Text=selected==null?"One tiny raven. One very questionable arcade.\n\nInstall the free starter pack, or choose your own Doom WAD.":
        (selected.Engine=="woof"?"Austrian Avian Association\nBase: "+System.IO.Path.GetFileName(selected.Path):(selected.Freedoom?"Freedoom starter game":"Doom game file")+"\n"+selected.Name)+"\n\nPress Play. Save frequently. I make no promises about your aim.";
      play.Enabled=engine&&selected!=null&&!Alive(game)&&!Alive(setup);pick.Enabled=!Alive(game)&&!Alive(setup);install.Enabled=!Alive(game)&&!Alive(setup);
      avian.Enabled=!Alive(game)&&!Alive(setup);install.Text=selected!=null&&selected.Engine=="woof"?"Install Avian engine":"Install starter pack";
      if(selected!=null&&!engine)status.Text=selected.Engine=="woof"?"Install Avian engine to add the compatible Woof! engine.":"Engine not installed yet. Install the starter pack.";
      else if(selected!=null)status.Text="Ready: "+selected.Name+(selected.Engine=="woof"?" (Woof!; "+System.IO.Path.GetFileName(selected.Path)+").":".")+" Saves stay in Corbie's arcade folder.";
    }
    void ChooseAvian() {
      try {var read=GameFiles.Avian(root,selected!=null&&selected.Doom2?selected.Path:null);GameFiles.Choose(read,root);RefreshChoice();}
      catch(Exception ex){MessageBox.Show(this,"Keep AUAVIAN.wad and optional AAA_WS.wad in doom/games.\n\n"+ex.Message,"Austrian Avian",MessageBoxButtons.OK,MessageBoxIcon.Information);}
    }
    void ChooseGame() {
      using(var dialog=new OpenFileDialog { Title="Choose a classic Doom base game (then choose Austrian Avian to use a Doom II base with that mod)",Filter="Doom base game (*.wad)|*.wad",CheckFileExists=true,Multiselect=false }) {
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try{var read=GameFiles.Read(dialog.FileName);GameFiles.Choose(read,root);RefreshChoice();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Not a supported game file",MessageBoxButtons.OK,MessageBoxIcon.Information);}
      }
    }
    void InstallStarter() {
      string script=System.IO.Path.Combine(root,selected!=null&&selected.Engine=="woof"?"Setup-Avian.ps1":"Setup-Doom.ps1");
      if(!File.Exists(script)){status.Text="Keep the doom folder and its setup script beside Corbie-Doom.cmd.";return;}
      try {
        logs.Clear();setup=Process.Start(new ProcessStartInfo("powershell.exe","-NoProfile -NonInteractive -ExecutionPolicy RemoteSigned -File "+GameFiles.Quote(script)) {
          UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true
        });
        setup.OutputDataReceived+=SetupLog;setup.ErrorDataReceived+=SetupLog;setup.BeginOutputReadLine();setup.BeginErrorReadLine();
        play.Enabled=pick.Enabled=avian.Enabled=install.Enabled=false;status.Text="Downloading the official game engine. No administrator access needed.";
      }catch(Exception ex){status.Text="Setup could not start: "+ex.Message;}
    }
    void SetupLog(object sender,DataReceivedEventArgs e) {
      if(string.IsNullOrWhiteSpace(e.Data)||closing)return;
      try{BeginInvoke(new Action(()=>{if(!closing)status.Text=e.Data.Length>180?e.Data.Substring(0,180):e.Data;}));}catch{}
    }
    void StartGame() {
      if(Alive(game)||Alive(setup))return;
      try {
        selected=GameFiles.Selected(root);if(selected==null)throw new InvalidDataException("Choose a game or install the starter pack first.");
        GameFiles.Prepare(selected,root);logs.Clear();child=IntPtr.Zero;embedded=false;
        var start=new ProcessStartInfo(GameFiles.EnginePath(selected,root),GameFiles.Arguments(selected,root)) {
          UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=System.IO.Path.GetDirectoryName(GameFiles.EnginePath(selected,root)),RedirectStandardOutput=true,RedirectStandardError=true
        };
        game=Process.Start(start);game.OutputDataReceived+=GameLog;game.ErrorDataReceived+=GameLog;game.BeginOutputReadLine();game.BeginErrorReadLine();
        started=DateTime.UtcNow;play.Enabled=pick.Enabled=avian.Enabled=install.Enabled=false;status.Text="Starting "+selected.Name+"...";
      }catch(Exception ex){RefreshChoice();status.Text="Doom could not start: "+ex.Message;}
    }
    void GameLog(object sender,DataReceivedEventArgs e) {if(e.Data!=null)lock(logs){logs.AppendLine(e.Data);if(logs.Length>6000)logs.Remove(0,logs.Length-4000);}}
    void Poll() { try{PollCore();}catch(InvalidOperationException){status.Text="The game window changed while starting. Close the game and try Play again.";} }
    void PollCore() {
      if(setup!=null&&!Alive(setup)) {
        int code=setup.ExitCode;setup.WaitForExit();setup.Dispose();setup=null;RefreshChoice();
        if(code!=0)status.Text="Engine setup failed. Check your connection and retry the install button.";
      }
      if(game==null)return;
      if(!Alive(game)) {
        int code=game.ExitCode;game.WaitForExit();game.Dispose();game=null;child=IntPtr.Zero;embedded=false;pop.Enabled=false;placeholder.Visible=true;RefreshChoice();
        if(code!=0){string detail;lock(logs)detail=logs.ToString();status.Text="Doom stopped (code "+code+"). "+(detail.Length>180?detail.Substring(detail.Length-180):detail);}
        else status.Text="Game closed. You can play again; saves are kept.";
        return;
      }
      if(child!=IntPtr.Zero)return;
      game.Refresh();IntPtr window=game.MainWindowHandle;
      if(window!=IntPtr.Zero) {
        child=window;oldStyle=Native.GetWindowLong(window,-16);
        Native.SetWindowLong(window,-16,(oldStyle&~unchecked((int)0x80CF0000))|0x40000000);
        Native.SetParent(window,screen.Handle);
        if(Native.GetParent(window)==screen.Handle) {
          embedded=true;placeholder.Visible=false;pop.Enabled=true;ResizeGame();status.Text="Click inside the game. The raven accepts no responsibility for demons.";
        } else {
          Native.SetWindowLong(window,-16,oldStyle);Native.SetParent(window,IntPtr.Zero);Native.ShowWindow(window,5);
          status.Text="Windows kept Doom in its own window. You can still play; close it to return here.";
        }
      } else if((DateTime.UtcNow-started).TotalSeconds>25) {
        status.Text="Doom has not opened a game window. Close this arcade to stop this launch, then try again.";
      }
    }
    void ResizeGame() {if(embedded&&child!=IntPtr.Zero)Native.SetWindowPos(child,IntPtr.Zero,0,0,screen.ClientSize.Width,screen.ClientSize.Height,0x0034);}
    void PopOut() {
      if(!embedded||child==IntPtr.Zero)return;
      Native.SetParent(child,IntPtr.Zero);
      if(Native.GetParent(child)==screen.Handle){status.Text="Windows could not separate the window; Doom is still here.";return;}
      Native.SetWindowLong(child,-16,oldStyle);Native.SetWindowPos(child,IntPtr.Zero,120,120,820,660,0x0034);
      Native.SetWindowText(child,"Corbie's Arcade - "+selected.Name);Native.ShowWindow(child,5);embedded=false;pop.Enabled=false;placeholder.Visible=true;
      status.Text="Doom is in a separate window. Close its game window to return to the arcade.";
    }
    void ClosingArcade(object sender,FormClosingEventArgs e) {
      if(Alive(setup)){MessageBox.Show(this,"Let the starter download finish before closing.","Corbie's Arcade");e.Cancel=true;return;}
      if(Alive(game)&&MessageBox.Show(this,"Close Doom too? Any unsaved progress will be lost.","Close Corbie's arcade?",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes){e.Cancel=true;return;}
      closing=true;timer.Stop();
      // Only this arcade's own child process is stopped, never other Doom sessions.
      if(Alive(game))try{game.Kill();game.WaitForExit(2000);}catch{}
      if(game!=null)game.Dispose();timer.Dispose();
    }
  }
  internal static class Native {
    [DllImport("user32.dll")]internal static extern IntPtr SetParent(IntPtr child,IntPtr parent);
    [DllImport("user32.dll")]internal static extern IntPtr GetParent(IntPtr child);
    [DllImport("user32.dll")]internal static extern int GetWindowLong(IntPtr window,int index);
    [DllImport("user32.dll")]internal static extern int SetWindowLong(IntPtr window,int index,int value);
    [DllImport("user32.dll")]internal static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int w,int h,uint flags);
    [DllImport("user32.dll")]internal static extern bool ShowWindow(IntPtr window,int command);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]internal static extern bool SetWindowText(IntPtr window,string text);
  }
  public static class Program {
    [STAThread] public static int Main(string[] args) {
      if(args.Length==1&&args[0]=="--player") {
        try {
          string root=AppDomain.CurrentDomain.BaseDirectory;
          string python=System.IO.Path.Combine(root,"agent-env","Scripts","python.exe");
          if(!File.Exists(python))throw new FileNotFoundException("Run Setup-Corbie-Doom-Player.cmd first.");
          Process.Start(new ProcessStartInfo(python,"-B "+GameFiles.Quote(System.IO.Path.Combine(root,"player.py"))) {UseShellExecute=false,CreateNoWindow=false,WorkingDirectory=root});return 0;
        }catch(Exception ex){MessageBox.Show(ex.Message,"Corbie Doom player");return 1;}
      }
      if(args.Length==3&&args[0]=="--inspect-avian") {
        try {string root=System.IO.Path.GetFullPath(args[1]);var read=GameFiles.Avian(root,null);File.WriteAllText(args[2],new JavaScriptSerializer().Serialize(new {Game=read,Engine=GameFiles.EnginePath(read,root),Arguments=GameFiles.Arguments(read,root)}));return 0;}
        catch(Exception ex){File.WriteAllText(args[2],"FAIL: "+ex.Message);return 1;}
      }
      if(args.Length==3&&args[0]=="--inspect") {
        try {var read=GameFiles.Read(System.IO.Path.GetFullPath(args[1]));File.WriteAllText(args[2],new JavaScriptSerializer().Serialize(read));return 0;}
        catch(Exception ex){File.WriteAllText(args[2],"FAIL: "+ex.Message);return 1;}
      }
      if(args.Length==2&&args[0]=="--check") {
        try { Check.Run(System.IO.Path.GetFullPath(args[1]));return 0; }
        catch(Exception ex){File.WriteAllText(args[1],"FAIL: "+ex);return 1;}
      }
      Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new Arcade());return 0;
    }
  }
  public static class Check {
    static void Require(bool value,string reason){if(!value)throw new Exception(reason);}
    static void Reject(Action action,string reason){bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Require(rejected,reason);}
    public static void Run(string result) {
      string root=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(result),"doom-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
      try {
        string wad=System.IO.Path.Combine(root,"test & game.wad");
        string[] names={"E1M1","PLAYPAL","COLORMAP","PNAMES","TEXTURE1","THINGS","LINEDEFS","FREEDOOM"};
        using(var writer=new BinaryWriter(File.Create(wad))) {
          writer.Write(Encoding.ASCII.GetBytes("IWAD"));writer.Write(names.Length);writer.Write(12);
          foreach(string name in names){writer.Write(0);writer.Write(0);writer.Write(Encoding.ASCII.GetBytes(name.PadRight(8,'\0')));}
        }
        var read=GameFiles.Read(wad);Require(read.Freedoom&&!read.Doom2&&read.Key.Length==16,"Game identification/hash failed.");
        GameFiles.Choose(read,root);Require(GameFiles.Selected(root).Path==read.Path,"Game choice did not persist.");
        GameFiles.Prepare(read,root);string state=System.IO.Path.Combine(root,"state",read.Key);File.WriteAllText(System.IO.Path.Combine(state,"crispy.cfg"),"my settings");
        GameFiles.Prepare(read,root);Require(File.ReadAllText(System.IO.Path.Combine(state,"crispy.cfg"))=="my settings","Existing settings were overwritten.");
        string launch=GameFiles.Arguments(read,root);Require(launch.Contains("-iwad \""+wad+"\"")&&launch.Contains("-savedir")&&!launch.Contains("-server"),"Quoted single-player launch failed.");
        Require(File.Exists(wad),"Original game was removed.");Reject(()=>GameFiles.Read(@"\\server\game.wad"),"Network path accepted.");
        Reject(()=>GameFiles.Quote("bad\"path"),"Command-line quote accepted.");
        string games=System.IO.Path.Combine(root,"games");Directory.CreateDirectory(games);
        byte[] baseBytes=File.ReadAllBytes(wad);Array.Clear(baseBytes,20,8);Array.Copy(Encoding.ASCII.GetBytes("MAP01"),0,baseBytes,20,5);
        string baseWad=System.IO.Path.Combine(games,"freedoom2.wad");File.WriteAllBytes(baseWad,baseBytes);
        byte[] modBytes=(byte[])baseBytes.Clone();modBytes[0]=(byte)'P';string modWad=System.IO.Path.Combine(games,"AUAVIAN.wad");File.WriteAllBytes(modWad,modBytes);
        var avian=GameFiles.Avian(root,null);Require(avian.Engine=="woof"&&avian.Mods.Length==1&&avian.Path==baseWad&&avian.Key!=GameFiles.Read(baseWad).Key,"Avian base, engine or isolated save identity failed.");
        GameFiles.Choose(avian,root);Require(GameFiles.Selected(root).Engine=="woof","Avian selection did not persist.");
        string avianArgs=GameFiles.Arguments(avian,root);Require(avianArgs.Contains("-file \""+modWad+"\"")&&avianArgs.Contains("-noautoload")&&avianArgs.Contains("-save ")&&!avianArgs.Contains("-extraconfig"),"Woof arguments incorrect.");
        GameFiles.Prepare(avian,root);string woofConfig=System.IO.Path.Combine(root,"state",avian.Key,"woof.cfg");File.WriteAllText(woofConfig,"preserved");GameFiles.Prepare(avian,root);Require(File.ReadAllText(woofConfig)=="preserved","Woof settings overwritten.");
        File.WriteAllBytes(System.IO.Path.Combine(games,"AAA_WS.wad"),modBytes);var widescreen=GameFiles.Avian(root,null);Require(widescreen.Mods.Length==2&&widescreen.Key!=avian.Key,"Widescreen content missing from save identity.");
        Reject(()=>GameFiles.Avian(root,wad),"Doom 1 accepted as Avian base.");Reject(()=>GameFiles.ReadMod(baseWad),"IWAD accepted as mod.");
        Array.Copy(BitConverter.GetBytes(int.MaxValue),0,modBytes,8,4);File.WriteAllBytes(modWad,modBytes);Reject(()=>GameFiles.Avian(root,null),"Malformed Avian accepted.");
        byte[] bytes=File.ReadAllBytes(wad);bytes[0]=(byte)'P';File.WriteAllBytes(wad,bytes);Reject(()=>GameFiles.Read(wad),"PWAD accepted.");
        bytes[0]=(byte)'I';Array.Copy(BitConverter.GetBytes(int.MaxValue),0,bytes,8,4);File.WriteAllBytes(wad,bytes);Reject(()=>GameFiles.Read(wad),"Invalid directory accepted.");
        Array.Copy(BitConverter.GetBytes(12),0,bytes,8,4);Array.Copy(BitConverter.GetBytes(int.MaxValue),0,bytes,12,4);File.WriteAllBytes(wad,bytes);Reject(()=>GameFiles.Read(wad),"Invalid lump accepted.");
        File.WriteAllText(result,"PASS: IWAD/PWAD structure; classic and Avian choice persistence; base/mod/widescreen content-keyed saves; settings preservation; quoted engine-specific args; original preserved; wrong bases, network paths, quote injection, malformed directories and out-of-bounds lumps rejected.");
      } finally {
        // This path is generated directly beneath the explicitly supplied result directory.
        string parent=System.IO.Path.GetFullPath(System.IO.Path.GetDirectoryName(result)).TrimEnd(System.IO.Path.DirectorySeparatorChar)+System.IO.Path.DirectorySeparatorChar;
        if(!System.IO.Path.GetFullPath(root).StartsWith(parent,StringComparison.OrdinalIgnoreCase)||!System.IO.Path.GetFileName(root).StartsWith("doom-check-"))throw new Exception("Invalid check cleanup path.");
        Directory.Delete(root,true);
      }
    }
  }
}
