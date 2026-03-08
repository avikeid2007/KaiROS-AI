$files = @(
    "MainWindow.xaml",
    "Views\ChatView.xaml",
    "Views\DocumentView.xaml",
    "Views\ModelCatalogView.xaml",
    "Views\SettingsView.xaml",
    "Views\AddCustomModelDialog.xaml",
    "Themes\ModernTheme.xaml",
    "Controls\CodeBlock.xaml"
)

foreach ($f in $files) {
    $path = "c:\Users\User\source\repos\avikeid2007\Kairos.local\KaiROS.AI\$f"
    try {
        [xml](Get-Content $path -Raw -Encoding UTF8) | Out-Null
        Write-Host "OK: $f"
    } catch {
        Write-Host "ERROR: $f - $($_.Exception.Message)"
    }
}
