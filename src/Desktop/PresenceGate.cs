using System;
using System.Threading;

namespace CorbiePet {
  // Keeps one desktop creature alive at a time. A second launch wakes the
  // existing bird and then exits instead of starting another set of timers,
  // microphones, watchers, and sound effects.
  public sealed class PresenceGate : IDisposable {
    readonly Mutex mutex;
    readonly EventWaitHandle summonEvent;
    readonly RegisteredWaitHandle summonWait;
    bool disposed;

    public bool IsPrimary { get; private set; }
    public event Action Summoned;

    public PresenceGate(string identity) {
      if(string.IsNullOrWhiteSpace(identity)) throw new ArgumentException("Presence identity is required.");
      string safe=identity.Replace('\\','_').Replace('/','_');
      bool created;
      mutex=new Mutex(true,@"Local\"+safe+"_Mutex",out created);
      IsPrimary=created;
      summonEvent=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\"+safe+"_Summon");
      if(IsPrimary) {
        summonWait=ThreadPool.RegisterWaitForSingleObject(summonEvent,(state,timedOut)=> {
          if(disposed || timedOut) return;
          var handler=Summoned;
          if(handler!=null) handler();
        },null,Timeout.Infinite,false);
      }
    }

    public void SignalExisting() {
      if(!IsPrimary && !disposed) summonEvent.Set();
    }

    public void Dispose() {
      if(disposed) return;
      disposed=true;
      if(summonWait!=null) summonWait.Unregister(null);
      summonEvent.Dispose();
      if(IsPrimary) {
        try { mutex.ReleaseMutex(); } catch(ApplicationException) { }
      }
      mutex.Dispose();
    }

    public static void Check() {
      string id="CorbiePresenceTest_"+Guid.NewGuid().ToString("N");
      using(var first=new PresenceGate(id)) {
        if(!first.IsPrimary) throw new Exception("first presence gate was not primary");
        bool summoned=false;
        first.Summoned+=()=>summoned=true;
        using(var second=new PresenceGate(id)) {
          if(second.IsPrimary) throw new Exception("presence gate allowed a duplicate instance");
          second.SignalExisting();
          if(!SpinWait.SpinUntil(()=>summoned,1000)) throw new Exception("presence summon signal was not delivered");
        }
      }
    }
  }
}
