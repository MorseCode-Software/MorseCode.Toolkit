using JetBrains.Annotations;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     What a <see cref="VirtualizingItemsControl" /> reports to its source: the positions that
///     show, and the positions that it wants rows for.
/// </summary>
/// <remarks>
///     <para>
///         The two ranges come in one value, thus they are always from the same instant.
///     </para>
///     <para>
///         The two ranges are inside the list as the control knew it at the report. The count of
///         the view model can be different by then. Thus, a view model that builds its own rows
///         stops at its own count.
///     </para>
/// </remarks>
/// <param name="Visible">
///     The positions that Avalonia has a container for. With the default panel, these are the
///     positions that show, and a row that shows in part is one of them.
/// </param>
/// <param name="Constructed">
///     The positions that the control wants rows for. These are the positions of
///     <paramref name="Visible" />, with more positions on each side so that a scroll finds its rows.
///     The control sets how many.
/// </param>
[PublicAPI]
// ReSharper disable once InheritdocConsiderUsage - The summary of ValueType does not say what the control reports.
public readonly record struct ReportedRange(ItemRange Visible, ItemRange Constructed)
{
    /// <summary>
    ///     Gets the report with no position.
    /// </summary>
    public static ReportedRange Empty { get; } = new(Visible: ItemRange.Empty, Constructed: ItemRange.Empty);

    // The number of positions after the end of the list that the control also wants rows for, when
    // the list gets them. A range in this value cannot hold them, because each range stays inside the
    // list. Only the slice of a collection source reads this number.
    internal int Ahead { get; init; }
}
