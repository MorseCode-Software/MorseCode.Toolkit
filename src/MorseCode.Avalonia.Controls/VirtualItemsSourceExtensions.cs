using System;
using System.Collections.Generic;
using System.Linq;
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
    ///     control wants rows for, and a <c>Map</c> after it builds a row for each key in the slice.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The count of the control is the number of keys in the collection. The count, the rows,
    ///         and their offset come from one cell, so that they always agree. The request is the one
    ///         that <c>CreateScrollRequest</c> makes.
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
    /// <returns>
    ///     The source. Expose it on the view model as an <see cref="IVirtualItemsSource" />, and bind
    ///     <see cref="VirtualizingItemsControl.Source" /> to it. The view model reads its rows from it.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" />, <paramref name="collection" />, <paramref name="project" />,
    ///     or <paramref name="scrollRequests" /> is null.
    /// </exception>
    public static IVirtualItemsSource<TRow> CreateVirtualItemsSource<TKey, TIdentity, TState, TRow>(
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
                StreamSink<ReportedRange> reports = Stream.CreateSink<ReportedRange>();
                Cell<ReportedRange> range = reports.Hold(initialValue: ReportedRange.Empty);

                // The offset and the limit are two views of the one cell. Thus, the slice always reads
                // both values of the same report. The limit also has the positions that the control
                // wants after the end of the list. Thus, a key that the collection gets at its end is in
                // the slice already, and its row comes with the new count. A report that changes only
                // the positions that show does not change the slice.
                Cell<int> offset = range.Map(f: static reported => reported.Constructed.First).Calm();

                Cell<int> limit =
                    range.Map(f: static reported => reported.Constructed.Count + reported.Ahead).Calm();

                // The output releases the rows with the view model. A caller cannot forget it.
                MappedItems<TRow> mapped =
                    output.AddDisposable(
                        disposable: collection.Slice(offsetCell: offset, limitCell: limit)
                            .Map(
                                project: project,
                                retainedBeyondTheView: retainedBeyondTheView,
                                onEvicted: onEvicted));

                // The count, the offset, and the rows are one value. Thus, they always agree, and the
                // control applies them in one update.
                Cell<VirtualRows> rows =
                    offset.Lift(
                        c2: mapped.Items,
                        c3: collection.KeysCell,
                        f: static (first, items, keys) =>
                            new VirtualRows(TotalCount: keys.Count, Offset: first, Items: items));

                // The place of the rows that show in the rows of the slice.
                Cell<ItemRange> shown =
                    range.Map(
                        f: static reported =>
                            reported.Visible with { First = reported.Visible.First - reported.Constructed.First });

                return new VirtualItemsSource<TRow>(
                    values: Build(
                        bindableFactory: output.BindableFactory,
                        rows: rows,
                        reportedRanges: reports,
                        scrollRequests: scrollRequests,
                        clears: clears),
                    constructedRows: mapped.Items,
                    visibleRows: RowsAt(items: mapped.Items, place: shown),
                    firstVisibilityAmount: range.Map(f: static reported => reported.FirstVisibilityAmount).Calm(),
                    lastVisibilityAmount: range.Map(f: static reported => reported.LastVisibilityAmount).Calm(),
                    reported: range);
            });
    }

    /// <summary>
    ///     Makes the source of a collection, as
    ///     <see cref="CreateVirtualItemsSource{TKey,TIdentity,TState,TRow}(ViewModelBase.IOutput,ReactiveCollection{TKey,TIdentity,TState},Func{TKey,TRow},Stream{int},Stream{Unit},int,Action{TRow})" />
    ///     does, with scroll requests by key and not by position.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The source finds the position of a key in the collection in the transaction of the
    ///         request. Thus, the view model does not keep positions, which move when the collection
    ///         changes. A key that the collection does not have at that instant makes no request.
    ///     </para>
    ///     <para>
    ///         This method has its own name. A collection with keys of type <c>int</c> would make an
    ///         overload by position and an overload by key the same method.
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
    /// <param name="scrollRequests">The keys to scroll to.</param>
    /// <param name="clears">The events that withdraw a request that the control did not use yet. None by default.</param>
    /// <param name="retainedBeyondTheView">The number of keys that left the slice, for which the map keeps a row.</param>
    /// <param name="onEvicted">Receives a row that the map removed. Release its resources here.</param>
    /// <returns>
    ///     The source. Expose it on the view model as an <see cref="IVirtualItemsSource" />, and bind
    ///     <see cref="VirtualizingItemsControl.Source" /> to it. The view model reads its rows from it.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" />, <paramref name="collection" />, <paramref name="project" />,
    ///     or <paramref name="scrollRequests" /> is null.
    /// </exception>
    public static IVirtualItemsSource<TRow> CreateVirtualItemsSourceByKey<TKey, TIdentity, TState, TRow>(
        this ViewModelBase.IOutput output,
        ReactiveCollection<TKey, TIdentity, TState> collection,
        Func<TKey, TRow> project,
        Stream<TKey> scrollRequests,
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

        return Transaction.Run(
            f: () =>
            {
                // The position of the key at the instant of the request. A key that is not in the
                // collection gives none, and the filter drops it.
                Stream<int> positions =
                    scrollRequests.Snapshot(c: collection.KeysCell, f: static (key, keys) => keys.IndexOf(key: key))
                        .FilterSome();

                return output.CreateVirtualItemsSource(
                    collection: collection,
                    project: project,
                    scrollRequests: positions,
                    clears: clears,
                    retainedBeyondTheView: retainedBeyondTheView,
                    onEvicted: onEvicted);
            });
    }

    /// <summary>
    ///     Makes the source from a cell and a sink that the view model already has. Use this when the
    ///     rows do not come from a <c>ReactiveCollection</c>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The view model holds what the control reports on <paramref name="reportedRanges" />,
    ///         with <see cref="ReportedRange.Empty" /> as the first value, and answers in
    ///         <paramref name="rows" />. Make the count, the rows, and their offset from one cell, so
    ///         that they always agree.
    ///     </para>
    ///     <para>
    ///         Build rows for <see cref="ReportedRange.Constructed" />. That range is inside the list
    ///         as the control knew it at the report, and the count can be different by then. Thus,
    ///         stop at the count of the view model.
    ///     </para>
    ///     <para>
    ///         The control reports at each step of a scroll, because the amount of a row that shows
    ///         changes then. Calm <see cref="ReportedRange.Constructed" /> before the rows are built
    ///         from it. If not, each step builds the rows again.
    ///     </para>
    /// </remarks>
    /// <param name="output">The output of the view model. The source makes its values with the factory of the output.</param>
    /// <param name="rows">The rows for some of the positions, with the position of the first and the number of positions in the whole list.</param>
    /// <param name="reportedRanges">The sink that receives each report of the control.</param>
    /// <param name="scrollRequests">The positions to scroll to.</param>
    /// <param name="clears">The events that withdraw a request that the control did not use yet. None by default.</param>
    /// <returns>The source. Expose it on the view model, and bind <see cref="VirtualizingItemsControl.Source" /> to it.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" />, <paramref name="rows" />, <paramref name="reportedRanges" />, or
    ///     <paramref name="scrollRequests" /> is null.
    /// </exception>
    public static IVirtualItemsSource CreateVirtualItemsSource(
        this ViewModelBase.IOutput output,
        Cell<VirtualRows> rows,
        StreamSink<ReportedRange> reportedRanges,
        Stream<int> scrollRequests,
        Stream<Unit>? clears = null)
    {
        ArgumentNullException.ThrowIfNull(argument: output);
        ArgumentNullException.ThrowIfNull(argument: rows);
        ArgumentNullException.ThrowIfNull(argument: reportedRanges);
        ArgumentNullException.ThrowIfNull(argument: scrollRequests);

        return Transaction.Run(
            f: () =>
                Build(
                    bindableFactory: output.BindableFactory,
                    rows: rows,
                    reportedRanges: reportedRanges,
                    scrollRequests: scrollRequests,
                    clears: clears));
    }

    /// <summary>
    ///     Makes the source of a list that has all of its rows. Use this when the view model holds each row,
    ///     and wants only the virtualization of the row views.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The count of the control is the count of the list, and the rows start at position zero. The
    ///         control has each row, thus no row waits for the report. The source keeps the report, and
    ///         the view model can read it, with the rows at its positions, from the source.
    ///     </para>
    ///     <para>
    ///         A view model that makes its rows for the report uses the overload that takes a sink of the
    ///         report. The sink is not optional there, because a report that goes nowhere is a list whose
    ///         rows never load. Here the source has the sink, thus this is an overload of its own.
    ///     </para>
    /// </remarks>
    /// <typeparam name="TRow">
    ///     The type of a row. It is a class so that the list fits <see cref="VirtualRows" /> without a copy.
    /// </typeparam>
    /// <param name="output">The output of the view model. The source makes its values with the factory of the output.</param>
    /// <param name="rows">All the rows of the list, in their order.</param>
    /// <param name="scrollRequests">The positions to scroll to. None by default.</param>
    /// <param name="clears">The events that withdraw a request that the control did not use yet. None by default.</param>
    /// <returns>
    ///     The source. Expose it on the view model as an <see cref="IVirtualItemsSource" />, and bind
    ///     <see cref="VirtualizingItemsControl.Source" /> to it. The view model reads the rows that show
    ///     from it.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" /> or <paramref name="rows" /> is null.
    /// </exception>
    public static IVirtualItemsSource<TRow> CreateVirtualItemsSource<TRow>(
        this ViewModelBase.IOutput output,
        Cell<IReadOnlyList<TRow>> rows,
        Stream<int>? scrollRequests = null,
        Stream<Unit>? clears = null)
        where TRow : class
    {
        ArgumentNullException.ThrowIfNull(argument: output);
        ArgumentNullException.ThrowIfNull(argument: rows);

        return Transaction.Run(
            f: () =>
            {
                StreamSink<ReportedRange> reports = Stream.CreateSink<ReportedRange>();
                Cell<ReportedRange> range = reports.Hold(initialValue: ReportedRange.Empty);

                // The rows start at position zero. Thus, a range of the report is a place in the list.
                return new VirtualItemsSource<TRow>(
                    values: Build(
                        bindableFactory: output.BindableFactory,
                        rows: rows.Map(
                            f: static list => new VirtualRows(TotalCount: list.Count, Offset: 0, Items: list)),
                        reportedRanges: reports,
                        scrollRequests: scrollRequests ?? Stream.Never<int>(),
                        clears: clears),
                    constructedRows: RowsAt(items: rows, place: range.Map(f: static reported => reported.Constructed)),
                    visibleRows: RowsAt(items: rows, place: range.Map(f: static reported => reported.Visible)),
                    firstVisibilityAmount: range.Map(f: static reported => reported.FirstVisibilityAmount).Calm(),
                    lastVisibilityAmount: range.Map(f: static reported => reported.LastVisibilityAmount).Calm(),
                    reported: range);
            });
    }

    // The rows of a place in a list, where the two change. The control reports at each step of a scroll,
    // and most steps do not move the place. The first calm stops those steps. The list can change while
    // the rows of the place stay. The second calm stops a value that has the same rows.
    private static Cell<IReadOnlyList<TRow>> RowsAt<TRow>(Cell<IReadOnlyList<TRow>> items, Cell<ItemRange> place)
        where TRow : class =>
        place.Calm()
            .Lift(c2: items, f: static (range, list) => RowsAt(items: list, place: range))
            .Calm(
                areEqual: static (left, right) =>
                    left.SequenceEqual(second: right, comparer: ReferenceEqualityComparer.Instance));

    // The rows of a place in a list. The place stops at the two ends of the list. The report of the
    // control and the rows of the slice can be from different instants.
    private static IReadOnlyList<TRow> RowsAt<TRow>(IReadOnlyList<TRow> items, ItemRange place)
    {
        int first = Math.Clamp(value: place.First, min: 0, max: items.Count);
        int end = Math.Clamp(value: place.First + place.Count, min: first, max: items.Count);

        return [.. items.Skip(count: first).Take(count: end - first)];
    }

    private static VirtualItemsSource Build(
        IBindableFactory bindableFactory,
        Cell<VirtualRows> rows,
        StreamSink<ReportedRange> reportedRanges,
        Stream<int> scrollRequests,
        Stream<Unit>? clears)
    {
        ScrollRequestBindables scroll =
            bindableFactory.CreateScrollRequest(
                requests: scrollRequests,
                clears: clears ?? Stream.Never<Unit>());

        return new VirtualItemsSource(
            rows: bindableFactory.CreateOneWay(cell: rows),
            rangeReported: bindableFactory.CreateBindableAction(firingsStreamSink: reportedRanges),
            scrollRequest: scroll.Request,
            scrollHandled: scroll.Handled);
    }
}
