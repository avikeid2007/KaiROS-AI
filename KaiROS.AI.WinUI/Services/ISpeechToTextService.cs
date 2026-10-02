using System;
using System.Threading;
using System.Threading.Tasks;

namespace KaiROS.AI.WinUI.Services;

public interface ISpeechToTextService
{
    bool IsListening { get; }
    bool IsAvailable { get; }
    bool IsDownloadingModel { get; }
    double DownloadProgress { get; }
    event EventHandler<string> TranscriptionCompleted;
    event EventHandler<string> PartialTranscription;
    event EventHandler<float> AudioLevelChanged;
    event EventHandler<double> DownloadProgressChanged;
    Task StartListeningAsync(CancellationToken ct = default);
    Task StopListeningAsync();
    Task<string> TranscribeFileAsync(string audioFilePath);
    Task<bool> EnsureModelDownloadedAsync(string modelSize, CancellationToken ct = default);
}
