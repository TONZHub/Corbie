using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace CorbiePet {
  public sealed class HabitatFrame {
    public int Width { get; set; }
    public int Height { get; set; }
    public int[] Pixels { get; set; }
    public bool CinemaStage { get; set; }
  }
  public sealed class CinemaCommand { public string Action { get; set; } }
  public static class CinemaContainment {
    // Leave room for body gestures and the upward startle pose. Limits describe
    // allowed window origins, so the entire 180px pet stays inside the frame.
    public static Rect Limits(Rect scene,double width,double height) {
      if(scene.IsEmpty || !Finite(scene.Left) || !Finite(scene.Top) || !Finite(scene.Width) || !Finite(scene.Height)
        || !Finite(width) || !Finite(height) || width<=0 || height<=0 || scene.Width<width+64 || scene.Height<height+96) return Rect.Empty;
      return new Rect(scene.Left+32,scene.Top+64,scene.Width-width-64,scene.Height-height-96);
    }
    static bool Finite(double n) { return !double.IsNaN(n) && !double.IsInfinity(n); }
    public static Point Keep(Point p,Rect limits) {
      return new Point(Math.Max(limits.Left,Math.Min(limits.Right,p.X)),Math.Max(limits.Top,Math.Min(limits.Bottom,p.Y)));
    }
    public static void Check() {
      var limits=Limits(new Rect(-600,50,540,960),180,180);
      if(limits.IsEmpty || limits.Left!=-568 || limits.Right!=-272 || limits.Top!=114 || limits.Bottom!=798) throw new Exception("Cinema wall margins or negative-monitor coordinates failed.");
      if(Keep(new Point(-1000,9999),limits)!=new Point(limits.Left,limits.Bottom)) throw new Exception("Cinema wall clamp failed.");
      if(!Limits(new Rect(0,0,200,240),180,180).IsEmpty || !Limits(Rect.Empty,180,180).IsEmpty || !Limits(new Rect(0,0,600,800),double.NaN,180).IsEmpty) throw new Exception("Invalid cinema walls accepted.");
      var flight=new FlightMotion(); var from=new Point(limits.Left+20,limits.Bottom-30);var to=new Point(limits.Right-20,limits.Bottom-30);
      if(!flight.Start(from,to,limits)) throw new Exception("Contained flight rejected.");
      for(int i=0;i<150&&flight.Active;i++) { flight.Step(0.033,false);if(!limits.Contains(flight.Position)) throw new Exception("Flight escaped cinema walls."); }
      if(flight.Start(from,new Point(limits.Right+1,from.Y),limits)) throw new Exception("Outside cinema flight accepted.");
    }
  }

  // User-selected desktop coordinates avoid browser chrome, zoom and monitor
  // guessing. The overlay exists only while the user is drawing the set walls.
  public sealed class CinemaWallPicker : Window {
    readonly Canvas canvas=new Canvas();
    readonly Rectangle box=new Rectangle { Stroke=Brushes.Wheat,StrokeThickness=2,Fill=new SolidColorBrush(Color.FromArgb(35,240,220,170)) };
    readonly TextBlock label=new TextBlock { Text="Draw a box around the cinema frame. Escape cancels.",Foreground=Brushes.Wheat,Background=new SolidColorBrush(Color.FromArgb(230,15,22,32)),FontSize=18,Padding=new Thickness(15) };
    Point start; bool drawing;
    public CinemaWallPicker(Action<Point,Point> picked) {
      Title="Corbie cinema walls"; WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=new SolidColorBrush(Color.FromArgb(65,0,0,0));Topmost=true;ShowInTaskbar=false;Cursor=Cursors.Cross;
      Left=SystemParameters.VirtualScreenLeft;Top=SystemParameters.VirtualScreenTop;Width=SystemParameters.VirtualScreenWidth;Height=SystemParameters.VirtualScreenHeight;
      Content=canvas;canvas.Children.Add(box);canvas.Children.Add(label);Canvas.SetLeft(label,30);Canvas.SetTop(label,30);box.Visibility=Visibility.Hidden;
      KeyDown+=(s,e)=>{if(e.Key==Key.Escape){e.Handled=true;Close();}};
      MouseLeftButtonDown+=(s,e)=>{start=e.GetPosition(canvas);drawing=true;box.Visibility=Visibility.Visible;CaptureMouse();UpdateBox(start);e.Handled=true;};
      MouseMove+=(s,e)=>{if(drawing)UpdateBox(e.GetPosition(canvas));};
      MouseLeftButtonUp+=(s,e)=>{
        if(!drawing)return;drawing=false;ReleaseMouseCapture();var end=e.GetPosition(canvas);
        if(Math.Abs(end.X-start.X)<244||Math.Abs(end.Y-start.Y)<276){label.Text="That box is too small. Give his whole body room, then try again. Escape cancels.";return;}
        var a=PointToScreen(start);var b=PointToScreen(end);picked(a,b);Close();e.Handled=true;
      };
    }
    void UpdateBox(Point end) { Canvas.SetLeft(box,Math.Min(start.X,end.X));Canvas.SetTop(box,Math.Min(start.Y,end.Y));box.Width=Math.Abs(end.X-start.X);box.Height=Math.Abs(end.Y-start.Y); }
  }
  public sealed class HabitatSense {
    public double X,Y,PreviousX,PreviousY,Horizontal,Vertical,Strength,Age;
    public bool Fresh;
    public DateTime ReceivedUtc;
  }
  // A deliberately small visual front end: track bright moving shapes in a dark
  // habitat using pixels, not browser object names or their prescribed positions.
  public sealed class HabitatRetina {
    double lastX,lastY;
    bool hadLight;
    DateTime lastUtc;
    public HabitatSense Read(HabitatFrame f,DateTime now) {
      if(f==null || f.Width!=48 || f.Height!=32 || f.Pixels==null || f.Pixels.Length!=1536) throw new InvalidDataException("Expected a 48 by 32 retina.");
      double total=0,x=0,y=0;
      for(int i=0;i<f.Pixels.Length;i++) {
        int pixel=f.Pixels[i]; if(pixel<0 || pixel>255) throw new InvalidDataException("Invalid retina pixel.");
        double weight=Math.Max(0,pixel-180); total+=weight; x+=(i%48+0.5)*weight; y+=(i/48+0.5)*weight;
      }
      bool light=total>40;
      double px=light?(x/total/48-0.5)*800:0,py=light?(y/total/32-0.5)*480:0;
      double age=(now-lastUtc).TotalSeconds;
      bool fresh=hadLight && light && age>=0.04 && age<=0.35 && Math.Sqrt((px-lastX)*(px-lastX)+(py-lastY)*(py-lastY))<=220;
      var sense=new HabitatSense { X=px,Y=py,PreviousX=lastX,PreviousY=lastY,Age=age,Fresh=fresh,Strength=light?Math.Min(1,total/180):0,
        Horizontal=fresh?(px-lastX)/age/200:0,Vertical=fresh?(py-lastY)/age/200:0,ReceivedUtc=now };
      lastX=px; lastY=py; hadLight=light; lastUtc=now; return sense;
    }
    public static void Check(FlyGraph graph) {
      var retina=new HabitatRetina(); var f=new HabitatFrame { Width=48,Height=32,Pixels=new int[1536] }; DateTime time=DateTime.UtcNow;
      f.Pixels[16*48+18]=255; var first=retina.Read(f,time); if(first.Fresh || first.Strength<=0) throw new Exception("First visual frame created false motion.");
      Array.Clear(f.Pixels,0,1536); f.Pixels[16*48+20]=255;
      var next=retina.Read(f,time.AddSeconds(0.1)); if(!next.Fresh || next.Horizontal<=0 || next.Vertical!=0) throw new Exception("Retina did not detect rightward pixel motion.");
      var still=retina.Read(f,time.AddSeconds(0.2)); if(still.Horizontal!=0 || still.Vertical!=0) throw new Exception("Still pixels created motion.");
      Array.Clear(f.Pixels,0,1536); var dark=retina.Read(f,time.AddSeconds(0.3)); if(dark.Fresh || dark.Strength!=0) throw new Exception("Dark frame did not settle.");
      bool bad=false; f.Pixels[0]=256; try { retina.Read(f,time); } catch(InvalidDataException) { bad=true; } if(!bad) throw new Exception("Invalid pixels accepted.");
      f.Pixels[0]=0;
      var nerves=new FlyNerves(graph); double response=0;
      for(int step=0;step<25;step++) {
        Array.Clear(f.Pixels,0,1536); f.Pixels[16*48+8+step]=255;
        var sense=retina.Read(f,time.AddSeconds(1+step*0.1)); nerves.Step(0.1,sense.Horizontal,sense.Vertical,sense.Strength); response=Math.Max(response,nerves.Attention);
      }
      if(response<0.02) throw new Exception("Habitat pixels did not excite imported motion circuit.");
    }
  }

  // Small loopback-only HTTP service. Static files are explicitly whitelisted;
  // no workspace file browsing, remote binding, CORS or administrator rights.
  public sealed class HabitatBridge : IDisposable {
    readonly TcpListener listener;
    readonly string root,token=Guid.NewGuid().ToString("N");
    readonly object gate=new object();
    readonly HabitatRetina retina=new HabitatRetina();
    readonly SemaphoreSlim clients=new SemaphoreSlim(4);
    volatile bool disposed;
    HabitatSense latest;
    string snapshot="{}";
    int reset;
    int cinemaSelection;
    int cinemaRelease;
    DateTime cinemaUtc=DateTime.MinValue;
    DateTime lastFrame=DateTime.MinValue;
    public string Url { get; private set; }
    public HabitatBridge(string root,int port=18775) {
      this.root=root;
      if(!File.Exists(Path.Combine(root,"index.html"))) throw new FileNotFoundException("Habitat page is missing.");
      listener=new TcpListener(IPAddress.Loopback,port); listener.Start(8);
      Url="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port+"/";
      var ignored=Accept();
    }
    async Task Accept() {
      while(!disposed) try {
        var client=await listener.AcceptTcpClientAsync().ConfigureAwait(false);
        if(!clients.Wait(0)) { client.Close(); continue; }
        var work=Task.Run(()=> { try { Serve(client); } finally { client.Close(); clients.Release(); } });
      } catch(ObjectDisposedException) { break; } catch(SocketException) { if(disposed) break; }
    }
    public bool Read(out HabitatSense sense) {
      lock(gate) { sense=latest; return !disposed && sense!=null && (DateTime.UtcNow-sense.ReceivedUtc).TotalSeconds<0.5; }
    }
    public bool TakeReset() { return Interlocked.Exchange(ref reset,0)==1; }
    public bool TakeCinemaSelection() { return Interlocked.Exchange(ref cinemaSelection,0)==1; }
    public bool TakeCinemaRelease() { return Interlocked.Exchange(ref cinemaRelease,0)==1; }
    public bool CinemaFresh { get { lock(gate) return !disposed && (DateTime.UtcNow-cinemaUtc).TotalSeconds<2; } }
    public void TouchCinema() { lock(gate) cinemaUtc=DateTime.UtcNow; }
    public void ReleaseCinema() { lock(gate) cinemaUtc=DateTime.MinValue; Interlocked.Exchange(ref cinemaSelection,0);Interlocked.Exchange(ref cinemaRelease,1); }
    public void Publish(object state) { string json=new JavaScriptSerializer().Serialize(state); lock(gate) snapshot=json; }
    void Reply(NetworkStream stream,int code,string type,byte[] body) {
      string head="HTTP/1.1 "+code+" "+(code==200?"OK":"Error")+"\r\nContent-Type: "+type+"\r\nContent-Length: "+body.Length+"\r\nConnection: close\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nContent-Security-Policy: default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'; connect-src 'self'; frame-ancestors 'none'\r\n\r\n";
      byte[] bytes=Encoding.ASCII.GetBytes(head); stream.Write(bytes,0,bytes.Length); stream.Write(body,0,body.Length);
    }
    void Json(NetworkStream s,int code,string json) { Reply(s,code,"application/json; charset=utf-8",Encoding.UTF8.GetBytes(json)); }
    void Serve(TcpClient client) {
      client.ReceiveTimeout=2000; client.SendTimeout=2000;
      using(var stream=client.GetStream()) try {
        var header=new List<byte>(); int endings=0;
        while(header.Count<8192) {
          int b=stream.ReadByte(); if(b<0) return; header.Add((byte)b);
          endings=b==(endings==0 || endings==2?13:10)?endings+1:(b==13?1:0); if(endings==4) break;
        }
        if(endings!=4) { Json(stream,431,"{\"error\":\"Header too large\"}"); return; }
        string[] lines=Encoding.ASCII.GetString(header.ToArray()).Split(new[]{"\r\n"},StringSplitOptions.None);
        string[] request=lines[0].Split(' '); if(request.Length!=3) { Json(stream,400,"{}"); return; }
        var headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        for(int i=1;i<lines.Length;i++) { int colon=lines[i].IndexOf(':'); if(colon<1) continue; string key=lines[i].Substring(0,colon).Trim(); if(headers.ContainsKey(key)) { Json(stream,400,"{}"); return; } headers.Add(key,lines[i].Substring(colon+1).Trim()); }
        string host; string expected=new Uri(Url).Authority;
        if(!headers.TryGetValue("Host",out host) || host!=expected) { Json(stream,403,"{}"); return; }
        string origin; if(headers.TryGetValue("Origin",out origin) && origin!=Url.TrimEnd('/')) { Json(stream,403,"{}"); return; }
        if(request[0]=="GET" && request[1]=="/api/state") {
          string state; lock(gate) state=snapshot;
          Json(stream,200,"{\"token\":\""+token+"\",\"state\":"+state+"}"); return;
        }
        if(request[0]=="GET") {
          string file=request[1]=="/"?"index.html":request[1]=="/habitat.js"?"habitat.js":request[1]=="/habitat.css"?"habitat.css":request[1]=="/cinema.html"?"cinema.html":request[1]=="/cinema.js"?"cinema.js":null;
          if(file==null) { Json(stream,404,"{}"); return; }
          Reply(stream,200,file.EndsWith(".js")?"text/javascript; charset=utf-8":file.EndsWith(".css")?"text/css; charset=utf-8":"text/html; charset=utf-8",File.ReadAllBytes(Path.Combine(root,file))); return;
        }
        string supplied,contentType,lengthText;
        int length;
        if(request[0]!="POST") { Json(stream,403,"{}"); return; }
        if(!headers.TryGetValue("Content-Type",out contentType) || !contentType.StartsWith("application/json",StringComparison.OrdinalIgnoreCase) || headers.ContainsKey("Transfer-Encoding") ||
          !headers.TryGetValue("Content-Length",out lengthText) || !int.TryParse(lengthText,out length) || length<2 || length>16000) { Json(stream,400,"{\"error\":\"Expected bounded JSON body\"}"); return; }
        string expect;
        if(headers.TryGetValue("Expect",out expect) && expect.Equals("100-continue",StringComparison.OrdinalIgnoreCase)) {
          byte[] continuation=Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n"); stream.Write(continuation,0,continuation.Length);
        }
        var body=new byte[length]; int offset=0; while(offset<length) { int n=stream.Read(body,offset,length-offset); if(n==0) return; offset+=n; }
        if(!headers.TryGetValue("X-Corbie-Token",out supplied) || supplied!=token) { Json(stream,403,"{}"); return; }
        if(request[1]=="/api/reset") { Interlocked.Exchange(ref reset,1); Json(stream,200,"{}"); return; }
        if(request[1]=="/api/cinema") {
          var command=new JavaScriptSerializer().Deserialize<CinemaCommand>(Encoding.UTF8.GetString(body));
          if(command!=null&&command.Action=="select") { TouchCinema();Interlocked.Exchange(ref cinemaSelection,1);Json(stream,200,"{}");return; }
          if(command!=null&&command.Action=="release") { ReleaseCinema();Json(stream,200,"{}");return; }
          Json(stream,400,"{}");return;
        }
        if(request[1]!="/api/retina") { Json(stream,404,"{}"); return; }
        var f=new JavaScriptSerializer { MaxJsonLength=16000 }.Deserialize<HabitatFrame>(Encoding.UTF8.GetString(body));
        lock(gate) {
          DateTime now=DateTime.UtcNow;
          if((now-lastFrame).TotalMilliseconds<65) { Json(stream,429,"{}"); return; }
          latest=retina.Read(f,now); lastFrame=now;
          if(f.CinemaStage) cinemaUtc=now;
        }
        Json(stream,200,"{}");
      } catch(InvalidDataException) { try { Json(stream,400,"{\"error\":\"Invalid retina\"}"); } catch { } }
        catch(ArgumentException) { try { Json(stream,400,"{}"); } catch { } }
        catch(IOException) { } catch(ObjectDisposedException) { }
        catch(Exception) { try { Json(stream,400,"{\"error\":\"Invalid request\"}"); } catch { } }
    }
    public void Dispose() { disposed=true; listener.Stop(); lock(gate) latest=null; }
    public static void Check(string root,FlyGraph graph) {
      HabitatRetina.Check(graph);
      using(var bridge=new HabitatBridge(root,0)) {
        bridge.Publish(new { Attention=0.1,Mode="test" });
        using(var client=new WebClient()) {
          var envelope=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(client.DownloadString(bridge.Url+"api/state"));
          if(!client.DownloadString(bridge.Url).Contains("Corbie")) throw new Exception("Habitat page not served.");
          if(!client.DownloadString(bridge.Url+"cinema.html").Contains("Roll camera") || !client.DownloadString(bridge.Url+"cinema.js").Contains("/api/retina")) throw new Exception("Cinema assets not served.");
          bool traversalDenied=false;
          try { client.DownloadString(bridge.Url+"unlisted.html"); } catch(WebException ex) { traversalDenied=((HttpWebResponse)ex.Response).StatusCode==HttpStatusCode.NotFound; }
          if(!traversalDenied) throw new Exception("Unlisted static file accepted.");
          client.Headers["Content-Type"]="application/json"; client.Headers["X-Corbie-Token"]=(string)envelope["token"];
          var frame=new HabitatFrame { Width=48,Height=32,Pixels=new int[1536] }; frame.Pixels[16*48+18]=255;
          try { client.UploadString(bridge.Url+"api/retina",new JavaScriptSerializer().Serialize(frame)); }
          catch(WebException ex) { using(var reader=new StreamReader(ex.Response.GetResponseStream())) throw new Exception("Retina upload: "+reader.ReadToEnd(),ex); }
          HabitatSense sense; if(!bridge.Read(out sense) || sense.Strength<=0) throw new Exception("HTTP retina did not reach bridge.");
          Thread.Sleep(550); if(bridge.Read(out sense)) throw new Exception("Stale habitat retained sensory control.");
          client.Headers["Content-Type"]="application/json";
          try { client.UploadString(bridge.Url+"api/reset","{}"); }
          catch(WebException ex) { using(var reader=new StreamReader(ex.Response.GetResponseStream())) throw new Exception("Reset upload: "+reader.ReadToEnd(),ex); }
          if(!bridge.TakeReset() || bridge.TakeReset()) throw new Exception("Reset command not consumed once.");
          client.Headers["Content-Type"]="application/json";client.UploadString(bridge.Url+"api/cinema","{\"Action\":\"select\"}");
          if(!bridge.TakeCinemaSelection()||bridge.TakeCinemaSelection()||!bridge.CinemaFresh)throw new Exception("Cinema wall selection was not queued once.");
          client.Headers["Content-Type"]="application/json";client.UploadString(bridge.Url+"api/cinema","{\"Action\":\"release\"}");
          if(bridge.CinemaFresh||!bridge.TakeCinemaRelease()||bridge.TakeCinemaRelease())throw new Exception("Cinema wall release failed.");
          bool unknownDenied=false;client.Headers["Content-Type"]="application/json";
          try{client.UploadString(bridge.Url+"api/cinema","{\"Action\":\"move-any-window\"}");}catch(WebException ex){unknownDenied=((HttpWebResponse)ex.Response).StatusCode==HttpStatusCode.BadRequest;}
          if(!unknownDenied)throw new Exception("Unknown cinema action accepted.");
          frame.CinemaStage=true;client.Headers["Content-Type"]="application/json";client.UploadString(bridge.Url+"api/retina",new JavaScriptSerializer().Serialize(frame));
          if(!bridge.CinemaFresh)throw new Exception("Cinema pixels did not renew the wall lease.");
          Thread.Sleep(2100);if(bridge.CinemaFresh)throw new Exception("Closed cinema retained its wall lease.");
          client.Headers["Content-Type"]="application/json"; client.Headers["X-Corbie-Token"]="wrong"; bool denied=false;
          try { client.UploadString(bridge.Url+"api/reset","{}"); } catch(WebException ex) { denied=((HttpWebResponse)ex.Response).StatusCode==HttpStatusCode.Forbidden; }
          if(!denied) throw new Exception("Unauthenticated habitat mutation accepted.");
          denied=false;client.Headers["Content-Type"]="application/json";
          try{client.UploadString(bridge.Url+"api/cinema","{\"Action\":\"select\"}");}catch(WebException ex){denied=((HttpWebResponse)ex.Response).StatusCode==HttpStatusCode.Forbidden;}
          if(!denied)throw new Exception("Unauthenticated wall selection accepted.");
        }
      }
    }
    // Bounded development harness for inspecting the page without starting a
    // second desktop pet. It uses the same imported circuit and reflex classes.
    public static void Preview(string root,FlyGraph graph) {
      using(var bridge=new HabitatBridge(root)) {
        var brain=new FlyNerves(graph); var reflex=new CursorPassReflex(); double hop=0; DateTime consumed=DateTime.MinValue;
        int stepping=0;
        using(var timer=new Timer(_=> {
          if(Interlocked.Exchange(ref stepping,1)!=0) return;
          try {
            if(bridge.TakeReset()) { brain.Reset(); reflex.Reset(); hop=0; }
            HabitatSense s; bool receiving=bridge.Read(out s);
            if(receiving) {
              bool fresh=s.ReceivedUtc!=consumed && s.Fresh;
              double age=(DateTime.UtcNow-s.ReceivedUtc).TotalSeconds;
              brain.Step(0.033,age<0.18?s.Horizontal:0,age<0.18?s.Vertical:0,s.Strength);
              if(reflex.Step(0.033,hop<=0,fresh,s.Age,new Point(s.PreviousX,s.PreviousY),new Point(s.X,s.Y),new Point(0,0))) hop=1;
              consumed=s.ReceivedUtc;
            } else { brain.Step(0.033,0,0,0); reflex.Relax(0.033); }
            hop=Math.Max(0,hop-0.033*1.67);
            bridge.Publish(new { Preview=true,Connected=receiving,Seeing=receiving,Enabled=true,Attention=brain.Attention,Habituation=brain.Habituation,Wariness=reflex.Wariness,Familiarity=reflex.Familiarity,
              Energy=80,Hop=hop,Flying=false,FlightProgress=0,Walking=false,Direction=1,Paused=false,Sleeping=false,Blocked=false,GazeX=s==null?0:s.X/400,GazeY=s==null?0:s.Y/240,Groups=graph.Groups.Length });
          } finally { Interlocked.Exchange(ref stepping,0); }
        },null,0,33)) { using(var done=new ManualResetEvent(false)) done.WaitOne(TimeSpan.FromMinutes(20)); }
      }
    }
  }

  public partial class Pet {
    Rect cinemaScene=Rect.Empty;
    CinemaWallPicker cinemaPicker;
    bool CinemaWallsActive { get { return !cinemaScene.IsEmpty && habitat!=null && habitat.CinemaFresh; } }
    Rect CinemaLimits { get { return CinemaWallsActive?CinemaContainment.Limits(cinemaScene,Width,Height):Rect.Empty; } }
    void SelectCinemaWalls() {
      if(cinemaPicker!=null||habitat==null)return;
      StopWalking();CancelBirdFlight(false);
      cinemaPicker=new CinemaWallPicker((a,b)=>{
        var source=PresentationSource.FromVisual(this);if(source==null)return;
        var matrix=source.CompositionTarget.TransformFromDevice;a=matrix.Transform(a);b=matrix.Transform(b);
        var chosen=new Rect(a,b);if(CinemaContainment.Limits(chosen,Width,Height).IsEmpty)return;
        cinemaScene=chosen;CancelBirdFlight(false);StopWalking();ConstrainCinema();
      });
      cinemaPicker.Closed+=(s,e)=>{cinemaPicker=null;};cinemaPicker.Show();cinemaPicker.Activate();cinemaPicker.Focus();
    }
    void ConstrainCinema() {
      if(cinemaScene.IsEmpty)return;
      if(!CinemaWallsActive){cinemaScene=Rect.Empty;return;}
      if(dragging||cinemaPicker!=null)return;
      var limits=CinemaLimits;if(limits.IsEmpty)return;
      var kept=CinemaContainment.Keep(new Point(Left,Top),limits);
      bool hit=kept.X!=Left||kept.Y!=Top;
      if(hit){if(birdFlight.Active)CancelBirdFlight(false);if(isFlung){isFlung=false;flingVx=flingVy=0;}Left=kept.X;Top=kept.Y;}
      if(Left<=limits.Left)direction=1;else if(Left>=limits.Right)direction=-1;
      routeMin=limits.Left;routeMax=limits.Right;
    }
    void CheckCinemaWalls() {
      CinemaContainment.Check();
      habitat=new HabitatBridge(HabitatRoot,0);
      try {
        habitat.TouchCinema();cinemaScene=new Rect(100,80,540,800);Left=0;Top=1000;autoPerch=true;
        ConstrainCinema();var limits=CinemaLimits;
        if(!limits.Contains(new Point(Left,Top))||routeMin!=limits.Left||routeMax!=limits.Right)throw new Exception("Pet escaped set walls.");
        UpdateWalkRoute();if(routeMin!=limits.Left||routeMax!=limits.Right)throw new Exception("Desktop route replaced cinema route.");
        Left=limits.Right;direction=1;ConstrainCinema();if(direction!=-1)throw new Exception("Pet did not turn at right set wall.");
        Left=limits.Left;direction=-1;ConstrainCinema();if(direction!=1)throw new Exception("Pet did not turn at left set wall.");
        wandering=true;walking=true;walkTime=5;idle=Idle.Quiet;Left=limits.Right-1;direction=1;StepWalk(0.1);
        if(Left!=limits.Right||direction!=-1)throw new Exception("Walking did not bounce at the set edge.");
        isFlung=true;flingVx=900;Left=limits.Right+40;ConstrainCinema();if(isFlung||flingVx!=0||Left!=limits.Right)throw new Exception("Fling escaped set walls.");
        paused=true;Left=limits.Left-200;Animate();if(!limits.Contains(new Point(Left,Top)))throw new Exception("Early animation return bypassed walls.");paused=false;
        if(FlightBounds()!=limits)throw new Exception("Flight still uses full monitor.");
        var inside=new Point(limits.Left+20,limits.Bottom-30);Left=inside.X;Top=inside.Y;
        if(!birdFlight.Start(inside,new Point(limits.Right+30,inside.Y),new Rect(0,0,2000,2000)))throw new Exception("Escape fixture failed.");
        Left=limits.Right+1;ConstrainCinema();if(birdFlight.Active||Left!=limits.Right)throw new Exception("Wall correction retained an escaping flight.");
        habitat.ReleaseCinema();habitat.TouchCinema();PublishHabitat();ConstrainCinema();if(CinemaWallsActive||!cinemaScene.IsEmpty)throw new Exception("Released walls retained pet after another frame.");
      }finally{StopHabitat();}
    }
    HabitatBridge habitat;
    TextBlock habitatStatus;
    bool habitatSeeing;
    string habitatError;
    DateTime habitatConsumedUtc;
    string HabitatRoot { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"habitat"); } }
    void OpenHabitat() { OpenHabitatPage(""); }
    void OpenCinema() { OpenHabitatPage("cinema.html"); }
    void OpenHabitatPage(string page) {
      try {
        if(flyNerves==null) throw new InvalidOperationException("Import the fly circuit first.");
        if(habitat==null) habitat=new HabitatBridge(HabitatRoot);
        SetNerves(true); habitatError=null;
        System.Diagnostics.Process.Start(habitat.Url+page);
      } catch(Exception ex) { habitatError=ex.Message; }
      RefreshHabitat();
    }
    void StopHabitat() { if(cinemaPicker!=null)cinemaPicker.Close();cinemaScene=Rect.Empty;if(habitat!=null) habitat.Dispose(); habitat=null; habitatSeeing=false; nervesSample=false; RefreshHabitat(); }
    void BuildHabitatControls(StackPanel body) {
      body.Children.Add(Section("Habitat sandbox",habitatStatus=Status(),Actions(ActionButton("Open habitat",OpenHabitat),ActionButton("Open cinema",OpenCinema),ActionButton("Disconnect habitat",StopHabitat)),
        new TextBlock { Text="A local browser clearing sends only its 48 × 32 scene pixels into Corbie's motion circuit. Closing the page releases his attention back to the desktop cursor. Screen capture is not used.",TextWrapping=TextWrapping.Wrap,Foreground=MutedText }));
    }
    void RefreshHabitat() {
      if(habitatStatus==null) return;
      habitatStatus.Text=habitatError!=null?"Habitat: "+habitatError:habitat==null?"Habitat disconnected":habitatSeeing?"Eyes on the habitat": "Habitat open; waiting for scene pixels";
    }
    void PublishHabitat() {
      if(habitat==null) return;
      if(habitat.TakeCinemaRelease()){cinemaScene=Rect.Empty;if(cinemaPicker!=null)cinemaPicker.Close();}
      if(habitat.TakeCinemaSelection())SelectCinemaWalls();
      if(!habitat.CinemaFresh&&cinemaPicker!=null)cinemaPicker.Close();
      if(habitat.TakeReset()) ResetNerveAttention();
      HabitatSense seen; bool receiving=habitat.Read(out seen);
      habitatSeeing=receiving && NervesAllowed(wasDancing) && !birdFlight.Active;
      habitat.Publish(new { Connected=receiving,Seeing=habitatSeeing,Enabled=nervesEnabled,Attention=flyNerves==null?0:flyNerves.Attention,Habituation=flyNerves==null?0:flyNerves.Habituation,
        Wariness=NerveWariness,Familiarity=cursorStartle.Familiarity,ShimejiFamiliarity=ShimejiFamiliarity,Energy=mood.Snapshot.Energy,Hop=hop,Flying=birdFlight.Active || cursorFollowingActive,FlightProgress=birdFlight.Active?birdFlight.Progress:0,
        Walking=walking,Direction=direction,Paused=paused,Sleeping=isNightSleep,Blocked=!NervesAllowed(wasDancing),GazeX=seen==null?0:seen.X/400,GazeY=seen==null?0:seen.Y/240,Groups=flyNerves==null?0:flyNerves.Graph.Groups.Length,
        Weather=weatherDrive.Known?weatherReading.Summary:"Weather unknown",WeatherBehavior=weatherDrive.Behavior,WeatherSource="Open-Meteo",Shelter=weatherDrive.Shelter,CinemaWalls=CinemaWallsActive,CinemaPicking=cinemaPicker!=null });
    }
    bool StepHabitatNerves(double dt) {
      HabitatSense sense;
      habitatSeeing=habitat!=null && habitat.Read(out sense) && nervesMayReact && !birdFlight.Active;
      if(!habitatSeeing) return false;
      habitat.Read(out sense);
      bool newFrame=sense.ReceivedUtc!=habitatConsumedUtc;
      double age=(DateTime.UtcNow-sense.ReceivedUtc).TotalSeconds;
      flyNerves.Step(dt,age<0.18?sense.Horizontal:0,age<0.18?sense.Vertical:0,sense.Strength*weatherDrive.Sensitivity);
      nervesCursorOffset=sense.X;
      bool startled=cursorStartle.Step(dt,!scritches.Engaged && !scritches.Celebrating && hop<=0 && peckingCulpritTimer<=0 && thermalAlertTimer<=0 && offendedTimer<=0 && mischiefChaseTimer<=0 && perchArrivalTime<=0,
        newFrame && sense.Fresh,sense.Age,new Point(sense.PreviousX,sense.PreviousY),new Point(sense.X,sense.Y),new Point(0,0));
      habitatConsumedUtc=sense.ReceivedUtc;
      ApplyNerveReaction(dt,startled); nervesSample=false; return true;
    }
    void CheckHabitatIntegration() {
      SetNerves(true); nervesMayReact=true; nervesCooldown=0;
      habitat=new HabitatBridge(HabitatRoot,0);
      try {
        using(var client=new WebClient()) {
          var json=new JavaScriptSerializer();
          var envelope=json.Deserialize<Dictionary<string,object>>(client.DownloadString(habitat.Url+"api/state"));
          client.Headers["X-Corbie-Token"]=(string)envelope["token"];
          var frame=new HabitatFrame { Width=48,Height=32,Pixels=new int[1536] };
          double attention=0;
          for(int i=0;i<12;i++) {
            Array.Clear(frame.Pixels,0,1536); frame.Pixels[16*48+8+i*2]=255;
            client.Headers["Content-Type"]="application/json";
            client.UploadString(habitat.Url+"api/retina",json.Serialize(frame));
            if(!StepHabitatNerves(0.1)) throw new Exception("Desktop pet rejected fresh habitat pixels.");
            attention=Math.Max(attention,flyNerves.Attention); Thread.Sleep(100);
          }
          if(attention<0.02 || !habitatSeeing) throw new Exception("Desktop pet did not attend to habitat motion.");
          nervesMayReact=false; if(StepHabitatNerves(0.1)) throw new Exception("Blocked pet accepted habitat input.");
          nervesMayReact=true; Thread.Sleep(550);
          if(StepHabitatNerves(0.1)) throw new Exception("Stale habitat retained desktop control.");
          client.Headers["Content-Type"]="application/json"; client.UploadString(habitat.Url+"api/reset","{}");
          PublishHabitat();
          if(flyNerves.Attention!=0 || cursorStartle.Wariness!=0) throw new Exception("Habitat reset did not clear desktop brain.");
        }
      } finally { StopHabitat(); SetNerves(false); }
    }
  }
}
