using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SodaFlow;
using SodaFlow.Bindable.ObjectModel;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Mvvm.Tests;

public sealed class ViewModelBaseTests
{
    [Test]
    public async Task NullSchedulerFailsAtTheCall()
    {
        Exception caught = Catch(
            action: static () => ViewModelBase.CreateBase(
                // ReSharper disable once NullableWarningSuppressionIsUsed - The null scheduler is the input under test: CreateBase must refuse it at run time.
                bindingScheduler: null!,
                continuation: static (_, construct) =>
                    construct.Construct(constructor: static viewModelBase => viewModelBase)));

        await Assert.That(caught).IsTypeOf<ArgumentNullException>();
        await Assert.That(((ArgumentNullException)caught).ParamName).IsEqualTo(expected: "bindingScheduler");
    }

    [Test]
    public async Task NullDisposableFailsAtRegistration()
    {
        Exception? caught = null;

        _ = Create(
            register: output =>
                // ReSharper disable once NullableWarningSuppressionIsUsed - The null entry is the input under test: AddDisposable must refuse it at run time.
                caught = Catch(action: () => output.AddDisposable<IDisposable>(disposable: null!)));

        await Assert.That(caught).IsTypeOf<ArgumentNullException>();
    }

    [Test]
    public async Task NullListenerFailsAtRegistration()
    {
        Exception? caught = null;

        _ = Create(
            register: output =>
                // ReSharper disable once NullableWarningSuppressionIsUsed - The null entry is the input under test: AddListener must refuse it at run time.
                caught = Catch(action: () => output.AddListener<IWeakListener>(listener: null!)));

        await Assert.That(caught).IsTypeOf<ArgumentNullException>();
    }

    // Before the check, a null entry got to Dispose, and the composite refused it there. Thus, Dispose
    // threw and disposed none of the entries.
    [Test]
    public async Task RefusedNullDoesNotStopTheDisposalOfTheOtherEntries()
    {
        List<string> log = [];

        ViewModelBase viewModel = Create(
            register: output =>
            {
                output.AddDisposable(disposable: new Recording(log: log, name: "A"));

                // ReSharper disable once NullableWarningSuppressionIsUsed - The null entry is the input under test: AddDisposable must refuse it at run time.
                _ = Catch(action: () => output.AddDisposable<IDisposable>(disposable: null!));

                output.AddDisposable(disposable: new Recording(log: log, name: "B"));
            });

        viewModel.Dispose();

        await Assert.That(log).IsEquivalentTo(expected: ["B", "A"], ordering: CollectionOrdering.Matching);
    }

    [Test]
    public async Task DisposeDisposesTheEntriesOfTheConstructionOneTime()
    {
        List<string> log = [];

        ViewModelBase viewModel = Create(
            register: output =>
            {
                output.AddDisposable(disposable: new Recording(log: log, name: "A"));
                output.AddDisposable(disposable: new Recording(log: log, name: "B"));
            });

        viewModel.Dispose();
        viewModel.Dispose();

        await Assert.That(log).IsEquivalentTo(expected: ["B", "A"], ordering: CollectionOrdering.Matching);
    }

    // Before one list held the two kinds, Dispose stopped each listener before it disposed any
    // disposable, in the sequence B, A, C. After that, one list released them in the sequence A, B, C.
    // That sequence stopped a registration after the registrations that it can use.
    [Test]
    public async Task DisposeReleasesListenersAndDisposablesInTheReverseOrderOfTheirRegistration()
    {
        List<string> log = [];

        ViewModelBase viewModel = Create(
            register: output =>
            {
                output.AddDisposable(disposable: new Recording(log: log, name: "A"));
                output.AddListener(listener: new RecordingListener(log: log, name: "B"));
                output.AddDisposable(disposable: new Recording(log: log, name: "C"));
            });

        viewModel.Dispose();

        await Assert.That(log).IsEquivalentTo(expected: ["C", "B", "A"], ordering: CollectionOrdering.Matching);
    }

    [Test]
    public async Task TwoFailuresThrowOneAggregateInTheOrderOfTheirRelease()
    {
        Exception first = new InvalidOperationException(message: "A failed.");
        Exception second = new InvalidOperationException(message: "B failed.");

        ViewModelBase viewModel = Create(
            register: output =>
            {
                output.AddDisposable(disposable: Disposable.FromAction(onDispose: () => throw first));
                output.AddDisposable(disposable: Disposable.FromAction(onDispose: () => throw second));
            });

        Exception caught = Catch(action: viewModel.Dispose);

        await Assert.That(caught).IsTypeOf<AggregateException>();

        await Assert
            .That(((AggregateException)caught).InnerExceptions)
            .IsEquivalentTo(expected: [second, first], ordering: CollectionOrdering.Matching);
    }

    // Before the seal, the composite read the registrations in Dispose. Thus, Dispose released an
    // entry that a kept output added after construction, and an entry added after Dispose leaked.
    [Test]
    public async Task DisposableRegistrationAfterConstructionFailsAndIsNotKept()
    {
        List<string> log = [];
        List<ViewModelBase.IOutput> kept = [];

        ViewModelBase viewModel = Create(register: kept.Add);

        Exception caught =
            Catch(action: () => kept[0].AddDisposable(disposable: new Recording(log: log, name: "late")));

        viewModel.Dispose();

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
        await Assert.That(log).IsEmpty();
    }

    [Test]
    public async Task ListenerRegistrationAfterConstructionFailsAndIsNotKept()
    {
        List<string> log = [];
        List<ViewModelBase.IOutput> kept = [];

        ViewModelBase viewModel = Create(register: kept.Add);

        Exception caught =
            Catch(action: () => kept[0].AddListener(listener: new RecordingListener(log: log, name: "late")));

        viewModel.Dispose();

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
        await Assert.That(log).IsEmpty();
    }

    [Test]
    public async Task BindableAfterConstructionFails()
    {
        List<ViewModelBase.IOutput> kept = [];

        _ = Create(register: kept.Add);

        Exception caught =
            Catch(action: () => kept[0].BindableFactory.CreateOneWay(cell: Cell.CreateSink(initialValue: 1)));

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
    }

    // The base seals the output when the subclass calls Construct, and before the constructor runs.
    [Test]
    public async Task RegistrationInTheConstructorFails()
    {
        List<string> log = [];

        Exception caught = Catch(
            action: () => ViewModelBase.CreateBase(
                bindingScheduler: BindingScheduler.Immediate,
                continuation: (output, construct) => construct.Construct(
                    constructor: viewModelBase =>
                    {
                        output.AddDisposable(disposable: new Recording(log: log, name: "in constructor"));

                        return viewModelBase;
                    })));

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
    }

    // A second Construct call cannot make a second owner of the same entries.
    [Test]
    public async Task SecondConstructFails()
    {
        Exception caught = Catch(
            action: static () => ViewModelBase.CreateBase(
                bindingScheduler: BindingScheduler.Immediate,
                continuation: static (_, construct) =>
                {
                    construct.Construct(constructor: static viewModelBase => viewModelBase).Dispose();

                    return construct.Construct(constructor: static viewModelBase => viewModelBase);
                }));

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
    }

    // A field-like event keeps each handler, and the target of the handler, alive with the view model.
    [Test]
    public async Task PropertyChangedKeepsNoHandler()
    {
        ViewModelBase viewModel = Create(register: static _ => { });

        WeakReference subscriber = Subscribe(viewModel: viewModel);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        await Assert.That(subscriber.IsAlive).IsFalse();

        GC.KeepAlive(obj: viewModel);
    }

    [Test]
    public async Task PropertyChangedAcceptsTheRemovalOfAHandler()
    {
        ViewModelBase viewModel = Create(register: static _ => { });
        Subscriber subscriber = new();

        viewModel.PropertyChanged += subscriber.OnPropertyChanged;
        viewModel.PropertyChanged -= subscriber.OnPropertyChanged;

        await Assert.That(viewModel).IsNotNull();
    }

    // A different method, so that no local of the test keeps the subscriber alive.
    [MethodImpl(methodImplOptions: MethodImplOptions.NoInlining)]
    private static WeakReference Subscribe(INotifyPropertyChanged viewModel)
    {
        Subscriber subscriber = new();
        viewModel.PropertyChanged += subscriber.OnPropertyChanged;

        return new WeakReference(target: subscriber);
    }

    private static ViewModelBase Create(Action<ViewModelBase.IOutput> register) =>
        ViewModelBase.CreateBase(
            bindingScheduler: BindingScheduler.Immediate,
            continuation: (output, construct) =>
            {
                register(obj: output);

                return construct.Construct(constructor: static viewModelBase => viewModelBase);
            });

    private static Exception Catch(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            return exception;
        }

        throw new InvalidOperationException(message: "The action did not throw an exception.");
    }

    private sealed class Recording(ICollection<string> log, string name) : IDisposable
    {
        private ICollection<string> Log { get; } = log;

        private string Name { get; } = name;

        public void Dispose() => this.Log.Add(this.Name);
    }

    private sealed class Subscriber
    {
        // ReSharper disable once MemberCanBeMadeStatic.Local - The handler must be an instance method, so that the delegate refers to the subscriber that the test watches.
        public void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
        }
    }

    private sealed class RecordingListener(ICollection<string> log, string name) : IWeakListener
    {
        private ICollection<string> Log { get; } = log;

        private string Name { get; } = name;

        public void Unlisten() => this.Log.Add(this.Name);

        public IListenerWithWeakReference GetListenerWithWeakReference() =>
            throw new NotSupportedException(message: "ViewModelBase does not ask for this view.");
    }
}
