using System.Collections.Generic;
using JetBrains.Annotations;
using SodaFlow;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     A source with the rows that the view model can read. The view gets it as an
///     <see cref="IVirtualItemsSource" />, and the view model keeps it with the type of its rows.
/// </summary>
/// <remarks>
///     The source of a collection and the source of a list that has all of its rows are of this type.
/// </remarks>
/// <typeparam name="TRow">The type of a row.</typeparam>
[PublicAPI]
// ReSharper disable once InheritdocConsiderUsage - The summary of IVirtualItemsSource does not say what the view model reads.
public interface IVirtualItemsSource<TRow> : IVirtualItemsSource
    where TRow : class
{
    /// <summary>
    ///     Gets the rows at the positions that the control wants rows for, in sequence. These are the
    ///     rows for <see cref="ReportedRange.Constructed" />.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         For the source of a collection, the list is the list in
    ///         <see cref="IVirtualItemsSource.Rows" />, with the type of the rows. That source can keep
    ///         more rows than these, for keys that left the range.
    ///     </para>
    ///     <para>
    ///         For the source of a list that has all of its rows, the list is a part of the list in
    ///         <see cref="IVirtualItemsSource.Rows" />. The cell changes only when a different row is
    ///         at one of the positions.
    ///     </para>
    /// </remarks>
    Cell<IReadOnlyList<TRow>> ConstructedRows { get; }

    /// <summary>
    ///     Gets the rows at the positions that show, in sequence. These are the rows for
    ///     <see cref="ReportedRange.Visible" />.
    /// </summary>
    /// <remarks>
    ///     The cell changes only when a different row shows. A scroll that brings no new row does not
    ///     change it.
    /// </remarks>
    Cell<IReadOnlyList<TRow>> VisibleRows { get; }

    /// <summary>
    ///     Gets how much of the first of the <see cref="VisibleRows" /> shows, from 0.0 to 1.0.
    /// </summary>
    /// <remarks>
    ///     Each row between the first and the last shows in full. With one row that shows, this is the
    ///     number of <see cref="LastVisibilityAmount" />. With none, it is 0.0.
    /// </remarks>
    Cell<double> FirstVisibilityAmount { get; }

    /// <summary>
    ///     Gets how much of the last of the <see cref="VisibleRows" /> shows, from 0.0 to 1.0.
    /// </summary>
    Cell<double> LastVisibilityAmount { get; }

    /// <summary>
    ///     Gets the last report of the control.
    /// </summary>
    /// <remarks>
    ///     The control reports at each step of a scroll. Calm a part of the report before a heavy
    ///     calculation uses it.
    /// </remarks>
    Cell<ReportedRange> Reported { get; }
}
