using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     The list that a <see cref="VirtualizingItemsControl" /> gives to Avalonia. It
///     has one position for each item of the whole list. A position that the rows
///     cover holds one of the rows. Each other position holds a
///     <see cref="RowPlaceholder" />.
/// </summary>
/// <remarks>
///     This list builds nothing and keeps nothing but the rows. Thus, a list of a
///     million positions costs no more than the rows.
/// </remarks>
// ReSharper disable once InheritdocConsiderUsage - The summary of IList does not say what this list holds.
internal sealed class VirtualItemList : IList, IReadOnlyList<object?>, INotifyCollectionChanged
{
    // More changed positions than this are cheaper for the panel as a reset than as one event for each
    // position. A reset also clears the containers, thus the limit is well above any usual number of rows.
    private const int ResetThreshold = 512;

    private VirtualRows rows = VirtualRows.Empty;

    /// <inheritdoc />
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <inheritdoc cref="IReadOnlyCollection{T}.Count" />
    public int Count { get; private set; }

    /// <inheritdoc />
    public bool IsReadOnly => true;

    /// <inheritdoc />
    public bool IsFixedSize => false;

    /// <inheritdoc />
    public bool IsSynchronized => false;

    /// <inheritdoc />
    public object SyncRoot => this;

    /// <inheritdoc cref="IList.this[int]" />
    public object? this[int index]
    {
        get
        {
            if (index < 0 || index >= this.Count)
            {
                throw new ArgumentOutOfRangeException(paramName: nameof(index));
            }

            return ItemAt(rows: this.rows, index: index);
        }
        set => throw new NotSupportedException();
    }

    /// <summary>
    ///     Changes the length of the list and the rows, and tells the listeners
    ///     which positions changed.
    /// </summary>
    /// <param name="newRows">
    ///     The rows, with the number of positions in the whole list. A negative number counts as zero.
    /// </param>
    public void Update(VirtualRows newRows)
    {
        int newCount = Math.Max(val1: 0, val2: newRows.TotalCount);
        int oldCount = this.Count;
        VirtualRows oldRows = this.rows;

        this.Count = newCount;
        this.rows = newRows;

        // The events for a long run of added or removed positions would hold one item for each of them.
        // That costs memory in proportion to the change, thus a large change is a reset.
        if (Math.Abs(value: newCount - oldCount) > ResetThreshold)
        {
            this.Raise(args: new NotifyCollectionChangedEventArgs(action: NotifyCollectionChangedAction.Reset));

            return;
        }

        // A listener that reads the list while it handles an event sees the final state. Each event is
        // still correct for its own positions, because it names a position and the item that is there.
        List<int> changed =
            ChangedPositions(
                oldRows: oldRows,
                oldCount: oldCount,
                newRows: newRows,
                newCount: newCount);

        if (changed.Count > ResetThreshold)
        {
            this.Raise(args: new NotifyCollectionChangedEventArgs(action: NotifyCollectionChangedAction.Reset));

            return;
        }

        if (newCount > oldCount)
        {
            List<object?> added = [];

            for (int index = oldCount; index < newCount; index++)
            {
                added.Add(item: ItemAt(rows: newRows, index: index));
            }

            this.Raise(
                args: new NotifyCollectionChangedEventArgs(
                    action: NotifyCollectionChangedAction.Add,
                    changedItems: added,
                    startingIndex: oldCount));
        }
        else if (newCount < oldCount)
        {
            List<object?> removed = [];

            for (int index = newCount; index < oldCount; index++)
            {
                removed.Add(item: ItemAt(rows: oldRows, index: index));
            }

            this.Raise(
                args: new NotifyCollectionChangedEventArgs(
                    action: NotifyCollectionChangedAction.Remove,
                    changedItems: removed,
                    startingIndex: newCount));
        }

        foreach (int index in changed)
        {
            this.Raise(
                args: new NotifyCollectionChangedEventArgs(
                    action: NotifyCollectionChangedAction.Replace,
                    newItem: ItemAt(rows: newRows, index: index),
                    oldItem: ItemAt(rows: oldRows, index: index),
                    index: index));
        }
    }

    /// <inheritdoc />
    public IEnumerator GetEnumerator() => this.Enumerate().GetEnumerator();

    /// <inheritdoc />
    IEnumerator<object?> IEnumerable<object?>.GetEnumerator() => this.Enumerate().GetEnumerator();

    /// <inheritdoc />
    public bool Contains(object? value) => this.IndexOf(value: value) >= 0;

    /// <inheritdoc />
    /// <remarks>
    ///     A placeholder has no index of its own, because it stands for many positions.
    /// </remarks>
    public int IndexOf(object? value)
    {
        if (value is null or RowPlaceholder)
        {
            return -1;
        }

        for (int i = 0; i < this.rows.Items.Count; i++)
        {
            int index = this.rows.Offset + i;

            if (index < this.Count && Equals(objA: this.rows.Items[index: i], objB: value))
            {
                return index;
            }
        }

        return -1;
    }

    /// <inheritdoc />
    public void CopyTo(Array array, int index)
    {
        foreach (object? item in this.Enumerate())
        {
            array.SetValue(value: item, index: index++);
        }
    }

    /// <inheritdoc />
    int IList.Add(object? value) => throw new NotSupportedException();

    /// <inheritdoc />
    void IList.Clear() => throw new NotSupportedException();

    /// <inheritdoc />
    void IList.Insert(int index, object? value) => throw new NotSupportedException();

    /// <inheritdoc />
    void IList.Remove(object? value) => throw new NotSupportedException();

    /// <inheritdoc />
    void IList.RemoveAt(int index) => throw new NotSupportedException();

    // The caller makes sure that the index is inside the list.
    private static object? ItemAt(VirtualRows rows, int index)
    {
        int inRows = index - rows.Offset;

        return inRows >= 0 && inRows < rows.Items.Count ? rows.Items[index: inRows] : RowPlaceholder.Instance;
    }

    // The positions that were in the list before and after, and that hold a different item now. Only
    // the two sets of rows can differ, because every other position is a placeholder in both.
    private static List<int> ChangedPositions(
        VirtualRows oldRows,
        int oldCount,
        VirtualRows newRows,
        int newCount)
    {
        int shared = Math.Min(val1: oldCount, val2: newCount);
        int from = Math.Max(val1: 0, val2: Math.Min(val1: oldRows.Offset, val2: newRows.Offset));

        int to =
            Math.Min(
                val1: shared,
                val2: Math.Max(
                    val1: oldRows.Offset + oldRows.Items.Count,
                    val2: newRows.Offset + newRows.Items.Count));

        List<int> changed = [];

        for (int index = from; index < to; index++)
        {
            object? before = ItemAt(rows: oldRows, index: index);
            object? after = ItemAt(rows: newRows, index: index);

            if (!ReferenceEquals(objA: before, objB: after))
            {
                changed.Add(item: index);
            }
        }

        return changed;
    }

    private IEnumerable<object?> Enumerate()
    {
        for (int index = 0; index < this.Count; index++)
        {
            yield return ItemAt(rows: this.rows, index: index);
        }
    }

    private void Raise(NotifyCollectionChangedEventArgs args) => this.CollectionChanged?.Invoke(sender: this, e: args);
}
