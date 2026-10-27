using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Threading;
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
///         The control has a position for each of <see cref="TotalCount" /> items.
///         Thus, the scroll bar, the size of the scroll area, and
///         <see cref="ItemsControl.ScrollIntoView(int)" /> work as on any items control. Avalonia
///         makes containers for the positions that show, and the control reports
///         them in <see cref="VisibleRange" />, with <see cref="Buffer" /> more
///         positions on each side. A position that <see cref="Rows" /> does not
///         cover shows <see cref="PlaceholderTemplate" />.
///     </para>
///     <para>
///         In the view model, send <see cref="VisibleRange" /> to the offset and the
///         limit of <c>Slice</c>, and build the rows with <c>Map</c> after it. The
///         <c>Map</c> builds a row for a key only when the key is inside the slice.
///         Make a <see cref="VirtualRows" /> from the offset of the slice and the
///         items of the map, and bind it to <see cref="Rows" />.
///     </para>
///     <para>
///         To scroll, the view model sets <see cref="ScrollRequest" /> to the position that it wants
///         to show. The control scrolls there when it can, and then sets the property back to
///         none. Thus, a request made before the control is bound or laid out is not lost.
///     </para>
///     <para>
///         Do not set <see cref="ItemsControl.ItemsSource" />. The control sets it. A
///         placeholder must be about as high as a row. Otherwise, Avalonia shows more
///         placeholders than the screen needs, and the view model builds rows that the
///         user cannot see.
///     </para>
/// </remarks>
[PublicAPI]
// ReSharper disable once InheritdocConsiderUsage - The summary of ItemsControl does not say what this control reports.
public class VirtualizingItemsControl : ItemsControl
{
    /// <summary>
    ///     Defines the <see cref="TotalCount" /> property.
    /// </summary>
    public static readonly StyledProperty<int> TotalCountProperty =
        AvaloniaProperty.Register<VirtualizingItemsControl, int>(name: nameof(TotalCount), defaultValue: 0);

    /// <summary>
    ///     Defines the <see cref="Rows" /> property.
    /// </summary>
    public static readonly StyledProperty<VirtualRows> RowsProperty =
        AvaloniaProperty.Register<VirtualizingItemsControl, VirtualRows>(
            name: nameof(Rows),
            defaultValue: VirtualRows.Empty);

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

    /// <summary>
    ///     Defines the <see cref="ScrollRequest" /> property.
    /// </summary>
    public static readonly StyledProperty<Maybe<int>> ScrollRequestProperty =
        AvaloniaProperty.Register<VirtualizingItemsControl, Maybe<int>>(
            name: nameof(ScrollRequest),
            defaultValue: Maybe<int>.None,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    ///     Defines the <see cref="VisibleRange" /> property.
    /// </summary>
    public static readonly DirectProperty<VirtualizingItemsControl, ItemRange> VisibleRangeProperty =
        AvaloniaProperty.RegisterDirect<VirtualizingItemsControl, ItemRange>(
            name: nameof(VisibleRange),
            getter: static control => control.visibleRange,
            defaultBindingMode: BindingMode.OneWayToSource);

    private static readonly FuncTemplate<Panel?> DefaultPanel =
        new(func: static () => new VirtualizingStackPanel());

    private readonly VirtualItemList items = new();

    // True from a request to report until the dispatcher runs the report.
    private bool isReportPending;

    // True from a request to scroll until the dispatcher runs it.
    private bool isScrollPending;

    private ItemRange visibleRange = ItemRange.Empty;

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
    ///     Gets or sets the number of positions in the whole list.
    /// </summary>
    public int TotalCount
    {
        get => this.GetValue(property: TotalCountProperty);
        set => this.SetValue(property: TotalCountProperty, value: value);
    }

    /// <summary>
    ///     Gets or sets the rows for some of the positions.
    /// </summary>
    public VirtualRows Rows
    {
        get => this.GetValue(property: RowsProperty);
        set => this.SetValue(property: RowsProperty, value: value);
    }

    /// <summary>
    ///     Gets or sets the template for a position that <see cref="Rows" /> does
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
    ///     Gets or sets the number of positions that <see cref="VisibleRange" />
    ///     adds on each side of the positions that show.
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

    /// <summary>
    ///     Gets or sets the position that the view model wants to show. The control scrolls to it
    ///     and then sets the property to none.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Bind this property two-way. The view model sets a position. The control scrolls
    ///         there, and writes none back to say that it did. The view model keeps the last request
    ///         until then, thus a request made before the control is bound is still there when the
    ///         control binds.
    ///     </para>
    ///     <para>
    ///         The control waits for its Loaded event and for at least that many positions. A request
    ///         for a position beyond <see cref="TotalCount" /> stays until the count grows or the
    ///         view model sets another request. A negative position can never apply, so the control
    ///         sets it back to none at once. A new request replaces one that waits.
    ///     </para>
    ///     <para>
    ///         The position is a number and not an item, because the control has no row for a
    ///         position that <see cref="Rows" /> does not cover. The scroll brings the position into
    ///         view, as <see cref="ItemsControl.ScrollIntoView(int)" /> does.
    ///     </para>
    /// </remarks>
    public Maybe<int> ScrollRequest
    {
        get => this.GetValue(property: ScrollRequestProperty);
        set => this.SetValue(property: ScrollRequestProperty, value: value);
    }

    /// <summary>
    ///     Gets the positions that show, with <see cref="Buffer" /> more on each side.
    /// </summary>
    /// <remarks>
    ///     Bind this property one-way to source. The control sets it after the
    ///     containers change, in a later item of the dispatcher.
    /// </remarks>
    public ItemRange VisibleRange => this.visibleRange;

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(ListBox);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change: change);

        if (change.Property == ItemsSourceProperty && !ReferenceEquals(objA: change.NewValue, objB: this.items))
        {
            throw new InvalidOperationException(
                message: "VirtualizingItemsControl sets ItemsSource itself. Bind TotalCount and Rows instead.");
        }

        if (change.Property == TotalCountProperty || change.Property == RowsProperty)
        {
            this.items.Update(newCount: Math.Max(val1: 0, val2: this.TotalCount), newRows: this.Rows);
        }
        else if (change.Property == BufferProperty)
        {
            this.RequestReport();
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

    private void RequestScroll()
    {
        if (this.isScrollPending)
        {
            return;
        }

        this.isScrollPending = true;
        this.Dispatcher.Post(action: this.ApplyScroll, priority: DispatcherPriority.Normal);
    }

    // The request is a value of the view model, thus it stays until the control can use it. Setting it
    // back to none is how the control says that it scrolled.
    private void ApplyScroll()
    {
        this.isScrollPending = false;

        this.ScrollRequest.MatchSome(
            onSome: position =>
            {
                if (position < 0)
                {
                    this.ScrollRequest = Maybe<int>.None;
                }
                else if (this.IsLoaded && this.ItemsPanelRoot is not null && position < this.items.Count)
                {
                    this.ScrollIntoView(index: position);
                    this.ScrollRequest = Maybe<int>.None;
                }
            });
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

        this.SetAndRaise(property: VisibleRangeProperty, field: ref this.visibleRange, value: range);
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
            return this.items.Count == 0 ? ItemRange.Empty : this.visibleRange;
        }

        int buffer = Math.Max(val1: 0, val2: this.Buffer);
        int start = Math.Max(val1: 0, val2: realized.Min() - buffer);
        int end = Math.Min(val1: this.items.Count, val2: realized.Max() + 1 + buffer);

        return new ItemRange(First: start, Count: Math.Max(val1: 0, val2: end - start));
    }
}
