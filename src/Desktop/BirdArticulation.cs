using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ShapePath=System.Windows.Shapes.Path;

namespace CorbiePet {
  public sealed class BirdPoseLibrary {
    public sealed class Data { public int version { get; set; } public Dictionary<string,List<Dictionary<string,double>>> frames { get; set; } }
    public static readonly string[] Channels={"neckX","neckY","tail","wingTip","torso","leftKnee","rightKnee"};
    public static readonly double[] Limits={4,3,12,25,5,10,10};
    readonly Data data;
    public bool Loaded { get { return data!=null; } }
    public BirdPoseLibrary(string file) {
      try {
        if(new FileInfo(file).Length>256*1024) return;
        var candidate=new JavaScriptSerializer().Deserialize<Data>(File.ReadAllText(file));
        if(candidate==null || candidate.version!=1 || candidate.frames==null || candidate.frames.Count!=8) return;
        foreach(string state in new [] {"idle","walk","talk","stretch","preen","sleep","flight","dance"}) {
          List<Dictionary<string,double>> frames;
          if(!candidate.frames.TryGetValue(state,out frames) || frames==null || frames.Count!=48) return;
          foreach(var frame in frames) for(int i=0;i<Channels.Length;i++) {
            double value;
            if(frame==null || !frame.TryGetValue(Channels[i],out value) || double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value)>Limits[i]) return;
          }
        }
        data=candidate;
      } catch { } // Optional artwork data must never prevent the pet starting.
    }
    public double[] Sample(string state,double phase) {
      var result=new double[Channels.Length];
      if(data==null || state==null || double.IsNaN(phase) || double.IsInfinity(phase) || !data.frames.ContainsKey(state)) return result;
      var frames=data.frames[state]; double frame=(phase-Math.Floor(phase))*frames.Count;
      int left=(int)frame,right=(left+1)%frames.Count; double blend=frame-left;
      for(int i=0;i<result.Length;i++) result[i]=frames[left][Channels[i]]*(1-blend)+frames[right][Channels[i]]*blend;
      return result;
    }
  }

  public partial class Pet {
    readonly BirdPoseLibrary poseLibrary=new BirdPoseLibrary(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"motion","avian-poses.json"));
    readonly TranslateTransform neckJoint=new TranslateTransform();
    readonly RotateTransform tailJoint=new RotateTransform(0,78,129),wingTipJoint=new RotateTransform(0,64,113),torsoJoint=new RotateTransform(0,97,136);
    readonly double[] jointPose=new double[7];
    double appliedLeftKnee=double.NaN,appliedRightKnee=double.NaN;
    void BuildArticulation(Canvas torso) {
      ((ShapePath)torso.Children[0]).RenderTransform=tailJoint;
      var bodyTransforms=new TransformGroup(); bodyTransforms.Children.Add(torsoJoint); bodyTransforms.Children.Add(bodyRest); torso.RenderTransform=bodyTransforms;
      var head=(Canvas)torso.Children[4]; ((TransformGroup)head.RenderTransform).Children.Add(neckJoint);
      var wing=(Canvas)torso.Children[3];
      var upperGeometry=Geometry.Parse("M 74,86 C 43,90 48,118 64,125 L 76,114 C 82,103 84,95 74,86 Z");
      var tipGeometry=Geometry.Parse("M 58,110 C 55,121 65,133 78,137 C 71,124 84,116 76,109 Z");
      ((ShapePath)wing.Children[0]).Data=upperGeometry;
      // Each feather detail belongs to the piece beneath it. The old full-length
      // line stayed still when the tip turned, leaving its end outside the wing.
      var upperLine=(ShapePath)wing.Children[1];
      upperLine.Data=Geometry.Parse("M 63,105 Q 61,110 63,115"); upperLine.Clip=upperGeometry;
      var tip=new Canvas { Width=180,Height=180,RenderTransform=wingTipJoint };
      tip.Children.Add(new ShapePath { Data=tipGeometry,Fill=ColorBrush("#171829") });
      tip.Children.Add(new ShapePath { Data=Geometry.Parse("M 63,115 Q 65,123 74,129"),Stroke=ColorBrush("#45425D"),StrokeThickness=2,
        StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Clip=tipGeometry });
      wing.Children.Insert(1,tip);
    }
    void ResetArticulation() {
      Array.Clear(jointPose,0,jointPose.Length); ApplyJointPose();
    }
    string ArticulationState() {
      if(birdFlight.Active || cursorFollowingActive) return "flight";
      if(wasDancing) return "dance";
      if(companion!=null && companion.IsSpeaking) return "talk";
      if(isNightSleep || idle==Idle.Sleepy) return "sleep";
      if(walking) return "walk";
      if(idle==Idle.Stretch) return "stretch";
      if(idle==Idle.Preen) return "preen";
      return "idle";
    }
    void StepArticulation(double dt) {
      if(paused || octopusMode || dragging || isFlung) return;
      string state=ArticulationState();
      double phase=state=="walk"?stride/(2*Math.PI):state=="flight"?(cursorFollowingActive?cursorFlight.Elapsed:birdFlight.Elapsed)*24/(2*Math.PI):state=="dance"?dancePhase:ticks/2.4;
      BlendArticulation(state,phase,dt);
    }
    void BlendArticulation(string state,double phase,double dt) {
      if(double.IsNaN(dt) || double.IsInfinity(dt) || dt<=0) return;
      var target=poseLibrary.Sample(state,phase); double blend=1-Math.Exp(-Math.Min(.1,dt)*12);
      for(int i=0;i<jointPose.Length;i++) jointPose[i]+=(target[i]-jointPose[i])*blend;
      ApplyJointPose();
    }
    void ApplyJointPose() {
      neckJoint.X=jointPose[0]; neckJoint.Y=jointPose[1]; tailJoint.Angle=jointPose[2]; wingTipJoint.Angle=jointPose[3]; torsoJoint.Angle=jointPose[4];
      if(leftFootVisual==null || rightFootVisual==null) return;
      // Bend the leg above the ankle; toe positions remain planted at rest.
      if(double.IsNaN(appliedLeftKnee) || Math.Abs(appliedLeftKnee-jointPose[5])>.01) {
        leftFootVisual.Data=Geometry.Parse(string.Format(CultureInfo.InvariantCulture,"M 92,136 Q {0},141 90,147 L 81,148 M 90,147 L 99,149",91+jointPose[5]*.45)); appliedLeftKnee=jointPose[5];
      }
      if(double.IsNaN(appliedRightKnee) || Math.Abs(appliedRightKnee-jointPose[6])>.01) {
        rightFootVisual.Data=Geometry.Parse(string.Format(CultureInfo.InvariantCulture,"M 110,136 Q {0},141 109,147 L 103,150 M 109,147 L 119,149",110+jointPose[6]*.45)); appliedRightKnee=jointPose[6];
      }
    }
    void CheckArticulation() {
      if(!poseLibrary.Loaded) throw new Exception("Authored pose library is missing or invalid.");
      ResetIdle(); var foot=leftFootVisual.TransformToAncestor(scene).Transform(new Point(90,147));
      foreach(string state in new [] {"idle","talk","stretch","preen","sleep","flight","dance","walk"}) {
        for(int frame=0;frame<48;frame++) {
          BlendArticulation(state,frame/48.0,.033);
          for(int i=0;i<jointPose.Length;i++) if(double.IsNaN(jointPose[i]) || Math.Abs(jointPose[i])>BirdPoseLibrary.Limits[i]+.001) throw new Exception("Joint escaped its range.");
          if((leftFootVisual.TransformToAncestor(scene).Transform(new Point(90,147))-foot).Length>.001) throw new Exception("Secondary articulation moved the planted foot.");
        }
      }
      ResetArticulation(); walking=true; stride=1; StepArticulation(.1); if(Math.Abs(wingTipJoint.Angle)<.1) throw new Exception("Walking wing follow-through missing.");
      paused=true; double held=tailJoint.Angle; StepArticulation(.1); if(tailJoint.Angle!=held) throw new Exception("Paused rig moved."); paused=false;
      SwitchForm(); if(tailJoint.Angle!=0 || neckJoint.X!=0) throw new Exception("Form switch kept stale joints."); SwitchForm(); ResetIdle();
      var missing=new BirdPoseLibrary(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"motion","missing.json"));
      if(missing.Loaded || missing.Sample("walk",.5)[0]!=0) throw new Exception("Optional pose fallback failed.");
      string invalid=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"corbie-invalid-pose-"+Guid.NewGuid().ToString("N")+".json");
      try {
        File.WriteAllText(invalid,"{\"version\":1,\"frames\":{}}");
        if(new BirdPoseLibrary(invalid).Loaded) throw new Exception("Invalid pose library accepted.");
      } finally { if(File.Exists(invalid)) File.Delete(invalid); }
    }
    void RenderArticulationPreview(string file) {
      var drawing=new DrawingVisual();
      using(var context=drawing.RenderOpen()) {
        context.DrawRectangle(ColorBrush("#EAE5F1"),null,new Rect(0,0,1440,360));
        string[] states={"idle","talk","stretch","preen","sleep","flight","dance","walk"};
        for(int row=0;row<2;row++) for(int column=0;column<8;column++) {
          ResetIdle(); beakOpen.Angle=0; wingStretch.Angle=states[column]=="flight"?35:states[column]=="stretch"?-48:0;
          double phase=0,extreme=row==0?double.PositiveInfinity:double.NegativeInfinity;
          for(int frame=0;frame<48;frame++) {
            double sample=poseLibrary.Sample(states[column],frame/48.0)[3];
            if(row==0?sample<extreme:sample>extreme) { extreme=sample;phase=frame/48.0; }
          }
          for(int frame=0;frame<20;frame++) BlendArticulation(states[column],phase,.033);
          if(states[column]=="talk") beakOpen.Angle=row==0?18:3;
          if(states[column]=="sleep") PoseNightSleep();
          scene.Measure(new Size(180,180)); scene.Arrange(new Rect(0,0,180,180)); scene.UpdateLayout();
          var image=new System.Windows.Media.Imaging.RenderTargetBitmap(180,180,96,96,PixelFormats.Pbgra32); image.Render(scene);
          context.DrawImage(image,new Rect(column*180,row*180,180,180));
        }
      }
      var sheet=new System.Windows.Media.Imaging.RenderTargetBitmap(1440,360,96,96,PixelFormats.Pbgra32); sheet.Render(drawing);
      var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(sheet));
      using(var stream=File.Create(file)) encoder.Save(stream); ResetIdle();
    }
  }
}
