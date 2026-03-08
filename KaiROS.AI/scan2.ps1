$enc = [System.Text.Encoding]::UTF8
foreach ($fname in @("ChatView.xaml","DocumentView.xaml","ModelCatalogView.xaml")) {
    $f = "c:\Users\User\source\repos\avikeid2007\Kairos.local\KaiROS.AI\Views\$fname"
    $text = [System.IO.File]::ReadAllText($f, $enc)
    Write-Host ""
    Write-Host "=== $fname ==="
    $mlist = [regex]::Matches($text, '[^\x09\x0A\x0D\x20-\x7E]')
    $seen = @{}
    foreach ($m in $mlist) {
        $pos = $m.Index
        $start = [Math]::Max(0,$pos-40)
        $len = [Math]::Min(80, $text.Length - $start)
        $ctx = $text.Substring($start, $len) -replace "`r`n"," " -replace "`n"," " -replace "`r"," "
        $key = $ctx.Trim()
        if ($key.Length -gt 70) { $key = $key.Substring(0,70) }
        if (-not $seen[$key]) {
            $seen[$key] = $true
            Write-Host "  $key"
        }
    }
}
