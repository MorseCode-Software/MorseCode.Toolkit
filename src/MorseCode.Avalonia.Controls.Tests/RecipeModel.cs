using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using SodaFlow;
using SodaFlow.Collections;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     The view model that the documentation of <see cref="VirtualizingItemsControl" /> describes,
///     on a real SodaFlow collection. The range that the view writes goes to the offset and the
///     limit of a <c>Slice</c>. A <c>Map</c> after it builds a row for each key in the slice.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of INotifyPropertyChanged does not say what this model builds.
internal sealed class RecipeModel : INotifyPropertyChanged, IDisposable
{
    private readonly CellSink<int> offset = Cell.CreateSink(initialValue: 0);
    private readonly CellSink<int> limit = Cell.CreateSink(initialValue: 0);
    private readonly MappedItems<VirtualRow> mapped;
    private readonly IListener listener;

    /// <summary>
    ///     Makes the model.
    /// </summary>
    /// <param name="total">The number of items in the collection.</param>
    /// <param name="retainedBeyondTheView">
    ///     The number of keys that left the slice, for which the map keeps a row.
    /// </param>
    public RecipeModel(int total, int retainedBeyondTheView)
    {
        this.TotalCount = total;

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

        // The slice, the map, and the cell that holds the offset with its rows are one construction.
        // Thus, the offset and the rows always agree.
        (MappedItems<VirtualRow> map, IListener listen) =
            Transaction.Run(
                f: () =>
                {
                    MappedItems<VirtualRow> rows =
                        collection
                            .Slice(offsetCell: this.offset, limitCell: this.limit)
                            .Map(
                                project: key =>
                                {
                                    this.Built.Add(item: key);

                                    return new VirtualRow(Position: key);
                                },
                                retainedBeyondTheView: retainedBeyondTheView,
                                onEvicted: row => this.Evicted.Add(item: row.Position));

                    Cell<VirtualWindow> window =
                        this.offset.Lift(
                            c2: rows.Items,
                            f: static (first, items) =>
                                new VirtualWindow(
                                    Offset: first,
                                    Items: [.. items.Cast<object?>()]));

                    IListener subscription =
                        window.Listen(
                            handler: value =>
                            {
                                this.Window = value;

                                this.PropertyChanged?.Invoke(
                                    sender: this,
                                    e: new PropertyChangedEventArgs(propertyName: nameof(this.Window)));
                            });

                    return (rows, subscription);
                });

        this.mapped = map;
        this.listener = listen;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     Gets the number of positions in the whole list.
    /// </summary>
    // ReSharper disable once UnusedAutoPropertyAccessor.Global - A binding in the view reads this property by its name.
    public int TotalCount { get; }

    /// <summary>
    ///     Gets the rows for the last range that the view wrote.
    /// </summary>
    public VirtualWindow Window { get; private set; } = VirtualWindow.Empty;

    /// <summary>
    ///     Gets or sets the range that shows. Only the view calls the setter.
    /// </summary>
    // ReSharper disable once UnusedMember.Global - A binding in the view writes this property by its name.
    public ItemRange VisibleRange
    {
        get;
        set
        {
            field = value;

            // The offset and the limit change in one transaction, thus the slice moves one time.
            Transaction.RunVoid(
                action: () =>
                {
                    this.offset.Send(a: value.First);
                    this.limit.Send(a: value.Count);
                });
        }
    } = ItemRange.Empty;

    /// <summary>
    ///     Gets the key of each row that the map built, in sequence.
    /// </summary>
    public List<int> Built { get; } = [];

    /// <summary>
    ///     Gets the key of each row that the map released, in sequence.
    /// </summary>
    public List<int> Evicted { get; } = [];

    /// <inheritdoc />
    public void Dispose()
    {
        this.listener.Unlisten();
        this.mapped.Dispose();
    }

    private sealed record RecipeId(int Value);

    private sealed record RecipeState;
}
