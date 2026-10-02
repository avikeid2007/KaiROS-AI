## Summary
Add embedding-based semantic search across chat history and documents using LLamaSharp's embedding support.

## Motivation
Current search is keyword-based. Semantic search finds related content by meaning, dramatically improving the search experience across chat history and documents.

## Detailed Requirements

### Service: `EmbeddingService.cs`
```csharp
public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text);
    Task IndexMessageAsync(string sessionId, string messageContent, string messageId);
    Task<List<SearchResult>> SemanticSearchAsync(string query, int topK = 10);
    Task RebuildIndexAsync(IProgress<int> progress = null);
}

public class SearchResult
{
    public string MessageId { get; set; }
    public string SessionId { get; set; }
    public string Content { get; set; }
    public float Score { get; set; }  // Cosine similarity
}
```

- Use `LLamaEmbedder` from LLamaSharp to generate embeddings
- Store embeddings in SQLite as BLOB
- Cosine similarity search for finding related content
- Index: chat messages, documents, notes (when those features exist)

### Database (`DatabaseService.cs`)
Add `Embeddings` table:
```sql
CREATE TABLE IF NOT EXISTS Embeddings (
    Id TEXT PRIMARY KEY,
    SourceType TEXT NOT NULL,   -- 'message', 'document', 'note'
    SourceId TEXT NOT NULL,
    Content TEXT NOT NULL,
    Embedding BLOB NOT NULL,
    CreatedAt TEXT NOT NULL
);
```

### UI
- Enhanced search in chat history sidebar with "Semantic" toggle button
- Search results ranked by relevance score (show score as percentage)
- Highlight matching content in search results

### Background Indexing
- Index new messages automatically after each conversation
- Use a background thread/task to avoid impacting chat performance
- Show indexing progress in status bar

## Files to Create/Modify
- **[NEW]** `Services/EmbeddingService.cs` + `IEmbeddingService.cs`
- **[NEW]** `Models/SearchResult.cs`
- **[MODIFY]** `Services/DatabaseService.cs` - add Embeddings table
- **[MODIFY]** `ViewModels/ChatViewModel.cs` - integrate semantic search
- **[MODIFY]** `Views/ChatView.xaml` - semantic search toggle in history panel

## Acceptance Criteria
- [ ] Embeddings generated for chat messages using LLamaSharp
- [ ] Semantic search returns relevant results by meaning, not just keywords
- [ ] Search results ranked by cosine similarity score
- [ ] Background indexing does not impact chat performance
- [ ] Works with any loaded GGUF model that supports embeddings
