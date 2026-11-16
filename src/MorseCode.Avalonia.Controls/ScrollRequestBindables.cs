using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     The bindables of a scroll request: the request that the view model makes, and the action that
///     the list control runs to report it.
/// </summary>
internal sealed class ScrollRequestBindables
{
    /// <summary>
    ///     Makes the pair.
    /// </summary>
    /// <param name="request">The request to show a position, or none.</param>
    /// <param name="handled">The action that the control runs with the request that it handled.</param>
    internal ScrollRequestBindables(
        IOneWayBindableValue<Maybe<ScrollRequest>> request,
        IBindableAction<ScrollRequest> handled)
    {
        this.Request = request;
        this.Handled = handled;
    }

    /// <summary>
    ///     Gets the request to show a position, or none.
    /// </summary>
    internal IOneWayBindableValue<Maybe<ScrollRequest>> Request { get; }

    /// <summary>
    ///     Gets the action that the control runs with the request that it handled.
    /// </summary>
    internal IBindableAction<ScrollRequest> Handled { get; }
}
