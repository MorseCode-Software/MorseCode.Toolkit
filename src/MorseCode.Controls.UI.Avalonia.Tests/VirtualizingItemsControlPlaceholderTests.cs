using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using MorseCode.Controls.ViewModels;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Controls.UI.Avalonia.Tests;

/// <summary>
///     Checks what a placeholder of a shown <see cref="VirtualizingItemsControl" /> shows, with no
///     placeholder template and when the template changes.
/// </summary>
public sealed class VirtualizingItemsControlPlaceholderTests
{
    private const int Total = 30;
    private const int RowHeight = 20;

    // With no template, a placeholder is an empty line of text. It shows no text, and it has a height, thus
    // the panel does not make a container for each position.
    [Test]
    public Task APlaceholderWithNoTemplateShowsNoTextAndHasAHeight() =>
        TestApplication.Run(
            test: static async () =>
            {
                GivenRowsModel model = new();
                VirtualizingItemsControl list = MakeList(model: model);
                Window window = Show(list: list);

                model.Give(value: new VirtualRows(TotalCount: Total, Offset: 0, Items: []));
                Settle(window: window);

                List<Control?> views = Views(list: list);

                await Assert.That(views).IsNotEmpty();
                await Assert.That(views.Count).IsLessThan(maximum: Total);
                await Assert.That(views.All(predicate: static view => view is TextBlock { Text: null or "" })).IsTrue();
                await Assert.That(views.All(predicate: static view => view is { Bounds.Height: > 0 })).IsTrue();

                window.Close();
            });

    // A template that the app sets or changes at run time reaches each placeholder on the screen, and the
    // rows keep the row template. No template brings the default back.
    [Test]
    public Task APlaceholderTemplateThatChangesReachesEachPlaceholderOnTheScreen() =>
        TestApplication.Run(
            test: static async () =>
            {
                GivenRowsModel model = new();
                VirtualizingItemsControl list = MakeList(model: model);
                Window window = Show(list: list);

                // Three rows, and placeholders after them.
                model.Give(
                    value: new VirtualRows(
                        TotalCount: Total,
                        Offset: 0,
                        Items:
                        [
                            new VirtualRow(Position: 0),
                            new VirtualRow(Position: 1),
                            new VirtualRow(Position: 2)
                        ]));

                Settle(window: window);

                list.PlaceholderTemplate = Template(text: "one");
                Settle(window: window);
                List<string> first = Texts(list: list);

                list.PlaceholderTemplate = Template(text: "two");
                Settle(window: window);
                List<string> second = Texts(list: list);

                list.PlaceholderTemplate = null;
                Settle(window: window);
                List<string> none = Texts(list: list);

                await Assert.That(first.Take(count: 3))
                    .IsEquivalentTo(expected: ["row", "row", "row"], ordering: CollectionOrdering.Matching);

                await Assert.That(first.Skip(count: 3).All(predicate: static text => text == "one")).IsTrue();
                await Assert.That(second.Take(count: 3).All(predicate: static text => text == "row")).IsTrue();
                await Assert.That(second.Skip(count: 3).All(predicate: static text => text == "two")).IsTrue();
                await Assert.That(none.Skip(count: 3).All(predicate: static text => text == string.Empty)).IsTrue();
                await Assert.That(first.Count).IsGreaterThan(minimum: 3);

                window.Close();
            });

    private static VirtualizingItemsControl MakeList(IVirtualItemsSource model) =>
        new()
        {
            ItemTemplate = new FuncDataTemplate<object>(
                build: static (_, _) => new TextBlock { Height = RowHeight, Text = "row" },
                supportsRecycling: true),
            Source = model
        };

    private static Window Show(VirtualizingItemsControl list)
    {
        Window window = new() { Content = list, Width = 300, Height = 200 };
        window.Show();

        return window;
    }

    private static FuncDataTemplate<object> Template(string text) =>
        new(build: (_, _) => new TextBlock { Height = RowHeight, Text = text }, supportsRecycling: true);

    // The view of each realized container, in the order of the positions.
    private static List<Control?> Views(ItemsControl list) =>
    [
        .. list.GetRealizedContainers()
            .OrderBy(keySelector: list.IndexFromContainer)
            .Select(selector: static container => (container as ContentPresenter)?.Child)
    ];

    // The text of each realized container, in the order of the positions. A missing text is empty.
    private static List<string> Texts(ItemsControl list) =>
    [
        .. Views(list: list).Select(selector: static view => (view as TextBlock)?.Text ?? string.Empty)
    ];

    // Runs the work of the dispatcher and the layout until nothing is left, as ShownVirtual does.
    private static void Settle(Layoutable window)
    {
        for (int pass = 0; pass < 8; pass++)
        {
            window.Dispatcher.RunJobs();
            window.UpdateLayout();
        }
    }
}
