using System.Collections.Generic;
using JetBrains.Annotations;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     The items that a <see cref="VirtualizingItemsControl" /> can show, and the
///     position of the first one in the whole list.
/// </summary>
/// <remarks>
///     Make this value from one cell of the view model, so that the offset and the
///     items always belong together. For example, lift the cell of the offset of a
///     <c>Slice</c> with the items of the <c>Map</c> that follows it.
/// </remarks>
/// <param name="Offset">The position of the first item in the whole list.</param>
/// <param name="Items">
///     The items, in sequence. The item at index <c>i</c> shows at position
///     <c>Offset + i</c>.
/// </param>
[PublicAPI]
public sealed record VirtualRows(int Offset, IReadOnlyList<object?> Items)
{
    /// <summary>
    ///     Gets the rows with no item.
    /// </summary>
    public static VirtualRows Empty { get; } = new(Offset: 0, Items: []);
}
