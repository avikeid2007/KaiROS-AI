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

# Robot 🤖 = F0 9F A4 96
$robot = W([byte[]]@(0xF0,0x9F,0xA4,0x96))
$old = "Text=""$($robot) Assistant"""
$new = ''
$c = $c.Replace($old, 'Text="&#xE9CE; Assistant"')
Write-Host "Robot in CI: $($c -ne $orig)"

# Reload after robot fix detection
$c1 = $c

# Streaming dots — find what chars are used
$m = [regex]::Match($c, 'Text="([^"]+)"\s*\r?\n\s*Foreground="\{StaticResource PrimaryBrush\}"')
if ($m.Success) {
    $dotStr = $m.Groups[1].Value
    Write-Host "Streaming dots string found: len=$($dotStr.Length)"
    $dotStr.ToCharArray() | ForEach-Object { Write-Host "  char: U+$([int]$_ )" }
    $c = $c.Replace(
        "Text=""$dotStr""",
        'Text="&#x2022;&#x2022;&#x2022;"')
    Write-Host "Streaming fixed: $($c -ne $c1)"
}

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

# ▶ = E2 96 B6
$play = W([byte[]]@(0xE2,0x96,0xB6))
# ⬛ = E2 AC 9B
$stop_sq = W([byte[]]@(0xE2,0xAC,0x9B))
# 🌐 = F0 9F 8C 90
$globe = W([byte[]]@(0xF0,0x9F,0x8C,0x90))
# 🗑 = F0 9F 97 91
$trash = W([byte[]]@(0xF0,0x9F,0x97,0x91))
# 🔧 = F0 9F 94 A7
$wrench = W([byte[]]@(0xF0,0x9F,0x94,0xA7))
# ✨ = E2 9C A8
$sparkle = W([byte[]]@(0xE2,0x9C,0xA8))
# ✕ = E2 9C 95
$xmark = W([byte[]]@(0xE2,0x9C,0x95))
# 📄 = F0 9F 93 84
$doc = W([byte[]]@(0xF0,0x9F,0x93,0x84))
# 🗄 = F0 9F 97 84
$cabinet = W([byte[]]@(0xF0,0x9F,0x97,0x84))
# 💻 = F0 9F 92 BB
$laptop = W([byte[]]@(0xF0,0x9F,0x92,0xBB))

# ▶ icon-only list button
$c = $c.Replace("Content=""$play""", 'Content="&#x25B6;"')
# ⬛ icon-only list button
$c = $c.Replace("Content=""$stop_sq""", 'Content="&#x25A0;"')
# 🌐 icon-only list button
$c = $c.Replace("Content=""$globe""", 'Content="&#xE8A7;"')
# 🗑 icon-only list button — can be multiple
$c = $c.Replace("Content=""$trash""", 'Content="&#xE74D;"')
# ✨ Create New Service title
$c = $c.Replace("Text=""$sparkle Create New Service""", 'Text="Create New Service"')
# 🔧 wrench icon TextBlock next to service name
$c = $c.Replace("Text=""$wrench """, 'Text="&#xE8FB; "')
# 🌐 View API button with text
$c = $c.Replace("Content=""$globe View API""", 'Content="View API"')
# ▶ Start button with text
$c = $c.Replace("Content=""$play Start""", 'Content="&#x25B6; Start"')
# ⬛ Stop button with text
$c = $c.Replace("Content=""$stop_sq Stop""", 'Content="&#x25A0; Stop"')
# 🗑 Delete button with text
$c = $c.Replace("Content=""$trash Delete""", 'Content="Delete"')
# MenuFlyoutItem texts — strip emoji prefixes
$c = $c.Replace("Text=""$doc File""", 'Text="File"')
$c = $c.Replace("Text=""$globe Web URL""", 'Text="Web URL"')
# 🗄️ = 🗄 + variation selector — try both
$cabinet_vs = $cabinet + [char]0xFE0F
$c = $c.Replace("Text=""$cabinet_vs Database (Reset soon)""", 'Text="Database (Reset soon)"')
$c = $c.Replace("Text=""$cabinet Database (Reset soon)""", 'Text="Database (Reset soon)"')
$c = $c.Replace("Text=""$laptop GitHub (Reset soon)""", 'Text="GitHub (Reset soon)"')
# ✕ remove source button
$c = $c.Replace("Content=""$xmark""", 'Content="&#xE711;"')

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

# ➕ = E2 9E 95
$plus = W([byte[]]@(0xE2,0x9E,0x95))
# ⚙️ = E2 9A 99 EF B8 8F
$gear_vs = W([byte[]]@(0xE2,0x9A,0x99,0xEF,0xB8,0x8F))
$gear = W([byte[]]@(0xE2,0x9A,0x99))

$c = $c.Replace("Content=""$plus Add Custom Model""", 'Content="+ Add Custom Model"')
$c = $c.Replace("Text=""$gear_vs Variant:""", 'Text="Variant:"')
$c = $c.Replace("Text=""$gear Variant:""", 'Text="Variant:"')
# Also check the ï¸ suffix variant
$c = [regex]::Replace($c, 'Text="[^\x00-\x7F]+ Variant:"', 'Text="Variant:"')

if ($c -ne $orig) {
    [System.IO.File]::WriteAllText($f, $c, $enc)
    $fixed += "ModelCatalogView.xaml"
} else { Write-Host "No changes: ModelCatalogView.xaml" }

# ============================================================
# AddCustomModelDialog.xaml -- real emoji (not corrupted)
# ============================================================
$f = "c:\Users\User\source\repos\avikeid2007\Kairos.local\KaiROS.AI\Views\AddCustomModelDialog.xaml"
$c = [System.IO.File]::ReadAllText($f, $enc)
$orig = $c

# ➕ real emoji U+2795
$plus_real = [char]::ConvertFromUtf32(0x2795)
# 📁 real emoji U+1F4C1
$folder = [char]::ConvertFromUtf32(0x1F4C1)
# 🌐 real emoji U+1F310
$globe_real = [char]::ConvertFromUtf32(0x1F310)

$c = $c.Replace("Title=""$plus_real Add Custom Model""", 'Title="Add Custom Model"')
$c = $c.Replace("Content=""$folder Local File""", 'Content="Local File"')
$c = $c.Replace("Content=""$globe_real Download URL""", 'Content="Download URL"')

if ($c -ne $orig) {
    [System.IO.File]::WriteAllText($f, $c, $enc)
    $fixed += "AddCustomModelDialog.xaml"
} else { Write-Host "No changes: AddCustomModelDialog.xaml" }

# ============================================================
# SettingsView.xaml — bullet • U+2022 in Run text is fine for PRI
# ============================================================

Write-Host ""
if ($fixed.Count -gt 0) { Write-Host "FIXED: $($fixed -join ', ')" }
else { Write-Host "Nothing changed." }
