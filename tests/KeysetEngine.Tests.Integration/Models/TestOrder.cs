namespace KeysetEngine.Tests.Integration.Models;

/// <summary>
/// Test entity representing an e-commerce order for keyset pagination integration verification.
/// </summary>
public sealed class TestOrder
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}
