$env:GITHUB_TOKEN = ''
$env:GH_TOKEN = ''
$repo = "avikeid2007/KaiROS-AI"
$base = "d:\source\avikeid2007\KaiROS-AI\.github\ISSUES\bodies"

$issues = @(
    @{ title = "Feature: AI Memory - Persistent Context Across Sessions"; file = "01-ai-memory.md"; labels = "feature,tier-1-high-impact,WinUI" },
    @{ title = "Feature: Voice Input (Speech-to-Text) with Local Whisper"; file = "02-voice-input.md"; labels = "feature,tier-1-high-impact,WinUI" },
    @{ title = "Feature: Text-to-Speech (TTS) Voice Output"; file = "03-tts.md"; labels = "feature,tier-1-high-impact,WinUI" },
    @{ title = "Feature: Deep Research - Multi-Step Web Research with Reports"; file = "04-deep-research.md"; labels = "feature,tier-1-high-impact,WinUI" },
    @{ title = "Feature: Agent Mode - Tool-Using AI for Multi-Step Tasks"; file = "05-agent-mode.md"; labels = "feature,tier-1-high-impact,WinUI" },
    @{ title = "Feature: YouTube Video Summarization"; file = "06-youtube.md"; labels = "feature,tier-2-productivity,WinUI" },
    @{ title = "Feature: Side-by-Side Model Comparison"; file = "07-model-compare.md"; labels = "feature,tier-2-productivity,WinUI" },
    @{ title = "Feature: Backup and Restore - Data Export/Import"; file = "08-backup-restore.md"; labels = "feature,tier-2-productivity,WinUI" },
    @{ title = "Feature: Notes System with Markdown Editor and AI Assist"; file = "09-notes.md"; labels = "feature,tier-2-productivity,WinUI" },
    @{ title = "Feature: Task Management with AI Extraction"; file = "10-tasks.md"; labels = "feature,tier-2-productivity,WinUI" },
    @{ title = "Feature: Document Management Library"; file = "11-doc-library.md"; labels = "feature,tier-3-advanced,WinUI" },
    @{ title = "Feature: Semantic Search with Embeddings"; file = "12-embeddings.md"; labels = "feature,tier-3-advanced,WinUI" },
    @{ title = "Feature: Skills / Prompt Template System"; file = "13-skills.md"; labels = "feature,tier-3-advanced,WinUI" },
    @{ title = "Feature: AI Image Generation Integration"; file = "14-image-gen.md"; labels = "feature,tier-3-advanced,WinUI" },
    @{ title = "Feature: Sandboxed Shell/Terminal Access"; file = "15-shell.md"; labels = "feature,tier-3-advanced,WinUI" },
    @{ title = "Feature: MCP (Model Context Protocol) Client Support"; file = "16-mcp.md"; labels = "feature,tier-3-advanced,WinUI" },
    @{ title = "Feature: Calendar Integration (Google Calendar / CalDAV)"; file = "17-calendar.md"; labels = "feature,tier-3-advanced,WinUI" },
    @{ title = "Feature: Webhook System for External Automation"; file = "18-webhooks.md"; labels = "feature,tier-3-advanced,WinUI" },
    @{ title = "Feature: Multi-User Authentication for REST API"; file = "19-multi-user-auth.md"; labels = "feature,tier-3-advanced,WinUI" },
    @{ title = "Feature: Email Integration with AI-Powered Inbox"; file = "20-email.md"; labels = "feature,tier-3-advanced,WinUI" }
)

foreach ($issue in $issues) {
    $filepath = Join-Path $base $issue.file
    Write-Host "Creating: $($issue.title)..."
    gh issue create --repo $repo --title $issue.title --body-file $filepath --label $issue.labels
    Start-Sleep -Seconds 2
}

Write-Host "Done! All 20 issues created."
