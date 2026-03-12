using KaiROS.AI.Uno.Models;

namespace KaiROS.AI.Uno.Services;

public interface IHardwareDetectionService
{
    Task<HardwareInfo> DetectHardwareAsync();
    ExecutionBackend GetRecommendedBackend();
    bool IsBackendAvailable(ExecutionBackend backend);
    void ClearCache();
    void SetSelectedBackend(ExecutionBackend backend);
}
