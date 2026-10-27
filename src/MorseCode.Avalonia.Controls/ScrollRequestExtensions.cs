using System;
using SodaFlow;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     Makes the bindable values that ask a list control to scroll to a position.
/// </summary>
internal static class ScrollRequestExtensions
{
    /// <summary>
    ///     Makes the bindable values that ask a list control to scroll to a position, and that
    ///     receive the report that it did.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The request is none at first. Each position on <paramref name="requests" /> makes it
    ///         that position. The control reports the position that it handled, and that clears the
    ///         request only if the request is still that position. Thus, a request that the view
    ///         model makes before the view binds is still there when the view binds. A newer
    ///         request that comes while a report is on its way is not cleared. Bind
    ///         <see cref="ScrollRequestBindables.Request" /> to the <c>ScrollRequest</c> property of
    ///         <c>VirtualizingItemsControl</c>, and <see cref="ScrollRequestBindables.Handled" /> to its
    ///         <c>ScrollHandled</c> property. Use a bindable action to fire
    ///         <paramref name="requests" />.
    ///     </para>
    /// </remarks>
    /// <param name="bindableFactory">The factory that makes the values. A view model gets it from <c>IOutput</c>.</param>
    /// <param name="requests">The positions to scroll to.</param>
    /// <returns>The bindable values.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="bindableFactory" /> or <paramref name="requests" /> is null.
    /// </exception>
    internal static ScrollRequestBindables CreateScrollRequest(
        this IBindableFactory bindableFactory,
        Stream<int> requests) =>
        bindableFactory.CreateScrollRequest(requests: requests, clears: Stream.Never<Unit>());

    /// <summary>
    ///     Makes the bindable values that ask a list control to scroll to a position, and that
    ///     receive the report that it did. The view model can also clear the request.
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
    /// <returns>The bindable values.</returns>
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

        // One transaction, so that the values start from one instant as the rest of the view model does.
        return Transaction.Run(
            f: () =>
            {
                // The reports of the view. A report of none is how the control resets itself, and it
                // clears nothing.
                StreamSink<Maybe<int>> handled = Stream.CreateSink<Maybe<int>>();

                // Each source is a change of the request. A request wins over a clear, and a clear over a
                // report. A report clears the request only if it names the request that is there now.
                Cell<Maybe<int>> request =
                    requests.Map(f: static position => Change(f: _ => Maybe<int>.Some(value: position)))
                        .OrElse(s2: clears.Map(f: static _ => Change(f: static _ => Maybe<int>.None)))
                        .OrElse(
                            s2: handled.Map(
                                f: static position =>
                                    Change(f: current => current.Equals(other: position) ? Maybe<int>.None : current)))
                        .Accum(initialState: Maybe<int>.None, f: static (change, current) => change(arg: current));

                return new ScrollRequestBindables(
                    request: bindableFactory.CreateOneWay(cell: request),
                    handled: bindableFactory.CreateOneWayToSource(
                        editsStreamSink: handled,
                        initialValue: Maybe<int>.None));
            });
    }

    // Names the type once, so that a lambda that changes the request reads as a value.
    private static Func<Maybe<int>, Maybe<int>> Change(Func<Maybe<int>, Maybe<int>> f) => f;
}
