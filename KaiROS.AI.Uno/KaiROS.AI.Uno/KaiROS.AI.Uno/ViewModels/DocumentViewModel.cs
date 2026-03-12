using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KaiROS.AI.Uno.Models;
using KaiROS.AI.Uno.Services;
using System.Collections.ObjectModel;

namespace KaiROS.AI.Uno.ViewModels;

public partial class DocumentViewModel : ViewModelBase
{
    private readonly IDocumentService _documentService;
    private readonly IRaasService _raasService;

    [ObservableProperty]
    private ObservableCollection<Document> _documents = [];

    [ObservableProperty]
    private string _statusMessage = "No documents loaded";

    public ObservableCollection<RaasConfiguration> RaasConfigurations => _raasService.Configurations;

    [ObservableProperty]
    private string _newServiceName = "New Service";

    [ObservableProperty]
    private string _newServiceDescription = "";

    [ObservableProperty]
    private int _newServicePort = 5001;

    [ObservableProperty]
    private string _newServiceSystemPrompt = "You are a helpful AI assistant.";

    [ObservableProperty]
    private RaasConfiguration? _selectedConfiguration;

    [ObservableProperty]
    private bool _isCreatingService;

    partial void OnSelectedConfigurationChanged(RaasConfiguration? value)
    {
        if (value != null) IsCreatingService = false;
    }

    public DocumentViewModel(IDocumentService documentService, IRaasService raasService)
    {
        _documentService = documentService;
        _raasService = raasService;
    }

    [RelayCommand]
    private void StartCreatingService()
    {
        SelectedConfiguration = null;
        IsCreatingService = true;
    }

    [RelayCommand]
    private void RemoveDocument(Document document)
    {
        if (document == null) return;
        _documentService.RemoveDocument(document.Id);
        Documents.Remove(document);
        StatusMessage = Documents.Count > 0 ? $"{Documents.Count} document(s) loaded" : "No documents loaded";
    }

    [RelayCommand]
    private void ClearAll()
    {
        _documentService.ClearAllDocuments();
        Documents.Clear();
        StatusMessage = "No documents loaded";
    }

    [RelayCommand]
    private async Task CreateService()
    {
        if (string.IsNullOrWhiteSpace(NewServiceName))
            return;

        var config = new RaasConfiguration
        {
            Name = NewServiceName,
            Description = NewServiceDescription,
            Port = NewServicePort,
            SystemPrompt = NewServiceSystemPrompt
        };

        await _raasService.CreateConfigurationAsync(config);

        NewServiceName = "New Service";
        NewServiceDescription = "";
        NewServicePort++;
        NewServiceSystemPrompt = "You are a helpful AI assistant.";

        IsCreatingService = false;
    }

    [RelayCommand]
    private async Task DeleteService(RaasConfiguration config)
    {
        if (config != null)
        {
            await _raasService.DeleteConfigurationAsync(config.Id);
        }
    }

    [RelayCommand]
    private async Task StartService(RaasConfiguration config)
    {
        try
        {
            await _raasService.StartServiceAsync(config.Id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to start service: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task StopService(RaasConfiguration config)
    {
        await _raasService.StopServiceAsync(config.Id);
    }

    [RelayCommand]
    private async Task RemoveSourceFromService(RagSource source)
    {
        if (SelectedConfiguration != null && source != null)
        {
            await _raasService.RemoveSourceAsync(SelectedConfiguration.Id, source);
        }
    }

    public override async Task InitializeAsync()
    {
        foreach (var doc in _documentService.LoadedDocuments)
        {
            if (!Documents.Any(d => d.Id == doc.Id)) Documents.Add(doc);
        }

        await _raasService.InitializeAsync();

        StatusMessage = Documents.Count > 0
            ? $"{Documents.Count} document(s) loaded"
            : "No documents loaded. Upload documents to chat with them.";
    }
}
