using System.IO;
using System.Windows;
using KaiROS.AI.Models;
using Microsoft.Win32;
using WpfMessageBox = System.Windows.MessageBox;

namespace KaiROS.AI.Views;

public partial class AddCustomModelDialog : Window
{
    public CustomModelEntity? Result { get; private set; }
    
    public AddCustomModelDialog()
    {
        InitializeComponent();
        
        // Toggle panels based on radio button
        LocalFileRadio.Checked += (s, e) => 
        {
            LocalFilePanel.Visibility = Visibility.Visible;
            DownloadUrlPanel.Visibility = Visibility.Collapsed;
        };
        DownloadUrlRadio.Checked += (s, e) => 
        {
            LocalFilePanel.Visibility = Visibility.Collapsed;
            DownloadUrlPanel.Visibility = Visibility.Visible;
            
            // Sync vision panels
            if (IsVisionModelCheck.IsChecked == true)
            {
                LocalMmProjPanel.Visibility = Visibility.Collapsed;
                RemoteMmProjPanel.Visibility = Visibility.Visible;
            }
        };

        LocalFileRadio.Checked += (s, e) => 
        {
            LocalFilePanel.Visibility = Visibility.Visible;
            DownloadUrlPanel.Visibility = Visibility.Collapsed;
            
            // Sync vision panels
            if (IsVisionModelCheck.IsChecked == true)
            {
                LocalMmProjPanel.Visibility = Visibility.Visible;
                RemoteMmProjPanel.Visibility = Visibility.Collapsed;
            }
        };
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
            MmProjFilePathBox.Clear();
            MmProjDownloadUrlBox.Clear();
        }
    }
    
    private void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "GGUF Model Files (*.gguf)|*.gguf|All Files (*.*)|*.*",
            Title = "Select Model File"
        };
        
        if (dialog.ShowDialog() == true)
        {
            FilePathBox.Text = dialog.FileName;
            
            // Auto-fill display name if empty
            if (string.IsNullOrWhiteSpace(DisplayNameBox.Text))
            {
                DisplayNameBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
    }

    private void BrowseMmProjFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "GGUF Model Files (*.gguf)|*.gguf|All Files (*.*)|*.*",
            Title = "Select Multi-Modal Projector File"
        };
        
        if (dialog.ShowDialog() == true)
        {
            MmProjFilePathBox.Text = dialog.FileName;
        }
    }
    
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
    
    private void AddModel_Click(object sender, RoutedEventArgs e)
    {
        // Validate
        if (string.IsNullOrWhiteSpace(DisplayNameBox.Text))
        {
            WpfMessageBox.Show("Please enter a display name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        bool isLocal = LocalFileRadio.IsChecked == true;
        
        if (isLocal && string.IsNullOrWhiteSpace(FilePathBox.Text))
        {
            WpfMessageBox.Show("Please select a model file.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        if (!isLocal && string.IsNullOrWhiteSpace(DownloadUrlBox.Text))
        {
            WpfMessageBox.Show("Please enter a download URL.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        if (isLocal && !File.Exists(FilePathBox.Text))
        {
            WpfMessageBox.Show("The selected file does not exist.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool isVision = IsVisionModelCheck.IsChecked == true;
        if (isVision)
        {
            if (isLocal && string.IsNullOrWhiteSpace(MmProjFilePathBox.Text))
            {
                WpfMessageBox.Show("Please select a Multi-Modal Projector file for the Vision Model.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!isLocal && string.IsNullOrWhiteSpace(MmProjDownloadUrlBox.Text))
            {
                WpfMessageBox.Show("Please enter a Multi-Modal Projector URL for the Vision Model.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (isLocal && !File.Exists(MmProjFilePathBox.Text))
            {
                WpfMessageBox.Show("The selected Multi-Modal Projector file does not exist.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        
        // Create result
        var fileName = isLocal 
            ? Path.GetFileName(FilePathBox.Text) 
            : Path.GetFileName(new Uri(DownloadUrlBox.Text).LocalPath);
        
        long fileSize = 0;
        if (isLocal && File.Exists(FilePathBox.Text))
        {
            fileSize = new FileInfo(FilePathBox.Text).Length;
        }
        
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
        
        DialogResult = true;
        Close();
    }
}
