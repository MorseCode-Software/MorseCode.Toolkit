using System.Collections.Generic;
using SodaFlow;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     A source with the type of its rows. It has the values of an <see cref="IVirtualItemsSource" />,
///     and the cells that the view model reads.
/// </summary>
/// <typeparam name="TRow">The type of a row.</typeparam>
// ReSharper disable once InheritdocConsiderUsage - The summary of IVirtualItemsSource does not say how this class makes the source.
internal sealed class VirtualItemsSource<TRow>(
    in IVirtualItemsSource values,
    in Cell<IReadOnlyList<TRow>> constructedRows,
    in Cell<IReadOnlyList<TRow>> visibleRows,
    in Cell<double> firstVisibilityAmount,
    in Cell<double> lastVisibilityAmount,
    in Cell<ReportedRange> reported) : IVirtualItemsSource<TRow>
    where TRow : class
{
    /// <inheritdoc />
    public IOneWayBindableValue<VirtualRows> Rows { get; } = values.Rows;

    /// <inheritdoc />
    public IBindableAction<ReportedRange> RangeReported { get; } = values.RangeReported;

    /// <inheritdoc />
    public IOneWayBindableValue<Maybe<int>> ScrollRequest { get; } = values.ScrollRequest;

    /// <inheritdoc />
    public IBindableAction<int> ScrollHandled { get; } = values.ScrollHandled;

    /// <inheritdoc />
    public Cell<IReadOnlyList<TRow>> ConstructedRows { get; } = constructedRows;

    /// <inheritdoc />
    public Cell<IReadOnlyList<TRow>> VisibleRows { get; } = visibleRows;

    /// <inheritdoc />
    public Cell<double> FirstVisibilityAmount { get; } = firstVisibilityAmount;

    /// <inheritdoc />
    public Cell<double> LastVisibilityAmount { get; } = lastVisibilityAmount;

    /// <inheritdoc />
    public Cell<ReportedRange> Reported { get; } = reported;
}
