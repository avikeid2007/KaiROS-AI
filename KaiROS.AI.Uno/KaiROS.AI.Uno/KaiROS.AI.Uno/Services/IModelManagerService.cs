using KaiROS.AI.Uno.Models;

namespace KaiROS.AI.Uno.Services;

public interface IModelManagerService
{
    IReadOnlyList<LLMModelInfo> Models { get; }
    LLMModelInfo? ActiveModel { get; }
    string ModelsDirectory { get; }
    bool IsVisionModelLoaded { get; }
    bool IsNativeBackendAvailable { get; }

    Task InitializeAsync();
    Task<bool> DownloadModelAsync(LLMModelInfo model, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    Task PauseDownloadAsync(LLMModelInfo model);
    Task ResumeDownloadAsync(LLMModelInfo model);
    Task<bool> DeleteModelAsync(LLMModelInfo model);
    Task<bool> SetActiveModelAsync(LLMModelInfo model, IProgress<double>? progress = null);
    Task UnloadModelAsync();
    Task<bool> VerifyModelAsync(LLMModelInfo model);
    void SetModelsDirectory(string path);

    event EventHandler<LLMModelInfo>? ModelDownloadStarted;
    event EventHandler<LLMModelInfo>? ModelDownloadCompleted;
    event EventHandler<LLMModelInfo>? ModelLoaded;
    event EventHandler? ModelUnloaded;
    event EventHandler<double>? ModelLoadProgress;
}
