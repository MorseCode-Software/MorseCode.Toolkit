using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.VisualTree;
using MorseCode.Controls.ViewModels;
using SodaFlow.Functional;

namespace MorseCode.Controls.UI.Avalonia.Tests;

/// <summary>
///     A <see cref="VirtualizingItemsControl" /> in a window, with the source of a
///     model. The window is 200 high. Unless a test gives other heights, each row and
///     each placeholder is 20 high, and the window shows ten of them.
/// </summary>
/// <remarks>
///     The row template is as a XAML template with no data type is. It accepts each
///     item, it uses the view that it built before, and the view reads its row from
///     its data context.
/// </remarks>
// ReSharper disable once InheritdocConsiderUsage - The summary of IDisposable does not say what this closes.
internal sealed class ShownVirtual : IDisposable
{
    private const string PlaceholderText = "placeholder";
    private const string NoRowText = "no row";
    private const int DefaultHeight = 20;

    private readonly Window window;

    /// <summary>
    ///     Shows the control with its default panel, and runs its first layout.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="buffer">
    ///     The number of positions that the control adds on each side of the positions that show.
    /// </param>
    public ShownVirtual(IVirtualItemsSource model, int buffer)
        : this(
            model: model,
            buffer: Maybe<int>.Some(value: buffer),
            panel: Maybe<Func<Panel>>.None,
            rowHeight: DefaultHeight,
            placeholderHeight: DefaultHeight)
    {
    }

    /// <summary>
    ///     Shows the control with a panel of the test, and runs its first layout.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="buffer">
    ///     The number of positions that the control adds on each side of the positions that show.
    /// </param>
    /// <param name="panel">Makes the panel of the control.</param>
    public ShownVirtual(IVirtualItemsSource model, int buffer, Func<Panel> panel)
        : this(
            model: model,
            buffer: Maybe<int>.Some(value: buffer),
            panel: Maybe<Func<Panel>>.Some(value: panel),
            rowHeight: DefaultHeight,
            placeholderHeight: DefaultHeight)
    {
    }

    /// <summary>
    ///     Shows the control with its default panel and with rows and placeholders of the given
    ///     heights, and runs its first layout.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="buffer">
    ///     The number of positions that the control adds on each side of the positions that show.
    /// </param>
    /// <param name="rowHeight">The height of each row.</param>
    /// <param name="placeholderHeight">The height of each placeholder.</param>
    public ShownVirtual(IVirtualItemsSource model, int buffer, int rowHeight, int placeholderHeight)
        : this(
            model: model,
            buffer: Maybe<int>.Some(value: buffer),
            panel: Maybe<Func<Panel>>.None,
            rowHeight: rowHeight,
            placeholderHeight: placeholderHeight)
    {
    }

    private ShownVirtual(
        IVirtualItemsSource model,
        Maybe<int> buffer,
        Maybe<Func<Panel>> panel,
        int rowHeight,
        int placeholderHeight)
    {
        // An app cannot set the number of positions on each side. A test can, with the constructor that
        // an app cannot call.
        this.List =
            buffer.Match(
                onSome: static positions => new VirtualizingItemsControl(bufferFor: _ => positions),
                onNone: static () => new VirtualizingItemsControl());

        this.List.ItemTemplate =
            new FuncDataTemplate<object>(
                build: (_, _) => this.BuildRowView(height: rowHeight),
                supportsRecycling: true);

        this.List.PlaceholderTemplate =
            new FuncDataTemplate<object>(
                build: (_, _) => new TextBlock { Text = PlaceholderText, Height = placeholderHeight },
                supportsRecycling: true);

        this.List.Source = model;

        panel.MatchSome(onSome: make => this.List.ItemsPanel = new FuncTemplate<Panel?>(func: make));

        this.window = new Window { Content = this.List, Width = 300, Height = 200 };
        this.window.Show();
        this.Settle();
    }

    /// <summary>
    ///     Gets the control.
    /// </summary>
    public VirtualizingItemsControl List { get; }

    /// <summary>
    ///     Shows the control as an app makes it, and runs its first layout. The control sets the number
    ///     of positions on each side.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="rowHeight">The height of each row and of each placeholder.</param>
    /// <returns>The shown control.</returns>
    public static ShownVirtual AsAnAppMakesIt(IVirtualItemsSource model, int rowHeight) =>
        new(
            model: model,
            buffer: Maybe<int>.None,
            panel: Maybe<Func<Panel>>.None,
            rowHeight: rowHeight,
            placeholderHeight: rowHeight);

    /// <summary>
    ///     Gets the number of row views that the row template built.
    /// </summary>
    public int RowViewsBuilt { get; private set; }

    /// <summary>
    ///     Gets the number of times that a row view got a placeholder as its data context. A row view
    ///     with bindings cannot show a placeholder, thus each one is a defect of the control.
    /// </summary>
    public int PlaceholdersInRowViews { get; private set; }

    /// <summary>
    ///     Gets the texts that showed at each layout after the call of <see cref="WatchLayouts" />.
    /// </summary>
    public List<string> TextsAtLayouts { get; } = [];

    /// <summary>
    ///     Gets the scroll viewer of the control.
    /// </summary>
    public ScrollViewer Scroller => this.List.GetVisualDescendants().OfType<ScrollViewer>().First();

    /// <summary>
    ///     Takes the control out of the window. Thus, the control leaves the visual tree, as a view
    ///     does when the user closes a tab.
    /// </summary>
    public void Remove() => this.window.Content = null;

    /// <summary>
    ///     Moves the control into a box of the given height that clips it, and runs the layout. The
    ///     control keeps the height of the window. Thus, a control above the list hides the rows outside
    ///     the box, and the scroll viewer of the list does not.
    /// </summary>
    /// <param name="height">The height of the box.</param>
    /// <param name="hiddenAtTheTop">The height of the part of the list that is above the box.</param>
    public void ClipTo(int height, int hiddenAtTheTop)
    {
        this.window.Content = null;

        // By default, a control that is higher than its place goes to the center of the place.
        this.List.Height = this.window.Height;
        this.List.VerticalAlignment = VerticalAlignment.Top;
        this.List.Margin = new Thickness(left: 0, top: -hiddenAtTheTop, right: 0, bottom: 0);

        this.window.Content =
            new Border
            {
                Height = height,
                VerticalAlignment = VerticalAlignment.Top,
                ClipToBounds = true,
                Child = this.List
            };

        this.Settle();
    }

    /// <summary>
    ///     Puts the control back into the window, and runs the layout.
    /// </summary>
    public void Return()
    {
        this.window.Content = this.List;
        this.Settle();
    }

    /// <summary>
    ///     Runs the work of the dispatcher and the layout until nothing is left. Thus,
    ///     a report, an answer, and the layout that follows it all happen.
    /// </summary>
    public void Settle()
    {
        for (int pass = 0; pass < 8; pass++)
        {
            this.window.Dispatcher.RunJobs();
            this.window.UpdateLayout();
        }
    }

    /// <summary>
    ///     Starts to record the texts that show at each layout, in <see cref="TextsAtLayouts" />. Thus,
    ///     a test can find a text that shows only between two changes.
    /// </summary>
    public void WatchLayouts() =>
        this.List.LayoutUpdated +=
            (_, _) => this.TextsAtLayouts.AddRange(collection: this.Shown().Select(selector: static pair => pair.Text));

    /// <summary>
    ///     Gets the text that each realized container shows, by position. A position
    ///     with a placeholder shows as "placeholder".
    /// </summary>
    /// <returns>The texts, in the order of the positions.</returns>
    public IReadOnlyList<(int Position, string Text)> Shown() =>
    [
        .. this.List.GetRealizedContainers()
            .Select(
                selector: container =>
                (
                    Position: this.List.IndexFromContainer(container: container),
                    Text: ((container as ContentPresenter)?.Child as TextBlock)?.Text ?? "none"))
            .OrderBy(keySelector: static pair => pair.Position)
    ];

    /// <inheritdoc />
    public void Dispose() => this.window.Close();

    private TextBlock BuildRowView(int height)
    {
        this.RowViewsBuilt++;

        TextBlock view = new() { Height = height };
        view.DataContextChanged += this.ShowRow;

        return view;
    }

    // Does the work of a binding to the text of the row.
    private void ShowRow(object? sender, EventArgs e)
    {
        if (sender is not TextBlock view)
        {
            return;
        }

        if (view.DataContext is RowPlaceholder)
        {
            this.PlaceholdersInRowViews++;
        }

        view.Text = (view.DataContext as VirtualRow)?.Text ?? NoRowText;
    }
}
