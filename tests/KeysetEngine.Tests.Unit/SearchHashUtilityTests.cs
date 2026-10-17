using FluentAssertions;
using KeysetEngine.Core;
using Xunit;

namespace KeysetEngine.Tests.Unit;

public class SearchHashUtilityTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ComputeHash_NullOrWhitespace_ReturnsNull(string? search)
    {
        // Act
        string? hash = SearchHashUtility.ComputeHash(search);

        // Assert
        hash.Should().BeNull();
    }

    [Fact]
    public void ComputeHash_Deterministic_ProducesConsistentCompactHash()
    {
        // Act
        string? hash1 = SearchHashUtility.ComputeHash("laptop");
        string? hash2 = SearchHashUtility.ComputeHash("laptop");

        // Assert
        hash1.Should().NotBeNull();
        hash1.Should().Be(hash2);
        hash1!.Length.Should().Be(16); // 16 hex chars (8 bytes)
    }

    [Fact]
    public void ComputeHash_TrimsWhitespace()
    {
        // Act
        string? hashUntrimmed = SearchHashUtility.ComputeHash("  keyboard  ");
        string? hashTrimmed = SearchHashUtility.ComputeHash("keyboard");

        // Assert
        hashUntrimmed.Should().Be(hashTrimmed);
    }

    [Fact]
    public void ComputeHash_DifferentTerms_ProducesDifferentHashes()
    {
        // Act
        string? hash1 = SearchHashUtility.ComputeHash("apple");
        string? hash2 = SearchHashUtility.ComputeHash("orange");

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Matches_MatchingSearchAndHash_ReturnsTrue()
    {
        // Arrange
        string search = "monitor";
        string? expectedHash = SearchHashUtility.ComputeHash(search);

        // Act
        bool matches = SearchHashUtility.Matches(search, expectedHash);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact]
    public void Matches_BothNullOrEmpty_ReturnsTrue()
    {
        // Act & Assert
        SearchHashUtility.Matches(null, null).Should().BeTrue();
        SearchHashUtility.Matches("   ", null).Should().BeTrue();
    }

    [Fact]
    public void Matches_MismatchedSearch_ReturnsFalse()
    {
        // Arrange
        string? hashForOldSearch = SearchHashUtility.ComputeHash("headphones");

        // Act & Assert
        SearchHashUtility.Matches("speakers", hashForOldSearch).Should().BeFalse();
        SearchHashUtility.Matches(null, hashForOldSearch).Should().BeFalse();
        SearchHashUtility.Matches("headphones", null).Should().BeFalse();
    }
}
