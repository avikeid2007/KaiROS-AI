using KaiROS.AI.WinUI.Models;

namespace KaiROS.AI.WinUI.Services;

public interface IDownloadService
{
    Task<bool> DownloadFileAsync(string url, string destinationPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    Task PauseDownloadAsync(string modelName);
    Task ResumeDownloadAsync(string modelName);
    Task<bool> VerifyFileIntegrityAsync(string filePath, long expectedSize);
    bool HasPartialDownload(string modelName);
}
