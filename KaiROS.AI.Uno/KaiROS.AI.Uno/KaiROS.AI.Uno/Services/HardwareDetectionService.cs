using KaiROS.AI.Uno.Models;
using System.Runtime.InteropServices;

namespace KaiROS.AI.Uno.Services;

public class HardwareDetectionService : IHardwareDetectionService
{
    private HardwareInfo? _cachedHardware;
    private ExecutionBackend _selectedBackend = ExecutionBackend.Cpu;

    public async Task<HardwareInfo> DetectHardwareAsync()
    {
        if (_cachedHardware != null)
            return _cachedHardware;

        var info = new HardwareInfo
        {
            TotalRamBytes = GetTotalRamBytes(),
            AvailableRamBytes = GetAvailableRamBytes(),
            HasCuda = false,
            HasVulkan = false,
            HasNpu = false
        };

#if DESKTOP
        // Detect GPU on Windows desktop
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                info.GpuName = await GetGpuNameAsync();
                info.HasCuda = DetectCuda();
                info.HasVulkan = DetectVulkan();
            }
            catch
            {
                // Ignore detection errors
            }
        }
#else
        // WASM - no native GPU detection
        info.GpuName = "Browser (WebGPU may be available)";
#endif

        // Determine available backends
        info.AvailableBackends.Add(ExecutionBackend.Cpu);

#if DESKTOP
        if (info.HasCuda)
        {
            info.AvailableBackends.Add(ExecutionBackend.Cuda);
            info.RecommendedBackend = ExecutionBackend.Cuda;
        }
        if (info.HasVulkan)
        {
            info.AvailableBackends.Add(ExecutionBackend.Vulkan);
            if (info.RecommendedBackend == ExecutionBackend.Cpu)
                info.RecommendedBackend = ExecutionBackend.Vulkan;
        }
#endif

        info.SelectedBackend = info.RecommendedBackend;
        _selectedBackend = info.SelectedBackend;

#if DESKTOP
        info.StatusMessage = $"Detected: {info.TotalRamText} RAM" +
            (string.IsNullOrEmpty(info.GpuName) ? "" : $", {info.GpuName}");
#else
        info.StatusMessage = "WebAssembly mode - AI inference unavailable. Use desktop app for local LLM.";
#endif

        _cachedHardware = info;
        return info;
    }

    public ExecutionBackend GetRecommendedBackend()
    {
        return _cachedHardware?.RecommendedBackend ?? ExecutionBackend.Cpu;
    }

    public bool IsBackendAvailable(ExecutionBackend backend)
    {
        return _cachedHardware?.AvailableBackends.Contains(backend) ?? backend == ExecutionBackend.Cpu;
    }

    public void ClearCache()
    {
        _cachedHardware = null;
    }

    public void SetSelectedBackend(ExecutionBackend backend)
    {
        _selectedBackend = backend;
        if (_cachedHardware != null)
            _cachedHardware.SelectedBackend = backend;
    }

    private long GetTotalRamBytes()
    {
#if DESKTOP
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Environment.SystemPageSize * 1024 * 1024;
        }
#endif
        // Default for WASM/other platforms
        return 4L * 1024 * 1024 * 1024; // 4GB default
    }

    private long GetAvailableRamBytes()
    {
        return GetTotalRamBytes() / 2;
    }

#if DESKTOP
    private async Task<string> GetGpuNameAsync()
    {
        await Task.CompletedTask;
        // Would use WMI on Windows to detect GPU
        return string.Empty;
    }

    private bool DetectCuda()
    {
        // Check for CUDA installation
        return false;
    }

    private bool DetectVulkan()
    {
        // Check for Vulkan support
        return false;
    }
#endif
}
