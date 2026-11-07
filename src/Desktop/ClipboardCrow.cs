using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace CorbiePet {
  public sealed class CleanUrlResult {
    public string OriginalUrl { get; set; }
    public string CleanedUrl { get; set; }
    public int StrippedCount { get; set; }
    public bool WasCleaned { get { return StrippedCount > 0 && !string.Equals(OriginalUrl, CleanedUrl, StringComparison.Ordinal); } }
  }

  public static class ClipboardCrow {
    static readonly HashSet<string> TrackingParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
      "utm_source", "utm_medium", "utm_campaign", "utm_term", "utm_content", "utm_id",
      "fbclid", "gclid", "dclid", "si", "ref", "ref_src", "ref_url", "igshid", "mc_eid",
      "_hsenc", "_hsmi", "mkevt", "aff_platform", "share_id", "tracking_id", "srsltid",
      "feature", "trk", "sc_cid"
    };

    static readonly Regex HexRegex = new Regex(@"^#([0-9a-fA-F]{6}|[0-9a-fA-F]{3})$", RegexOptions.Compiled);

    public static CleanUrlResult CleanUrl(string text) {
      var result = new CleanUrlResult { OriginalUrl = text, CleanedUrl = text, StrippedCount = 0 };
      if (string.IsNullOrWhiteSpace(text)) return result;
      text = text.Trim();

      Uri uri;
      if (!Uri.TryCreate(text, UriKind.Absolute, out uri)) return result;
      if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return result;
      if (string.IsNullOrEmpty(uri.Query)) return result;

      string query = uri.Query.TrimStart('?');
      var pairs = query.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
      var kept = new List<string>();
      int stripped = 0;

      foreach (var pair in pairs) {
        int eq = pair.IndexOf('=');
        string key = eq >= 0 ? pair.Substring(0, eq) : pair;
        if (TrackingParams.Contains(key)) {
          stripped++;
        } else {
          kept.Add(pair);
        }
      }

      if (stripped > 0) {
        var builder = new UriBuilder(uri);
        builder.Query = kept.Count > 0 ? string.Join("&", kept) : "";
        result.CleanedUrl = builder.Uri.ToString();
        result.StrippedCount = stripped;
      }
      return result;
    }

    public static Color? DetectHexColor(string text) {
      if (string.IsNullOrWhiteSpace(text)) return null;
      text = text.Trim();
      var m = HexRegex.Match(text);
      if (!m.Success) return null;

      string hex = m.Groups[1].Value;
      try {
        if (hex.Length == 3) {
          byte r = Convert.ToByte(new string(hex[0], 2), 16);
          byte g = Convert.ToByte(new string(hex[1], 2), 16);
          byte b = Convert.ToByte(new string(hex[2], 2), 16);
          return Color.FromRgb(r, g, b);
        } else if (hex.Length == 6) {
          byte r = Convert.ToByte(hex.Substring(0, 2), 16);
          byte g = Convert.ToByte(hex.Substring(2, 2), 16);
          byte b = Convert.ToByte(hex.Substring(4, 2), 16);
          return Color.FromRgb(r, g, b);
        }
      } catch { }
      return null;
    }

    public static string TryFormatJson(string text) {
      if (string.IsNullOrWhiteSpace(text)) return null;
      text = text.Trim();
      if (!((text.StartsWith("{") && text.EndsWith("}")) || (text.StartsWith("[") && text.EndsWith("]")))) return null;
      if (text.Length < 20 || text.Contains("\n  ")) return null; // Already formatted

      try {
        var ser = new System.Web.Script.Serialization.JavaScriptSerializer();
        var obj = ser.DeserializeObject(text);
        if (obj == null) return null;
        return PrettyPrintJson(text);
      } catch {
        return null;
      }
    }

    static string PrettyPrintJson(string input) {
      var indent = 0;
      var quoted = false;
      var sb = new System.Text.StringBuilder();
      for (var i = 0; i < input.Length; i++) {
        var ch = input[i];
        switch (ch) {
          case '"':
            sb.Append(ch);
            bool escaped = false;
            var index = i;
            while (index > 0 && input[--index] == '\\') escaped = !escaped;
            if (!escaped) quoted = !quoted;
            break;
          case '{':
          case '[':
            sb.Append(ch);
            if (!quoted) {
              sb.AppendLine();
              indent += 2;
              sb.Append(new string(' ', indent));
            }
            break;
          case '}':
          case ']':
            if (!quoted) {
              sb.AppendLine();
              indent = Math.Max(0, indent - 2);
              sb.Append(new string(' ', indent));
            }
            sb.Append(ch);
            break;
          case ':':
            sb.Append(ch);
            if (!quoted) sb.Append(" ");
            break;
          case ',':
            sb.Append(ch);
            if (!quoted) {
              sb.AppendLine();
              sb.Append(new string(' ', indent));
            }
            break;
          default:
            if (!quoted && char.IsWhiteSpace(ch)) { }
            else sb.Append(ch);
            break;
        }
      }
      return sb.ToString();
    }

    public static void SelfCheck() {
      // Test URL cleaner
      var url1 = "https://example.com/watch?v=12345&utm_source=twitter&si=abc987&other=keep";
      var res1 = CleanUrl(url1);
      if (!res1.WasCleaned || res1.StrippedCount != 2) throw new Exception("ClipboardCrow: Expected 2 stripped params");
      if (res1.CleanedUrl.Contains("utm_source") || res1.CleanedUrl.Contains("si")) throw new Exception("ClipboardCrow: Tracking params not stripped");
      if (!res1.CleanedUrl.Contains("other=keep") || !res1.CleanedUrl.Contains("v=12345")) throw new Exception("ClipboardCrow: Kept params missing");

      // Test URL with only tracking params
      var url2 = "https://example.com/?fbclid=XYZ123";
      var res2 = CleanUrl(url2);
      if (!res2.WasCleaned || res2.CleanedUrl.Contains("?")) throw new Exception("ClipboardCrow: Trailing query mark not stripped");

      // Test Hex detection
      var col1 = DetectHexColor("#FF5733");
      if (!col1.HasValue || col1.Value.R != 255 || col1.Value.G != 87 || col1.Value.B != 51) throw new Exception("ClipboardCrow: Hex #FF5733 failed");
      var col2 = DetectHexColor("#F0A");
      if (!col2.HasValue || col2.Value.R != 255 || col2.Value.G != 0 || col2.Value.B != 170) throw new Exception("ClipboardCrow: Short hex failed");
      if (DetectHexColor("NotAColor").HasValue) throw new Exception("ClipboardCrow: False positive on non-hex");

      // Test JSON formatting
      var rawJson = "{\"title\":\"Raven\",\"nested\":{\"items\":[1,2,3]}}";
      var formatted = TryFormatJson(rawJson);
      if (formatted == null || !formatted.Contains("\n") || !formatted.Contains("  ")) throw new Exception("ClipboardCrow: JSON format failed");
    }
  }
}
