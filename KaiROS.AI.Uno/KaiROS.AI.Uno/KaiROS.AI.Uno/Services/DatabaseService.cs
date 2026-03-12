using KaiROS.AI.Uno.Models;
using Microsoft.Data.Sqlite;
using System.Collections.ObjectModel;

namespace KaiROS.AI.Uno.Services;

public class DatabaseService : IDatabaseService
{
    private const string DbFileName = "kairos.db";
    private string _dbPath = string.Empty;
    private bool _isInitialized;

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

#if DESKTOP
        _dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KaiROS", DbFileName);

        var directory = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        // Create tables
        var createTablesCmd = connection.CreateCommand();
        createTablesCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS CustomModels (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                Description TEXT,
                FilePath TEXT NOT NULL,
                DownloadUrl TEXT,
                SizeBytes INTEGER,
                AddedDate TEXT,
                IsLocal INTEGER,
                IsVisionModel INTEGER,
                MmProjFilePath TEXT,
                MmProjDownloadUrl TEXT
            );

            CREATE TABLE IF NOT EXISTS RaasConfigurations (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Description TEXT,
                Port INTEGER,
                SystemPrompt TEXT
            );

            CREATE TABLE IF NOT EXISTS RagSources (
                Id TEXT PRIMARY KEY,
                ConfigId TEXT,
                Type INTEGER,
                Name TEXT,
                Value TEXT,
                IsEnabled INTEGER,
                FOREIGN KEY(ConfigId) REFERENCES RaasConfigurations(Id)
            );
        ";
        await createTablesCmd.ExecuteNonQueryAsync();
#else
        // WASM - use in-memory storage or IndexedDB via JS interop
        // For now, just mark as initialized
#endif
        _isInitialized = true;
    }

    public async Task<List<CustomModelEntity>> GetCustomModelsAsync()
    {
        var models = new List<CustomModelEntity>();

#if DESKTOP
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM CustomModels";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            models.Add(new CustomModelEntity
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                DisplayName = reader.GetString(2),
                Description = reader.IsDBNull(3) ? "" : reader.GetString(3),
                FilePath = reader.GetString(4),
                DownloadUrl = reader.IsDBNull(5) ? "" : reader.GetString(5),
                SizeBytes = reader.IsDBNull(6) ? 0 : reader.GetInt64(6),
                AddedDate = reader.IsDBNull(7) ? DateTime.UtcNow : DateTime.Parse(reader.GetString(7)),
                IsLocal = reader.IsDBNull(8) || reader.GetInt32(8) == 1,
                IsVisionModel = !reader.IsDBNull(9) && reader.GetInt32(9) == 1,
                MmProjFilePath = reader.IsDBNull(10) ? "" : reader.GetString(10),
                MmProjDownloadUrl = reader.IsDBNull(11) ? "" : reader.GetString(11)
            });
        }
#endif

        return models;
    }

    public async Task AddCustomModelAsync(CustomModelEntity model)
    {
#if DESKTOP
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO CustomModels (Name, DisplayName, Description, FilePath, DownloadUrl, SizeBytes, AddedDate, IsLocal, IsVisionModel, MmProjFilePath, MmProjDownloadUrl)
            VALUES ($name, $displayName, $description, $filePath, $downloadUrl, $sizeBytes, $addedDate, $isLocal, $isVisionModel, $mmProjFilePath, $mmProjDownloadUrl)";

        cmd.Parameters.AddWithValue("$name", model.Name);
        cmd.Parameters.AddWithValue("$displayName", model.DisplayName);
        cmd.Parameters.AddWithValue("$description", model.Description ?? "");
        cmd.Parameters.AddWithValue("$filePath", model.FilePath);
        cmd.Parameters.AddWithValue("$downloadUrl", model.DownloadUrl ?? "");
        cmd.Parameters.AddWithValue("$sizeBytes", model.SizeBytes);
        cmd.Parameters.AddWithValue("$addedDate", model.AddedDate.ToString("O"));
        cmd.Parameters.AddWithValue("$isLocal", model.IsLocal ? 1 : 0);
        cmd.Parameters.AddWithValue("$isVisionModel", model.IsVisionModel ? 1 : 0);
        cmd.Parameters.AddWithValue("$mmProjFilePath", model.MmProjFilePath ?? "");
        cmd.Parameters.AddWithValue("$mmProjDownloadUrl", model.MmProjDownloadUrl ?? "");

        await cmd.ExecuteNonQueryAsync();
#endif
    }

    public async Task DeleteCustomModelAsync(int id)
    {
#if DESKTOP
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM CustomModels WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);

        await cmd.ExecuteNonQueryAsync();
#endif
    }

    public async Task<List<RaasConfiguration>> GetRaasConfigsAsync()
    {
        var configs = new List<RaasConfiguration>();

#if DESKTOP
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM RaasConfigurations";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            configs.Add(new RaasConfiguration
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Port = reader.GetInt32(3),
                SystemPrompt = reader.IsDBNull(4) ? "" : reader.GetString(4)
            });
        }
#endif

        return configs;
    }

    public async Task AddRaasConfigAsync(RaasConfiguration config)
    {
#if DESKTOP
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO RaasConfigurations (Id, Name, Description, Port, SystemPrompt)
            VALUES ($id, $name, $description, $port, $systemPrompt)";

        cmd.Parameters.AddWithValue("$id", config.Id);
        cmd.Parameters.AddWithValue("$name", config.Name);
        cmd.Parameters.AddWithValue("$description", config.Description ?? "");
        cmd.Parameters.AddWithValue("$port", config.Port);
        cmd.Parameters.AddWithValue("$systemPrompt", config.SystemPrompt ?? "");

        await cmd.ExecuteNonQueryAsync();
#endif
    }

    public async Task DeleteRaasConfigAsync(string id)
    {
#if DESKTOP
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM RaasConfigurations WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);

        await cmd.ExecuteNonQueryAsync();
#endif
    }

    public async Task AddRagSourceAsync(string configId, RagSource source)
    {
#if DESKTOP
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO RagSources (Id, ConfigId, Type, Name, Value, IsEnabled)
            VALUES ($id, $configId, $type, $name, $value, $isEnabled)";

        cmd.Parameters.AddWithValue("$id", source.Id);
        cmd.Parameters.AddWithValue("$configId", configId);
        cmd.Parameters.AddWithValue("$type", (int)source.Type);
        cmd.Parameters.AddWithValue("$name", source.Name);
        cmd.Parameters.AddWithValue("$value", source.Value);
        cmd.Parameters.AddWithValue("$isEnabled", source.IsEnabled ? 1 : 0);

        await cmd.ExecuteNonQueryAsync();
#endif
    }

    public async Task DeleteRagSourceAsync(string sourceId)
    {
#if DESKTOP
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM RagSources WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", sourceId);

        await cmd.ExecuteNonQueryAsync();
#endif
    }
}
