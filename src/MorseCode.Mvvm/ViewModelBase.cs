using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using MorseCode.StagedConstruction;
using SodaFlow;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;

namespace MorseCode.Mvvm;

/// <summary>
///     The base of a view model in staged construction. The static <c>Create</c> method of a view
///     model calls <see cref="CreateBase{TResult}" />. The view model derives from this class and
///     gives the base that it gets to <see cref="ViewModelBase(ViewModelBase)" />. Or, the view model
///     keeps the base that it gets, and its <see cref="IViewModel" /> members call the members of the
///     base.
/// </summary>
/// <remarks>
///     <para>
///         During its construction, the view model adds each subscription to the registrations through
///         <see cref="IOutput" />. The registration stays open while the constructor that
///         <see cref="Constructor{TBaseValues}.Construct{TResult}" /> calls runs, so that the constructor
///         of the subclass can add its own subscriptions. It closes when that constructor returns, or
///         when <see cref="Dispose" /> runs. After that, each member of <see cref="IOutput" /> fails.
///     </para>
///     <para>
///         If that constructor throws, the base releases the registrations and the exception goes to the
///         caller unchanged. If a registration also throws when the base releases it, the caller gets an
///         <see cref="AggregateException" /> with the exception of the constructor first.
///     </para>
///     <para>
///         <see cref="Dispose" /> releases the last registration first and the first registration last.
///         Usually, a registration uses only the registrations before it. Thus, each registration
///         stops before the registrations that it uses.
///     </para>
/// </remarks>
[PublicAPI]
// ReSharper disable once InheritdocConsiderUsage - The summary of IViewModel does not say how a view model uses this base.
public class ViewModelBase : IViewModel
{
    private readonly IDisposable registrations;

    // 1 after a subclass gives this base to the protected constructor. Two view models cannot own the
    // same registrations, so a second subclass cannot take this base.
    private int adopted;

    private ViewModelBase(IDisposable registrations) => this.registrations = registrations;

    /// <summary>
    ///     Makes a view model with the registrations of <paramref name="viewModelBase" />. A subclass
    ///     calls this constructor with the base that
    ///     <see cref="Constructor{TBaseValues}.Construct{TResult}" /> gives to it.
    /// </summary>
    /// <remarks>
    ///     <see cref="Dispose" /> on the subclass releases the registrations of the construction of
    ///     <paramref name="viewModelBase" />. One subclass only can take a base. A second call with the
    ///     same base fails with an <see cref="InvalidOperationException" />, because two view models
    ///     cannot own the same registrations.
    /// </remarks>
    /// <param name="viewModelBase">The base that gives its registrations to the subclass.</param>
    /// <exception cref="ArgumentNullException"><paramref name="viewModelBase" /> is null.</exception>
    /// <exception cref="InvalidOperationException">
    ///     A different subclass took <paramref name="viewModelBase" /> before.
    /// </exception>
    protected ViewModelBase(ViewModelBase viewModelBase)
        // Each field of the base goes to the private constructor, which is the one place that assigns
        // fields. No statement can run before this call, so the null check is part of the argument: a
        // null base fails with an ArgumentNullException and not a NullReferenceException.
        : this(
            registrations: (viewModelBase ?? throw new ArgumentNullException(paramName: nameof(viewModelBase)))
            .registrations)
    {
        // The private constructor only assigns. Thus, a refusal here leaves no state behind.
        if (Interlocked.Exchange(location1: ref viewModelBase.adopted, value: 1) != 0)
        {
            throw new InvalidOperationException(
                message: "A different view model already derives from this base. Two view models "
                    + "cannot own the same registrations.");
        }
    }

    /// <summary>
    ///     The only stage of the base. It gives the subclass an <see cref="IOutput" /> for its
    ///     registrations, and the handle that constructs the base.
    /// </summary>
    /// <remarks>
    ///     The subclass must call <see cref="Constructor{TBaseValues}.Construct{TResult}" /> one time only.
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
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="bindingScheduler" /> or <paramref name="continuation" /> is null.
    /// </exception>
    public static TResult CreateBase<TResult>(
        IBindingScheduler bindingScheduler,
        StageContinuation<IOutput, Constructor<ViewModelBase>, TResult> continuation)
    {
        // The null checks occur here, where the caller is on the stack, and before the output exists.
        if (bindingScheduler is null)
        {
            throw new ArgumentNullException(paramName: nameof(bindingScheduler));
        }

        if (continuation is null)
        {
            throw new ArgumentNullException(paramName: nameof(continuation));
        }

        Output output = new(bindingScheduler: bindingScheduler);

        return continuation(output: output, next: new ViewModelBaseConstructor(output: output));
    }

    // The base owns the output from the start, and the output releases what it holds on the first Dispose
    // call. Thus, a Dispose call during the constructor, or on a different thread, releases the
    // registrations that exist at that time, and each registration after it fails.
    private sealed class ViewModelBaseConstructor(in Output output) : Constructor<ViewModelBase>
    {
        private readonly Output output = output;

        /// <inheritdoc />
        protected override TResult ConstructCore<TResult>(Func<ViewModelBase, TResult> constructor)
        {
            TResult result;

            try
            {
                result = constructor(arg: new ViewModelBase(registrations: this.output));
            }
            catch (Exception constructionFailure)
            {
                // The subclass cannot release a base that it never received, and nobody else can. If a
                // registration also fails to stop, the caller gets both exceptions, and the exception of
                // the constructor is the first.
                try
                {
                    this.output.Dispose();
                }
                catch (Exception releaseFailure)
                {
                    throw new AggregateException(constructionFailure, releaseFailure);
                }

                throw;
            }

            this.output.Seal();

            return result;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     The first call releases each registration one time, the last registration first. Each
    ///     subsequent call does nothing, also when two threads call at the same time. A call during
    ///     the construction releases the registrations that exist then, and closes the registration.
    ///     The remarks of <see cref="Disposable.Composite" /> give the result of an exception. An
    ///     <see cref="AggregateException" /> holds the exceptions in the order of their release.
    /// </remarks>
    public void Dispose()
    {
        this.registrations.Dispose();

        // A subclass can add a finalizer. This call makes that finalizer unnecessary after Dispose, and the
        // subclass does not have to implement IDisposable again to make it.
        GC.SuppressFinalize(obj: this);
    }

    /// <summary>
    ///     This event does not occur, and it keeps no handler.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The properties of a view model do not change, and each bindable value sends its own
    ///         notifications. Thus, this event has no work to do.
    ///     </para>
    ///     <para>
    ///         This event exists only to prevent a memory leak in WPF. WPF uses a
    ///         <c>PropertyDescriptor</c> to monitor a binding source without
    ///         <see cref="INotifyPropertyChanged" />, and that monitor keeps the source alive after the
    ///         view closes. <see cref="IViewModel" /> includes the interface only to prevent this leak.
    ///         Do not remove the interface or this event.
    ///     </para>
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
    ///     Use this during the construction only. After the constructor that
    ///     <see cref="Constructor{TBaseValues}.Construct{TResult}" /> calls returns or throws, or after
    ///     <see cref="ViewModelBase.Dispose" /> runs, each member fails with an
    ///     <see cref="InvalidOperationException" />. This is also true on a different thread. A
    ///     registration either gets into the registrations that Dispose releases, or fails.
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
        /// <exception cref="InvalidOperationException">The registrations are closed.</exception>
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
        /// <exception cref="InvalidOperationException">The registrations are closed.</exception>
        T AddDisposable<T>(T disposable)
            where T : IDisposable;

        /// <summary>
        ///     Makes bindable values and actions, and adds each object that it makes to the registrations.
        /// </summary>
        /// <remarks>
        ///     After the registrations close, each method fails with an
        ///     <see cref="InvalidOperationException" />. The method does this check before it makes the
        ///     object. Thus, a refused call attaches nothing to the graph. The registrations can close
        ///     on a different thread while the method makes the object. Then, the method disposes the
        ///     object and fails.
        /// </remarks>
        IBindableFactory BindableFactory { get; }
    }

    // The in modifier makes the parameter readonly, and the compiler refuses to capture it in a member.
    // Thus, only the field initializer uses the parameter, and the parameter cannot become mutable state.
    private sealed class Output(in IBindingScheduler bindingScheduler) : IOutput, IBindableFactory, IDisposable
    {
        // One list, so that Dispose releases the last entry first, whatever its type.
        private readonly List<IDisposable> registrations = [];

        private readonly BindableFactory bindableFactory =
            new(bindingScheduler: bindingScheduler);

        // This code reads and writes isSealed and registrations only while it holds this lock. Thus, a
        // registration on a different thread either gets into the composite or fails, and it does not
        // leak. This code does not hold the lock while SodaFlow makes a bindable. SodaFlow can hold a
        // lock of its own, and a thread that holds that lock can call this output.
        private readonly object gate = new();

        private bool isSealed;

        // The first Dispose call makes this composite, and it closes the registrations at the same time.
        // The composite releases each entry one time, also when two threads call Dispose.
        private IDisposable? released;

        /// <inheritdoc />
        public T AddListener<T>(T listener)
            where T : IWeakListener
        {
            // The null check occurs here, where the caller is on the stack. A null entry that gets to
            // Dispose stops the disposal of all of the entries.
            if (listener is null)
            {
                throw new ArgumentNullException(paramName: nameof(listener));
            }

            // The delegate refers to the listener. Thus, the list keeps a weak listener alive for the
            // life of the view model.
            this.Add(registration: Disposable.FromAction(onDispose: listener.Unlisten));
            return listener;
        }

        /// <inheritdoc />
        public T AddDisposable<T>(T disposable)
            where T : IDisposable
        {
            // The null check occurs here, where the caller is on the stack. A null entry that gets to
            // Dispose stops the disposal of all of the entries.
            if (disposable is null)
            {
                throw new ArgumentNullException(paramName: nameof(disposable));
            }

            this.Add(registration: disposable);
            return disposable;
        }

        /// <inheritdoc />
        public IBindableFactory BindableFactory => this;

        // The base calls this when the constructor of the subclass returns. After it, a registration fails
        // and does not leak.
        public void Seal()
        {
            lock (this.gate)
            {
                this.isSealed = true;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            IDisposable composite;

            lock (this.gate)
            {
                this.isSealed = true;

                // Keep the static call. The Reverse method of List<T> reverses the list in place and
                // returns nothing. An extension call uses that method and not the method of LINQ.
                this.released ??= Disposable.Composite(disposables: Enumerable.Reverse(source: this.registrations));
                composite = this.released;
            }

            composite.Dispose();
        }

        /// <inheritdoc />
        public IOneWayBindableValue<T> CreateOneWay<T>(Cell<T> cell, IEqualityComparer<T>? comparer = null) =>
            this.Make(make: () => this.bindableFactory.CreateOneWay(cell: cell, comparer: comparer));

        /// <inheritdoc />
        public ITwoWayBindableValue<T> CreateTwoWay<T>(
            Cell<T> cell,
            StreamSink<T> editsStreamSink,
            IEqualityComparer<T>? comparer = null) =>
            this.Make(
                make: () =>
                    this.bindableFactory.CreateTwoWay(cell: cell, editsStreamSink: editsStreamSink, comparer: comparer));

        /// <inheritdoc />
        public ITwoWayBindableValue<T> CreateTwoWay<T>(CellSink<T> sink, IEqualityComparer<T>? comparer = null) =>
            this.Make(make: () => this.bindableFactory.CreateTwoWay(sink: sink, comparer: comparer));

        /// <inheritdoc />
        public IOneWayToSourceBindableValue<T> CreateOneWayToSource<T>(
            StreamSink<T> editsStreamSink,
            T initialValue,
            IEqualityComparer<T>? comparer = null) =>
            this.Make(
                make: () =>
                    this.bindableFactory.CreateOneWayToSource(
                        editsStreamSink: editsStreamSink,
                        initialValue: initialValue,
                        comparer: comparer));

        /// <inheritdoc />
        public IOneWayToSourceBindableValue<T> CreateOneWayToSource<T>(
            CellSink<T> sink,
            IEqualityComparer<T>? comparer = null) =>
            this.Make(make: () => this.bindableFactory.CreateOneWayToSource(sink: sink, comparer: comparer));

        /// <inheritdoc />
        public IBindableAction<T> CreateBindableAction<T>(
            StreamSink<T> firingsStreamSink,
            Cell<bool>? isEnabledCell = null)
            where T : notnull =>
            this.Make(
                make: () =>
                    this.bindableFactory.CreateBindableAction(
                        firingsStreamSink: firingsStreamSink,
                        isEnabledCell: isEnabledCell));

        /// <inheritdoc />
        public IBindableAction CreateBindableAction(
            StreamSink<Unit> firingsStreamSink,
            Cell<bool>? isEnabledCell = null) =>
            this.Make(
                make: () =>
                    this.bindableFactory.CreateBindableAction(
                        firingsStreamSink: firingsStreamSink,
                        isEnabledCell: isEnabledCell));

        /// <inheritdoc />
        public IBindableAction<Maybe<T>> CreateBindableAction<T>(
            StreamSink<Maybe<T>> firingsStreamSink,
            Cell<bool>? isEnabledCell = null)
            where T : notnull =>
            this.Make(
                make: () =>
                    this.bindableFactory.CreateBindableAction(
                        firingsStreamSink: firingsStreamSink,
                        isEnabledCell: isEnabledCell));

        private static InvalidOperationException SealedException() =>
            new(
                message: "The registrations are closed. Register each disposable, listener, and bindable "
                    + "before the constructor that Construct calls returns, and before Dispose.");

        private void Add(IDisposable registration)
        {
            lock (this.gate)
            {
                this.ThrowIfSealed();
                this.registrations.Add(registration);
            }
        }

        // The first check refuses a late call before SodaFlow makes anything. The registrations can close
        // while SodaFlow makes the object. Then, the second check finds the seal, and this method
        // disposes the object and fails. Thus, the object does not stay attached to the graph.
        private T Make<T>(Func<T> make)
            where T : IDisposable
        {
            lock (this.gate)
            {
                this.ThrowIfSealed();
            }

            T made = make();

            lock (this.gate)
            {
                if (!this.isSealed)
                {
                    this.registrations.Add(made);
                    return made;
                }
            }

            made.Dispose();

            throw SealedException();
        }

        // The caller must hold the lock.
        private void ThrowIfSealed()
        {
            if (this.isSealed)
            {
                throw SealedException();
            }
        }
    }
}
