using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using SodaFlow.Functional;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     Checks that a <see cref="VirtualizingItemsControl" /> asks for rows only at the positions that show,
///     and that those rows replace the placeholders.
/// </summary>
public sealed class VirtualizingItemsControlTests
{
    private const string Placeholder = "placeholder";

    private static readonly int[] Source = [1, 2, 3];

    [Test]
    public Task AFirstLayoutBuildsOnlyTheRowsThatShow() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                using ShownVirtual shown = new(model: model, buffer: 0);

                await Assert.That(model.Built)
                    .IsEquivalentTo(
                        expected: [.. Enumerable.Range(start: 0, count: 10)],
                        ordering: CollectionOrdering.Matching);

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(
                        expected: [.. Enumerable.Range(start: 0, count: 10).Select(static i => $"Row {i}")],
                        ordering: CollectionOrdering.Matching);
            });

    [Test]
    public Task TheBufferWidensTheRangeThatTheViewReports() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                using ShownVirtual shown = new(model: model, buffer: 5);

                await Assert.That(model.Writes.Last()).IsEqualTo(expected: new ItemRange(First: 0, Count: 15));
                await Assert.That(model.Built.Count).IsEqualTo(expected: 15);
                await Assert.That(shown.List.VisibleRange).IsEqualTo(expected: new ItemRange(First: 0, Count: 15));
            });

    [Test]
    public Task TheViewReportsOneRangeForOneLayout() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                using ShownVirtual shown = new(model: model, buffer: 0);

                shown.Settle();

                await Assert.That(model.Writes.Count).IsEqualTo(expected: 1);
            });

    [Test]
    public Task AnAnswerThatIsLateShowsPlaceholdersAtTheSameHeightUntilItComes() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                model.Hold();
                using ShownVirtual shown = new(model: model, buffer: 0);

                await Assert.That(Texts(shown: shown).Distinct().ToList())
                    .IsEquivalentTo(expected: [Placeholder], ordering: CollectionOrdering.Matching);

                // A placeholder is as high as a row, thus the control did not ask for more than a screen.
                await Assert.That(Texts(shown: shown).Count).IsEqualTo(expected: 10);
                await Assert.That(model.Built).IsEmpty();

                model.Release();
                shown.Settle();

                await Assert.That(Texts(shown: shown)[0]).IsEqualTo(expected: "Row 0");
                await Assert.That(Texts(shown: shown).Any(predicate: static text => text == Placeholder)).IsFalse();
            });

    [Test]
    public Task TheScrollAreaIsAsLongAsTheWholeList() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                using ShownVirtual shown = new(model: model, buffer: 0);

                await Assert.That(shown.Scroller.Extent.Height).IsEqualTo(expected: 20000);
            });

    [Test]
    public Task AScrollBuildsTheRowsThatItReachesAndNoRowBetween() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                using ShownVirtual shown = new(model: model, buffer: 0);

                shown.Scroller.Offset = new Vector(x: 0, y: 10000);
                shown.Settle();

                await Assert.That(Texts(shown: shown)[0]).IsEqualTo(expected: "Row 500");
                await Assert.That(Texts(shown: shown).Any(predicate: static text => text == Placeholder)).IsFalse();

                // The first screen and the screen at 500 are the only ones that anything built.
                await Assert.That(model.Built.Count).IsEqualTo(expected: 20);
                await Assert.That(model.Built.Any(predicate: static position => position is > 9 and < 500)).IsFalse();
            });

    [Test]
    public Task AScrollBackFindsTheRowsThatTheModelKept() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                using ShownVirtual shown = new(model: model, buffer: 0);

                shown.Scroller.Offset = new Vector(x: 0, y: 10000);
                shown.Settle();
                shown.Scroller.Offset = new Vector(x: 0, y: 0);
                shown.Settle();

                await Assert.That(Texts(shown: shown)[0]).IsEqualTo(expected: "Row 0");
                await Assert.That(model.Built.Count).IsEqualTo(expected: 20);
            });

    [Test]
    public Task ScrollIntoViewShowsARowThatNothingBuiltYet() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                using ShownVirtual shown = new(model: model, buffer: 0);

                shown.List.ScrollIntoView(index: 700);
                shown.Settle();

                await Assert.That(Texts(shown: shown).Any(predicate: static text => text == "Row 700")).IsTrue();
                await Assert.That(model.Built.Any(predicate: static position => position is > 9 and < 690)).IsFalse();
            });

    [Test]
    public Task AListThatShrinksShowsOnlyThePositionsThatAreLeft() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                using ShownVirtual shown = new(model: model, buffer: 0);

                model.Resize(total: 4);
                shown.Settle();

                await Assert.That(Texts(shown: shown))
                    .IsEquivalentTo(
                        expected: ["Row 0", "Row 1", "Row 2", "Row 3"],
                        ordering: CollectionOrdering.Matching);

                // The extent of a scroll viewer is never less than its viewport, thus the panel gives the height.
                await Assert.That(shown.List.ItemsPanelRoot?.DesiredSize.Height).IsEqualTo(expected: 80);
            });

    [Test]
    public Task AListThatGrowsLengthensTheScrollAreaAndBuildsNothingNew() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                using ShownVirtual shown = new(model: model, buffer: 0);
                int built = model.Built.Count;

                model.Resize(total: 3000);
                shown.Settle();

                await Assert.That(shown.Scroller.Extent.Height).IsEqualTo(expected: 60000);
                await Assert.That(model.Built.Count).IsEqualTo(expected: built);
            });

    [Test]
    public Task AnEmptyListShowsNothingAndReportsAnEmptyRange() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 0);
                using ShownVirtual shown = new(model: model, buffer: 3);

                await Assert.That(Texts(shown: shown)).IsEmpty();
                await Assert.That(model.Built).IsEmpty();
            });

    [Test]
    public Task AnItemsSourceThatTheCallerSetsIsRefused() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualizingItemsControl list = new();

                await Assert.That(() => list.ItemsSource = Source).Throws<InvalidOperationException>();
            });

    // The view model made the request before the control was bound. The control keeps nothing of its
    // own. The binding gives it the request when it binds, and it scrolls at the Loaded event.
    [Test]
    public Task ARequestMadeBeforeTheControlIsBoundScrollsAndIsCleared() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                model.Request(position: 700);
                using ShownVirtual shown = new(model: model, buffer: 0);

                await Assert.That(Texts(shown: shown).Any(predicate: static text => text == "Row 700")).IsTrue();
                await Assert.That(model.ScrollRequest).IsEqualTo(expected: Maybe<int>.None);

                await Assert.That(model.ScrollWrites)
                    .IsEquivalentTo(expected: [Maybe<int>.None], ordering: CollectionOrdering.Matching);
            });

    [Test]
    public Task ARequestMadeAfterTheControlIsShownScrollsAndIsCleared() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                using ShownVirtual shown = new(model: model, buffer: 0);

                model.Request(position: 700);
                shown.Settle();

                await Assert.That(Texts(shown: shown).Any(predicate: static text => text == "Row 700")).IsTrue();
                await Assert.That(model.ScrollRequest).IsEqualTo(expected: Maybe<int>.None);

                await Assert.That(model.ScrollWrites)
                    .IsEquivalentTo(expected: [Maybe<int>.None], ordering: CollectionOrdering.Matching);

                await Assert.That(model.Built.Any(predicate: static position => position is > 9 and < 690)).IsFalse();
            });

    // A request for a position that the list does not have yet is not lost. It waits for the count.
    [Test]
    public Task ARequestBeyondTheCountWaitsForTheCount() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 100);
                using ShownVirtual shown = new(model: model, buffer: 0);

                model.Request(position: 500);
                shown.Settle();

                await Assert.That(Texts(shown: shown)[0]).IsEqualTo(expected: "Row 0");
                await Assert.That(model.ScrollRequest).IsEqualTo(expected: Maybe<int>.Some(value: 500));
                await Assert.That(model.ScrollWrites).IsEmpty();

                model.Resize(total: 1000);
                shown.Settle();

                await Assert.That(Texts(shown: shown).Any(predicate: static text => text == "Row 500")).IsTrue();
                await Assert.That(model.ScrollRequest).IsEqualTo(expected: Maybe<int>.None);
            });

    // A negative position can never apply, thus the control clears it and does not scroll.
    [Test]
    public Task ANegativeRequestIsClearedWithoutScrolling() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                using ShownVirtual shown = new(model: model, buffer: 0);

                model.Request(position: -1);
                shown.Settle();

                await Assert.That(Texts(shown: shown)[0]).IsEqualTo(expected: "Row 0");
                await Assert.That(model.ScrollRequest).IsEqualTo(expected: Maybe<int>.None);
            });

    // The view model keeps the last request. Two requests before the dispatcher runs give one scroll,
    // to the second position.
    [Test]
    public Task ALaterRequestReplacesOneThatWaits() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                using ShownVirtual shown = new(model: model, buffer: 0);

                model.Request(position: 300);
                model.Request(position: 700);
                shown.Settle();

                await Assert.That(Texts(shown: shown).Any(predicate: static text => text == "Row 700")).IsTrue();
                await Assert.That(model.Built.Any(predicate: static position => position is > 9 and < 690)).IsFalse();
                await Assert.That(model.ScrollRequest).IsEqualTo(expected: Maybe<int>.None);
            });

    // The reset makes the same request possible again.
    [Test]
    public Task TheSameRequestScrollsAgainAfterTheControlClearedIt() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                using ShownVirtual shown = new(model: model, buffer: 0);

                model.Request(position: 700);
                shown.Settle();
                shown.Scroller.Offset = new Vector(x: 0, y: 0);
                shown.Settle();

                await Assert.That(Texts(shown: shown)[0]).IsEqualTo(expected: "Row 0");

                model.Request(position: 700);
                shown.Settle();

                await Assert.That(Texts(shown: shown).Any(predicate: static text => text == "Row 700")).IsTrue();
                await Assert.That(model.ScrollWrites.Count).IsEqualTo(expected: 2);
            });

    private static List<string> Texts(ShownVirtual shown) =>
    [
        .. shown.Shown().Select(selector: static pair => pair.Text)
    ];
}
