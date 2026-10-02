## Summary
Extend the existing RAG document support into a full Document Management Library - upload, organize, tag, and search documents persistently. Documents can be used as context for any chat.

## Motivation
Currently documents are used per-RAG-session. A persistent document library lets users build a personal knowledge base.

## Detailed Requirements

### Enhancements to existing `DocumentView.xaml`
- Add folder/category organization (tree view or flat categories)
- Tag documents with custom labels
- Full-text search across all indexed documents
- Document preview panel (show first N lines or summary)
- Bulk operations (select multiple, delete, re-index)
- "Chat with this document" quick action -> opens chat with RAG context pre-loaded

### Database Changes (`DatabaseService.cs`)
Add `DocumentLibrary` table:
```sql
CREATE TABLE IF NOT EXISTS DocumentLibrary (
    Id TEXT PRIMARY KEY,
    FileName TEXT NOT NULL,
    FilePath TEXT NOT NULL,
    FileType TEXT,
    FileSize INTEGER,
    Tags TEXT,
    Category TEXT DEFAULT 'Uncategorized',
    IndexedAt TEXT,
    ChunkCount INTEGER DEFAULT 0,
    Summary TEXT
);
```

### Service Enhancements (`DocumentService.cs`)
- Persistent document indexing (currently appears per-session)
- Background re-indexing when documents change
- Cross-document keyword search
- Auto-generate document summary on import using loaded LLM
- Support for more file types: XLSX, PPTX, HTML, MD

## Files to Create/Modify
- **[MODIFY]** `Views/DocumentView.xaml` - add library management UI
- **[MODIFY]** `ViewModels/DocumentViewModel.cs` - add library management logic
- **[MODIFY]** `Services/DocumentService.cs` - persistent indexing, search
- **[MODIFY]** `Services/DatabaseService.cs` - add DocumentLibrary table
- **[NEW]** `Models/DocumentLibraryItem.cs`

## Acceptance Criteria
- [ ] Documents organized in categories with custom tags
- [ ] Full-text search across all indexed documents
- [ ] Quick "Chat with document" action from library
- [ ] Documents and indexes persist across app restarts
- [ ] Bulk operations (multi-select delete, re-index)
- [ ] Auto-summary generated on document import
