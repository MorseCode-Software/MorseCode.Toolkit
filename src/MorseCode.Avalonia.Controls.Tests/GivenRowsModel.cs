using SodaFlow;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     A source whose rows are the value that a test gives. It does not use the
///     range that the control reports. Thus, a test can give rows that no view model
///     gives for that range.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of IVirtualItemsSource does not say where these rows come from.
internal sealed class GivenRowsModel : IVirtualItemsSource
{
    private readonly CellSink<VirtualRows> rows = Cell.CreateSink(initialValue: VirtualRows.Empty);

    /// <summary>
    ///     Initializes a new instance of the <see cref="GivenRowsModel" /> class, with no rows.
    /// </summary>
    public GivenRowsModel()
    {
        BindableFactory factory = new(bindingScheduler: BindingScheduler.Immediate);

        this.Rows = factory.CreateOneWay(cell: this.rows);
        this.ScrollRequest = factory.CreateOneWay(cell: Cell.Constant(value: Maybe<ScrollRequest>.None));

        this.RangeReported = factory.CreateBindableAction(firingsStreamSink: Stream.CreateSink<ReportedRange>());
        this.ScrollHandled = factory.CreateBindableAction(firingsStreamSink: Stream.CreateSink<ScrollRequest>());
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
    ///     Gives the rows to the control.
    /// </summary>
    /// <param name="value">The rows, with the number of positions in the whole list.</param>
    public void Give(VirtualRows value) => this.rows.Send(a: value);
}
