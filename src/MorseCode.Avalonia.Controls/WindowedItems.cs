using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     The list that a <see cref="VirtualizingItemsControl" /> gives to Avalonia. It
///     has one position for each item of the whole list. A position that the window
///     covers holds a row from the window. Each other position holds a
///     <see cref="RowPlaceholder" />.
/// </summary>
/// <remarks>
///     This list builds nothing and keeps nothing but the window. Thus, a list of a
///     million positions costs no more than the window.
/// </remarks>
// ReSharper disable once InheritdocConsiderUsage - The summary of IList does not say what this list holds.
internal sealed class WindowedItems : IList, IReadOnlyList<object?>, INotifyCollectionChanged
{
    // More changed positions than this are cheaper for the panel as a reset than as one event for each
    // position. A reset also clears the containers, thus the limit is well above any usual window.
    private const int ResetThreshold = 512;

    private VirtualWindow window = VirtualWindow.Empty;

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

            return ItemAt(window: this.window, index: index);
        }
        set => throw new NotSupportedException();
    }

    /// <summary>
    ///     Changes the length of the list and the window, and tells the listeners
    ///     which positions changed.
    /// </summary>
    /// <param name="newCount">The number of positions in the whole list.</param>
    /// <param name="newWindow">The window.</param>
    public void Update(int newCount, VirtualWindow newWindow)
    {
        int oldCount = this.Count;
        VirtualWindow oldWindow = this.window;

        this.Count = newCount;
        this.window = newWindow;

        // A listener that reads the list while it handles an event sees the final state. Each event is
        // still correct for its own positions, because it names a position and the item that is there.
        List<int> changed =
            ChangedPositions(
                oldWindow: oldWindow,
                oldCount: oldCount,
                newWindow: newWindow,
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
                added.Add(item: ItemAt(window: newWindow, index: index));
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
                removed.Add(item: ItemAt(window: oldWindow, index: index));
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
                    newItem: ItemAt(window: newWindow, index: index),
                    oldItem: ItemAt(window: oldWindow, index: index),
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

        for (int i = 0; i < this.window.Items.Count; i++)
        {
            int index = this.window.Offset + i;

            if (index < this.Count && Equals(objA: this.window.Items[index: i], objB: value))
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
    private static object? ItemAt(VirtualWindow window, int index)
    {
        int inWindow = index - window.Offset;

        return inWindow >= 0 && inWindow < window.Items.Count ? window.Items[index: inWindow] : RowPlaceholder.Instance;
    }

    // The positions that were in the list before and after, and that hold a different item now. Only
    // the two windows can differ, because every other position is a placeholder in both.
    private static List<int> ChangedPositions(
        VirtualWindow oldWindow,
        int oldCount,
        VirtualWindow newWindow,
        int newCount)
    {
        int shared = Math.Min(val1: oldCount, val2: newCount);
        int from = Math.Max(val1: 0, val2: Math.Min(val1: oldWindow.Offset, val2: newWindow.Offset));

        int to =
            Math.Min(
                val1: shared,
                val2: Math.Max(
                    val1: oldWindow.Offset + oldWindow.Items.Count,
                    val2: newWindow.Offset + newWindow.Items.Count));

        List<int> changed = [];

        for (int index = from; index < to; index++)
        {
            object? before = ItemAt(window: oldWindow, index: index);
            object? after = ItemAt(window: newWindow, index: index);

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
            yield return ItemAt(window: this.window, index: index);
        }
    }

    private void Raise(NotifyCollectionChangedEventArgs args) => this.CollectionChanged?.Invoke(sender: this, e: args);
}
