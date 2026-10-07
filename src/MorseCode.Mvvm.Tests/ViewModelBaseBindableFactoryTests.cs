using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SodaFlow;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Mvvm.Tests;

// Each test makes a bindable through the factory of the output, and examines a behavior that the
// bindable shows only until its Dispose method runs. Thus, each test shows that Dispose of the view
// model disposes the object that the factory made.
public sealed class ViewModelBaseBindableFactoryTests
{
    [Test]
    public async Task OneWayValueStopsItsNotificationsAtDispose()
    {
        CellSink<int> cell = Cell.CreateSink(initialValue: 1);
        List<IOneWayBindableValue<int>> made = [];

        ViewModelBase viewModel =
            Create(register: output => made.Add(item: output.BindableFactory.CreateOneWay(cell: cell)));

        int notifications = 0;
        made[0].PropertyChanged += (_, _) => notifications++;

        cell.Send(a: 2);
        int notificationsBeforeDispose = notifications;

        viewModel.Dispose();
        cell.Send(a: 3);

        await Assert.That(notificationsBeforeDispose).IsEqualTo(expected: 1);
        await Assert.That(notifications).IsEqualTo(expected: 1);
    }

    [Test]
    public async Task TwoWayValueOverAStreamRefusesAWriteAfterDispose()
    {
        CellSink<int> cell = Cell.CreateSink(initialValue: 1);
        StreamSink<int> edits = Stream.CreateSink<int>();
        List<ITwoWayBindableValue<int>> made = [];

        ViewModelBase viewModel =
            Create(
                register: output =>
                    made.Add(item: output.BindableFactory.CreateTwoWay(cell: cell, editsStreamSink: edits)));

        made[0].Value = 2;
        viewModel.Dispose();

        Exception caught = Catch(action: () => made[0].Value = 3);

        await Assert.That(caught).IsTypeOf<ObjectDisposedException>();
    }

    [Test]
    public async Task TwoWayValueOverACellSinkRefusesAWriteAfterDispose()
    {
        CellSink<int> sink = Cell.CreateSink(initialValue: 1);
        List<ITwoWayBindableValue<int>> made = [];

        ViewModelBase viewModel =
            Create(register: output => made.Add(item: output.BindableFactory.CreateTwoWay(sink: sink)));

        made[0].Value = 2;
        viewModel.Dispose();

        Exception caught = Catch(action: () => made[0].Value = 3);

        await Assert.That(caught).IsTypeOf<ObjectDisposedException>();
    }

    [Test]
    public async Task OneWayToSourceValueOverAStreamDropsAWriteAfterDispose()
    {
        StreamSink<int> edits = Stream.CreateSink<int>();
        List<int> received = [];
        IListener listener = edits.ListenStrong(handler: received.Add);
        List<IOneWayToSourceBindableValue<int>> made = [];

        ViewModelBase viewModel =
            Create(
                register: output =>
                    made.Add(
                        item: output.BindableFactory.CreateOneWayToSource(editsStreamSink: edits, initialValue: 0)));

        made[0].Value = 2;
        viewModel.Dispose();
        made[0].Value = 3;
        listener.Unlisten();

        await Assert.That(received).IsEquivalentTo(expected: [2], ordering: CollectionOrdering.Matching);
    }

    [Test]
    public async Task OneWayToSourceValueOverACellSinkDropsAWriteAfterDispose()
    {
        CellSink<int> sink = Cell.CreateSink(initialValue: 0);
        List<IOneWayToSourceBindableValue<int>> made = [];

        ViewModelBase viewModel =
            Create(register: output => made.Add(item: output.BindableFactory.CreateOneWayToSource(sink: sink)));

        made[0].Value = 2;
        viewModel.Dispose();
        made[0].Value = 3;

        await Assert.That(sink.Sample()).IsEqualTo(expected: 2);
    }

    [Test]
    public async Task ActionWithAParameterIsNotAvailableAfterDispose()
    {
        StreamSink<string> firings = Stream.CreateSink<string>();
        List<IBindableAction<string>> made = [];

        ViewModelBase viewModel =
            Create(
                register: output =>
                    made.Add(item: output.BindableFactory.CreateBindableAction(firingsStreamSink: firings)));

        bool availableBeforeDispose = made[0].CanExecute(parameter: "parameter");
        viewModel.Dispose();

        await Assert.That(availableBeforeDispose).IsTrue();
        await Assert.That(made[0].CanExecute(parameter: "parameter")).IsFalse();
    }

    [Test]
    public async Task ActionWithNoParameterIsNotAvailableAfterDispose()
    {
        StreamSink<Unit> firings = Stream.CreateSink<Unit>();
        List<IBindableAction> made = [];

        ViewModelBase viewModel =
            Create(
                register: output =>
                    made.Add(item: output.BindableFactory.CreateBindableAction(firingsStreamSink: firings)));

        bool availableBeforeDispose = made[0].CanExecute(parameter: null);
        viewModel.Dispose();

        await Assert.That(availableBeforeDispose).IsTrue();
        await Assert.That(made[0].CanExecute(parameter: null)).IsFalse();
    }

    [Test]
    public async Task ActionWithAnOptionalParameterIsNotAvailableAfterDispose()
    {
        StreamSink<Maybe<string>> firings = Stream.CreateSink<Maybe<string>>();
        List<IBindableAction<Maybe<string>>> made = [];

        ViewModelBase viewModel =
            Create(
                register: output =>
                    made.Add(item: output.BindableFactory.CreateBindableAction(firingsStreamSink: firings)));

        bool availableBeforeDispose = made[0].CanExecute(parameter: null);
        viewModel.Dispose();

        await Assert.That(availableBeforeDispose).IsTrue();
        await Assert.That(made[0].CanExecute(parameter: null)).IsFalse();
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
}
