using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.Storage;

namespace KaiROS.AI.WinUI.Views;

public partial class ChatView : UserControl
{
    public ChatView()
    {
        InitializeComponent();
    }

    // â”€â”€â”€ Session Sidebar handlers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void SessionItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext != null &&
            DataContext is ViewModels.ChatViewModel vm)
        {
            vm.LoadSessionCommand.Execute(fe.DataContext);
        }
    }

    private void DeleteSession_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag != null &&
            DataContext is ViewModels.ChatViewModel vm)
        {
            vm.DeleteSessionCommand.Execute(btn.Tag);
        }
    }

    // â”€â”€â”€ Keyboard accelerators â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void SendAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs e)
    {
        if (DataContext is ViewModels.ChatViewModel vm)
        {
            if (vm.SendMessageCommand.CanExecute(null))
                vm.SendMessageCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void NewSessionAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs e)
    {
        if (DataContext is ViewModels.ChatViewModel vm)
        {
            vm.NewSessionCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void ClearChatAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs e)
    {
        if (DataContext is ViewModels.ChatViewModel vm)
        {
            vm.ClearChatCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void ToggleSearchAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs e)
    {
        if (DataContext is ViewModels.ChatViewModel vm)
        {
            vm.ToggleSearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    // â”€â”€â”€ MessageInput events â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void MessageInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Enter to Send (no Shift)
        if (e.Key == VirtualKey.Enter &&
            !Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
        {
            if (DataContext is ViewModels.ChatViewModel vm && vm.IsEnterToSendEnabled)
            {
                e.Handled = true;
                if (vm.SendMessageCommand.CanExecute(null))
                    vm.SendMessageCommand.Execute(null);
            }
        }

        // Ctrl+V â€” check clipboard for image
        if (e.Key == VirtualKey.V &&
            Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
        {
            _ = HandleImagePasteAttemptAsync();
        }
    }

    private async Task HandleImagePasteAttemptAsync()
    {
        try
        {
            var dataView = Clipboard.GetContent();

            // File-drop list
            if (dataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await dataView.GetStorageItemsAsync();
                if (items.Count > 0 && items[0] is StorageFile file)
                {
                    var ext = System.IO.Path.GetExtension(file.Name).ToLowerInvariant();
                    if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp")
                    {
                        if (DataContext is ViewModels.ChatViewModel vm)
                        {
                            vm.AttachedImagePath = file.Path;
                            vm.HasAttachedImage = true;
                        }
                    }
                }
                return;
            }

            // Raw bitmap
            if (dataView.Contains(StandardDataFormats.Bitmap))
            {
                var streamRef = await dataView.GetBitmapAsync();
                using var stream = await streamRef.OpenReadAsync();
                var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Kairos_{Guid.NewGuid()}.png");

                var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
                using var fileStream = await StorageFile.GetFileFromPathAsync(tempPath)
                    .AsTask()
                    .ContinueWith(_ => Task.FromResult<IStorageFile?>(null)); // Will fail â€” use StorageFolder approach

                // Simpler: copy the stream to a temp file
                var folder = await StorageFolder.GetFolderFromPathAsync(System.IO.Path.GetTempPath());
                var tempFile = await folder.CreateFileAsync($"Kairos_{Guid.NewGuid()}.png",
                    CreationCollisionOption.GenerateUniqueName);
                using (var outStream = await tempFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await Windows.Storage.Streams.RandomAccessStream.CopyAsync(stream, outStream);
                }

                if (DataContext is ViewModels.ChatViewModel vmBmp)
                {
                    vmBmp.AttachedImagePath = tempFile.Path;
                    vmBmp.HasAttachedImage = true;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Clipboard paste failed: {ex.Message}");
        }
    }

    // â”€â”€â”€ Drag & Drop â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void MessageInput_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.Handled = true;
        }
    }

    private async void MessageInput_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0 && items[0] is StorageFile file)
                {
                    var ext = System.IO.Path.GetExtension(file.Name).ToLowerInvariant();
                    if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp")
                    {
                        if (DataContext is ViewModels.ChatViewModel vm)
                        {
                            vm.AttachedImagePath = file.Path;
                            vm.HasAttachedImage = true;
                            e.Handled = true;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Drop failed: {ex.Message}");
        }
    }
}
