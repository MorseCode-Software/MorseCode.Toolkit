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
///     Checks the list that a <see cref="VirtualizingItemsControl" /> gives to Avalonia. The tests
///     read the events that the list raises when the length changes, and the positions that it gives
///     when the rows change.
/// </summary>
public sealed class VirtualItemListTests
{
    [Test]
    public async Task APositionThatTheRowsCoverHoldsItsRowAndEveryOtherPositionHoldsAPlaceholder()
    {
        VirtualItemList items = Items(count: 10, rows: Rows(offset: 3, 3, 4));

        await Assert.That(items.Count).IsEqualTo(expected: 10);
        await Assert.That(items[2]).IsSameReferenceAs(expected: RowPlaceholder.Instance);
        await Assert.That(PositionAt(items: items, index: 3)).IsEqualTo(expected: 3);
        await Assert.That(PositionAt(items: items, index: 4)).IsEqualTo(expected: 4);
        await Assert.That(items[5]).IsSameReferenceAs(expected: RowPlaceholder.Instance);
    }

    [Test]
    public async Task RowsThatEndAfterTheListShowOnlyThePositionsOfTheList()
    {
        VirtualItemList items = Items(count: 4, rows: Rows(offset: 2, 2, 3, 4, 5));

        await Assert.That(items.Count).IsEqualTo(expected: 4);

        await Assert.That(items.Count(predicate: static item => item is VirtualRow))
            .IsEqualTo(expected: 2);
    }

    [Test]
    public async Task APositionOutsideTheListIsRefused()
    {
        VirtualItemList items = Items(count: 3, rows: VirtualRows.Empty);

        await Assert.That(() => items[3]).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AnUpdateThatChangesNothingRaisesNothing()
    {
        VirtualRows rows = Rows(offset: 1, 1, 2);
        VirtualItemList items = Items(count: 5, rows: rows);
        List<NotifyCollectionChangedEventArgs> events = Record(items: items);

        IEnumerable<ItemChange> changes = items.Update(newRows: rows with { TotalCount = 5 });

        await Assert.That(events).IsEmpty();
        await Assert.That(changes).IsEmpty();
    }

    [Test]
    public async Task RowsThatMoveRaiseNoEventAndGiveOnlyThePositionsThatChanged()
    {
        VirtualRow shared = new(Position: 2);

        VirtualItemList items =
            Items(
                count: 10,
                rows: new VirtualRows(TotalCount: 0, Offset: 1, Items: [new VirtualRow(Position: 1), shared]));

        List<NotifyCollectionChangedEventArgs> events = Record(items: items);

        List<ItemChange> changes =
        [
            .. items.Update(
                newRows: new VirtualRows(TotalCount: 10, Offset: 2, Items: [shared, new VirtualRow(Position: 3)]))
        ];

        // Position 1 loses its row, and position 3 gets one. Position 2 keeps the same row.
        await Assert.That(events).IsEmpty();

        await Assert.That(changes.Select(selector: static change => change.Index).ToList())
            .IsEquivalentTo(expected: [1, 3], ordering: CollectionOrdering.Matching);

        await Assert.That((changes[0].Before as VirtualRow)?.Position).IsEqualTo(expected: 1);
        await Assert.That(changes[0].After).IsSameReferenceAs(expected: RowPlaceholder.Instance);
        await Assert.That(changes[1].Before).IsSameReferenceAs(expected: RowPlaceholder.Instance);
        await Assert.That((changes[1].After as VirtualRow)?.Position).IsEqualTo(expected: 3);
    }

    // The list reads only the two sets of rows. This test reads each position of the list before and
    // after the update, and compares.
    [Test]
    public async Task TheChangedPositionsOfEachTwoSetsOfRowsAreThePositionsWhoseItemDiffers()
    {
        int[] counts = [0, 5, 20, 40];
        int[] offsets = [0, 1, 3, 8, 17, 25, 39, 45];
        int[] lengths = [0, 1, 4, 10];

        // The new rows from the same pool keep the row object of a position. The new rows from the
        // other pool have a different row object for it.
        VirtualRow[] pool = Pool();
        VirtualRow[] otherPool = Pool();

        List<(VirtualRows Old, VirtualRows New)> cases =
        [
            .. from oldCount in counts
            from newCount in counts
            from oldOffset in offsets
            from newOffset in offsets
            from oldLength in lengths
            from newLength in lengths
            from newPool in new[] { pool, otherPool }
            select (
                Window(pool: pool, count: oldCount, offset: oldOffset, length: oldLength),
                Window(pool: newPool, count: newCount, offset: newOffset, length: newLength))
        ];

        List<string> wrong =
        [
            .. cases.Where(predicate: static pair => !GivesThePositionsThatDiffer(oldRows: pair.Old, newRows: pair.New))
                .Select(
                    selector: static pair =>
                        $"{pair.Old.TotalCount}: {pair.Old.Offset}+{pair.Old.Items.Count} to "
                        + $"{pair.New.TotalCount}: {pair.New.Offset}+{pair.New.Items.Count}")
        ];

        await Assert.That(cases.Count).IsEqualTo(expected: 32768);
        await Assert.That(wrong).IsEmpty();
    }

    // A jump puts the new rows far from the old rows. Each position between them holds the
    // placeholder before and after, thus the list must not read it.
    [Test]
    public async Task AJumpToFarRowsReadsOnlyTheRows()
    {
        const int farOffset = 9_000_000;

        ReadCountingList near = new(items: [.. Pool().Take(count: 30)]);

        ReadCountingList far =
            new(
                items:
                [
                    .. Enumerable.Range(start: farOffset, count: 30)
                        .Select(selector: static position => new VirtualRow(Position: position))
                ]);

        VirtualItemList items =
            Items(count: 10_000_000, rows: new VirtualRows(TotalCount: 0, Offset: 0, Items: near));

        int readsBefore = near.Reads + far.Reads;

        List<ItemChange> changes =
            [.. items.Update(newRows: new VirtualRows(TotalCount: 10_000_000, Offset: farOffset, Items: far))];

        await Assert.That(changes.Select(selector: static change => change.Index).ToList())
            .IsEquivalentTo(
                expected: [.. Enumerable.Range(start: 0, count: 30), .. Enumerable.Range(start: farOffset, count: 30)],
                ordering: CollectionOrdering.Matching);

        // A walk of each position between the rows reads the two lists 9 million times.
        await Assert.That(near.Reads + far.Reads - readsBefore).IsLessThan(maximum: 1000);
    }

    [Test]
    public async Task AReplaceRaisesOneEventForThePosition()
    {
        VirtualItemList items = Items(count: 10, rows: Rows(offset: 1, 1, 2));
        List<NotifyCollectionChangedEventArgs> events = Record(items: items);
        VirtualRow before = new(Position: 7);

        items.Replace(change: new ItemChange(Index: 2, Before: before, After: items[2]));

        await Assert.That(events.Count).IsEqualTo(expected: 1);
        await Assert.That(events[0].Action).IsEqualTo(expected: NotifyCollectionChangedAction.Replace);
        await Assert.That(events[0].NewStartingIndex).IsEqualTo(expected: 2);
        await Assert.That(FirstNewItem(args: events[0])).IsSameReferenceAs(expected: items[2]);
        await Assert.That(events[0].OldItems?[0]).IsSameReferenceAs(expected: before);
    }

    [Test]
    public async Task AListThatGrowsAddsPlaceholdersAtTheEnd()
    {
        VirtualItemList items = Items(count: 3, rows: VirtualRows.Empty);
        List<NotifyCollectionChangedEventArgs> events = Record(items: items);

        items.Update(newRows: VirtualRows.Empty with { TotalCount = 5 });

        await Assert.That(events.Count).IsEqualTo(expected: 1);
        await Assert.That(events[0].Action).IsEqualTo(expected: NotifyCollectionChangedAction.Add);
        await Assert.That(events[0].NewStartingIndex).IsEqualTo(expected: 3);
        await Assert.That(events[0].NewItems?.Count).IsEqualTo(expected: 2);
        await Assert.That(items.Count).IsEqualTo(expected: 5);
    }

    [Test]
    public async Task AListThatShrinksRemovesTheEnd()
    {
        VirtualItemList items = Items(count: 5, rows: VirtualRows.Empty);
        List<NotifyCollectionChangedEventArgs> events = Record(items: items);

        items.Update(newRows: VirtualRows.Empty with { TotalCount = 2 });

        await Assert.That(events.Count).IsEqualTo(expected: 1);
        await Assert.That(events[0].Action).IsEqualTo(expected: NotifyCollectionChangedAction.Remove);
        await Assert.That(events[0].OldStartingIndex).IsEqualTo(expected: 2);
        await Assert.That(events[0].OldItems?.Count).IsEqualTo(expected: 3);
    }

    [Test]
    public async Task AListThatGrowsAndFillsItsRowsAddsTheTailAndGivesTheRest()
    {
        VirtualItemList items = Items(count: 2, rows: VirtualRows.Empty);
        List<NotifyCollectionChangedEventArgs> events = Record(items: items);

        List<ItemChange> changes = [.. items.Update(newRows: Rows(offset: 1, 1, 2, 3) with { TotalCount = 4 })];

        await Assert.That(events.Select(selector: static args => args.Action).ToList())
            .IsEquivalentTo(expected: [NotifyCollectionChangedAction.Add], ordering: CollectionOrdering.Matching);

        // Positions 2 and 3 are new and already hold rows. Position 1 was a placeholder.
        await Assert.That(changes.Select(selector: static change => change.Index).ToList())
            .IsEquivalentTo(expected: [1], ordering: CollectionOrdering.Matching);

        await Assert.That(PositionAt(items: items, index: 3)).IsEqualTo(expected: 3);
    }

    // The control shows a changed position in the container that it has, thus the number of changed
    // positions has no limit.
    [Test]
    public async Task AChangeToManyPositionsRaisesNoEvent()
    {
        VirtualItemList items = Items(count: 5000, rows: VirtualRows.Empty);
        List<NotifyCollectionChangedEventArgs> events = Record(items: items);

        List<ItemChange> changes =
        [
            .. items.Update(
                newRows: Rows(offset: 0, positions: [.. Enumerable.Range(start: 0, count: 1000)]) with
                {
                    TotalCount = 5000
                })
        ];

        await Assert.That(events).IsEmpty();
        await Assert.That(changes.Count).IsEqualTo(expected: 1000);
    }

    [Test]
    public async Task AResetGivesNoPositions()
    {
        VirtualItemList items = Items(count: 100, rows: Rows(offset: 0, 0, 1));
        List<NotifyCollectionChangedEventArgs> events = Record(items: items);

        IEnumerable<ItemChange> changes = items.Update(newRows: Rows(offset: 0, 0, 1) with { TotalCount = 5000 });

        await Assert.That(events.Count).IsEqualTo(expected: 1);
        await Assert.That(events[0].Action).IsEqualTo(expected: NotifyCollectionChangedAction.Reset);
        await Assert.That(changes).IsEmpty();
    }

    [Test]
    public async Task AListThatGrowsByManyPositionsRaisesOneReset()
    {
        VirtualItemList items = Items(count: 0, rows: VirtualRows.Empty);
        List<NotifyCollectionChangedEventArgs> events = Record(items: items);

        items.Update(newRows: VirtualRows.Empty with { TotalCount = 500_000 });

        await Assert.That(events.Count).IsEqualTo(expected: 1);
        await Assert.That(events[0].Action).IsEqualTo(expected: NotifyCollectionChangedAction.Reset);
        await Assert.That(items.Count).IsEqualTo(expected: 500_000);
    }

    [Test]
    public async Task AListThatShrinksByManyPositionsRaisesOneReset()
    {
        VirtualItemList items = Items(count: 500_000, rows: VirtualRows.Empty);
        List<NotifyCollectionChangedEventArgs> events = Record(items: items);

        items.Update(newRows: VirtualRows.Empty with { TotalCount = 10 });

        await Assert.That(events.Count).IsEqualTo(expected: 1);
        await Assert.That(events[0].Action).IsEqualTo(expected: NotifyCollectionChangedAction.Reset);
        await Assert.That(items.Count).IsEqualTo(expected: 10);
    }

    // The largest change that is still one event for the added positions.
    [Test]
    public async Task AListThatGrowsByTheLimitStillRaisesOneAdd()
    {
        VirtualItemList items = Items(count: 100, rows: VirtualRows.Empty);
        List<NotifyCollectionChangedEventArgs> events = Record(items: items);

        items.Update(newRows: VirtualRows.Empty with { TotalCount = 612 });

        await Assert.That(events.Count).IsEqualTo(expected: 1);
        await Assert.That(events[0].Action).IsEqualTo(expected: NotifyCollectionChangedAction.Add);
        await Assert.That(events[0].NewItems?.Count).IsEqualTo(expected: 512);
    }

    [Test]
    public async Task IndexOfFindsARowAndNeverAPlaceholder()
    {
        VirtualItemList items = Items(count: 10, rows: Rows(offset: 4, 4, 5));
        object? row = items[5];

        await Assert.That(items.IndexOf(value: row)).IsEqualTo(expected: 5);
        await Assert.That(items.IndexOf(value: RowPlaceholder.Instance)).IsEqualTo(expected: -1);
        await Assert.That(items.IndexOf(value: null)).IsEqualTo(expected: -1);
        await Assert.That(items.Contains(value: new VirtualRow(Position: 6))).IsFalse();
    }

    [Test]
    public async Task TheListRefusesEveryEdit()
    {
        IList items = Items(count: 3, rows: VirtualRows.Empty);

        await Assert.That(() => items.Add(value: new object())).Throws<NotSupportedException>();
        await Assert.That(items.Clear).Throws<NotSupportedException>();
        await Assert.That(() => items.Insert(index: 0, value: new object())).Throws<NotSupportedException>();
        await Assert.That(() => items.Remove(value: new object())).Throws<NotSupportedException>();
        await Assert.That(() => items.RemoveAt(index: 0)).Throws<NotSupportedException>();
        await Assert.That(() => items[0] = new object()).Throws<NotSupportedException>();
    }

    private static VirtualRows Rows(int offset, params int[] positions) =>
        new(
            TotalCount: 0,
            Offset: offset,
            Items: [.. positions.Select(selector: static position => new VirtualRow(Position: position))]);

    private static int? PositionAt(VirtualItemList items, int index) => (items[index] as VirtualRow)?.Position;

    private static object? FirstNewItem(NotifyCollectionChangedEventArgs args) => args.NewItems?[0];

    private static VirtualItemList Items(int count, VirtualRows rows)
    {
        VirtualItemList items = new();
        items.Update(newRows: rows with { TotalCount = count });

        return items;
    }

    private static List<NotifyCollectionChangedEventArgs> Record(INotifyCollectionChanged items)
    {
        List<NotifyCollectionChangedEventArgs> events = [];
        items.CollectionChanged += (_, args) => events.Add(item: args);

        return events;
    }

    private static VirtualRow[] Pool() =>
    [
        .. Enumerable.Range(start: 0, count: 60)
            .Select(selector: static position => new VirtualRow(Position: position))
    ];

    private static VirtualRows Window(IEnumerable<VirtualRow> pool, int count, int offset, int length) =>
        new(TotalCount: count, Offset: offset, Items: [.. pool.Skip(count: offset).Take(count: length)]);

    // Reads each position of the list before and after the update. A row is a record, thus the test
    // compares the items as references.
    private static bool GivesThePositionsThatDiffer(VirtualRows oldRows, VirtualRows newRows)
    {
        VirtualItemList items = new();
        items.Update(newRows: oldRows);

        object?[] before = [.. items];
        List<ItemChange> given = [.. items.Update(newRows: newRows)];
        object?[] after = [.. items];

        List<int> expected =
        [
            .. Enumerable.Range(start: 0, count: Math.Min(val1: before.Length, val2: after.Length))
                .Where(predicate: index => !ReferenceEquals(objA: before[index], objB: after[index]))
        ];

        return given.Select(selector: static change => change.Index).SequenceEqual(second: expected)
            && given.All(
                predicate: change =>
                    ReferenceEquals(objA: change.Before, objB: before[change.Index])
                    && ReferenceEquals(objA: change.After, objB: after[change.Index]));
    }

    // Counts each read of the count and of an item.
    // ReSharper disable once InheritdocConsiderUsage - The summary of IReadOnlyList does not say what this list counts.
    private sealed class ReadCountingList(in IReadOnlyList<object?> items) : IReadOnlyList<object?>
    {
        private readonly IReadOnlyList<object?> items = items;

        public int Reads { get; private set; }

        public int Count
        {
            get
            {
                this.Reads++;

                return this.items.Count;
            }
        }

        public object? this[int index]
        {
            get
            {
                this.Reads++;

                return this.items[index: index];
            }
        }

        public IEnumerator<object?> GetEnumerator() => this.items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
