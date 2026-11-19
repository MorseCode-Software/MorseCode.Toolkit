using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Threading;
using MorseCode.Controls.ViewModels;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Controls.UI.Avalonia.Tests;

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

    // A typed row template does not match an item of a different type. The presenter then shows that item
    // with a template that it finds itself, and the view of that template must not go to a row. Before,
    // the control kept each view that left a presenter of its template, and the next row showed the
    // view of the default template.
    [Test]
    public Task TheControlKeepsOnlyTheViewsThatTheRowTemplateBuilt() =>
        TestApplication.Run(
            test: static async () =>
            {
                GivenRowsModel model = new();
                using ShownVirtual shown = new(model: model, buffer: 0);

                shown.List.ItemTemplate =
                    new FuncDataTemplate<VirtualRow>(build: static (_, _) => TypedRowView(), supportsRecycling: true);

                // The rows at the even positions are of the row template, and the rows at the odd positions
                // are text.
                model.Give(
                    value: new VirtualRows(
                        TotalCount: 1000,
                        Offset: 0,
                        Items:
                        [
                            .. Enumerable.Range(start: 0, count: RowsOnTheScreen)
                                .Select<int, object?>(selector: static position =>
                                    position % 2 == 0 ? new VirtualRow(Position: position) : $"text {position}")
                        ]));

                shown.Settle();

                // The panel also has a container beyond the screen, which shows a placeholder.
                List<Control?> typedViews =
                [
                    .. shown.List.GetRealizedContainers()
                        .OfType<ContentPresenter>()
                        .Where(predicate: static presenter => presenter.Content is VirtualRow)
                        .Select(selector: static presenter => presenter.Child)
                ];

                await Assert.That(typedViews.Count).IsEqualTo(expected: RowsOnTheScreen / 2);

                model.Give(value: VirtualRows.Empty with { TotalCount = 1000 });
                shown.Settle();

                await Assert.That(
                        shown.List.KeptRowViews.ToHashSet(comparer: ReferenceEqualityComparer.Instance)
                            .SetEquals(other: typedViews))
                    .IsTrue();

                model.Give(value: Given(first: 100, count: RowsOnTheScreen));
                shown.Settle();

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(
                        expected: Rows(first: 100, count: RowsOnTheScreen),
                        ordering: CollectionOrdering.Matching);
            });

    // The control marks each view of the row template in a weak table. Here the row template builds a
    // view for each row and does not use the kept view that it gets. Thus, only the mark refers to the
    // views of the first rows, and the collector must remove them while the control and its template stay.
    [Test]
    public Task TheMarkOfARowViewDoesNotKeepTheViewAlive() =>
        TestApplication.Run(
            test: static async () =>
            {
                GivenRowsModel model = new();
                model.Give(value: Given(first: 0, count: RowsOnTheScreen));
                using ShownVirtual shown = new(model: model, buffer: 0);

                NewViewForEachRow template = new();
                shown.List.ItemTemplate = template;
                shown.Settle();

                model.Give(value: VirtualRows.Empty with { TotalCount = 1000 });
                shown.Settle();

                int firstViews = template.Built.Count;

                await Assert.That(firstViews).IsGreaterThanOrEqualTo(minimum: RowsOnTheScreen);
                await Assert.That(shown.List.KeptRowViews.Count).IsEqualTo(expected: firstViews);

                // Each row takes a kept view, and the row template does not use it.
                model.Give(value: Given(first: 100, count: RowsOnTheScreen));
                shown.Settle();

                await Assert.That(shown.List.KeptRowViews.Count).IsEqualTo(expected: 0);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(
                        expected: Rows(first: 100, count: RowsOnTheScreen),
                        ordering: CollectionOrdering.Matching);

                List<WeakReference<Control>> dropped = [.. template.Built.Take(count: firstViews)];

                await Assert.That(await CollectAll(views: dropped)).IsTrue();

                // The control and its template must be alive while the collector runs, because the
                // table of the marks is in the template of the control.
                await Assert.That(shown.List.ItemTemplate).IsSameReferenceAs(expected: template);
            });

    // The table of the marks belongs to the template of the control. A view that the app keeps after
    // the control is gone must not keep the control through its mark. A mark on the view itself, such as
    // a property that refers to the template, does that.
    [Test]
    public Task AKeptViewThatOutlivesTheControlDoesNotKeepTheControlAlive() =>
        TestApplication.Run(
            test: static async () =>
            {
                GivenRowsModel model = new();

                (WeakReference<VirtualizingItemsControl> control, List<Control> kept) =
                    ShowKeepViewsAndRemove(model: model);

                await Assert.That(kept.Count).IsGreaterThanOrEqualTo(minimum: RowsOnTheScreen);

                // A job of the dispatcher that the window left behind can hold the control for one more
                // turn. A leak holds it for all of them.
                for (int attempt = 0; attempt < 50 && IsAlive(reference: control); attempt++)
                {
                    await Collect();
                }

                await Assert.That(IsAlive(reference: control)).IsFalse();

                // The views and the source must be alive while the collector runs, because they are what
                // can keep the control.
                GC.KeepAlive(obj: kept);
                GC.KeepAlive(obj: model);
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

    // With no ItemTemplate, DisplayMemberBinding gives the text of each row. Before, the control set the
    // template of each presenter to the ItemTemplate, which is null. That replaced the template of the
    // base, and each row showed the ToString of its object.
    [Test]
    public Task DisplayMemberBindingShowsTheMemberOfEachRowAcrossAScroll() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                using ShownVirtual shown = new(model: model, buffer: RowsOnTheScreen);

                ShowTheMember(shown: shown, path: nameof(VirtualRow.Text));

                bool isCorrect = ShowsItsRows(shown: shown);

                foreach (int rows in new[] { 3, 7, -2, 9, -9, 40, -35 })
                {
                    ScrollBy(shown: shown, rows: rows);
                    isCorrect &= ShowsItsRows(shown: shown);
                }

                await Assert.That(isCorrect).IsTrue();
            });

    // The control sets the template of a container when a placeholder and a row change places. Thus, the
    // template of the base, which it sets only when it prepares a container, is gone after a placeholder.
    // The rows that come at the placeholders also use the kept views.
    [Test]
    public Task DisplayMemberBindingShowsTheMemberOfARowThatArrivesAtAPlaceholder() =>
        TestApplication.Run(
            test: static async () =>
            {
                GivenRowsModel model = new();
                model.Give(value: Given(first: 0, count: RowsOnTheScreen));
                using ShownVirtual shown = new(model: model, buffer: 0);

                ShowTheMember(shown: shown, path: nameof(VirtualRow.Text));

                await Assert.That(Texts(shown: shown).Take(count: RowsOnTheScreen))
                    .IsEquivalentTo(expected: Rows(first: 0, count: RowsOnTheScreen), ordering: CollectionOrdering.Matching);

                model.Give(value: VirtualRows.Empty with { TotalCount = 1000 });
                shown.Settle();

                List<Control> kept = [.. shown.List.KeptRowViews];

                await Assert.That(kept.Count).IsGreaterThanOrEqualTo(minimum: RowsOnTheScreen);

                model.Give(value: Given(first: 100, count: RowsOnTheScreen));
                shown.Settle();

                await Assert.That(Texts(shown: shown).Take(count: RowsOnTheScreen))
                    .IsEquivalentTo(
                        expected: Rows(first: 100, count: RowsOnTheScreen),
                        ordering: CollectionOrdering.Matching);

                HashSet<Control> keptBefore = new(collection: kept, comparer: ReferenceEqualityComparer.Instance);

                await Assert.That(
                        Views(shown: shown)
                            .Take(count: RowsOnTheScreen)
                            .All(predicate: view => view is not null && keptBefore.Contains(item: view)))
                    .IsTrue();
            });

    // A new DisplayMemberBinding gives each row the new member, also in a view that the control kept.
    [Test]
    public Task ANewDisplayMemberBindingShowsTheNewMember() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                using ShownVirtual shown = new(model: model, buffer: RowsOnTheScreen);

                ShowTheMember(shown: shown, path: nameof(VirtualRow.Text));

                shown.List.DisplayMemberBinding = new Binding(path: nameof(VirtualRow.Position));
                shown.Settle();

                bool isCorrect = ShowsItsPositions(shown: shown);

                ScrollBy(shown: shown, rows: 40);
                isCorrect &= ShowsItsPositions(shown: shown);

                await Assert.That(isCorrect).IsTrue();
            });

    private static void ShowTheMember(ShownVirtual shown, string path)
    {
        // The base does not accept DisplayMemberBinding while there is an ItemTemplate.
        shown.List.ItemTemplate = null;
        shown.List.DisplayMemberBinding = new Binding(path: path);
        shown.Settle();
    }

    // True when each container shows the number of its position.
    private static bool ShowsItsPositions(ShownVirtual shown) =>
        shown.Shown().All(predicate: static pair => pair.Text == pair.Position.ToString(provider: CultureInfo.InvariantCulture));

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

    // A view of a typed row template that reads its row from its data context, as a binding does.
    private static TextBlock TypedRowView()
    {
        TextBlock view = new() { Height = RowHeight };

        view.DataContextChanged +=
            static (sender, _) =>
            {
                if (sender is TextBlock shownView)
                {
                    shownView.Text = (shownView.DataContext as VirtualRow)?.Text ?? "no row";
                }
            };

        return view;
    }

    // A separate frame makes the control, the window, and the kept views. Thus, no slot of the stack of
    // the test holds the control.
    [MethodImpl(methodImplOptions: MethodImplOptions.NoInlining)]
    private static (WeakReference<VirtualizingItemsControl> Control, List<Control> Kept) ShowKeepViewsAndRemove(
        GivenRowsModel model)
    {
        ShownVirtual shown = new(model: model, buffer: 0);

        // The template of the test host refers to the host, and the host refers to the control. This
        // template refers to nothing.
        shown.List.ItemTemplate =
            new FuncDataTemplate<object>(
                build: static (_, _) => new TextBlock { Height = RowHeight },
                supportsRecycling: true);

        model.Give(value: Given(first: 0, count: RowsOnTheScreen));
        shown.Settle();
        model.Give(value: VirtualRows.Empty with { TotalCount = 1000 });
        shown.Settle();

        List<Control> kept = [.. shown.List.KeptRowViews];
        WeakReference<VirtualizingItemsControl> control = new(target: shown.List);

        shown.Remove();
        shown.Dispose();

        // A report or a scroll that the control posted holds the control until the dispatcher runs it.
        Dispatcher.UIThread.RunJobs();

        return (control, kept);
    }

    // Runs the collector until no view is alive, or until the attempts end.
    private static async Task<bool> CollectAll(IReadOnlyCollection<WeakReference<Control>> views)
    {
        for (int attempt = 0; attempt < 50 && AnyIsAlive(references: views); attempt++)
        {
            await Collect();
        }

        return !AnyIsAlive(references: views);
    }

    private static async Task Collect()
    {
        await Task.Delay(millisecondsDelay: 10);
        Dispatcher.UIThread.RunJobs();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    // TryGetTarget gives a strong reference. In a frame of its own, that reference is gone when the call
    // returns, and the collector does not see it.
    [MethodImpl(methodImplOptions: MethodImplOptions.NoInlining)]
    private static bool AnyIsAlive(IEnumerable<WeakReference<Control>> references) =>
        references.Any(predicate: static reference => reference.TryGetTarget(target: out _));

    [MethodImpl(methodImplOptions: MethodImplOptions.NoInlining)]
    private static bool IsAlive(WeakReference<VirtualizingItemsControl> reference) =>
        reference.TryGetTarget(target: out _);

    private static List<Control?> Views(ShownVirtual shown) =>
    [
        .. shown.List.GetRealizedContainers()
            .Select(selector: static container => (container as ContentPresenter)?.Child)
    ];

    // A row template that builds a view for each row and does not use the view that it gets. It refers
    // to each view that it built only through a weak reference.
    private sealed class NewViewForEachRow : IRecyclingDataTemplate
    {
        public List<WeakReference<Control>> Built { get; } = [];

        public bool Match(object? data) => data is VirtualRow;

        public Control Build(object? param) => this.Build(data: param, existing: null);

        public Control Build(object? data, Control? existing)
        {
            TextBlock view = new() { Height = RowHeight, Text = (data as VirtualRow)?.Text };
            this.Built.Add(item: new WeakReference<Control>(target: view));

            return view;
        }
    }

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
