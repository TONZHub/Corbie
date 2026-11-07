using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;

// A small, game-scoped input adapter. No commands or arbitrary applications can be controlled.
class GameWindow {
  [StructLayout(LayoutKind.Sequential)] struct RECT { public int L,T,R,B; }
  [StructLayout(LayoutKind.Sequential)] struct POINT { public int X,Y; public POINT(int x,int y){X=x;Y=y;} }
  [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public INPUTUNION data; }
  [StructLayout(LayoutKind.Explicit)] struct INPUTUNION {
    [FieldOffset(0)] public MOUSEINPUT mouse;
    [FieldOffset(0)] public KEYBDINPUT key;
  }
  [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx,dy;public uint mouseData,dwFlags,time;public UIntPtr extra; }
  [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort vk,scan;public uint flags,time;public UIntPtr extra; }
  [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr w,out RECT r);
  [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr w,ref POINT p);
  [DllImport("user32.dll")] static extern bool IsIconic(IntPtr w);
  [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr w);
  [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr w,int n);
  [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
  [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
  [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr w,uint flag);
  [DllImport("user32.dll")] static extern uint SendInput(uint n,INPUT[] inputs,int size);
  [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
  [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
  static readonly JavaScriptSerializer json=new JavaScriptSerializer {MaxJsonLength=20000000};
  static string expectedPath;static Process game;static IntPtr hwnd;
  static volatile bool armed;static string lastId;static DateTime captured;
  static RECT lastRect;static POINT lastOrigin;static byte[] lastPixels;
  static RECT Bounds(out POINT origin) {
    RECT r;
    if(game==null || game.HasExited || game.MainWindowHandle!=hwnd || !GetClientRect(hwnd,out r) || IsIconic(hwnd)) throw new Exception("Game closed, changed window, or is minimized.");
    origin=new POINT(0,0);if(!ClientToScreen(hwnd,ref origin) || r.R<100 || r.B<100) throw new Exception("Game window is unavailable.");
    return r;
  }
  static void Foreground() {
    if(GetForegroundWindow()!=hwnd) {armed=false;throw new Exception("Paused: the game lost focus. Corbie will not click another application.");}
  }
  static Bitmap Capture(RECT r,POINT p) {
    var bmp=new Bitmap(r.R,r.B,PixelFormat.Format24bppRgb);
    try {using(var g=Graphics.FromImage(bmp))g.CopyFromScreen(p.X,p.Y,0,0,bmp.Size,CopyPixelOperation.SourceCopy);return bmp;}
    catch {bmp.Dispose();throw;}
  }
  static byte[] Pixels(Bitmap bmp) {
    using(var small=new Bitmap(bmp,new Size(64,48))) {
      byte[] result=new byte[64*48*3];int i=0;
      for(int y=0;y<48;y++)for(int x=0;x<64;x++){var c=small.GetPixel(x,y);result[i++]=c.R;result[i++]=c.G;result[i++]=c.B;}
      return result;
    }
  }
  static object Attach() {
    Process selected=null;
    foreach(var p in Process.GetProcessesByName("pm2")) {
      try {if(string.Equals(Path.GetFullPath(p.MainModule.FileName),expectedPath,StringComparison.OrdinalIgnoreCase) && p.MainWindowHandle!=IntPtr.Zero) {
        if(selected!=null)throw new InvalidOperationException("Two game windows are open. Close one first.");selected=p;
      }} catch(InvalidOperationException){throw;} catch{}
    }
    if(selected==null)throw new Exception("Open Princess Maker 2 Refine first. Its installed pm2.exe was not found running.");
    game=selected;hwnd=game.MainWindowHandle;armed=false;lastId=null;
    return new {pid=game.Id,title=game.MainWindowTitle,armed=false};
  }
  static object Observe() {
    POINT p;RECT r=Bounds(out p);Foreground();
    using(var bmp=Capture(r,p))using(var stream=new MemoryStream()) {
      lastPixels=Pixels(bmp);lastRect=r;lastOrigin=p;lastId=Guid.NewGuid().ToString("N");captured=DateTime.UtcNow;
      bmp.Save(stream,ImageFormat.Png);
      return new {id=lastId,width=r.R,height=r.B,png=Convert.ToBase64String(stream.ToArray()),detail=FrameVision.Prepare(bmp),armed=armed,title=game.MainWindowTitle};
    }
  }
  static void Fresh(string id) {
    if(!armed)throw new Exception("Paused. Start play again explicitly.");
    Foreground();POINT p;RECT r=Bounds(out p);
    if(id!=lastId || lastId==null || (DateTime.UtcNow-captured).TotalSeconds>180)throw new Exception("Observation expired. Read the game again.");
    if(r.R!=lastRect.R || r.B!=lastRect.B || p.X!=lastOrigin.X || p.Y!=lastOrigin.Y)throw new Exception("Game window moved or resized. Read it again.");
    using(var bmp=Capture(r,p)) {
      byte[] now=Pixels(bmp);int changed=0;
      for(int i=0;i<now.Length;i+=3)if(Math.Abs(now[i]-lastPixels[i])+Math.Abs(now[i+1]-lastPixels[i+1])+Math.Abs(now[i+2]-lastPixels[i+2])>90)changed++;
      if(changed>64*48/5)throw new Exception("Game screen changed while Corbie was thinking. No input sent; read it again.");
    }
  }
  static void Send(INPUT input) {
    if(SendInput(1,new[]{input},Marshal.SizeOf(typeof(INPUT)))!=1)throw new Exception("Windows refused game input. Check that the game and Corbie have the same permissions.");
  }
  static object Act(System.Collections.Generic.Dictionary<string,object> a) {
    Fresh(Convert.ToString(a["observation"]));
    string type=Convert.ToString(a["type"]);
    if(type=="click") {
      double x=Convert.ToDouble(a["x"]),y=Convert.ToDouble(a["y"]);
      if(double.IsNaN(x)||double.IsNaN(y)||double.IsInfinity(x)||double.IsInfinity(y)||x<0||x>1000||y<0||y>1000)throw new Exception("Click is outside the game.");
      var target=new POINT(lastOrigin.X+(int)Math.Round(x*(lastRect.R-1)/1000),lastOrigin.Y+(int)Math.Round(y*(lastRect.B-1)/1000));
      // A small popup or changed menu can leave most of the screen untouched.
      // Check the actual target neighbourhood as well as the whole-frame comparison.
      using(var bmp=Capture(lastRect,lastOrigin)) {
        byte[] now=Pixels(bmp);int differences=0;
        int cx=(int)Math.Round(x*63/1000),cy=(int)Math.Round(y*47/1000);
        for(int yy=Math.Max(0,cy-2);yy<=Math.Min(47,cy+2);yy++)
          for(int xx=Math.Max(0,cx-2);xx<=Math.Min(63,cx+2);xx++) {
            int i=(yy*64+xx)*3;
            if(Math.Abs(now[i]-lastPixels[i])+Math.Abs(now[i+1]-lastPixels[i+1])+Math.Abs(now[i+2]-lastPixels[i+2])>90)differences++;
          }
        if(differences>6)throw new Exception("The chosen game control changed. Read it again.");
      }
      if(GetAncestor(WindowFromPoint(target),2)!=hwnd)throw new Exception("Another window covers the chosen control. No input sent.");
      if(!SetCursorPos(target.X,target.Y))throw new Exception("Cursor movement failed.");
      Foreground();if(!armed)throw new Exception("Paused.");
      Send(new INPUT{type=0,data=new INPUTUNION{mouse=new MOUSEINPUT{dwFlags=2}}});
      Thread.Sleep(80); // Older game loops may poll button state once per frame.
      // Always release an injected button, even if the pause key is pressed between down and up.
      Send(new INPUT{type=0,data=new INPUTUNION{mouse=new MOUSEINPUT{dwFlags=4}}});
    } else if(type=="key") {
      string key=Convert.ToString(a["key"]);ushort vk;
      switch(key){case "Enter":vk=13;break;case "Space":vk=32;break;case "Escape":vk=27;break;case "Tab":vk=9;break;case "ArrowLeft":vk=37;break;case "ArrowUp":vk=38;break;case "ArrowRight":vk=39;break;case "ArrowDown":vk=40;break;default:throw new Exception("That key is not permitted.");}
      Foreground();if(!armed)throw new Exception("Paused.");
      Send(new INPUT{type=1,data=new INPUTUNION{key=new KEYBDINPUT{vk=vk}}});
      Thread.Sleep(80);
      Send(new INPUT{type=1,data=new INPUTUNION{key=new KEYBDINPUT{vk=vk,flags=2}}});
    } else throw new Exception("Unknown game input.");
    lastId=null;
    return new {performed=true};
  }
  static object Dispatch(System.Collections.Generic.Dictionary<string,object> a) {
    string command=Convert.ToString(a["command"]);
    if(command=="prepare")return FrameVision.FromPng(Convert.ToString(a["png"]));
    if(command=="attach")return Attach();
    if(command=="pause"){armed=false;return new {armed=false};}
    if(command=="arm" || command=="focus") {
      if(game==null || game.HasExited || game.MainWindowHandle!=hwnd)throw new Exception("Game window is unavailable.");
      ShowWindow(hwnd,9);POINT p;Bounds(out p);SetForegroundWindow(hwnd);Thread.Sleep(150);Foreground();armed=command=="arm";return new {armed=armed};
    }
    if(command=="status")return new {armed=armed,attached=game!=null};
    if(command=="observe")return Observe();
    if(command=="act")return Act(a);
    throw new Exception("Unknown game command.");
  }
  static void Main(string[] args) {
    SetProcessDPIAware();
    if(args.Length!=1 || !File.Exists(args[0]) || !string.Equals(Path.GetFileName(args[0]),"pm2.exe",StringComparison.OrdinalIgnoreCase)) {Console.Error.WriteLine("Expected the installed Princess Maker 2 Refine pm2.exe.");Environment.Exit(1);}
    expectedPath=Path.GetFullPath(args[0]);
    var stop=new Thread(()=>{while(true){if((GetAsyncKeyState(0x77)&0x8000)!=0 || armed && GetForegroundWindow()!=hwnd)armed=false;Thread.Sleep(20);}}){IsBackground=true};stop.Start();
    string line;
    while((line=Console.ReadLine())!=null) {
      try {Console.WriteLine(json.Serialize(new {ok=true,result=Dispatch(json.Deserialize<System.Collections.Generic.Dictionary<string,object>>(line))}));}
      catch(Exception e){armed=false;Console.WriteLine(json.Serialize(new {ok=false,error=e.Message}));}
    }
    armed=false;
  }
}
