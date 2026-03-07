$root = "c:\Users\User\source\repos\avikeid2007\Kairos.local\KaiROS.AI"
$exts = @("*.xaml","*.xml","*.appxmanifest","*.resw","*.resx","*.priconfig")

Get-ChildItem -Path $root -Recurse -Include $exts | Where-Object {
    $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\'
} | ForEach-Object {
    $file = $_
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    $results = @()
    for ($i = 0; $i -lt $text.Length; $i++) {
        $c = [int][char]$text[$i]
        # Skip BOM (FEFF), normal ASCII, and tab/CR/LF
        if ($c -gt 127 -and $c -ne 0xFEFF -and $c -ne 0xFFFE) {
            $hex = "U+{0:X4}" -f $c
            $results += "$hex (at index $i, context: '$($text[[Math]::Max(0,$i-10)..[Math]::Min($text.Length-1,$i+10)] -join '')')"
        }
    }
    if ($results.Count -gt 0) {
        Write-Host ""
        Write-Host "=== $($file.FullName) ===" -ForegroundColor Cyan
        $results | Select-Object -First 10 | ForEach-Object { Write-Host "  $_" }
        if ($results.Count -gt 10) { Write-Host "  ... and $($results.Count - 10) more" }
    }
}
Write-Host "`nScan complete."
