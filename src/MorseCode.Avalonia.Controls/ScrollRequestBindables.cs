using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     The bindable values of a scroll request: the request that the view model makes, and the report
///     that the list control sends back.
/// </summary>
internal sealed class ScrollRequestBindables
{
    /// <summary>
    ///     Makes the pair.
    /// </summary>
    /// <param name="request">The position to show, or none.</param>
    /// <param name="handled">The position that the control handled.</param>
    internal ScrollRequestBindables(
        IOneWayBindableValue<Maybe<int>> request,
        IOneWayToSourceBindableValue<Maybe<int>> handled)
    {
        this.Request = request;
        this.Handled = handled;
    }

    /// <summary>
    ///     Gets the position to show, or none. Bind it one way to the control.
    /// </summary>
    internal IOneWayBindableValue<Maybe<int>> Request { get; }

    /// <summary>
    ///     Gets the position that the control handled. Bind it one way to the source.
    /// </summary>
    internal IOneWayToSourceBindableValue<Maybe<int>> Handled { get; }
}
