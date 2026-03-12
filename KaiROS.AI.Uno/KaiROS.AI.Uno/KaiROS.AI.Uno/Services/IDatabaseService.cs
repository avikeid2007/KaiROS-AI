using KaiROS.AI.Uno.Models;

namespace KaiROS.AI.Uno.Services;

public interface IDatabaseService
{
    Task InitializeAsync();
    // Models
    Task<List<CustomModelEntity>> GetCustomModelsAsync();
    Task AddCustomModelAsync(CustomModelEntity model);
    Task DeleteCustomModelAsync(int id);

    // RaaS
    Task<List<RaasConfiguration>> GetRaasConfigsAsync();
    Task AddRaasConfigAsync(RaasConfiguration config);
    Task DeleteRaasConfigAsync(string id);
    Task AddRagSourceAsync(string configId, RagSource source);
    Task DeleteRagSourceAsync(string sourceId);
}
