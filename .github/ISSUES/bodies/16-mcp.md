## Summary
Add Model Context Protocol (MCP) client support - connect to external MCP tool servers to extend KaiROS capabilities with third-party tools.

## Motivation
MCP is the emerging standard for AI tool integration (created by Anthropic). Supporting it future-proofs KaiROS and allows connection to a growing ecosystem of community MCP servers.

## Detailed Requirements

### MCP Client Service: `McpClientService.cs`
```csharp
public interface IMcpClientService
{
    Task<List<McpServerInfo>> GetConfiguredServersAsync();
    Task ConnectAsync(string serverId);
    Task DisconnectAsync(string serverId);
    Task<List<McpTool>> DiscoverToolsAsync(string serverId);
    Task<McpToolResult> ExecuteToolAsync(string serverId, string toolName, Dictionary<string, object> args);
    event EventHandler<McpConnectionStatus> ConnectionStatusChanged;
}

public class McpServerConfig
{
    public string Id { get; set; }
    public string Name { get; set; }
    public McpTransport Transport { get; set; }  // Stdio, SSE
    public string Command { get; set; }          // For stdio: command to run
    public string[] Args { get; set; }           // For stdio: command arguments
    public string Url { get; set; }              // For SSE: server URL
    public Dictionary<string, string> Env { get; set; }  // Environment variables
}

public enum McpTransport { Stdio, SSE }
```

- Implement MCP client protocol (JSON-RPC 2.0 over stdio or SSE)
- Connect to user-configured MCP servers
- Discover available tools via `tools/list` method
- Execute tool calls via `tools/call` method and return results

### Configuration UI in Settings
- "MCP Servers" section in `SettingsView.xaml`:
  - Add/Remove MCP server connections
  - Per server: name, transport type, command/URL, environment variables
  - Connection status indicator (green dot = connected, red = disconnected)
  - "Test Connection" button
  - List of discovered tools per server

### Integration with Agent Mode
- MCP tools appear alongside built-in tools when Agent Mode is active
- Tool schemas auto-discovered from MCP server `tools/list` response
- Depends on Agent Mode feature being implemented (can be developed in parallel with stub interface)

### NuGet
- Consider using `ModelContextProtocol` NuGet package if available, or implement minimal JSON-RPC client

## Files to Create/Modify
- **[NEW]** `Services/McpClientService.cs` + `IMcpClientService.cs`
- **[NEW]** `Models/McpModels.cs` (McpServerConfig, McpTool, McpToolResult)
- **[MODIFY]** `Views/SettingsView.xaml` - MCP server configuration section
- **[MODIFY]** `ViewModels/SettingsViewModel.cs` - MCP settings and commands
- **[MODIFY]** `Services/DatabaseService.cs` - add McpServers table

## Acceptance Criteria
- [ ] Connect to MCP servers via stdio or SSE transport
- [ ] Discover and list available tools from connected servers
- [ ] Execute MCP tool calls and receive results
- [ ] Add/remove/edit servers in Settings UI
- [ ] Connection status indicator per server
- [ ] Tools usable from Agent Mode (when available)
