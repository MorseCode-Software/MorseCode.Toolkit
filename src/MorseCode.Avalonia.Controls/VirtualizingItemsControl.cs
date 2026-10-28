using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using JetBrains.Annotations;
using SodaFlow.Functional;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     An items control for a long list whose rows are heavy to build. It tells the
///     view model which positions Avalonia shows, and the view model gives back the
///     rows for only those positions.
/// </summary>
/// <remarks>
///     <para>
///         Set <see cref="Source" /> to the one object that the view model gives. The control has a
///         position for each item that the source counts. Thus, the scroll bar, the size of the
///         scroll area, and <see cref="ItemsControl.ScrollIntoView(int)" /> work as on any items
///         control. Avalonia makes containers for the positions that show, and the control reports
///         them to the source, with <see cref="Buffer" /> more positions on each side. A position
///         that the rows of the source do not cover shows <see cref="PlaceholderTemplate" />.
///     </para>
///     <para>
///         In the view model, make the source with <c>CreateVirtualItemsSource</c>. It sends the
///         reported range to the offset and the limit of <c>Slice</c>, and builds the rows with
///         <c>Map</c> after it. The <c>Map</c> builds a row for a key only when the key is inside
///         the slice.
///     </para>
///     <para>
///         To scroll, the view model makes a request in the source. The control scrolls there when
///         it can, and then reports the position back. Thus, a request made before the control is
///         bound or laid out is not lost.
///     </para>
///     <para>
///         Do not set <see cref="ItemsControl.ItemsSource" />. The control sets it. A
///         placeholder must be about as high as a row. Otherwise, Avalonia shows more
///         placeholders than the screen needs, and the view model builds rows that the
///         user cannot see.
///     </para>
///     <para>
///         The control uses the style of <see cref="ListBox" />. Thus, a style in the application
///         that selects <c>ListBox</c> also applies to this control.
///     </para>
/// </remarks>
[PublicAPI]
// ReSharper disable once InheritdocConsiderUsage - The summary of ItemsControl does not say what this control reports.
public class VirtualizingItemsControl : ItemsControl
{
    /// <summary>
    ///     Defines the <see cref="Source" /> property.
    /// </summary>
    public static readonly StyledProperty<IVirtualItemsSource?> SourceProperty =
        AvaloniaProperty.Register<VirtualizingItemsControl, IVirtualItemsSource?>(name: nameof(Source));

    // The source feeds these three properties, and the tests of the control read them.
    internal static readonly StyledProperty<int> TotalCountProperty =
        AvaloniaProperty.Register<VirtualizingItemsControl, int>(name: nameof(TotalCount), defaultValue: 0);

    internal static readonly StyledProperty<VirtualRows> RowsProperty =
        AvaloniaProperty.Register<VirtualizingItemsControl, VirtualRows>(
            name: nameof(Rows),
            defaultValue: VirtualRows.Empty);

    internal static readonly StyledProperty<Maybe<int>> ScrollRequestProperty =
        AvaloniaProperty.Register<VirtualizingItemsControl, Maybe<int>>(
            name: nameof(ScrollRequest),
            defaultValue: Maybe<int>.None);

    /// <summary>
    ///     Defines the <see cref="PlaceholderTemplate" /> property.
    /// </summary>
    public static readonly StyledProperty<IDataTemplate?> PlaceholderTemplateProperty =
        AvaloniaProperty.Register<VirtualizingItemsControl, IDataTemplate?>(name: nameof(PlaceholderTemplate));

    /// <summary>
    ///     Defines the <see cref="Buffer" /> property.
    /// </summary>
    public static readonly StyledProperty<int> BufferProperty =
        AvaloniaProperty.Register<VirtualizingItemsControl, int>(name: nameof(Buffer), defaultValue: 10);

    private static readonly FuncTemplate<Panel?> DefaultPanel =
        new(func: static () => new VirtualizingStackPanel());

    private readonly VirtualItemList items = new();

    // True from a request to report until the dispatcher runs the report.
    private bool isReportPending;

    // True from a request to scroll until the dispatcher runs it.
    private bool isScrollPending;

    // True from the moment that the control handled the request that it holds until the request changes.
    // The view model clears the request after it receives the report, and that can take a moment.
    private bool isRequestHandled;

    // The source that the control reads and reports to.
    private IVirtualItemsSource? source;

    // The handlers on the values of the source. They are set only while the control follows the source,
    // which is while it is in a visual tree. A handler is a reference from the source to the control, and
    // a view model can outlive its view. Thus, a control that left the tree holds none.
    private PropertyChangedEventHandler? onTotalCountChanged;
    private PropertyChangedEventHandler? onRowsChanged;
    private PropertyChangedEventHandler? onScrollRequestChanged;

    // The default panel of an items control does not virtualize. This is the default panel of the list
    // box of Avalonia.
    static VirtualizingItemsControl() =>
        ItemsPanelProperty.OverrideDefaultValue<VirtualizingItemsControl>(defaultValue: DefaultPanel);

    /// <summary>
    ///     Initializes a new instance of the <see cref="VirtualizingItemsControl" /> class.
    /// </summary>
    // ReSharper disable once InheritdocConsiderUsage - The constructor of ItemsControl does not say what this one sets.
    public VirtualizingItemsControl()
    {
        this.ItemsSource = this.items;
        this.ContainerPrepared += (_, _) => this.RequestReport();
        this.ContainerClearing += (_, _) => this.RequestReport();
    }

    /// <summary>
    ///     Gets or sets the object that the view model gives to the control: the count, the rows, the
    ///     range that shows, and the scroll request.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The control reads the count, the rows, and the scroll request from the source, and writes
    ///         the range that shows and the position that it scrolled to. Make the source with
    ///         <c>CreateVirtualItemsSource</c>. A change of this property detaches the control from the
    ///         old source and attaches it to the new one.
    ///     </para>
    ///     <para>
    ///         The control follows the source only while it is in a visual tree. A control that left the
    ///         tree keeps no reference in the source. Thus, a view model that outlives its view does not
    ///         keep the view alive. The control reads the source again when it returns to a tree.
    ///     </para>
    /// </remarks>
    public IVirtualItemsSource? Source
    {
        get => this.GetValue(property: SourceProperty);
        set => this.SetValue(property: SourceProperty, value: value);
    }

    internal int TotalCount
    {
        get => this.GetValue(property: TotalCountProperty);
        set => this.SetValue(property: TotalCountProperty, value: value);
    }

    internal VirtualRows Rows
    {
        get => this.GetValue(property: RowsProperty);
        set => this.SetValue(property: RowsProperty, value: value);
    }

    /// <summary>
    ///     Gets or sets the template for a position that the rows of the source do
    ///     not cover.
    /// </summary>
    /// <remarks>
    ///     The data context of the template is the control's own placeholder, which has
    ///     no data. Give the template the height of a row.
    /// </remarks>
    public IDataTemplate? PlaceholderTemplate
    {
        get => this.GetValue(property: PlaceholderTemplateProperty);
        set => this.SetValue(property: PlaceholderTemplateProperty, value: value);
    }

    /// <summary>
    ///     Gets or sets the number of positions that the control adds on each side of the
    ///     positions that show, when it reports the range to the source.
    /// </summary>
    /// <remarks>
    ///     A scroll that is faster than the view model can answer shows placeholders.
    ///     More positions on each side give the view model time to answer.
    /// </remarks>
    public int Buffer
    {
        get => this.GetValue(property: BufferProperty);
        set => this.SetValue(property: BufferProperty, value: value);
    }

    // The position that the view model wants to show. The control scrolls to it and then reports the
    // position to the source. It scrolls once for each value of this property. If the view model has not
    // cleared the request yet, a change of the count does not scroll again. A request beyond the count
    // stays until the count grows. A negative position can never apply, so the control reports it at once
    // and does not scroll.
    internal Maybe<int> ScrollRequest
    {
        get => this.GetValue(property: ScrollRequestProperty);
        set => this.SetValue(property: ScrollRequestProperty, value: value);
    }

    // The positions that show, with the buffer on each side. The control reports them to the source.
    internal ItemRange VisibleRange { get; private set; } = ItemRange.Empty;

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(ListBox);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change: change);

        if (change.Property == ItemsSourceProperty && !ReferenceEquals(objA: change.NewValue, objB: this.items))
        {
            throw new InvalidOperationException(
                message: "VirtualizingItemsControl sets ItemsSource itself. Set Source instead.");
        }

        if (change.Property == TotalCountProperty || change.Property == RowsProperty)
        {
            this.items.Update(newCount: Math.Max(val1: 0, val2: this.TotalCount), newRows: this.Rows);
        }
        else if (change.Property == BufferProperty)
        {
            this.RequestReport();
        }
        else if (change.Property == SourceProperty)
        {
            this.AttachSource(newSource: change.GetNewValue<IVirtualItemsSource?>());
        }

        // A new request value is a request that the control has not handled.
        if (change.Property == ScrollRequestProperty)
        {
            this.isRequestHandled = false;
        }

        // A request can wait for the count, and the count can arrive after the request.
        if (change.Property == ScrollRequestProperty || change.Property == TotalCountProperty)
        {
            this.RequestScroll();
        }
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e: e);

        // A request that came before the Loaded event can scroll now.
        this.RequestScroll();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e: e);

        // The source can change while the control is out of the tree. The range that the control
        // reported last stays, thus the view model keeps the rows for it and the return shows no
        // placeholders.
        if (this.source is not null)
        {
            this.Follow(source: this.source);
            this.ReadSource(source: this.source);
            this.RequestReport();
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        this.StopFollowing();

        base.OnDetachedFromVisualTree(e: e);
    }

    /// <inheritdoc />
    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container: container, item: item, index: index);

        // Avalonia can reuse a container for a position that gets a different kind of item. Thus, the
        // template is set in both directions.
        IDataTemplate? template = item is RowPlaceholder ? this.PlaceholderTemplate : this.ItemTemplate;

        switch (container)
        {
            case ContentPresenter presenter:
                presenter.ContentTemplate = template;

                break;
            case ContentControl control:
                control.ContentTemplate = template;

                break;
        }
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(Control container)
    {
        // The base clears the content before the template. A presenter in a tree builds its child again
        // at each of the two changes. The ContentPresenter of Avalonia 12 builds an explicit template
        // also for no content. Thus, the row template builds a row view for null, and the second change
        // removes it. The template goes first here, so that the build for no content does not occur.
        switch (container)
        {
            case ContentPresenter presenter:
                presenter.ClearValue(property: ContentPresenter.ContentTemplateProperty);

                break;
            case ContentControl control:
                control.ClearValue(property: ContentControl.ContentTemplateProperty);

                break;
        }

        base.ClearContainerForItemOverride(container: container);
    }

    private void RequestScroll()
    {
        if (this.isScrollPending)
        {
            return;
        }

        this.isScrollPending = true;
        this.Dispatcher.Post(action: this.ApplyScroll, priority: DispatcherPriority.Normal);
    }

    // The request is a value of the view model, thus it stays until the control can use it. The report in
    // ScrollHandled is how the control says that it used it.
    private void ApplyScroll()
    {
        this.isScrollPending = false;

        if (this.isRequestHandled)
        {
            return;
        }

        this.ScrollRequest.MatchSome(
            onSome: position =>
            {
                if (position < 0)
                {
                    this.Handle(position: position);
                }
                else if (this.IsLoaded && this.ItemsPanelRoot is not null && position < this.items.Count)
                {
                    this.ScrollIntoView(index: position);
                    this.Handle(position: position);
                }
            });
    }

    private void Handle(int position)
    {
        // The flag comes first. The report can make the view model clear or replace the request at once,
        // and that change of the request resets the flag.
        this.isRequestHandled = true;

        // The second write makes a report of the same position a change again, for a source that drops
        // an equal write.
        if (this.source is not null)
        {
            this.source.ScrollHandled.Value = Maybe<int>.Some(value: position);
            this.source.ScrollHandled.Value = Maybe<int>.None;
        }
    }

    private void AttachSource(IVirtualItemsSource? newSource)
    {
        this.StopFollowing();

        this.source = newSource;

        if (newSource is null)
        {
            // A control with no source shows nothing and has no request.
            this.ScrollRequest = Maybe<int>.None;
            this.TotalCount = 0;
            this.Rows = VirtualRows.Empty;

            return;
        }

        if (this.IsAttachedToVisualTree())
        {
            this.Follow(source: newSource);
        }

        this.ReadSource(source: newSource);

        // The new source has no range yet.
        this.VisibleRange = ItemRange.Empty;
        this.RequestReport();
    }

    private void Follow(IVirtualItemsSource source)
    {
        this.onTotalCountChanged = (_, _) => this.TotalCount = source.TotalCount.Value;
        this.onRowsChanged = (_, _) => this.Rows = source.Rows.Value;
        this.onScrollRequestChanged = (_, _) => this.ScrollRequest = source.ScrollRequest.Value;

        source.TotalCount.PropertyChanged += this.onTotalCountChanged;
        source.Rows.PropertyChanged += this.onRowsChanged;
        source.ScrollRequest.PropertyChanged += this.onScrollRequestChanged;
    }

    private void StopFollowing()
    {
        if (this.source is null)
        {
            return;
        }

        if (this.onTotalCountChanged is not null)
        {
            this.source.TotalCount.PropertyChanged -= this.onTotalCountChanged;
        }

        if (this.onRowsChanged is not null)
        {
            this.source.Rows.PropertyChanged -= this.onRowsChanged;
        }

        if (this.onScrollRequestChanged is not null)
        {
            this.source.ScrollRequest.PropertyChanged -= this.onScrollRequestChanged;
        }

        this.onTotalCountChanged = null;
        this.onRowsChanged = null;
        this.onScrollRequestChanged = null;
    }

    // The source can hold values already. The rows go after the count, as a change of both does.
    private void ReadSource(IVirtualItemsSource source)
    {
        this.TotalCount = source.TotalCount.Value;
        this.Rows = source.Rows.Value;
        this.ScrollRequest = source.ScrollRequest.Value;
    }

    private void RequestReport()
    {
        if (this.isReportPending)
        {
            return;
        }

        this.isReportPending = true;
        this.Dispatcher.Post(action: this.Report, priority: DispatcherPriority.Normal);
    }

    private void Report()
    {
        this.isReportPending = false;

        ItemRange range = this.Measure();

        this.VisibleRange = range;

        // The source drops a write that equals the last one.
        this.source?.VisibleRange.Value = range;
    }

    // Finds the positions of the containers that Avalonia shows now, and adds the buffer on each side.
    private ItemRange Measure()
    {
        List<int> realized =
        [
            .. this.GetRealizedContainers()
                .Select(selector: this.IndexFromContainer)
                .Where(predicate: static index => index >= 0)
        ];

        if (realized.Count == 0)
        {
            // No container, with positions to show, means that the control is not laid out. This is also
            // the case after a change of the items. The panel clears its containers then, and makes them
            // again at the next layout. Thus, the last range stays. If it did not, the view model would
            // drop its rows, the items would change again, and the cycle would repeat without end.
            return this.items.Count == 0 ? ItemRange.Empty : this.VisibleRange;
        }

        int buffer = Math.Max(val1: 0, val2: this.Buffer);
        int start = Math.Max(val1: 0, val2: realized.Min() - buffer);
        int end = Math.Min(val1: this.items.Count, val2: realized.Max() + 1 + buffer);

        return new ItemRange(First: start, Count: Math.Max(val1: 0, val2: end - start));
    }
}
