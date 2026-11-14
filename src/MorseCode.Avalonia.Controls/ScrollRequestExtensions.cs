using System;
using SodaFlow;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     Makes the bindables that ask a list control to scroll to a position.
/// </summary>
internal static class ScrollRequestExtensions
{
    /// <summary>
    ///     Makes the bindables that ask a list control to scroll to a position, and that receive
    ///     the report that it did.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The request is none at first. Each position on <paramref name="requests" /> makes it
    ///         a request for that position, with the next number. The control reports the request
    ///         that it handled, and that clears the request only if the request is still that one.
    ///         Thus, a request that the view model makes before the view binds is still there when
    ///         the view binds. A newer request that comes while a report is on its way is not
    ///         cleared, also when it asks for the same position. Use a bindable action to fire
    ///         <paramref name="requests" />.
    ///     </para>
    /// </remarks>
    /// <param name="bindableFactory">The factory that makes the values. A view model gets it from <c>IOutput</c>.</param>
    /// <param name="requests">The positions to scroll to.</param>
    /// <returns>The bindables.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="bindableFactory" /> or <paramref name="requests" /> is null.
    /// </exception>
    internal static ScrollRequestBindables CreateScrollRequest(
        this IBindableFactory bindableFactory,
        Stream<int> requests) =>
        bindableFactory.CreateScrollRequest(requests: requests, clears: Stream.Never<Unit>());

    /// <summary>
    ///     Makes the bindables that ask a list control to scroll to a position, and that receive
    ///     the report that it did. The view model can also clear the request.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This is the overload without <paramref name="clears" />, with one addition. Each
    ///         value on <paramref name="clears" /> sets the request to none. Use it to withdraw a
    ///         request that the control did not use yet.
    ///     </para>
    ///     <para>
    ///         When a request and a clear or a report come in one transaction, the request wins.
    ///     </para>
    /// </remarks>
    /// <param name="bindableFactory">The factory that makes the values. A view model gets it from <c>IOutput</c>.</param>
    /// <param name="requests">The positions to scroll to.</param>
    /// <param name="clears">The events that withdraw the request.</param>
    /// <returns>The bindables.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="bindableFactory" />, <paramref name="requests" />, or
    ///     <paramref name="clears" /> is null.
    /// </exception>
    internal static ScrollRequestBindables CreateScrollRequest(
        this IBindableFactory bindableFactory,
        Stream<int> requests,
        Stream<Unit> clears)
    {
        ArgumentNullException.ThrowIfNull(argument: bindableFactory);
        ArgumentNullException.ThrowIfNull(argument: requests);
        ArgumentNullException.ThrowIfNull(argument: clears);

        // One transaction, so that the bindables start from one instant as the rest of the view model does.
        return Transaction.Run(
            f: () =>
            {
                // The reports of the view. The control reports one time for each request that it handles.
                StreamSink<ScrollRequest> handled = Stream.CreateSink<ScrollRequest>();

                // Each source is a change of the state. A request wins over a clear, and a clear over a
                // report. A report clears the request only if it is the request that is there now.
                Cell<Maybe<ScrollRequest>> request =
                    requests.Map(f: static position => Change(f: state => state.Ask(position: position)))
                        .OrElse(s2: clears.Map(f: static _ => Change(f: static state => state.Clear())))
                        .OrElse(
                            s2: handled.Map(
                                f: static reported =>
                                    Change(f: state => state.IsFor(request: reported) ? state.Clear() : state)))
                        .Accum(initialState: State.Empty, f: static (change, state) => change(arg: state))
                        .Map(f: static state => state.Request);

                return new ScrollRequestBindables(
                    request: bindableFactory.CreateOneWay(cell: request),
                    handled: bindableFactory.CreateBindableAction(firingsStreamSink: handled));
            });
    }

    // Names the type once, so that a lambda that changes the state reads as a value.
    private static Func<State, State> Change(Func<State, State> f) => f;

    // The request that is there now, and the number that the next request gets. The number goes up
    // at each request and never down. Thus, no two requests of one source are equal.
    private readonly record struct State(Maybe<ScrollRequest> Request, long Next)
    {
        internal static State Empty { get; } = new(Request: Maybe<ScrollRequest>.None, Next: 0);

        internal State Ask(int position) =>
            new(
                Request: Maybe<ScrollRequest>.Some(value: new ScrollRequest(Position: position, Number: this.Next)),
                Next: this.Next + 1);

        internal State Clear() => this with { Request = Maybe<ScrollRequest>.None };

        internal bool IsFor(ScrollRequest request) =>
            this.Request.Equals(other: Maybe<ScrollRequest>.Some(value: request));
    }
}
