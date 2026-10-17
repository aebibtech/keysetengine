using FluentAssertions;
using KeysetEngine.Core;
using KeysetEngine.DataProtection;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace KeysetEngine.Tests.Unit;

public class DataProtectionCursorProtectorTests
{
    private readonly EphemeralDataProtectionProvider _provider = new();

    private DataProtectionCursorProtector CreateProtector() => new(_provider);

    [Fact]
    public void ProtectAndUnprotect_ValidToken_RoundtripsSuccessfully()
    {
        // Arrange
        var protector = CreateProtector();
        var originalToken = new KeysetCursorToken(
            SortValueJson: "\"Order-999\"",
            TieBreakerValueJson: "54321",
            IsAscending: true,
            SearchHash: "c0ffee1234567890"
        );

        // Act
        string opaqueCursor = protector.Protect(originalToken, "Order");
        KeysetCursorToken? restoredToken = protector.Unprotect(opaqueCursor, "Order");

        // Assert
        opaqueCursor.Should().NotBeNullOrWhiteSpace();
        opaqueCursor.Should().NotContain("+").And.NotContain("/").And.NotContain("=");

        restoredToken.Should().NotBeNull();
        restoredToken!.SortValueJson.Should().Be(originalToken.SortValueJson);
        restoredToken.TieBreakerValueJson.Should().Be(originalToken.TieBreakerValueJson);
        restoredToken.IsAscending.Should().Be(originalToken.IsAscending);
        restoredToken.SearchHash.Should().Be(originalToken.SearchHash);
    }

    [Fact]
    public void Unprotect_PurposeMismatch_ReturnsNull()
    {
        // Arrange
        var protector = CreateProtector();
        var token = new KeysetCursorToken(
            SortValueJson: "123.45",
            TieBreakerValueJson: "\"guid-id\"",
            IsAscending: false,
            SearchHash: null
        );

        // Act - Protected with purpose "Order", but unprotected with "Customer"
        string opaqueCursor = protector.Protect(token, "Order");
        KeysetCursorToken? restoredToken = protector.Unprotect(opaqueCursor, "Customer");

        // Assert - Must return null without throwing CryptographicException
        restoredToken.Should().BeNull();
    }

    [Fact]
    public void Unprotect_TamperedCiphertext_ReturnsNullGracefully()
    {
        // Arrange
        var protector = CreateProtector();
        var token = new KeysetCursorToken(
            SortValueJson: "\"SensitivePrice\"",
            TieBreakerValueJson: "1",
            IsAscending: true,
            SearchHash: null
        );

        string opaqueCursor = protector.Protect(token, "Catalog");

        // Tamper with bytes: flip characters in the middle
        char[] chars = opaqueCursor.ToCharArray();
        chars[chars.Length / 2] = chars[chars.Length / 2] == 'A' ? 'B' : 'A';
        string tamperedCursor = new(chars);

        // Act
        KeysetCursorToken? restored = protector.Unprotect(tamperedCursor, "Catalog");

        // Assert
        restored.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Unprotect_NullOrWhitespaceCursor_ReturnsNull(string? cursor)
    {
        // Arrange
        var protector = CreateProtector();

        // Act
        KeysetCursorToken? result = protector.Unprotect(cursor, "AnyPurpose");

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Unprotect_NullOrWhitespacePurpose_ReturnsNull(string? purpose)
    {
        // Arrange
        var protector = CreateProtector();
        var token = new KeysetCursorToken("\"value\"", "1", true, null);
        string cursor = protector.Protect(token, "ValidPurpose");

        // Act
        KeysetCursorToken? result = protector.Unprotect(cursor, purpose!);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("random-garbage-string-12345")]
    [InlineData("!@#$%^&*()_+=")]
    [InlineData("YWJj")] // "abc"
    public void Unprotect_MalformedPayload_ReturnsNull(string malformedCursor)
    {
        // Arrange
        var protector = CreateProtector();

        // Act
        KeysetCursorToken? result = protector.Unprotect(malformedCursor, "TestPurpose");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Protect_NullToken_ThrowsArgumentNullException()
    {
        // Arrange
        var protector = CreateProtector();

        // Act
        Action act = () => protector.Protect(null!, "Order");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Protect_NullOrWhitespacePurpose_ThrowsArgumentException(string? purpose)
    {
        // Arrange
        var protector = CreateProtector();
        var token = new KeysetCursorToken("\"val\"", "1", true, null);

        // Act
        Action act = () => protector.Protect(token, purpose!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
