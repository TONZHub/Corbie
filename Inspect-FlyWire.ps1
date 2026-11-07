param([string]$DataPath = (Join-Path $PSScriptRoot 'brain-data\fafb-v783\connections_princeton.csv.gz'))
$ErrorActionPreference = 'Stop'
# Offline validation only. This does not simulate neurons or change pet behavior.
if (-not ('CorbieData.FlyWireInspection' -as [type])) {
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Globalization;
namespace CorbieData {
  public sealed class FlyWireInspection {
    public long Rows;
    public long Synapses;
    public int Neurons;
    public Dictionary<string,long> TransmitterRows = new Dictionary<string,long>();
    public static FlyWireInspection Read(string path) {
      var report = new FlyWireInspection();
      var ids = new HashSet<ulong>();
      using (var file = File.OpenRead(path))
      using (var gzip = new GZipStream(file, CompressionMode.Decompress))
      using (var reader = new StreamReader(gzip)) {
        if (reader.ReadLine() != "pre_root_id,post_root_id,neuropil,syn_count,nt_type")
          throw new InvalidDataException("Unexpected FlyWire connection columns.");
        string line;
        while ((line = reader.ReadLine()) != null) {
          var cells = line.Split(',');
          ulong pre, post; long count;
          if (cells.Length != 5 ||
              !UInt64.TryParse(cells[0], NumberStyles.None, CultureInfo.InvariantCulture, out pre) ||
              !UInt64.TryParse(cells[1], NumberStyles.None, CultureInfo.InvariantCulture, out post) ||
              !Int64.TryParse(cells[3], NumberStyles.None, CultureInfo.InvariantCulture, out count) ||
              pre == 0 || post == 0 || count <= 0 || String.IsNullOrWhiteSpace(cells[2]) || String.IsNullOrWhiteSpace(cells[4]))
            throw new InvalidDataException("Invalid connection row " + (report.Rows + 2));
          ids.Add(pre); ids.Add(post);
          report.Rows++;
          report.Synapses = checked(report.Synapses + count);
          if (!report.TransmitterRows.ContainsKey(cells[4])) report.TransmitterRows[cells[4]] = 0;
          report.TransmitterRows[cells[4]]++;
        }
      }
      if (report.Rows == 0) throw new InvalidDataException("No connections in dataset.");
      report.Neurons = ids.Count;
      return report;
    }
  }
}
'@
}
$resolved = (Resolve-Path -LiteralPath $DataPath).Path
$report = [CorbieData.FlyWireInspection]::Read($resolved)
$metadata = [ordered]@{
  Dataset = 'FlyWire FAFB; user selected v783'
  Source = 'https://codex.flywire.ai/api/download?dataset=fafb'
  File = [IO.Path]::GetFileName($resolved)
  SHA256 = (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash
  ValidatedUtc = [DateTime]::UtcNow.ToString('o')
  ConnectionRows = $report.Rows
  SynapsesRepresented = $report.Synapses
  NeuronIdsInConnections = $report.Neurons
  TransmitterRows = $report.TransmitterRows
  CellTypeFilePresent = (Test-Path -LiteralPath (Join-Path ([IO.Path]::GetDirectoryName($resolved)) 'consolidated_cell_types.csv.gz'))
  Status = 'Wiring data validated. Circuit selection and simulation are not implemented.'
}
$manifest = Join-Path ([IO.Path]::GetDirectoryName($resolved)) 'inspection.json'
[IO.File]::WriteAllText($manifest, ($metadata | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
$metadata | ConvertTo-Json -Depth 4
