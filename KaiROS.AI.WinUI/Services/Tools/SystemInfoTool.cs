using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KaiROS.AI.WinUI.Services.Tools;

public class SystemInfoTool : ITool
{
    public string Name => "SystemInfo";
    public string Description => "Gets information about the local system (CPU count, Memory usage, drives, and top processes).";
    public string ParametersJsonSchema => "{\"type\":\"object\",\"properties\":{}}";

    public bool RequiresConfirmation => false;
    public string GetConfirmationDetails(Dictionary<string, object> parameters) => "";

    public Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct)
    {
        try
        {
            var cpuCount = Environment.ProcessorCount;
            var osVersion = Environment.OSVersion;
            
            // Get disk drive info
            var drivesText = new List<string>();
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                var freeGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                var totalGb = drive.TotalSize / (1024.0 * 1024 * 1024);
                drivesText.Add($"- Drive {drive.Name} ({drive.DriveFormat}): {freeGb:F1} GB free of {totalGb:F1} GB");
            }

            // Get top processes by memory
            var processes = Process.GetProcesses()
                .Select(p => {
                    try { return new { p.ProcessName, MemoryMb = p.WorkingSet64 / (1024.0 * 1024) }; }
                    catch { return null; }
                })
                .Where(p => p != null)
                .OrderByDescending(p => p!.MemoryMb)
                .Take(5)
                .Select(p => $"- {p!.ProcessName}: {p.MemoryMb:F1} MB");

            var output = $"OS: {osVersion}\n" +
                         $"Logical Processors: {cpuCount}\n" +
                         $"Disk Drives:\n{string.Join("\n", drivesText)}\n" +
                         $"Top 5 Processes by RAM Usage:\n{string.Join("\n", processes)}";

            return Task.FromResult(new ToolResult { Success = true, Output = output });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult { Success = false, Error = $"Failed to get system info: {ex.Message}" });
        }
    }
}
