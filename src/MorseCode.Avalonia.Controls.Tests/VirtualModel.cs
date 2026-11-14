using System;
using System.Collections.Generic;
using System.Linq;
using SodaFlow;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     A source for a <see cref="VirtualizingItemsControl" />. It does what a <c>Slice</c> followed by
///     a <c>Map</c> does. It builds a row for each position that the view wants a row for, and for
///     no other position. It keeps its own record of each report, so a test can check the protocol of
///     the control without the graph of a view model.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of IVirtualItemsSource does not say what this model builds.
internal sealed class VirtualModel : IVirtualItemsSource
{
    private readonly Dictionary<int, VirtualRow> rows = [];
    private readonly CellSink<int> totalCount;
    private readonly StreamSink<int> requests = Stream.CreateSink<int>();
    private readonly StreamSink<Unit> releases = Stream.CreateSink<Unit>();
    private readonly Cell<Maybe<ScrollRequest>> request;

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

        StreamSink<ReportedRange> rangeReports = Stream.CreateSink<ReportedRange>();
        StreamSink<ScrollRequest> handledReports = Stream.CreateSink<ScrollRequest>();

        this.totalCount = Cell.CreateSink(initialValue: total);

        // The model is a graph and sends nothing from a listener. The rows are a function of the range,
        // the count, and the releases, thus they answer in the same transaction as the report.
        Cell<ReportedRange> range = rangeReports.Hold(initialValue: ReportedRange.Empty);
        Cell<int> released = this.releases.Map(f: _ => ++this.releaseCount).Hold(initialValue: 0);

        Cell<VirtualRows> rowsCell =
            range.Lift(
                c2: this.totalCount,
                c3: released,
                f: (reported, count, _) => this.Answer(wanted: reported.Constructed, count: count));

        // Each request gets the next number, as in the source of a view model. A report of the request
        // that the model holds clears it, unless the model keeps it.
        this.request =
            this.requests.Map(
                    f: static position =>
                        Change(
                            f: held =>
                                new Held(
                                    Request: Maybe<ScrollRequest>.Some(
                                        value: new ScrollRequest(Position: position, Number: held.Next)),
                                    Next: held.Next + 1)))
                .OrElse(
                    s2: handledReports.Map(
                        f: handled =>
                            Change(
                                f: held =>
                                    !this.KeepsHandledRequest
                                    && held.Request.Equals(other: Maybe<ScrollRequest>.Some(value: handled))
                                        ? held with { Request = Maybe<ScrollRequest>.None }
                                        : held)))
                .Accum(
                    initialState: new Held(Request: Maybe<ScrollRequest>.None, Next: 0),
                    f: static (change, held) => change(arg: held))
                .Map(f: static held => held.Request);

        this.Rows = factory.CreateOneWay(cell: rowsCell);
        this.ScrollRequest = factory.CreateOneWay(cell: this.request);

        this.RangeReported = factory.CreateBindableAction(firingsStreamSink: rangeReports);
        this.ScrollHandled = factory.CreateBindableAction(firingsStreamSink: handledReports);

        // These listeners only record.
        this.listeners =
        [
            rangeReports.Listen(handler: this.RecordRange),
            handledReports.Listen(handler: handled => this.ScrollReports.Add(item: handled.Position))
        ];
    }

    /// <inheritdoc />
    public IOneWayBindableValue<VirtualRows> Rows { get; }

    /// <inheritdoc />
    public IBindableAction<ReportedRange> RangeReported { get; }

    /// <inheritdoc />
    public IOneWayBindableValue<Maybe<ScrollRequest>> ScrollRequest { get; }

    /// <inheritdoc />
    public IBindableAction<ScrollRequest> ScrollHandled { get; }

    /// <summary>
    ///     Gets the position of the request that the model holds now. A test makes a request with
    ///     <see cref="Request" />, and a report of the view can clear it.
    /// </summary>
    public Maybe<int> CurrentRequest => this.request.Sample().Map(f: static held => held.Position);

    /// <summary>
    ///     Gets or sets whether the model leaves the request when the view reports it. This stands for
    ///     the time that a report takes to reach the view model.
    /// </summary>
    public bool KeepsHandledRequest { get; init; }

    /// <summary>
    ///     Gets or sets whether the model makes a new row object for each position at each answer. The
    ///     docs do not tell a view model to keep its row objects.
    /// </summary>
    public bool MakesNewRows { get; init; }

    /// <summary>
    ///     Gets the position of each request that the view reported as handled, in sequence.
    /// </summary>
    public List<int> ScrollReports { get; } = [];

    /// <summary>
    ///     Gets the range that the view wanted rows for at each report, in sequence.
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
    ///     Asks the view to scroll to a position, as a view model does. It does not count as a report
    ///     of the view.
    /// </summary>
    /// <param name="position">The position.</param>
    public void Request(int position) => this.requests.Send(a: position);

    // Names the type once, so that a lambda that changes the request reads as a value.
    private static Func<Held, Held> Change(Func<Held, Held> f) => f;

    private void RecordRange(ReportedRange value)
    {
        this.Writes.Add(item: value.Constructed);

        // A view that reports without end is a defect. Without this check, it hangs the test run.
        if (this.Writes.Count > 200)
        {
            throw new InvalidOperationException(
                message: "The view reported a range more than 200 times: "
                         + string.Join(separator: " ", values: this.Writes.Take(count: 12)));
        }
    }

    private VirtualRows Answer(ItemRange wanted, int count)
    {
        // The count is always current. Only the rows are late.
        if (this.isHeld)
        {
            return this.lastRows with { TotalCount = count };
        }

        int first = wanted.First;
        int end = Math.Min(val1: first + wanted.Count, val2: count);
        List<object?> items = [];

        for (int position = first; position < end; position++)
        {
            if (this.MakesNewRows || !this.rows.TryGetValue(key: position, value: out VirtualRow? row))
            {
                row = new VirtualRow(Position: position);
                this.rows[key: position] = row;
                this.Built.Add(item: position);
            }

            items.Add(item: row);
        }

        this.lastRows = new VirtualRows(TotalCount: count, Offset: first, Items: items);

        return this.lastRows;
    }

    // The request that the model holds, and the number that the next request gets.
    private readonly record struct Held(Maybe<ScrollRequest> Request, long Next);
}
