# KaiROS.AI — Project Status & Migration Log

**Last Updated**: March 7, 2026  
**Project**: WPF → WinUI 3 Migration + Bug Fixes  
**Build Status**: ✅ Succeeded (0 errors, 81 MVVMTK0045 warnings — harmless AOT hints)

---

## What Was Done

### Phase 1 — WPF to WinUI 3 Full Migration
- Converted entire project from WPF (`net9.0-windows`) to WinUI 3 (`net9.0-windows10.0.19041.0`)
- Replaced all WPF namespaces (`System.Windows.*`) with WinUI 3 equivalents (`Microsoft.UI.Xaml.*`)
- Replaced `Dispatcher` with `DispatcherQueue`
- Replaced `IValueConverter` (WPF) with WinUI 3 `IValueConverter` (different interface signature)
- Replaced `MessageBox.Show` with WinUI 3 `ContentDialog`
- Added `Microsoft.WindowsAppSDK 1.8.260209005` and removed WPF-specific packages
- Added `UseWinUI=true` and MSIX packaging config to `.csproj`
- Created `Directory.Build.props` to fix `dotnet run` PRI task path issue
- Fixed `AssemblyInfo.cs` to remove WPF-specific attributes

### Phase 2 — UI & Theme Fixes
- **MainWindow.xaml**: Added `Background="{StaticResource BackgroundBrush}"` to root Grid — dark theme now visible
- **MainWindow.xaml**: Fixed `ContentControl` — set `Margin="0"`, `HorizontalContentAlignment="Stretch"`, `VerticalContentAlignment="Stretch"`
- **ModernTheme.xaml**: Fixed `ModernListBoxItem` — nav items now show white text when selected (purple background)
  - Added `x:Name="contentPresenter"` to ContentPresenter
  - `Selected` and `SelectedUnfocused` visual states set `Foreground` to `TextPrimaryBrush`

### Phase 3 — Threading Fixes (ChatViewModel.cs)
Root cause: WinUI 3 requires ALL UI/collection mutations on the UI thread. Events from LLamaSharp fire on background threads.

**Fixed:**
- `OnModelLoaded` / `OnModelUnloaded` — now dispatch via `_dispatcherQueue.TryEnqueue`
- `CollectionChanged` on `_raasService.Configurations` — dispatches `UpdateKnowledgeBaseList`
- `InitializeAsync` — sessions loaded off-thread, applied inside `TryEnqueue` block; `UpdateKnowledgeBaseList` dispatched
- `LoadSession` — all post-`await` UI work wrapped in `TryEnqueue`; passes `_dispatcherQueue` to each `new ChatMessageViewModel`
- `DeleteSession` — `Sessions.Remove` and state reset in `TryEnqueue`
- Removed orphaned `LoadSessionsAsync` method

**ChatMessageViewModel (inner class):**
- Constructor: `public ChatMessageViewModel(ChatMessage message, DispatcherQueue? dispatcherQueue = null)`
- `_dispatcherQueue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread()`
- `AppendContent` — lock-safe buffer, `EnqueueUI` for all UI updates
- `EnqueueUI` — inline on UI thread, dispatches when on background thread
- `EnsureFlushTimer` — only created/started on UI thread via `EnqueueUI`
- `FinalizeStreaming`, `CleanupContent` — lock buffer then `EnqueueUI` for content updates

### Phase 4 — "Unable to Send Message" Fix

**Root Cause Discovered:**  
In WPF, `TextBox.Text` defaulted to `TwoWay` binding mode.  
**In WinUI 3, `{Binding}` defaults to `OneWay` for ALL properties including `TextBox.Text`.**  
This meant `UserInput` was never written back from the TextBox → `IsNullOrWhiteSpace(UserInput)` was always true → `SendMessage` returned immediately every time.

**Bindings fixed with `Mode=TwoWay`:**

| File | Control | Property |
|------|---------|----------|
| `Views/ChatView.xaml` | TextBox | `UserInput` ← **THE root cause** |
| `Views/ChatView.xaml` | TextBox | `SearchText` |
| `Views/ChatView.xaml` | ComboBox | `SelectedKnowledgeBase` |
| `Views/SettingsView.xaml` | TextBox | `ApiPort` |
| `Views/SettingsView.xaml` | TextBox | `SystemPrompt` |
| `Views/DocumentView.xaml` | ComboBox | `SelectedConfiguration` |
| `Views/DocumentView.xaml` | TextBox | `NewServiceName` |
| `Views/DocumentView.xaml` | TextBox | `NewServicePort` |
| `Views/DocumentView.xaml` | TextBox | `NewServiceDescription` |
| `Views/DocumentView.xaml` | TextBox | `NewServiceSystemPrompt` |
| `Views/ModelCatalogView.xaml` | ComboBox | `SelectedOrganization` |
| `Views/ModelCatalogView.xaml` | ComboBox | `SelectedFamily` |
| `Views/ModelCatalogView.xaml` | ComboBox | `SelectedVariant` |
| `Views/ModelCatalogView.xaml` | ComboBox | `SelectedVisionOption` |

**SendMessage restructured:**
- Outer `try-catch` wrapping ALL code (session creation, message adding, RAG, inference)
- Errors now shown as visible `⚠️ Error: ...` chat messages (no more silent swallowing by `AsyncRelayCommand`)
- `userMessage` `ChatMessageViewModel` now passes `_dispatcherQueue`
- `savedInput` saved before clearing, restored on failure so user can retry

---

## What Is Pending / Not Yet Verified

### 1. Runtime Test — Send Message End-to-End
**Status**: Not yet verified since fixes were applied.  
**Test Steps:**
1. Launch app (`dotnet run`)
2. Load a model from the Models tab
3. Type a message in the chat input box
4. Press Enter or click Send
5. Verify user bubble appears on the right
6. Verify assistant streaming response appears on the left
7. If error occurs, verify `⚠️ Error: ...` message appears in chat

### 2. Context Size Mismatch (Potential Crash)
**Status**: Identified but NOT fixed.  
**Issue**: `ModelManagerService` loads model weights with `ContextSize = 4096`, but `ChatService.InitializeContext()` creates a new `LLamaContext` with `ContextSize = 8192`. This mismatch may cause a LLamaSharp error at runtime when the first message is sent.  
**Fix needed** in `Services/ChatService.cs` — `InitializeContext()`:
```csharp
// Change from 8192 to match ModelManagerService
ContextSize = 4096,
```

### 3. Web Search Feature
**Status**: UI toggle exists (`IsWebSearchEnabled`), implementation unknown — needs verification that it doesn't throw unhandled exceptions.

### 4. RAG / Knowledge Base Feature
**Status**: UI exists for selecting knowledge bases, `_raasService` integration is wired up, but end-to-end test not done.

### 5. Model Catalog Download
**Status**: UI exists with Organization/Family/Variant ComboBoxes (bindings now fixed with `Mode=TwoWay`), but download flow not tested.

### 6. API Server (RaaS)
**Status**: `SettingsView.xaml` has ApiPort/SystemPrompt bindings fixed. The HTTP endpoint (`RaaS.http`) exists. Whether the embedded API server starts and responds correctly is untested.

### 7. MSIX Store Packaging
**Status**: `Package.appxmanifest` and signing cert thumbprint configured. `Build-MSIX.ps1` exists. Not verified end-to-end for Store submission.

### 8. Document (RAG) Ingestion
**Status**: `DocumentView.xaml` bindings fixed. Actual PDF/DOCX/HTML ingestion pipeline via `DocumentFormat.OpenXml`, `itext7`, `HtmlAgilityPack` not tested.

---

## Key Technical Notes for Future Sessions

### WinUI 3 vs WPF Binding Differences
| Feature | WPF | WinUI 3 |
|---------|-----|---------|
| Default TextBox.Text binding mode | TwoWay | **OneWay** |
| Default ComboBox.SelectedItem binding mode | TwoWay | **OneWay** |
| All other bindings default | OneWay | OneWay |
| `UpdateSourceTrigger=PropertyChanged` | Supported | Supported |
| `Dispatcher` | `System.Windows.Threading.Dispatcher` | `Microsoft.UI.Dispatching.DispatcherQueue` |

**Rule**: Always add `Mode=TwoWay` explicitly to ALL user-editable controls in WinUI 3.

### Build Command
```powershell
cd "c:\Users\User\source\repos\avikeid2007\Kairos.local\KaiROS.AI"
dotnet build KaiROS.AI.csproj /p:Platform=x64 /nologo
```

### Run Command
```powershell
cd "c:\Users\User\source\repos\avikeid2007\Kairos.local\KaiROS.AI"
dotnet run
```

### Project Stack
- **Framework**: net9.0-windows10.0.19041.0, WinUI 3
- **WindowsAppSDK**: 1.8.260209005
- **MVVM**: CommunityToolkit.Mvvm 8.4.0
- **DI**: Microsoft.Extensions.DependencyInjection 10.0.3
- **LLM**: LLamaSharp 0.26.0 (CUDA12 + CPU + Vulkan backends)
- **DB**: Microsoft.Data.Sqlite 10.0.3
- **Packaging**: MSIX (WindowsPackageType=MSIX, signed with cert thumbprint)
