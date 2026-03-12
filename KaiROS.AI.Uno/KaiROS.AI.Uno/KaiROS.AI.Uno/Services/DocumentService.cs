using KaiROS.AI.Uno.Models;

namespace KaiROS.AI.Uno.Services;

public class DocumentService : IDocumentService
{
    private readonly List<Document> _loadedDocuments = [];

    public IReadOnlyList<Document> LoadedDocuments => _loadedDocuments;

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task<Document> LoadDocumentAsync(string filePath)
    {
#if DESKTOP
        var content = await GetDocumentContentAsync(filePath);
        var doc = new Document
        {
            FileName = Path.GetFileName(filePath),
            FilePath = filePath,
            Content = content,
            Type = GetDocumentType(filePath),
            FileSizeBytes = new FileInfo(filePath).Length
        };

        // Split into chunks
        doc.Chunks = CreateChunks(content, 500);

        _loadedDocuments.Add(doc);
        return doc;
#else
        // WASM - would need to use JS interop for file access
        var doc = new Document
        {
            FileName = "Document loading not available on web",
            FilePath = "",
            Content = "",
            Type = DocumentType.Unknown
        };
        return doc;
#endif
    }

    public void RemoveDocument(string documentId)
    {
        var doc = _loadedDocuments.FirstOrDefault(d => d.Id == documentId);
        if (doc != null)
        {
            _loadedDocuments.Remove(doc);
        }
    }

    public void ClearAllDocuments()
    {
        _loadedDocuments.Clear();
    }

    public string GetContextForQuery(string query, int maxChunks = 3)
    {
        // Simple keyword matching - would use embeddings in production
        var relevantChunks = _loadedDocuments
            .SelectMany(d => d.Chunks)
            .Take(maxChunks)
            .Select(c => c.Content)
            .ToList();

        return string.Join("\n\n", relevantChunks);
    }

    public async Task<string> GetDocumentContentAsync(string filePath)
    {
#if DESKTOP
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".txt" or ".md" => await File.ReadAllTextAsync(filePath),
            ".json" => await File.ReadAllTextAsync(filePath),
            _ => await File.ReadAllTextAsync(filePath)
        };
#else
        return string.Empty;
#endif
    }

#if DESKTOP
    private DocumentType GetDocumentType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => DocumentType.Pdf,
            ".docx" => DocumentType.Word,
            ".txt" or ".md" => DocumentType.Text,
            _ => DocumentType.Unknown
        };
    }

    private List<DocumentChunk> CreateChunks(string content, int chunkSize)
    {
        var chunks = new List<DocumentChunk>();
        var words = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < words.Length; i += chunkSize)
        {
            var chunkWords = words.Skip(i).Take(chunkSize).ToArray();
            chunks.Add(new DocumentChunk
            {
                Index = chunks.Count,
                Content = string.Join(' ', chunkWords),
                StartPosition = i,
                EndPosition = Math.Min(i + chunkSize, words.Length)
            });
        }

        return chunks;
    }
#endif
}
