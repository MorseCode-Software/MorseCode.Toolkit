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
    /// <param name="request">The position to show, or none.</param>
    /// <param name="handled">The action that the control runs with the position that it handled.</param>
    internal ScrollRequestBindables(IOneWayBindableValue<Maybe<int>> request, IBindableAction<int> handled)
    {
        this.Request = request;
        this.Handled = handled;
    }

    /// <summary>
    ///     Gets the position to show, or none. Bind it one way to the control.
    /// </summary>
    internal IOneWayBindableValue<Maybe<int>> Request { get; }

    /// <summary>
    ///     Gets the action that the control runs with the position that it handled.
    /// </summary>
    internal IBindableAction<int> Handled { get; }
}
