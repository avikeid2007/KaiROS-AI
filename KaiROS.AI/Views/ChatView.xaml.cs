namespace KaiROS.AI.Views;

public partial class ChatView : System.Windows.Controls.UserControl
{
    public ChatView()
    {
        InitializeComponent();
        System.Windows.DataObject.AddPastingHandler(MessageInput, OnPaste);
    }

    private void ExportButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.ContextMenu != null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
        }
    }

    private void MessageInput_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Check if Enter is pressed without Shift
        if (e.Key == System.Windows.Input.Key.Enter && !System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
        {
            if (DataContext is ViewModels.ChatViewModel vm && vm.IsEnterToSendEnabled)
            {
                e.Handled = true;
                if (vm.SendMessageCommand.CanExecute(null))
                {
                    vm.SendMessageCommand.Execute(null);
                }
            }
        }
        
        // Handle Ctrl+V directly to bypass TextBox restrictions on non-text clipboards
        if (e.Key == System.Windows.Input.Key.V && System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
        {
            HandleImagePasteAttempt(e);
        }
    }
    
    // Attempt to paste an image manually if Ctrl+V is pressed
    private void HandleImagePasteAttempt(System.Windows.RoutedEventArgs e)
    {
        try
        {
            if (System.Windows.Clipboard.ContainsFileDropList())
            {
                var files = System.Windows.Clipboard.GetFileDropList();
                if (files.Count > 0)
                {
                    var file = files[0];
                    if (file != null)
                    {
                        var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                        if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif" || ext == ".webp")
                        {
                            if (DataContext is ViewModels.ChatViewModel vm)
                            {
                                vm.AttachedImagePath = file;
                                vm.HasAttachedImage = true;
                                e.Handled = true;
                                return;
                            }
                        }
                    }
                }
            }
            if (System.Windows.Clipboard.ContainsImage())
            {
                var image = System.Windows.Clipboard.GetImage();
                if (image != null && DataContext is ViewModels.ChatViewModel vm)
                {
                    string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Kairos_{System.Guid.NewGuid()}.png");
                    using (var fs = new System.IO.FileStream(tempPath, System.IO.FileMode.Create))
                    {
                        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
                        encoder.Save(fs);
                    }
                    vm.AttachedImagePath = tempPath;
                    vm.HasAttachedImage = true;
                    e.Handled = true;
                }
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Clipboard paste failed: {ex.Message}");
        }
    }

    private void OnPaste(object sender, System.Windows.DataObjectPastingEventArgs e)
    {
        // Intercept standard Paste commands (e.g. from context menu context)
        try
        {
            if (e.DataObject.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                if (e.DataObject.GetData(System.Windows.DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    var file = files[0];
                    var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif" || ext == ".webp")
                    {
                        if (DataContext is ViewModels.ChatViewModel vm)
                        {
                            vm.AttachedImagePath = file;
                            vm.HasAttachedImage = true;
                            e.CancelCommand(); // Prevent text insertion
                            e.Handled = true;
                            return;
                        }
                    }
                }
            }
            if (e.DataObject.GetDataPresent(System.Windows.DataFormats.Bitmap))
            {
                if (e.DataObject.GetData(System.Windows.DataFormats.Bitmap) is System.Windows.Media.Imaging.BitmapSource bitmapSrc)
                {
                    if (DataContext is ViewModels.ChatViewModel vm)
                    {
                        string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Kairos_{System.Guid.NewGuid()}.png");
                        using (var fs = new System.IO.FileStream(tempPath, System.IO.FileMode.Create))
                        {
                            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmapSrc));
                            encoder.Save(fs);
                        }
                        vm.AttachedImagePath = tempPath;
                        vm.HasAttachedImage = true;
                        e.CancelCommand(); // Prevent text insertion
                        e.Handled = true;
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnPaste failed: {ex.Message}");
        }
    }

    private void MessageInput_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void MessageInput_PreviewDrop(object sender, System.Windows.DragEventArgs e)
    {
        try
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    var file = files[0];
                    var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif" || ext == ".webp")
                    {
                        if (DataContext is ViewModels.ChatViewModel vm)
                        {
                            vm.AttachedImagePath = file;
                            vm.HasAttachedImage = true;
                            e.Handled = true;
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Drop failed: {ex.Message}");
        }
    }
}
