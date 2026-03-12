using KaiROS.AI.Uno.Models;
using System.Collections.ObjectModel;

namespace KaiROS.AI.Uno.Services;

public class RaasService : IRaasService
{
    private readonly IDatabaseService _databaseService;
    private readonly IChatService _chatService;
    private readonly ObservableCollection<RaasConfiguration> _configurations = [];

    public ObservableCollection<RaasConfiguration> Configurations => _configurations;

    public RaasService(IDatabaseService databaseService, IChatService chatService)
    {
        _databaseService = databaseService;
        _chatService = chatService;
    }

    public async Task InitializeAsync()
    {
        var configs = await _databaseService.GetRaasConfigsAsync();
        _configurations.Clear();
        foreach (var config in configs)
        {
            _configurations.Add(config);
        }
    }

    public async Task CreateConfigurationAsync(RaasConfiguration config)
    {
        await _databaseService.AddRaasConfigAsync(config);
        _configurations.Add(config);
    }

    public async Task UpdateConfigurationAsync(RaasConfiguration config)
    {
        // Would implement update logic
        await Task.CompletedTask;
    }

    public async Task DeleteConfigurationAsync(string id)
    {
        await _databaseService.DeleteRaasConfigAsync(id);
        var config = _configurations.FirstOrDefault(c => c.Id == id);
        if (config != null)
        {
            _configurations.Remove(config);
        }
    }

    public async Task AddSourceAsync(string configId, string filePath)
    {
        var config = _configurations.FirstOrDefault(c => c.Id == configId);
        if (config != null)
        {
            var source = new RagSource
            {
                Type = RagSourceType.File,
                Name = Path.GetFileName(filePath),
                Value = filePath
            };
            config.Sources.Add(source);
            await _databaseService.AddRagSourceAsync(configId, source);
        }
    }

    public async Task AddWebSourceAsync(string configId, string url)
    {
        var config = _configurations.FirstOrDefault(c => c.Id == configId);
        if (config != null)
        {
            var source = new RagSource
            {
                Type = RagSourceType.Web,
                Name = url,
                Value = url
            };
            config.Sources.Add(source);
            await _databaseService.AddRagSourceAsync(configId, source);
        }
    }

    public async Task RemoveSourceAsync(string configId, RagSource source)
    {
        var config = _configurations.FirstOrDefault(c => c.Id == configId);
        if (config != null)
        {
            config.Sources.Remove(source);
            await _databaseService.DeleteRagSourceAsync(source.Id);
        }
    }

    public Task StartServiceAsync(string configId)
    {
        var config = _configurations.FirstOrDefault(c => c.Id == configId);
        if (config != null)
        {
            config.IsRunning = true;
            // Would start HTTP server
        }
        return Task.CompletedTask;
    }

    public Task StopServiceAsync(string configId)
    {
        var config = _configurations.FirstOrDefault(c => c.Id == configId);
        if (config != null)
        {
            config.IsRunning = false;
            // Would stop HTTP server
        }
        return Task.CompletedTask;
    }

    public bool IsServiceRunning(string configId)
    {
        var config = _configurations.FirstOrDefault(c => c.Id == configId);
        return config?.IsRunning ?? false;
    }

    public object? GetServer(string configId)
    {
        // Would return the HTTP server instance
        return null;
    }
}
