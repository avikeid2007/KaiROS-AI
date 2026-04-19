using KaiROS.AI.WinUI.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace KaiROS.AI.WinUI.Services;

public class RaasService : IRaasService
{
    private readonly IDatabaseService _databaseService;
    private readonly IChatService _chatService;
    private readonly IEnumerable<IRagSourceProvider> _sourceProviders;
    
    // In-memory store of running servers: ConfigId -> ServerInstance
    private readonly Dictionary<string, ApiServer> _runningServers = []; 
    
    private readonly string _raasRootStoragePath;

    public ObservableCollection<RaasConfiguration> Configurations { get; } = [];

    public RaasService(IDatabaseService databaseService, IChatService chatService, IEnumerable<IRagSourceProvider> sourceProviders)
    {
        _databaseService = databaseService;
        _chatService = chatService;
        _sourceProviders = sourceProviders;
        
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _raasRootStoragePath = Path.Combine(appData, "KaiROS.AI", "RaaS");
        Directory.CreateDirectory(_raasRootStoragePath);
    }

    public async Task InitializeAsync()
    {
        var configs = await _databaseService.GetRaasConfigsAsync();
        Configurations.Clear();
        foreach (var config in configs)
        {
            config.IsRunning = false;
            Configurations.Add(config);
        }
    }

    public async Task CreateConfigurationAsync(RaasConfiguration config)
    {
        // 1. Create managed directory
        var configDir = Path.Combine(_raasRootStoragePath, config.Id);
        Directory.CreateDirectory(configDir);

        // 2. Add to DB
        await _databaseService.AddRaasConfigAsync(config);
        Configurations.Add(config);
    }

    public async Task UpdateConfigurationAsync(RaasConfiguration config)
    {
        await _databaseService.UpdateRaasConfigAsync(config);
    }

    public async Task DeleteConfigurationAsync(string id)
    {
        var config = Configurations.FirstOrDefault(c => c.Id == id);
        if (config != null)
        {
            if (IsServiceRunning(id)) await StopServiceAsync(id);

            // 1. Delete from DB (Cascades sources)
            await _databaseService.DeleteRaasConfigAsync(id);
            
            // 2. Delete managed directory
            var configDir = Path.Combine(_raasRootStoragePath, id);
            if (Directory.Exists(configDir))
            {
                try { Directory.Delete(configDir, true); } catch { }
            }

            Configurations.Remove(config);
        }
    }
    
    // NEW: Source Management Methods
    public async Task AddSourceAsync(string configId, string filePath)
    {
        var config = Configurations.FirstOrDefault(c => c.Id == configId);
        if (config == null) return;

        if (!File.Exists(filePath)) return;

        // 1. Generate unique ID for source
        var sourceId = Guid.NewGuid().ToString();
        var originalName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath);
        
        // 2. Copy file to managed store
        var configDir = Path.Combine(_raasRootStoragePath, config.Id);
        Directory.CreateDirectory(configDir); // Ensure exists
        
        var targetFileName = $"{sourceId}{extension}";
        var targetPath = Path.Combine(configDir, targetFileName);
        
        File.Copy(filePath, targetPath, overwrite: true);
        
        // 3. Create Source Object
        var source = new RagSource
        {
            Id = sourceId,
            Name = originalName, // Display Name
            Value = targetPath,  // Managed Path
            Type = RagSourceType.File, // Assuming file for now
            IsEnabled = true
        };
        
        // 4. DB Insert
        await _databaseService.AddRagSourceAsync(configId, source);
        
        // 5. Update UI
        config.Sources.Add(source);
    }
    
    public async Task AddWebSourceAsync(string configId, string url)
    {
        var config = Configurations.FirstOrDefault(c => c.Id == configId);
        if (config == null) return;

        if (string.IsNullOrWhiteSpace(url)) return;

        // 1. Create Source Object
        var sourceId = Guid.NewGuid().ToString();
        var source = new RagSource
        {
            Id = sourceId,
            Name = url, // Display Name same as URL for now, user can rename later if we implement rename
            Value = url,
            Type = RagSourceType.Web,
            IsEnabled = true
        };
        
        // 2. DB Insert
        await _databaseService.AddRagSourceAsync(configId, source);
        
        // 3. Update UI
        config.Sources.Add(source);
    }
    
    public async Task RemoveSourceAsync(string configId, RagSource source)
    {
        var config = Configurations.FirstOrDefault(c => c.Id == configId);
        if (config == null) return;
        
        // 1. Delete from DB
        await _databaseService.DeleteRagSourceAsync(source.Id);
        
        // 2. Delete physical file
        if (File.Exists(source.Value))
        {
            try { File.Delete(source.Value); } catch { }
        }
        
        // 3. Update UI
        config.Sources.Remove(source);
    }

    // ... (Start/Stop methods mostly same, just slight cleanup) ...
    
    public async Task StartServiceAsync(string configId)
    {
        var config = Configurations.FirstOrDefault(c => c.Id == configId);
        if (config == null || IsServiceRunning(configId)) return;

        try
        {
            var ragEngine = new RagEngine(_sourceProviders);
            foreach (var source in config.Sources)
            {
                // Verify file still exists? 
                if (source.IsEnabled) await ragEngine.AddSourceAsync(source);
            }

            var server = new ApiServer(config, _chatService, ragEngine);
            await server.StartAsync();
            
            _runningServers[configId] = server;
            config.IsRunning = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to start RaaS service {config.Name}: {ex.Message}");
            throw; 
        }
    }

    public async Task StopServiceAsync(string configId)
    {
        var config = Configurations.FirstOrDefault(c => c.Id == configId);
        
        if (_runningServers.ContainsKey(configId))
        {
            var server = _runningServers[configId];
            await server.StopAsync();
            _runningServers.Remove(configId);
        }
        
        if (config != null) config.IsRunning = false;
    }

    public bool IsServiceRunning(string configId) => _runningServers.ContainsKey(configId);
    
    public ApiServer? GetServer(string configId)
    {
        _runningServers.TryGetValue(configId, out var server);
        return server;
    }
}
