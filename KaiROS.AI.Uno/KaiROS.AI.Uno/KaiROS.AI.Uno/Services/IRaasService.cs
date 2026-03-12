using KaiROS.AI.Uno.Models;
using System.Collections.ObjectModel;

namespace KaiROS.AI.Uno.Services;

public interface IRaasService
{
    ObservableCollection<RaasConfiguration> Configurations { get; }

    Task InitializeAsync();

    Task CreateConfigurationAsync(RaasConfiguration config);
    Task UpdateConfigurationAsync(RaasConfiguration config);
    Task DeleteConfigurationAsync(string id);

    Task AddSourceAsync(string configId, string filePath);
    Task AddWebSourceAsync(string configId, string url);
    Task RemoveSourceAsync(string configId, RagSource source);

    Task StartServiceAsync(string configId);
    Task StopServiceAsync(string configId);

    bool IsServiceRunning(string configId);
    object? GetServer(string configId); // Returns ApiServer on desktop platforms
}
