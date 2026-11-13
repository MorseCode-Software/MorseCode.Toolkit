using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using MorseCode.Controls.ViewModels;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Controls.UI.Avalonia.Tests;

/// <summary>
///     Checks a shown <see cref="VirtualizingItemsControl" /> while its collection changes, and while
///     it scrolls by less than a screen. Each test counts the values of the rows that the view model
///     sends. A control that reports an incorrect range makes the view model send the rows again.
/// </summary>
public sealed class VirtualizingItemsControlEditTests
{
    private const string PlaceholderText = "placeholder";

    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task AScrollByThreeRowsBringsTheRowsOnce(int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: buffer);

                int before = model.RowsValues;

                shown.Scroller.Offset = new Vector(x: 0, y: 60);
                shown.Settle();

                await Assert.That(model.RowsValues - before).IsEqualTo(expected: 1);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 3, count: 10), ordering: CollectionOrdering.Matching);

                before = model.RowsValues;

                shown.Scroller.Offset = new Vector(x: 0, y: 0);
                shown.Settle();

                await Assert.That(model.RowsValues - before).IsEqualTo(expected: 1);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 0, count: 10), ordering: CollectionOrdering.Matching);
            });

    // Each row that shows moves up by one position, thus each container gets a different row.
    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task ARemovedKeyAboveTheRowsBringsTheRowsOnce(int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: buffer);

                shown.Scroller.Offset = new Vector(x: 0, y: 10000);
                shown.Settle();

                int before = model.RowsValues;

                model.Remove(key: 0);
                shown.Settle();

                await Assert.That(model.RowsValues - before).IsEqualTo(expected: 1);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 501, count: 10), ordering: CollectionOrdering.Matching);
            });

    // The rows above the key stay, and only the containers from the key down get a different row.
    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task ARemovedKeyInsideTheRowsBringsTheRowsOnce(int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: buffer);

                int before = model.RowsValues;

                model.Remove(key: 4);
                shown.Settle();

                await Assert.That(model.RowsValues - before).IsEqualTo(expected: 1);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(
                        expected: [.. Rows(first: 0, count: 4), .. Rows(first: 5, count: 6)],
                        ordering: CollectionOrdering.Matching);
            });

    // The list ends in the window, thus the removal also removes the last container. The control wants
    // rows for a number of positions after the last container, and the slice of the source ends there.
    // Thus, the slice gets shorter, and the second value is the answer to it, with the same rows.
    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task ARemovedKeyInAListThatEndsInsideTheWindowBringsTheRowsAndThenTheSameRowsForTheShorterRange(
        int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 5, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: buffer);

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
                using ShownVirtual shown = new(model: model, buffer: buffer);

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
                using ShownVirtual shown = new(model: model, buffer: 0);

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
                using ShownVirtual shown = new(model: model, buffer: 10);

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
                using ShownVirtual shown = new(model: model, buffer: 10);

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

    // A list that the user reads at its end while keys arrive, such as a log.
    [Test]
    public Task AKeyAddedAtTheEndOfALongListThatShowsItsEndComesWithItsRow() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: 10);

                shown.Scroller.Offset = new Vector(x: 0, y: 19800);
                shown.Settle();

                await Assert.That(shown.List.ReportedRange.Visible)
                    .IsEqualTo(expected: new ItemRange(First: 990, Count: 10));

                await Assert.That(shown.List.ReportedRange.Constructed)
                    .IsEqualTo(expected: new ItemRange(First: 980, Count: 20));

                shown.WatchLayouts();

                model.Add(key: 1000);
                shown.Settle();
                shown.Scroller.Offset = new Vector(x: 0, y: 19820);
                shown.Settle();

                await Assert.That(shown.TextsAtLayouts).IsNotEmpty();
                await Assert.That(shown.TextsAtLayouts).DoesNotContain(expected: PlaceholderText);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 991, count: 10), ordering: CollectionOrdering.Matching);
            });

    // The sum of the last position and the positions on each side stops at the largest number. The
    // slice of the view model then has each key.
    [Test]
    public Task TheLargestBufferAsksForEachRow() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: int.MaxValue);

                await Assert.That(shown.List.ReportedRange.Constructed)
                    .IsEqualTo(expected: new ItemRange(First: 0, Count: 1000));

                await Assert.That(model.Built.Count).IsEqualTo(expected: 1000);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 0, count: 10), ordering: CollectionOrdering.Matching);
            });

    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task AnAddedKeyBelowTheRowsBringsTheCountOnly(int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: buffer);

                int before = model.RowsValues;
                int built = model.Built.Count;

                model.Add(key: 1000);
                shown.Settle();

                await Assert.That(model.RowsValues - before).IsEqualTo(expected: 1);
                await Assert.That(model.Built.Count).IsEqualTo(expected: built);
                await Assert.That(shown.Scroller.Extent.Height).IsEqualTo(expected: 20020);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 0, count: 10), ordering: CollectionOrdering.Matching);
            });

    // The row of a key is made from the key. Thus, a change of the state of an item is not a change of
    // the rows.
    [Test]
    [Arguments(0)]
    [Arguments(10)]
    public Task AChangedStateOfAKeyThatShowsBringsNoRows(int buffer) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: buffer);

                int before = model.RowsValues;

                model.Update(key: 3);
                shown.Settle();

                await Assert.That(model.RowsValues - before).IsEqualTo(expected: 0);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 0, count: 10), ordering: CollectionOrdering.Matching);
            });

    // The positions that show are the positions with a container. A row that shows in part is one of
    // them. The range that the control wants rows for has the positions on each side.
    [Test]
    public Task TheControlReportsThePositionsThatShowAndTheLargerRangeAroundThem() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = new(model: model, buffer: 10);

                // The rows are 20 high. Row 100 and row 110 show in part.
                shown.Scroller.Offset = new Vector(x: 0, y: 2007);
                shown.Settle();

                ReportedRange reported = shown.List.ReportedRange;

                await Assert.That(reported.Visible).IsEqualTo(expected: new ItemRange(First: 100, Count: 11));
                await Assert.That(reported.Constructed).IsEqualTo(expected: new ItemRange(First: 90, Count: 31));
                await Assert.That(reported.Ahead).IsEqualTo(expected: 0);
            });

    // An app cannot set the number of positions on each side. The control takes the number of rows on
    // the screen, and not fewer than 10.
    [Test]
    [Arguments(40, 5, 15)]
    [Arguments(20, 10, 20)]
    [Arguments(10, 20, 40)]
    public Task TheControlWantsRowsForOneScreenOnEachSideAndNotFewerThanTen(int rowHeight, int onScreen, int wanted) =>
        TestApplication.Run(
            test: async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = ShownVirtual.AsAnAppMakesIt(model: model, rowHeight: rowHeight);

                await Assert.That(shown.List.ReportedRange.Visible)
                    .IsEqualTo(expected: new ItemRange(First: 0, Count: onScreen));

                await Assert.That(shown.List.ReportedRange.Constructed)
                    .IsEqualTo(expected: new ItemRange(First: 0, Count: wanted));
            });

    // A scroll shows no placeholder when its step is not longer than the positions on one side. With
    // 20 rows on the screen, 10 positions are too few for a step of one screen.
    [Test]
    public Task AScrollByOneScreenShowsNoPlaceholderWhenManyRowsShow() =>
        TestApplication.Run(
            test: static async () =>
            {
                using RecipeModel model = new(total: 1000, retainedBeyondTheView: 512);
                using ShownVirtual shown = ShownVirtual.AsAnAppMakesIt(model: model, rowHeight: 10);

                shown.WatchLayouts();

                foreach (int step in Enumerable.Range(start: 1, count: 5))
                {
                    shown.Scroller.Offset = new Vector(x: 0, y: step * 200);
                    shown.Settle();
                }

                await Assert.That(shown.TextsAtLayouts).IsNotEmpty();
                await Assert.That(shown.TextsAtLayouts).DoesNotContain(expected: PlaceholderText);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 100, count: 20), ordering: CollectionOrdering.Matching);
            });

    // A view model with its own rows gets no row for a position after the end of its list. The control
    // reports when the count changes, and not only when a container changes. Thus, the row of the new
    // position comes before the layout that shows it.
    [Test]
    public Task ANewPositionAtAnEndThatShowsGetsItsRowBeforeItsLayoutFromAViewModelWithItsOwnRows() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 5);
                using ShownVirtual shown = new(model: model, buffer: 10);

                shown.WatchLayouts();

                model.Resize(total: 6);
                shown.Settle();

                await Assert.That(shown.TextsAtLayouts).IsNotEmpty();
                await Assert.That(shown.TextsAtLayouts).DoesNotContain(expected: PlaceholderText);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 0, count: 6), ordering: CollectionOrdering.Matching);
            });

    // A list that was empty has no container until its next layout. The control wants rows for the
    // first positions when the count comes.
    [Test]
    public Task TheFirstPositionOfAnEmptyListGetsItsRowBeforeItsLayoutFromAViewModelWithItsOwnRows() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 0);
                using ShownVirtual shown = new(model: model, buffer: 10);

                shown.WatchLayouts();

                model.Resize(total: 1);
                shown.Settle();

                await Assert.That(shown.TextsAtLayouts).IsNotEmpty();
                await Assert.That(shown.TextsAtLayouts).DoesNotContain(expected: PlaceholderText);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(expected: Rows(first: 0, count: 1), ordering: CollectionOrdering.Matching);
            });

    // The texts of a number of rows in sequence. A placeholder is never one of them. Thus, a test that
    // compares with these texts also checks that no placeholder shows.
    private static List<string> Rows(int first, int count) =>
        [.. Enumerable.Range(start: first, count: count).Select(selector: static key => $"Row {key}")];

    private static List<string> Texts(ShownVirtual shown) =>
        [.. shown.Shown().Select(selector: static pair => pair.Text)];
}
