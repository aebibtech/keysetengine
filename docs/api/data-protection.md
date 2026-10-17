# KeysetEngine.DataProtection API Reference

Namespace: `KeysetEngine.DataProtection`

---

## Interfaces

### `ICursorProtector`
Abstraction for encrypting and decrypting keyset cursor tokens.

```csharp
namespace KeysetEngine.Core;

public interface ICursorProtector
{
    string Protect(KeysetCursorToken token, string entityPurpose);
    KeysetCursorToken? Unprotect(string? opaqueCursor, string entityPurpose);
}
```

---

## Implementations

### `DataProtectionCursorProtector`
Protects and unprotects cursor tokens using ASP.NET Core Data Protection (`IDataProtectionProvider`).

```csharp
public sealed class DataProtectionCursorProtector : ICursorProtector
{
    public DataProtectionCursorProtector(IDataProtectionProvider provider);

    public string Protect(KeysetCursorToken token, string entityPurpose);
    public KeysetCursorToken? Unprotect(string? opaqueCursor, string entityPurpose);
}
```

#### Behavior
- Uses sub-purpose hierarchy: `provider.CreateProtector("KeysetEngine", entityPurpose, "v1")`.
- Resiliently catches `CryptographicException`, `FormatException`, and `JsonException`, returning `null` rather than throwing errors.

---

### `Base64UrlCursorProtector`
Lightweight, non-cryptographic implementation available in `KeysetEngine.Core` for testing.

```csharp
public sealed class Base64UrlCursorProtector : ICursorProtector
{
    public static Base64UrlCursorProtector Instance { get; }
}
```

---

## Dependency Injection Extensions

### `KeysetDataProtectionServiceCollectionExtensions`

```csharp
public static IServiceCollection AddKeysetDataProtection(this IServiceCollection services);
```

Registers `DataProtectionCursorProtector` as a singleton implementation of `ICursorProtector`.
Requires `services.AddDataProtection()` to be configured.
