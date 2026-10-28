using System;
using JetBrains.Annotations;
using MorseCode.Mvvm;
using SodaFlow;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Collections;
using SodaFlow.Functional;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     Makes the <see cref="IVirtualItemsSource" /> that a view model gives to a
///     <see cref="VirtualizingItemsControl" />.
/// </summary>
[PublicAPI]
public static class VirtualItemsSourceExtensions
{
    /// <summary>
    ///     Makes the source of a collection. A <c>Slice</c> of the collection follows the range that the
    ///     control reports, and a <c>Map</c> after it builds a row for each key in the slice.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The count of the control is the number of keys in the collection. The rows and their
    ///         offset come from one cell, so that they always agree. The request is the one that
    ///         <c>CreateScrollRequest</c> makes.
    ///     </para>
    ///     <para>
    ///         The <c>Map</c> keeps a row for a key that left the slice until more than
    ///         <paramref name="retainedBeyondTheView" /> keys have left. Then it calls
    ///         <paramref name="onEvicted" /> for the oldest. When you dispose of the view model, the output
    ///         releases the rows that are still in use.
    ///     </para>
    /// </remarks>
    /// <typeparam name="TKey">The type of the keys of the collection.</typeparam>
    /// <typeparam name="TIdentity">The type of the immutable part of an item.</typeparam>
    /// <typeparam name="TState">The type of the mutable part of an item.</typeparam>
    /// <typeparam name="TRow">
    ///     The type of the row for a key. It is a class so that the rows fit <see cref="VirtualRows" />
    ///     without a copy.
    /// </typeparam>
    /// <param name="output">The output of the view model. The source uses its factory, and the output releases the rows.</param>
    /// <param name="collection">The collection to show.</param>
    /// <param name="project">Builds the row for a key. It runs one time for each key that the map keeps.</param>
    /// <param name="scrollRequests">The positions to scroll to.</param>
    /// <param name="clears">The events that withdraw a request that the control did not use yet. None by default.</param>
    /// <param name="retainedBeyondTheView">The number of keys that left the slice, for which the map keeps a row.</param>
    /// <param name="onEvicted">Receives a row that the map removed. Release its resources here.</param>
    /// <returns>The source. Expose it on the view model, and bind <see cref="VirtualizingItemsControl.Source" /> to it.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" />, <paramref name="collection" />, <paramref name="project" />,
    ///     or <paramref name="scrollRequests" /> is null.
    /// </exception>
    public static IVirtualItemsSource CreateVirtualItemsSource<TKey, TIdentity, TState, TRow>(
        this ViewModelBase.IOutput output,
        ReactiveCollection<TKey, TIdentity, TState> collection,
        Func<TKey, TRow> project,
        Stream<int> scrollRequests,
        Stream<Unit>? clears = null,
        int retainedBeyondTheView = MappedItems.DefaultRetainedBeyondTheView,
        Action<TRow>? onEvicted = null)
        where TKey : notnull
        where TIdentity : notnull
        where TRow : class
    {
        ArgumentNullException.ThrowIfNull(argument: output);
        ArgumentNullException.ThrowIfNull(argument: collection);
        ArgumentNullException.ThrowIfNull(argument: project);
        ArgumentNullException.ThrowIfNull(argument: scrollRequests);

        // One transaction, so that the count, the slice, and the rows start from one instant.
        return Transaction.Run(
            f: () =>
            {
                CellSink<ItemRange> range = Cell.CreateSink(initialValue: ItemRange.Empty);

                // The offset and the limit are two views of the one cell. Thus, the slice always reads
                // both values of the same range.
                Cell<int> offset = range.Map(f: static visible => visible.First);
                Cell<int> limit = range.Map(f: static visible => visible.Count);

                // The output releases the rows with the view model. A caller cannot forget it.
                MappedItems<TRow> mapped =
                    output.AddDisposable(
                        disposable: collection.Slice(offsetCell: offset, limitCell: limit)
                            .Map(
                                project: project,
                                retainedBeyondTheView: retainedBeyondTheView,
                                onEvicted: onEvicted));

                // The offset and the rows are one value. Thus, they always agree.
                Cell<VirtualRows> rows =
                    offset.Lift(
                        c2: mapped.Items,
                        f: static (first, items) => new VirtualRows(Offset: first, Items: items));

                Cell<int> totalCount = collection.KeysCell.Map(f: static keys => keys.Count);

                return Build(
                    bindableFactory: output.BindableFactory,
                    totalCount: totalCount,
                    rows: rows,
                    visibleRange: range,
                    scrollRequests: scrollRequests,
                    clears: clears);
            });
    }

    /// <summary>
    ///     Makes the source from cells and sinks that the view model already has. Use this when the rows
    ///     do not come from a <c>ReactiveCollection</c>.
    /// </summary>
    /// <remarks>
    ///     The view model reads the range from <paramref name="visibleRange" /> and answers in
    ///     <paramref name="rows" />. Make the rows and their offset from one cell, so that they always
    ///     agree.
    /// </remarks>
    /// <param name="output">The output of the view model. The source makes its values with the factory of the output.</param>
    /// <param name="totalCount">The number of positions in the whole list.</param>
    /// <param name="rows">The rows for some of the positions, with the position of the first.</param>
    /// <param name="visibleRange">The cell that receives the range that the control reports.</param>
    /// <param name="scrollRequests">The positions to scroll to.</param>
    /// <param name="clears">The events that withdraw a request that the control did not use yet. None by default.</param>
    /// <returns>The source. Expose it on the view model, and bind <see cref="VirtualizingItemsControl.Source" /> to it.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" />, <paramref name="totalCount" />, <paramref name="rows" />,
    ///     <paramref name="visibleRange" />, or <paramref name="scrollRequests" /> is null.
    /// </exception>
    public static IVirtualItemsSource CreateVirtualItemsSource(
        this ViewModelBase.IOutput output,
        Cell<int> totalCount,
        Cell<VirtualRows> rows,
        CellSink<ItemRange> visibleRange,
        Stream<int> scrollRequests,
        Stream<Unit>? clears = null)
    {
        ArgumentNullException.ThrowIfNull(argument: output);
        ArgumentNullException.ThrowIfNull(argument: totalCount);
        ArgumentNullException.ThrowIfNull(argument: rows);
        ArgumentNullException.ThrowIfNull(argument: visibleRange);
        ArgumentNullException.ThrowIfNull(argument: scrollRequests);

        return Transaction.Run(
            f: () =>
                Build(
                    bindableFactory: output.BindableFactory,
                    totalCount: totalCount,
                    rows: rows,
                    visibleRange: visibleRange,
                    scrollRequests: scrollRequests,
                    clears: clears));
    }

    private static VirtualItemsSource Build(
        IBindableFactory bindableFactory,
        Cell<int> totalCount,
        Cell<VirtualRows> rows,
        CellSink<ItemRange> visibleRange,
        Stream<int> scrollRequests,
        Stream<Unit>? clears)
    {
        ScrollRequestBindables scroll =
            bindableFactory.CreateScrollRequest(
                requests: scrollRequests,
                clears: clears ?? Stream.Never<Unit>());

        return new VirtualItemsSource(
            totalCount: bindableFactory.CreateOneWay(cell: totalCount),
            rows: bindableFactory.CreateOneWay(cell: rows),
            visibleRange: bindableFactory.CreateOneWayToSource(sink: visibleRange),
            scrollRequest: scroll.Request,
            scrollHandled: scroll.Handled);
    }
}
