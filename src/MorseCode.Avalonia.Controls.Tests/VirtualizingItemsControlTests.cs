using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
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
                await Assert.That(model.CurrentRequest).IsEqualTo(expected: Maybe<int>.None);

                await Assert.That(model.ScrollWrites)
                    .IsEquivalentTo(
                        expected: [Maybe<int>.Some(value: 700), Maybe<int>.None],
                        ordering: CollectionOrdering.Matching);
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
                await Assert.That(model.CurrentRequest).IsEqualTo(expected: Maybe<int>.None);

                await Assert.That(model.ScrollWrites)
                    .IsEquivalentTo(
                        expected: [Maybe<int>.Some(value: 700), Maybe<int>.None],
                        ordering: CollectionOrdering.Matching);

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
                await Assert.That(model.CurrentRequest).IsEqualTo(expected: Maybe<int>.Some(value: 500));
                await Assert.That(model.ScrollWrites).IsEmpty();

                model.Resize(total: 1000);
                shown.Settle();

                await Assert.That(Texts(shown: shown).Any(predicate: static text => text == "Row 500")).IsTrue();
                await Assert.That(model.CurrentRequest).IsEqualTo(expected: Maybe<int>.None);
            });

    // The request and the count change in one turn of the dispatcher, before the posted scroll runs. The
    // second change finds the scroll already posted, and the scroll reads both values when it runs.
    [Test]
    public Task ARequestAndACountThatChangeInOneTurnScrollOnce() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 100);
                using ShownVirtual shown = new(model: model, buffer: 0);

                model.Request(position: 500);
                model.Resize(total: 1000);
                shown.Settle();

                await Assert.That(Texts(shown: shown).Any(predicate: static text => text == "Row 500")).IsTrue();
                await Assert.That(model.CurrentRequest).IsEqualTo(expected: Maybe<int>.None);

                await Assert.That(model.ScrollWrites)
                    .IsEquivalentTo(
                        expected: [Maybe<int>.Some(value: 500), Maybe<int>.None],
                        ordering: CollectionOrdering.Matching);
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
                await Assert.That(model.CurrentRequest).IsEqualTo(expected: Maybe<int>.None);
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
                await Assert.That(model.CurrentRequest).IsEqualTo(expected: Maybe<int>.None);
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
                await Assert.That(model.ScrollWrites.Count).IsEqualTo(expected: 4);
            });

    // The view model clears the request when the report reaches it, and that can be later than a change of
    // the count. The control must not scroll again to a request that it already handled.
    [Test]
    public Task ARequestThatTheViewModelHasNotClearedYetDoesNotScrollAgainWhenTheCountChanges() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000) { KeepsHandledRequest = true };
                using ShownVirtual shown = new(model: model, buffer: 0);

                model.Request(position: 700);
                shown.Settle();
                shown.Scroller.Offset = new Vector(x: 0, y: 0);
                shown.Settle();

                model.Resize(total: 1001);
                shown.Settle();

                await Assert.That(Texts(shown: shown)[0]).IsEqualTo(expected: "Row 0");

                await Assert.That(model.ScrollWrites)
                    .IsEquivalentTo(
                        expected: [Maybe<int>.Some(value: 700), Maybe<int>.None],
                        ordering: CollectionOrdering.Matching);
            });

    // A request that replaces a handled one, before the view model clears the old one, is a new request.
    [Test]
    public Task ANewRequestAfterAHandledOneScrollsEvenWhileTheOldOneIsNotCleared() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000) { KeepsHandledRequest = true };
                using ShownVirtual shown = new(model: model, buffer: 0);

                model.Request(position: 300);
                shown.Settle();
                model.Request(position: 700);
                shown.Settle();

                await Assert.That(Texts(shown: shown).Any(predicate: static text => text == "Row 700")).IsTrue();

                await Assert.That(model.ScrollWrites)
                    .IsEquivalentTo(
                        expected:
                        [
                            Maybe<int>.Some(value: 300), Maybe<int>.None, Maybe<int>.Some(value: 700),
                            Maybe<int>.None
                        ],
                        ordering: CollectionOrdering.Matching);
            });

    // The control reads the count from its source. A new source replaces the old one, and the old one
    // no longer reaches the control. A control with no source shows nothing.
    [Test]
    public Task ANewSourceReplacesTheOldOneAndNoSourceShowsNothing() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel first = new(total: 1000);
                VirtualModel second = new(total: 50);
                using ShownVirtual shown = new(model: first, buffer: 0);
                VirtualizingItemsControl list = shown.List;

                await Assert.That(list.Rows.TotalCount).IsEqualTo(expected: 1000);

                list.Source = second;

                await Assert.That(list.Rows.TotalCount).IsEqualTo(expected: 50);

                first.Resize(total: 2000);

                await Assert.That(list.Rows.TotalCount).IsEqualTo(expected: 50);

                second.Resize(total: 60);

                await Assert.That(list.Rows.TotalCount).IsEqualTo(expected: 60);

                list.Source = null;
                second.Resize(total: 70);

                await Assert.That(list.Rows.TotalCount).IsEqualTo(expected: 0);
                await Assert.That(list.ScrollRequest).IsEqualTo(expected: Maybe<int>.None);
            });

    // A view model can outlive its view: a tab closes, a page navigates away, and the view model stays.
    // The control follows its source only while it is in a visual tree. A control that left the tree
    // ignores the source, and reads it again when it returns.
    [Test]
    public Task AControlThatLeftTheTreeDoesNotFollowItsSourceUntilItReturns() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                using ShownVirtual shown = new(model: model, buffer: 0);

                shown.Remove();
                model.Resize(total: 2000);
                model.Request(position: 700);
                shown.Settle();

                await Assert.That(shown.List.Rows.TotalCount).IsEqualTo(expected: 1000);
                await Assert.That(shown.List.ScrollRequest).IsEqualTo(expected: Maybe<int>.None);
                await Assert.That(model.ScrollWrites).IsEmpty();

                shown.Return();

                await Assert.That(shown.List.Rows.TotalCount).IsEqualTo(expected: 2000);
                await Assert.That(Texts(shown: shown).Any(predicate: static text => text == "Row 700")).IsTrue();
                await Assert.That(model.CurrentRequest).IsEqualTo(expected: Maybe<int>.None);
            });

    // The handlers on the values of the source are the only references from the source to the control.
    // A control that left the tree removes them. Otherwise, a view model that outlives its view keeps
    // every control that was bound to it, and the rows that each of them holds.
    [Test]
    public Task AControlThatLeftTheTreeIsNotKeptAliveByItsSource() =>
        TestApplication.Run(
            test: static async () =>
            {
                VirtualModel model = new(total: 1000);
                WeakReference<VirtualizingItemsControl> control = ShowAndRemove(model: model);

                // A job of the dispatcher that the window left behind can hold the control for one more
                // turn. A leak holds it for all of them.
                for (int attempt = 0; attempt < 50 && IsAlive(control: control); attempt++)
                {
                    await Task.Delay(millisecondsDelay: 10);
                    Dispatcher.UIThread.RunJobs();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }

                await Assert.That(IsAlive(control: control)).IsFalse();

                // The source must be alive while the collector runs, because the source is what could
                // keep the control.
                GC.KeepAlive(obj: model);
            });

    // The control and the window must not stay on the stack of the test. Thus, a separate method makes
    // them, and the frame of the test does not hold them.
    [MethodImpl(methodImplOptions: MethodImplOptions.NoInlining)]
    private static WeakReference<VirtualizingItemsControl> ShowAndRemove(IVirtualItemsSource model)
    {
        ShownVirtual shown = new(model: model, buffer: 0);
        WeakReference<VirtualizingItemsControl> control = new(target: shown.List);

        shown.Remove();
        shown.Dispose();

        // A report or a scroll that the control posted holds the control until the dispatcher runs it.
        Dispatcher.UIThread.RunJobs();

        return control;
    }

    // TryGetTarget gives a strong reference. In the frame of the test, that reference stays in a slot of
    // the stack until the method returns, and the collector sees it. In a frame of its own, it is gone
    // when the call returns.
    [MethodImpl(methodImplOptions: MethodImplOptions.NoInlining)]
    private static bool IsAlive(WeakReference<VirtualizingItemsControl> control) =>
        control.TryGetTarget(target: out _);

    private static List<string> Texts(ShownVirtual shown) =>
    [
        .. shown.Shown().Select(selector: static pair => pair.Text)
    ];
}
