using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KaiROS.AI.WinUI;
using KaiROS.AI.WinUI.Models;
using KaiROS.AI.WinUI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Collections.ObjectModel;

namespace KaiROS.AI.WinUI.ViewModels;

public partial class DocumentViewModel : ViewModelBase
{
    private readonly IDocumentService _documentService;
    private readonly IRaasService _raasService;
    private readonly DispatcherQueue _dispatcherQueue;
    
    // --- Global Documents (Existing) ---
    [ObservableProperty]
    public partial ObservableCollection<Document> Documents { get; set; } = [];
    
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "No documents loaded";
    
    // --- RaaS Management (New) ---
    public ObservableCollection<RaasConfiguration> RaasConfigurations => _raasService.Configurations;

    [ObservableProperty]
    public partial string NewServiceName { get; set; } = "New Service";

    [ObservableProperty]
    public partial string NewServiceDescription { get; set; } = "";

    [ObservableProperty]
    public partial int NewServicePort { get; set; } = 5001;

    [ObservableProperty]
    public partial string NewServiceSystemPrompt { get; set; } = "You are a helpful AI assistant.";

    [ObservableProperty]
    public partial RaasConfiguration? SelectedConfiguration { get; set; }
    
    [ObservableProperty]
    public partial bool IsCreatingService { get; set; }
    
    partial void OnSelectedConfigurationChanged(RaasConfiguration? value)
    {
        if (value != null) IsCreatingService = false;
    }

    public DocumentViewModel(IDocumentService documentService, IRaasService raasService)
    {
        _documentService = documentService;
        _raasService = raasService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }
    
    // --- Global Document Commands ---

    [RelayCommand]
    private void StartCreatingService()
    {
        SelectedConfiguration = null;
        IsCreatingService = true;
        
        // Auto-calculate next available port
        var usedPorts = RaasConfigurations.Select(c => c.Port).ToHashSet();
        var nextPort = 5001;
        while (usedPorts.Contains(nextPort)) nextPort++;
        NewServicePort = nextPort;
    }

    [RelayCommand]
    private async Task LoadDocument()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add(".md");
        picker.FileTypeFilter.Add(".docx");
        picker.FileTypeFilter.Add(".pdf");
        picker.FileTypeFilter.Add(".csv");
        picker.FileTypeFilter.Add(".json");
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        var mainWindow = KaiROS.AI.WinUI.App.Current.Services.GetRequiredService<MainWindow>();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(mainWindow));

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            IsLoading = true;
            StatusMessage = "Loading document...";
            
            try
            {
                var doc = await _documentService.LoadDocumentAsync(file.Path);
                Documents.Add(doc);
                StatusMessage = $"Loaded: {doc.FileName} ({doc.Chunks.Count} chunks)";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
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

    // --- RaaS Commands ---

    [RelayCommand]
    private async Task CreateService()
    {
        if (string.IsNullOrWhiteSpace(NewServiceName))
        {
            return;
        }

        // Validate port uniqueness
        if (RaasConfigurations.Any(c => c.Port == NewServicePort))
        {
            var mainWindow = KaiROS.AI.WinUI.App.Current.Services.GetRequiredService<KaiROS.AI.WinUI.MainWindow>();
            var dlg = new ContentDialog
            {
                Title = "Port Conflict",
                Content = $"Port {NewServicePort} is already used by another service. Please choose a different port.",
                CloseButtonText = "OK",
                XamlRoot = mainWindow.Content.XamlRoot
            };
            await dlg.ShowAsync();
            return;
        }

        var config = new RaasConfiguration
        {
            Name = NewServiceName,
            Description = NewServiceDescription,
            Port = NewServicePort,
            SystemPrompt = NewServiceSystemPrompt
        };

        try
        {
            await _raasService.CreateConfigurationAsync(config);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[KaiROS] Failed to create service: {ex.Message}");
            return;
        }
        
        // Reset form
        NewServiceName = "New Service";
        NewServiceDescription = "";
        NewServiceSystemPrompt = "You are a helpful AI assistant.";
        
        IsCreatingService = false;
        
        // Auto-select the newly created service so user sees the details + Add Source
        SelectedConfiguration = config;
    }

    [RelayCommand]
    private async Task DeleteService(RaasConfiguration config)
    {
        var mainWindow = KaiROS.AI.WinUI.App.Current.Services.GetRequiredService<KaiROS.AI.WinUI.MainWindow>();
        var dlg = new ContentDialog
        {
            Title = "Confirm Delete",
            Content = $"Are you sure you want to delete '{config.Name}'?",
            PrimaryButtonText = "Yes",
            CloseButtonText = "No",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = mainWindow.Content.XamlRoot
        };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
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
    private void OpenServiceUrl(RaasConfiguration config)
    {
        if (config == null || !config.IsRunning) return;
        
        try
        {
            var url = $"http://localhost:{config.Port}/";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open browser: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task AddFileSource(RaasConfiguration config)
    {
        var mainWindow = KaiROS.AI.WinUI.App.Current.Services.GetRequiredService<KaiROS.AI.WinUI.MainWindow>();
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(mainWindow));
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add(".md");
        picker.FileTypeFilter.Add(".docx");
        picker.FileTypeFilter.Add(".pdf");
        picker.FileTypeFilter.Add(".csv");
        picker.FileTypeFilter.Add(".json");
        picker.FileTypeFilter.Add("*");

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            await _raasService.AddSourceAsync(config.Id, file.Path);
        }
    }

    [RelayCommand]
    private async Task AddWebSource(RaasConfiguration config)
    {
        var url = await ShowInputDialogAsync("Enter Website URL:", "Add Web Source", "https://");
        if (!string.IsNullOrWhiteSpace(url))
        {
            await _raasService.AddWebSourceAsync(config.Id, url);
        }
    }

    private string ShowInputDialog(string text, string title, string defaultText = "")
    {
        // Synchronous stub - use ShowInputDialogAsync instead
        return defaultText;
    }

    private async Task<string> ShowInputDialogAsync(string prompt, string title, string defaultText = "")
    {
        var mainWindow = KaiROS.AI.WinUI.App.Current.Services.GetRequiredService<KaiROS.AI.WinUI.MainWindow>();
        var tb = new TextBox { Text = defaultText };
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(new TextBlock { Text = prompt });
        sp.Children.Add(tb);
        var dlg = new ContentDialog
        {
            Title = title,
            Content = sp,
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = mainWindow.Content.XamlRoot
        };
        var result = await dlg.ShowAsync();
        return result == ContentDialogResult.Primary ? tb.Text.Trim() : string.Empty;
    }
    
    [RelayCommand]
    private async Task RemoveSourceFromService(RagSource source)
    {
        // We rely on SelectedConfiguration being the context.
        if (SelectedConfiguration != null && source != null)
        {
             await _raasService.RemoveSourceAsync(SelectedConfiguration.Id, source);
        }
    }

    public override async Task InitializeAsync()
    {
        // Load global documents — dispatch to UI thread because
        // InitializeAsync may resume on a background continuation.
        var docsToAdd = _documentService.LoadedDocuments
            .Where(doc => !Documents.Any(d => d.Id == doc.Id))
            .ToList();

        if (docsToAdd.Count > 0)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                foreach (var doc in docsToAdd) Documents.Add(doc);
            });
        }
        
        // Initialize RaaS service
        await _raasService.InitializeAsync();
        
        StatusMessage = Documents.Count > 0 
            ? $"{Documents.Count} document(s) loaded" 
            : "No documents loaded. Upload documents to chat with them.";
    }
}
