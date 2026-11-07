using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Web.Script.Serialization;

namespace CorbiePet {
  public partial class Pet {
    RoostMemory roost;
    NestMemory nest;
    MissionBoard mission;
    TrinketToybox toybox;
    readonly RavenMischief mischief=new RavenMischief();
    readonly LaunchWatcher launchWatcher=new LaunchWatcher();
    readonly GameSessionBuddy gameSession=new GameSessionBuddy();
    TerritoryMap territory;
    MoltSeasonBook moltBook;
    ChronicleBook chronicleBook;
    TextBlock territoryStatus, moltSeasonStatus, chronicleStatus;
    ListBox chronicleList, territoryMapList;
    CheckBox territoryToggle, moltSeasonToggle, chronicleToggle;
    double territorySaveWait=40;
    double loreTickWait=2;
    readonly FocusSprint focusSprint=new FocusSprint();
    readonly CursorGame cursorGame=new CursorGame();
    readonly AttentionBid attentionBid=new AttentionBid();
    CancellationTokenSource forageRequest;
    double forageWait=30,roostSaveWait=60;
    double sensedTemperature;
    DateTime sensedTemperatureUtc=DateTime.MinValue, sensedMusicUtc=DateTime.MinValue;
    bool focusHeld;
    ListBox forageList,journalList,trinketList;
    readonly System.Collections.Generic.List<ForageItem> forageSuggestions=new System.Collections.Generic.List<ForageItem>();
    string lastForageStatus="Scan for older installers, forgotten downloads and possible copies.";
    TextBlock forageStatus,focusStatus,focusFeedback,roostStatus,trinketStatus,missionStatus,missionFeedback;
    TextBox focusTask,missionTitle,missionStep,missionNote;
    ComboBox focusMinutes;
    CheckBox journalToggle,contextToggle,cursorToggle,bidToggle,forageToggle;
    CheckBox clipboardToggle,foleyToggle,skyToggle,pesterToggle,shimejiToggle;
    CheckBox wakeToggle,mischiefToggle,perchPersonalityToggle,launchReactToggle,gameSessionToggle;
    string journalViewKey,trinketViewKey;
    readonly EyeRestCoach eyeRestCoach=new EyeRestCoach();
    readonly ShimejiWatcher shimejiWatcher=new ShimejiWatcher();
    double shimejiProbeWait=1,shimejiNoticeTimer,shimejiInteractionTimer,shimejiLostTimer;
    double shimejiTargetX,shimejiTargetY;
    bool shimejiInteracting;
    string pendingShimejiRemark;
    int shimejiPlayMode; // 0 watch, 1 tug-down, 2 bounce, 3 flee, 4 circle, 5 peck-guard
    double shimejiPlayTimer;
    double shimejiTugPulse;
    double shimejiOrbit;
    bool shimejiBounceAnnounced;
    bool shimejiTugAnnounced;
    bool shimejiStealAnnounced;
    bool territoryPeckWarned;
    bool morningRitualDone;
    int morningRitualDay=-1;
    double returnToRoostTimer;
    double chroniclePoseTimer;
    bool weeklyChronicleOffered;
    TextBlock chroniclePinStatus;
    TextBlock trinketPowerStatus;
    double shimejiSulkTimer;
    double shimejiTruceTimer;
    double missionShepherdCooldown;
    double dayPoseTimer;
    CircadianPhase dayPhase=CircadianPhase.Daylight;
    WeatherKind dayWeather=WeatherKind.Clear;
    int lastScrapCondition=-1;

    void InitializeRoost() {
      roost=new RoostMemory(testing?null:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Corbie","roost.json"));
      nest=new NestMemory(testing?null:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Corbie","nest-memory.json"));
      mission=new MissionBoard(testing?null:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Corbie","mission.json"),testing);
      toybox=new TrinketToybox(testing?null:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Corbie","toybox.json"),testing);
      territory=new TerritoryMap(testing?null:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Corbie","territory.json"));
      moltBook=new MoltSeasonBook(testing?null:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Corbie","molt-season.json"));
      chronicleBook=new ChronicleBook(testing?null:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Corbie","chronicle.json"));
      nestEggPath=testing?null:ShipRitual.DefaultPath;
      if(!testing) SecretPlaylist.EnsureFile();
      nest.ApplyOnLaunch(mood,roost.Data.JournalEnabled);
      mischief.Enabled=roost.Data.MischiefEnabled;
      if(toybox!=null && !string.IsNullOrEmpty(toybox.WearingId) && roost.Data.Trinkets!=null) {
        var worn=roost.Data.Trinkets.FirstOrDefault(t=>t!=null && string.Equals(t.Id,toybox.WearingId,StringComparison.OrdinalIgnoreCase));
        if(worn!=null) {
          wearingAccessory=true;
          if(trinketVisual!=null) {
            try { trinketVisual.Fill=(SolidColorBrush)new BrushConverter().ConvertFromString(worn.ColorHex??"#FFD700"); } catch { }
            trinketVisual.Visibility=Visibility.Visible;
          }
        }
      }
      UpdateQuestScrap();
      focusSprint.Restore(roost.Data.FocusTitle,roost.Data.FocusRemaining);
      companion.RequestAttention=OfferAttention;
      CreatureFoley.Enabled=roost.Data.FoleyEnabled;
      ShowInTaskbar=roost.Data.TaskbarButton;
      ApplyTrinketMolt();
      if(PiperVoice.Configured && roost.Data.VoiceEnabled) {
        companion.SetVoice(true, false);
        if(voiceItem!=null) voiceItem.IsChecked=true;
        if(trayVoice!=null) trayVoice.Checked=true;
      }
    }
    void SaveRoost() {
      roost.Data.FocusTitle=focusSprint.Title; roost.Data.FocusRemaining=focusSprint.Remaining; roost.Save();
    }
    void CloseRoost() { if(forageRequest!=null) forageRequest.Cancel(); attentionBid.Cancel(); if(nest!=null) nest.NoteSessionEnd(mood.Snapshot,roost.Data.JournalEnabled); if(territory!=null) territory.Save(); if(moltBook!=null) moltBook.Save(); if(chronicleBook!=null) chronicleBook.Save(); SaveRoost(); }
    bool CanInitiate() {
      return !ViewerMailBusy && !TikTokActive && !FlockActive && !ShowTellActive && !cursorFollowEnabled && !paused && !shuttingDown && !fullscreenOrPresentation && !wasDancing && !companion.Quiet && !companion.Visible && !companion.VoiceBusy && !looking && !discoveryLooking && !dragging && !isFlung && !peckingArchive && !isDragHovering && micRequest==null && !ContextMenu.IsOpen && !tray.ContextMenuStrip.Visible && !focusSprint.Active && !breakCoach.OnBreak && !breakCoach.Due && !(gameSession!=null && gameSession.SoftMute && !CurrentTrinketPowers().SoftMuteTolerant);
    }
    void OfferAttention(Action action) {
      if(!CanInitiate()) return;
      if(!roost.Data.AttentionBids) { action(); return; }
      attentionBid.Offer(action); StopWalking(); ResetIdle();
    }
    // Actions that change user state always require a click, regardless of the
    // casual-chatter attention preference.
    void OfferActionBid(Action action) {
      if(!CanInitiate()) return;
      attentionBid.Offer(action); StopWalking(); ResetIdle();
    }
    void StepRoost(double dt,double idleSeconds) {
      focusHeld=paused || idleSeconds>=120 || breakCoach.OnBreak || breakCoach.Due;
      if(nest!=null) nest.Tick(dt,roost.Data.JournalEnabled);
      focusSprint.Step(dt,!focusHeld);
      attentionBid.Step(dt,CanInitiate());
      bool canFinish=!paused && !shuttingDown && !fullscreenOrPresentation && !companion.Visible && !companion.VoiceBusy && !looking && !dragging && !isFlung && micRequest==null && !ContextMenu.IsOpen && !tray.ContextMenuStrip.Visible && !breakCoach.OnBreak && !breakCoach.Due;
      if(focusSprint.CompletionPending && canFinish) {
        focusSprint.Acknowledge();
        roost.Remember("focus","Finished a focus sprint: "+focusSprint.Title,DateTime.UtcNow);
        nest.NoteFocusComplete(roost.Data.JournalEnabled);
        if(mission!=null) mission.NoteFocusComplete(focusSprint.Title);
        var earnedTrinket=roost.ScavengeTrinket(new Random());
        if(earnedTrinket!=null) ApplyTrinketMolt();
        SaveRoost();
        string message="Sprint done. You showed up for "+focusSprint.Title+". Now unbend your spine, you productive goblin.";
        if(earnedTrinket!=null) message+=" Found a shiny '"+earnedTrinket.Name+"' for my hoard!";
        if(mission!=null && mission.Active) message+=" Mission progress noted.";
        if(companion.Quiet) companion.SaySilent(message); else companion.Say(message);
        CreatureFoley.Play(FoleyKind.Clack,companion.Quiet);
        ScheduleReturnToRoost();
        BeginBreakNow();
      }
      StepMissionGlance();
      StepMissionShepherd(dt);
      StepPerchPersonality(dt);
      StepMischief(dt);
      StepDayCycle(dt);
      StepLaunchWatcher(dt);
      StepGameSession(dt);
      StepLoreSystems(dt);
      if(mission!=null && mission.Active) UpdateQuestScrap();
      roostSaveWait-=dt;
      if(roostSaveWait<=0) { roostSaveWait=60; SaveRoost(); }
      if(!testing && roost.Data.ForageEnabled && !paused && !fullscreenOrPresentation && !focusSprint.Active) {
        forageWait-=dt;
        if(forageWait<=0) { forageWait=1800; var ignored=ScanDownloads(false); }
      }
      if(!testing) {
        TryShipRitual();
        StepNotificationForage(dt);
        StepShimejiSocial(dt);
      }
    }
    void StepShimejiSocial(double dt) {
      if(cursorFollowEnabled) { EndShimejiInteraction();return; }
      shimejiNoticeTimer=Math.Max(0,shimejiNoticeTimer-dt);
      if(!roost.Data.ShimejiEnabled) { EndShimejiInteraction(); ResetShimejiSessionFlags(); shimejiWatcher.Reset(); pendingShimejiRemark=null; return; }
      bool brainLinked=ShimejiBrainLinked;
      if(brainLinked && (!NervesAllowed(wasDancing,true) || birdFlight.Active || HabitatHasInput())) { EndShimejiInteraction(); shimejiMotion.Reset(); pendingShimejiRemark=null; return; }
      if(pendingShimejiRemark!=null && (!brainLinked || ShimejiBrainEngaged()) && CanInitiate() && !DayCycle.MuteChatter(dayPhase) && !isNightSleep) {
        string remark=pendingShimejiRemark; pendingShimejiRemark=null;
        OfferAttention(()=>companion.Say(remark));
      } else if(pendingShimejiRemark!=null && (DayCycle.MuteChatter(dayPhase) || isNightSleep)) {
        pendingShimejiRemark=null;
      }
      shimejiProbeWait-=dt;
      ShimejiObservation seen=null;
      if(shimejiProbeWait<=0) {
        shimejiProbeWait=brainLinked?2.2:shimejiInteracting?0.45:2.2;
        Point origin=PointToScreen(new Point(Width/2,Height/2));
        seen=shimejiWatcher.Poll(origin.X,origin.Y);
        if(seen.Present && seen.HasTarget) {
          Point local=PointFromScreen(new Point(seen.TargetX,seen.TargetY));
          shimejiTargetX=Left+local.X; shimejiTargetY=Top+local.Y;
          shimejiLostTimer=2.8;
          if(!brainLinked && (seen.Change==ShimejiChange.Arrived || seen.Change==ShimejiChange.Multiplied)) {
            shimejiInteracting=true;
            shimejiInteractionTimer=seen.Change==ShimejiChange.Arrived?16:10;
            PickShimejiPlay(true);
          } else if(shimejiInteracting) {
            shimejiInteractionTimer=Math.Max(shimejiInteractionTimer,4.5);
          }
        }
        if(seen!=null && seen.Change!=ShimejiChange.None) NoticeShimejiChange(seen);
      }
      SampleShimejiMotion(dt);
      if(brainLinked) {
        if(!ShimejiBrainEngaged()) { EndShimejiInteraction(); return; }
        if(!shimejiInteracting) { shimejiInteracting=true; shimejiInteractionTimer=10; shimejiLostTimer=2.8; PickShimejiPlay(true); }
        if(shimejiStartle.Wariness>0.3 && shimejiPlayMode!=3) { shimejiPlayMode=3; shimejiPlayTimer=2.5; }
        if(hop>0 || nervesStartleTimer>0 || weatherDrive.Shelter>0.6) { if(walking) StopWalking(); return; }
      }
      if(!shimejiInteracting) return;
      shimejiInteractionTimer=Math.Max(0,shimejiInteractionTimer-dt);
      shimejiLostTimer=Math.Max(0,shimejiLostTimer-dt);
      shimejiPlayTimer=Math.Max(0,shimejiPlayTimer-dt);
      bool canMove=!paused && !isNightSleep && !shuttingDown && !fullscreenOrPresentation && !wasDancing && !looking && !dragging && !isFlung && !peckingArchive && !isDragHovering && !companion.Visible && !companion.VoiceBusy && micRequest==null && !focusSprint.Active && !breakCoach.OnBreak && !breakCoach.Due && !notificationEdge.HuntActive && !eyeRestCoach.GlideActive && !eyeRestCoach.PesterBossActive && mischiefChaseTimer<=0;
      if(shimejiInteractionTimer<=0 || shimejiLostTimer<=0) { EndShimejiInteraction(); return; }
      direction=shimejiTargetX<Left+Width/2?-1:1; facing.ScaleX=direction;
      if(!canMove) { if(walking) StopWalking(); return; }
      if(notificationEdge.HuntActive) {
        shimejiTruceTimer=Math.Max(shimejiTruceTimer,4);
        shimejiPlayMode=0; shimejiPlayTimer=1.5;
      }
      shimejiTruceTimer=Math.Max(0,shimejiTruceTimer-dt);
      shimejiSulkTimer=Math.Max(0,shimejiSulkTimer-dt);
      if(shimejiSulkTimer>0) {
        SetPerching(false); StopWalking();
        headTilt.Angle=22; wingStretch.Angle=-30; hop=0;
        if(eye!=null) eye.Height=octoEyeLeft.Height=octoEyeRight.Height=3;
        shimejiNoticeTimer=Math.Max(shimejiNoticeTimer,0.2);
        return;
      }
      if(shimejiPlayTimer<=0) PickShimejiPlay(false);
      var area=CurrentDesktopBounds();
      double cx=Left+Width/2, cy=Top+Height*0.35;
      double dxS=shimejiTargetX-cx, dyS=shimejiTargetY-cy;
      double dist=Math.Sqrt(dxS*dxS+dyS*dyS);
      // Real climb: mascot clearly above head/shoulders and overlapping body.
      // Bounce/joust closes the gap on purpose - that is NOT climbing.
      bool overlappingX=Math.Abs(shimejiTargetX-cx)<Width*0.55;
      bool aboveShoulders=shimejiTargetY<Top+Height*0.22;
      bool climbing=overlappingX && aboveShoulders && shimejiPlayMode!=2 && shimejiPlayMode!=3;
      if(climbing && shimejiPlayMode!=1 && (!brainLinked?random.Next(100)<10:ShimejiFamiliarity>0.55 && flyNerves.Attention>0.04 && shimejiStartle.Wariness<0.1)) {
        shimejiPlayMode=1; shimejiPlayTimer=2.8; shimejiTugPulse=0;
        hop=1; CreatureFoley.Play(FoleyKind.Whoosh,companion.Quiet);
        if(!shimejiTugAnnounced && random.Next(100)<40) {
          shimejiTugAnnounced=true;
          pendingShimejiRemark=octopusMode
            ? "It jumped on me. I am being used as scaffolding. Rude."
            : "It climbed me. I am a ladder now. This is undignified.";
        }
      } else if(!shimejiBounceAnnounced && shimejiPlayMode==2 && dist<75 && hop>0) {
        shimejiBounceAnnounced=true;
        pendingShimejiRemark=octopusMode
          ? "Joust engaged. Tentacle vs bean."
          : "Bounce joust. I am winning this staring contest with physics.";
      }
      if(shimejiPlayMode==1 && shimejiPlayTimer<=0.05 && climbing) {
        shimejiSulkTimer=2.8; hop=0;
        CreatureFoley.Play(FoleyKind.Whoosh,companion.Quiet);
      }
      double desiredX, desiredY;
      if(shimejiPlayMode==1) {
        // Tug-down: shimeji leaps up; Corbie gets yanked toward the floor in pulses.
        shimejiTugPulse+=dt;
        desiredX=shimejiTargetX-Width/2+Math.Sin(ticks*8)*10;
        desiredY=Math.Min(area.Bottom-Height-4, Top+dt*(90+Math.Sin(shimejiTugPulse*9)*40));
        if(shimejiTugPulse>0.35) { hop=Math.Max(hop,0.85); shimejiTugPulse=0; }
        headTilt.Angle=18; wingStretch.Angle=-22;
        SetPerching(false);
      } else if(shimejiPlayMode==2) {
        // Bounce joust: close the gap then hop.
        desiredX=shimejiTargetX-Width/2; desiredY=shimejiTargetY-Height*0.55;
        if(dist<70 && hop<=0) hop=1;
        headTilt.Angle=-10;
      } else if(shimejiPlayMode==3) {
        // Flee then re-engage later via PickShimejiPlay.
        desiredX=cx-direction*160-Width/2; desiredY=Math.Max(area.Top+20, Top-8);
        hop=Math.Max(hop,0.5); wingStretch.Angle=-18;
        SetPerching(false);
      } else if(shimejiPlayMode==4) {
        // Circle / orbit the little menace.
        shimejiOrbit+=dt*2.2;
        desiredX=shimejiTargetX+Math.Cos(shimejiOrbit)*110-Width/2;
        desiredY=shimejiTargetY+Math.Sin(shimejiOrbit)*36-Height*0.5;
        SetPerching(false);
      } else if(shimejiPlayMode==5) {
        desiredX=shimejiTargetX+(direction<0?90:-90)-Width/2;
        desiredY=shimejiTargetY-Height*0.5;
        if(dist<110 && hop<=0 && random.Next(40)==0) {
          hop=1; headTilt.Angle=14;
          CreatureFoley.Play(FoleyKind.Clack,companion.Quiet);
        }
        if(mission!=null && mission.Active) UpdateQuestScrap();
      } else if(shimejiPlayMode==6) {
        desiredX=shimejiTargetX-Width/2; desiredY=shimejiTargetY-Height*0.45;
        if(dist<70) {
          hop=Math.Max(hop,0.8); headTilt.Angle=-16; wingStretch.Angle=-20;
          if(wearingAccessory && toybox!=null && random.Next(120)==0) {
            if(toybox.TryStealWorn()) {
              wearingAccessory=false;
              if(trinketVisual!=null) trinketVisual.Visibility=Visibility.Hidden;
              shimejiSulkTimer=3.5;
              CreatureFoley.Play(FoleyKind.Caw,companion.Quiet);
              if(!shimejiStealAnnounced) {
                shimejiStealAnnounced=true;
                pendingShimejiRemark="It stole my shiny. This is war.";
              }
            }
          } else if(!wearingAccessory && hop<=0 && random.Next(80)==0) hop=1;
        }
        SetPerching(false);
      } else if(shimejiPlayMode==7) {
        SetPerching(true);
        desiredX=shimejiTargetX+(direction<0?55:-55)-Width/2;
        desiredY=shimejiTargetY-Height*0.7;
        if(dist<85 && hop<=0 && random.Next(50)==0) {
          hop=1; wingStretch.Angle=-26;
          CreatureFoley.Play(FoleyKind.Clack,companion.Quiet);
        }
      } else {
        double safeGap=octopusMode?105:125;
        desiredX=shimejiTargetX+(direction<0?safeGap:-safeGap)-Width/2;
        desiredY=shimejiTargetY-Height*0.62;
      }
      desiredX=Math.Max(area.Left,Math.Min(area.Right-Width,desiredX));
      desiredY=Math.Max(area.Top,Math.Min(area.Bottom-Height,desiredY));
      double dx=desiredX-Left,dy=desiredY-Top,distance=Math.Sqrt(dx*dx+dy*dy);
      double speed=(shimejiPlayMode==1?130:shimejiPlayMode==3?150:shimejiPlayMode==4?110:(octopusMode?72:96))*(brainLinked?weatherDrive.Activity:1);
      if(distance>12) {
        walking=true; stride+=dt*11*mood.MovementSpeed;
        double step=Math.Min(distance,speed*dt*mood.MovementSpeed);
        Left+=dx/distance*step; Top+=dy/distance*step;
      } else if(walking) StopWalking();
      shimejiNoticeTimer=Math.Max(shimejiNoticeTimer,0.25);
    }
    void PickShimejiPlay(bool firstMeet) {
      if(shimejiTruceTimer>0 || shimejiSulkTimer>0) { shimejiPlayMode=0; shimejiPlayTimer=2; return; }
      int roll=firstMeet?random.Next(40):random.Next(100);
      if(firstMeet || roll<18) shimejiPlayMode=0;
      else if(roll<34) shimejiPlayMode=1;
      else if(roll<48) shimejiPlayMode=2;
      else if(roll<58) shimejiPlayMode=3;
      else if(roll<70) shimejiPlayMode=4;
      else if(roll<80) shimejiPlayMode=5;
      else if(roll<90) shimejiPlayMode=6;
      else shimejiPlayMode=7;
      if(ShimejiBrainLinked) shimejiPlayMode=MascotMotionSense.ChoosePlay(flyNerves.Attention,shimejiStartle.Wariness,ShimejiFamiliarity,weatherDrive.Shelter,firstMeet,roll);
      if(!firstMeet && (!ShimejiBrainLinked || flyNerves.Attention>0.04 && shimejiStartle.Wariness<0.3 && weatherDrive.Shelter<0.6) && roost!=null && roost.Data.TerritoryEnabled && territory!=null
          && territory.SoftDefendChance(shimejiTargetX, shimejiTargetY, random)) {
        shimejiPlayMode=5; // peck-guard soft defend of claimed roost
        if(!territoryPeckWarned) {
          territoryPeckWarned=true;
          pendingShimejiRemark=octopusMode
            ? "Territory pulse. That corner is claimed."
            : "That roost is claimed. Soft peck warning.";
        }
      }
      shimejiPlayTimer=2.4+random.NextDouble()*3.2;
      shimejiTugPulse=0;
      if(shimejiPlayMode==1 || shimejiPlayMode==3 || shimejiPlayMode==4 || shimejiPlayMode==7) SetPerching(false);
      if(!firstMeet && shimejiPlayMode==1 && !shimejiTugAnnounced && random.Next(100)<35) {
        shimejiTugAnnounced=true;
        pendingShimejiRemark=octopusMode
          ? "Vertical harassment detected. Deploying counter-squish."
          : "It yanked my tailfeathers. I am filing a formal complaint with gravity.";
      } else if(!firstMeet && shimejiPlayMode==3 && random.Next(100)<20)
        pendingShimejiRemark="Tactical retreat from the bean. I will be back.";
      else if(!firstMeet && shimejiPlayMode==5 && mission!=null && mission.Active && random.Next(100)<25)
        pendingShimejiRemark="Stay away from the quest scrap, you sticky-handed mascot.";
    }
    void NoticeShimejiChange(ShimejiObservation seen) {
      shimejiNoticeTimer=4;
      if(!ShimejiBrainLinked) { StopWalking(); ResetIdle(); hop=seen.Change==ShimejiChange.Departed?0:1; }
      if(seen.Change==ShimejiChange.Arrived) {
        pendingShimejiRemark=octopusMode
          ? "Hold on. Another desktop creature? Fine. It can have one corner. I have eight arms and seniority."
          : "Hold on. A Shimeji? Nobody told me we were getting a roommate. I am watching the sticky little gremlin.";
        if(!ShimejiBrainLinked) CreatureFoley.Play(octopusMode?FoleyKind.SuctionPop:FoleyKind.Caw,companion.Quiet);
        roost.Remember("shimeji","Noticed another desktop creature arrive",DateTime.UtcNow);
        PickShimejiPlay(true);
      } else if(seen.Change==ShimejiChange.Multiplied) {
        pendingShimejiRemark="It multiplied. Of course it bloody multiplied. The desktop now has an ecosystem.";
        roost.Remember("shimeji","The Shimeji population increased",DateTime.UtcNow);
        if(!ShimejiBrainLinked) { shimejiPlayMode=2; shimejiPlayTimer=3.5; hop=1; }
      } else {
        EndShimejiInteraction();
        ResetShimejiSessionFlags();
        pendingShimejiRemark="The Shimeji has left. I remain. Natural selection is beautiful.";
        roost.Remember("shimeji","The other desktop creature departed",DateTime.UtcNow);
      }
    }
    void EndShimejiInteraction() {
      bool wasInteracting=shimejiInteracting;
      shimejiInteracting=false; shimejiInteractionTimer=shimejiLostTimer=0;
      shimejiPlayMode=0; shimejiPlayTimer=0; shimejiTugPulse=0;
      if(wasInteracting && walking) { StopWalking(); ResetIdle(); }
    }
    void ResetShimejiSessionFlags() {
      shimejiBounceAnnounced=false;
      shimejiTugAnnounced=false;
      shimejiStealAnnounced=false;
      shimejiSulkTimer=0;
      shimejiTruceTimer=0;
      ResetShimejiNerves();
    }
    void TryShipRitual() {
      if(shipCelebratedThisSession || string.IsNullOrEmpty(nestEggPath)) return;
      var egg=ShipRitual.PeekFresh(nestEggPath,6);
      if(egg==null) return;
      shipCelebratedThisSession=true;
      ShipRitual.MarkCelebrated(nestEggPath);
      SetPerching(false);
      StopWalking(); ResetIdle();
      hop=1.0;
      mood.FileSuccess();
      flashBadgeTimer=4.5;
      if(eye!=null) eye.Fill=ColorBrush("#FFD700");
      CreatureFoley.Play(FoleyKind.Caw,companion.Quiet);
      string line=ShipRitual.VictoryLine(egg,octopusMode);
      if(companion.Quiet) companion.SaySilent(line); else companion.Say(line);
      roost.Remember("ship","Ship ritual celebrated for "+(egg.Build??"build"),DateTime.UtcNow);
      var shipLoot=roost.ScavengePlayTrinket(random,"play_ship");
      if(shipLoot!=null) ApplyTrinketMolt();
    }
    void StepNotificationForage(double dt) {
      notificationEdge.Tick(dt);
      if(notificationEdge.HuntActive || shimejiInteracting || paused || fullscreenOrPresentation || isFlung || eyeRestCoach.GlideActive || eyeRestCoach.PesterBossActive) return;
      notifProbeWait-=dt;
      if(notifProbeWait>0) return;
      notifProbeWait=7+random.NextDouble()*5;
      if(!CanInitiate() || !wandering || isNightSleep) return;
      var area=CurrentDesktopBounds();
      var signal=notificationEdge.Probe(area.Left,area.Top,area.Right,area.Bottom,random);
      if(!signal.Found) return;
      if(!notificationEdge.TryBeginHunt(signal,Left,Top,50)) return;
      SetPerching(false);
      StopWalking(); ResetIdle();
      hop=1;
      CreatureFoley.Play(FoleyKind.Whoosh,companion.Quiet);
      if(random.Next(100)<45 && CanInitiate()) {
        OfferAttention(()=>companion.Say(octopusMode
          ? "Something blinked on the notification edge. I hunt the shine -- never the words."
          : "Toast-glow on the taskbar edge. I hop to the noise. I do not read it."));
      }
    }
    bool StepCursorPlay(double dt,bool dancing) {
      if(testing) return false;
      var pointer=System.Windows.Forms.Cursor.Position;
      var local=PointFromScreen(new Point(pointer.X,pointer.Y));
      bool allowed=roost.Data.CursorPlay && wandering && !dancing && CanInitiate() && !attentionBid.Active && peckingCulpritTimer<=0 && thermalAlertTimer<=0 && offendedTimer<=0;
      bool pounce=cursorGame.Step(dt,pointer.X,pointer.Y,local.X>-150 && local.X<Width+150 && Math.Abs(local.Y-95)<110,allowed);
      if(pounce) { StopWalking(); ResetIdle(); hop=1; mood.CursorPlay(); }
      if(cursorGame.Chasing) {
        StopWalking(); ResetIdle(); direction=local.X<Width/2?-1:1;
        facing.ScaleX=direction; double distance=local.X-Width/2;
        // Follow only the existing perch route; never move or click the pointer.
        if(!autoPerch) UpdateWalkRoute();
        double before=Left;
        if(Math.Abs(distance)>22) Left=Math.Max(routeMin,Math.Min(routeMax,Left+Math.Sign(distance)*Math.Min(Math.Abs(distance),dt*38)));
        if(autoPerch) perchOffset+=Left-before;
      }
      return cursorGame.Chasing || pounce || attentionBid.Active;
    }
        void PoseRoost() {
      if(attentionBid.Active) {
        headTilt.Angle=12+Math.Sin(ticks*3)*3; wingStretch.Angle=-12;
        glint.Visibility=Visibility.Visible; RestingBody.Y-=Math.Abs(Math.Sin(ticks*3))*2;
        if(octopusMode) tentacles[0].Angle+=25;
        ToolTip="Corbie has a thought. Click him to hear it, or let him settle.";
      } else {
        if(shimejiSulkTimer>0) {
          headTilt.Angle=22; wingStretch.Angle=-28; RestingBody.Y+=1;
        } else if(shimejiInteracting && shimejiPlayMode==1) {
          headTilt.Angle=16+Math.Sin(ticks*9)*6; wingStretch.Angle=-24; motion.Y+=Math.Abs(Math.Sin(ticks*8))*3;
        } else if(shimejiNoticeTimer>0) { headTilt.Angle=-13+Math.Sin(ticks*4)*2; wingStretch.Angle=-8; }
        if(cursorGame.Chasing) { headTilt.Angle=-9; squish.ScaleY-=0.06; }
        if(mischiefChaseTimer>0) {
          headTilt.Angle=-14; wingStretch.Angle=-Math.Sin(ticks*10)*14;
          motion.Y-=Math.Abs(Math.Sin(ticks*10))*3;
        } else if(missionPoseTimer>0) {
          headTilt.Angle=-16+Math.Sin(ticks*5)*4; wingStretch.Angle=-10;
        } else if(roost!=null && roost.Data.PerchPersonalityEnabled && perchMoodKind!=PerchKind.Other
                  && hop<=0 && !walking && idle==Idle.Quiet) {
          if(perchMoodKind==PerchKind.Music) {
            RestingBody.Y-=Math.Abs(Math.Sin(ticks*6))*2.2;
            wingStretch.Angle=Math.Sin(ticks*5)*10;
          } else if(perchMoodKind==PerchKind.Code) {
            headTilt.Angle=-6+Math.Sin(ticks*1.4)*2; wingStretch.Angle=-4;
          } else if(perchMoodKind==PerchKind.Art) {
            headTilt.Angle=Math.Sin(ticks*0.9)*8;
            wingStretch.Angle=-Math.Abs(Math.Sin(ticks*0.7))*6;
          } else if(perchMoodKind==PerchKind.School) {
            headTilt.Angle=4; wingStretch.Angle=-8;
          }
        } else if(roost!=null && roost.Data.SkyRoostEnabled && hop<=0 && !walking && idle==Idle.Quiet
                  && !shimejiInteracting && mischiefChaseTimer<=0 && shimejiSulkTimer<=0) {
          if(dayPhase==CircadianPhase.Morning) { wingStretch.Angle=-12; headTilt.Angle=6; }
          else if(dayPhase==CircadianPhase.Daylight) { RestingBody.Y-=Math.Abs(Math.Sin(ticks*3.2))*1.2; }
          else if(dayPhase==CircadianPhase.Twilight) { headTilt.Angle=10; wingStretch.Angle=6; }
          if(dayWeather==WeatherKind.Rain || dayWeather==WeatherKind.Storm) {
            headTilt.Angle+=4; wingStretch.Angle-=8; squish.ScaleY-=0.03;
          } else if(dayWeather==WeatherKind.Snow) {
            squish.ScaleX+=0.04; RestingBody.Y+=0.8;
          }
        }
        string tip="Corbie - drag to perch or fling - double-click to hop\nRight-click for Corbie's Nest";
        if(mission!=null && mission.Active) tip+="\nMission: "+mission.Title+(mission.HasNextStep?" - "+mission.NextStep:"");
        if(perchMoodKind!=PerchKind.Other) tip+="\nPerch mood: "+perchMoodKind;
        if(wearingAccessory) tip+="\nWearing a shiny";
        ToolTip=tip;
      }
    }
    string BuildSensoryContext() {
      if(!roost.Data.ContextEnabled) return null;
      DateTime now=DateTime.UtcNow; roost.Prune(now);
      var song=musicBrain.Current;
      string music=musicTasteEnabled && song!=null && (now-sensedMusicUtc).TotalSeconds<20
        ? (song.Playing?"Playing: ":"Paused: ")+RoostMemory.Short(song.Title,150)+" by "+RoostMemory.Short(song.Artist,100)+". Corbie's preference (-1 dislikes to +1 likes): "+musicBrain.CurrentFeeling.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+(musicBrain.CurrentFavourite==null?"":". This is one of Corbie's own favourite tunes.")
        : "No fresh music metadata available.";
      string thermal=temperatureEnabled && (now-sensedTemperatureUtc).TotalSeconds<30
        ? sensedTemperature.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+" degrees C, measured recently" : "No fresh CPU temperature reading available.";
      return new JavaScriptSerializer().Serialize(new {
        mood=mood.Snapshot.Mood,energy=mood.Snapshot.Energy,irritation=mood.Snapshot.Irritation,hunger=mood.Snapshot.Hunger,
        foraging=new { berriesFound=desktopForageBrain.SweetFound,dubiousCrumbsTried=desktopForageBrain.SourTried,sourAversion=desktopForageBrain.SourAversion },
        temperature=thermal,music=music,weather=WeatherContext(),holidays=HolidayContext(),discoveries=DiscoveryContext(),
        screen=SensoryContext.RecentScreen(lastVisionContext,lastVisionLook.ToUniversalTime(),now),
        focus=focusSprint.Status(focusHeld),
        school=schoolCalendar.Configured?schoolCalendar.Overdue(DateTime.Now).Count+" calendar items past due; submission status is unknown":"School calendar is not connected.",
        recentEvents=roost.Data.JournalEnabled?roost.Data.Entries.AsEnumerable().Reverse().Take(6).Select(x=>new { daysAgo=(int)(now-x.Utc).TotalDays,kind=x.Kind,text=x.Text }).ToArray():null
      });
    }
    void StepDayCycle(double dt) {
      if(testing || roost==null || !roost.Data.SkyRoostEnabled) {
        dayPhase=CircadianPhase.Daylight; dayWeather=WeatherKind.Clear; return;
      }
      dayPoseTimer-=dt;
      if(dayPoseTimer>0) return;
      dayPoseTimer=8+random.NextDouble()*6;
      dayWeather=weatherDrive.Known?weatherReading.Kind:WeatherKind.Clear;
      var sky=SkyWeather.Evaluate(DateTime.Now, dayWeather);
      dayPhase=sky.Phase;
      int today=DateTime.Now.DayOfYear;
      if(today!=morningRitualDay) { morningRitualDay=today; morningRitualDone=false; }
      if(dayPhase==CircadianPhase.Morning && hop<=0 && !walking && idle==Idle.Quiet) {
        bool atHome=false;
        if(roost.Data.TerritoryEnabled && territory!=null) {
          TerritoryCell cell;
          atHome=territory.ContainsPoint(Left+Width*0.5, Top+Height*0.5, out cell) && territory.Favorite!=null
            && cell!=null && territory.Favorite.Screen==cell.Screen && territory.Favorite.Corner==cell.Corner;
        }
        if(!morningRitualDone && atHome) {
          morningRitualDone=true;
          hop=0.85; wingStretch.Angle=-28; headTilt.Angle=12; idle=Idle.Stretch; idleDuration=2.8; idleTime=0;
          CreatureFoley.Play(FoleyKind.Whoosh, companion.Quiet);
        } else if(random.Next(100)<35) {
          hop=0.55; wingStretch.Angle=-20; headTilt.Angle=8;
        }
      } else if(dayPhase==CircadianPhase.Daylight && hop<=0 && !walking && idle==Idle.Quiet && random.Next(100)<28) {
        hop=0.4;
      }
    }
    void StepMissionShepherd(double dt) {
      missionShepherdCooldown=Math.Max(0,missionShepherdCooldown-dt);
      if(testing || mission==null || !mission.ShouldShepherd(DateTime.UtcNow)) return;
      if(missionShepherdCooldown>0 || isNightSleep || DayCycle.MuteChatter(dayPhase)) return;
      if(!CanInitiate() || companion.Visible || shimejiInteracting || notificationEdge.HuntActive) return;
      missionShepherdCooldown=420;
      SetPerching(false); StopWalking(); ResetIdle(); hop=1;
      var area=CurrentDesktopBounds();
      Left=Math.Max(area.Left, area.Right-Width-36);
      Top=Math.Max(area.Top, area.Bottom-Height-28);
      CreatureFoley.Play(FoleyKind.Clack,companion.Quiet);
      OfferActionBid(()=> {
        OpenNestPage("Roost");
        companion.SaySilent("Quest scrap is going cold. Next step still waits.");
      });
    }
    void StepMissionGlance() {
      if(testing || mission==null || !mission.Active) return;
      UpdateQuestScrap();
      if(!CanInitiate()) return;
      if(!mission.ShouldGlance(DateTime.UtcNow)) return;
      missionPoseTimer=3.2; hop=0.85; StopWalking(); ResetIdle();
      flashBadgeTimer=Math.Max(flashBadgeTimer,2.5);
      string line=mission.TakeGlance(octopusMode);
      if(random.Next(100)<60) OfferAttention(()=>companion.Say(line));
      else CreatureFoley.Play(FoleyKind.Caw,companion.Quiet);
    }



    TrinketPowerMods CurrentTrinketPowers() {
      string wear=toybox!=null?toybox.WearingId:null;
      return TrinketPowers.From(roost!=null?roost.Data.Trinkets:null, wear);
    }
    void ScheduleReturnToRoost() {
      if(roost==null || !roost.Data.TerritoryEnabled) return;
      if(territory==null || territory.Favorite==null) return;
      returnToRoostTimer=1.6;
    }
    void StepLoreSystems(double dt) {
      if(testing || roost==null) return;
      // Return-to-roost after focus / game (tiny ritual)
      if(returnToRoostTimer>0) {
        returnToRoostTimer-=dt;
        if(returnToRoostTimer<=0) {
          Home();
          hop=Math.Max(hop,0.7);
          idle=Idle.Stretch; idleDuration=2.2; idleTime=0;
        }
      }
      if(chroniclePoseTimer>0) {
        chroniclePoseTimer-=dt;
        if(hop<=0 && !walking && idle==Idle.Quiet) {
          idle=Idle.Curious; idleDuration=2.4; idleTime=0;
          headTilt.Angle=10; wingStretch.Angle=-12;
        }
      }
      loreTickWait-=dt;
      if(loreTickWait>0) return;
      loreTickWait=2.0;
      if(territory!=null && roost.Data.TerritoryEnabled) {
        string claimed=territory.Tick(2.0, Left, Top, Width, Height, true, roost.Data.JournalEnabled, roost, random);
        territorySaveWait-=2.0;
        if(territorySaveWait<=0) { territory.Save(); territorySaveWait=40; }
        if(!string.IsNullOrEmpty(claimed)) {
          // Ship-ish claim dance
          SetPerching(false); StopWalking(); ResetIdle();
          hop=1.0; flashBadgeTimer=Math.Max(flashBadgeTimer,2.8);
          CreatureFoley.Play(FoleyKind.Caw, companion.Quiet);
          if(CanInitiate()) OfferAttention(()=>companion.Say(octopusMode
            ? ("Territory annexed: "+claimed+". Tiny victory ripple.")
            : ("Roost claimed: "+claimed+". Brief ship dance. Very serious.")));
        }
      }
      if(moltBook!=null && roost.Data.MoltSeasonEnabled) {
        if(MoltSeason.MaybeRotate(moltBook.State, random)) {
          moltBook.Save();
          ApplyTrinketMolt();
          roost.Remember("molt","Molt season shifted to "+moltBook.State.Kind,DateTime.UtcNow);
          if(CanInitiate()) OfferAttention(()=>companion.Say(octopusMode
            ? ("Season turn. Plumage drinks "+moltBook.State.Kind+".")
            : ("Molt season turned. Feathers lean "+moltBook.State.Kind+".")));
        }
        string shed=MoltSeason.MaybeFeatherShed(moltBook.State, roost.Data.JournalEnabled, random, octopusMode);
        if(shed!=null) {
          moltBook.Save();
          roost.Remember("molt",shed,DateTime.UtcNow);
          ApplyTrinketMolt();
        }
      }
      // Living chronicle: one auto weekly roost report per process lifetime when due
      if(!weeklyChronicleOffered && roost.Data.ChronicleEnabled && chronicleBook!=null
          && FlightChronicle.DueForWeekly(chronicleBook.State, DateTime.UtcNow)
          && !isNightSleep && CanInitiate()) {
        weeklyChronicleOffered=true;
        string report=SpeakChronicle(true);
        OfferAttention(()=>companion.Say(report));
        chroniclePoseTimer=8;
      }
    }
    string SpeakChronicle(bool markSpoken) {
      if(roost==null || !roost.Data.ChronicleEnabled)
        return "Chronicle is off - enable it in the Nest Lore tab.";
      var powers=CurrentTrinketPowers();
      var lines=FlightChronicle.Build(roost.Data.Entries, mission, territory, moltBook!=null?moltBook.State:null, DateTime.UtcNow, powers.ChronicleFlavor);
      if(chronicleBook!=null) {
        chronicleBook.ReplaceLines(lines);
        string pin=FlightChronicle.PickPin(lines);
        if(!string.IsNullOrEmpty(pin)) chronicleBook.Pin(pin);
        if(markSpoken) chronicleBook.MarkSpoken(DateTime.UtcNow);
      }
      chroniclePoseTimer=Math.Max(chroniclePoseTimer,6);
      RefreshLorePanel();
      return FlightChronicle.Speak(lines, octopusMode);
    }
    string TerritoryStatusSpeech() {
      if(territory==null || roost==null || !roost.Data.TerritoryEnabled)
        return "Territory is off or still unmapped.";
      return territory.StatusLine()+". Open Nest Lore for the map.";
    }
    string MoltSeasonSpeech() {
      if(moltBook==null || roost==null || !roost.Data.MoltSeasonEnabled)
        return "Molt seasons are off.";
      var tint=TrinketMolt.FromHoard(roost.Data.Trinkets);
      tint=MoltSeason.ApplySeason(tint, moltBook.State);
      return MoltSeason.Summary(moltBook.State, tint);
    }
    void RefreshLorePanel() {
      if(territoryStatus!=null && territory!=null)
        territoryStatus.Text=territory.StatusLine();
      if(territoryMapList!=null && territory!=null) {
        territoryMapList.Items.Clear();
        foreach(var line in territory.AsciiMap().Split('\n')) territoryMapList.Items.Add(line);
      }
      if(moltSeasonStatus!=null && moltBook!=null) {
        var tint=roost!=null?TrinketMolt.FromHoard(roost.Data.Trinkets):null;
        if(tint!=null && roost!=null && roost.Data.MoltSeasonEnabled) tint=MoltSeason.ApplySeason(tint, moltBook.State);
        moltSeasonStatus.Text=MoltSeason.Summary(moltBook.State, tint);
      }
      if(chronicleList!=null && roost!=null) {
        var powers=CurrentTrinketPowers();
        var lines=FlightChronicle.Build(roost.Data.Entries, mission, territory, moltBook!=null?moltBook.State:null, DateTime.UtcNow, powers.ChronicleFlavor);
        chronicleList.Items.Clear();
        foreach(var line in lines) chronicleList.Items.Add(line);
        if(chronicleStatus!=null) chronicleStatus.Text=lines.Count+" chronicle lines - weekly roost report available.";
        if(chroniclePinStatus!=null)
          chroniclePinStatus.Text=!string.IsNullOrEmpty(chronicleBook!=null?chronicleBook.State.PinnedHighlight:null)
            ? ("Pinned: "+chronicleBook.State.PinnedHighlight)
            : "Pinned: none yet - speak a roost report to pin a highlight.";
        if(trinketPowerStatus!=null) trinketPowerStatus.Text=powers.Summary;
      }
    }
    void BuildLoreTab() {
      var body=TabBody();
      territoryToggle=Toggle("Claim desktop corners as territory",value=> { roost.Data.TerritoryEnabled=value; roost.Save(); });
      territoryStatus=Status();
      territoryMapList=new ListBox { MinHeight=120, MaxHeight=200, Background=new SolidColorBrush(Color.FromRgb(0xF7,0xEF,0xE0)), Foreground=new SolidColorBrush(Color.FromRgb(0x3A,0x28,0x18)), FontFamily=new System.Windows.Media.FontFamily("Consolas") };
      body.Children.Add(Section("Territory map",territoryToggle,territoryStatus,territoryMapList,Actions(
        ActionButton("Refresh map",()=>RefreshLorePanel()),
        ActionButton("Go to home roost",Home)
      )));
      moltSeasonToggle=Toggle("Living molt seasons (tint shifts over days)",value=> { roost.Data.MoltSeasonEnabled=value; ApplyTrinketMolt(); roost.Save(); });
      moltSeasonStatus=Status();
      body.Children.Add(Section("Molt seasons",moltSeasonToggle,moltSeasonStatus,Actions(
        ActionButton("Molt report",()=>companion.Say(MoltSeasonSpeech()))
      )));
      chronicleToggle=Toggle("Flight chronicle (Nest weekly roost report)",value=> { roost.Data.ChronicleEnabled=value; roost.Save(); });
      chronicleStatus=Status();
      chroniclePinStatus=Status();
      chronicleList=new ListBox { MinHeight=140, MaxHeight=260, Background=new SolidColorBrush(Color.FromRgb(0xF7,0xEF,0xE0)), Foreground=new SolidColorBrush(Color.FromRgb(0x3A,0x28,0x18)) };
      trinketPowerStatus=Status();
      body.Children.Add(Section("Trinket powers",trinketPowerStatus,new TextBlock {
        Text="Hoard rarities and a worn shiny lightly change hop spring, soft-mute tolerance during games, and chronicle ink flavor.",
        Foreground=MutedText, TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,0,0,4)
      }));
      body.Children.Add(Section("Flight chronicle",chronicleToggle,chronicleStatus,chroniclePinStatus,chronicleList,Actions(
        ActionButton("Rebuild chronicle",()=>RefreshLorePanel()),
        ActionButton("Speak roost report",()=>companion.Say(SpeakChronicle(true)))
      )));
      AddTab("Lore",body);
    }
    void StepGameSession(double dt) {
      if(testing || roost==null || gameSession==null) return;
      bool enabled=roost.Data.GameSessionBuddyEnabled;
      var ev=gameSession.Step(dt,enabled,octopusMode,random);
      if(ev==null) return;
      if(ev.Started) {
        SetPerching(true);
        StopWalking(); ResetIdle();
        hop=Math.Max(hop,0.9);
        if(!string.IsNullOrEmpty(ev.JournalText))
          roost.Remember("steam",ev.JournalText,DateTime.UtcNow);
        // Soft mute engages inside GameSessionBuddy; speak the settle line directly.
        if(!string.IsNullOrEmpty(ev.Remark)) {
          if(companion.Quiet) companion.SaySilent(ev.Remark);
          else companion.Say(ev.Remark);
        }
        CreatureFoley.Play(FoleyKind.Clack,companion.Quiet);
      }
      if(ev.Ended) {
        if(!string.IsNullOrEmpty(ev.JournalText) && !ev.Started)
          roost.Remember("steam",ev.JournalText,DateTime.UtcNow);
        else if(ev.Ended && ev.Minutes>0 && ev.Started==false)
          roost.Remember("steam",ev.JournalText,DateTime.UtcNow);
        // When switch packs end+start, journal already merged on start path.
        if(ev.Ended && !ev.Started && !string.IsNullOrEmpty(ev.Remark)) {
          if(companion.Quiet) companion.SaySilent(ev.Remark);
          else companion.Say(ev.Remark);
        }
        if(ev.AwardTrinket) {
          var loot=roost.ScavengePlayTrinket(random,ev.TrinketBiasId);
          if(loot!=null) {
            ApplyTrinketMolt();
            flashBadgeTimer=Math.Max(flashBadgeTimer,3.2);
          }
        }
        if(!ev.Started) ScheduleReturnToRoost();
      }
    }
    void StepLaunchWatcher(double dt) {
      if(testing || roost==null || launchWatcher==null) return;
      if(!roost.Data.LaunchReactEnabled) return;
      if(isNightSleep || DayCycle.MuteChatter(dayPhase)) return;
      var cue=launchWatcher.Step(dt,true,octopusMode,random);
      if(cue==null) return;
      if(!string.IsNullOrEmpty(cue.JournalText) && !string.IsNullOrEmpty(cue.JournalKind))
        roost.Remember(cue.JournalKind,cue.JournalText,DateTime.UtcNow);
      hop=Math.Max(hop,0.7);
      if(gameSession!=null && gameSession.SoftMute) return; // session buddy owns the voice
      if(!CanInitiate()) return;
      if(string.IsNullOrEmpty(cue.Remark)) return;
      OfferAttention(()=>companion.Say(cue.Remark));
    }
    string NoteCurrentGame() {
      var cue=launchWatcher.CaptureNow(octopusMode,random,GetForegroundWindow());
      if(cue==null) return "Sensors shrugged.";
      if(!string.IsNullOrEmpty(cue.JournalText) && !string.IsNullOrEmpty(cue.JournalKind)) {
        if(roost!=null && roost.Data.JournalEnabled) roost.Remember(cue.JournalKind,cue.JournalText,DateTime.UtcNow);
        else if(roost!=null && !roost.Data.JournalEnabled)
          return "Journal is off - flip Remember small events in the Nest, then ask again.";
      }
      hop=Math.Max(hop,0.85);
      return cue.Remark??"Noted.";
    }
    void StepPerchPersonality(double dt) {
      if(testing || roost==null || !roost.Data.PerchPersonalityEnabled) {
        perchHopScale=1; perchMoodKind=PerchKind.Other; return;
      }
      string perch=NestMemory.ProcessNameFromWindow(perchWindow);
      if(string.IsNullOrEmpty(perch) || !autoPerch) {
        perchHopScale=1; perchMoodKind=PerchKind.Other; return;
      }
      var moodInfo=PerchPersonality.MoodFor(perch,octopusMode);
      perchHopScale=moodInfo.HopScale*CurrentTrinketPowers().HopScale;
      perchMoodKind=moodInfo.Kind;
      // Tiny random hops for lively perches (Music); body pose applied in PoseRoost.
      if(moodInfo.Kind==PerchKind.Music && hop<=0 && !walking && idle==Idle.Quiet && random.Next(400)==0) hop=0.55;
      if(moodInfo.Kind==PerchKind.Browser && hop<=0 && !walking && idle==Idle.Quiet && random.Next(700)==0) hop=0.4;
      if(moodInfo.Kind==PerchKind.Other || string.IsNullOrEmpty(moodInfo.IdleRemark)) return;
      if(!CanInitiate() || companion.Visible) return;
      if((DateTime.UtcNow-lastPerchPersonalityUtc).TotalMinutes<14) return;
      lastPerchPersonalityUtc=DateTime.UtcNow;
      if(random.Next(100)<35) OfferAttention(()=>companion.Say(moodInfo.IdleRemark));
    }
    void StepMischief(double dt) {
      if(testing || roost==null) return;
      mischief.Enabled=roost.Data.MischiefEnabled;
      if(!mischief.Enabled || mischiefChaseTimer>0) return;
      bool allowed=!cursorFollowEnabled && !paused && !shuttingDown && !fullscreenOrPresentation && !dragging && !isFlung
        && !looking && !peckingArchive && micRequest==null && !ContextMenu.IsOpen
        && !tray.ContextMenuStrip.Visible && !attentionBid.Active && !notificationEdge.HuntActive
        && !shimejiInteracting && !eyeRestCoach.GlideActive && !focusSprint.Active
        && !breakCoach.OnBreak && hop<=0;
      var pointer=System.Windows.Forms.Cursor.Position;
      var cue=mischief.Step(dt,Left,Top,pointer.X,pointer.Y,allowed);
      if(cue==null) return;
      SetPerching(false); StopWalking(); ResetIdle();
      if(cue.Kind==MischiefKind.Chase) {
        mischiefChaseTimer=2.4; mischiefChaseTx=cue.TargetX; mischiefChaseTy=cue.TargetY;
        hop=1; direction=cue.TargetX<Left?-1:1; facing.ScaleX=direction;
      } else if(cue.Kind==MischiefKind.HideEdge || cue.Kind==MischiefKind.FleeDialog) {
        hop=1;
        Left=Math.Max(8,Math.Min(System.Windows.SystemParameters.WorkArea.Width-Width-8,cue.TargetX));
        Top=Math.Max(8,Math.Min(System.Windows.SystemParameters.WorkArea.Height-Height-8,cue.TargetY));
        wingStretch.Angle=-28;
      } else if(cue.Kind==MischiefKind.DropFeather) {
        hop=0.75; flashBadgeTimer=2.2;
        if(droppedFeather!=null) {
          Canvas.SetLeft(droppedFeather,70+random.Next(40));
          Canvas.SetTop(droppedFeather,95);
          droppedFeather.Opacity=0.95;
          droppedFeather.Visibility=Visibility.Visible;
          droppedFeatherTimer=2.8;
        }
      } else if(cue.Kind==MischiefKind.InspectIcon) {
        hop=0.55; headTilt.Angle=-18; wingStretch.Angle=-12;
        Top=Math.Min(Top+18, System.Windows.SystemParameters.WorkArea.Height-Height-8);
      }
      CreatureFoley.Play(cue.Kind==MischiefKind.FleeDialog?FoleyKind.Whoosh:FoleyKind.Clack,companion.Quiet);
      if(!string.IsNullOrEmpty(cue.Remark) && random.Next(100)<22)
        OfferAttention(()=>companion.Say(cue.Remark));
    }
    void UpdateQuestScrap() {
      if(questScrap==null) return;
      bool on=mission!=null && mission.Active;
      questScrap.Visibility=on?Visibility.Visible:Visibility.Hidden;
      if(!on) { lastScrapCondition=-1; return; }
      int cond=mission.ScrapCondition(DateTime.UtcNow);
      lastScrapCondition=cond;
      try {
        if(cond==0) {
          questScrap.Fill=(SolidColorBrush)new BrushConverter().ConvertFromString("#F5E6C8");
          questScrap.Stroke=(SolidColorBrush)new BrushConverter().ConvertFromString("#8B6914");
          questScrap.Opacity=0.98;
        } else if(cond==1) {
          questScrap.Fill=(SolidColorBrush)new BrushConverter().ConvertFromString("#E8D4A8");
          questScrap.Stroke=(SolidColorBrush)new BrushConverter().ConvertFromString("#7A5A20");
          questScrap.Opacity=0.9;
        } else if(cond==2) {
          questScrap.Fill=(SolidColorBrush)new BrushConverter().ConvertFromString("#C4A574");
          questScrap.Stroke=(SolidColorBrush)new BrushConverter().ConvertFromString("#5C4030");
          questScrap.Opacity=0.75;
        } else {
          questScrap.Fill=(SolidColorBrush)new BrushConverter().ConvertFromString("#8B7355");
          questScrap.Stroke=(SolidColorBrush)new BrushConverter().ConvertFromString("#3E2F1C");
          questScrap.Opacity=0.55;
        }
      } catch { }
      if(!mission.HasNextStep) questScrap.Opacity=Math.Min(questScrap.Opacity,0.65);
    }
    void StartMissionPersonal() {
      try {
        mission.StartPersonal(missionTitle.Text);
        missionFeedback.Text="";
        hop=1; StopWalking(); ResetIdle(); UpdateQuestScrap();
        companion.Say(mission.AskNextStepLine(octopusMode));
        roost.Remember("mission","Started personal mission: "+mission.Title,DateTime.UtcNow);
        RefreshRoostPanel();
      } catch(Exception ex) { missionFeedback.Text=ex.Message; }
    }
    void StartMissionSchool() {
      try {
        var selected=panelSchoolList!=null?panelSchoolList.SelectedItem as SchoolRow:null;
        if(selected==null) { missionFeedback.Text="Select a Blackboard deadline first."; return; }
        mission.StartBlackboard(selected.Event);
        missionFeedback.Text="";
        hop=1; StopWalking(); ResetIdle(); UpdateQuestScrap();
        companion.Say(mission.AskNextStepLine(octopusMode));
        roost.Remember("mission","Started Blackboard mission: "+mission.Title,DateTime.UtcNow);
        RefreshRoostPanel();
      } catch(Exception ex) { missionFeedback.Text=ex.Message; }
    }
    void SaveMissionStep() {
      try {
        mission.SetNextStep(missionStep.Text);
        missionFeedback.Text="Next step locked.";
        hop=0.7; UpdateQuestScrap();
        companion.SaySilent("Quest scrap updated: "+mission.NextStep);
        if(!string.IsNullOrWhiteSpace(missionStep.Text) && focusTask!=null && string.IsNullOrWhiteSpace(focusTask.Text))
          focusTask.Text=missionStep.Text;
        RefreshRoostPanel();
      } catch(Exception ex) { missionFeedback.Text=ex.Message; }
    }
    void AddMissionNote() {
      if(mission==null || !mission.Active) { missionFeedback.Text="No active mission."; return; }
      mission.AddNote(missionNote.Text);
      missionNote.Text="";
      missionFeedback.Text="Progress note saved locally.";
      RefreshRoostPanel();
    }
    void CompleteMissionStep() {
      if(mission==null || !mission.CompleteStep()) { missionFeedback.Text="Set a next step before finishing one."; return; }
      mood.FileSuccess();
      hop=1; flashBadgeTimer=3; StopWalking(); ResetIdle();
      var earned=roost.ScavengeTrinket(new Random());
      if(earned!=null) { ApplyTrinketMolt(); ShowTrinketInspect(earned); }
      string msg="Step complete. Mood improved.";
      if(earned!=null) msg+=" Shiny acquired: "+earned.Name+".";
      companion.Say(msg);
      missionFeedback.Text=msg;
      UpdateQuestScrap(); RefreshRoostPanel();
    }
    void PutTrinketAway() {
      if(toybox!=null)toybox.ClearWearing();
      wearingAccessory=false;trinketInspectTimer=0;inspectedTrinket=null;
      if(trinketVisual!=null) {
        trinketVisual.Visibility=Visibility.Hidden;
        Canvas.SetLeft(trinketVisual,148);Canvas.SetTop(trinketVisual,68);
      }
      RefreshRoostPanel();
      companion.SaySilent("Fine. Back in the hoard. Still mine.");
    }
    void GiveSelectedTrinket() {
      var sel=trinketList!=null?trinketList.SelectedItem as TrinketItem:null;
      if(sel==null) { companion.Say("Ooh, pick a shiny from the hoard first! Go on, I'm practically vibrating!"); return; }
      var reaction=toybox.Give(sel);
      StopWalking(); ResetIdle(); hop=1;
      ShowTrinketInspect(sel);
      if(reaction==ToyReaction.Wear || reaction==ToyReaction.Attached) {
        wearingAccessory=true;
        if(trinketVisual!=null) {
          try { trinketVisual.Fill=(SolidColorBrush)new BrushConverter().ConvertFromString(sel.ColorHex??"#FFD700"); } catch { }
          trinketVisual.Visibility=Visibility.Visible;
          Canvas.SetLeft(trinketVisual,148); Canvas.SetTop(trinketVisual,68);
        }
        trinketInspectTimer=0;
      } else if(reaction==ToyReaction.Toss) {
        wearingAccessory=false;
        if(trinketVisual!=null) {
          Canvas.SetLeft(trinketVisual,160); Canvas.SetTop(trinketVisual,40);
          trinketInspectTimer=1.6;
        }
        StartFling(direction*180, -220);
      } else if(reaction==ToyReaction.Hide) {
        wearingAccessory=false;
        if(trinketVisual!=null) {
          Canvas.SetLeft(trinketVisual,95); Canvas.SetTop(trinketVisual,118);
          trinketInspectTimer=1.2;
        }
        headTilt.Angle=14; wingStretch.Angle=-20;
      } else if(reaction==ToyReaction.Dislike) {
        wearingAccessory=false;
        scritches.Celebrate(1.6);
        if(trinketVisual!=null) trinketVisual.Visibility=Visibility.Hidden;
      } else {
        wearingAccessory=string.Equals(toybox.WearingId,sel.Id,StringComparison.OrdinalIgnoreCase);
      }
      companion.Say(toybox.Remark(sel,reaction,octopusMode));
      CreatureFoley.Play(FoleyKind.Clack,companion.Quiet);
      roost.Remember("toybox","Gave "+sel.Name+" - "+reaction,DateTime.UtcNow);
      RefreshRoostPanel();
    }
    async Task ScanDownloads(bool explicitRequest) {
      if(forageRequest!=null || shuttingDown) return;
      var request=new CancellationTokenSource(); forageRequest=request;
      SetForageStatus("Foraging through Downloads...");
      try {
        var decisions=roost.Data.Decisions.ToArray();
        var result=await Task.Run(()=>DownloadsForager.Scan(DownloadsForager.Folder(),DateTime.UtcNow,decisions,request.Token));
        request.Token.ThrowIfCancellationRequested(); if(shuttingDown) return;
        var hidden=new System.Collections.Generic.HashSet<string>(roost.Data.Decisions.Where(x=>x.UntilUtc>DateTime.UtcNow).Select(x=>x.Key));
        result.Items.RemoveAll(x=>hidden.Contains(x.Key));
        forageSuggestions.Clear(); forageSuggestions.AddRange(result.Items); RefreshForageList();
        SetForageStatus(result.Items.Count+" suggestions. "+(result.Limited?"Checked the first 1,000 files. ":"")+"Names, sizes and dates only; possible copies are not verified duplicates.");
        if(!explicitRequest && result.Items.Count>0) OfferAttention(ShowDownloadsSuggestions);
      } catch(OperationCanceledException) { SetForageStatus("Foraging cancelled."); }
      catch(Exception ex) { SetForageStatus("Could not check Downloads: "+ex.Message); }
      finally { if(forageRequest==request) forageRequest=null; request.Dispose(); }
    }
    void ShowDownloadsSuggestions() {
      if(!OpenNestPage("Files")) return;
      corbiePanel.UpdateLayout();
      if(forageList!=null) forageList.BringIntoView();
      companion.SaySilent("Found some ageing shinies in Downloads. You decide which ones stay.");
    }
    void SetForageStatus(string text) { lastForageStatus=text; if(forageStatus!=null) forageStatus.Text=text; }
    void RefreshForageList() {
      if(forageList==null) return;
      forageList.Items.Clear(); foreach(var item in forageSuggestions) forageList.Items.Add(item);
    }
    void ChooseForage(bool keep) {
      var items=forageList.SelectedItems.Cast<ForageItem>().ToArray();
      if(items.Length==0) { SetForageStatus("Select one or more suggestions first."); return; }
      foreach(var item in items) { roost.Decide(item,keep); forageSuggestions.Remove(item); }
      RefreshForageList();
      SetForageStatus(keep?"Kept "+items.Length+" file(s). I won't suggest these versions again.":"Snoozed "+items.Length+" file(s) for one day.");
    }
    void RecycleForage() {
      RecycleForageItems(forageList.SelectedItems.Cast<ForageItem>().ToArray(),ConfirmForageRecycle,RecycleBinHoard.SendToHoard);
    }
    bool ConfirmForageRecycle(string[] paths) {
      var dialog=new HoardConfirmDialog(corbiePanel??(Window)this,paths); dialog.ShowDialog();
      if(dialog.Confirmed && dialog.DontWarnAgain) SetWarnBeforeHoarding(false);
      return dialog.Confirmed;
    }
    void RecycleForageItems(ForageItem[] items,Func<string[],bool> confirm,Func<string,bool> recycle) {
      if(items.Length==0) { SetForageStatus("Select one or more suggestions first."); return; }
      if(RecycleBinHoard.WarnBeforeHoarding && !confirm(items.Select(x=>x.Path).ToArray())) {
        SetForageStatus("Cancelled. Your files stayed where they were."); return;
      }
      int recycled=0,changed=0,failed=0;
      foreach(var item in items) {
        // Recheck after the dialog: files can change while the user decides.
        if(!item.Unchanged()) { changed++; continue; }
        if(!recycle(item.Path)) { failed++; continue; }
        forageSuggestions.Remove(item); recycled++;
      }
      RefreshForageList();
      if(recycled>0) {
        mood.FileSuccess();
        roost.Remember("forage","Tidied "+recycled+" Downloads item(s) into the Recycle Bin.",DateTime.UtcNow);
      }
      SetForageStatus("Recycled "+recycled+" of "+items.Length+" selected file(s)."+
        (recycled>0?" You can restore them in the Windows Recycle Bin.":"")+
        (changed>0?" Skipped "+changed+" changed or missing file(s); scan again.":"")+
        (failed>0?" Windows could not recycle "+failed+" file(s).":""));
    }
    void BuildForageSection(StackPanel body) {
      forageToggle=Toggle("Let Corbie check Downloads every 30 minutes",value=> { roost.Data.ForageEnabled=value; roost.Save(); forageWait=1; if(!value && forageRequest!=null) forageRequest.Cancel(); });
      forageList=new ListBox { SelectionMode=SelectionMode.Extended,MinHeight=130,MaxHeight=220,Background=new SolidColorBrush(Color.FromRgb(0xF7,0xEF,0xE0)),Foreground=new SolidColorBrush(Color.FromRgb(0x3A,0x28,0x18)) };
      forageStatus=Status(); forageStatus.Text=lastForageStatus; RefreshForageList();
      body.Children.Add(Section("Downloads foraging",forageToggle,forageStatus,
        new TextBlock { Text="Hold Ctrl to pick several files, or Shift to select a range. Actions apply to every selected file.",Foreground=MutedText,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8) },forageList,Actions(
        ActionButton("Scan Downloads",()=> { var ignored=ScanDownloads(true); }),
        ActionButton("Keep",()=>ChooseForage(true)),ActionButton("Snooze one day",()=>ChooseForage(false)),
        ActionButton("Send to Recycle Bin",RecycleForage),
        ActionButton("Reset Keep / Snooze",()=> { roost.Data.Decisions.Clear(); roost.Save(); forageStatus.Text="Decisions cleared. Scan again to reconsider files."; })
      )));
    }
    void CheckDownloadsSelection() {
      OpenNestPage("Files");
      if(forageList.SelectionMode!=SelectionMode.Extended) throw new Exception("Downloads must allow Ctrl/Shift multi-selection.");
      string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"downloads-test-"+Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(folder);
      var items=new ForageItem[3];
      try {
        for(int i=0;i<items.Length;i++) {
          string path=Path.Combine(folder,"sample-"+i+".txt"); File.WriteAllText(path,"sample");
          var file=new FileInfo(path);
          items[i]=new ForageItem { Path=path,Name=file.Name,Key=path,Bytes=file.Length,ModifiedUtc=file.LastWriteTimeUtc };
        }
        Action load=()=> { forageSuggestions.Clear(); forageSuggestions.AddRange(items); RefreshForageList(); forageList.SelectedItems.Add(items[0]); forageList.SelectedItems.Add(items[1]); };
        load(); ChooseForage(true);
        if(forageSuggestions.Count!=1 || forageSuggestions[0]!=items[2] || roost.Data.Decisions.Count!=2 || roost.Data.Decisions.Any(x=>x.UntilUtc!=DateTime.MaxValue)) throw new Exception("Keep did not apply only to the selected files.");
        roost.Data.Decisions.Clear(); load(); ChooseForage(false);
        if(forageSuggestions.Count!=1 || roost.Data.Decisions.Count!=2 || roost.Data.Decisions.Any(x=>(x.UntilUtc-DateTime.UtcNow).TotalHours<23)) throw new Exception("Batch snooze failed.");
        load(); SetWarnBeforeHoarding(true); int confirmations=0,recycles=0;
        Func<string,bool> recycle=path=> { recycles++; return true; };
        RecycleForageItems(forageList.SelectedItems.Cast<ForageItem>().ToArray(),paths=> { confirmations++; return false; },recycle);
        if(confirmations!=1 || recycles!=0 || forageSuggestions.Count!=3) throw new Exception("Cancelled batch changed files or suggestions.");
        Exception dialogError=null;
        Dispatcher.BeginInvoke(new Action(()=> {
          HoardConfirmDialog dialog=null;
          try {
            dialog=Application.Current.Windows.OfType<HoardConfirmDialog>().Single();
            var content=(StackPanel)((Border)dialog.Content).Child;
            var preview=(TextBlock)content.Children.OfType<ScrollViewer>().Single().Content;
            if(!preview.Text.Contains(items[0].Name) || !preview.Text.Contains(items[1].Name) || preview.Text.Contains(items[2].Name)) throw new Exception("Batch confirmation did not show exactly the selected files.");
            content.Children.OfType<CheckBox>().Single().IsChecked=true;
            content.Children.OfType<StackPanel>().Single().Children.OfType<Button>().Single(x=>x.IsDefault).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
          } catch(Exception ex) { dialogError=ex; if(dialog!=null) dialog.Close(); }
        }));
        RecycleForageItems(forageList.SelectedItems.Cast<ForageItem>().ToArray(),paths=> { confirmations++; return ConfirmForageRecycle(paths); },recycle);
        if(dialogError!=null) throw dialogError;
        if(confirmations!=2 || recycles!=2 || forageSuggestions.Count!=1 || RecycleBinHoard.WarnBeforeHoarding || panelWarnHoard.IsChecked==true || warnHoardItem.IsChecked || trayWarnHoard.Checked) throw new Exception("Confirmed batch or session warning opt-out failed.");
        load(); File.AppendAllText(items[1].Path,"changed");
        RecycleForageItems(forageList.SelectedItems.Cast<ForageItem>().ToArray(),paths=> { throw new Exception("Opted-out warning was shown again."); },recycle);
        if(recycles!=3 || forageSuggestions.Count!=2 || !forageSuggestions.Contains(items[1]) || !lastForageStatus.Contains("Skipped 1")) throw new Exception("Changed file was recycled or not reported.");
        load(); RecycleForageItems(forageList.SelectedItems.Cast<ForageItem>().ToArray(),paths=>true,path=>false);
        if(forageSuggestions.Count!=3 || !lastForageStatus.Contains("Windows could not recycle 1")) throw new Exception("Failed files were removed from suggestions.");
        SetWarnBeforeHoarding(true); load();
        RecycleForageItems(forageList.SelectedItems.Cast<ForageItem>().ToArray(),paths=> { confirmations++; return false; },recycle);
        if(confirmations!=3 || recycles!=3 || panelWarnHoard.IsChecked!=true) throw new Exception("Re-enabling session warnings failed.");
        RecycleForageItems(new ForageItem[0],paths=> { throw new Exception("Empty selection prompted."); },recycle);
        if(!lastForageStatus.Contains("Select one or more")) throw new Exception("Empty selection has no feedback.");
        if(items.Any(x=>!File.Exists(x.Path))) throw new Exception("Selection checks must not recycle real test files.");
      } finally {
        foreach(var item in items) if(item!=null && File.Exists(item.Path)) File.Delete(item.Path);
        Directory.Delete(folder);
      }
      CloseCorbiePanel();
    }
    void StartFocus(bool school) {
      if(focusSprint.Active || focusSprint.CompletionPending) { focusFeedback.Text="Finish or cancel the current sprint before starting another."; return; }
      string title=focusTask.Text;
      if(school) {
        var selected=panelSchoolList.SelectedItem as SchoolRow;
        if(selected==null) { focusFeedback.Text="Select a deadline above first."; return; }
        title=selected.Event.Title;
      }
      try {
        focusSprint.Start(title,new [] { 10,25,45 }[Math.Max(0,focusMinutes.SelectedIndex)]);
        focusFeedback.Text="";
        attentionBid.Cancel(); SaveRoost();
        roost.Remember("focus","Started a focus sprint: "+focusSprint.Title,DateTime.UtcNow);
        companion.SaySilent("One bite: "+focusSprint.Title+". You work; I'll keep my beak shut.");
      } catch(ArgumentException ex) { focusFeedback.Text=ex.Message; }
    }
    void BuildFocusSection(StackPanel body) {
      focusTask=new TextBox { MaxLength=180,Margin=new Thickness(0,4,0,8),ToolTip="One small next step, such as outline the first paragraph" };
      focusMinutes=new ComboBox { Margin=new Thickness(0,0,0,8) };
      foreach(int minutes in new [] { 10,25,45 }) focusMinutes.Items.Add(minutes+" minutes"); focusMinutes.SelectedIndex=1;
      focusStatus=Status(); focusFeedback=Status();
      body.Children.Add(Section("Deadline focus sprint",new TextBlock { Text="One small next step (or select a deadline above)",Foreground=MutedText },focusTask,focusMinutes,focusStatus,focusFeedback,Actions(
        ActionButton("Focus selected deadline",()=>StartFocus(true)),ActionButton("Focus my small task",()=>StartFocus(false)),
        ActionButton("Pause / resume sprint",()=> { if(focusSprint.Active) { focusSprint.Paused=!focusSprint.Paused; SaveRoost(); } }),
        ActionButton("Cancel sprint",()=> { focusSprint.Cancel(); focusFeedback.Text="Sprint cancelled."; SaveRoost(); })
      ),new TextBlock { Text="Timer rests during breaks, pet pause, and after two minutes away. Restarted sessions resume paused. A finished sprint offers a break; it never marks an assignment submitted.",Foreground=MutedText,TextWrapping=TextWrapping.Wrap }));
    }
    void BuildMissionSection(StackPanel body) {
      missionTitle=new TextBox { MaxLength=160,Margin=new Thickness(0,4,0,6),ToolTip="Personal mission title" };
      missionStep=new TextBox { MaxLength=180,Margin=new Thickness(0,4,0,6),ToolTip="One small concrete next step" };
      missionNote=new TextBox { MaxLength=220,Margin=new Thickness(0,4,0,6),ToolTip="Brief local progress note" };
      missionStatus=Status(); missionFeedback=Status();
      body.Children.Add(Section("Current Mission",missionStatus,
        new TextBlock { Text="Personal mission title",Foreground=MutedText },missionTitle,
        new TextBlock { Text="Next concrete step",Foreground=MutedText },missionStep,
        new TextBlock { Text="Progress note",Foreground=MutedText },missionNote,missionFeedback,Actions(
          ActionButton("Start personal mission",StartMissionPersonal),
          ActionButton("Start from selected Blackboard item",StartMissionSchool),
          ActionButton("Set next step",SaveMissionStep),
          ActionButton("Save progress note",AddMissionNote),
          ActionButton("Finish current step",CompleteMissionStep),
          ActionButton("Abandon mission",()=> { if(mission!=null) { mission.Abandon(); UpdateQuestScrap(); companion.SaySilent("Mission abandoned. Quest scrap dropped."); RefreshRoostPanel(); } })
        ),new TextBlock { Text="Corbie carries a quest scrap while a mission is active. Focus sprints and breaks count as progress. Avoiding the step can earn sparse judgmental glances (max 2/day).",Foreground=MutedText,TextWrapping=TextWrapping.Wrap }));
    }
    void BuildRoostTab() {
      journalViewKey=null;
      trinketViewKey=null;
      var body=TabBody();
      BuildMissionSection(body);
      contextToggle=Toggle("Use Corbie's senses in microphone chat",value=> { roost.Data.ContextEnabled=value; CancelMicTalk(); chat.Clear(); roost.Save(); });
      journalToggle=Toggle("Remember small events for seven days",value=> { roost.Data.JournalEnabled=value; CancelMicTalk(); chat.Clear(); roost.Save(); });
      cursorToggle=Toggle("Play with a nearby, resting cursor",value=> { roost.Data.CursorPlay=value; roost.Save(); });
      bidToggle=Toggle("Ask for attention with a gesture before casual chatter",value=> { roost.Data.AttentionBids=value; attentionBid.Cancel(); roost.Save(); });
      clipboardToggle=Toggle("The Clipboard Crow (clean tracking params & preview hex colors)",value=> { roost.Data.ClipboardEnabled=value; roost.Save(); });
      foleyToggle=Toggle("Creature sound effects (caws, beak clicks, flutters, suction pops)",value=> { roost.Data.FoleyEnabled=value; CreatureFoley.Enabled=value; roost.Save(); });
      skyToggle=Toggle("Circadian roosting (local night sleep posture)",value=> { roost.Data.SkyRoostEnabled=value; roost.Save(); });
      pesterToggle=Toggle("Screen Pester Boss (confiscate cursor after 3 snoozed detox breaks)",value=> { roost.Data.PesterBossEnabled=value; roost.Save(); });
      shimejiToggle=Toggle("Meet and react to Shimeji desktop creatures",value=> { roost.Data.ShimejiEnabled=value; EndShimejiInteraction(); ResetShimejiNerves(); shimejiWatcher.Reset(); pendingShimejiRemark=null; roost.Save(); });
      wakeToggle=Toggle("Wake phrase for single mic requests: say Corbie first",value=> { roost.Data.WakePhraseEnabled=value; roost.Save(); });
      mischiefToggle=Toggle("Raven mischief mode (harmless desktop tricks)",value=> { roost.Data.MischiefEnabled=value; mischief.Enabled=value; roost.Save(); });
      perchPersonalityToggle=Toggle("Perch personalities by app family",value=> { roost.Data.PerchPersonalityEnabled=value; roost.Save(); });
      launchReactToggle=Toggle("Notice new apps opening (Nest-note Steam games)",value=> { roost.Data.LaunchReactEnabled=value; if(!value) launchWatcher.Reset(); roost.Save(); });
      gameSessionToggle=Toggle("Game session buddy (settle + soft mute + session length note)",value=> { roost.Data.GameSessionBuddyEnabled=value; if(!value) gameSession.Reset(); roost.Save(); });
      body.Children.Add(Section("Shared senses and creature play",contextToggle,cursorToggle,bidToggle,clipboardToggle,foleyToggle,skyToggle,pesterToggle,shimejiToggle,shimejiNerveStatus=Status(),wakeToggle,mischiefToggle,perchPersonalityToggle,launchReactToggle,gameSessionToggle,Actions(
        ActionButton("Test Caw",()=> CreatureFoley.Play(FoleyKind.Caw,false)),
        ActionButton("Test Clack",()=> CreatureFoley.Play(FoleyKind.Clack,false)),
        ActionButton("Test Whoosh",()=> CreatureFoley.Play(FoleyKind.Whoosh,false)),
        ActionButton("Test Pop",()=> CreatureFoley.Play(FoleyKind.SuctionPop,false))
      ),new TextBlock {
        Text="A head tilt and raised wing mean he has a thought. Click to hear it; ignore him and he settles. The Clipboard Crow offers to clean tracking garbage when you copy links. With the fly nervous system on, nearby Shimeji movement drives his motion circuit: unfamiliar neighbors get watched, calm encounters build familiarity, and fast close passes can startle him. Familiarity and attention allow playful approaches. Detection reads only process identity and window geometry; this does not recognize sprite artwork. Habitat input takes priority.",Foreground=MutedText,TextWrapping=TextWrapping.Wrap }));
      trinketList=new ListBox { MinHeight=110,MaxHeight=180,Background=new SolidColorBrush(Color.FromRgb(0xF7,0xEF,0xE0)),Foreground=new SolidColorBrush(Color.FromRgb(0x3A,0x28,0x18)) };
      trinketStatus=Status();
      body.Children.Add(Section("Corbie's Shiny Trinket Hoard",trinketStatus,trinketList,Actions(
        ActionButton("Inspect selected",()=> {
          var sel=trinketList.SelectedItem as TrinketItem;
          if(sel!=null) {
            companion.Say(TrinketCatalog.InspectRemark(sel,octopusMode));
            ShowTrinketInspect(sel);
          } else {
            companion.Say("Pick a shiny first! Oh, I can't WAIT to boast about it. Go on, pick one!");
          }
        }),
        ActionButton("Scavenge a shiny",()=> {
          var item=roost.ScavengeTrinket(new Random());
          if(item!=null) {
            companion.Say("OH! OH! Found something SHINY! "+item.Name+" ("+item.Rarity+")! "+item.Description+" I love it. I LOVE it!");
            RefreshRoostPanel();
            CreatureFoley.Play(FoleyKind.Clack,companion.Quiet);
          }
        }),
        ActionButton("Give selected to Corbie",GiveSelectedTrinket),
        ActionButton("Put trinket away",PutTrinketAway)
      ),new TextBlock { Text="Corvids hoard shiny objects. Earn treasures by completing focus sprints, full breaks, cracking archives, treating Corbie, or finishing mission steps. Give one to Corbie and he may wear, toss, hide, dislike, or become bizarrely attached.",Foreground=MutedText,TextWrapping=TextWrapping.Wrap }));
      journalList=new ListBox { MinHeight=140,MaxHeight=240,Background=new SolidColorBrush(Color.FromRgb(0xF7,0xEF,0xE0)),Foreground=new SolidColorBrush(Color.FromRgb(0x3A,0x28,0x18)) };
      roostStatus=Status();
      body.Children.Add(Section("Raven's Roost Journal",journalToggle,roostStatus,journalList,Actions(
        ActionButton("Forget journal",()=> { CancelMicTalk(); roost.Clear(); if(nest!=null) nest.Clear(); chat.Clear(); journalViewKey=null; RefreshRoostPanel(); })
      ),new TextBlock { Text="Stored only on this PC: track changes, focus sessions, treats and tidying, plus Nest memory habits (favorite perch, focus streak, treat timing, hot-app grudges). No transcripts or screen contents. Turn memory off to stop learning; Forget clears journal and Nest memory.",Foreground=MutedText,TextWrapping=TextWrapping.Wrap }));
      AddTab("Roost",body);
    }
    void RefreshRoostPanel() {
      if(roostStatus==null) return;
      contextToggle.IsChecked=roost.Data.ContextEnabled; journalToggle.IsChecked=roost.Data.JournalEnabled;
      cursorToggle.IsChecked=roost.Data.CursorPlay; bidToggle.IsChecked=roost.Data.AttentionBids;
      if(forageToggle!=null) forageToggle.IsChecked=roost.Data.ForageEnabled;
      if(clipboardToggle!=null) clipboardToggle.IsChecked=roost.Data.ClipboardEnabled;
      if(foleyToggle!=null) foleyToggle.IsChecked=roost.Data.FoleyEnabled;
      if(skyToggle!=null) skyToggle.IsChecked=roost.Data.SkyRoostEnabled;
      if(pesterToggle!=null) pesterToggle.IsChecked=roost.Data.PesterBossEnabled;
      if(shimejiToggle!=null) shimejiToggle.IsChecked=roost.Data.ShimejiEnabled;
      if(wakeToggle!=null) wakeToggle.IsChecked=roost.Data.WakePhraseEnabled;
      if(mischiefToggle!=null) mischiefToggle.IsChecked=roost.Data.MischiefEnabled;
      if(perchPersonalityToggle!=null) perchPersonalityToggle.IsChecked=roost.Data.PerchPersonalityEnabled;
      if(launchReactToggle!=null) launchReactToggle.IsChecked=roost.Data.LaunchReactEnabled;
      if(gameSessionToggle!=null) gameSessionToggle.IsChecked=roost.Data.GameSessionBuddyEnabled;
      if(territoryToggle!=null) territoryToggle.IsChecked=roost.Data.TerritoryEnabled;
      if(moltSeasonToggle!=null) moltSeasonToggle.IsChecked=roost.Data.MoltSeasonEnabled;
      if(chronicleToggle!=null) chronicleToggle.IsChecked=roost.Data.ChronicleEnabled;
      RefreshLorePanel();
      if(focusStatus!=null) focusStatus.Text=focusSprint.Status(focusHeld); roost.Prune(DateTime.UtcNow);
      if(missionStatus!=null) missionStatus.Text=mission!=null?mission.StatusLine():"Missions unavailable";
      UpdateQuestScrap();
      roostStatus.Text=roost.Error??(roost.Data.Entries.Count+" events - "+(nest!=null?nest.StatusLine():"nest memory idle")+" - journal auto-forgets after seven days");
      if(trinketStatus!=null && roost.Data.Trinkets!=null) {
        trinketStatus.Text=roost.Data.Trinkets.Count+" shiny trinkets in hoard";
        string tkey=roost.Data.Trinkets.Count+"|"+(roost.Data.Trinkets.Count>0?roost.Data.Trinkets[0].Id:"none");
        if(tkey!=trinketViewKey) {
          trinketViewKey=tkey; trinketList.Items.Clear();
          foreach(var item in roost.Data.Trinkets) trinketList.Items.Add(item);
        }
      }
      string key=roost.Data.Entries.Count+"|"+(roost.Data.Entries.Count>0?roost.Data.Entries.Last().Utc.Ticks:0);
      if(key!=journalViewKey) { journalViewKey=key; journalList.Items.Clear(); foreach(var entry in roost.Data.Entries.AsEnumerable().Reverse()) journalList.Items.Add(entry); }
    }
    void CheckRoostIntegration() {
      OpenCorbiePanel();
      if(shimejiToggle==null || shimejiToggle.IsChecked!=roost.Data.ShimejiEnabled) throw new Exception("Shimeji recognition preference was not shown in the Roost.");
      focusTask.Text="Outline one paragraph"; focusMinutes.SelectedIndex=0; StartFocus(false);
      if(!focusSprint.Active || focusSprint.Remaining!=600) throw new Exception("Manual focus sprint did not start from the panel.");
      focusSprint.Cancel();
      panelSchoolList.Items.Clear(); var row=new SchoolRow { Event=new SchoolEvent { Id="fixture",Title="Essay draft",Due=DateTime.Now.AddDays(-1) },Label="Essay draft" };
      panelSchoolList.Items.Add(row); panelSchoolList.SelectedItem=row; StartFocus(true);
      if(focusSprint.Title!="Essay draft" || schoolCalendar.IsDone(row.Event)) throw new Exception("Deadline focus selection changed submission state.");
      focusSprint.Cancel(); companion.Dismiss();
      roost.Data.AttentionBids=false; int guardedAction=0;
      OfferActionBid(()=>guardedAction++);
      if(guardedAction!=0 || !attentionBid.Active) throw new Exception("Action bid bypassed click confirmation.");
      if(!attentionBid.Accept() || guardedAction!=1) throw new Exception("Accepted action bid did not run exactly once.");
      string before=lastVisionContext; DateTime beforeTime=lastVisionLook;
      lastVisionContext="An editor with an essay"; lastVisionLook=DateTime.Now;
      if(!BuildSensoryContext().Contains("An editor with an essay")) throw new Exception("Chat context lost a fresh screen observation.");
      lastVisionLook=DateTime.Now.AddMinutes(-3);
      if(BuildSensoryContext().Contains("An editor with an essay")) throw new Exception("Chat context leaked stale screen data.");
      roost.Data.ContextEnabled=false;
      if(BuildSensoryContext()!=null) throw new Exception("Disabled chat context was included.");
      roost.Data.ContextEnabled=true; lastVisionContext=before; lastVisionLook=beforeTime;
      roost.Remember("test","A fixture event",DateTime.UtcNow);
      roost.ScavengeTrinket(new Random(42));
      RefreshCorbiePanel();
      int count=journalList.Items.Count;
      forageSuggestions.Add(new ForageItem { Name="fixture.exe",Path="fixture.exe",Reason="preview",Bytes=1024 });
      corbiePanel.Close(); OpenCorbiePanel();
      if(journalList.Items.Count!=count || forageList.Items.Count!=1 || trinketList.Items.Count!=1) throw new Exception("Nest reopening lost journal, forage, or trinket results.");
      forageSuggestions.Clear(); roost.Clear(); corbiePanel.Close();
    }
    void RenderRoostPreview(string directory) {
      Directory.CreateDirectory(directory);
      roost.Remember("focus","Finished a focus sprint: outline the essay introduction",DateTime.UtcNow.AddMinutes(-20));
      roost.Remember("music","Listened to a favourite track",DateTime.UtcNow.AddMinutes(-12));
      roost.Remember("treat","Received a treat. Bribery remains effective.",DateTime.UtcNow);
      forageSuggestions.Add(new ForageItem { Name="old-installer.exe",Reason="installer unchanged for 28 days",Bytes=44040192 });
      forageSuggestions.Add(new ForageItem { Name="notes (1).pdf",Reason="possible duplicate (name and size only)",Bytes=182000 });
      OpenCorbiePanel(); corbiePanel.Width=650; corbiePanel.Height=1000;
      foreach(int tab in new [] { 7,6,3 }) {
        panelTabs.SelectedIndex=tab; corbiePanel.UpdateLayout();
        var frame=new System.Windows.Media.Imaging.RenderTargetBitmap((int)corbiePanel.ActualWidth,(int)corbiePanel.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);
        frame.Render(corbiePanel);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(frame));
        using(var stream=File.Create(Path.Combine(directory,"nest-"+tab+".png"))) encoder.Save(stream);
      }
      corbiePanel.Close();
    }
  }
}
