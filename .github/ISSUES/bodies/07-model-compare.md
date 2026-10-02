## Summary
Add side-by-side model comparison - send the same prompt to two loaded models simultaneously and compare their outputs.

## Motivation
Helps users evaluate which model performs best for their use case. Great for benchmarking and testing.

## Detailed Requirements

### UI: CompareView
- Add "Compare" navigation item to `MainWindow.xaml` NavigationView (Tag="4")
- Split-pane layout: two chat columns side by side
- Model selector dropdown at top of each column (from available models in catalog)
- Shared input field at bottom - sends to both models simultaneously
- Performance stats displayed per column (tokens/sec, total tokens, time taken)

### Implementation
- Reuse existing `IChatService` infrastructure
- Since LLamaSharp may not support two models simultaneously in memory, implement sequential execution:
  - Load Model A -> generate response -> unload
  - Load Model B -> generate response -> unload
  - Display both results when complete
- Show progress: "Running Model A..." then "Running Model B..."
- Consider caching model loads if same model is selected again

### ViewModel: `CompareViewModel.cs`
```csharp
public class CompareViewModel : ViewModelBase
{
    public ObservableCollection<LLMModelInfo> AvailableModels { get; }
    public LLMModelInfo ModelA { get; set; }
    public LLMModelInfo ModelB { get; set; }
    public string SharedInput { get; set; }
    public string ResponseA { get; set; }
    public string ResponseB { get; set; }
    public InferenceStats StatsA { get; set; }
    public InferenceStats StatsB { get; set; }
    public IRelayCommand CompareCommand { get; }
}
```

## Files to Create/Modify
- **[NEW]** `Views/CompareView.xaml` + `CompareView.xaml.cs`
- **[NEW]** `ViewModels/CompareViewModel.cs`
- **[MODIFY]** `MainWindow.xaml` - add Compare nav item
- **[MODIFY]** `MainWindow.xaml.cs` - handle Compare navigation

## Acceptance Criteria
- [ ] Split-pane comparison view with model selectors
- [ ] Same prompt sent to both models
- [ ] Results and performance stats displayed side by side
- [ ] Clear visual indication of which model produced which output
- [ ] Handles case where only one model is downloaded
