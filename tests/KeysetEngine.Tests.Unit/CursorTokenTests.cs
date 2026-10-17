using System.Text.Json;
using FluentAssertions;
using KeysetEngine.Core;
using Xunit;

namespace KeysetEngine.Tests.Unit;

public class CursorTokenTests
{
    [Fact]
    public void Base64UrlSerializer_Roundtrip_RestoresIdenticalToken()
    {
        // Arrange
        var token = new KeysetCursorToken(
            SortValueJson: JsonSerializer.Serialize("Alpha"),
            TieBreakerValueJson: JsonSerializer.Serialize(42),
            IsAscending: true,
            SearchHash: "a1b2c3d4e5f60718"
        );

        // Act
        string cursor = Base64UrlCursorSerializer.Serialize(token);
        KeysetCursorToken? deserialized = Base64UrlCursorSerializer.Deserialize(cursor);

        // Assert
        cursor.Should().NotBeNullOrWhiteSpace();
        cursor.Should().NotContain("+").And.NotContain("/").And.NotContain("=");

        deserialized.Should().NotBeNull();
        deserialized!.SortValueJson.Should().Be(token.SortValueJson);
        deserialized.TieBreakerValueJson.Should().Be(token.TieBreakerValueJson);
        deserialized.IsAscending.Should().Be(token.IsAscending);
        deserialized.SearchHash.Should().Be(token.SearchHash);
    }

    [Fact]
    public void Base64UrlSerializer_WithDateTimeOffsetAndGuid_SerializesCleanly()
    {
        // Arrange
        var date = new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
        var id = Guid.NewGuid();

        var token = new KeysetCursorToken(
            SortValueJson: JsonSerializer.Serialize(date),
            TieBreakerValueJson: JsonSerializer.Serialize(id),
            IsAscending: false,
            SearchHash: null
        );

        // Act
        string cursor = Base64UrlCursorSerializer.Serialize(token);
        KeysetCursorToken? deserialized = Base64UrlCursorSerializer.Deserialize(cursor);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.IsAscending.Should().BeFalse();
        deserialized.SearchHash.Should().BeNull();

        DateTimeOffset restoredDate = JsonSerializer.Deserialize<DateTimeOffset>(deserialized.SortValueJson);
        Guid restoredId = JsonSerializer.Deserialize<Guid>(deserialized.TieBreakerValueJson);

        restoredDate.Should().Be(date);
        restoredId.Should().Be(id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Base64UrlSerializer_Deserialize_NullOrWhitespace_ReturnsNull(string? invalidCursor)
    {
        // Act
        KeysetCursorToken? result = Base64UrlCursorSerializer.Deserialize(invalidCursor);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("not-valid-base64!@#$")]
    [InlineData("aW52YWxpZA")] // "invalid" string in Base64 (not valid KeysetCursorToken JSON)
    [InlineData("{}")] // Invalid Base64
    public void Base64UrlSerializer_Deserialize_MalformedInput_ReturnsNull(string malformedCursor)
    {
        // Act
        KeysetCursorToken? result = Base64UrlCursorSerializer.Deserialize(malformedCursor);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Base64UrlSerializer_Serialize_NullToken_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => Base64UrlCursorSerializer.Serialize(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Base64UrlCursorProtector_ImplementsICursorProtector_Correctly()
    {
        // Arrange
        ICursorProtector protector = new Base64UrlCursorProtector();
        var token = new KeysetCursorToken(
            SortValueJson: "\"test\"",
            TieBreakerValueJson: "100",
            IsAscending: true,
            SearchHash: null
        );

        // Act
        string cursor = protector.Protect(token, "TestEntity");
        KeysetCursorToken? restored = protector.Unprotect(cursor, "TestEntity");

        // Assert
        restored.Should().NotBeNull();
        restored!.SortValueJson.Should().Be(token.SortValueJson);
        restored.TieBreakerValueJson.Should().Be(token.TieBreakerValueJson);
    }
}
