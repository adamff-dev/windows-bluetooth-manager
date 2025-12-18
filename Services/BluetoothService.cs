using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BluetoothManager.Helpers;
using BluetoothManager.Models;

namespace BluetoothManager.Services
{
    public class BluetoothService
    {
        private readonly string _dataDir;
        private readonly string _dataFile;

        public BluetoothService()
        {
            _dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BluetoothManager");
            if (!Directory.Exists(_dataDir)) Directory.CreateDirectory(_dataDir);
            _dataFile = Path.Combine(_dataDir, "devices.json");
            EnsureDataFile();
        }

        private void EnsureDataFile()
        {
            if (!File.Exists(_dataFile))
            {
                File.WriteAllText(_dataFile, JsonSerializer.Serialize(new { names = new Dictionary<string, string>() }, new JsonSerializerOptions { WriteIndented = true }));
            }
        }

        public async Task<(bool Ok, string Error, List<Device> Devices)> ListDevicesAsync()
        {
            var psScript = @"
Get-PnpDevice -Class Bluetooth | 
  Where-Object { $_.HardwareID -match 'DEV_' } | 
  Select-Object FriendlyName, Status, Class, HardwareID,
    @{N='Address';E={[uInt64]('0x{0}' -f $_.HardwareID[0].Substring(12))}},
    @{N='MAC';E={
      $hex = $_.HardwareID[0].Substring(12)
      ($hex -split '(?<=\\G.{2})' -ne '') -join ':'
    }} | 
  ForEach-Object { 
    Write-Output ('{0}|{1}|{2}|{3}' -f $_.FriendlyName, $_.Status, $_.MAC, $_.Address) 
  }
";

            var tempFile = Path.GetTempFileName() + ".ps1";
            File.WriteAllText(tempFile, psScript, Encoding.UTF8);
            try
            {
                var (ok, stdout, stderr) = await RunProcessAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{tempFile}\"");
                if (!ok) return (false, stderr ?? "Unknown error", null);
                var lines = (stdout ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToList();
                var devices = lines.Select(line =>
                {
                    var parts = line.Split('|');
                    return new Device
                    {
                        FriendlyName = parts.ElementAtOrDefault(0) ?? string.Empty,
                        Status = parts.ElementAtOrDefault(1) ?? string.Empty,
                        MAC = parts.ElementAtOrDefault(2) ?? string.Empty,
                        Address = parts.ElementAtOrDefault(3) ?? string.Empty
                    };
                }).ToList();

                var data = ReadNames();
                var devicesWithNames = devices.Select(d =>
                {
                    d.DisplayName = data.ContainsKey(d.MAC) ? data[d.MAC] : d.FriendlyName;
                    return d;
                }).ToList();

                // Add saved names that are not present
                foreach (var macSaved in data.Keys)
                {
                    if (!devicesWithNames.Any(x => x.MAC == macSaved))
                    {
                        devicesWithNames.Add(new Device
                        {
                            FriendlyName = data[macSaved],
                            Status = StringResources.GetString("DeviceStatusSaved"),
                            MAC = macSaved,
                            Address = null,
                            DisplayName = data[macSaved],
                            Known = true
                        });
                    }
                }

                return (true, null, devicesWithNames);
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }

        private Dictionary<string, string> ReadNames()
        {
            var json = File.ReadAllText(_dataFile);
            using var doc = JsonDocument.Parse(json);
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (doc.RootElement.TryGetProperty("names", out var node))
            {
                foreach (var prop in node.EnumerateObject()) names[prop.Name] = prop.Value.GetString();
            }
            return names;
        }

        private void WriteNames(Dictionary<string, string> names)
        {
            var wrapper = new { names };
            File.WriteAllText(_dataFile, JsonSerializer.Serialize(wrapper, new JsonSerializerOptions { WriteIndented = true }));
        }

        public Task<bool> SaveNameAsync(string mac, string name)
        {
            try
            {
                var names = ReadNames();
                if (string.IsNullOrWhiteSpace(name)) names.Remove(mac);
                else names[mac] = name.Trim();
                WriteNames(names);
                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> ClearAllNamesAsync()
        {
            try
            {
                WriteNames(new Dictionary<string, string>());
                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public async Task<(bool Ok, string Error)> UnpairByMacAsync(string mac)
        {
            // Normalize MAC to colon-separated lowercase (aa:bb:cc:...)
            var macNormalized = NormalizeMac(mac);

            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "BluetoothDevicePairing.exe"),
                Path.Combine(AppContext.BaseDirectory, "..", "BluetoothDevicePairing.exe"),
                Path.Combine(Environment.CurrentDirectory, "BluetoothDevicePairing.exe"),
                Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) ?? AppContext.BaseDirectory, "BluetoothDevicePairing.exe")
            };

            var collected = new StringBuilder();
            foreach (var exe in candidates.Distinct())
            {
                try
                {
                    if (!File.Exists(exe))
                    {
                        collected.AppendLine($"No encontrado: {exe}");
                        continue;
                    }

                    Log($"Intentando desemparejar usando: {exe} mac={macNormalized}");
                    var (ok, stdout, stderr) = await RunProcessAsync(exe, $"unpair-by-mac --mac {macNormalized} --type Bluetooth");
                    collected.AppendLine($"exe: {exe} exitOk={ok}");
                    if (!string.IsNullOrWhiteSpace(stdout)) collected.AppendLine($"stdout: {stdout}");
                    if (!string.IsNullOrWhiteSpace(stderr)) collected.AppendLine($"stderr: {stderr}");
                    if (ok)
                    {
                        Log($"Desemparejado OK via {exe} stdout={stdout} stderr={stderr}");
                        return (true, stdout ?? "");
                    }
                }
                catch (Exception ex)
                {
                    collected.AppendLine($"Excepción ejecutando {exe}: {ex.Message}");
                }
            }

            // Fallback: PowerShell + pnputil (may require Admin)
            Log($"Falling back to pnputil for mac={macNormalized}");
            var psScript = $@"
$mac = '{macNormalized.Replace(":", "")}'
Get-PnpDevice -Class Bluetooth | Where-Object {{ 
  $_.HardwareID -match 'DEV_' -and $_.HardwareID[0] -match ($mac -replace ':', '')
}} | ForEach-Object {{
  pnputil /remove-device $_.InstanceId
}}
";
            var (ok2, stdout2, stderr2) = await RunProcessAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript.Replace("\"", "\\\"")}\"");
            collected.AppendLine($"pnputil exitOk={ok2}");
            if (!string.IsNullOrWhiteSpace(stdout2)) collected.AppendLine($"pnputil stdout: {stdout2}");
            if (!string.IsNullOrWhiteSpace(stderr2)) collected.AppendLine($"pnputil stderr: {stderr2}");
            if (ok2)
            {
                Log($"Desemparejado OK via pnputil stdout={stdout2} stderr={stderr2}");
                return (true, stdout2);
            }

            // WinRT fallback (try DeviceInformation pairing/unpairing)
#if WINRT
            try
            {
                var winrt = await TryUnpairWithWinRTAsync(macNormalized);
                collected.AppendLine($"WinRT result: ok={winrt.Ok} err={winrt.Error}");
                if (winrt.Ok) return (true, winrt.Error);
                var msg = collected.ToString();
                Log($"Unpair failed all methods: {msg}");
                return (false, $"pnputil: {stderr2}\nWinRT: {winrt.Error}\nDetails:\n{msg}");
            }
            catch (Exception ex)
            {
                var msg = collected.ToString();
                Log($"Unpair exception WinRT: {ex.Message} details: {msg}");
                return (false, $"pnputil: {stderr2}\nWinRT exception: {ex.Message}\nDetails:\n{msg}");
            }
#else
            var msgFinal = collected.ToString();
            var relevantError = ExtractRelevantError(msgFinal);
            Log($"Unpair failed all methods. Details: {msgFinal}");
            return (false, $"Failed to unpair device. Reason: {relevantError}");
#endif
        }

        public async Task<(bool Ok, string Error)> PairByMacAsync(string mac)
        {
            // Normalize MAC to colon-separated lowercase (aa:bb:cc:...)
            var macNormalized = NormalizeMac(mac);

            var exeName = "BluetoothDevicePairing.exe";
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, exeName),
                Path.Combine(AppContext.BaseDirectory, "..", exeName),
                Path.Combine(Environment.CurrentDirectory, exeName),
                Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) ?? AppContext.BaseDirectory, exeName)
            };

            var collected = new StringBuilder();
            foreach (var exe in candidates.Distinct())
            {
                try
                {
                    if (!File.Exists(exe))
                    {
                        collected.AppendLine($"No encontrado: {exe}");
                        continue;
                    }

                    Log($"Intentando emparejar usando: {exe} mac={macNormalized}");
                    var (ok, stdout, stderr) = await RunProcessAsync(exe, $"pair-by-mac --mac {macNormalized} --type Bluetooth");
                    collected.AppendLine($"exe: {exe} exitOk={ok}");
                    if (!string.IsNullOrWhiteSpace(stdout)) collected.AppendLine($"stdout: {stdout}");
                    if (!string.IsNullOrWhiteSpace(stderr)) collected.AppendLine($"stderr: {stderr}");
                    if (ok)
                    {
                        Log($"Emparejado OK via {exe} stdout={stdout} stderr={stderr}");
                        return (true, stdout ?? "");
                    }
                }
                catch (Exception ex)
                {
                    collected.AppendLine($"Excepción ejecutando {exe}: {ex.Message}");
                }
            }

#if WINRT
            try
            {
                var winrt = await TryPairWithWinRTAsync(macNormalized);
                collected.AppendLine($"WinRT result: ok={winrt.Ok} err={winrt.Error}");
                if (winrt.Ok) return (true, winrt.Error);
                var msg = collected.ToString();
                Log($"Pair failed all methods: {msg}");
                return (false, $"WinRT: {winrt.Error}\nDetails:\n{msg}");
            }
            catch (Exception ex)
            {
                var msg = collected.ToString();
                Log($"Pair exception WinRT: {ex.Message} details: {msg}");
                return (false, $"WinRT exception: {ex.Message}\nDetails:\n{msg}");
            }
#else
            var msgFinal = collected.ToString();
            var relevantError = ExtractRelevantError(msgFinal);
            Log($"Pair failed all methods. Details: {msgFinal}");
            return (false, relevantError);
#endif
        }

        private Task<(bool Ok, string Stdout, string Stderr)> RunProcessAsync(string fileName, string arguments)
        {
            var tcs = new TaskCompletionSource<(bool, string, string)>();
            try
            {
                var psi = new ProcessStartInfo(fileName, arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
                var sbOut = new StringBuilder();
                var sbErr = new StringBuilder();
                p.OutputDataReceived += (_, e) => { if (e.Data != null) sbOut.AppendLine(e.Data); };
                p.ErrorDataReceived += (_, e) => { if (e.Data != null) sbErr.AppendLine(e.Data); };
                p.Exited += (_, __) => tcs.TrySetResult((p.ExitCode == 0, sbOut.ToString(), sbErr.ToString()));
                try
                {
                    p.Start();
                }
                catch (Exception ex)
                {
                    tcs.TrySetResult((false, string.Empty, $"Failed to start process: {ex.Message}"));
                    return tcs.Task;
                }
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                tcs.TrySetResult((false, string.Empty, ex.Message));
            }
            return tcs.Task;
        }

        private string ExtractRelevantError(string collectedDetails)
        {
            // Extract the most relevant error from collected logs
            if (string.IsNullOrWhiteSpace(collectedDetails)) return "Unknown error";
            
            var lines = collectedDetails.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            // Look for "Failed:" messages in stdout (most specific errors)
            foreach (var line in lines)
            {
                if (line.Contains("Failed:"))
                {
                    var idx = line.IndexOf("Failed:");
                    if (idx >= 0)
                        return line.Substring(idx).Trim();
                }
            }
            
            // Look for stdout (often contains the actual error)
            foreach (var line in lines)
            {
                if (line.Contains("stdout:"))
                {
                    var idx = line.IndexOf("stdout:");
                    if (idx >= 0)
                    {
                        var stdout = line.Substring(idx + 7).Trim();
                        if (!string.IsNullOrWhiteSpace(stdout) && !stdout.StartsWith("(none)"))
                            return stdout;
                    }
                }
            }
            
            // Look for stderr next
            foreach (var line in lines)
            {
                if (line.Contains("stderr:"))
                {
                    var idx = line.IndexOf("stderr:");
                    if (idx >= 0)
                    {
                        var stderr = line.Substring(idx + 7).Trim();
                        if (!string.IsNullOrWhiteSpace(stderr))
                            return stderr;
                    }
                }
            }
            
            // Return first meaningful line
            return lines.FirstOrDefault(l => !l.StartsWith("exe:") && !l.Contains("No encontrado") && !string.IsNullOrWhiteSpace(l)) 
                ?? "Operation failed - check logs for details";
        }

        private void Log(string message)
        {
            try
            {
                var path = Path.Combine(_dataDir, "bt.log");
                File.AppendAllText(path, $"{DateTime.Now:o} {message}\n");
            }
            catch
            {
                // ignore logging errors
            }
        }

#if WINRT
        // WinRT-based pairing/unpairing fallbacks
        private async Task<(bool Ok, string Error)> TryPairWithWinRTAsync(string mac)
        {
            try
            {
                var macNoColons = (mac ?? string.Empty).Replace(":", "").ToUpperInvariant();
                // AQS filter for Bluetooth devices by address
                var aqs = $"System.Devices.Aep.DeviceAddress:=\"{macNoColons}\" AND System.Devices.Aep.ProtocolId:=\"{\"e0cbf06c-cd8b-4647-bb8a-263b43f0f974\"}\"";
                var additional = new[] { "System.Devices.Aep.IsPaired" };
                var devices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(aqs, additional);
                if (devices.Count == 0) return (false, "WinRT: dispositivo no encontrado");
                var info = devices[0];
                if (info.Pairing.IsPaired) return (true, "Ya emparejado");
                var result = await info.Pairing.PairAsync();
                if (result.Status == Windows.Devices.Enumeration.DevicePairingResultStatus.Paired) return (true, "Emparejado");
                return (false, $"WinRT Pair status: {result.Status}");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private async Task<(bool Ok, string Error)> TryUnpairWithWinRTAsync(string mac)
        {
            try
            {
                var macNoColons = (mac ?? string.Empty).Replace(":", "").ToUpperInvariant();
                var aqs = $"System.Devices.Aep.DeviceAddress:=\"{macNoColons}\" AND System.Devices.Aep.ProtocolId:=\"{\"e0cbf06c-cd8b-4647-bb8a-263b43f0f974\"}\"";
                var additional = new[] { "System.Devices.Aep.IsPaired" };
                var devices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(aqs, additional);
                if (devices.Count == 0) return (false, "WinRT: dispositivo no encontrado");
                var info = devices[0];
                if (!info.Pairing.IsPaired) return (true, "No estaba emparejado");
                var result = await info.Pairing.UnpairAsync();
                if (result.Status == Windows.Devices.Enumeration.DeviceUnpairingResultStatus.Unpaired) return (true, "Desemparejado");
                return (false, $"WinRT Unpair status: {result.Status}");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
#endif

        private string NormalizeMac(string mac)
        {
            if (string.IsNullOrWhiteSpace(mac)) return mac ?? string.Empty;
            // keep only hex chars
            var hex = new string(mac.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            // remove common separators
            hex = hex.Replace("-", "").Replace(":", "").Replace(" ", "");
            if (hex.Length != 12) return mac.ToLowerInvariant();
            var parts = Enumerable.Range(0, 6).Select(i => hex.Substring(i*2, 2));
            return string.Join(":", parts).ToLowerInvariant();
        }
    }
}