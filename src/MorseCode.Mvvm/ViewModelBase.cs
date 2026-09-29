using System;
using System.Collections.Generic;
using System.ComponentModel;
using JetBrains.Annotations;
using MorseCode.StagedConstruction;
using SodaFlow;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;

namespace MorseCode.Mvvm;

/// <summary>
///     The base of a view model in staged construction. The static <c>Create</c> method of a view
///     model calls <see cref="CreateBase{TResult}" />. The view model keeps the base that it gets, and
///     its <see cref="IViewModel" /> members call the members of the base.
/// </summary>
/// <remarks>
///     <para>
///         During its construction, the view model adds each subscription to the registrations through
///         <see cref="IOutput" />. The call to <see cref="IConstruct{TBaseValues}.Construct{TResult}" />
///         closes the registration. After that call, each member of <see cref="IOutput" /> fails.
///     </para>
///     <para>
///         <see cref="Dispose" /> releases the registrations in the order of their registration.
///     </para>
/// </remarks>
[PublicAPI]
// ReSharper disable once InheritdocConsiderUsage - The summary of IViewModel does not say how a view model uses this base.
public sealed class ViewModelBase : IViewModel
{
    private readonly IDisposable registrations;

    private ViewModelBase(IDisposable registrations) => this.registrations = registrations;

    /// <summary>
    ///     The only stage of the base. It gives the subclass an <see cref="IOutput" /> for its
    ///     registrations, and the handle that constructs the base.
    /// </summary>
    /// <remarks>
    ///     The subclass must call <see cref="IConstruct{TBaseValues}.Construct{TResult}" /> one time only.
    ///     A second call fails with an <see cref="InvalidOperationException" />, because two view
    ///     models cannot own the same registrations.
    /// </remarks>
    /// <param name="bindingScheduler">
    ///     The scheduler for the bindable values that <see cref="IOutput.BindableFactory" /> makes.
    ///     Capture it on the UI thread with <see cref="SynchronizationContextBindingScheduler.Capture" />.
    ///     A test supplies <see cref="BindingScheduler.Immediate" />.
    /// </param>
    /// <param name="continuation">The remaining construction of the subclass.</param>
    /// <typeparam name="TResult">
    ///     The type that <paramref name="continuation" /> returns. Usually, it is the subclass.
    /// </typeparam>
    /// <returns>The value that <paramref name="continuation" /> returns.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bindingScheduler" /> is null.</exception>
    public static TResult CreateBase<TResult>(
        IBindingScheduler bindingScheduler,
        StageContinuation<IOutput, IConstruct<ViewModelBase>, TResult> continuation)
    {
        // The null check occurs here, where the caller is on the stack.
        ArgumentNullException.ThrowIfNull(argument: bindingScheduler);

        Output output = new(bindingScheduler: bindingScheduler);

        return continuation(
            output: output,
            next: Construct
                .From(values: output)
                .Select(
                    selector: static constructedOutput =>
                        new ViewModelBase(registrations: constructedOutput.Seal())));
    }

    /// <inheritdoc />
    /// <remarks>
    ///     The first call releases each registration one time, in the order of their registration. Each
    ///     subsequent call does nothing, also when two threads call at the same time. The remarks of
    ///     <see cref="Disposable.Composite" /> give the result of an exception.
    /// </remarks>
    public void Dispose() => this.registrations.Dispose();

    /// <summary>
    ///     This event does not occur, and it keeps no handler.
    /// </summary>
    /// <remarks>
    ///     The properties of a view model do not change, and each bindable value sends its own
    ///     notifications. The view model implements <see cref="INotifyPropertyChanged" /> because WPF
    ///     uses a <c>PropertyDescriptor</c> to monitor a binding source without it. That monitor keeps
    ///     the source alive.
    /// </remarks>
    // ReSharper disable once InheritdocConsiderUsage - The summary of INotifyPropertyChanged.PropertyChanged does not say that this event does not occur.
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add { }
        remove { }
    }

    /// <summary>
    ///     The output of <see cref="CreateBase{TResult}" />. The subclass adds each subscription of its
    ///     construction to the registrations here, and <see cref="ViewModelBase.Dispose" /> releases them.
    /// </summary>
    /// <remarks>
    ///     Use this during the construction only. After the subclass calls
    ///     <see cref="IConstruct{TBaseValues}.Construct{TResult}" />, each member fails with an
    ///     <see cref="InvalidOperationException" />.
    /// </remarks>
    [PublicAPI]
    public interface IOutput
    {
        /// <summary>
        ///     Adds <paramref name="listener" /> to the registrations. <see cref="ViewModelBase.Dispose" />
        ///     stops it.
        /// </summary>
        /// <remarks>
        ///     The view model keeps a reference to the listener. Thus, the garbage collector does not
        ///     remove a weak listener before the view model releases it.
        /// </remarks>
        /// <param name="listener">The listener.</param>
        /// <typeparam name="T">The type of the listener.</typeparam>
        /// <returns>
        ///     <paramref name="listener" />, thus a caller can add a listener in the expression that makes
        ///     it.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="listener" /> is null.</exception>
        /// <exception cref="InvalidOperationException">The subclass called Construct.</exception>
        T AddListener<T>(T listener)
            where T : IWeakListener;

        /// <summary>
        ///     Adds <paramref name="disposable" /> to the registrations. <see cref="ViewModelBase.Dispose" />
        ///     disposes it.
        /// </summary>
        /// <param name="disposable">The disposable.</param>
        /// <typeparam name="T">The type of the disposable.</typeparam>
        /// <returns>
        ///     <paramref name="disposable" />, thus a caller can add a disposable in the expression that
        ///     makes it.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="disposable" /> is null.</exception>
        /// <exception cref="InvalidOperationException">The subclass called Construct.</exception>
        T AddDisposable<T>(T disposable)
            where T : IDisposable;

        /// <summary>
        ///     Makes bindable values and actions, and adds each object that it makes to the registrations.
        /// </summary>
        /// <remarks>
        ///     After the subclass calls Construct, each method fails with an
        ///     <see cref="InvalidOperationException" />. The method does this check before it makes the
        ///     object. Thus, a refused call attaches nothing to the graph.
        /// </remarks>
        IBindableFactory BindableFactory { get; }
    }

    private sealed class Output(IBindingScheduler bindingScheduler) : IOutput, IBindableFactory
    {
        // One list, so that Dispose releases the entries in the order of their registration.
        private readonly List<IDisposable> registrations = [];

        private readonly BindableFactory bindableFactory =
            new(bindingScheduler: bindingScheduler);

        private bool isSealed;

        /// <inheritdoc />
        public T AddListener<T>(T listener)
            where T : IWeakListener
        {
            // The null check occurs here, where the caller is on the stack. A null entry that gets to
            // Dispose stops the disposal of all of the entries.
            ArgumentNullException.ThrowIfNull(argument: listener);
            this.ThrowIfSealed();

            // The delegate refers to the listener. Thus, the list keeps a weak listener alive for the
            // life of the view model.
            this.registrations.Add(Disposable.FromAction(onDispose: listener.Unlisten));
            return listener;
        }

        /// <inheritdoc />
        public T AddDisposable<T>(T disposable)
            where T : IDisposable
        {
            // The null check occurs here, where the caller is on the stack. A null entry that gets to
            // Dispose stops the disposal of all of the entries.
            ArgumentNullException.ThrowIfNull(argument: disposable);
            this.ThrowIfSealed();

            this.registrations.Add(disposable);
            return disposable;
        }

        /// <inheritdoc />
        public IBindableFactory BindableFactory => this;

        // The base calls this when the subclass calls Construct. Thus, the composite holds each entry
        // that the construction registered, and a registration after it fails and does not leak.
        public IDisposable Seal()
        {
            this.ThrowIfSealed();
            this.isSealed = true;

            return Disposable.Composite(disposables: this.registrations);
        }

        /// <inheritdoc />
        public IOneWayBindableValue<T> CreateOneWay<T>(Cell<T> cell, IEqualityComparer<T>? comparer = null)
        {
            this.ThrowIfSealed();

            IOneWayBindableValue<T> oneWayBindableValue =
                this.bindableFactory.CreateOneWay(cell: cell, comparer: comparer);

            this.registrations.Add(oneWayBindableValue);

            return oneWayBindableValue;
        }

        /// <inheritdoc />
        public ITwoWayBindableValue<T> CreateTwoWay<T>(
            Cell<T> cell,
            StreamSink<T> editsStreamSink,
            IEqualityComparer<T>? comparer = null)
        {
            this.ThrowIfSealed();

            ITwoWayBindableValue<T> twoWayBindableValue =
                this.bindableFactory.CreateTwoWay(cell: cell, editsStreamSink: editsStreamSink, comparer: comparer);

            this.registrations.Add(twoWayBindableValue);

            return twoWayBindableValue;
        }

        /// <inheritdoc />
        public ITwoWayBindableValue<T> CreateTwoWay<T>(CellSink<T> sink, IEqualityComparer<T>? comparer = null)
        {
            this.ThrowIfSealed();

            ITwoWayBindableValue<T> twoWayBindableValue =
                this.bindableFactory.CreateTwoWay(sink: sink, comparer: comparer);

            this.registrations.Add(twoWayBindableValue);

            return twoWayBindableValue;
        }

        /// <inheritdoc />
        public IOneWayToSourceBindableValue<T> CreateOneWayToSource<T>(
            StreamSink<T> editsStreamSink,
            T initialValue,
            IEqualityComparer<T>? comparer = null)
        {
            this.ThrowIfSealed();

            IOneWayToSourceBindableValue<T> oneWayToSourceBindableValue =
                this.bindableFactory.CreateOneWayToSource(
                    editsStreamSink: editsStreamSink,
                    initialValue: initialValue,
                    comparer: comparer);

            this.registrations.Add(oneWayToSourceBindableValue);

            return oneWayToSourceBindableValue;
        }

        /// <inheritdoc />
        public IOneWayToSourceBindableValue<T> CreateOneWayToSource<T>(
            CellSink<T> sink,
            IEqualityComparer<T>? comparer = null)
        {
            this.ThrowIfSealed();

            IOneWayToSourceBindableValue<T> oneWayToSourceBindableValue =
                this.bindableFactory.CreateOneWayToSource(sink: sink, comparer: comparer);

            this.registrations.Add(oneWayToSourceBindableValue);

            return oneWayToSourceBindableValue;
        }

        /// <inheritdoc />
        public IBindableAction<T> CreateBindableAction<T>(
            StreamSink<T> firingsStreamSink,
            Cell<bool>? isEnabledCell = null)
            where T : notnull
        {
            this.ThrowIfSealed();

            IBindableAction<T> bindableAction =
                this.bindableFactory.CreateBindableAction(
                    firingsStreamSink: firingsStreamSink,
                    isEnabledCell: isEnabledCell);

            this.registrations.Add(bindableAction);

            return bindableAction;
        }

        /// <inheritdoc />
        public IBindableAction CreateBindableAction(
            StreamSink<Unit> firingsStreamSink,
            Cell<bool>? isEnabledCell = null)
        {
            this.ThrowIfSealed();

            IBindableAction bindableAction =
                this.bindableFactory.CreateBindableAction(
                    firingsStreamSink: firingsStreamSink,
                    isEnabledCell: isEnabledCell);

            this.registrations.Add(bindableAction);

            return bindableAction;
        }

        /// <inheritdoc />
        public IBindableAction<Maybe<T>> CreateBindableAction<T>(
            StreamSink<Maybe<T>> firingsStreamSink,
            Cell<bool>? isEnabledCell = null)
            where T : notnull
        {
            this.ThrowIfSealed();

            IBindableAction<Maybe<T>> bindableAction =
                this.bindableFactory.CreateBindableAction(
                    firingsStreamSink: firingsStreamSink,
                    isEnabledCell: isEnabledCell);

            this.registrations.Add(bindableAction);

            return bindableAction;
        }

        private void ThrowIfSealed()
        {
            if (this.isSealed)
            {
                throw new InvalidOperationException(
                    message: "The view model is already constructed. Register each disposable, listener, "
                        + "and bindable before the subclass calls Construct.");
            }
        }
    }
}
