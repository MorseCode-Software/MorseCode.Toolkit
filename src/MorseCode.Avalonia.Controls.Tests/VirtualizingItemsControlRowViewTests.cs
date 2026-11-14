using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     Checks that a shown <see cref="VirtualizingItemsControl" /> keeps the view of a row that leaves
///     the screen, and gives it to the next row that comes on the screen.
/// </summary>
public sealed class VirtualizingItemsControlRowViewTests
{
    private const int RowHeight = 20;
    private const int RowsOnTheScreen = 10;

    [Test]
    public Task AScrollBuildsNoRowView() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                using ShownVirtual shown = new(model: model, buffer: RowsOnTheScreen);

                // The first step makes the containers that the panel keeps beyond the screen, and each
                // of them needs a view.
                ScrollBy(shown: shown, rows: 3);
                int built = shown.RowViewsBuilt;

                for (int step = 0; step < 40; step++)
                {
                    ScrollBy(shown: shown, rows: 3);
                }

                await Assert.That(shown.RowViewsBuilt - built).IsEqualTo(expected: 0);
                await Assert.That(ShowsItsRows(shown: shown)).IsTrue();
            });

    [Test]
    public Task AScrollInTheTwoDirectionsShowsEachRowAtItsPosition() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                using ShownVirtual shown = new(model: model, buffer: RowsOnTheScreen);
                bool isCorrect = true;

                foreach (int rows in new[] { 3, 7, -2, 9, -9, 4, -5, 1, 8, -8 })
                {
                    ScrollBy(shown: shown, rows: rows);
                    isCorrect &= ShowsItsRows(shown: shown);
                }

                await Assert.That(isCorrect).IsTrue();
                await Assert.That(shown.PlaceholdersInRowViews).IsEqualTo(expected: 0);
            });

    [Test]
    public Task ARowThatAPlaceholderReplacesLeavesItsViewWithTheControl() =>
        TestApplication.Run(
            test: static async () =>
            {
                GivenRowsModel model = new();
                model.Give(value: Given(first: 0, count: RowsOnTheScreen));
                using ShownVirtual shown = new(model: model, buffer: 0);

                await Assert.That(shown.List.KeptRowViews.Count).IsEqualTo(expected: 0);

                model.Give(value: VirtualRows.Empty with { TotalCount = 1000 });
                shown.Settle();

                await Assert.That(shown.List.KeptRowViews.Count).IsEqualTo(expected: RowsOnTheScreen);

                // A kept view that holds its row keeps the row, and each object that the row holds,
                // from the garbage collector.
                await Assert.That(shown.List.KeptRowViews.All(predicate: static view => view.DataContext is null))
                    .IsTrue();
            });

    [Test]
    public Task ARowThatArrivesAtAPlaceholderTakesAKeptView() =>
        TestApplication.Run(
            test: static async () =>
            {
                GivenRowsModel model = new();
                model.Give(value: Given(first: 0, count: RowsOnTheScreen));
                using ShownVirtual shown = new(model: model, buffer: 0);

                model.Give(value: VirtualRows.Empty with { TotalCount = 1000 });
                shown.Settle();

                List<Control> kept = [.. shown.List.KeptRowViews];
                int built = shown.RowViewsBuilt;

                model.Give(value: Given(first: 100, count: RowsOnTheScreen));
                shown.Settle();

                await Assert.That(shown.RowViewsBuilt - built).IsEqualTo(expected: 0);
                await Assert.That(shown.List.KeptRowViews.Count).IsEqualTo(expected: 0);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(
                        expected: Rows(first: 100, count: RowsOnTheScreen),
                        ordering: CollectionOrdering.Matching);

                await Assert.That(
                        Views(shown: shown).ToHashSet(comparer: ReferenceEqualityComparer.Instance)
                            .SetEquals(other: kept))
                    .IsTrue();
            });

    // The presenter of Avalonia calls the overload of Build that takes a view. A caller of the other
    // overload has no view to give, and gets a row view all the same. For no content, it gets nothing.
    [Test]
    public Task TheTemplateOfAContainerBuildsARowViewForACallerWithNoView() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                using ShownVirtual shown = new(model: model, buffer: 0);

                IDataTemplate? template =
                    shown.List.GetRealizedContainers().OfType<ContentPresenter>().First().ContentTemplate;

                int built = shown.RowViewsBuilt;

                await Assert.That(template?.Build(param: new VirtualRow(Position: 5))).IsNotNull();
                await Assert.That(shown.RowViewsBuilt - built).IsEqualTo(expected: 1);
                await Assert.That(template?.Build(param: null)).IsNull();
            });

    [Test]
    public Task ARowTemplateThatDoesNotUseItsViewAgainGetsAViewForEachRow() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                using ShownVirtual shown = new(model: model, buffer: RowsOnTheScreen);
                int built = 0;

                shown.List.ItemTemplate =
                    new FuncDataTemplate<object>(
                        build: (_, _) =>
                        {
                            built++;

                            return new TextBlock { Height = RowHeight };
                        },
                        supportsRecycling: false);

                shown.Settle();
                ScrollBy(shown: shown, rows: 3);
                int builtBefore = built;

                for (int step = 0; step < 10; step++)
                {
                    ScrollBy(shown: shown, rows: 3);
                }

                // Three rows come on the screen at each step.
                await Assert.That(built - builtBefore).IsEqualTo(expected: 30);
                await Assert.That(shown.List.KeptRowViews.Count).IsEqualTo(expected: 0);
            });

    [Test]
    public Task AControlThatDoesNotRecycleRowViewsBuildsAViewForEachRow() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                using ShownVirtual shown = new(model: model, buffer: RowsOnTheScreen);

                ScrollBy(shown: shown, rows: 3);

                shown.List.RecyclesRowViews = false;
                shown.Settle();
                int built = shown.RowViewsBuilt;

                for (int step = 0; step < 10; step++)
                {
                    ScrollBy(shown: shown, rows: 3);
                }

                await Assert.That(shown.RowViewsBuilt - built).IsEqualTo(expected: 30);
                await Assert.That(shown.List.KeptRowViews.Count).IsEqualTo(expected: 0);
                await Assert.That(ShowsItsRows(shown: shown)).IsTrue();
            });

    [Test]
    public Task ADifferentRowTemplateGetsNoViewOfTheTemplateFromBefore() =>
        TestApplication.Run(
            test: static async () =>
            {
                GivenRowsModel model = new();
                model.Give(value: Given(first: 0, count: RowsOnTheScreen));
                using ShownVirtual shown = new(model: model, buffer: 0);

                model.Give(value: VirtualRows.Empty with { TotalCount = 1000 });
                shown.Settle();

                List<Control> kept = [.. shown.List.KeptRowViews];
                List<Control?> received = [];

                FuncDataTemplate<object> different =
                    new(build: static (_, _) => new TextBlock { Height = RowHeight }, supportsRecycling: true);

                // The template of Avalonia gives back the view that it receives. This one records it.
                shown.List.ItemTemplate = new Recording(inner: different, received: received);
                shown.Settle();

                model.Give(value: Given(first: 100, count: RowsOnTheScreen));
                shown.Settle();

                await Assert.That(received.Count).IsEqualTo(expected: RowsOnTheScreen);

                HashSet<object?> keptBefore = new(collection: kept, comparer: ReferenceEqualityComparer.Instance);

                await Assert.That(received.Any(predicate: keptBefore.Contains)).IsFalse();
            });

    [Test]
    public Task TheControlKeepsNoMoreViewsThanThePanelHadContainers() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                using ShownVirtual shown = new(model: model, buffer: RowsOnTheScreen);
                int mostContainers = 0;
                int mostKept = 0;

                // Steps in the rows that the model has, and jumps beyond them.
                foreach (int rows in new[] { 3, 9, -4, 200, 5, -150, 7, 400, -3, -300, 2, 11, -11 })
                {
                    ScrollBy(shown: shown, rows: rows);
                    mostContainers = Math.Max(val1: mostContainers, val2: shown.List.GetRealizedContainers().Count());
                    mostKept = Math.Max(val1: mostKept, val2: shown.List.KeptRowViews.Count);
                }

                await Assert.That(mostKept).IsLessThanOrEqualTo(maximum: mostContainers);
                await Assert.That(ShowsItsRows(shown: shown)).IsTrue();
            });

    private static void ScrollBy(ShownVirtual shown, int rows)
    {
        shown.Scroller.Offset += new Vector(x: 0, y: rows * RowHeight);
        shown.Settle();
    }

    // True when each container shows the row of its position.
    private static bool ShowsItsRows(ShownVirtual shown) =>
        shown.Shown().All(predicate: static pair => pair.Text == $"Row {pair.Position}");

    // The rows for a number of positions in sequence, from the first position of a list of 1,000. The
    // row of a position shows the number that the position has plus the first number.
    private static VirtualRows Given(int first, int count) =>
        new(
            TotalCount: 1000,
            Offset: 0,
            Items:
            [
                .. Enumerable.Range(start: first, count: count)
                    .Select(selector: static position => new VirtualRow(Position: position))
            ]);

    private static List<string> Rows(int first, int count) =>
        [.. Enumerable.Range(start: first, count: count).Select(selector: static key => $"Row {key}")];

    private static List<string> Texts(ShownVirtual shown) =>
        [.. shown.Shown().Select(selector: static pair => pair.Text)];

    private static List<Control?> Views(ShownVirtual shown) =>
    [
        .. shown.List.GetRealizedContainers()
            .Select(selector: static container => (container as ContentPresenter)?.Child)
    ];

    // A row template that records the view that it receives for each row.
    private sealed class Recording(in IRecyclingDataTemplate inner, in List<Control?> received) : IRecyclingDataTemplate
    {
        private readonly IRecyclingDataTemplate inner = inner;
        private readonly List<Control?> received = received;

        public bool Match(object? data) => this.inner.Match(data: data);

        public Control? Build(object? param) => this.Build(data: param, existing: null);

        public Control? Build(object? data, Control? existing)
        {
            this.received.Add(item: existing);

            return this.inner.Build(data: data, existing: existing);
        }
    }
}
