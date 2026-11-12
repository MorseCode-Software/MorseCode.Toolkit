using MorseCode.Controls.ViewModels;
using SodaFlow;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;

namespace MorseCode.Controls.UI.Avalonia.Tests;

/// <summary>
///     A source whose rows are the value that a test gives. It does not read the
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
        this.ScrollRequest = factory.CreateOneWay(cell: Cell.Constant(value: Maybe<int>.None));

        this.Range =
            factory.CreateOneWayToSource(
                editsStreamSink: Stream.CreateSink<ReportedRange>(),
                initialValue: ReportedRange.Empty);

        this.ScrollHandled =
            factory.CreateOneWayToSource(
                editsStreamSink: Stream.CreateSink<Maybe<int>>(),
                initialValue: Maybe<int>.None);
    }

    /// <inheritdoc />
    public IOneWayBindableValue<VirtualRows> Rows { get; }

    /// <inheritdoc />
    public IOneWayToSourceBindableValue<ReportedRange> Range { get; }

    /// <inheritdoc />
    public IOneWayBindableValue<Maybe<int>> ScrollRequest { get; }

    /// <inheritdoc />
    public IOneWayToSourceBindableValue<Maybe<int>> ScrollHandled { get; }

    /// <summary>
    ///     Gives the rows to the control.
    /// </summary>
    /// <param name="value">The rows, with the number of positions in the whole list.</param>
    public void Give(VirtualRows value) => this.rows.Send(a: value);
}
