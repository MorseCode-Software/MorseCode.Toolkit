using System.Collections.Generic;
using JetBrains.Annotations;
using SodaFlow;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     The source of a collection, with the rows that the view model can read. The view gets it as an
///     <see cref="IVirtualItemsSource" />, and the view model keeps it with the type of its rows.
/// </summary>
/// <typeparam name="TRow">The type of the row for a key.</typeparam>
[PublicAPI]
// ReSharper disable once InheritdocConsiderUsage - The summary of IVirtualItemsSource does not say what the view model reads.
public interface IVirtualItemsSource<TRow> : IVirtualItemsSource
    where TRow : class
{
    /// <summary>
    ///     Gets the rows that the source gives to the control, in sequence. These are the rows for
    ///     <see cref="ReportedRange.Constructed" />.
    /// </summary>
    /// <remarks>
    ///     The list is the list in <see cref="IVirtualItemsSource.Rows" />, with the type of the rows.
    ///     The source can keep more rows than these, for keys that left the range.
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
    ///     Gets the last report of the control.
    /// </summary>
    /// <remarks>
    ///     The control reports at each step of a scroll. Calm a part of the report before a heavy
    ///     calculation uses it.
    /// </remarks>
    Cell<ReportedRange> Reported { get; }
}
