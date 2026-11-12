using JetBrains.Annotations;

namespace MorseCode.Controls.UI.Avalonia;

/// <summary>
///     The report of a <see cref="VirtualizingItemsControl" /> to its source. It has the positions
///     that show, and how much of the first and of the last of them shows. It also has the positions
///     that the control wants rows for.
/// </summary>
/// <remarks>
///     <para>
///         The parts of this value come in one write, thus they are always from the same instant.
///     </para>
///     <para>
///         The two ranges are inside the list as the control knew it at the report. The count of
///         the view model can be different by then. Thus, a view model that builds its own rows
///         stops at its own count.
///     </para>
///     <para>
///         The rows are in one line in the direction of the scroll. Thus, each row between the
///         first and the last shows in full, and only the two amounts are necessary. The viewport
///         is the part of the list that no control above it clips. A window that covers the list
///         does not change an amount.
///     </para>
///     <para>
///         The control makes a new value at each step of a scroll, because the amounts change
///         then. <see cref="Constructed" /> changes less often. Thus, a view model that builds its
///         own rows calms <see cref="Constructed" /> before it builds from it.
///     </para>
/// </remarks>
/// <param name="Visible">
///     The positions that show. A row that shows in part is one of them. A row that a control above
///     the list hides is not, although the panel can keep a container for it.
/// </param>
/// <param name="Constructed">
///     The positions that the control wants rows for. These are the positions of
///     <paramref name="Visible" />, with more positions on each side so that a scroll finds its rows.
///     The control sets how many.
/// </param>
/// <param name="FirstVisibilityAmount">
///     How much of the row at the first position of <paramref name="Visible" /> shows. It is 1.0 for
///     all of the row, and less for a row that the start of the viewport cuts. It is the part of the
///     row, in the direction of the scroll, that is in the viewport. With no position that shows, it
///     is 0.0.
/// </param>
/// <param name="LastVisibilityAmount">
///     How much of the row at the last position of <paramref name="Visible" /> shows, as
///     <paramref name="FirstVisibilityAmount" /> does for the first. With one position that shows, the
///     two amounts are the same number.
/// </param>
[PublicAPI]
// ReSharper disable once InheritdocConsiderUsage - The summary of ValueType does not say what the control reports.
public readonly record struct ReportedRange(
    ItemRange Visible,
    ItemRange Constructed,
    double FirstVisibilityAmount,
    double LastVisibilityAmount)
{
    /// <summary>
    ///     Gets the report with no position.
    /// </summary>
    public static ReportedRange Empty { get; } =
        new(
            Visible: ItemRange.Empty,
            Constructed: ItemRange.Empty,
            FirstVisibilityAmount: 0.0,
            LastVisibilityAmount: 0.0);

    // The number of positions after the end of the list that the control also wants rows for, when
    // the list gets them. A range in this value cannot hold them, because each range stays inside the
    // list. Only the slice of a collection source reads this number.
    internal int Ahead { get; init; }
}
