$enc = [System.Text.Encoding]::UTF8
$w1252 = [System.Text.Encoding]::GetEncoding(1252)
function W([byte[]]$b) { return $w1252.GetString($b) }

$fixed = @()

# ============================================================
# ChatView.xaml
# ============================================================
$f = "c:\Users\User\source\repos\avikeid2007\Kairos.local\KaiROS.AI\Views\ChatView.xaml"
$c = [System.IO.File]::ReadAllText($f, $enc)
$orig = $c

# "âŽ Enter to Send" — ↵ U+23CE = E2 8F 8E
$enter = W([byte[]]@(0xE2, 0x8F, 0x8E))
$c = $c.Replace("Text=""$enter Enter to Send""", 'Text="Enter to Send"')

# <Run Text="ðŸ""/> — 📐 U+1F4D0 = F0 9F 93 90 (context window icon)
$ctx = W([byte[]]@(0xF0, 0x9F, 0x93, 0x90))
$c = $c.Replace("<Run Text=""$ctx""/>", '<Run FontFamily="Segoe MDL2 Assets" Text="&#xE8EF;"/>')

if ($c -ne $orig) {
    [System.IO.File]::WriteAllText($f, $c, $enc)
    $fixed += "ChatView.xaml"
} else { Write-Host "No changes: ChatView.xaml" }

# ============================================================
# DocumentView.xaml
# ============================================================
$f = "c:\Users\User\source\repos\avikeid2007\Kairos.local\KaiROS.AI\Views\DocumentView.xaml"
$c = [System.IO.File]::ReadAllText($f, $enc)
$orig = $c

# "▾" U+25BE = E2 96 BE (dropdown arrow in "Add Source ▾")
$down = W([byte[]]@(0xE2, 0x96, 0xBE))
$c = $c.Replace("Content=""+ Add Source $down""", 'Content="+ Add Source &#x25BE;"')

# "🗄️" (file cabinet + var selector) = F0 9F 97 84 EF B8 8F
$cabinet_vs = W([byte[]]@(0xF0, 0x9F, 0x97, 0x84, 0xEF, 0xB8, 0x8F))
$cabinet    = W([byte[]]@(0xF0, 0x9F, 0x97, 0x84))
$c = $c.Replace("Text=""$cabinet_vs Database (Reset soon)""", 'Text="Database (Reset soon)"')
$c = $c.Replace("Text=""$cabinet Database (Reset soon)""", 'Text="Database (Reset soon)"')

# catch with regex in case variation selector encoding differs
$c = [regex]::Replace($c, 'Text="[^\x00-\x7F]{2,10} Database \(Reset soon\)"', 'Text="Database (Reset soon)"')

if ($c -ne $orig) {
    [System.IO.File]::WriteAllText($f, $c, $enc)
    $fixed += "DocumentView.xaml"
} else { Write-Host "No changes: DocumentView.xaml" }

# ============================================================
# ModelCatalogView.xaml
# ============================================================
$f = "c:\Users\User\source\repos\avikeid2007\Kairos.local\KaiROS.AI\Views\ModelCatalogView.xaml"
$c = [System.IO.File]::ReadAllText($f, $enc)
$orig = $c

# "👁️" = F0 9F 91 81 EF B8 8F
$eye_vs = W([byte[]]@(0xF0, 0x9F, 0x91, 0x81, 0xEF, 0xB8, 0x8F))
$eye    = W([byte[]]@(0xF0, 0x9F, 0x91, 0x81))
$c = $c.Replace("Text=""$eye_vs Vision:""", 'Text="Vision:"')
$c = $c.Replace("Text=""$eye Vision:""", 'Text="Vision:"')
$c = [regex]::Replace($c, 'Text="[^\x00-\x7F]{2,10} Vision:"', 'Text="Vision:"')

# "✓" U+2713 = E2 9C 93
$check = W([byte[]]@(0xE2, 0x9C, 0x93))
$c = $c.Replace("Text=""$check""", 'Text="&#x2713;"')

# "★" U+2605 = E2 98 85  (corrupted version in badge Text)
$star_c = W([byte[]]@(0xE2, 0x98, 0x85))
$c = $c.Replace("Text=""$star_c Recommended""", 'Text="&#x2605; Recommended"')

if ($c -ne $orig) {
    [System.IO.File]::WriteAllText($f, $c, $enc)
    $fixed += "ModelCatalogView.xaml"
} else { Write-Host "No changes: ModelCatalogView.xaml" }

Write-Host ""
if ($fixed.Count -gt 0) { Write-Host "FIXED: $($fixed -join ', ')" }
else { Write-Host "Nothing changed." }
