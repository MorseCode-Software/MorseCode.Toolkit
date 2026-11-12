using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.Primitives;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Controls.UI.Avalonia.Tests;

/// <summary>
///     Checks a <see cref="VirtualizingItemsControl" /> whose panel is a <see cref="RecyclingPanel" />.
///     The control cannot read the range of such a panel. Thus, it reads the containers, and it must
///     read them only after the layout of the panel. Each test counts the values of the rows that the
///     view model sends, as the tests with the default panel do.
/// </summary>
public sealed class VirtualizingItemsControlPanelTests
{
    private const string PlaceholderText = "placeholder";

    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task AFirstLayoutReportsThePositionsOfTheContainers(int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = Show(model: model, buffer: buffer);

                await Assert.That(model.RowsValues).IsEqualTo(expected: 1);

                await Assert.That(shown.List.ReportedRange.Constructed)
                    .IsEqualTo(expected: new ItemRange(First: 0, Count: 10 + buffer));

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 0, count: 10), ordering: CollectionOrdering.Matching);
            });

    // Each container gets a different row. The control changes the containers of this panel as it
    // changes those of the default panel.
    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task ARemovedFirstKeyBringsTheRowsOnce(int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = Show(model: model, buffer: buffer);

                int before = model.RowsValues;

                model.Remove(key: 0);
                shown.Settle();

                await Assert.That(model.RowsValues - before).IsEqualTo(expected: 1);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 1, count: 10), ordering: CollectionOrdering.Matching);
            });

    // Only the containers from the key down get a different row.
    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task ARemovedKeyInsideTheRowsBringsTheRowsOnce(int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = Show(model: model, buffer: buffer);

                int before = model.RowsValues;

                model.Remove(key: 4);
                shown.Settle();

                await Assert.That(model.RowsValues - before).IsEqualTo(expected: 1);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(
                        expected: [.. Rows(first: 0, count: 4), .. Rows(first: 5, count: 6)],
                        ordering: CollectionOrdering.Matching);
            });

    // The second value is the answer to the shorter range, as with the default panel.
    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task ARemovedKeyInAListThatEndsInsideTheWindowBringsTheRowsAndThenTheSameRowsForTheShorterRange(
        int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 5, retainedBeyondTheView: 512);
                using ShownVirtual shown = Show(model: model, buffer: buffer);

                int before = model.RowsValues;
                int built = model.Built.Count;

                model.Remove(key: 2);
                shown.Settle();

                await Assert.That(model.RowsValues - before).IsEqualTo(expected: 2);
                await Assert.That(model.Built.Count).IsEqualTo(expected: built);

                await Assert.That(shown.List.ReportedRange.Constructed)
                    .IsEqualTo(expected: new ItemRange(First: 0, Count: 4));

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(
                        expected: ["Row 0", "Row 1", "Row 3", "Row 4"],
                        ordering: CollectionOrdering.Matching);
            });

    // The four tests that follow are the tests of the default panel, with this panel.
    // Each range that the control reports stops at the count. The positions that the control wants after
    // the end of the list are a number, which only the slice of a collection source reads.
    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task TheRangesOfAListThatEndsInsideTheWindowStayInsideTheList(int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 5, retainedBeyondTheView: 512);
                using ShownVirtual shown = Show(model: model, buffer: buffer);

                ReportedRange reported = shown.List.ReportedRange;

                await Assert.That(reported.Visible).IsEqualTo(expected: new ItemRange(First: 0, Count: 5));
                await Assert.That(reported.Constructed).IsEqualTo(expected: new ItemRange(First: 0, Count: 5));
                await Assert.That(reported.Ahead).IsEqualTo(expected: buffer);
            });

    // With no buffer, the control wants no position after the end of the list. The first value has the
    // new count. The control can report the new position only after the panel made a container for it.
    // The second value has the row for that position.
    [Test]
    public Task AnAddedKeyAtAnEndThatShowsBringsTheCountAndThenTheRowWithNoBuffer() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 5, retainedBeyondTheView: 512);
                using ShownVirtual shown = Show(model: model, buffer: 0);

                int before = model.RowsValues;

                model.Add(key: 5);
                shown.Settle();

                await Assert.That(model.RowsValues - before).IsEqualTo(expected: 2);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 0, count: 6), ordering: CollectionOrdering.Matching);
            });

    // With a buffer, the slice of the source goes on after the end of the list. Thus, the first value
    // has the row of the new key, and no layout shows a placeholder. The control then reports the
    // longer ranges, and the second value is the answer to them, with the same rows.
    [Test]
    public Task AnAddedKeyAtAnEndThatShowsComesWithItsRow() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 5, retainedBeyondTheView: 512);
                using ShownVirtual shown = Show(model: model, buffer: 10);

                shown.WatchLayouts();

                int before = model.RowsValues;

                model.Add(key: 5);
                shown.Settle();

                await Assert.That(model.RowsValues - before).IsEqualTo(expected: 2);
                await Assert.That(shown.TextsAtLayouts).IsNotEmpty();
                await Assert.That(shown.TextsAtLayouts).DoesNotContain(expected: PlaceholderText);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 0, count: 6), ordering: CollectionOrdering.Matching);
            });

    // An empty list shows no position, and the control wants the positions that come. Thus, the first
    // key comes with its row.
    [Test]
    public Task TheFirstKeyOfAnEmptyListComesWithItsRow() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 0, retainedBeyondTheView: 512);
                using ShownVirtual shown = Show(model: model, buffer: 10);

                await Assert.That(shown.List.ReportedRange)
                    .IsEqualTo(expected: ReportedRange.Empty with { Ahead = 10 });

                shown.WatchLayouts();

                model.Add(key: 0);
                shown.Settle();

                await Assert.That(shown.TextsAtLayouts).IsNotEmpty();
                await Assert.That(shown.TextsAtLayouts).DoesNotContain(expected: PlaceholderText);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 0, count: 1), ordering: CollectionOrdering.Matching);
            });

    // The control cannot change the row in a container of this type, thus it replaces the item. The
    // panel recycles the container of a replaced item at once, and makes it again at the next layout.
    // A report between the two reads too few containers. The view model then drops rows that show, and
    // the control asks for them again without end.
    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task ARemovedKeyBringsTheRowsOnceWhenTheControlReplacesTheItems(int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);

                using ShownVirtual shown =
                    new(
                        model: model,
                        buffer: buffer,
                        panel: static () =>
                            new RecyclingPanel(makeContainer: static () => new HeaderedContentControl()));

                RecyclingPanel? panel = shown.List.ItemsPanelRoot as RecyclingPanel;
                int recycled = panel?.Recycled ?? 0;
                int before = model.RowsValues;

                model.Remove(key: 4);
                shown.Settle();

                // The six positions from the key down got a different row, each by a replaced item.
                await Assert.That((panel?.Recycled ?? 0) - recycled).IsEqualTo(expected: 6);
                await Assert.That(model.RowsValues - before).IsEqualTo(expected: 1);

                await Assert.That(shown.List.ReportedRange.Constructed)
                    .IsEqualTo(expected: new ItemRange(First: 0, Count: 10 + buffer));
            });

    private static ShownVirtual Show(IVirtualItemsSource model, int buffer) =>
        new(model: model, buffer: buffer, panel: static () => new RecyclingPanel());

    // The texts of a number of rows in sequence. A placeholder is never one of them. Thus, a test that
    // compares with these texts also checks that no placeholder shows.
    private static List<string> Rows(int first, int count) =>
        [.. Enumerable.Range(start: first, count: count).Select(selector: static key => $"Row {key}")];

    private static List<string> Texts(ShownVirtual shown) =>
        [.. shown.Shown().Select(selector: static pair => pair.Text)];
}
