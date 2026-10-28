using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     The values of a <see cref="VirtualizingItemsControl" />. <c>CreateVirtualItemsSource</c> makes
///     it, and gives it to the view model as an <see cref="IVirtualItemsSource" />.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of IVirtualItemsSource does not say how this class makes the source.
internal sealed class VirtualItemsSource : IVirtualItemsSource
{
    internal VirtualItemsSource(
        IOneWayBindableValue<VirtualRows> rows,
        IOneWayToSourceBindableValue<ItemRange> visibleRange,
        IOneWayBindableValue<Maybe<int>> scrollRequest,
        IOneWayToSourceBindableValue<Maybe<int>> scrollHandled)
    {
        this.Rows = rows;
        this.VisibleRange = visibleRange;
        this.ScrollRequest = scrollRequest;
        this.ScrollHandled = scrollHandled;
    }

    /// <inheritdoc />
    public IOneWayBindableValue<VirtualRows> Rows { get; }

    /// <inheritdoc />
    public IOneWayToSourceBindableValue<ItemRange> VisibleRange { get; }

    /// <inheritdoc />
    public IOneWayBindableValue<Maybe<int>> ScrollRequest { get; }

    /// <inheritdoc />
    public IOneWayToSourceBindableValue<Maybe<int>> ScrollHandled { get; }
}
