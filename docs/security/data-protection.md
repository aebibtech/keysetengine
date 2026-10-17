# Cryptographic Protection & Cursor Tokens

::: tip Direct Answer (Executive Summary)
`KeysetEngine.DataProtection` protects cursor tokens using **ASP.NET Core Data Protection** (AES-256-GCM / HMAC-SHA256 authenticated encryption) with purpose isolation (`KeysetEngine:{entityPurpose}:v1`). It prevents internal primary key enumeration, cross-endpoint token reuse, and data leakage. When invalid or tampered tokens are supplied, it catches cryptographic exceptions and gracefully resets pagination to Page 1 instead of throwing 500 errors.
:::

Exposing raw database keys, sequential IDs, or simple Base64-encoded timestamps in API cursor tokens presents serious security and architectural risks.

---

## Why Plain or Base64 Cursors Are Dangerous

Many naive cursor pagination implementations simply Base64-encode internal values:

```json
// Naive Base64 cursor: eyJDcmVhdGVkQXQiOiIyMDI2LTEwLTE3VDEyOjAwOjAwWiIsIklkIjoxMDA1fQ==
// Decoded: {"CreatedAt":"2026-10-17T12:00:00Z","Id":1005}
```

### Vulnerabilities:
1. **Data Leakage**: Sequential IDs reveal internal business volume, customer counts, and user registration velocity.
2. **Scraping & Enumeration**: Attackers can forge cursors to bypass rate limits or scrape specific database partitions.
3. **Cross-Endpoint Substitution**: A cursor generated for `/api/customers` could be sent to `/api/admin/audit-logs`, triggering unexpected behavior or privilege escalation.

---

## ASP.NET Core Data Protection Integration

`KeysetEngine.DataProtection` integrates directly with **ASP.NET Core Data Protection** to encrypt and cryptographically sign every cursor token using authenticated encryption (AES-256-GCM / HMAC-SHA256).

### 1. Sub-Purpose Isolation
Cursors are cryptographically scoped with hierarchical sub-purposes:

```csharp
// Internally created by DataProtectionCursorProtector
var protector = provider.CreateProtector("KeysetEngine", entityPurpose, "v1");
```

- A token generated for `Order` cannot be decrypted or used on `Customer`.
- If an entity specifies a custom tenant purpose (e.g., `Order:tenant-acme`), tokens cannot be replayed across tenants.

---

## Graceful Error Handling & Tamper Resilience

Clients may occasionally supply invalid, modified, expired, or corrupted cursor strings.

`DataProtectionCursorProtector.Unprotect()` intercepts cryptographic exceptions internally:

```csharp
public KeysetCursorToken? Unprotect(string? opaqueCursor, string entityPurpose)
{
    if (string.IsNullOrWhiteSpace(opaqueCursor)) return null;

    try
    {
        IDataProtector protector = GetProtector(entityPurpose);
        string json = protector.Unprotect(opaqueCursor);
        return JsonSerializer.Deserialize<KeysetCursorToken>(json);
    }
    catch (CryptographicException)
    {
        // Tampered, expired, or purpose-mismatched token
        return null;
    }
    catch (Exception ex) when (ex is FormatException or JsonException)
    {
        // Malformed payload
        return null;
    }
}
```

### The Result:
When `Unprotect` returns `null`, KeysetEngine treats the request as a fresh query and returns **Page 1**. The application never throws unhandled 500 errors or leaks cryptographic stack traces to API consumers.

---

## Registration & Setup

```csharp
using KeysetEngine.DataProtection;

// Standard ASP.NET Core Data Protection
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(@"/path/to/keys"))
    .SetApplicationName("KeysetEngineDemo");

// Register KeysetEngine Data Protection Protector
builder.Services.AddKeysetDataProtection();
```

---

## Testing Without Data Protection: `Base64UrlCursorProtector`

For unit tests or scenarios where ASP.NET Core Data Protection keys are unavailable, `KeysetEngine.Core` provides `Base64UrlCursorProtector`:

```csharp
using KeysetEngine.Core;

ICursorProtector protector = Base64UrlCursorProtector.Instance;
```
