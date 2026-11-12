using System;
using System.Collections.Generic;
using System.Linq;
using MorseCode.Mvvm;
using SodaFlow;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Collections;
using SodaFlow.Functional;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     The view model that the documentation of <see cref="VirtualizingItemsControl" /> describes, on a
///     real SodaFlow collection. The source comes from <c>CreateVirtualItemsSource</c>, which slices the
///     collection with the range that the view writes and maps each key in the slice to a row.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of IVirtualItemsSource does not say what this model builds.
internal sealed class RecipeModel : IVirtualItemsSource, IDisposable
{
    // A control that asks for rows without end is a defect. More values than this stop the test.
    private const int RowsValuesLimit = 200;

    private readonly StreamSink<CollectionEdit<int, RecipeId, RecipeState>> edits =
        Stream.CreateSink<CollectionEdit<int, RecipeId, RecipeState>>();

    private readonly IVirtualItemsSource<VirtualRow> source;
    private readonly ViewModelBase viewModel;

    // SodaFlow holds a listener weakly, so this field keeps the listener alive.
    private readonly IListener rowsListener;

    /// <summary>
    ///     Makes the model.
    /// </summary>
    /// <param name="total">The number of items in the collection.</param>
    /// <param name="retainedBeyondTheView">
    ///     The number of keys that left the slice, for which the map keeps a row.
    /// </param>
    public RecipeModel(int total, int retainedBeyondTheView)
    {
        ReactiveCollection<int, RecipeId, RecipeState> collection =
            ReactiveCollection<int, RecipeId, RecipeState>.Create(
                keySelector: static id => id.Value,
                initialItems:
                [
                    .. Enumerable.Range(start: 0, count: total)
                        .Select(
                            selector: static key =>
                                new Item<RecipeId, RecipeState>(
                                    identity: new RecipeId(Value: key),
                                    state: new RecipeState(Revision: 0)))
                ],
                this.edits);

        // The view model asks for a scroll with the action. The request is a value, thus a request that
        // comes before the view binds is still there when it binds. The view reports the position that
        // it handled, and that report clears the request. The base view model owns the source, and it
        // releases the rows when the model is disposed of.
        (this.viewModel, this.source, this.ScrollTo) =
            ViewModelBase.CreateBase(
                bindingScheduler: BindingScheduler.Immediate,
                continuation: (output, construct) =>
                {
                    StreamSink<int> requests = Stream.CreateSink<int>();

                    IVirtualItemsSource<VirtualRow> made =
                        output.CreateVirtualItemsSource(
                            collection: collection,
                            project: key =>
                            {
                                this.Built.Add(item: key);

                                return new VirtualRow(Position: key);
                            },
                            scrollRequests: requests,
                            retainedBeyondTheView: retainedBeyondTheView,
                            onEvicted: row => this.Evicted.Add(item: row.Position));

                    IBindableAction<int> scrollTo =
                        output.BindableFactory.CreateBindableAction(firingsStreamSink: requests);

                    return construct.Construct(constructor: viewModelBase => (viewModelBase, made, scrollTo));
                });

        this.rowsListener = this.source.Rows.Cell.Updates().Listen(handler: _ => this.CountRows());
    }

    /// <inheritdoc />
    public IOneWayBindableValue<VirtualRows> Rows => this.source.Rows;

    /// <inheritdoc />
    public IOneWayToSourceBindableValue<ReportedRange> Range => this.source.Range;

    /// <inheritdoc />
    public IOneWayBindableValue<Maybe<int>> ScrollRequest => this.source.ScrollRequest;

    /// <inheritdoc />
    public IOneWayToSourceBindableValue<Maybe<int>> ScrollHandled => this.source.ScrollHandled;

    /// <summary>
    ///     Gets the rows at the positions that show, as the view model reads them from its source.
    /// </summary>
    public Cell<IReadOnlyList<VirtualRow>> VisibleRows => this.source.VisibleRows;

    /// <summary>
    ///     Gets the action that asks the view to scroll to a position.
    /// </summary>
    public IBindableAction<int> ScrollTo { get; }

    /// <summary>
    ///     Gets the key of each row that the map built, in sequence.
    /// </summary>
    public List<int> Built { get; } = [];

    /// <summary>
    ///     Gets the key of each row that the map released, in sequence.
    /// </summary>
    public List<int> Evicted { get; } = [];

    /// <summary>
    ///     Gets the number of values of the rows that the model sent after it was made. The model must
    ///     send one value for one change of the view or of the collection. A control that reports an
    ///     incorrect range makes the model send more.
    /// </summary>
    public int RowsValues { get; private set; }

    /// <summary>
    ///     Adds an item at the end of the collection.
    /// </summary>
    /// <param name="key">The key of the item.</param>
    public void Add(int key) =>
        this.edits.Send(
            a: CollectionEdit<int, RecipeId, RecipeState>.Add(
                new Item<RecipeId, RecipeState>(
                    identity: new RecipeId(Value: key),
                    state: new RecipeState(Revision: 0))));

    /// <summary>
    ///     Removes an item from the collection.
    /// </summary>
    /// <param name="key">The key of the item.</param>
    public void Remove(int key) => this.edits.Send(a: CollectionEdit<int, RecipeId, RecipeState>.Remove(key));

    /// <summary>
    ///     Changes the state of an item. The key and the position of the item stay.
    /// </summary>
    /// <param name="key">The key of the item.</param>
    public void Update(int key) =>
        this.edits.Send(
            a: CollectionEdit<int, RecipeId, RecipeState>.Update(
                key: key,
                transform: static state => new RecipeState(Revision: state.Revision + 1)));

    /// <inheritdoc />
    public void Dispose()
    {
        this.rowsListener.Unlisten();
        this.viewModel.Dispose();
    }

    private void CountRows()
    {
        this.RowsValues++;

        if (this.RowsValues > RowsValuesLimit)
        {
            throw new InvalidOperationException(
                message: $"The model sent the rows more than {RowsValuesLimit} times. The view asks without end.");
        }
    }

    private sealed record RecipeId(int Value);

    private sealed record RecipeState(int Revision);
}
