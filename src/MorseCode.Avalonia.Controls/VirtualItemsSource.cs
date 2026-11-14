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
        IBindableAction<ReportedRange> rangeReported,
        IOneWayBindableValue<Maybe<int>> scrollRequest,
        IBindableAction<int> scrollHandled)
    {
        this.Rows = rows;
        this.RangeReported = rangeReported;
        this.ScrollRequest = scrollRequest;
        this.ScrollHandled = scrollHandled;
    }

    /// <inheritdoc />
    public IOneWayBindableValue<VirtualRows> Rows { get; }

    /// <inheritdoc />
    public IBindableAction<ReportedRange> RangeReported { get; }

    /// <inheritdoc />
    public IOneWayBindableValue<Maybe<int>> ScrollRequest { get; }

    /// <inheritdoc />
    public IBindableAction<int> ScrollHandled { get; }
}
