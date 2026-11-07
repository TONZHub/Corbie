using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CorbiePet {
  public sealed class WisdomDeck {
    static readonly string[] lines={
      "Being behind is a position, not a personality. Pick one small thing you can finish, and let that be enough to begin. Yes, I'm aware that's annoyingly sensible.",
      "You don't need the confidence to finish before you start. You need enough curiosity to try the next bit. The confidence can catch up; it has legs.",
      "A nest starts with one rather unimpressive twig. Write the ugly first sentence. You can be brilliant in the second draft.",
      "Rest before you're forced to stop. There is no prize for running yourself into the ground. I'll guard the shinies; go have a life.",
      "If you only have ten minutes, spend them making the next ten easier. Open the document, find the instructions, leave yourself a clear place to begin.",
      "A mistake is information with an unfortunate entrance. Find out what it tells you, fix what you can, and spare yourself the prosecution speech.",
      "You can care about someone and still tell them no. Affection does not require unlimited access to your energy. Even I occasionally close the Nest.",
      "Before calling yourself lazy, ask whether the next step is clear. 'Sort my life out' is a threat. 'Reply to that one email' is a task.",
      "Not every thought deserves a perch. Notice it, ask whether it's useful, and let the unhelpful ones fly past without building them a bloody palace.",
      "Finishing something small teaches you more than endlessly admiring something perfect in your head. Make the small version real. Then argue with it.",
      "Ask for help while the problem is still a problem, rather than waiting for it to become a verdict on your character. It never needed to be one.",
      "You are allowed to change your mind when the evidence changes. That's learning. Stubbornness merely has a louder marketing department.",
      "When everything feels urgent, find the consequence rather than the loudest notification. What actually needs you next? Give that one thing a little room.",
      "Keep promises to yourself small enough to keep. Trust grows through evidence, not a spectacular plan made while you're avoiding lunch.",
      "Someone else's pace tells you very little about your own path. Compare your next step with where you were, not with their carefully edited highlight reel.",
      "You can miss something and still be better off without it. Both can be true. No need to bully yourself into a simpler story.",
      "A useful apology names what happened and what you'll change. Punishing yourself indefinitely does neither. Repair the twig you broke.",
      "Don't let embarrassment make an overdue task more overdue. A plain, honest message is often a better start than another day spent rehearsing the shame.",
      "If a plan only works when you feel magnificent, give it a smaller version for ordinary days. Ordinary days do most of the work around here.",
      "You don't have to solve your whole future to choose a decent afternoon. Pick one thing that leaves tomorrow a little kinder.",
      "Enjoying something does not obligate you to become impressive at it. Sometimes a hobby is allowed to be a lovely waste of time. I collect bottlecaps, for heaven's sake.",
      "When you're unsure, separate what you know from what you're guessing. Write both down. The guesses become much less regal when they have to introduce themselves.",
      "A boundary works best when it tells someone what you will do. You can't control every other creature's behaviour. Believe me, I've filed complaints.",
      "Notice what went well before moving the goalposts again. You can want to improve and still let a good effort count. I hereby permit one modest victory lap."
    };
    readonly Random random;
    readonly List<int> remaining=new List<int>();
    int last=-1;
    public WisdomDeck(Random random=null) { this.random=random??new Random(); }
    public int Count { get { return lines.Length; } }
    public string Next() {
      if(remaining.Count==0)for(int i=0;i<lines.Length;i++)remaining.Add(i);
      int pick=random.Next(remaining.Count);
      if(remaining.Count>1&&remaining[pick]==last)pick=(pick+1)%remaining.Count;
      last=remaining[pick];remaining.RemoveAt(pick);return lines[last];
    }
  }
  public static class SageAdvice {
    static readonly WisdomDeck deck=new WisdomDeck();
    static readonly object gate=new object();
    public const string Guidance="The human has asked for considered advice. Be a perceptive, fond friend with a raven's dry wit. Take their actual situation seriously: offer a useful perspective, explain the tradeoff when relevant, and suggest one small, feasible next step. Validate without assuming feelings or inventing facts. Be candid about what you don't know; ask one focused question if a missing detail would change the advice. Avoid fortune-cookie platitudes, grand certainty, celebrity/chef impressions, and jokes at the expense of vulnerability. Rest, boundaries, and asking for help are legitimate choices; don't shame the human or turn every problem into a productivity lecture. No stage directions or fictional memories. Up to four sentences and about 90 words; clarity matters more than a punchline.";
    public static string Next() { lock(gate)return deck.Next(); }
    internal static string Normalize(string text) {
      text=(text??"").Trim().ToLowerInvariant();
      text=Regex.Replace(text,@"^(?:(?:hey|okay|ok)\s+)?corb(?:ie|y)[\s,:!.-]*","");
      text=Regex.Replace(text,@"[^\p{L}\p{N}\s']","");text=Regex.Replace(text,@"\s+"," ").Trim();
      text=Regex.Replace(text,@"^(?:(?:please|can you|could you|would you)\s+)+","");
      return Regex.Replace(text,@"\s+please$","");
    }
    public static bool IsGeneralRequest(string text) {
      string s=Normalize(text);
      foreach(string phrase in new[]{"wisdom","sage advice","some sage advice","give me advice","give me some advice","give me sage advice","give me some sage advice","give me wisdom","give me some wisdom","share some wisdom","tell me something wise","give me some wise words","any advice","any words of wisdom","words of wisdom","give me some words of wisdom","impart your wisdom"})if(s==phrase)return true;
      return false;
    }
    public static bool IsRequest(string text) {
      string s=Normalize(text);
      if(Regex.IsMatch(s,@"\b(?:(?:dont|don't|do not|stop|avoid)\s+(?:give|giving|offer|offering|need|want)|no|without|not asking for)\b.{0,35}\b(?:advice|wisdom|wise words)\b"))return false;
      return IsGeneralRequest(text)||Regex.IsMatch(s,@"\b(?:need|want|would like|could use|give me|offer|share|asking for|ask for|have (?:some|any)|some|any)\b.{0,60}\b(?:advice|wisdom|wise words|words of wisdom)\b");
    }
    public static void Check() {
      var bag=new WisdomDeck(new Random(7));string previous=null;
      for(int cycle=0;cycle<5;cycle++) {
        var seen=new HashSet<string>();
        for(int i=0;i<bag.Count;i++){var line=bag.Next();if(line==previous||!seen.Add(line)||string.IsNullOrWhiteSpace(line)||line.Length>700)throw new Exception("Wisdom repeated or exceeded its speaking bound.");previous=line;}
      }
      if(!IsGeneralRequest("Corbie, could you give me some wisdom, please?")||IsGeneralRequest("Give me some advice about university"))throw new Exception("General wisdom swallowed a specific question.");
      if(!IsRequest("I need advice about school")||!IsRequest("Could I have some advice about taking a break?")||IsRequest("Please don't give me advice")||IsRequest("What's your favourite song?"))throw new Exception("Advice intent routing failed.");
      if(WakeCommand.Parse("Corbie I need advice about school",true).Action!=WakeAction.None||WakeCommand.Parse("Corbie I need advice about taking a break",true).Action!=WakeAction.None||WakeCommand.Parse("Corbie what's due",true).Action!=WakeAction.WhatsDue||WakeCommand.Parse("I need advice",true).Action!=WakeAction.NotWoken)throw new Exception("Advice bypassed or was swallowed by the wake-command gate.");
      var chat=new CorbieChat();string answer=chat.Reply("Corbie give me some wisdom",false,CancellationToken.None).GetAwaiter().GetResult();
      if(string.IsNullOrEmpty(answer)||answer.Length>700)throw new Exception("Offline spoken wisdom failed.");
      bool cancelled=false;using(var stop=new CancellationTokenSource()){stop.Cancel();try{chat.Reply("give me some wisdom",false,stop.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}}
      if(!cancelled)throw new Exception("Cancelled wisdom request answered.");
    }
  }
  public partial class Pet {
    CancellationTokenSource sageRequest;
    readonly CorbieChat sageChat=new CorbieChat();
    int sageGeneration;
    string sageDraft="",sageAnswer="",sageSource="General wisdom is available even with the local brain off.";
    TextBox sageQuestion,sageResponse;
    TextBlock sageStatus;
    Button sageAsk,sageCancel;
    void BuildSageControls(StackPanel body) {
      sageQuestion=new TextBox { Text=sageDraft,MaxLength=500,MinHeight=64,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,7,0,7) };
      sageQuestion.TextChanged+=(s,e)=>{sageDraft=sageQuestion.Text;RefreshSageControls();};
      sageQuestion.PreviewKeyDown+=(s,e)=>{if(e.Key==System.Windows.Input.Key.Enter&&(System.Windows.Input.Keyboard.Modifiers&System.Windows.Input.ModifierKeys.Control)!=0){e.Handled=true;var ignored=ThinkSageAdvice();}};
      sageResponse=new TextBox { Text=sageAnswer,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,AcceptsReturn=true,MaxHeight=230,MinHeight=50,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Background=System.Windows.Media.Brushes.Transparent,BorderThickness=new Thickness(0),Margin=new Thickness(0,8,0,4) };
      sageStatus=Status();sageAsk=ActionButton("Think this through",()=>{var ignored=ThinkSageAdvice();});sageCancel=ActionButton("Cancel thinking",CancelSageThinking);
      System.Windows.Automation.AutomationProperties.SetName(sageQuestion,"What's on your mind?");System.Windows.Automation.AutomationProperties.SetName(sageResponse,"Corbie's advice");
      body.Children.Add(Section("A word from the bird",new TextBlock { Text="A little perspective, with feathers. Ask for general wisdom or tell him what's on your mind. Ctrl+Enter asks about your question.",Foreground=MutedText,TextWrapping=TextWrapping.Wrap },new TextBlock { Text="What's on your mind? (optional)",Foreground=MutedText,Margin=new Thickness(0,8,0,0) },sageQuestion,
        Actions(ActionButton("Give me some wisdom",GiveSageWisdom),sageAsk,sageCancel,ActionButton("Clear advice",ClearSageAdvice)),sageResponse,sageStatus));RefreshSageControls();
    }
    void RefreshSageControls() {
      if(sageStatus==null)return;
      sageStatus.Text=sageRequest!=null?"Thinking locally...":sageSource;
      if(sageResponse.Text!=sageAnswer)sageResponse.Text=sageAnswer;
      sageAsk.IsEnabled=sageRequest==null&&!string.IsNullOrWhiteSpace(sageDraft);sageCancel.IsEnabled=sageRequest!=null;sageCancel.Visibility=sageRequest==null?Visibility.Collapsed:Visibility.Visible;
    }
    void CancelSageThinking() { sageGeneration++;if(sageRequest!=null)sageRequest.Cancel();sageSource="Thinking cancelled.";RefreshSageControls(); }
    void ClearSageAdvice() {
      CancelSageThinking();sageChat.Clear();companion.Dismiss();sageDraft=sageAnswer="";sageSource="Advice cleared from this Corbie session.";
      if(sageQuestion!=null)sageQuestion.Text="";RefreshSageControls();
    }
    void ShowSageAdvice(string answer,string source) {
      CancelViewerMail(false);StopTikTokTake(false);
      if(shuttingDown)return;sageAnswer=answer;sageSource=source;RefreshSageControls();mood.Conversation();
      if(companion.Quiet||micRequest!=null||(lessonNotebook!=null&&lessonNotebook.IsRecording))companion.SaySilent(answer);else companion.Say(answer);
    }
    void GiveSageWisdom() { CancelSageThinking();ShowSageAdvice(SageAdvice.Next(),"From Corbie's little book of wisdom. No local model needed."); }
    async Task ThinkSageAdvice() {
      if(shuttingDown||sageRequest!=null||string.IsNullOrWhiteSpace(sageDraft))return;
      if(ViewerMailBusy) {CancelViewerMail(false);StopTikTokTake(false);}
      if(discoveryLooking)CancelDiscovery();
      if(looking)CancelLooking();
      string question=sageDraft.Trim();if(question.Length>500)question=question.Substring(0,500);
      if(SageAdvice.IsGeneralRequest(question)){GiveSageWisdom();return;}
      int generation=++sageGeneration;var request=new CancellationTokenSource();request.CancelAfter(TimeSpan.FromSeconds(45));sageRequest=request;RefreshSageControls();
      try {
        string answer=await sageChat.Reply(question,octopusMode,request.Token,BuildSensoryContext(),true);
        if(generation!=sageGeneration||request.IsCancellationRequested||shuttingDown)return;
        ShowSageAdvice(answer,"Thought through locally. Advice is kept only for this session.");
      }catch(OperationCanceledException){
        if(generation==sageGeneration&&!shuttingDown)ShowSageAdvice("Thinking took too long. A general bit of wisdom instead: "+SageAdvice.Next(),"Thinking timed out. This is general wisdom, not a tailored answer.");
      }catch(Exception){
        if(generation==sageGeneration&&!shuttingDown)ShowSageAdvice("My thinking brain isn't answering. A general bit of wisdom instead: "+SageAdvice.Next(),"Local thinking unavailable. This is general wisdom, not a tailored answer.");
      }finally{if(sageRequest==request)sageRequest=null;request.Dispose();if(!shuttingDown)RefreshSageControls();}
    }
    void CheckSageAdvice(string preview) {
      SageAdvice.Check();var body=new StackPanel { Width=620,Background=PanelBackground };BuildSageControls(body);
      if(sageAsk.IsEnabled||sageCancel.IsEnabled)throw new Exception("Empty advice question can be sent.");
      sageQuestion.Text="I'm behind on a creative project and keep avoiding it. Where do I start?";
      if(!sageAsk.IsEnabled)throw new Exception("Advice question cannot be sent.");
      sageRequest=new CancellationTokenSource();RefreshSageControls();
      if(sageAsk.IsEnabled||sageCancel.Visibility!=Visibility.Visible)throw new Exception("Thinking controls did not guard an active request.");
      CancelSageThinking();if(!sageRequest.IsCancellationRequested)throw new Exception("Cancel thinking did not cancel the request.");sageRequest.Dispose();sageRequest=null;RefreshSageControls();
      GiveSageWisdom();if(string.IsNullOrEmpty(sageAnswer)||sageResponse.Text!=sageAnswer)throw new Exception("Wisdom did not reach the Nest.");
      ClearSageAdvice();if(sageAnswer!=""||sageDraft!=""||sageQuestion.Text!=""||sageAsk.IsEnabled)throw new Exception("Advice clearing retained text.");
      sageQuestion.Text="Give me some wisdom";ThinkSageAdvice().GetAwaiter().GetResult();
      if(sageRequest!=null||!sageSource.Contains("No local model"))throw new Exception("Typed general wisdom claimed to use the model.");
      sageQuestion.Text="I'm behind on a creative project and keep avoiding it. Where do I start?";GiveSageWisdom();
      if(!string.IsNullOrEmpty(preview)){
        body.Measure(new Size(620,double.PositiveInfinity));body.Arrange(new Rect(0,0,620,body.DesiredSize.Height));body.UpdateLayout();
        var image=new System.Windows.Media.Imaging.RenderTargetBitmap(620,(int)Math.Ceiling(body.DesiredSize.Height),96,96,System.Windows.Media.PixelFormats.Pbgra32);image.Render(body);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));using(var file=System.IO.File.Create(preview))encoder.Save(file);
      }
      companion.Dismiss();
    }
  }
}
