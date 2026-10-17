namespace KeysetEngine.Core;

/// <summary>
/// Specifies the direction of pagination navigation.
/// </summary>
public enum PageDirection
{
    /// <summary>
    /// Navigates forward from the current cursor boundary.
    /// </summary>
    Forward = 0,

    /// <summary>
    /// Navigates backward from the current cursor boundary.
    /// </summary>
    Backward = 1
}
