using System;
using JetBrains.Annotations;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     The values of a <see cref="VirtualizingItemsControl" />, and the resources behind them. Dispose of
///     it with the view model.
/// </summary>
/// <remarks>
///     Make it with <c>CreateVirtualItemsSource</c>. Disposing of it releases the rows that the
///     collection overload built. It does not dispose of the bindable values, because the factory that
///     made them owns them.
/// </remarks>
[PublicAPI]
// ReSharper disable once InheritdocConsiderUsage - The summary of IVirtualItemsSource does not say what this class releases.
public sealed class VirtualItemsSource : IVirtualItemsSource, IDisposable
{
    private readonly IDisposable? owned;

    internal VirtualItemsSource(
        IOneWayBindableValue<int> totalCount,
        IOneWayBindableValue<VirtualRows> rows,
        IOneWayToSourceBindableValue<ItemRange> visibleRange,
        IOneWayBindableValue<Maybe<int>> scrollRequest,
        IOneWayToSourceBindableValue<Maybe<int>> scrollHandled,
        IDisposable? owned)
    {
        this.TotalCount = totalCount;
        this.Rows = rows;
        this.VisibleRange = visibleRange;
        this.ScrollRequest = scrollRequest;
        this.ScrollHandled = scrollHandled;
        this.owned = owned;
    }

    /// <inheritdoc />
    public IOneWayBindableValue<int> TotalCount { get; }

    /// <inheritdoc />
    public IOneWayBindableValue<VirtualRows> Rows { get; }

    /// <inheritdoc />
    public IOneWayToSourceBindableValue<ItemRange> VisibleRange { get; }

    /// <inheritdoc />
    public IOneWayBindableValue<Maybe<int>> ScrollRequest { get; }

    /// <inheritdoc />
    public IOneWayToSourceBindableValue<Maybe<int>> ScrollHandled { get; }

    /// <inheritdoc />
    public void Dispose() => this.owned?.Dispose();
}
