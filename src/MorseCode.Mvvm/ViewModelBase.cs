using System;
using System.Collections.Generic;
using System.ComponentModel;
using JetBrains.Annotations;
using MorseCode.StagedConstruction;
using SodaFlow;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;

namespace MorseCode.Mvvm;

[PublicAPI]
public sealed class ViewModelBase : IViewModel
{
    private readonly IDisposable registrations;

    private ViewModelBase(IDisposable registrations) => this.registrations = registrations;

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
                        new ViewModelBase(registrations: constructedOutput.Seal())));
    }

    /// <inheritdoc />
    public void Dispose() => this.registrations.Dispose();

    // The properties of a view model do not change, and each bindable value sends its own
    // notifications. Thus, this event does not occur, and it keeps no handler. WPF watches a binding
    // source without this interface through a PropertyDescriptor, which keeps the source alive.
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add { }
        remove { }
    }

    [PublicAPI]
    public interface IOutput
    {
        T AddListener<T>(T listener)
            where T : IWeakListener;

        T AddDisposable<T>(T disposable)
            where T : IDisposable;

        IBindableFactory BindableFactory { get; }
    }

    private sealed class Output(IBindingScheduler bindingScheduler) : IOutput, IBindableFactory
    {
        // One list, so that Dispose releases the entries in the order of their registration.
        private readonly List<IDisposable> registrations = [];

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
