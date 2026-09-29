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

[PublicAPI]
public class ViewModelBase : IViewModel
{
    private readonly Action onDispose;

    private bool disposed;

    private ViewModelBase(Action onDispose) =>
        this.onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));

    public static T CreateBase<T>(
        IBindingScheduler bindingScheduler,
        StageContinuation<IOutput, IConstruct<ViewModelBase>, T> continuation)
    {
        Output output = new(bindingScheduler);

        return continuation(
            output: output,
            next: Construct
                .From(values: output)
                .Select(
                    selector: static constructedOutput =>
                    {
                        IDisposable registrations = constructedOutput.Seal();

                        return new ViewModelBase(onDispose: registrations.Dispose);
                    }));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(location1: ref this.disposed, value: true))
        {
            return;
        }

        this.onDispose();

        GC.SuppressFinalize(this);
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [PublicAPI]
    public interface IOutput
    {
        T AddListener<T>(T listener)
            where T : IWeakListener;

        T AddDisposable<T>(T disposable)
            where T : IDisposable;

        IBindableFactory BindableFactory { get; }
    }

    private class Output(in IBindingScheduler bindingScheduler) : IOutput, IBindableFactory
    {
        private readonly List<IWeakListener> listeners = [];
        private readonly List<IDisposable> disposables = [];

        private readonly BindableFactory bindableFactory =
            new(bindingScheduler ?? throw new ArgumentNullException(nameof(bindingScheduler)));

        private bool isSealed;

        /// <inheritdoc />
        public T AddListener<T>(T listener)
            where T : IWeakListener
        {
            // The null check occurs here, where the caller is on the stack. A null entry that gets to
            // Dispose stops the disposal of all of the entries.
            ArgumentNullException.ThrowIfNull(argument: listener);
            this.ThrowIfSealed();

            this.listeners.Add(listener);
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

            this.disposables.Add(disposable);
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

            return Disposable.Composite(
                this.listeners.Select(static listener => Disposable.FromAction(listener.Unlisten))
                    .Concat(this.disposables));
        }

        /// <inheritdoc />
        public IOneWayBindableValue<T> CreateOneWay<T>(Cell<T> cell, IEqualityComparer<T>? comparer = null)
        {
            this.ThrowIfSealed();

            IOneWayBindableValue<T> oneWayBindableValue =
                this.bindableFactory.CreateOneWay(cell: cell, comparer: comparer);

            this.disposables.Add(oneWayBindableValue);

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

            this.disposables.Add(twoWayBindableValue);

            return twoWayBindableValue;
        }

        /// <inheritdoc />
        public ITwoWayBindableValue<T> CreateTwoWay<T>(CellSink<T> sink, IEqualityComparer<T>? comparer = null)
        {
            this.ThrowIfSealed();

            ITwoWayBindableValue<T> twoWayBindableValue =
                this.bindableFactory.CreateTwoWay(sink: sink, comparer: comparer);

            this.disposables.Add(twoWayBindableValue);

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

            this.disposables.Add(oneWayToSourceBindableValue);

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

            this.disposables.Add(oneWayToSourceBindableValue);

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

            this.disposables.Add(bindableAction);

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

            this.disposables.Add(bindableAction);

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

            this.disposables.Add(bindableAction);

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
