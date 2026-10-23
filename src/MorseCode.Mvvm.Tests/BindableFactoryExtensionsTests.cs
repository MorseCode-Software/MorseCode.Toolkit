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

public sealed class BindableFactoryExtensionsTests
{
    private static readonly BindableFactory Factory = new(bindingScheduler: BindingScheduler.Immediate);

    [Test]
    public async Task TheValueIsNoneUntilARequestComes()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();

        ITwoWayBindableValue<Maybe<int>> scrollRequest = Factory.CreateScrollRequest(requests: requests);

        await Assert.That(scrollRequest.Value).IsEqualTo(expected: Maybe<int>.None);
    }

    [Test]
    public async Task ARequestMakesTheValueThatPosition()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();
        ITwoWayBindableValue<Maybe<int>> scrollRequest = Factory.CreateScrollRequest(requests: requests);

        requests.Send(a: 700);

        await Assert.That(scrollRequest.Value).IsEqualTo(expected: Maybe<int>.Some(value: 700));
    }

    [Test]
    public async Task TheLastRequestWins()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();
        ITwoWayBindableValue<Maybe<int>> scrollRequest = Factory.CreateScrollRequest(requests: requests);

        requests.Send(a: 300);
        requests.Send(a: 700);

        await Assert.That(scrollRequest.Value).IsEqualTo(expected: Maybe<int>.Some(value: 700));
    }

    // The control does this after it scrolls.
    [Test]
    public async Task AWriteOfNoneByTheViewClearsTheRequest()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();
        ITwoWayBindableValue<Maybe<int>> scrollRequest = Factory.CreateScrollRequest(requests: requests);

        requests.Send(a: 700);
        scrollRequest.Value = Maybe<int>.None;

        await Assert.That(scrollRequest.Value).IsEqualTo(expected: Maybe<int>.None);
    }

    [Test]
    public async Task ARequestAfterTheClearMakesTheValueThatPositionAgain()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();
        ITwoWayBindableValue<Maybe<int>> scrollRequest = Factory.CreateScrollRequest(requests: requests);
        List<Maybe<int>> seen = [];
        scrollRequest.PropertyChanged += (_, _) => seen.Add(item: scrollRequest.Value);

        requests.Send(a: 700);
        scrollRequest.Value = Maybe<int>.None;
        requests.Send(a: 700);

        await Assert.That(seen)
            .IsEquivalentTo(
                expected: [Maybe<int>.Some(value: 700), Maybe<int>.None, Maybe<int>.Some(value: 700)],
                ordering: CollectionOrdering.Matching);
    }

    [Test]
    public async Task AClearOfTheViewModelWithdrawsTheRequest()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();
        StreamSink<Unit> clears = Stream.CreateSink<Unit>();

        ITwoWayBindableValue<Maybe<int>> scrollRequest =
            Factory.CreateScrollRequest(requests: requests, clears: clears);

        requests.Send(a: 700);
        clears.Send(a: Unit.Value);

        await Assert.That(scrollRequest.Value).IsEqualTo(expected: Maybe<int>.None);
    }

    [Test]
    public async Task ARequestAndAClearInOneTransactionLeaveTheRequest()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();
        StreamSink<Unit> clears = Stream.CreateSink<Unit>();

        ITwoWayBindableValue<Maybe<int>> scrollRequest =
            Factory.CreateScrollRequest(requests: requests, clears: clears);

        Transaction.RunVoid(
            action: () =>
            {
                clears.Send(a: Unit.Value);
                requests.Send(a: 700);
            });

        await Assert.That(scrollRequest.Value).IsEqualTo(expected: Maybe<int>.Some(value: 700));
    }

    // A view model gets its factory from IOutput. Dispose of the view model releases the value.
    [Test]
    public async Task TheValueStopsItsNotificationsWhenTheViewModelIsDisposed()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();
        List<ITwoWayBindableValue<Maybe<int>>> made = [];

        ViewModelBase viewModel =
            ViewModelBase.CreateBase(
                bindingScheduler: BindingScheduler.Immediate,
                continuation: (output, construct) =>
                {
                    made.Add(item: output.BindableFactory.CreateScrollRequest(requests: requests));

                    return construct.Construct(constructor: static viewModelBase => viewModelBase);
                });

        int notifications = 0;
        made[0].PropertyChanged += (_, _) => notifications++;

        requests.Send(a: 1);
        int notificationsBeforeDispose = notifications;

        viewModel.Dispose();
        requests.Send(a: 2);

        await Assert.That(notificationsBeforeDispose).IsEqualTo(expected: 1);
        await Assert.That(notifications).IsEqualTo(expected: 1);
    }

    [Test]
    public async Task ANullArgumentIsRefusedAndNamed()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();

        // The null of each call is the input under test: the method must refuse it at run time.
        Exception noFactory =
            Catch(
                action: () =>
                    // ReSharper disable once NullableWarningSuppressionIsUsed - The null is the input under test.
                    BindableFactoryExtensions.CreateScrollRequest(
                        bindableFactory: null!,
                        requests: requests));

        // ReSharper disable once NullableWarningSuppressionIsUsed - The null is the input under test.
        Exception noRequests = Catch(action: static () => Factory.CreateScrollRequest(requests: null!));

        Exception noClears =
            Catch(
                // ReSharper disable once NullableWarningSuppressionIsUsed - The null is the input under test.
                action: () => Factory.CreateScrollRequest(requests: requests, clears: null!));

        await Assert.That((noFactory as ArgumentNullException)?.ParamName).IsEqualTo(expected: "bindableFactory");
        await Assert.That((noRequests as ArgumentNullException)?.ParamName).IsEqualTo(expected: "requests");
        await Assert.That((noClears as ArgumentNullException)?.ParamName).IsEqualTo(expected: "clears");
    }

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
