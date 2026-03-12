using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KaiROS.AI.Uno.Models;
using KaiROS.AI.Uno.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace KaiROS.AI.Uno.ViewModels;

public partial class ModelCatalogViewModel : ViewModelBase
{
    private readonly IModelManagerService _modelManager;
    private readonly IDatabaseService _databaseService;
    private readonly IHardwareDetectionService _hardwareService;
    private readonly Dictionary<string, CancellationTokenSource> _downloadCts = [];

    public event EventHandler? ModelActivated;

    public string SelectedBackendName => _hardwareService.GetRecommendedBackend().ToString();

    public async Task<string> GetSelectedBackendNameAsync()
    {
        var hw = await _hardwareService.DetectHardwareAsync();
        return hw.SelectedBackend.ToString();
    }

    [ObservableProperty]
    private ObservableCollection<ModelItemViewModel> _models = [];

    [ObservableProperty]
    private ObservableCollection<ModelItemViewModel> _filteredModels = [];

    [ObservableProperty]
    private ObservableCollection<ModelItemViewModel> _downloadedModels = [];

    [ObservableProperty]
    private ObservableCollection<OrganizationGroup> _groupedModels = [];

    [ObservableProperty]
    private string _selectedCategory = "all";

    [ObservableProperty]
    private bool _showRecommendedOnly;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedOrganization = "all";

    [ObservableProperty]
    private string _selectedFamily = "all";

    [ObservableProperty]
    private string _selectedVariant = "all";

    [ObservableProperty]
    private string _selectedVisionOption = "All";

    public ObservableCollection<string> Organizations { get; } = ["all"];
    public ObservableCollection<string> Families { get; } = ["all"];
    public ObservableCollection<string> Variants { get; } = ["all", "All", "CPU-Only", "GPU-Recommended"];
    public ObservableCollection<string> VisionOptions { get; } = ["All", "Vision Only", "Text Only"];

    public ModelCatalogViewModel(
        IModelManagerService modelManager,
        IDatabaseService databaseService,
        IHardwareDetectionService hardwareService)
    {
        _modelManager = modelManager;
        _databaseService = databaseService;
        _hardwareService = hardwareService;
    }

    public override async Task InitializeAsync()
    {
        IsLoading = true;

        try
        {
            Models.Clear();
            Organizations.Clear();
            Organizations.Add("all");
            Families.Clear();
            Families.Add("all");

            foreach (var model in _modelManager.Models)
            {
                var vm = new ModelItemViewModel(model, this);
                Models.Add(vm);

                if (!string.IsNullOrEmpty(model.Organization) && !Organizations.Contains(model.Organization))
                    Organizations.Add(model.Organization);
                if (!string.IsNullOrEmpty(model.Family) && !Families.Contains(model.Family))
                    Families.Add(model.Family);
            }

            ApplyFilters();
        }
        finally
        {
            IsLoading = false;
        }

        await Task.CompletedTask;
    }

    partial void OnSelectedCategoryChanged(string value) => ApplyFilters();
    partial void OnShowRecommendedOnlyChanged(bool value) => ApplyFilters();
    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedOrganizationChanged(string value) => ApplyFilters();
    partial void OnSelectedFamilyChanged(string value) => ApplyFilters();
    partial void OnSelectedVariantChanged(string value) => ApplyFilters();
    partial void OnSelectedVisionOptionChanged(string value) => ApplyFilters();

    private void ApplyFilters()
    {
        var filtered = Models.AsEnumerable();

        if (SelectedCategory != "all")
        {
            filtered = filtered.Where(m => m.Model.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedOrganization != "all")
        {
            filtered = filtered.Where(m => m.Model.Organization.Equals(SelectedOrganization, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedFamily != "all")
        {
            filtered = filtered.Where(m => m.Model.Family.Equals(SelectedFamily, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedVariant != "all")
        {
            filtered = filtered.Where(m => m.Model.Variant.Equals(SelectedVariant, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedVisionOption == "Vision Only")
        {
            filtered = filtered.Where(m => m.Model.IsVisionModel);
        }
        else if (SelectedVisionOption == "Text Only")
        {
            filtered = filtered.Where(m => !m.Model.IsVisionModel);
        }

        if (ShowRecommendedOnly)
        {
            filtered = filtered.Where(m => m.Model.IsRecommended);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(m =>
                m.Model.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                m.Model.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                m.Model.Organization.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                m.Model.Family.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        FilteredModels = new ObservableCollection<ModelItemViewModel>(filtered);

        DownloadedModels = new ObservableCollection<ModelItemViewModel>(
            filtered.Where(m => m.IsDownloaded).OrderByDescending(m => m.IsActive));

        var orgPriority = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Meta", 0 },
            { "Microsoft", 1 },
            { "Google", 2 }
        };

        var groups = filtered
            .GroupBy(m => m.Model.Organization)
            .OrderBy(g => orgPriority.TryGetValue(g.Key, out var priority) ? priority : 100)
            .ThenBy(g => g.Key)
            .Select(g => new OrganizationGroup(
                g.Key,
                g.First().Model.OrgLogoUrl,
                new ObservableCollection<ModelItemViewModel>(g.OrderBy(m => m.Model.SizeBytes))))
            .ToList();

        GroupedModels = new ObservableCollection<OrganizationGroup>(groups);
    }

    [RelayCommand]
    private void CollapseAll()
    {
        foreach (var group in GroupedModels)
        {
            group.IsExpanded = false;
        }
    }

    [RelayCommand]
    private void ExpandAll()
    {
        foreach (var group in GroupedModels)
        {
            group.IsExpanded = true;
        }
    }

    [RelayCommand]
    private void FilterByCategory(string category)
    {
        SelectedCategory = category;
    }

    [RelayCommand]
    private void ToggleRecommendedFilter()
    {
        ShowRecommendedOnly = !ShowRecommendedOnly;
    }

    public async Task DownloadModelAsync(ModelItemViewModel modelVm)
    {
        var model = modelVm.Model;
        var cts = new CancellationTokenSource();
        _downloadCts[model.Name] = cts;

        modelVm.IsDownloading = true;
        modelVm.IsPaused = false;

        try
        {
            var progress = new Progress<double>(p => modelVm.DownloadProgress = p);
            var success = await _modelManager.DownloadModelAsync(model, progress, cts.Token);

            if (success)
            {
                modelVm.DownloadProgress = 100;
                modelVm.ErrorMessage = null;
                modelVm.IsDownloaded = true;
                modelVm.IsDownloading = false;
            }
            else
            {
                modelVm.IsDownloading = false;
                modelVm.IsDownloaded = false;
                modelVm.ErrorMessage = model.LoadError ?? "Download failed. Please try again.";
            }
        }
        catch (OperationCanceledException)
        {
            modelVm.IsDownloading = false;
            modelVm.IsPaused = true;
        }
        catch (Exception ex)
        {
            modelVm.IsDownloading = false;
            modelVm.ErrorMessage = ex.Message;
        }
        finally
        {
            _downloadCts.Remove(model.Name);
        }
    }

    public async Task PauseDownloadAsync(ModelItemViewModel modelVm)
    {
        if (_downloadCts.TryGetValue(modelVm.Model.Name, out var cts))
        {
            cts.Cancel();
        }
        await _modelManager.PauseDownloadAsync(modelVm.Model);
        modelVm.IsPaused = true;
    }

    public async Task ResumeDownloadAsync(ModelItemViewModel modelVm)
    {
        modelVm.IsPaused = false;
        await DownloadModelAsync(modelVm);
    }

    public async Task SetActiveModelAsync(ModelItemViewModel modelVm)
    {
        modelVm.IsLoading = true;
        modelVm.LoadingProgress = 0;
        modelVm.ErrorMessage = null;

        Debug.WriteLine($"SetActiveModelAsync: Starting for model {modelVm.Model.Name}");
        Debug.WriteLine($"  IsDownloaded: {modelVm.Model.IsDownloaded}");
        Debug.WriteLine($"  LocalPath: {modelVm.Model.LocalPath ?? "null"}");
        Debug.WriteLine($"  IsNativeBackendAvailable: {_modelManager.IsNativeBackendAvailable}");

        try
        {
            foreach (var m in Models)
            {
                m.IsActive = false;
            }

            var progress = new Progress<double>(p => modelVm.LoadingProgress = p);
            var success = await _modelManager.SetActiveModelAsync(modelVm.Model, progress);
            
            Debug.WriteLine($"SetActiveModelAsync: Result = {success}");
            Debug.WriteLine($"  ActiveModel after: {_modelManager.ActiveModel?.Name ?? "null"}");

            modelVm.IsActive = success;

            if (success)
            {
                ModelActivated?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                var error = modelVm.Model.LoadError;
                modelVm.ErrorMessage = !string.IsNullOrEmpty(error)
                    ? $"Failed to load: {error}"
                    : "Failed to load model. The file may be corrupted - try deleting and re-downloading.";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SetActiveModelAsync: Exception = {ex}");
            modelVm.ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            modelVm.IsLoading = false;
        }
    }

    public async Task DeleteModelAsync(ModelItemViewModel modelVm)
    {
        if (modelVm.Model.IsCustomModel)
        {
            await _databaseService.DeleteCustomModelAsync(modelVm.Model.CustomModelId);
            Models.Remove(modelVm);
            FilteredModels.Remove(modelVm);
        }

        await _modelManager.DeleteModelAsync(modelVm.Model);
        modelVm.IsDownloaded = false;
        modelVm.IsActive = false;
        modelVm.DownloadProgress = 0;
    }
}

public partial class ModelItemViewModel : ObservableObject
{
    private readonly ModelCatalogViewModel _parent;

    public LLMModelInfo Model { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLoadButton))]
    private bool _isDownloaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLoadButton))]
    private bool _isDownloading;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoadingText))]
    private bool _isLoading;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string? _errorMessage;

    public bool ShowLoadButton => IsDownloaded && !IsDownloading;

    public string LoadingText
    {
        get
        {
            if (!IsLoading) return "";
            var backend = _parent.SelectedBackendName;
            if (LoadingProgress > 0 && LoadingProgress < 100)
                return $"Loading on {backend}... {LoadingProgress:0}%";
            else
                return $"Loading on {backend}...";
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoadingText))]
    private double _loadingProgress;

    public ModelItemViewModel(LLMModelInfo model, ModelCatalogViewModel parent)
    {
        Model = model;
        _parent = parent;
        _isDownloaded = model.IsDownloaded;
        _isActive = model.IsActive;
        _downloadProgress = model.DownloadProgress;
    }

    [RelayCommand]
    private async Task Download() => await _parent.DownloadModelAsync(this);

    [RelayCommand]
    private async Task Pause() => await _parent.PauseDownloadAsync(this);

    [RelayCommand]
    private async Task Resume() => await _parent.ResumeDownloadAsync(this);

    [RelayCommand]
    private async Task SetActive() => await _parent.SetActiveModelAsync(this);

    [RelayCommand]
    private async Task Delete() => await _parent.DeleteModelAsync(this);
}

public partial class OrganizationGroup : ObservableObject
{
    public string OrganizationName { get; }
    public string LogoUrl { get; }
    public ObservableCollection<ModelItemViewModel> Models { get; }
    public int ModelCount => Models.Count;
    public int DownloadedCount => Models.Count(m => m.IsDownloaded);

    [ObservableProperty]
    private bool _isExpanded = true;

    public OrganizationGroup(string name, string logoUrl, ObservableCollection<ModelItemViewModel> models)
    {
        OrganizationName = name;
        LogoUrl = logoUrl;
        Models = models;
    }
}
