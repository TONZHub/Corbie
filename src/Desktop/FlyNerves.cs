using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using Forms = System.Windows.Forms;

namespace CorbiePet {
  public sealed class FlyGroup {
    public string Type { get; set; }
    public int Cells { get; set; }
  }
  public sealed class FlyEdge {
    public int From { get; set; }
    public int To { get; set; }
    public long Synapses { get; set; }
    public string Transmitter { get; set; }
  }
  public sealed class FlyGraph {
    public int Version { get; set; }
    public string ConnectionsSHA256 { get; set; }
    public string TypesSHA256 { get; set; }
    public string Assumptions { get; set; }
    public FlyGroup[] Groups { get; set; }
    public FlyEdge[] Edges { get; set; }
    public long SelectedRows { get; set; }
    public long SelectedSynapses { get; set; }
  }

  // Development-time import. Runtime reads only the small graph, never the full CSV.
  // Neurons with the same primary type are collapsed across both hemispheres.
  public static class FlyImport {
    public static bool Selected(string name) {
      return name != null && Regex.IsMatch(name, @"^(T[45][abcd]|LPi(0[1-9]|1[0-5])|HS[ENS]|VS[1-8])$");
    }
    public static string[] Csv(string line) {
      var fields=new List<string>(); var value=new StringBuilder(); bool quoted=false;
      for(int i=0;i<line.Length;i++) {
        char c=line[i];
        if(c=='"') { if(quoted && i+1<line.Length && line[i+1]=='"') { value.Append('"'); i++; } else quoted=!quoted; }
        else if(c==',' && !quoted) { fields.Add(value.ToString()); value.Length=0; }
        else value.Append(c);
      }
      if(quoted) throw new InvalidDataException("Unclosed CSV quote.");
      fields.Add(value.ToString()); return fields.ToArray();
    }
    static StreamReader Open(string path) { return new StreamReader(new GZipStream(File.OpenRead(path),CompressionMode.Decompress)); }
    static string Hash(string path) { using(var sha=SHA256.Create()) using(var f=File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(f)).Replace("-",""); }
    public static FlyGraph Build(string connections,string types) {
      var ids=new Dictionary<ulong,string>(); var counts=new Dictionary<string,int>();
      using(var reader=Open(types)) {
        if(reader.ReadLine()!="root_id,primary_type,additional_type(s)") throw new InvalidDataException("Unexpected cell-type columns.");
        string line;
        while((line=reader.ReadLine())!=null) {
          var c=Csv(line); ulong id;
          if(c.Length!=3 || !ulong.TryParse(c[0],out id) || id==0) throw new InvalidDataException("Invalid cell-type row.");
          if(!Selected(c[1])) continue;
          if(ids.ContainsKey(id)) throw new InvalidDataException("Duplicate selected neuron ID.");
          ids.Add(id,c[1]); if(!counts.ContainsKey(c[1])) counts[c[1]]=0; counts[c[1]]++;
        }
      }
      foreach(string input in new[]{"T4a","T4b","T4c","T4d","T5a","T5b","T5c","T5d"})
        if(!counts.ContainsKey(input)) throw new InvalidDataException("Missing motion input type: "+input);
      var groups=counts.OrderBy(c=>c.Key,StringComparer.Ordinal).Select(c=>new FlyGroup { Type=c.Key,Cells=c.Value }).ToArray();
      var indices=new Dictionary<string,int>(); for(int i=0;i<groups.Length;i++) indices.Add(groups[i].Type,i);
      var edges=new Dictionary<string,FlyEdge>(); long rows=0,synapses=0;
      using(var reader=Open(connections)) {
        if(reader.ReadLine()!="pre_root_id,post_root_id,neuropil,syn_count,nt_type") throw new InvalidDataException("Unexpected connection columns.");
        string line;
        while((line=reader.ReadLine())!=null) {
          var c=Csv(line); ulong pre,post; long count;
          if(c.Length!=5 || !ulong.TryParse(c[0],out pre) || !ulong.TryParse(c[1],out post) || !long.TryParse(c[3],out count) || pre==0 || post==0 || count<=0)
            throw new InvalidDataException("Invalid connection row.");
          string a,b; if(!ids.TryGetValue(pre,out a) || !ids.TryGetValue(post,out b)) continue;
          // Only these signs are modeled. GLUT is inhibitory only for selected LPi sources.
          if(c[4]!="ACH" && c[4]!="GABA" && !(c[4]=="GLUT" && a.StartsWith("LPi",StringComparison.Ordinal))) continue;
          string key=indices[a]+":"+indices[b]+":"+c[4]; FlyEdge edge;
          if(!edges.TryGetValue(key,out edge)) { edge=new FlyEdge { From=indices[a],To=indices[b],Transmitter=c[4] }; edges.Add(key,edge); }
          edge.Synapses=checked(edge.Synapses+count); rows++; synapses=checked(synapses+count);
        }
      }
      var graph=new FlyGraph { Version=1,Groups=groups,Edges=edges.Values.OrderBy(e=>e.From).ThenBy(e=>e.To).ThenBy(e=>e.Transmitter).ToArray(),SelectedRows=rows,SelectedSynapses=synapses,
        ConnectionsSHA256=Hash(connections),TypesSHA256=Hash(types),
        Assumptions="Type groups merge hemispheres and spatial columns; summed synapse counts are normalized per target. ACH excitatory, GABA inhibitory, LPi GLUT inhibitory; other transmitters omitted. Cursor direction supplies artificial T4/T5 inputs; ON/OFF contrast, receptor details and biological timing are not modeled. Habituation and pet actions are designed adapters, not measured fly behavior." };
      FlyNerves.Validate(graph); return graph;
    }
  }

  // Bounded recurrent rate model, not a spiking or whole-brain emulation.
  public sealed class FlyNerves {
    readonly FlyGraph graph;
    readonly double[] activity,next,drive;
    readonly double[] weights;
    double adaptation;
    public double Attention { get; private set; }
    public double Habituation { get { return adaptation; } }
    public FlyGraph Graph { get { return graph; } }
    public static void Validate(FlyGraph g) {
      if(g==null || g.Version!=1 || g.Groups==null || g.Groups.Length<9 || g.Groups.Length>64 || g.Edges==null || g.Edges.Length==0 || g.Edges.Length>4096) throw new InvalidDataException("Invalid fly circuit.");
      var seen=new HashSet<string>();
      foreach(var n in g.Groups) if(n==null || !FlyImport.Selected(n.Type) || n.Cells<1 || !seen.Add(n.Type)) throw new InvalidDataException("Invalid fly group.");
      foreach(string input in new[]{"T4a","T4b","T4c","T4d","T5a","T5b","T5c","T5d"}) if(!seen.Contains(input)) throw new InvalidDataException("Missing fly motion input.");
      if(!seen.Any(t=>t.StartsWith("HS") || t.StartsWith("VS"))) throw new InvalidDataException("Missing visual output groups.");
      foreach(var e in g.Edges) if(e==null || e.From<0 || e.To<0 || e.From>=g.Groups.Length || e.To>=g.Groups.Length || e.Synapses<1 || e.Synapses>1000000000L ||
        !(e.Transmitter=="ACH" || e.Transmitter=="GABA" || (e.Transmitter=="GLUT" && g.Groups[e.From].Type.StartsWith("LPi")))) throw new InvalidDataException("Invalid fly edge.");
    }
    public FlyNerves(FlyGraph graph) {
      Validate(graph); this.graph=graph;
      activity=new double[graph.Groups.Length]; next=new double[activity.Length]; drive=new double[activity.Length]; weights=new double[graph.Edges.Length];
      var totals=new double[activity.Length]; foreach(var e in graph.Edges) totals[e.To]+=e.Synapses;
      for(int i=0;i<weights.Length;i++) { var e=graph.Edges[i]; weights[i]=0.9*e.Synapses/totals[e.To]*(e.Transmitter=="ACH"?1:-1); }
    }
    static double Bound(double n) { return double.IsNaN(n)||double.IsInfinity(n)?0:Math.Max(0,Math.Min(1,n)); }
    public void Reset() { Array.Clear(activity,0,activity.Length); Attention=0; adaptation=0; }
    public void Step(double seconds,double horizontal,double vertical,double proximity) {
      if(double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds<=0) return;
      seconds=Math.Min(seconds,0.1); proximity=Bound(proximity);
      horizontal=double.IsNaN(horizontal)||double.IsInfinity(horizontal)?0:Math.Max(-1,Math.Min(1,horizontal));
      vertical=double.IsNaN(vertical)||double.IsInfinity(vertical)?0:Math.Max(-1,Math.Min(1,vertical));
      double strength=Math.Max(Math.Abs(horizontal),Math.Abs(vertical))*proximity;
      adaptation=Bound(adaptation+seconds*(strength*(1-adaptation)/5-(1-strength)*adaptation/18));
      Array.Clear(drive,0,drive.Length);
      for(int i=0;i<activity.Length;i++) {
        string t=graph.Groups[i].Type;
        if(t.StartsWith("T4") || t.StartsWith("T5")) {
          // Screen coordinates: a/right, b/left, c/up, d/down. An artificial adapter.
          double signal=t[2]=='a'?horizontal:t[2]=='b'?-horizontal:t[2]=='c'?-vertical:vertical;
          drive[i]=Math.Max(0,signal)*proximity*(1-0.8*adaptation);
        }
      }
      for(int i=0;i<weights.Length;i++) { var e=graph.Edges[i]; drive[e.To]+=weights[i]*activity[e.From]; }
      double ease=1-Math.Exp(-seconds/0.18);
      Attention=0;
      for(int i=0;i<activity.Length;i++) {
        next[i]=activity[i]+(Bound(drive[i])-activity[i])*ease;
        if(graph.Groups[i].Type.StartsWith("HS") || graph.Groups[i].Type.StartsWith("VS")) Attention=Math.Max(Attention,next[i]);
      }
      Array.Copy(next,activity,activity.Length);
    }
    public static void Check(FlyGraph actual) {
      Validate(actual);
      if(FlyImport.Csv("1,T4a,\"one,two\"").Length!=3 || FlyImport.Selected("T4xyz")) throw new Exception("CSV/type selection failed.");
      var groups=new List<FlyGroup>(); foreach(string t in new[]{"T4a","T4b","T4c","T4d","T5a","T5b","T5c","T5d","HSE"}) groups.Add(new FlyGroup { Type=t,Cells=1 });
      var fixture=new FlyGraph { Version=1,Groups=groups.ToArray(),Edges=new[]{new FlyEdge { From=0,To=8,Synapses=12,Transmitter="ACH" }} };
      var n=new FlyNerves(fixture); for(int i=0;i<20;i++) n.Step(0.1,1,0,1);
      double noticed=n.Attention; if(noticed<0.2) throw new Exception("Connection did not convey motion.");
      for(int i=0;i<180;i++) n.Step(0.1,1,0,1);
      if(n.Attention>=noticed*0.7 || n.Habituation<0.9) throw new Exception("Repeated motion did not habituate.");
      for(int i=0;i<1000;i++) n.Step(0.1,0,0,0);
      if(n.Attention>0.001 || n.Habituation>0.01) throw new Exception("No-input activity did not settle.");
      n.Reset(); for(int i=0;i<20;i++) n.Step(0.1,-1,0,1); if(n.Attention>0.001) throw new Exception("Direction adapter ignored sign.");
      fixture.Edges[0].Transmitter="GABA"; n=new FlyNerves(fixture); for(int i=0;i<20;i++) n.Step(0.1,1,0,1); if(n.Attention>0.001) throw new Exception("Inhibition was excitatory.");
      fixture.Edges[0].To=99; bool invalidRejected=false; try { Validate(fixture); } catch(InvalidDataException) { invalidRejected=true; }
      if(!invalidRejected) throw new Exception("Invalid edge index was accepted.");
      n=new FlyNerves(actual); double max=0;
      for(int i=0;i<2000;i++) { n.Step(0.033,Math.Sin(i/15.0),Math.Cos(i/21.0),1); max=Math.Max(max,n.Attention); if(double.IsNaN(n.Attention) || n.Attention<0 || n.Attention>1) throw new Exception("Unbounded neural response."); }
      if(max<0.02) throw new Exception("Imported circuit never responds to motion.");
      n.Step(double.NaN,double.NaN,double.PositiveInfinity,1); n.Reset(); if(n.Attention!=0 || n.Habituation!=0) throw new Exception("Reset failed.");
    }
  }

  // A designed startle adapter, separate from the scientific connectivity model.
  // Sweep the whole sampled path so a fast pass still counts when both endpoints
  // fall outside the near radius. Coordinates and speed use device-independent pixels.
  public sealed class CursorPassReflex {
    double cooldown;
    public double Wariness { get; private set; }
    public double Familiarity { get; private set; }
    public void Reset() { cooldown=0; Wariness=0; Familiarity=0; }
    static bool Finite(double n) { return !double.IsNaN(n) && !double.IsInfinity(n); }
    void Decay(double dt,bool calm) {
      if(!Finite(dt) || dt<=0) return;
      dt=Math.Min(0.1,dt);
      cooldown=Math.Max(0,cooldown-dt);
      Wariness*=Math.Exp(-dt/(calm?7:18));
      Familiarity*=Math.Exp(-dt/300);
    }
    public void Relax(double dt) { Decay(dt,true); }
    public bool Step(double dt,bool eligible,bool fresh,double age,Point before,Point after,Point head) {
      if(!eligible) return false;
      bool valid=fresh && Finite(age) && age>=0.008 && age<=0.15 && Finite(before.X) && Finite(before.Y) && Finite(after.X) && Finite(after.Y) && Finite(head.X) && Finite(head.Y);
      double dx=after.X-before.X,dy=after.Y-before.Y,length2=dx*dx+dy*dy;
      double length=Math.Sqrt(length2);
      double speed=valid?length/age:0;
      // Invalid samples cannot train memory; quiet observation lets alertness settle.
      Decay(dt,!valid || speed<250);
      if(cooldown>0 || !valid || length<12 || length>900 || speed<650) return false;
      double along=Math.Max(0,Math.Min(1,((head.X-before.X)*dx+(head.Y-before.Y)*dy)/length2));
      double nearX=before.X+along*dx-head.X,nearY=before.Y+along*dy-head.Y;
      if(nearX*nearX+nearY*nearY>120*120) return false;
      // Learn only once per pass/cooldown, not once per animation frame.
      // Familiar moderate passes soften the reflex; very fast motion can still startle.
      bool startled=speed>=650*(1+1.5*Familiarity);
      Familiarity+=0.18*(1-Familiarity);
      Wariness=startled?Math.Min(1,Wariness+0.55):Math.Max(Wariness,0.12);
      cooldown=2.5; return startled;
    }
    public static void Check() {
      var r=new CursorPassReflex(); var head=new Point(0,0); var a=new Point(-180,30); var b=new Point(180,30);
      if(!r.Step(0.033,true,true,0.08,a,b,head)) throw new Exception("Fast swept pass missed.");
      if(r.Step(0.033,true,true,0.08,b,a,head)) throw new Exception("Startle cooldown ignored.");
      for(int i=0;i<80;i++) r.Step(0.033,true,false,0.033,b,b,head);
      if(!r.Step(0.033,true,true,0.08,b,a,head)) throw new Exception("Startle cooldown did not recover.");
      r.Reset();
      if(r.Step(0.033,true,true,0.1,new Point(-20,0),new Point(20,0),head)) throw new Exception("Slow pass startled.");
      if(r.Step(0.033,true,true,0.03,new Point(-180,300),new Point(180,300),head)) throw new Exception("Far pass startled.");
      if(r.Step(0.033,true,true,0.01,new Point(0,0),new Point(1,0),head)) throw new Exception("Jitter startled.");
      if(r.Step(0.033,true,false,0.03,a,b,head) || r.Step(0.033,false,true,0.03,a,b,head) || r.Step(0.033,true,true,0.5,a,b,head)) throw new Exception("Blocked or stale sample startled.");
      if(r.Step(0.033,true,true,0.03,new Point(-1000,0),new Point(1000,0),head)) throw new Exception("Pointer teleport startled.");
      r.Reset(); a=new Point(-40,0); b=new Point(40,0);
      if(!r.Step(0.033,true,true,0.08,a,b,head) || r.Wariness<0.5) throw new Exception("First scare left no wary memory.");
      bool familiarPass=false;
      for(int pass=0;pass<5;pass++) {
        for(int i=0;i<80;i++) r.Step(0.033,true,true,0.033,b,b,head);
        familiarPass=r.Step(0.033,true,true,0.08,a,b,head);
      }
      if(familiarPass || r.Familiarity<0.6) throw new Exception("Repeated moderate passes did not become familiar.");
      double wary=r.Wariness,known=r.Familiarity;
      for(int i=0;i<200;i++) r.Step(0.1,true,true,0.1,b,b,head);
      if(r.Wariness>=wary*0.1 || r.Familiarity<known*0.9) throw new Exception("Calm did not settle wariness while retaining short-term familiarity.");
      double unchanged=r.Familiarity;
      if(r.Step(0.033,false,true,0.03,a,b,head) || r.Familiarity!=unchanged) throw new Exception("Blocked input trained memory.");
      if(!r.Step(0.033,true,true,0.04,new Point(-100,0),new Point(100,0),head)) throw new Exception("Familiarity suppressed a much faster scare.");
      r.Relax(0.1); r.Reset(); if(r.Wariness!=0 || r.Familiarity!=0) throw new Exception("Reset retained scare memory.");
    }
  }

  public partial class Pet {
    FlyNerves flyNerves;
    readonly CursorPassReflex cursorStartle=new CursorPassReflex();
    CheckBox nervesToggle;
    TextBlock nervesStatus;
    bool nervesEnabled,nervesSample,nervesMayReact;
    double nervesX,nervesY,nervesCursorOffset,nervesCooldown,nervesLastSampleSeconds;
    double nervesStartleTimer;
    string nervesError;
    string NervesPreference { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"corbie-nerves-enabled.txt"); } }
    static FlyGraph LoadFlyGraph() {
      string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"brain-data","fafb-v783","motion-circuit.json");
      if(new FileInfo(file).Length>1000000) throw new InvalidDataException("Fly circuit file is too large.");
      return new JavaScriptSerializer().Deserialize<FlyGraph>(File.ReadAllText(file));
    }
    void InitializeNerves() {
      try {
        flyNerves=new FlyNerves(LoadFlyGraph());
        nervesEnabled=!testing && File.Exists(NervesPreference) && File.ReadAllText(NervesPreference).Trim()=="enabled";
      } catch(Exception ex) { nervesError="Circuit unavailable: "+ex.Message; nervesEnabled=false; }
    }
    void SetNerves(bool enabled) {
      if(refreshingPanel) return;
      if(enabled && flyNerves==null) { companion.SaySilent(nervesError??"The fly circuit needs importing first."); return; }
      if(!testing) {
        try { File.WriteAllText(NervesPreference,enabled?"enabled":"disabled"); }
        catch(Exception ex) { companion.SaySilent("Couldn't save the nervous-system setting: "+ex.Message); return; }
      }
      if(nervesStartleTimer>0) hop=0;
      CancelDesktopForage();
      CancelBirdFlight(true);
      nervesStartleTimer=0; cursorStartle.Reset();
      ResetShimejiNerves(); EndShimejiInteraction();
      ResetScreenNerves();
      ResetPerchIntent();
      nervesEnabled=enabled; nervesSample=false; nervesMayReact=false; nervesCooldown=2;
      if(flyNerves!=null) flyNerves.Reset();
      RefreshNerves();
    }
    void BuildNervesSection(StackPanel body) {
      nervesToggle=Toggle("Experimental fly nervous system",SetNerves);
      body.Children.Add(Section("Fly nervous system",nervesToggle,nervesStatus=Status(),
        new TextBlock { Text="Move the pointer nearby: Corbie notices motion, watches, then may take a curious step. A fast close pass can startle him, followed by wary watching. Calm motion lets him relax; repeated harmless passes become familiar. Memory lasts this session. The visual circuit uses real FlyWire connections; these pet reactions use simplified rules. It does not read screen contents.",TextWrapping=TextWrapping.Wrap,Foreground=MutedText },
        Actions(ActionButton("Reset attention",ResetNerveAttention))));
    }
    void ResetNerveAttention() {
      if(flyNerves!=null) flyNerves.Reset();
      cursorStartle.Reset(); if(nervesStartleTimer>0) hop=0;
      ResetShimejiNerves();
      ResetScreenNerves();
      ResetPerchIntent();
      nervesStartleTimer=0; nervesSample=false; nervesCooldown=2; RefreshNerves();
    }
    void RefreshNerves() {
      RefreshFlightStatus();
      RefreshHabitat();
      RefreshShimejiNerves();
      RefreshScreenNerves();
      RefreshPerchIntent();
      if(nervesStatus==null) return;
      if(flyNerves==null) { nervesToggle.IsEnabled=false; nervesStatus.Text=nervesError??"No circuit loaded."; return; }
      bool prior=refreshingPanel; refreshingPanel=true;
      try { nervesToggle.IsChecked=nervesEnabled; } finally { refreshingPanel=prior; }
      string state=NerveWariness>0.3?"Wary":NerveWariness>0.05?"Settling":"Calm";
      nervesStatus.Text=(nervesEnabled?"On":"Off")+" · "+flyNerves.Graph.Groups.Length+" cell-type groups · "+flyNerves.Graph.SelectedSynapses.ToString("N0")+" source synapses\nAttention "+Math.Round(flyNerves.Attention*100)+"% · habituation "+Math.Round(flyNerves.Habituation*100)+"%\n"+state+" · watchfulness "+Math.Round(NerveWariness*100)+"% · cursor familiarity "+Math.Round(cursorStartle.Familiarity*100)+"%";
    }
    bool NervesAllowed(bool dancing,bool allowShimeji=false) {
      return nervesEnabled && flyNerves!=null && !ViewerMailBusy && !TikTokActive && !FlockActive && !ShowTellActive && !cursorFollowEnabled && !paused && !shuttingDown && !fullscreenOrPresentation && !isNightSleep && !dancing && !dragging && !looking && !isFlung
        && !companion.Visible && !companion.VoiceBusy && micRequest==null && sageRequest==null && !discoveryLooking && !focusSprint.Active && !breakCoach.OnBreak && !breakCoach.Due
        && !peckingArchive && (!shimejiInteracting || allowShimeji) && !attentionBid.Active && !notificationEdge.HuntActive && !eyeRestCoach.GlideActive && !eyeRestCoach.PesterBossActive
        && !ContextMenu.IsOpen && !tray.ContextMenuStrip.Visible && !companion.Quiet && (lessonNotebook==null || !lessonNotebook.IsRecording);
    }
    void StepNerves(double dt,bool dancing) {
      nervesCursorActive=false;
      nervesMayReact=NervesAllowed(dancing,ShimejiBrainLinked && !HabitatHasInput());
      if(birdFlight.Active) { nervesMayReact=false; nervesSample=false; cursorStartle.Relax(dt); RelaxShimejiNerves(dt); return; }
      if(!nervesMayReact) { nervesSample=false; if(!paused) { cursorStartle.Relax(dt); RelaxShimejiNerves(dt); if(nervesStartleTimer>0) hop=0; nervesStartleTimer=0; } return; }
      nervesStartleTimer=Math.Max(0,nervesStartleTimer-dt);
      if(StepHabitatNerves(dt)) { RelaxShimejiNerves(dt); return; }
      if(StepShimejiNerves(dt,animationClock.Elapsed.TotalSeconds)) { cursorStartle.Relax(dt); return; }
      var p=Forms.Cursor.Position;
      // Screen/DIP conversion preserves scale across monitors. No coordinates are stored on disk.
      var source=PresentationSource.FromVisual(this); if(source==null) { nervesSample=false; return; }
      var matrix=source.CompositionTarget.TransformFromDevice;
      var absolute=matrix.Transform(new Point(p.X,p.Y));
      var head=matrix.Transform(scene.PointToScreen(new Point(112,70)));
      double sampleNow=animationClock.Elapsed.TotalSeconds,sampleAge=sampleNow-nervesLastSampleSeconds;
      bool fresh=nervesSample && sampleAge>0 && sampleAge<0.25;
      bool startled=cursorStartle.Step(dt,!cursorGame.Chasing && !scritches.Engaged && !scritches.Celebrating && hop<=0 && peckingCulpritTimer<=0 && thermalAlertTimer<=0 && offendedTimer<=0 && mischiefChaseTimer<=0 && perchArrivalTime<=0,
        fresh,sampleAge,new Point(nervesX,nervesY),absolute,head);
      double dx=fresh?(absolute.X-nervesX)/sampleAge/200:0,dy=fresh?(absolute.Y-nervesY)/sampleAge/200:0;
      nervesLastSampleSeconds=sampleNow;
      nervesX=absolute.X; nervesY=absolute.Y; nervesSample=true;
      // Use world coordinates for the walking adapter; facing mirroring must not reverse it.
      nervesCursorOffset=absolute.X-head.X;
      double distance=Math.Sqrt((absolute.X-head.X)*(absolute.X-head.X)+(absolute.Y-head.Y)*(absolute.Y-head.Y));
      double proximity=Math.Max(0,1-distance/450);
      nervesCursorActive=Math.Max(Math.Abs(dx),Math.Abs(dy))*proximity>=0.03;
      ScreenMotionSignal screenSignal;
      // Route one sensory source through the circuit per frame. Real cursor
      // reflexes remain independent; screen pixels never enter the scare path.
      if(!nervesCursorActive && TryScreenNervesInput(sampleNow,out screenSignal)) {
        dx=screenSignal.Horizontal; dy=screenSignal.Vertical; proximity=screenSignal.Strength; nervesCursorOffset=screenSignal.X;
      }
      flyNerves.Step(dt,dx,dy,proximity*weatherDrive.Sensitivity);
      ApplyCursorReaction(dt,startled);
    }
    void ApplyCursorReaction(double dt,bool startled) {
      if(startled && !cursorGame.Chasing) mood.CursorScare();
      ApplyNerveReaction(dt,startled);
    }
    void ApplyNerveReaction(double dt,bool startled,double? wariness=null) {
      double wary=wariness??cursorStartle.Wariness;
      nervesCooldown=Math.Max(0,nervesCooldown-dt);
      if(startled) {
        StopWalking(); ResetIdle(); hop=1; nervesStartleTimer=0.65; nervesCooldown=4;
        flightEscapePending=flightsEnabled && !octopusMode && mood.Snapshot.Energy>=55 && cursorStartle.Wariness>0.8;
      } else if(nervesStartleTimer<=0 && wary<=0.3 && !walking && idle==Idle.Quiet && hop<=0 && flyNerves.Attention>0.025 && nervesCooldown<=0 && !scritches.Engaged) {
        idle=lastIdle=Idle.Curious; idleTime=0; idleDuration=2.2; nervesCooldown=6;
      }
    }
    void PoseNervesStartle() {
      if(!nervesMayReact) return;
      if(nervesStartleTimer<=0) {
        if(NerveWariness>0.05 && !walking && idle==Idle.Quiet && hop<=0 && !scritches.Engaged && !scritches.Celebrating && peckingCulpritTimer<=0 && thermalAlertTimer<=0 && offendedTimer<=0 && mischiefChaseTimer<=0 && !isDragHovering && missionPoseTimer<=0 && trinketInspectTimer<=0) {
          double wary=NerveWariness;
          // A restrained, alert pose; avoid sleepy squinting or a snapping neck.
          headTilt.Angle+=Math.Max(-8,Math.Min(8,nervesCursorOffset*0.02))*wary;
          wingStretch.Angle-=5*wary;
          RestingBody.X-=Math.Sign(nervesCursorOffset)*2.5*wary*facing.ScaleX;
          if(eye.Height>2) eye.Height=octoEyeLeft.Height=octoEyeRight.Height=11+wary*2;
        }
        return;
      }
      double arc=Math.Sin(Math.PI*(1-nervesStartleTimer/0.65));
      motion.Y-=arc*15;
      wingStretch.Angle-=arc*28;
      headTilt.Angle+=arc*9;
      eye.Height=octoEyeLeft.Height=octoEyeRight.Height=13;
      glint.Visibility=Visibility.Visible;
      if(octopusMode) for(int i=0;i<tentacles.Length;i++) tentacles[i].Angle+=Math.Sin(i*0.8+ticks*12)*arc*18;
    }
    void CheckNervesIntegration() {
      CursorPassReflex.Check();
      double irritation=mood.Snapshot.Irritation;
      ApplyCursorReaction(0.033,false);
      if(mood.Snapshot.Irritation!=irritation) throw new Exception("Calm cursor motion caused irritation.");
      ApplyCursorReaction(0.033,true);
      if(mood.Snapshot.Irritation<=irritation) throw new Exception("Cursor scare did not cause irritation.");
      irritation=mood.Snapshot.Irritation;
      mood.CursorPlay();
      ApplyNerveReaction(0.033,true,0.5);
      if(mood.Snapshot.Irritation!=irritation) throw new Exception("Play or another sensory source was counted as a cursor scare.");
      for(int i=0;i<500 && !cursorGame.Chasing;i++) cursorGame.Step(0.033,0,0,true,true);
      if(!cursorGame.Chasing) throw new Exception("Could not begin intentional cursor play.");
      ApplyCursorReaction(0.033,true);
      if(mood.Snapshot.Irritation!=irritation) throw new Exception("Intentional cursor play caused irritation.");
      cursorGame.Step(0.033,1000,1000,false,false); nervesStartleTimer=0; hop=0; ResetIdle();
      if(flyNerves==null) throw new Exception(nervesError??"No fly circuit loaded.");
      SetNerves(true); paused=true; if(NervesAllowed(false)) throw new Exception("Nerves ignore pause."); paused=false;
      isNightSleep=true; if(NervesAllowed(false)) throw new Exception("Nerves ignore sleep."); isNightSleep=false;
      if(NervesAllowed(true)) throw new Exception("Nerves interrupt dance.");
      fullscreenOrPresentation=true; if(NervesAllowed(false)) throw new Exception("Nerves interrupt fullscreen."); fullscreenOrPresentation=false;
      nervesMayReact=true;
      for(int form=0;form<2;form++) {
        motion.Y=0; nervesStartleTimer=0.325; PoseNervesStartle();
        if(motion.Y>-14 || eye.Height!=13 || double.IsNaN(wingStretch.Angle)) throw new Exception("Startle pose missing or invalid.");
        SwitchForm();
      }
      nervesStartleTimer=0.5; hop=1;
      SetNerves(false); if(NervesAllowed(false) || flyNerves.Attention!=0) throw new Exception("Nerves disable failed.");
      if(nervesStartleTimer!=0 || hop!=0) throw new Exception("Disabling nerves left a startle queued.");
      SetNerves(true); nervesMayReact=true;
      cursorStartle.Step(0.033,true,true,0.08,new Point(-40,0),new Point(40,0),new Point(0,0));
      nervesCursorOffset=200; walking=false; idle=Idle.Quiet; hop=0;
      for(int form=0;form<2;form++) {
        headTilt.Angle=wingStretch.Angle=0; motion.X=0; eye.Height=11;
        PoseNervesStartle();
        if(eye.Height<=11 || eye.Height>13 || Math.Abs(headTilt.Angle)>8 || Math.Abs(motion.X)>2.5 || wingStretch.Angle>=0) throw new Exception("Missing or excessive wary pose.");
        SwitchForm();
      }
      nervesMayReact=true; walkWait=0; walking=false; wandering=true; autoPerch=true;
      routeMin=0; routeMax=500; Left=250; idle=Idle.Quiet; hop=0;
      StepWalk(0.033); if(walking || walkWait!=0) throw new Exception("Wary Corbie immediately wandered off.");
      ResetNerveAttention(); if(cursorStartle.Wariness!=0 || cursorStartle.Familiarity!=0 || flyNerves.Attention!=0) throw new Exception("Attention reset retained memory.");
      StepWalk(0.033); if(!walking) throw new Exception("Clearing wariness did not restore walking."); StopWalking();
      SetNerves(false);
      CheckControlPanel(); if(nervesStatus==null) throw new Exception("Missing nervous-system controls.");
    }
  }
}
