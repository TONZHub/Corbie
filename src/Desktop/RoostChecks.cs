using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace CorbiePet {
  public static class RoostChecks {
    static void Require(bool pass,string message) { if(!pass) throw new Exception("Roost: "+message); }
    public static void Check() {
      CreatureFoley.SelfCheck();
      ClipboardCrow.SelfCheck();
      TrinketCatalog.SelfCheck();
      SkyWeather.SelfCheck();
      EyeRestCoach.SelfCheck();
      NestMemory.Check();
      NotificationEdge.Check();
      ShipRitual.Check();
      TrinketMolt.Check();
      MissionBoard.Check();
      PerchPersonality.Check();
      TrinketToybox.Check();
      WakeCommand.Check();
      RavenMischief.Check();
      LaunchWatcher.Check();
      GameSessionBuddy.Check();
      TerritoryMap.Check();
      MoltSeason.Check();
      FlightChronicle.Check();
      TrinketPowers.Check();

      DateTime now=DateTime.UtcNow;
      var memory=new RoostMemory(null);
      memory.Remember("test","old",now.AddDays(-8)); memory.Remember("test","fresh",now);
      memory.Remember("test","fresh",now);
      Require(memory.Data.Entries.Count==1,"seven-day retention or deduplication failed");
      var scavenged=memory.ScavengeTrinket(new Random(42));
      Require(scavenged!=null && memory.Data.Trinkets.Count==1,"trinket scavenge failed");
      Require(memory.Data.Entries.Count==2,"trinket event not recorded in journal");
      memory.Data.JournalEnabled=false; memory.Remember("test","disabled",now);
      Require(memory.Data.Entries.Count==2,"disabled journal recorded an event");
      memory.Clear(); Require(memory.Data.Entries.Count==0,"forget did not clear entries");
      memory.Data.JournalEnabled=true;
      for(int i=0;i<310;i++) memory.Data.Entries.Add(new RoostEntry { Utc=now,Text="event" });
      memory.Prune(now); Require(memory.Data.Entries.Count==300,"journal cap failed");
      Require(SensoryContext.RecentScreen("a secret",now.AddMinutes(-3),now).IndexOf("secret")<0,"stale screen included");
      Require(SensoryContext.RecentScreen("editor",now.AddSeconds(-20),now).Contains("editor"),"fresh screen missing");
      Require(!SensoryContext.RecentScreen("future",now.AddMinutes(2),now).Contains("future"),"future screen included");
      var sprint=new FocusSprint(); sprint.Start("Outline an overdue essay",5);
      sprint.Step(1,false); Require(sprint.Remaining==300,"held sprint advanced");
      sprint.Paused=true; sprint.Step(1,true); Require(sprint.Remaining==300,"paused sprint advanced");
      sprint.Paused=false; for(int i=0;i<300;i++) sprint.Step(1,true);
      Require(!sprint.Active && sprint.CompletionPending,"sprint never completed");
      sprint.Acknowledge(); Require(!sprint.CompletionPending,"completion repeats");
      sprint.Restore("Recover",42); Require(sprint.Paused && sprint.Remaining==42,"restart did not pause");
      sprint.Cancel(); Require(!sprint.Active && !sprint.CompletionPending,"cancel failed");
      var bid=new AttentionBid(); int accepted=0;
      bid.Offer(()=>accepted++); bid.Step(8,true); Require(!bid.Accept() && accepted==0,"ignored bid spoke");
      bid.Offer(()=>accepted++); bid.Step(1,false); Require(!bid.Active,"suppressed bid survived");
      bid.Offer(()=>accepted++); Require(bid.Accept() && !bid.Accept() && accepted==1,"bid must run exactly once");
      var game=new CursorGame(); bool pounced=false;
      for(int i=0;i<110;i++) pounced|=game.Step(0.1,100,100,true,true);
      Require(pounced,"resting nearby cursor never triggered a pounce");
      for(int i=0;i<400;i++) Require(!game.Step(0.1,100,100,true,false),"disabled cursor game pounced");
      game=new CursorGame(); for(int i=0;i<120;i++) Require(!game.Step(0.1,i*50,100,true,true),"fast cursor triggered pounce");
      string folder=Path.Combine(Path.GetTempPath(),"Corbie-roost-test-"+Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(folder);
      try {
        string setup=Path.Combine(folder,"setup.exe"),original=Path.Combine(folder,"notes.txt"),copy=Path.Combine(folder,"notes (1).txt"),fresh=Path.Combine(folder,"fresh.msi");
        foreach(var path in new [] { setup,original,copy,fresh }) { File.WriteAllText(path,"fixture"); File.SetLastWriteTimeUtc(path,now.AddDays(-20)); }
        File.SetLastWriteTimeUtc(fresh,now);
        var result=DownloadsForager.Scan(folder,now,new ForageDecision[0],CancellationToken.None);
        Require(result.Items.Count==2,"foraging included fresh files or missed installer/copy");
        var installer=result.Items.Single(x=>x.Path==setup);
        Require(result.Items.Single(x=>x.Path==copy).Reason.Contains("possible"),"duplicate claim is not qualified");
        Require(installer.Unchanged(),"unchanged file rejected");
        var disk=new RoostMemory(Path.Combine(folder,"roost.json"));
        disk.Decide(installer,false); disk.Remember("focus","Finished an essay outline",now); disk.Data.FocusTitle="Review"; disk.Data.FocusRemaining=90;
        disk.ScavengeTrinket(new Random(42));
        disk.Save();
        var loaded=new RoostMemory(Path.Combine(folder,"roost.json"));
        Require(loaded.Data.FocusTitle=="Review" && loaded.Data.FocusRemaining==90 && loaded.Data.Entries.Count==2 && loaded.Data.Trinkets.Count==1,"disk persistence failed");
        Require(DownloadsForager.Scan(folder,now,loaded.Data.Decisions,CancellationToken.None).Items.Count==1,"snoozed file resurfaced");
        Require(DownloadsForager.Scan(folder,now.AddDays(2),loaded.Data.Decisions,CancellationToken.None).Items.Any(x=>x.Path==setup),"snooze never expires");
        disk.Decide(installer,true);
        Require(!DownloadsForager.Scan(folder,now.AddDays(40),disk.Data.Decisions,CancellationToken.None).Items.Any(x=>x.Path==setup),"Keep was ignored");
        File.AppendAllText(setup,"changed"); Require(!installer.Unchanged(),"changed file accepted for recycling");
        Require(DownloadsForager.Scan(folder,now,disk.Data.Decisions,CancellationToken.None).Items.All(x=>x.Path!=setup),"newly changed installer flagged too soon");
        var cancel=new CancellationTokenSource(); cancel.Cancel(); bool cancelled=false;
        try { DownloadsForager.Scan(folder,now,new ForageDecision[0],cancel.Token); } catch(OperationCanceledException) { cancelled=true; }
        cancel.Dispose(); Require(cancelled,"scan ignored cancellation");
        loaded.Clear(); Require(new RoostMemory(Path.Combine(folder,"roost.json")).Data.Entries.Count==0,"forget was not persisted");
      } finally {
        // This directory contains only the exact fixture files created above.
        foreach(string path in Directory.GetFiles(folder)) File.Delete(path);
        Directory.Delete(folder);
      }
    }
  }
}
