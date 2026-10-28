using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.VisualTree;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     A <see cref="VirtualizingItemsControl" /> in a window, with the source of a
///     model. Each row and each placeholder is 20 high, and
///     the window shows ten of them.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of IDisposable does not say what this closes.
internal sealed class ShownVirtual : IDisposable
{
    private const string PlaceholderText = "placeholder";

    private readonly Window window;

    /// <summary>
    ///     Shows the control and runs its first layout.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="buffer">The buffer of the control.</param>
    public ShownVirtual(IVirtualItemsSource model, int buffer)
    {
        this.List =
            new VirtualizingItemsControl
            {
                Buffer = buffer,
                ItemTemplate =
                    new FuncDataTemplate<VirtualRow>(
                        build: static (row, _) => new TextBlock { Text = row.Text, Height = 20 },
                        supportsRecycling: true),
                PlaceholderTemplate =
                    new FuncDataTemplate<object>(
                        build: static (_, _) => new TextBlock { Text = PlaceholderText, Height = 20 },
                        supportsRecycling: true),
                Source = model
            };

        this.window = new Window { Content = this.List, Width = 300, Height = 200 };
        this.window.Show();
        this.Settle();
    }

    /// <summary>
    ///     Gets the control.
    /// </summary>
    public VirtualizingItemsControl List { get; }

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
}
