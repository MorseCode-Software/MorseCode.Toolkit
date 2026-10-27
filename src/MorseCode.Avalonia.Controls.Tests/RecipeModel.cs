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
    private readonly IVirtualItemsSource source;
    private readonly ViewModelBase viewModel;

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
                                    state: new RecipeState()))
                ],
                Stream.CreateSink<CollectionEdit<int, RecipeId, RecipeState>>());

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

                    IVirtualItemsSource made =
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
    }

    /// <inheritdoc />
    public IOneWayBindableValue<int> TotalCount => this.source.TotalCount;

    /// <inheritdoc />
    public IOneWayBindableValue<VirtualRows> Rows => this.source.Rows;

    /// <inheritdoc />
    public IOneWayToSourceBindableValue<ItemRange> VisibleRange => this.source.VisibleRange;

    /// <inheritdoc />
    public IOneWayBindableValue<Maybe<int>> ScrollRequest => this.source.ScrollRequest;

    /// <inheritdoc />
    public IOneWayToSourceBindableValue<Maybe<int>> ScrollHandled => this.source.ScrollHandled;

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

    /// <inheritdoc />
    public void Dispose() => this.viewModel.Dispose();

    private sealed record RecipeId(int Value);

    private sealed record RecipeState;
}
