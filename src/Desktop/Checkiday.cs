using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CorbiePet {
  public sealed class HolidayEvent {
    public string Id { get; set; }
    public string Name { get; set; }
    public string Url { get; set; }
    public override string ToString() { return Name; }
  }
  public sealed class HolidayDay {
    public string Date { get; set; }
    public string Timezone { get; set; }
    public DateTime FetchedUtc { get; set; }
    public List<HolidayEvent> Events { get; set; }
  }
  public sealed class CheckidayState {
    public bool DailyEnabled { get; set; }
    public string AnnouncedDate { get; set; }
    public string AttemptDate { get; set; }
    public int Attempts { get; set; }
    public DateTime LastAttemptUtc { get; set; }
  }
  public sealed class CheckidayClient {
    const int BodyLimit=262144;
    readonly string folder;
    readonly object sync=new object();
    readonly SemaphoreSlim requestGate=new SemaphoreSlim(1,1);
    readonly Func<HttpMessageHandler> handler;
    readonly Func<DateTime> utcNow;
    readonly JavaScriptSerializer json=new JavaScriptSerializer { MaxJsonLength=BodyLimit,RecursionLimit=16 };
    CheckidayState state=new CheckidayState();
    HolidayDay cache;
    string key;
    int credentialsVersion;
    public CheckidayClient(bool memoryOnly=false,string directory=null,Func<HttpMessageHandler> factory=null,Func<DateTime> clock=null) {
      folder=memoryOnly?null:directory??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Corbie");
      handler=factory??(()=>new HttpClientHandler { AllowAutoRedirect=false,UseProxy=true });utcNow=clock??(()=>DateTime.UtcNow);
      if(folder==null)return;
      try { key=Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(Read("checkiday.key",4096)),null,DataProtectionScope.CurrentUser)); } catch { key=null; }
      try { state=json.Deserialize<CheckidayState>(Read("checkiday-settings.json",8192))??new CheckidayState();state.Attempts=Math.Max(0,state.Attempts); } catch { state=new CheckidayState(); }
      try {
        var stored=json.Deserialize<HolidayDay>(Read("checkiday-cache.json",BodyLimit));
        DateTime date;
        if(stored!=null && DateTime.TryParseExact(stored.Date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date) && stored.Events!=null && stored.Events.Count<=100) {
          stored.Events=CleanEvents(stored.Events);stored.Timezone=Clean(stored.Timezone,80);cache=stored;
        }
      } catch { cache=null; }
    }
    string Read(string name,int limit) { string file=Path.Combine(folder,name);if(new FileInfo(file).Length>limit)throw new InvalidDataException();return File.ReadAllText(file); }
    void Write(string name,string value) {
      if(folder==null)return;
      Directory.CreateDirectory(folder);string path=Path.Combine(folder,name),temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
      try { File.WriteAllText(temp,value,new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path); }
      finally { if(File.Exists(temp))File.Delete(temp); }
    }
    void SaveState() { Write("checkiday-settings.json",json.Serialize(state)); }
    public bool Configured { get { lock(sync)return !string.IsNullOrEmpty(key); } }
    public bool DailyEnabled { get { lock(sync)return state.DailyEnabled; } }
    public bool NeedsDaily(DateTime local) { lock(sync)return Configured && state.DailyEnabled && state.AnnouncedDate!=DateKey(local); }
    public void SetDaily(bool enabled) {
      lock(sync) { bool old=state.DailyEnabled;state.DailyEnabled=enabled;try { SaveState(); }catch {state.DailyEnabled=old;throw;} }
    }
    public void SaveKey(string value) {
      value=(value??"").Trim();
      if(!Regex.IsMatch(value,@"\A[A-Za-z0-9_-]{16,256}\z"))throw new ArgumentException("Paste only your APILayer API key into this field.");
      lock(sync) {
        Write("checkiday.key",Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value),null,DataProtectionScope.CurrentUser)));
        credentialsVersion++;key=value;cache=null;
        if(folder!=null && File.Exists(Path.Combine(folder,"checkiday-cache.json")))File.Delete(Path.Combine(folder,"checkiday-cache.json"));
      }
    }
    public void ForgetKey() {
      lock(sync) {
        if(folder!=null && File.Exists(Path.Combine(folder,"checkiday.key")))File.Delete(Path.Combine(folder,"checkiday.key"));
        credentialsVersion++;key=null;cache=null;state.DailyEnabled=false;
        SaveState();
        if(folder!=null && File.Exists(Path.Combine(folder,"checkiday-cache.json")))File.Delete(Path.Combine(folder,"checkiday-cache.json"));
      }
    }
    static string DateKey(DateTime date) { return date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture); }
    public HolidayDay CachedToday(DateTime local) {
      lock(sync) {
        DateTime now=utcNow();
        return Configured && cache!=null && cache.Date==DateKey(local) && cache.FetchedUtc<=now.AddMinutes(2) && now-cache.FetchedUtc<TimeSpan.FromHours(30)?cache:null;
      }
    }
    public void MarkDiscussed(DateTime local) {
      lock(sync) {
        if(CachedToday(local)==null)return;
        string old=state.AnnouncedDate;state.AnnouncedDate=DateKey(local);
        try { SaveState(); }catch {state.AnnouncedDate=old;throw;}
      }
    }
    public bool ReserveDaily(DateTime local) {
      lock(sync) { if(!NeedsDaily(local) || CachedToday(local)==null)return false;MarkDiscussed(local);return true; }
    }
    public async Task<HolidayDay> Today(DateTime local,CancellationToken token) {
      token.ThrowIfCancellationRequested();await requestGate.WaitAsync(token).ConfigureAwait(false);
      try {
        string apiKey;int generation;DateTime started=utcNow();
        lock(sync) {
          var ready=CachedToday(local);if(ready!=null)return ready;
          if(!Configured)throw new InvalidOperationException("Save your Checkiday key in Nest > Companion > Odd little occasions first.");
          if(cache!=null && cache.Date!=DateKey(local) && started-cache.FetchedUtc<TimeSpan.FromMinutes(65))throw new InvalidOperationException("Checkiday's calendar hasn't rolled over to your local date yet. Try again in about an hour.");
          if(state.AttemptDate==DateKey(local) && state.Attempts>=3)throw new InvalidOperationException("Three Checkiday attempts today. Resting this connection until tomorrow to protect the free allowance.");
          if(started-state.LastAttemptUtc<TimeSpan.FromMinutes(10))throw new InvalidOperationException("Wait ten minutes before retrying Checkiday. No fresh list is available yet.");
          // Persist the attempt before sending; restarting cannot create a retry storm.
          if(state.AttemptDate!=DateKey(local)) { state.AttemptDate=DateKey(local);state.Attempts=0; }
          state.Attempts++;state.LastAttemptUtc=started;SaveState();apiKey=key;generation=credentialsVersion;
        }
        using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)) {
          deadline.CancelAfter(TimeSpan.FromSeconds(20));
          using(var client=new HttpClient(handler()) { Timeout=Timeout.InfiniteTimeSpan })
          using(var request=new HttpRequestMessage(HttpMethod.Get,"https://api.apilayer.com/checkiday/events?adult=false")) {
            request.Headers.TryAddWithoutValidation("apikey",apiKey);
            using(var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,deadline.Token).ConfigureAwait(false)) {
              if(response.StatusCode==HttpStatusCode.Unauthorized || response.StatusCode==HttpStatusCode.Forbidden)throw new InvalidOperationException("APILayer rejected the key or subscription. Check that your key has the Checkiday free plan attached.");
              if((int)response.StatusCode==429)throw new InvalidOperationException("Checkiday's request allowance is exhausted. Check your APILayer dashboard before retrying.");
              if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Checkiday didn't return a holiday list (HTTP "+(int)response.StatusCode+").");
              if(response.Content.Headers.ContentLength>BodyLimit)throw new InvalidDataException("Checkiday returned too much data.");
              using(var stream=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
              using(var body=new MemoryStream()) {
                byte[] buffer=new byte[4096];int count;
                while((count=await stream.ReadAsync(buffer,0,buffer.Length,deadline.Token).ConfigureAwait(false))>0) { if(body.Length+count>BodyLimit)throw new InvalidDataException("Checkiday returned too much data.");body.Write(buffer,0,count); }
                var result=Parse(Encoding.UTF8.GetString(body.ToArray()),utcNow());
                deadline.Token.ThrowIfCancellationRequested();
                lock(sync) {
                  token.ThrowIfCancellationRequested();if(generation!=credentialsVersion)throw new OperationCanceledException();
                  Write("checkiday-cache.json",json.Serialize(result));cache=result;
                }
                if(result.Date!=DateKey(local))throw new InvalidOperationException("Checkiday is reporting "+result.Date+" ("+result.Timezone+"). It isn't your local date yet; I'll wait rather than announce the wrong day.");
                return result;
              }
            }
          }
        }
      } finally { requestGate.Release(); }
    }
    sealed class ApiDay { public string date { get; set; } public string timezone { get; set; } public bool adult { get; set; } public ApiEvent[] events { get; set; } }
    sealed class ApiEvent { public string id { get; set; } public string name { get; set; } public string url { get; set; } }
    public static HolidayDay Parse(string raw,DateTime utc) {
      if(string.IsNullOrEmpty(raw) || raw.Length>BodyLimit)throw new InvalidDataException("Checkiday returned an invalid list.");
      var source=new JavaScriptSerializer { MaxJsonLength=BodyLimit,RecursionLimit=16 }.Deserialize<ApiDay>(raw);DateTime date;
      if(source==null || source.adult || source.events==null || source.events.Length>100 || !DateTime.TryParseExact(source.date,new[]{"MM/dd/yyyy","M/d/yyyy","yyyy-MM-dd"},CultureInfo.InvariantCulture,DateTimeStyles.None,out date))throw new InvalidDataException("Checkiday returned an invalid date or event list.");
      return new HolidayDay { Date=DateKey(date),Timezone=Clean(source.timezone,80),FetchedUtc=utc,Events=CleanEvents(source.events.Where(e=>e!=null).Select(e=>new HolidayEvent { Id=e.id,Name=e.name,Url=e.url })) };
    }
    static List<HolidayEvent> CleanEvents(IEnumerable<HolidayEvent> events) {
      var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var clean=new List<HolidayEvent>();
      foreach(var item in events) {
        if(item==null)continue;string name=Clean(item.Name,160);
        if(string.IsNullOrEmpty(name) || !seen.Add(name))continue;
        clean.Add(new HolidayEvent { Id=Clean(item.Id,64),Name=name,Url=ValidUrl(item.Url)?item.Url:null });
      }
      return clean;
    }
    static string Clean(string value,int max) { value=Regex.Replace(value??"",@"[\p{Cc}\p{Cf}]+"," ").Trim();return value.Length<=max?value:value.Substring(0,max); }
    public static bool ValidUrl(string value) {
      Uri url;return value!=null && value.Length<=800 && Uri.TryCreate(value,UriKind.Absolute,out url) && url.Scheme=="https" && url.IsDefaultPort && string.IsNullOrEmpty(url.UserInfo) && string.IsNullOrEmpty(url.Query) && (url.Host=="www.checkiday.com" || url.Host=="checkiday.com");
    }
    static bool Has(string text,params string[] words) { return words.Any(word=>text.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0); }
    static int Interest(HolidayEvent e) {
      if(Has(e.Name,"memorial","remembrance","awareness","cancer","aids","arthritis","indigenous","veteran","suicide","mental health"))return 0;
      if(Has(e.Name,"bird","raven","crow","feather"))return 100;
      if(Has(e.Name,"paperclip","button","shiny","treasure","coin","collect","rock day"))return 90;
      if(Has(e.Name,"cookie","nut day","seed","berry","cherry","picnic","snack","pizza","chocolate"))return 80;
      if(Has(e.Name,"music","song","book","silly","mischief","cat day","dog day"))return 60;
      return 10;
    }
    public static HolidayEvent Favourite(HolidayDay day) { return day==null || day.Events==null?null:day.Events.OrderByDescending(Interest).ThenBy(e=>e.Name,StringComparer.OrdinalIgnoreCase).FirstOrDefault(); }
    public static string Remark(HolidayDay day,HolidayEvent chosen=null) {
      var e=chosen??Favourite(day);if(e==null)return "Checkiday returned no observances for today. An unscheduled day. We can work with that.";
      string aside;
      switch(Interest(e)) {
        case 100:aside="Finally, a calendar with some respect for plumage. I shall be accepting compliments.";break;
        case 90:aside="An excellent occasion to appreciate a small, unnecessary treasure. My hoard is cultural, actually.";break;
        case 80:aside="Now there's an observance with potential. I am available for the snack committee.";break;
        case 60:aside="A little room for something you enjoy. I can begrudgingly endorse that.";break;
        case 0:aside="That one deserves a little care. Mark it in whatever way feels right to you.";break;
        default:
          string[] remarks={"Humans do find reasons to put things on a calendar. I rather like that about you.","You needn't make a grand event of it. A small nod will do.","There's always something worth noticing. Today, apparently, it's this."};
          int hash=0;foreach(char c in day.Date+e.Name)hash=(hash*31+c)&0x7fffffff;aside=remarks[hash%remarks.Length];break;
      }
      return "Checkiday says today's "+e.Name+". "+aside;
    }
    public static bool IsRequest(string text) {
      text=SageAdvice.Normalize((text??"").Replace("’","'")).Replace("'","");
      return new[]{"what are we celebrating","what are we celebrating today","whats the holiday","whats todays holiday","what is todays holiday","what holidays are today","what holidays are there today","what holiday is it","what holiday is it today","what day are we celebrating","checkiday","check todays holidays","todays holidays"}.Contains(text);
    }
  }
}
