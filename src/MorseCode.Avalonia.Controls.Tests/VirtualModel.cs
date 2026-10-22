using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using SodaFlow.Functional;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     A view model for a <see cref="VirtualizingItemsControl" />. It does what a
///     <c>Slice</c> followed by a <c>Map</c> does. It builds a row for each position
///     of the range that the view writes, and for no other position.
/// </summary>
/// <param name="total">The number of positions in the whole list.</param>
// ReSharper disable once InheritdocConsiderUsage - The summary of INotifyPropertyChanged does not say what this model builds.
internal sealed class VirtualModel(in int total) : INotifyPropertyChanged
{
    private readonly Dictionary<int, VirtualRow> rows = [];

    private ItemRange visibleRange = ItemRange.Empty;
    private Maybe<int> scrollRequest = Maybe<int>.None;
    private bool isHeld;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     Gets the number of positions in the whole list.
    /// </summary>
    public int TotalCount { get; private set; } = total;

    /// <summary>
    ///     Gets the rows for the last range that the view wrote.
    /// </summary>
    public VirtualRows Rows { get; private set; } = VirtualRows.Empty;

    /// <summary>
    ///     Gets or sets the range that shows. Only the view calls the setter.
    /// </summary>
    // ReSharper disable once UnusedMember.Global - A binding in the view writes this property by its name.
    public ItemRange VisibleRange
    {
        get => this.visibleRange;
        set
        {
            this.Writes.Add(item: value);

            // A view that reports without end is a defect. Without this check, it hangs the test run.
            if (this.Writes.Count > 200)
            {
                throw new InvalidOperationException(
                    message: "The view reported a range more than 200 times: "
                             + string.Join(separator: " ", values: this.Writes.Take(count: 12)));
            }

            this.visibleRange = value;

            if (!this.isHeld)
            {
                this.Answer();
            }
        }
    }

    /// <summary>
    ///     Gets or sets the position that the view model wants to show. Only the view calls the setter,
    ///     and the value that it writes is none. A test makes a request with <see cref="Request" />.
    /// </summary>
    // ReSharper disable once UnusedMember.Global - A binding in the view reads and writes this property by its name.
    public Maybe<int> ScrollRequest
    {
        get => this.scrollRequest;
        set
        {
            this.ScrollWrites.Add(item: value);
            this.scrollRequest = value;
        }
    }

    /// <summary>
    ///     Gets each value that the view wrote to <see cref="ScrollRequest" />, in sequence.
    /// </summary>
    public List<Maybe<int>> ScrollWrites { get; } = [];

    /// <summary>
    ///     Gets each range that the view wrote, in sequence.
    /// </summary>
    public List<ItemRange> Writes { get; } = [];

    /// <summary>
    ///     Gets the position of each row that the model built, in sequence.
    /// </summary>
    public List<int> Built { get; } = [];

    /// <summary>
    ///     Stops the model from answering a range until <see cref="Release" /> runs.
    ///     This stands for the time that a graph and a binding scheduler take.
    /// </summary>
    public void Hold() => this.isHeld = true;

    /// <summary>
    ///     Answers the last range that the view wrote.
    /// </summary>
    public void Release()
    {
        this.isHeld = false;
        this.Answer();
    }

    /// <summary>
    ///     Changes the number of positions in the whole list, and answers again.
    /// </summary>
    /// <param name="total">The new number of positions.</param>
    public void Resize(int total)
    {
        this.TotalCount = total;
        this.Raise(propertyName: nameof(this.TotalCount));
        this.Answer();
    }

    /// <summary>
    ///     Asks the view to scroll to a position, as a view model does. It does not count as a write of
    ///     the view.
    /// </summary>
    /// <param name="position">The position.</param>
    public void Request(int position)
    {
        this.scrollRequest = Maybe<int>.Some(value: position);
        this.Raise(propertyName: nameof(this.ScrollRequest));
    }

    private void Answer()
    {
        int first = this.visibleRange.First;
        int end = Math.Min(val1: first + this.visibleRange.Count, val2: this.TotalCount);
        List<object?> items = [];

        for (int position = first; position < end; position++)
        {
            if (!this.rows.TryGetValue(key: position, value: out VirtualRow? row))
            {
                row = new VirtualRow(Position: position);
                this.rows.Add(key: position, value: row);
                this.Built.Add(item: position);
            }

            items.Add(item: row);
        }

        this.Rows = new VirtualRows(Offset: first, Items: items);
        this.Raise(propertyName: nameof(this.Rows));
    }

    private void Raise(string propertyName) =>
        this.PropertyChanged?.Invoke(sender: this, e: new PropertyChangedEventArgs(propertyName: propertyName));
}
