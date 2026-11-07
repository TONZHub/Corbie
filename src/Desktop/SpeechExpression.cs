using System;

namespace CorbiePet {
  public static class SpeechExpression {
    public const string Guidance="Answer the actual question first. Match detail to the request: a casual remark can be one sentence; an explanation or practical question can use up to four clear sentences and about 90 words. Give a concrete answer and the useful reason or next step when relevant. Speak naturally, with varied sentence lengths and contractions. Let personality follow the point; do not force a joke or insult into every line. You can be curious, fond or thoughtful. Avoid stage directions, stock catchphrases and repeating the last reply. Say plainly when you do not know.";
    public static string Finish(string text) {
      text=(text??"").Trim(); if(text.Length<=700) return text;
      string cut=text.Substring(0,700); int sentence=cut.LastIndexOfAny(new [] {'.','!','?'});
      if(sentence>=200) return cut.Substring(0,sentence+1);
      cut=cut.Substring(0,697); int space=cut.LastIndexOf(' '); return cut.Substring(0,space>0?space:697).TrimEnd()+"...";
    }
    public static void Check() {
      string answer="The first thing to check is the cable. A loose connection can make the display blink. Try reseating it before changing the driver.";
      answer=answer+" Check both ends of the cable, including the socket behind the monitor. If the blinking continues, try a spare cable so you can narrow down the fault. Tell me whether the picture disappears completely or only flickers; that distinction helps decide what to check next.";
      if(answer.Length<=300 || Finish(answer)!=answer) throw new Exception("A complete explanation was cut short.");
      answer=new string('x',250)+". "+new string('y',600);
      if(!Finish(answer).EndsWith(".") || Finish(answer).Contains("yyy")) throw new Exception("Speech cutoff split an available sentence.");
      if(Finish(new string('x',1000)).Length>700 || Finish("  Fine.  ")!="Fine.") throw new Exception("Speech bound failed.");
      if(Finish(new string('x',699)+" "+new string('y',100)).Length>700) throw new Exception("Speech word-boundary cutoff exceeded its limit.");
    }
  }
}
