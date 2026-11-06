using System;
using System.Collections.Generic;
using System.Linq;
using SodaFlow;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     A source for a <see cref="VirtualizingItemsControl" />. It does what a <c>Slice</c> followed by
///     a <c>Map</c> does. It builds a row for each position of the range that the view writes, and for
///     no other position. It keeps its own record of each write, so a test can check the protocol of
///     the control without the graph of a view model.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of IVirtualItemsSource does not say what this model builds.
internal sealed class VirtualModel : IVirtualItemsSource
{
    private readonly Dictionary<int, VirtualRow> rows = [];
    private readonly CellSink<int> totalCount;
    private readonly StreamSink<int> requests = Stream.CreateSink<int>();
    private readonly StreamSink<Unit> releases = Stream.CreateSink<Unit>();
    private readonly Cell<Maybe<int>> request;

    // SodaFlow holds a listener weakly, so this field keeps the listeners alive.
    // ReSharper disable once NotAccessedField.Local - The field is there for its reference and not for its value.
    private readonly IListener[] listeners;

    private bool isHeld;
    private int releaseCount;
    private VirtualRows lastRows = VirtualRows.Empty;

    /// <summary>
    ///     Makes the model.
    /// </summary>
    /// <param name="total">The number of positions in the whole list.</param>
    public VirtualModel(int total)
    {
        BindableFactory factory = new(bindingScheduler: BindingScheduler.Immediate);

        StreamSink<ItemRange> rangeWrites = Stream.CreateSink<ItemRange>();
        StreamSink<Maybe<int>> handledWrites = Stream.CreateSink<Maybe<int>>();

        this.totalCount = Cell.CreateSink(initialValue: total);

        // The model is a graph and sends nothing from a listener. The rows are a function of the range,
        // the count, and the releases, thus they answer in the same transaction as the write.
        Cell<ItemRange> range = rangeWrites.Hold(initialValue: ItemRange.Empty);
        Cell<int> released = this.releases.Map(f: _ => ++this.releaseCount).Hold(initialValue: 0);

        Cell<VirtualRows> rowsCell =
            range.Lift(
                c2: this.totalCount,
                c3: released,
                f: (visible, count, _) => this.Answer(visible: visible, count: count));

        // A report of the position that the request holds clears it, unless the model keeps it.
        this.request =
            this.requests.Map(f: static position => Change(f: _ => Maybe<int>.Some(value: position)))
                .OrElse(
                    s2: handledWrites.Map(
                        f: handled =>
                            Change(
                                f: current =>
                                    !this.KeepsHandledRequest && current.Equals(other: handled)
                                        ? Maybe<int>.None
                                        : current)))
                .Accum(initialState: Maybe<int>.None, f: static (change, current) => change(arg: current));

        this.Rows = factory.CreateOneWay(cell: rowsCell);
        this.ScrollRequest = factory.CreateOneWay(cell: this.request);

        this.VisibleRange =
            factory.CreateOneWayToSource(editsStreamSink: rangeWrites, initialValue: ItemRange.Empty);

        this.ScrollHandled =
            factory.CreateOneWayToSource(editsStreamSink: handledWrites, initialValue: Maybe<int>.None);

        // These listeners only record.
        this.listeners = [rangeWrites.Listen(handler: this.RecordRange), handledWrites.Listen(handler: this.ScrollWrites.Add)];
    }

    /// <inheritdoc />
    public IOneWayBindableValue<VirtualRows> Rows { get; }

    /// <inheritdoc />
    public IOneWayToSourceBindableValue<ItemRange> VisibleRange { get; }

    /// <inheritdoc />
    public IOneWayBindableValue<Maybe<int>> ScrollRequest { get; }

    /// <inheritdoc />
    public IOneWayToSourceBindableValue<Maybe<int>> ScrollHandled { get; }

    /// <summary>
    ///     Gets the request that the model holds now. A test makes a request with
    ///     <see cref="Request" />, and a report of the view can clear it.
    /// </summary>
    public Maybe<int> CurrentRequest => this.request.Sample();

    /// <summary>
    ///     Gets or sets whether the model leaves the request when the view reports it. This stands for
    ///     the time that a report takes to reach the view model.
    /// </summary>
    public bool KeepsHandledRequest { get; init; }

    /// <summary>
    ///     Gets each value that the view wrote as the position that it handled, in sequence.
    /// </summary>
    public List<Maybe<int>> ScrollWrites { get; } = [];

    /// <summary>
    ///     Gets each range that the view wrote, in sequence.
    /// </summary>
    public List<ItemRange> Writes { get; } = [];

    /// <summary>
    ///     Gets the position of each row that the model built, in sequence.
    /// </summary>
    public List<int> Built { get; } = [];

    /// <summary>
    ///     Stops the model from answering a range until <see cref="Release" /> runs.
    ///     This stands for the time that a graph and a binding scheduler take.
    /// </summary>
    public void Hold() => this.isHeld = true;

    /// <summary>
    ///     Answers the last range that the view wrote.
    /// </summary>
    public void Release()
    {
        this.isHeld = false;
        this.releases.Send(a: Unit.Value);
    }

    /// <summary>
    ///     Changes the number of positions in the whole list, and answers again.
    /// </summary>
    /// <param name="total">The new number of positions.</param>
    public void Resize(int total) => this.totalCount.Send(a: total);

    /// <summary>
    ///     Asks the view to scroll to a position, as a view model does. It does not count as a write of
    ///     the view.
    /// </summary>
    /// <param name="position">The position.</param>
    public void Request(int position) => this.requests.Send(a: position);

    // Names the type once, so that a lambda that changes the request reads as a value.
    private static Func<Maybe<int>, Maybe<int>> Change(Func<Maybe<int>, Maybe<int>> f) => f;

    private void RecordRange(ItemRange value)
    {
        this.Writes.Add(item: value);

        // A view that reports without end is a defect. Without this check, it hangs the test run.
        if (this.Writes.Count > 200)
        {
            throw new InvalidOperationException(
                message: "The view reported a range more than 200 times: "
                         + string.Join(separator: " ", values: this.Writes.Take(count: 12)));
        }
    }

    private VirtualRows Answer(ItemRange visible, int count)
    {
        // The count is always current. Only the rows are late.
        if (this.isHeld)
        {
            return this.lastRows with { TotalCount = count };
        }

        int first = visible.First;
        int end = Math.Min(val1: first + visible.Count, val2: count);
        List<object?> items = [];

        for (int position = first; position < end; position++)
        {
            if (!this.rows.TryGetValue(key: position, value: out VirtualRow? row))
            {
                row = new VirtualRow(Position: position);
                this.rows.Add(key: position, value: row);
                this.Built.Add(item: position);
            }

            items.Add(item: row);
        }

        this.lastRows = new VirtualRows(TotalCount: count, Offset: first, Items: items);

        return this.lastRows;
    }
}
