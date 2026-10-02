## Summary
Add optional multi-user authentication to the REST API server - JWT-based auth with user accounts and API tokens for secure access control.

## Motivation
Enables sharing a KaiROS instance in enterprise or family settings. Protects the REST API from unauthorized access when exposed on a network.

## Detailed Requirements

### Auth Service: `AuthService.cs`
```csharp
public interface IAuthService
{
    bool IsAuthEnabled { get; }
    Task<AuthResult> LoginAsync(string username, string password);
    Task<AuthResult> RegisterAsync(string username, string password, string role = "user");
    Task<string> GenerateApiTokenAsync(string userId, string name);
    Task RevokeApiTokenAsync(string tokenId);
    Task<List<UserAccount>> GetUsersAsync();
    Task DeleteUserAsync(string userId);
    bool ValidateToken(string token, out ClaimsPrincipal principal);
}

public class UserAccount
{
    public string Id { get; set; }
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    public string Role { get; set; }  // "admin" or "user"
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class AuthResult
{
    public bool Success { get; set; }
    public string Token { get; set; }
    public string Error { get; set; }
    public UserAccount User { get; set; }
}
```

- Password hashing with bcrypt (use `BCrypt.Net-Next` NuGet)
- JWT token generation with configurable expiry
- API token support (long-lived tokens for programmatic access)
- Role-based access: admin (full access), user (chat/models only)

### API Changes (`ApiServer.cs`)
New endpoints:
- `POST /api/auth/login` - returns JWT
- `POST /api/auth/register` - create account (admin only after first user)
- `POST /api/auth/tokens` - generate API token
- `DELETE /api/auth/tokens/{id}` - revoke API token
- `GET /api/auth/users` - list users (admin only)

All existing endpoints require Bearer token when auth is enabled. Auth middleware checks JWT or API token.

### Settings (`SettingsView.xaml`)
- "API Authentication" section (under existing API Server settings):
  - Toggle: Enable/Disable authentication
  - First-time setup: create admin account dialog
  - User management: list users, add/remove (admin only)
  - API tokens: generate, list, revoke

### Database (`DatabaseService.cs`)
Add tables:
```sql
CREATE TABLE IF NOT EXISTS Users (Id TEXT PRIMARY KEY, Username TEXT UNIQUE, PasswordHash TEXT, Role TEXT, CreatedAt TEXT, LastLoginAt TEXT);
CREATE TABLE IF NOT EXISTS ApiTokens (Id TEXT PRIMARY KEY, UserId TEXT, Name TEXT, TokenHash TEXT, CreatedAt TEXT, LastUsedAt TEXT, FOREIGN KEY(UserId) REFERENCES Users(Id));
```

### NuGet Dependencies
- `BCrypt.Net-Next` for password hashing
- `System.IdentityModel.Tokens.Jwt` for JWT creation/validation

## Files to Create/Modify
- **[NEW]** `Services/AuthService.cs` + `IAuthService.cs`
- **[NEW]** `Models/AuthModels.cs` (UserAccount, AuthResult, ApiToken)
- **[MODIFY]** `Services/ApiServer.cs` - add auth middleware + auth endpoints
- **[MODIFY]** `Services/DatabaseService.cs` - add Users and ApiTokens tables
- **[MODIFY]** `Views/SettingsView.xaml` - auth settings section
- **[MODIFY]** `ViewModels/SettingsViewModel.cs` - auth management commands
- **[MODIFY]** `KaiROS.AI.WinUI.csproj` - add BCrypt.Net-Next NuGet

## Acceptance Criteria
- [ ] Optional auth toggle in Settings (disabled by default)
- [ ] User registration and login with hashed passwords
- [ ] JWT token issued on login with configurable expiry
- [ ] API token support for programmatic access
- [ ] All API endpoints protected when auth is enabled
- [ ] Admin can manage users and revoke tokens
- [ ] First user registered becomes admin automatically
