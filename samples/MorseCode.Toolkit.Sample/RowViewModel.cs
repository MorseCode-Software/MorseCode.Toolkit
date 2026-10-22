using SodaFlow;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;

namespace MorseCode.Toolkit.Sample;

/// <summary>
///     The view model of one row. Building it stands for a cost: it holds two bindable values, and
///     each one holds a subscription into the graph.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of IRowViewModel does not say how this class builds its values.
internal sealed class RowViewModel : TrackedViewModelBase, IRowViewModel
{
    private RowViewModel(Values values, int position, IOneWayBindableValue<string> detail)
        : base(values: values)
    {
        this.Position = position;
        this.Detail = detail;
    }

    /// <inheritdoc />
    public int Position { get; }

    /// <inheritdoc />
    public IOneWayBindableValue<string> Detail { get; }

    /// <summary>
    ///     Makes the view model of the row at <paramref name="position" />.
    /// </summary>
    /// <param name="bindingScheduler">The scheduler for the bindable values.</param>
    /// <param name="tracker">The tracker that counts the row.</param>
    /// <param name="position">The position of the row in the whole list.</param>
    /// <param name="state">
    ///     The state of the row in the collection. It has no value if the collection removes the row.
    /// </param>
    /// <returns>The view model.</returns>
    public static IRowViewModel Create(
        IBindingScheduler bindingScheduler,
        IRowTracker tracker,
        int position,
        Cell<Maybe<RowState>> state) =>
        Transaction.Run(
            f: () =>
                CreateBase(
                    bindingScheduler: bindingScheduler,
                    tracker: tracker,
                    continuation: (output, titleStage) =>
                        titleStage.Advance(
                            input: state.Map(f: static rowState => TitleOf(state: rowState)),
                            continuation: (_, construct) =>
                            {
                                IOneWayBindableValue<string> detail =
                                    output.BindableFactory.CreateOneWay(
                                        cell: state.Map(f: static rowState => $"{TitleOf(state: rowState).Length} characters"));

                                return construct.Construct(
                                    constructor: values => new RowViewModel(
                                        values: values,
                                        position: position,
                                        detail: detail));
                            })));

    private static string TitleOf(Maybe<RowState> state) =>
        state.Match(onSome: static rowState => rowState.Title, onNone: static () => "(removed)");
}
