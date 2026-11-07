using System.Collections.Generic;
using JetBrains.Annotations;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     The items that a <see cref="VirtualizingItemsControl" /> can show, the
///     position of the first one in the whole list, and the length of the whole list.
/// </summary>
/// <remarks>
///     Make this value from one cell of the view model, so that the count, the
///     offset, and the items always belong together. The control then applies them
///     in one update. For example, lift the offset of a <c>Slice</c> with the items
///     of the <c>Map</c> that follows it, and with the keys of the collection.
/// </remarks>
/// <param name="TotalCount">The number of positions in the whole list.</param>
/// <param name="Offset">The position of the first item in the whole list.</param>
/// <param name="Items">
///     The items, in sequence. The item at index <c>i</c> shows at position
///     <c>Offset + i</c>. The control reads this list when it needs a row, and not when it
///     receives the value. Thus, the list must not change. Give a new list for each change.
/// </param>
[PublicAPI]
public sealed record VirtualRows(int TotalCount, int Offset, IReadOnlyList<object?> Items)
{
    /// <summary>
    ///     Gets the rows of a list with no position.
    /// </summary>
    public static VirtualRows Empty { get; } = new(TotalCount: 0, Offset: 0, Items: []);
}
