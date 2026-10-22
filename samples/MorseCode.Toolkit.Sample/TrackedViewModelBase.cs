using MorseCode.Mvvm;
using MorseCode.StagedConstruction;
using SodaFlow;
using SodaFlow.Bindable.ObjectModel;

namespace MorseCode.Toolkit.Sample;

/// <summary>
///     The base of a view model that the <see cref="IRowTracker" /> counts. It has two stages. The
///     first stage gives the subclass the output of <see cref="ViewModelBase" />. The second stage
///     takes a cell from the subclass and returns a bindable value of that cell.
/// </summary>
/// <remarks>
///     The subclass knows the cell, and only the base can register the bindable value. Thus, a
///     second stage is the way for the two to exchange the values.
/// </remarks>
// ReSharper disable once InheritdocConsiderUsage - The summary of IViewModel does not say what this base counts.
internal abstract class TrackedViewModelBase : ViewModelBase
{
    /// <summary>
    ///     Makes a view model from the values that the last stage gives.
    /// </summary>
    /// <param name="values">The values.</param>
    // ReSharper disable once InheritdocConsiderUsage - The summary of the base constructor does not say what this one takes.
    protected TrackedViewModelBase(Values values)
        : base(viewModelBase: values.Base) =>
        this.Title = values.Title;

    /// <summary>
    ///     Gets the title.
    /// </summary>
    public IOneWayBindableValue<string> Title { get; }

    /// <summary>
    ///     Starts the construction of the base.
    /// </summary>
    /// <param name="bindingScheduler">The scheduler for the bindable values.</param>
    /// <param name="tracker">The tracker that counts this view model.</param>
    /// <param name="continuation">The remaining construction of the subclass.</param>
    /// <typeparam name="TResult">The type that <paramref name="continuation" /> returns.</typeparam>
    /// <returns>The value that <paramref name="continuation" /> returns.</returns>
    protected static TResult CreateBase<TResult>(
        IBindingScheduler bindingScheduler,
        IRowTracker tracker,
        StageContinuation<IOutput, Stage<Cell<string>, IOneWayBindableValue<string>, Constructor<Values>>, TResult>
            continuation) =>
        ViewModelBase.CreateBase(
            bindingScheduler: bindingScheduler,
            continuation: (output, construct) =>
            {
                // The base releases the last registration first. This registration is the first one.
                // Thus, it is the last to run, after each bindable value of the row stops.
                tracker.RowBuilt();
                output.AddDisposable(disposable: Disposable.FromAction(onDispose: tracker.RowReleased));

                return continuation(
                    output: output,
                    next: Stage.From<Cell<string>, IOneWayBindableValue<string>, Constructor<Values>>(
                        body: title =>
                        {
                            IOneWayBindableValue<string> bindable = output.BindableFactory.CreateOneWay(cell: title);

                            return (Output: bindable,
                                Next: construct.Select(
                                    selector: viewModelBase => new Values(Base: viewModelBase, Title: bindable)));
                        }));
            });

    /// <summary>
    ///     What the second stage gives to the constructor of the subclass.
    /// </summary>
    /// <param name="Base">The base that <see cref="ViewModelBase" /> made.</param>
    /// <param name="Title">The bindable value of the title.</param>
    internal sealed record Values(ViewModelBase Base, IOneWayBindableValue<string> Title);
}
