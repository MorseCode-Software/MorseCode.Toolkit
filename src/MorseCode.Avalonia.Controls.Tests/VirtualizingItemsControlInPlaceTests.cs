using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     Checks that a shown <see cref="VirtualizingItemsControl" /> keeps the container of a position
///     whose item changes, and that the list then stays at its position.
/// </summary>
public sealed class VirtualizingItemsControlInPlaceTests
{
    private const string PlaceholderText = "placeholder";

    [Test]
    public Task ARowThatChangesKeepsItsContainerAndItsView() =>
        TestApplication.Run(
            test: static async () =>
            {
                GivenRowsModel model = new();
                model.Give(value: Given(first: 0, count: 10));
                using ShownVirtual shown = new(model: model, buffer: 0);

                List<Control> containers = Containers(shown: shown);
                List<Control?> views = Views(shown: shown);
                int built = shown.RowViewsBuilt;
                int prepared = 0;
                shown.List.ContainerPrepared += (_, _) => prepared++;

                // Each position gets a different row object.
                model.Give(value: Given(first: 100, count: 10));
                shown.Settle();

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 100, count: 10), ordering: CollectionOrdering.Matching);

                await Assert.That(AreTheSame(first: Containers(shown: shown), second: containers)).IsTrue();

                await Assert.That(AreTheSame(first: Views(shown: shown), second: views)).IsTrue();

                await Assert.That(shown.RowViewsBuilt - built).IsEqualTo(expected: 0);
                await Assert.That(prepared).IsEqualTo(expected: 0);
            });

    [Test]
    public Task ARowThatArrivesAtAPlaceholderBuildsOneViewInTheSameContainer() =>
        TestApplication.Run(
            test: static async () =>
            {
                GivenRowsModel model = new();
                model.Give(value: VirtualRows.Empty with { TotalCount = 1000 });
                using ShownVirtual shown = new(model: model, buffer: 0);

                List<Control> containers = Containers(shown: shown);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(
                        expected: Enumerable.Repeat(element: PlaceholderText, count: 10),
                        ordering: CollectionOrdering.Matching);

                model.Give(value: Given(first: 0, count: 10));
                shown.Settle();

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 0, count: 10), ordering: CollectionOrdering.Matching);

                await Assert.That(AreTheSame(first: Containers(shown: shown), second: containers)).IsTrue();

                await Assert.That(shown.RowViewsBuilt).IsEqualTo(expected: 10);
                await Assert.That(shown.PlaceholdersInRowViews).IsEqualTo(expected: 0);
            });

    [Test]
    public Task ARowThatLeavesShowsAPlaceholderInTheSameContainer() =>
        TestApplication.Run(
            test: static async () =>
            {
                GivenRowsModel model = new();
                model.Give(value: Given(first: 0, count: 10));
                using ShownVirtual shown = new(model: model, buffer: 0);

                List<Control> containers = Containers(shown: shown);
                int built = shown.RowViewsBuilt;

                // The first five positions keep their row objects, and the other five get a placeholder.
                model.Give(value: shown.List.Rows with { Items = [.. shown.List.Rows.Items.Take(count: 5)] });
                shown.Settle();

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(
                        expected:
                        [
                            .. Rows(first: 0, count: 5), .. Enumerable.Repeat(element: PlaceholderText, count: 5)
                        ],
                        ordering: CollectionOrdering.Matching);

                await Assert.That(AreTheSame(first: Containers(shown: shown), second: containers)).IsTrue();

                await Assert.That(shown.RowViewsBuilt - built).IsEqualTo(expected: 0);
                await Assert.That(shown.PlaceholdersInRowViews).IsEqualTo(expected: 0);
            });

    // A control is its own container, thus the control cannot give its container a different item.
    // The list then raises the event that makes Avalonia build the container again.
    [Test]
    public Task AnItemThatIsAControlIsReplacedThroughThePanel() =>
        TestApplication.Run(
            test: static async () =>
            {
                GivenRowsModel model = new();
                VirtualRows rows = Given(first: 0, count: 10);
                model.Give(value: rows);
                using ShownVirtual shown = new(model: model, buffer: 0);

                List<Control> containers = Containers(shown: shown);
                List<int> replaced = [];

                if (shown.List.ItemsSource is INotifyCollectionChanged items)
                {
                    items.CollectionChanged +=
                        (_, args) =>
                        {
                            if (args.Action == NotifyCollectionChangedAction.Replace)
                            {
                                replaced.Add(item: args.NewStartingIndex);
                            }
                        };
                }

                TextBlock control = new() { Text = "a control", Height = 20 };

                model.Give(
                    value: rows with
                    {
                        Items = [.. rows.Items.Take(count: 3), control, .. rows.Items.Skip(count: 4)]
                    });

                shown.Settle();

                await Assert.That(replaced).IsEquivalentTo(expected: [3], ordering: CollectionOrdering.Matching);
                await Assert.That(shown.List.ContainerFromIndex(index: 3)).IsSameReferenceAs(expected: control);

                await Assert.That(
                        AreTheSame(
                            first: Containers(shown: shown).Where(predicate: static (_, index) => index != 3),
                            second: containers.Where(predicate: static (_, index) => index != 3)))
                    .IsTrue();

                model.Give(value: rows);
                shown.Settle();

                await Assert.That(replaced).IsEquivalentTo(expected: [3, 3], ordering: CollectionOrdering.Matching);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 0, count: 10), ordering: CollectionOrdering.Matching);
            });

    // The rows are higher than the placeholders, and the list came to its position by a jump. Before
    // the control kept its containers, the panel removed each of them at an edit above the rows. It
    // then estimated a different part of the list, and the view model sent rows for that part.
    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task ARemovedKeyAboveHigherRowsKeepsTheListAtItsPosition(int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: buffer, rowHeight: 40, placeholderHeight: 20);

                shown.Scroller.Offset = new Vector(x: 0, y: 5000);
                shown.Settle();

                // The list of 1,000 rows of 40 is 40,000 high, thus 5,000 is position 125.
                await Assert.That(shown.Shown().Select(selector: static pair => pair.Position).ToList())
                    .IsEquivalentTo(
                        expected: Enumerable.Range(start: 125, count: 5),
                        ordering: CollectionOrdering.Matching);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 125, count: 5), ordering: CollectionOrdering.Matching);

                int before = model.RowsValues;
                shown.WatchLayouts();

                model.Remove(key: 0);
                shown.Settle();

                await Assert.That(model.RowsValues - before).IsEqualTo(expected: 1);
                await Assert.That(shown.TextsAtLayouts).IsNotEmpty();
                await Assert.That(shown.TextsAtLayouts).DoesNotContain(expected: PlaceholderText);

                await Assert.That(shown.Shown().Select(selector: static pair => pair.Position).ToList())
                    .IsEquivalentTo(
                        expected: Enumerable.Range(start: 125, count: 5),
                        ordering: CollectionOrdering.Matching);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 126, count: 5), ordering: CollectionOrdering.Matching);
            });

    // A view model can make a new row object for each position at each answer. Before the control
    // kept its containers, each answer then replaced each container. With rows higher than the
    // placeholders, the panel estimated a different part of the list after each answer, without end.
    [Test]
    public Task AViewModelThatMakesNewRowObjectsSettlesAfterAJump() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000) { MakesNewRows = true };
                using ShownVirtual shown = new(model: model, buffer: 0, rowHeight: 40, placeholderHeight: 20);

                shown.Scroller.Offset = new Vector(x: 0, y: 5000);
                shown.Settle();

                // Ten placeholders show first, and then five rows. Thus, the control reports two ranges
                // at each position.
                await Assert.That(model.Writes)
                    .IsEquivalentTo(
                        expected:
                        [
                            new ItemRange(First: 0, Count: 10),
                            new ItemRange(First: 0, Count: 5),
                            new ItemRange(First: 125, Count: 10),
                            new ItemRange(First: 125, Count: 5)
                        ],
                        ordering: CollectionOrdering.Matching);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 125, count: 5), ordering: CollectionOrdering.Matching);
            });

    // The focus is state of the container, thus it stays at the position and goes to the next row.
    // Before the control kept its containers, the panel removed the container, and the focus left
    // the list.
    [Test]
    public Task TheFocusStaysInItsContainerWhenTheRowOfThePositionChanges() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: 0);

                Control container = Containers(shown: shown)[index: 3];
                container.Focusable = true;
                container.Focus();

                await Assert.That(container.IsFocused).IsTrue();

                model.Remove(key: 0);
                shown.Settle();

                await Assert.That(container.IsFocused).IsTrue();
                await Assert.That(shown.List.ContainerFromIndex(index: 3)).IsSameReferenceAs(expected: container);
                await Assert.That(Texts(shown: shown)[index: 3]).IsEqualTo(expected: "Row 4");
            });

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

    private static List<Control> Containers(ShownVirtual shown) =>
    [
        .. shown.List.GetRealizedContainers()
            .OrderBy(keySelector: container => shown.List.IndexFromContainer(container: container))
    ];

    // An assertion of TUnit compares two controls by their members. For these tests, the two must be
    // the same object.
    private static bool AreTheSame(IEnumerable<Control?> first, IEnumerable<Control?> second) =>
        first.SequenceEqual(second: second, comparer: ReferenceEqualityComparer.Instance);

    private static List<Control?> Views(ShownVirtual shown) =>
        [.. Containers(shown: shown).Select(selector: static container => (container as ContentPresenter)?.Child)];
}
