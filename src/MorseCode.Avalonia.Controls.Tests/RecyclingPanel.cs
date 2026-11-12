using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Generators;
using Avalonia.Input;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     A panel that makes containers for the first ten positions only, and that is not a
///     <see cref="VirtualizingStackPanel" />. At a change of an item, it does what that panel does. It
///     recycles the container at once, and makes it again only at the next layout. Thus, a test can
///     check the control with a panel whose range the control cannot read.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of VirtualizingPanel does not say what this panel recycles.
internal sealed class RecyclingPanel : VirtualizingPanel
{
    private const int Shown = 10;
    private const double RowHeight = 20;

    // The container of each position that has one.
    private readonly Dictionary<int, Control> containers = [];

    // Makes the container of a position from the generator, the item, the position, and the recycle key.
    private readonly Func<ItemContainerGenerator, object?, int, object?, Control> createContainer;

    /// <summary>
    ///     Makes a panel that gets each container from the items control.
    /// </summary>
    public RecyclingPanel()
        : this(
            createContainer: static (generator, item, index, recycleKey) =>
                generator.CreateContainer(item: item, index: index, recycleKey: recycleKey))
    {
    }

    /// <summary>
    ///     Makes a panel that gets each container from a function of the test. Thus, a test can
    ///     give the control a container of a type in which the control cannot change the item.
    /// </summary>
    /// <param name="makeContainer">Makes each container.</param>
    public RecyclingPanel(Func<Control> makeContainer)
        : this(createContainer: (_, _, _, _) => makeContainer())
    {
    }

    private RecyclingPanel(Func<ItemContainerGenerator, object?, int, object?, Control> createContainer) =>
        this.createContainer = createContainer;

    /// <summary>
    ///     Gets the number of containers that the panel recycled.
    /// </summary>
    public int Recycled { get; private set; }

    private ItemContainerGenerator Generator =>
        this.ItemContainerGenerator
        ?? throw new InvalidOperationException(message: "The panel is not in an items control.");

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        int count = Math.Min(val1: this.Items.Count, val2: Shown);
        double width = 0;

        this.RecycleFrom(first: count);

        for (int index = 0; index < count; index++)
        {
            if (!this.containers.TryGetValue(key: index, value: out Control? container))
            {
                container = this.Realize(index: index);
            }

            container.Measure(availableSize: availableSize.WithHeight(height: RowHeight));
            width = Math.Max(val1: width, val2: container.DesiredSize.Width);
        }

        return new Size(width: width, height: count * RowHeight);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (KeyValuePair<int, Control> pair in this.containers)
        {
            pair.Value.Arrange(
                rect: new Rect(x: 0, y: pair.Key * RowHeight, width: finalSize.Width, height: RowHeight));
        }

        return finalSize;
    }

    /// <inheritdoc />
    protected override void OnItemsChanged(IReadOnlyList<object?> items, NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(items: items, e: e);

        this.InvalidateMeasure();

        if (e.Action == NotifyCollectionChangedAction.Replace)
        {
            int count = e.OldItems?.Count ?? 0;

            for (int offset = 0; offset < count; offset++)
            {
                this.Recycle(index: e.OldStartingIndex + offset);
            }

            return;
        }

        // Each other change can move the items that come after it.
        this.RecycleFrom(
            first: e.Action switch
            {
                NotifyCollectionChangedAction.Add => e.NewStartingIndex,
                NotifyCollectionChangedAction.Remove => e.OldStartingIndex,
                _ => 0
            });
    }

    /// <inheritdoc />
    protected override Control? ScrollIntoView(int index) => this.ContainerFromIndex(index: index);

    /// <inheritdoc />
    protected override Control? ContainerFromIndex(int index) => this.containers.GetValueOrDefault(key: index);

    /// <inheritdoc />
    protected override int IndexFromContainer(Control container) =>
        this.containers.Where(predicate: pair => ReferenceEquals(objA: pair.Value, objB: container))
            .Select(selector: static pair => pair.Key)
            .DefaultIfEmpty(defaultValue: -1)
            .First();

    /// <inheritdoc />
    protected override IEnumerable<Control> GetRealizedContainers() => this.containers.Values;

    /// <inheritdoc />
    protected override IInputElement? GetControl(NavigationDirection direction, IInputElement? from, bool wrap) =>
        null;

    private Control Realize(int index)
    {
        object? item = this.Items[index: index];

        // The items of the tests are not controls, thus each item needs a container.
        _ = this.Generator.NeedsContainer(item: item, index: index, recycleKey: out object? recycleKey);

        Control container = this.createContainer(arg1: this.Generator, arg2: item, arg3: index, arg4: recycleKey);

        this.Generator.PrepareItemContainer(container: container, item: item, index: index);
        this.AddInternalChild(control: container);
        this.Generator.ItemContainerPrepared(container: container, item: item, index: index);
        this.containers.Add(key: index, value: container);

        return container;
    }

    private void Recycle(int index)
    {
        if (!this.containers.Remove(key: index, value: out Control? container))
        {
            return;
        }

        this.Generator.ClearItemContainer(container: container);
        this.RemoveInternalChild(child: container);
        this.Recycled++;
    }

    private void RecycleFrom(int first)
    {
        foreach (int index in this.containers.Keys.Where(predicate: index => index >= first).ToList())
        {
            this.Recycle(index: index);
        }
    }
}
