using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CorbiePet {
  public sealed class SchoolEvent {
    public string Id { get; set; }
    public string Title { get; set; }
    public string Course { get; set; }
    public string Url { get; set; }
    public DateTime Due { get; set; }
    public bool AllDay { get; set; }
  }
  sealed class SchoolSettingsFile {
    public int Version { get; set; }
    public bool Reminders { get; set; }
    public string ProtectedCalendarUrl { get; set; }
    public List<string> Done { get; set; }
  }

  public sealed class BlackboardCalendar : IDisposable {
    readonly bool testing;
    readonly string settingsPath;
    readonly HttpClient client;
    readonly List<SchoolEvent> events=new List<SchoolEvent>();
    readonly HashSet<string> done=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    string calendarUrl;
    bool disposed;
    public bool Reminders { get; private set; }
    public bool Configured { get { return !string.IsNullOrEmpty(calendarUrl); } }
    public int Revision { get; private set; }
    public DateTime LastRefresh { get; private set; }
    public string LastError { get; private set; }
    public List<SchoolEvent> Events { get { return events.Where(x=>!done.Contains(x.Id)).OrderBy(x=>x.Due).ToList(); } }

    public BlackboardCalendar(bool testing=false) {
      this.testing=testing; Reminders=true;
      settingsPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Corbie","school.json");
      System.Net.ServicePointManager.SecurityProtocol|=System.Net.SecurityProtocolType.Tls12;
      var handler=new HttpClientHandler { UseProxy=true,AllowAutoRedirect=true,AutomaticDecompression=System.Net.DecompressionMethods.GZip|System.Net.DecompressionMethods.Deflate };
      client=new HttpClient(handler) { Timeout=TimeSpan.FromSeconds(30) };
      client.DefaultRequestHeaders.UserAgent.ParseAdd("CorbieDesktopPet/0.26");
      if(!testing) Load();
    }
    public void Configure(string url) {
      url=NormalizeUrl(url);
      Uri parsed;
      if(!Uri.TryCreate((url??"").Trim(),UriKind.Absolute,out parsed) || parsed.Scheme!=Uri.UriSchemeHttps) throw new ArgumentException("Paste the complete HTTPS Share Calendar link from Blackboard.");
      if(!testing) Protect(parsed.AbsoluteUri); // Fail visibly if this Windows session cannot safely store the private link.
      calendarUrl=parsed.AbsoluteUri; LastError=null; Save(); Revision++;
    }
    public void Disconnect() { calendarUrl=null; events.Clear(); done.Clear(); LastRefresh=DateTime.MinValue; LastError=null; Save(); Revision++; }
    public void SetReminders(bool enabled) { Reminders=enabled; Save(); }
    public bool IsDone(SchoolEvent item) { return item!=null && done.Contains(item.Id); }
    public void MarkDone(SchoolEvent item) { if(item==null) return; done.Add(item.Id); Save(); Revision++; }
    public void Restore(SchoolEvent item) { if(item==null) return; done.Remove(item.Id); Save(); Revision++; }
    public async Task Refresh(CancellationToken token) {
      if(!Configured) throw new InvalidOperationException("Blackboard is not connected yet.");
      try {
        using(var response=await client.GetAsync(calendarUrl,HttpCompletionOption.ResponseHeadersRead,token)) {
          response.EnsureSuccessStatusCode();
          long? length=response.Content.Headers.ContentLength; if(length.HasValue && length.Value>5*1024*1024) throw new InvalidOperationException("That calendar feed is unexpectedly large.");
          string text=await response.Content.ReadAsStringAsync(); token.ThrowIfCancellationRequested();
          if(text.Length>5*1024*1024 || text.TrimStart().StartsWith("<")) throw new InvalidOperationException("Blackboard returned a web page instead of a calendar. Copy a fresh Share Calendar link.");
          var parsed=Parse(text); if(parsed.Count==0 && text.IndexOf("BEGIN:VCALENDAR",StringComparison.OrdinalIgnoreCase)<0) throw new InvalidOperationException("The link did not return a Blackboard calendar.");
          events.Clear(); events.AddRange(parsed); LastRefresh=DateTime.Now; LastError=null; Revision++;
        }
      } catch(OperationCanceledException) { throw; }
      catch(Exception ex) { LastError=Explain(ex); Revision++; throw new InvalidOperationException(LastError,ex); }
    }
    public List<SchoolEvent> Visible(DateTime now) {
      return Events.Where(x=>x.Due>=now.AddDays(-45) && x.Due<=now.AddDays(120)).ToList();
    }
    public List<SchoolEvent> Overdue(DateTime now) { return Events.Where(x=>x.Due<now && x.Due>=now.AddDays(-45)).ToList(); }
    public List<SchoolEvent> DueSoon(DateTime now) { return Events.Where(x=>x.Due>=now && x.Due<=now.AddHours(36)).ToList(); }
    public string Summary(DateTime now) {
      if(!Configured) return "Blackboard is not connected.";
      if(!string.IsNullOrEmpty(LastError)) return "Blackboard calendar error: "+LastError;
      int overdue=Overdue(now).Count,soon=DueSoon(now).Count;
      if(LastRefresh==DateTime.MinValue) return "Blackboard calendar connected · waiting for first refresh";
      return overdue+" past-due calendar item"+(overdue==1?"":"s")+" · "+soon+" due in the next 36 hours · refreshed "+LastRefresh.ToString("t");
    }
    public string Reminder(DateTime now) {
      var late=Overdue(now); if(late.Count>0) return "Blackboard reports "+late.Count+" past-due item"+(late.Count==1?"":"s")+". The oldest is \""+late[0].Title+"\". Open the School tab; we're triaging this nest.";
      var soon=DueSoon(now); if(soon.Count>0) return "School radar: \""+soon[0].Title+"\" is due "+FriendlyDue(soon[0].Due,now)+". Annoying, but actionable.";
      return null;
    }
    public static string FriendlyDue(DateTime due,DateTime now) {
      var span=due-now;
      if(span.TotalMinutes<0) return -span.TotalHours<48?Math.Max(1,(int)Math.Ceiling(-span.TotalHours))+" hours past its calendar date":Math.Max(2,(int)Math.Ceiling(-span.TotalDays))+" days past its calendar date";
      if(span.TotalHours<2) return "in "+Math.Max(1,(int)Math.Ceiling(span.TotalMinutes))+" minutes";
      if(span.TotalHours<48) return "in "+Math.Ceiling(span.TotalHours)+" hours";
      return "on "+due.ToString("ddd, MMM d");
    }
    internal static string NormalizeUrl(string value) {
      string url=(value??"").Trim().Trim('"','\'');
      if(url.StartsWith("webcal://",StringComparison.OrdinalIgnoreCase)) url="https://"+url.Substring(9);
      return url;
    }
    static string Explain(Exception ex) {
      Exception root=ex; while(root.InnerException!=null) root=root.InnerException;
      string detail=root.Message;
      if(ex is HttpRequestException || root is System.Net.WebException) return "Could not download the Blackboard calendar: "+detail+" Check that the Share Calendar link is current, then try again.";
      return ex.Message;
    }
    static string Unescape(string value) {
      return (value??"").Replace("\\n"," ").Replace("\\N"," ").Replace("\\,",",").Replace("\\;",";").Replace("\\\\","\\").Trim();
    }
    static DateTime ParseDate(string property,string value,out bool allDay) {
      allDay=property.IndexOf("VALUE=DATE",StringComparison.OrdinalIgnoreCase)>=0 || value.Length==8;
      DateTime result;
      string[] utc={"yyyyMMdd'T'HHmmss'Z'","yyyyMMdd'T'HHmm'Z'"};
      if(DateTime.TryParseExact(value,utc,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out result)) return result.ToLocalTime();
      string[] local={"yyyyMMdd'T'HHmmss","yyyyMMdd'T'HHmm","yyyyMMdd"};
      if(DateTime.TryParseExact(value,local,CultureInfo.InvariantCulture,DateTimeStyles.None,out result)) return DateTime.SpecifyKind(result,DateTimeKind.Local);
      if(DateTime.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.AssumeLocal,out result)) return result;
      return DateTime.MinValue;
    }
    internal static List<SchoolEvent> Parse(string text) {
      var raw=(text??"").Replace("\r\n","\n").Replace('\r','\n').Split('\n'); var lines=new List<string>();
      foreach(string source in raw) {
        if((source.StartsWith(" ") || source.StartsWith("\t")) && lines.Count>0) lines[lines.Count-1]+=source.Substring(1);
        else lines.Add(source);
      }
      var result=new List<SchoolEvent>(); SchoolEvent item=null; string description=null;
      foreach(string line in lines) {
        if(line.Equals("BEGIN:VEVENT",StringComparison.OrdinalIgnoreCase)) { item=new SchoolEvent(); description=null; continue; }
        if(line.Equals("END:VEVENT",StringComparison.OrdinalIgnoreCase)) {
          if(item!=null && !string.IsNullOrWhiteSpace(item.Title) && item.Due!=DateTime.MinValue) {
            if(string.IsNullOrEmpty(item.Id)) item.Id=(item.Title+"|"+item.Due.ToUniversalTime().ToString("o")).ToLowerInvariant();
            if(string.IsNullOrEmpty(item.Url) && !string.IsNullOrEmpty(description)) { var match=Regex.Match(description,@"https?://[^\s<>]+",RegexOptions.IgnoreCase); if(match.Success) item.Url=match.Value.TrimEnd('.',',',')'); }
            result.Add(item);
          }
          item=null; continue;
        }
        if(item==null) continue; int colon=line.IndexOf(':'); if(colon<=0) continue;
        string property=line.Substring(0,colon),name=property.Split(';')[0].ToUpperInvariant(),value=Unescape(line.Substring(colon+1));
        if(name=="SUMMARY") item.Title=value;
        else if(name=="UID") item.Id=value;
        else if(name=="URL") item.Url=value;
        else if(name=="LOCATION" || name=="CATEGORIES") { if(string.IsNullOrEmpty(item.Course)) item.Course=value; }
        else if(name=="DESCRIPTION") description=value;
        else if(name=="DTSTART") { bool allDay; item.Due=ParseDate(property,value,out allDay); item.AllDay=allDay; if(allDay && item.Due!=DateTime.MinValue) item.Due=item.Due.Date.AddDays(1).AddSeconds(-1); }
      }
      return result.GroupBy(x=>x.Id,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).OrderBy(x=>x.Due).ToList();
    }
    void Load() {
      try {
        if(!File.Exists(settingsPath)) return;
        var data=new JavaScriptSerializer().Deserialize<SchoolSettingsFile>(File.ReadAllText(settingsPath)); if(data==null) return;
        Reminders=data.Reminders;
        if(!string.IsNullOrEmpty(data.ProtectedCalendarUrl)) calendarUrl=Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(data.ProtectedCalendarUrl),null,DataProtectionScope.CurrentUser));
        if(data.Done!=null) foreach(string id in data.Done.Take(5000)) done.Add(id);
      } catch { calendarUrl=null; }
    }
    void Save() {
      if(testing) return;
      try {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)); string protectedUrl=null;
        if(!string.IsNullOrEmpty(calendarUrl)) protectedUrl=Protect(calendarUrl);
        var data=new SchoolSettingsFile { Version=1,Reminders=Reminders,ProtectedCalendarUrl=protectedUrl,Done=done.Take(5000).ToList() };
        string temp=settingsPath+".tmp"; File.WriteAllText(temp,new JavaScriptSerializer().Serialize(data)); if(File.Exists(settingsPath)) File.Replace(temp,settingsPath,null); else File.Move(temp,settingsPath);
      } catch { }
    }
    public void Dispose() { if(disposed) return; Save(); client.Dispose(); disposed=true; }
    static string Protect(string value) { return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value),null,DataProtectionScope.CurrentUser)); }
    public static void Check() {
      string sample="BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:essay-1\r\nSUMMARY:Essay One\r\nCATEGORIES:Writing 101\r\nDTSTART:20300102T235900Z\r\nDESCRIPTION:Open it at https://school.example/assignment/1\r\nEND:VEVENT\r\nBEGIN:VEVENT\r\nSUMMARY:Reading Day\r\nDTSTART;VALUE=DATE:20300103\r\nEND:VEVENT\r\nEND:VCALENDAR";
      var parsed=Parse(sample); if(parsed.Count!=2 || parsed[0].Title!="Essay One" || string.IsNullOrEmpty(parsed[0].Url) || !parsed[1].AllDay) throw new Exception("Blackboard calendar parsing failed.");
      if(NormalizeUrl("webcal://school.example/feed")!="https://school.example/feed") throw new Exception("Blackboard webcal link normalization failed.");
      try {
        byte[] secret=Encoding.UTF8.GetBytes("private-calendar-test"),protectedBytes=ProtectedData.Protect(secret,null,DataProtectionScope.CurrentUser),roundTrip=ProtectedData.Unprotect(protectedBytes,null,DataProtectionScope.CurrentUser);
        if(Encoding.UTF8.GetString(roundTrip)!="private-calendar-test") throw new Exception("Windows calendar-link protection failed.");
      } catch(CryptographicException) { /* The isolated smoke-test account may not have a loaded Windows profile. */ }
      using(var calendar=new BlackboardCalendar(true)) { calendar.Configure("https://school.example/calendar.ics"); calendar.events.AddRange(parsed); calendar.MarkDone(parsed[0]); if(calendar.Events.Count!=1) throw new Exception("Blackboard local completion tracking failed."); }
    }
  }
}
