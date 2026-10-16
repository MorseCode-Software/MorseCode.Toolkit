using JetBrains.Annotations;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     A run of positions in a list.
/// </summary>
/// <param name="First">The position of the first item in the run.</param>
/// <param name="Count">The number of items in the run.</param>
[PublicAPI]
// ReSharper disable once InheritdocConsiderUsage - The summary of ValueType does not say what a run is.
public readonly record struct ItemRange(int First, int Count)
{
    /// <summary>
    ///     Gets the run with no item.
    /// </summary>
    public static ItemRange Empty { get; } = new(First: 0, Count: 0);
}
