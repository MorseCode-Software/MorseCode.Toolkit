using System;
using JetBrains.Annotations;
using SodaFlow;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;

namespace MorseCode.Mvvm;

/// <summary>
///     Makes the bindable values that a view model gives to a list control.
/// </summary>
[PublicAPI]
public static class BindableFactoryExtensions
{
    /// <summary>
    ///     Makes the two-way bindable value that asks a list control to scroll to a position. The
    ///     list control writes none to it after it scrolls.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The value is none at first. Each position on <paramref name="requests" /> makes it
    ///         that position. The control writes none after it scrolls, and that clears the value.
    ///         Thus, a request that the view model makes before the view binds is still there when
    ///         the view binds. Bind the value to the <c>ScrollRequest</c> property of
    ///         <c>VirtualizingItemsControl</c>, and use a bindable action to fire
    ///         <paramref name="requests" />.
    ///     </para>
    /// </remarks>
    /// <param name="bindableFactory">The factory that makes the value. A view model gets it from <c>IOutput</c>.</param>
    /// <param name="requests">The positions to scroll to.</param>
    /// <returns>The bindable value.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="bindableFactory" /> or <paramref name="requests" /> is null.
    /// </exception>
    public static ITwoWayBindableValue<Maybe<int>> CreateScrollRequest(
        this IBindableFactory bindableFactory,
        Stream<int> requests) =>
        bindableFactory.CreateScrollRequest(requests: requests, clears: Stream.Never<Unit>());

    /// <summary>
    ///     Makes the two-way bindable value that asks a list control to scroll to a position. The
    ///     view model can also clear the value.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This is the overload without <paramref name="clears" />, with one addition. Each
    ///         value on <paramref name="clears" /> sets the value to none. Use it to withdraw a
    ///         request that the control did not use yet.
    ///     </para>
    ///     <para>
    ///         When a request and a clear come in one transaction, the request wins.
    ///     </para>
    /// </remarks>
    /// <param name="bindableFactory">The factory that makes the value. A view model gets it from <c>IOutput</c>.</param>
    /// <param name="requests">The positions to scroll to.</param>
    /// <param name="clears">The events that withdraw the request.</param>
    /// <returns>The bindable value.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="bindableFactory" />, <paramref name="requests" />, or
    ///     <paramref name="clears" /> is null.
    /// </exception>
    public static ITwoWayBindableValue<Maybe<int>> CreateScrollRequest(
        this IBindableFactory bindableFactory,
        Stream<int> requests,
        Stream<Unit> clears)
    {
        if (bindableFactory is null)
        {
            throw new ArgumentNullException(paramName: nameof(bindableFactory));
        }

        if (requests is null)
        {
            throw new ArgumentNullException(paramName: nameof(requests));
        }

        if (clears is null)
        {
            throw new ArgumentNullException(paramName: nameof(clears));
        }

        // One transaction, so that the value starts from one instant as the rest of the view model does.
        return Transaction.Run(
            f: () =>
            {
                // The writes of the view. The control writes none here after it scrolls.
                StreamSink<Maybe<int>> edits = Stream.CreateSink<Maybe<int>>();

                Cell<Maybe<int>> request =
                    requests.Map(f: static position => Maybe<int>.Some(value: position))
                        .OrElse(s2: clears.Map(f: static _ => Maybe<int>.None))
                        .OrElse(s2: edits)
                        .Hold(initialValue: Maybe<int>.None);

                return bindableFactory.CreateTwoWay(cell: request, editsStreamSink: edits);
            });
    }
}
