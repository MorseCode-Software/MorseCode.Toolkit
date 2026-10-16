using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Threading;
using JetBrains.Annotations;

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
///         positions on each side. A position that <see cref="Window" /> does not
///         cover shows <see cref="PlaceholderTemplate" />.
///     </para>
///     <para>
///         In the view model, send <see cref="VisibleRange" /> to the offset and the
///         limit of <c>Slice</c>, and build the rows with <c>Map</c> after it. The
///         <c>Map</c> builds a row for a key only when the key is inside the window.
///         Make a <see cref="VirtualWindow" /> from the offset of the slice and the
///         items of the map, and bind it to <see cref="Window" />.
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
    ///     Defines the <see cref="Window" /> property.
    /// </summary>
    public static readonly StyledProperty<VirtualWindow> WindowProperty =
        AvaloniaProperty.Register<VirtualizingItemsControl, VirtualWindow>(
            name: nameof(Window),
            defaultValue: VirtualWindow.Empty);

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
    ///     Defines the <see cref="VisibleRange" /> property.
    /// </summary>
    public static readonly DirectProperty<VirtualizingItemsControl, ItemRange> VisibleRangeProperty =
        AvaloniaProperty.RegisterDirect<VirtualizingItemsControl, ItemRange>(
            name: nameof(VisibleRange),
            getter: static control => control.visibleRange,
            defaultBindingMode: BindingMode.OneWayToSource);

    private static readonly FuncTemplate<Panel?> DefaultPanel =
        new(func: static () => new VirtualizingStackPanel());

    private readonly WindowedItems items = new();

    // True from a request to report until the dispatcher runs the report.
    private bool isReportPending;

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
    public VirtualWindow Window
    {
        get => this.GetValue(property: WindowProperty);
        set => this.SetValue(property: WindowProperty, value: value);
    }

    /// <summary>
    ///     Gets or sets the template for a position that <see cref="Window" /> does
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
                message: "VirtualizingItemsControl sets ItemsSource itself. Bind TotalCount and Window instead.");
        }

        if (change.Property == TotalCountProperty || change.Property == WindowProperty)
        {
            this.items.Update(newCount: Math.Max(val1: 0, val2: this.TotalCount), newWindow: this.Window);
        }
        else if (change.Property == BufferProperty)
        {
            this.RequestReport();
        }
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

        // A change of the items makes the panel clear its containers and make them again at the next
        // layout. A count before that layout sees none of them, and reports an empty range. Then the
        // view model answers with no rows, and the cycle repeats without end.
        this.UpdateLayout();

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
            // No container, with positions to show, means that the control is not laid out. Thus, the
            // last range stays. The view model then does not drop its rows.
            return this.items.Count == 0 ? ItemRange.Empty : this.visibleRange;
        }

        int buffer = Math.Max(val1: 0, val2: this.Buffer);
        int start = Math.Max(val1: 0, val2: realized.Min() - buffer);
        int end = Math.Min(val1: this.items.Count, val2: realized.Max() + 1 + buffer);

        return new ItemRange(First: start, Count: Math.Max(val1: 0, val2: end - start));
    }
}
