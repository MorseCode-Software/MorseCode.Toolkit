using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     Checks the list that a <see cref="VirtualizingItemsControl" /> gives to Avalonia, and the events
///     that it raises when the length or the window changes.
/// </summary>
public sealed class WindowedItemsTests
{
    [Test]
    public async Task APositionInTheWindowHoldsItsRowAndEveryOtherPositionHoldsAPlaceholder()
    {
        WindowedItems items = Items(count: 10, window: Window(offset: 3, 3, 4));

        await Assert.That(items.Count).IsEqualTo(expected: 10);
        await Assert.That(items[2]).IsSameReferenceAs(expected: RowPlaceholder.Instance);
        await Assert.That(PositionAt(items: items, index: 3)).IsEqualTo(expected: 3);
        await Assert.That(PositionAt(items: items, index: 4)).IsEqualTo(expected: 4);
        await Assert.That(items[5]).IsSameReferenceAs(expected: RowPlaceholder.Instance);
    }

    [Test]
    public async Task AWindowThatEndsAfterTheListShowsOnlyThePositionsOfTheList()
    {
        WindowedItems items = Items(count: 4, window: Window(offset: 2, 2, 3, 4, 5));

        await Assert.That(items.Count).IsEqualTo(expected: 4);

        await Assert.That(items.Count(predicate: static item => item is VirtualRow))
            .IsEqualTo(expected: 2);
    }

    [Test]
    public async Task APositionOutsideTheListIsRefused()
    {
        WindowedItems items = Items(count: 3, window: VirtualWindow.Empty);

        await Assert.That(() => items[3]).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AnUpdateThatChangesNothingRaisesNothing()
    {
        VirtualWindow window = Window(offset: 1, 1, 2);
        WindowedItems items = Items(count: 5, window: window);
        List<NotifyCollectionChangedEventArgs> events = Record(items: items);

        items.Update(newCount: 5, newWindow: window);

        await Assert.That(events).IsEmpty();
    }

    [Test]
    public async Task AWindowThatMovesReplacesOnlyThePositionsThatChanged()
    {
        VirtualRow shared = new(Position: 2);

        WindowedItems items =
            Items(
                count: 10,
                window: new VirtualWindow(Offset: 1, Items: [new VirtualRow(Position: 1), shared]));

        List<NotifyCollectionChangedEventArgs> events = Record(items: items);

        items.Update(
            newCount: 10,
            newWindow: new VirtualWindow(Offset: 2, Items: [shared, new VirtualRow(Position: 3)]));

        // Position 1 loses its row, and position 3 gets one. Position 2 keeps the same row.
        await Assert.That(events.Select(selector: static args => args.Action).ToList())
            .IsEquivalentTo(
                expected: [NotifyCollectionChangedAction.Replace, NotifyCollectionChangedAction.Replace],
                ordering: CollectionOrdering.Matching);

        await Assert.That(events.Select(selector: static args => args.NewStartingIndex).ToList())
            .IsEquivalentTo(expected: [1, 3], ordering: CollectionOrdering.Matching);

        await Assert.That(FirstNewItem(args: events[0])).IsSameReferenceAs(expected: RowPlaceholder.Instance);
        await Assert.That((FirstNewItem(args: events[1]) as VirtualRow)?.Position).IsEqualTo(expected: 3);
    }

    [Test]
    public async Task AListThatGrowsAddsPlaceholdersAtTheEnd()
    {
        WindowedItems items = Items(count: 3, window: VirtualWindow.Empty);
        List<NotifyCollectionChangedEventArgs> events = Record(items: items);

        items.Update(newCount: 5, newWindow: VirtualWindow.Empty);

        await Assert.That(events.Count).IsEqualTo(expected: 1);
        await Assert.That(events[0].Action).IsEqualTo(expected: NotifyCollectionChangedAction.Add);
        await Assert.That(events[0].NewStartingIndex).IsEqualTo(expected: 3);
        await Assert.That(events[0].NewItems?.Count).IsEqualTo(expected: 2);
        await Assert.That(items.Count).IsEqualTo(expected: 5);
    }

    [Test]
    public async Task AListThatShrinksRemovesTheEnd()
    {
        WindowedItems items = Items(count: 5, window: VirtualWindow.Empty);
        List<NotifyCollectionChangedEventArgs> events = Record(items: items);

        items.Update(newCount: 2, newWindow: VirtualWindow.Empty);

        await Assert.That(events.Count).IsEqualTo(expected: 1);
        await Assert.That(events[0].Action).IsEqualTo(expected: NotifyCollectionChangedAction.Remove);
        await Assert.That(events[0].OldStartingIndex).IsEqualTo(expected: 2);
        await Assert.That(events[0].OldItems?.Count).IsEqualTo(expected: 3);
    }

    [Test]
    public async Task AListThatGrowsAndFillsItsWindowAddsTheTailAndReplacesTheRest()
    {
        WindowedItems items = Items(count: 2, window: VirtualWindow.Empty);
        List<NotifyCollectionChangedEventArgs> events = Record(items: items);

        items.Update(newCount: 4, newWindow: Window(offset: 1, 1, 2, 3));

        await Assert.That(events.Select(selector: static args => args.Action).ToList())
            .IsEquivalentTo(
                expected: [NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Replace],
                ordering: CollectionOrdering.Matching);

        // Positions 2 and 3 are new and already hold rows. Position 1 was a placeholder.
        await Assert.That(events[1].NewStartingIndex).IsEqualTo(expected: 1);
        await Assert.That(PositionAt(items: items, index: 3)).IsEqualTo(expected: 3);
    }

    [Test]
    public async Task AChangeToManyPositionsRaisesOneReset()
    {
        WindowedItems items = Items(count: 5000, window: VirtualWindow.Empty);
        List<NotifyCollectionChangedEventArgs> events = Record(items: items);

        items.Update(
            newCount: 5000,
            newWindow: Window(offset: 0, positions: [.. Enumerable.Range(start: 0, count: 1000)]));

        await Assert.That(events.Count).IsEqualTo(expected: 1);
        await Assert.That(events[0].Action).IsEqualTo(expected: NotifyCollectionChangedAction.Reset);
    }

    [Test]
    public async Task IndexOfFindsARowAndNeverAPlaceholder()
    {
        WindowedItems items = Items(count: 10, window: Window(offset: 4, 4, 5));
        object? row = items[5];

        await Assert.That(items.IndexOf(value: row)).IsEqualTo(expected: 5);
        await Assert.That(items.IndexOf(value: RowPlaceholder.Instance)).IsEqualTo(expected: -1);
        await Assert.That(items.IndexOf(value: null)).IsEqualTo(expected: -1);
        await Assert.That(items.Contains(value: new VirtualRow(Position: 6))).IsFalse();
    }

    [Test]
    public async Task TheListRefusesEveryEdit()
    {
        IList items = Items(count: 3, window: VirtualWindow.Empty);

        await Assert.That(() => items.Add(value: new object())).Throws<NotSupportedException>();
        await Assert.That(items.Clear).Throws<NotSupportedException>();
        await Assert.That(() => items.Insert(index: 0, value: new object())).Throws<NotSupportedException>();
        await Assert.That(() => items.Remove(value: new object())).Throws<NotSupportedException>();
        await Assert.That(() => items.RemoveAt(index: 0)).Throws<NotSupportedException>();
        await Assert.That(() => items[0] = new object()).Throws<NotSupportedException>();
    }

    private static VirtualWindow Window(int offset, params int[] positions) =>
        new(
            Offset: offset,
            Items: [.. positions.Select(selector: static position => new VirtualRow(Position: position))]);

    private static int? PositionAt(WindowedItems items, int index) => (items[index] as VirtualRow)?.Position;

    private static object? FirstNewItem(NotifyCollectionChangedEventArgs args) => args.NewItems?[0];

    private static WindowedItems Items(int count, VirtualWindow window)
    {
        WindowedItems items = new();
        items.Update(newCount: count, newWindow: window);

        return items;
    }

    private static List<NotifyCollectionChangedEventArgs> Record(INotifyCollectionChanged items)
    {
        List<NotifyCollectionChangedEventArgs> events = [];
        items.CollectionChanged += (_, args) => events.Add(item: args);

        return events;
    }
}
