using KaiROS.AI.WinUI.Models;

namespace KaiROS.AI.WinUI.Services;

public interface IHardwareDetectionService
{
    Task<HardwareInfo> DetectHardwareAsync();
    ExecutionBackend GetRecommendedBackend();
    bool IsBackendAvailable(ExecutionBackend backend);
    void ClearCache();
    void SetSelectedBackend(ExecutionBackend backend);
}
