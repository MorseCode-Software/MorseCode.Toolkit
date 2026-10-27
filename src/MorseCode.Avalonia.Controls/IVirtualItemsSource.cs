using JetBrains.Annotations;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     The values that a <see cref="VirtualizingItemsControl" /> reads from a view model and writes to
///     it. The view model gives them to the control as one object, and the control binds to all of them.
/// </summary>
/// <remarks>
///     <para>
///         Make a source with <c>CreateVirtualItemsSource</c>. It builds the values so that they agree: the
///         rows that answer the range, and the request that the report clears.
///     </para>
///     <para>
///         The values go in two pairs. The control writes <see cref="VisibleRange" />, and the view model
///         answers with <see cref="Rows" />. The view model makes a <see cref="ScrollRequest" />, and the
///         control answers with <see cref="ScrollHandled" />.
///     </para>
/// </remarks>
[PublicAPI]
public interface IVirtualItemsSource
{
    /// <summary>
    ///     Gets the number of positions in the whole list.
    /// </summary>
    IOneWayBindableValue<int> TotalCount { get; }

    /// <summary>
    ///     Gets the rows for some of the positions, with the position of the first.
    /// </summary>
    IOneWayBindableValue<VirtualRows> Rows { get; }

    /// <summary>
    ///     Gets the value that the control writes with the positions that show, with its buffer on each
    ///     side.
    /// </summary>
    IOneWayToSourceBindableValue<ItemRange> VisibleRange { get; }

    /// <summary>
    ///     Gets the position that the view model wants to show, or none.
    /// </summary>
    /// <remarks>
    ///     The view model keeps the request until the control reports it. Thus, a request made before
    ///     the control is bound or laid out is not lost.
    /// </remarks>
    IOneWayBindableValue<Maybe<int>> ScrollRequest { get; }

    /// <summary>
    ///     Gets the value that the control writes with the position of a request that it handled.
    /// </summary>
    /// <remarks>
    ///     The control writes the position, and then none at once. A view model ignores none. The
    ///     view model clears the request only if the request is still that position.
    /// </remarks>
    IOneWayToSourceBindableValue<Maybe<int>> ScrollHandled { get; }
}
