namespace MorseCode.Avalonia.Controls;

/// <summary>
///     The item at a position that the window does not cover yet.
/// </summary>
internal sealed class RowPlaceholder
{
    /// <summary>
    ///     Gets the one placeholder. A placeholder has no data, thus one object serves
    ///     for all positions.
    /// </summary>
    public static RowPlaceholder Instance { get; } = new();

    private RowPlaceholder()
    {
    }
}
