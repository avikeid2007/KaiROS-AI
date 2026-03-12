using KaiROS.AI.Uno.Models;

namespace KaiROS.AI.Uno.Services;

public interface ISessionService
{
    Task InitializeAsync();
}

public interface IExportService
{
    Task ExportChatAsync(string filePath, string format);
}

public interface IDocumentService
{
    IReadOnlyList<Document> LoadedDocuments { get; }
    Task InitializeAsync();
    Task<Document> LoadDocumentAsync(string filePath);
    void RemoveDocument(string documentId);
    void ClearAllDocuments();
    string GetContextForQuery(string query, int maxChunks = 3);
    Task<string> GetDocumentContentAsync(string filePath);
}

public interface IKairosThemeService
{
    string CurrentTheme { get; }
    Task LoadSavedTheme();
    Task SetThemeAsync(string theme);
}
