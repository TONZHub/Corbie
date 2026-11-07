using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace CorbiePet {
  public class ArchiveInfo {
    public string Path { get; set; }
    public string FileName { get; set; }
    public long FileSizeBytes { get; set; }
    public int EntryCount { get; set; }
    public List<string> SampleEntries { get; set; }
    public List<string> Entries { get; set; }
    public bool IsEncrypted { get; set; }
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; }

    public ArchiveInfo() {
      SampleEntries = new List<string>();
      Entries = new List<string>();
      IsValid = true;
    }

    public string FormatSummary() {
      string sizeStr = FormatBytes(FileSizeBytes);
      string countStr = EntryCount == 1 ? "1 item" : EntryCount + " items";
      var sb = new StringBuilder();
      sb.Append(FileName + " (" + countStr + ", " + sizeStr + ")");
      if(SampleEntries.Count > 0) {
        sb.Append(":\n");
        int show = Math.Min(3, SampleEntries.Count);
        for(int i = 0; i < show; i++) {
          sb.Append("• " + SampleEntries[i] + "\n");
        }
        if(EntryCount > show) {
          sb.Append("• ... and " + (EntryCount - show) + " more");
        }
      }
      return sb.ToString().TrimEnd();
    }

    static string FormatBytes(long bytes) {
      if(bytes < 1024) return bytes + " B";
      if(bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
      if(bytes < 1024 * 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("F1") + " MB";
      return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("F1") + " GB";
    }
  }

  public class ExtractionResult {
    public bool Success { get; set; }
    public bool PasswordRequired { get; set; }
    public bool WrongPassword { get; set; }
    public bool ExistingFiles { get; set; }
    public bool CorruptedOrInvalid { get; set; }
    public string OutputDirectory { get; set; }
    public int ExtractedCount { get; set; }
    public string ErrorMessage { get; set; }
  }

  public static class ArchiveExtractor {
    static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
      ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".tbz2", ".xz", ".txz"
    };

    static string TarPath {
      get {
        string sysTar = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "tar.exe");
        if(File.Exists(sysTar)) return sysTar;
        return "tar.exe";
      }
    }

    public static bool IsSupportedArchive(string path) {
      if(string.IsNullOrEmpty(path)) return false;
      string ext = Path.GetExtension(path);
      if(string.IsNullOrEmpty(ext)) return false;
      if(SupportedExtensions.Contains(ext)) return true;
      if(path.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
         path.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase) ||
         path.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase)) {
        return true;
      }
      return false;
    }

    public static string GetArchiveBaseName(string path) {
      string fileName = Path.GetFileName(path);
      if(fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)) return fileName.Substring(0, fileName.Length - 7);
      if(fileName.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase)) return fileName.Substring(0, fileName.Length - 8);
      if(fileName.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase)) return fileName.Substring(0, fileName.Length - 7);
      return Path.GetFileNameWithoutExtension(path);
    }

    public static string GetUniqueTargetDirectory(string parentDir, string baseName) {
      string candidate = Path.Combine(parentDir, baseName);
      if(!Directory.Exists(candidate) && !File.Exists(candidate)) return candidate;
      int i = 1;
      while(true) {
        candidate = Path.Combine(parentDir, baseName + " (" + i + ")");
        if(!Directory.Exists(candidate) && !File.Exists(candidate)) return candidate;
        i++;
      }
    }

    public static ArchiveInfo Inspect(string filePath, string password = null) {
      var info = new ArchiveInfo {
        Path = filePath,
        FileName = Path.GetFileName(filePath)
      };

      if(!File.Exists(filePath)) {
        info.IsValid = false;
        info.ErrorMessage = "File does not exist: " + filePath;
        return info;
      }

      try {
        var fi = new FileInfo(filePath);
        info.FileSizeBytes = fi.Length;
        if(fi.Length == 0) {
          info.IsValid = false;
          info.ErrorMessage = "Archive file is 0 bytes.";
          return info;
        }
      } catch(Exception ex) {
        info.IsValid = false;
        info.ErrorMessage = ex.Message;
        return info;
      }

      string args = "-tf \"" + filePath + "\"";
      if(!string.IsNullOrEmpty(password)) {
        args = "--passphrase \"" + EscapeArg(password) + "\" " + args;
      }

      var psi = new ProcessStartInfo {
        FileName = TarPath,
        Arguments = args,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8
      };

      try {
        using(var proc = Process.Start(psi)) {
          var stdout = new StringBuilder();
          var stderr = new StringBuilder();
          proc.OutputDataReceived += (s, e) => { if(e.Data != null) stdout.AppendLine(e.Data); };
          proc.ErrorDataReceived += (s, e) => { if(e.Data != null) stderr.AppendLine(e.Data); };
          proc.BeginOutputReadLine();
          proc.BeginErrorReadLine();

          bool exited = proc.WaitForExit(10000);
          if(!exited) {
            try { proc.Kill(); } catch { }
            info.IsValid = false;
            info.ErrorMessage = "Inspection timed out.";
            return info;
          }
          proc.WaitForExit(); // Flush asynchronous stdout/stderr handlers.

          string err = stderr.ToString();
          string outText = stdout.ToString();

          if(proc.ExitCode != 0) {
            string combinedErr = (err + " " + outText).ToLowerInvariant();
            if(combinedErr.Contains("passphrase") || combinedErr.Contains("password") || combinedErr.Contains("encrypted")) {
              info.IsEncrypted = true;
              info.IsValid = true;
              info.ErrorMessage = "Passphrase required";
              return info;
            }

            // Fallback for zip if tar failed
            if(filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
              return InspectZipFallback(filePath);
            }

            info.IsValid = false;
            info.ErrorMessage = !string.IsNullOrEmpty(err) ? err.Trim() : "Failed to parse archive (exit code " + proc.ExitCode + ")";
            return info;
          }

          var lines = outText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
          int count = 0;
          foreach(var line in lines) {
            string entry = line.Trim();
            if(entry.StartsWith("./")) entry = entry.Substring(2);
            if(string.IsNullOrEmpty(entry)) continue;
            count++;
            info.Entries.Add(entry);
            if(info.SampleEntries.Count < 5) {
              info.SampleEntries.Add(entry);
            }
          }
          info.EntryCount = count;
          info.IsValid = true;
          return info;
        }
      } catch(Exception) {
        if(filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
          return InspectZipFallback(filePath);
        }
        info.IsValid = false;
        info.ErrorMessage = "Archive engine unavailable.";
        return info;
      }
    }

    static ArchiveInfo InspectZipFallback(string filePath) {
      var info = new ArchiveInfo {
        Path = filePath,
        FileName = Path.GetFileName(filePath)
      };
      try {
        var fi = new FileInfo(filePath);
        info.FileSizeBytes = fi.Length;
        using(var archive = ZipFile.OpenRead(filePath)) {
          info.EntryCount = archive.Entries.Count;
          foreach(var entry in archive.Entries) {
            info.Entries.Add(entry.FullName);
            if(info.SampleEntries.Count < 5) info.SampleEntries.Add(entry.FullName);
          }
          info.IsValid = true;
          return info;
        }
      } catch(InvalidDataException ex) {
        info.IsValid = false;
        info.ErrorMessage = "Malformed zip file: " + ex.Message;
        return info;
      } catch(Exception ex) {
        info.IsValid = false;
        info.ErrorMessage = ex.Message;
        return info;
      }
    }

    public static ExtractionResult Extract(string filePath, string targetDir, string password = null) {
      var result = new ExtractionResult {
        OutputDirectory = targetDir
      };

      if(!File.Exists(filePath)) {
        result.CorruptedOrInvalid = true;
        result.ErrorMessage = "File does not exist: " + filePath;
        return result;
      }

      var preview=Inspect(filePath,password);
      string root=Path.GetFullPath(targetDir).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
      foreach(string entry in preview.Entries) {
          string relative=entry.Replace('/',Path.DirectorySeparatorChar);
          while(relative.StartsWith("."+Path.DirectorySeparatorChar,StringComparison.Ordinal)) relative=relative.Substring(2);
          string destination;
          try { destination=Path.GetFullPath(Path.Combine(targetDir,relative)); }
          catch(Exception) { result.CorruptedOrInvalid=true; result.ErrorMessage="Archive contains an invalid path."; return result; }
          if(!destination.StartsWith(root,StringComparison.OrdinalIgnoreCase)) {
            result.CorruptedOrInvalid=true; result.ErrorMessage="Archive contains a path outside the destination."; return result;
          }
          if(Directory.Exists(targetDir) && !entry.EndsWith("/",StringComparison.Ordinal) && (File.Exists(destination) || Directory.Exists(destination))) {
            result.ExistingFiles=true;
            result.ErrorMessage="Existing files were left untouched. Nothing was extracted.";
            return result;
          }
      }

      bool createdTarget = false;
      if(!Directory.Exists(targetDir)) {
        try {
          Directory.CreateDirectory(targetDir);
          createdTarget = true;
        } catch(Exception ex) {
          result.CorruptedOrInvalid = true;
          result.ErrorMessage = "Could not create target directory: " + ex.Message;
          return result;
        }
      }

      // Protect files already in the destination, including in Extract here mode.
      string args = "-xkf \"" + filePath + "\" -C \"" + targetDir + "\"";
      if(!string.IsNullOrEmpty(password)) {
        args = "--passphrase \"" + EscapeArg(password) + "\" " + args;
      }

      var psi = new ProcessStartInfo {
        FileName = TarPath,
        Arguments = args,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8
      };

      try {
        using(var proc = Process.Start(psi)) {
          var stderr = new StringBuilder();
          proc.ErrorDataReceived += (s, e) => { if(e.Data != null) stderr.AppendLine(e.Data); };
          proc.BeginErrorReadLine();

          bool exited = proc.WaitForExit(60000);
          if(!exited) {
            try { proc.Kill(); } catch { }
            result.CorruptedOrInvalid = true;
            result.ErrorMessage = "Extraction timed out.";
            CleanupIfEmpty(targetDir, createdTarget);
            return result;
          }
          proc.WaitForExit(); // Flush asynchronous stderr before classifying failures.

          string err = stderr.ToString();

          if(proc.ExitCode == 0) {
            result.Success = true;
            result.ExtractedCount = CountFiles(targetDir);
            return result;
          }

          string combinedErr = err.ToLowerInvariant();
          if(combinedErr.Contains("file exists") || combinedErr.Contains("already exists")) {
            result.ExistingFiles = true;
            result.ErrorMessage = "Existing files were left untouched. Other archive files may have been extracted.";
            return result;
          }
          if(combinedErr.Contains("passphrase") || combinedErr.Contains("password") || combinedErr.Contains("encrypted")) {
            if(!string.IsNullOrEmpty(password)) {
              result.WrongPassword = true;
              result.ErrorMessage = "Incorrect passphrase.";
            } else {
              result.PasswordRequired = true;
              result.ErrorMessage = "Passphrase required.";
            }
            CleanupIfEmpty(targetDir, createdTarget);
            return result;
          }

          // Fallback for zip
          if(filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(password)) {
            try {
              ZipFile.ExtractToDirectory(filePath, targetDir);
              result.Success = true;
              result.ExtractedCount = CountFiles(targetDir);
              return result;
            } catch { }
          }

          result.CorruptedOrInvalid = true;
          result.ErrorMessage = !string.IsNullOrEmpty(err) ? err.Trim() : "Extraction failed (exit code " + proc.ExitCode + ")";
          CleanupIfEmpty(targetDir, createdTarget);
          return result;
        }
      } catch(Exception ex) {
        if(filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(password)) {
          try {
            ZipFile.ExtractToDirectory(filePath, targetDir);
            result.Success = true;
            result.ExtractedCount = CountFiles(targetDir);
            return result;
          } catch { }
        }

        result.CorruptedOrInvalid = true;
        result.ErrorMessage = ex.Message;
        CleanupIfEmpty(targetDir, createdTarget);
        return result;
      }
    }

    static void CleanupIfEmpty(string dir, bool wasCreated) {
      if(!wasCreated) return;
      try {
        if(Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length == 0) {
          Directory.Delete(dir, false);
        }
      } catch { }
    }

    static int CountFiles(string dir) {
      try {
        return Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length;
      } catch {
        return 0;
      }
    }

    static string EscapeArg(string arg) {
      if(arg == null) return "";
      return arg.Replace("\"", "\\\"");
    }

    public static void Check() {
      string tempDir = Path.Combine(Path.GetTempPath(), "CorbieArchiveTest_" + Guid.NewGuid().ToString("N"));
      try {
        Directory.CreateDirectory(tempDir);
        string srcDir = Path.Combine(tempDir, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "doc.txt"), "Testing Corbie peck open.");
        File.WriteAllText(Path.Combine(srcDir, "data.csv"), "a,b,c\n1,2,3");

        string zipPath = Path.Combine(tempDir, "pack.zip");
        ZipFile.CreateFromDirectory(srcDir, zipPath);

        // 1. Check supported extension
        if(!IsSupportedArchive(zipPath)) throw new Exception("zipPath should be supported archive");
        if(IsSupportedArchive("file.png")) throw new Exception("png should not be supported archive");

        // 2. Check inspect valid
        var info = Inspect(zipPath);
        if(!info.IsValid) throw new Exception("Valid zip inspection failed: " + info.ErrorMessage);
        if(info.EntryCount < 2) throw new Exception("Entry count mismatch: " + info.EntryCount);
        if(info.SampleEntries.Count == 0) throw new Exception("Sample entries empty");
        string summary = info.FormatSummary();
        if(string.IsNullOrEmpty(summary)) throw new Exception("FormatSummary was empty");

        // 3. Check extraction to unique folder
        string targetDir = GetUniqueTargetDirectory(tempDir, "pack_out");
        var res = Extract(zipPath, targetDir);
        if(!res.Success) throw new Exception("Extraction failed: " + res.ErrorMessage);
        if(!File.Exists(Path.Combine(targetDir, "doc.txt"))) throw new Exception("Extracted file missing: doc.txt");
        if(res.ExtractedCount != 2) throw new Exception("Extracted count mismatch: " + res.ExtractedCount);

        // Extract here must never replace a user's existing file.
        string occupiedDir = Path.Combine(tempDir, "occupied");
        Directory.CreateDirectory(occupiedDir);
        string original = Path.Combine(occupiedDir, "doc.txt");
        File.WriteAllText(original, "Keep this original.");
        var collision = Extract(zipPath, occupiedDir);
        if(!collision.ExistingFiles || File.ReadAllText(original)!="Keep this original.")
          throw new Exception("Archive extraction replaced an existing file");

        // 4. Check unique directory collision handling
        string targetDir2 = GetUniqueTargetDirectory(tempDir, "pack_out");
        if(targetDir2 == targetDir || !targetDir2.Contains("(1)")) {
          throw new Exception("Unique directory name collision failure: " + targetDir2);
        }

        // 5. Check corrupted archive
        string corruptPath = Path.Combine(tempDir, "corrupt.zip");
        File.WriteAllText(corruptPath, "THIS IS DEFINITELY NOT A VALID ZIP FILE HEADER JUNK DATA");
        var corruptInfo = Inspect(corruptPath);
        if(corruptInfo.IsValid && !corruptInfo.IsEncrypted) {
          throw new Exception("Corrupt archive was marked valid");
        }
        var corruptRes = Extract(corruptPath, Path.Combine(tempDir, "corrupt_out"));
        if(corruptRes.Success) throw new Exception("Corrupt archive extraction succeeded unexpectedly");
        if(!corruptRes.CorruptedOrInvalid) throw new Exception("Corrupt archive extraction should set CorruptedOrInvalid");
      } finally {
        try {
          string full=Path.GetFullPath(tempDir).TrimEnd(Path.DirectorySeparatorChar);
          string parent=Path.GetDirectoryName(full);
          if(string.Equals(parent,Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)
             && Path.GetFileName(full).StartsWith("CorbieArchiveTest_",StringComparison.Ordinal)
             && Directory.Exists(full)) Directory.Delete(full,true);
        } catch { }
      }
    }
  }
}
