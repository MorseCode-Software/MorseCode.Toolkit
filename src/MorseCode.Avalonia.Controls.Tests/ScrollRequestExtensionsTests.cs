using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MorseCode.Mvvm;
using SodaFlow;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Avalonia.Controls.Tests;

public sealed class ScrollRequestExtensionsTests
{
    private static readonly BindableFactory Factory = new(bindingScheduler: BindingScheduler.Immediate);

    [Test]
    public async Task TheRequestIsNoneUntilARequestComes()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();

        ScrollRequestBindables scroll = Factory.CreateScrollRequest(requests: requests);

        await Assert.That(scroll.Request.Value).IsEqualTo(expected: Maybe<ScrollRequest>.None);
    }

    [Test]
    public async Task ARequestMakesTheValueThatPosition()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();
        ScrollRequestBindables scroll = Factory.CreateScrollRequest(requests: requests);

        requests.Send(a: 700);

        await Assert.That(scroll.Request.Value).IsEqualTo(expected: Asked(position: 700, number: 0));
    }

    [Test]
    public async Task TheLastRequestWins()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();
        ScrollRequestBindables scroll = Factory.CreateScrollRequest(requests: requests);

        requests.Send(a: 300);
        requests.Send(a: 700);

        await Assert.That(scroll.Request.Value).IsEqualTo(expected: Asked(position: 700, number: 1));
    }

    // The control does this after it scrolls.
    [Test]
    public async Task AReportOfTheRequestThatIsThereClearsIt()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();
        ScrollRequestBindables scroll = Factory.CreateScrollRequest(requests: requests);

        requests.Send(a: 700);
        scroll.Handled.Execute(parameter: new ScrollRequest(Position: 700, Number: 0));

        await Assert.That(scroll.Request.Value).IsEqualTo(expected: Maybe<ScrollRequest>.None);
    }

    // The race that this design removes: the request changed while the report was on its way.
    [Test]
    public async Task AReportOfAnOlderRequestLeavesTheNewerRequest()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();
        ScrollRequestBindables scroll = Factory.CreateScrollRequest(requests: requests);

        requests.Send(a: 700);
        requests.Send(a: 800);
        scroll.Handled.Execute(parameter: new ScrollRequest(Position: 700, Number: 0));

        await Assert.That(scroll.Request.Value).IsEqualTo(expected: Asked(position: 800, number: 1));
    }

    // A binding thread that takes two changes together shows the control only the last. The number makes
    // the second request a different value. Thus, the control does not have to see none between the two.
    [Test]
    public async Task ASecondRequestForAPositionIsADifferentValue()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();
        ScrollRequestBindables scroll = Factory.CreateScrollRequest(requests: requests);
        List<Maybe<ScrollRequest>> seen = [];
        scroll.Request.PropertyChanged += (_, _) => seen.Add(item: scroll.Request.Value);

        requests.Send(a: 700);
        requests.Send(a: 700);

        await Assert.That(seen)
            .IsEquivalentTo(
                expected: [Asked(position: 700, number: 0), Asked(position: 700, number: 1)],
                ordering: CollectionOrdering.Matching);
    }

    // The report names the request and not only the position. A report that is on its way cannot clear a
    // newer request for the same position, which the control did not handle.
    [Test]
    public async Task AReportOfAnOlderRequestForTheSamePositionLeavesTheNewerRequest()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();
        ScrollRequestBindables scroll = Factory.CreateScrollRequest(requests: requests);

        requests.Send(a: 700);
        requests.Send(a: 700);
        scroll.Handled.Execute(parameter: new ScrollRequest(Position: 700, Number: 0));

        await Assert.That(scroll.Request.Value).IsEqualTo(expected: Asked(position: 700, number: 1));
    }

    // The number goes up at each request, also after a clear. A number that a clear gives again makes
    // a request equal to one that the control handled before.
    [Test]
    public async Task ARequestAfterAClearGetsANewNumber()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();
        StreamSink<Unit> clears = Stream.CreateSink<Unit>();

        ScrollRequestBindables scroll = Factory.CreateScrollRequest(requests: requests, clears: clears);

        requests.Send(a: 700);
        clears.Send(a: Unit.Value);
        requests.Send(a: 700);

        await Assert.That(scroll.Request.Value).IsEqualTo(expected: Asked(position: 700, number: 1));
    }

    [Test]
    public async Task ARequestAfterTheReportMakesTheValueThatPositionAgain()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();
        ScrollRequestBindables scroll = Factory.CreateScrollRequest(requests: requests);
        List<Maybe<ScrollRequest>> seen = [];
        scroll.Request.PropertyChanged += (_, _) => seen.Add(item: scroll.Request.Value);

        requests.Send(a: 700);
        scroll.Handled.Execute(parameter: new ScrollRequest(Position: 700, Number: 0));
        requests.Send(a: 700);

        await Assert.That(seen)
            .IsEquivalentTo(
                expected:
                [
                    Asked(position: 700, number: 0), Maybe<ScrollRequest>.None, Asked(position: 700, number: 1)
                ],
                ordering: CollectionOrdering.Matching);
    }

    [Test]
    public async Task AClearOfTheViewModelWithdrawsTheRequest()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();
        StreamSink<Unit> clears = Stream.CreateSink<Unit>();

        ScrollRequestBindables scroll = Factory.CreateScrollRequest(requests: requests, clears: clears);

        requests.Send(a: 700);
        clears.Send(a: Unit.Value);

        await Assert.That(scroll.Request.Value).IsEqualTo(expected: Maybe<ScrollRequest>.None);
    }

    [Test]
    public async Task ARequestAndAClearInOneTransactionLeaveTheRequest()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();
        StreamSink<Unit> clears = Stream.CreateSink<Unit>();

        ScrollRequestBindables scroll = Factory.CreateScrollRequest(requests: requests, clears: clears);

        Transaction.RunVoid(
            action: () =>
            {
                clears.Send(a: Unit.Value);
                requests.Send(a: 700);
            });

        await Assert.That(scroll.Request.Value).IsEqualTo(expected: Asked(position: 700, number: 0));
    }

    // A view model gets its factory from IOutput. Dispose of the view model releases the value.
    [Test]
    public async Task TheValueStopsItsNotificationsWhenTheViewModelIsDisposed()
    {
        StreamSink<int> requests = Stream.CreateSink<int>();
        List<ScrollRequestBindables> made = [];

        ViewModelBase viewModel =
            ViewModelBase.CreateBase(
                bindingScheduler: BindingScheduler.Immediate,
                continuation: (output, construct) =>
                {
                    made.Add(item: output.BindableFactory.CreateScrollRequest(requests: requests));

                    return construct.Construct(constructor: static viewModelBase => viewModelBase);
                });

        int notifications = 0;
        made[0].Request.PropertyChanged += (_, _) => notifications++;

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
                    ScrollRequestExtensions.CreateScrollRequest(
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

    private static Maybe<ScrollRequest> Asked(int position, long number) =>
        Maybe<ScrollRequest>.Some(value: new ScrollRequest(Position: position, Number: number));

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
