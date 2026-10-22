using System.Linq;
using MorseCode.Avalonia.Controls;
using MorseCode.Mvvm;
using SodaFlow;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Collections;

namespace MorseCode.Toolkit.Sample;

/// <summary>
///     The view model of the main window. It turns the range that the list reports into the rows
///     for that range, and it builds a view model only for those rows.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of IWorkbenchViewModel does not say how this class builds the rows.
internal sealed class WorkbenchViewModel : ViewModelBase, IWorkbenchViewModel
{
    private WorkbenchViewModel(
        ViewModelBase viewModelBase,
        int totalCount,
        IOneWayBindableValue<VirtualRows> rows,
        IOneWayToSourceBindableValue<ItemRange> visibleRange,
        IOneWayBindableValue<int> builtRows,
        IOneWayBindableValue<int> aliveRows)
        : base(viewModelBase: viewModelBase)
    {
        this.TotalCount = totalCount;
        this.Rows = rows;
        this.VisibleRange = visibleRange;
        this.BuiltRows = builtRows;
        this.AliveRows = aliveRows;
    }

    /// <inheritdoc />
    public int TotalCount { get; }

    /// <inheritdoc />
    public int JumpTarget => this.TotalCount * 9 / 10;

    /// <inheritdoc />
    public IOneWayBindableValue<VirtualRows> Rows { get; }

    /// <inheritdoc />
    public IOneWayToSourceBindableValue<ItemRange> VisibleRange { get; }

    /// <inheritdoc />
    public IOneWayBindableValue<int> BuiltRows { get; }

    /// <inheritdoc />
    public IOneWayBindableValue<int> AliveRows { get; }

    /// <summary>
    ///     Makes the view model.
    /// </summary>
    /// <param name="bindingScheduler">The scheduler for the bindable values.</param>
    /// <param name="tracker">The tracker that counts the row view models.</param>
    /// <param name="totalCount">The number of items in the collection.</param>
    /// <param name="retainedBeyondTheView">
    ///     The number of row view models to keep for keys that left the range.
    /// </param>
    /// <returns>The view model.</returns>
    public static IWorkbenchViewModel Create(
        IBindingScheduler bindingScheduler,
        IRowTracker tracker,
        int totalCount,
        int retainedBeyondTheView) =>
        Transaction.Run(
            f: () =>
                CreateBase(
                    bindingScheduler: bindingScheduler,
                    continuation: (output, construct) =>
                    {
                        ReactiveCollection<int, RowId, RowState> collection =
                            ReactiveCollection<int, RowId, RowState>.Create(
                                keySelector: static id => id.Number,
                                initialItems:
                                [
                                    .. Enumerable.Range(start: 0, count: totalCount)
                                        .Select(
                                            selector: static number =>
                                                new Item<RowId, RowState>(
                                                    identity: new RowId(Number: number),
                                                    state: new RowState(Title: $"Item {number:N0}")))
                                ],
                                Stream.CreateSink<CollectionEdit<int, RowId, RowState>>());

                        // The view writes the range. The offset and the limit are two views of this one
                        // cell. Thus, the slice always reads both values of the same range.
                        CellSink<ItemRange> range = Cell.CreateSink(initialValue: ItemRange.Empty);
                        Cell<int> offset = range.Map(f: static visible => visible.First);
                        Cell<int> limit = range.Map(f: static visible => visible.Count);

                        // This is the first registration, thus it is the last to stop. The rows stop
                        // after everything that reads them.
                        MappedItems<IRowViewModel> mapped =
                            output.AddDisposable(
                                disposable: collection
                                    .Slice(offsetCell: offset, limitCell: limit)
                                    .Map(
                                        project: number =>
                                            RowViewModel.Create(
                                                bindingScheduler: bindingScheduler,
                                                tracker: tracker,
                                                position: number,
                                                state: collection.StateCell(key: number)),
                                        retainedBeyondTheView: retainedBeyondTheView,
                                        onEvicted: static row => row.Dispose()));

                        // The offset and the rows are one value. Thus, they always agree.
                        Cell<VirtualRows> rowsAtOffset =
                            offset.Lift(
                                c2: mapped.Items,
                                f: static (first, items) =>
                                    new VirtualRows(Offset: first, Items: [.. items.Cast<object?>()]));

                        IOneWayBindableValue<VirtualRows> rows =
                            output.BindableFactory.CreateOneWay(cell: rowsAtOffset);

                        IOneWayToSourceBindableValue<ItemRange> visibleRange =
                            output.BindableFactory.CreateOneWayToSource(sink: range);

                        IOneWayBindableValue<int> builtRows =
                            output.BindableFactory.CreateOneWay(cell: tracker.Built);

                        IOneWayBindableValue<int> aliveRows =
                            output.BindableFactory.CreateOneWay(cell: tracker.Alive);

                        return construct.Construct(
                            constructor: viewModelBase =>
                                new WorkbenchViewModel(
                                    viewModelBase: viewModelBase,
                                    totalCount: totalCount,
                                    rows: rows,
                                    visibleRange: visibleRange,
                                    builtRows: builtRows,
                                    aliveRows: aliveRows));
                    }));

    private sealed record RowId(int Number);
}
