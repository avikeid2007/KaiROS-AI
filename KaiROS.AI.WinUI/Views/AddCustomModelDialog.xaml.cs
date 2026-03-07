using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using KaiROS.AI.WinUI.Models;
using Windows.Storage.Pickers;

namespace KaiROS.AI.WinUI.Views;

public partial class AddCustomModelDialog : ContentDialog
{
    public CustomModelEntity? Result { get; private set; }

    public AddCustomModelDialog()
    {
        InitializeComponent();
    }

    // â”€â”€â”€ Source type toggle â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void SourceType_Checked(object sender, RoutedEventArgs e)
    {
        if (LocalFilePanel == null) return;

        bool isLocal = LocalFileRadio.IsChecked == true;
        LocalFilePanel.Visibility = isLocal ? Visibility.Visible : Visibility.Collapsed;
        DownloadUrlPanel.Visibility = isLocal ? Visibility.Collapsed : Visibility.Visible;

        // Sync vision mm-proj panels
        if (IsVisionModelCheck?.IsChecked == true)
        {
            LocalMmProjPanel.Visibility = isLocal ? Visibility.Visible : Visibility.Collapsed;
            RemoteMmProjPanel.Visibility = isLocal ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private void IsVisionModelCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (VisionModelPanel == null) return;

        bool isVision = IsVisionModelCheck.IsChecked == true;
        VisionModelPanel.Visibility = isVision ? Visibility.Visible : Visibility.Collapsed;

        if (isVision)
        {
            bool isLocal = LocalFileRadio.IsChecked == true;
            LocalMmProjPanel.Visibility = isLocal ? Visibility.Visible : Visibility.Collapsed;
            RemoteMmProjPanel.Visibility = !isLocal ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            MmProjFilePathBox.Text = string.Empty;
            MmProjDownloadUrlBox.Text = string.Empty;
        }
    }

    // â”€â”€â”€ File pickers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private async void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializePicker(picker);
        picker.FileTypeFilter.Add(".gguf");
        picker.FileTypeFilter.Add("*");
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            FilePathBox.Text = file.Path;
            if (string.IsNullOrWhiteSpace(DisplayNameBox.Text))
                DisplayNameBox.Text = Path.GetFileNameWithoutExtension(file.Path);
        }
    }

    private async void BrowseMmProjFile_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializePicker(picker);
        picker.FileTypeFilter.Add(".gguf");
        picker.FileTypeFilter.Add("*");
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        var file = await picker.PickSingleFileAsync();
        if (file != null)
            MmProjFilePathBox.Text = file.Path;
    }

    private static void InitializePicker(FileOpenPicker picker)
    {
        // WinUI 3: Must associate picker with Window handle
        var mainWindow = KaiROS.AI.WinUI.App.Current.Services
            .GetRequiredService<MainWindow>();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(mainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
    }

    // â”€â”€â”€ Validation & result â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void AddModel_Click(ContentDialog sender, ContentDialogButtonClickEventArgs e)
    {
        // Validate â€” cancel close if invalid
        if (string.IsNullOrWhiteSpace(DisplayNameBox.Text))
        {
            ShowError("Please enter a display name.");
            e.Cancel = true;
            return;
        }

        bool isLocal = LocalFileRadio.IsChecked == true;

        if (isLocal && string.IsNullOrWhiteSpace(FilePathBox.Text))
        {
            ShowError("Please select a model file.");
            e.Cancel = true;
            return;
        }

        if (!isLocal && string.IsNullOrWhiteSpace(DownloadUrlBox.Text))
        {
            ShowError("Please enter a download URL.");
            e.Cancel = true;
            return;
        }

        if (isLocal && !File.Exists(FilePathBox.Text))
        {
            ShowError("The selected file does not exist.");
            e.Cancel = true;
            return;
        }

        bool isVision = IsVisionModelCheck.IsChecked == true;
        if (isVision)
        {
            if (isLocal && string.IsNullOrWhiteSpace(MmProjFilePathBox.Text))
            {
                ShowError("Please select a Multi-Modal Projector file for the Vision Model.");
                e.Cancel = true;
                return;
            }
            if (!isLocal && string.IsNullOrWhiteSpace(MmProjDownloadUrlBox.Text))
            {
                ShowError("Please enter a Multi-Modal Projector URL for the Vision Model.");
                e.Cancel = true;
                return;
            }
            if (isLocal && !File.Exists(MmProjFilePathBox.Text))
            {
                ShowError("The selected Multi-Modal Projector file does not exist.");
                e.Cancel = true;
                return;
            }
        }

        // Build result
        var fileName = isLocal
            ? Path.GetFileName(FilePathBox.Text)
            : Path.GetFileName(new Uri(DownloadUrlBox.Text).LocalPath);

        long fileSize = isLocal && File.Exists(FilePathBox.Text)
            ? new FileInfo(FilePathBox.Text).Length
            : 0;

        Result = new CustomModelEntity
        {
            Name = fileName,
            DisplayName = DisplayNameBox.Text.Trim(),
            Description = DescriptionBox.Text?.Trim() ?? string.Empty,
            FilePath = isLocal ? FilePathBox.Text : string.Empty,
            DownloadUrl = isLocal ? string.Empty : DownloadUrlBox.Text.Trim(),
            SizeBytes = fileSize,
            IsLocal = isLocal,
            AddedDate = DateTime.UtcNow,
            IsVisionModel = isVision,
            MmProjFilePath = isVision && isLocal ? MmProjFilePathBox.Text : string.Empty,
            MmProjDownloadUrl = isVision && !isLocal ? MmProjDownloadUrlBox.Text.Trim() : string.Empty
        };
    }

    private void ShowError(string message)
    {
        ValidationError.Text = message;
        ValidationError.Visibility = Visibility.Visible;
    }
}
