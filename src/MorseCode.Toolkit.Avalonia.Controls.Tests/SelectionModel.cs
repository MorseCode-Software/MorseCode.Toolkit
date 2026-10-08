using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace MorseCode.Toolkit.Avalonia.Controls.Tests;

/// <summary>
///     A view model with a list and a selected item. It records each value that
///     the view writes back.
/// </summary>
/// <param name="items">The first list.</param>
/// <param name="selected">The first selected item.</param>
// ReSharper disable once InheritdocConsiderUsage - The summary of INotifyPropertyChanged does not say what this model records.
internal sealed class SelectionModel(in IReadOnlyList<string> items, in string selected) : INotifyPropertyChanged
{
    private string? selected = selected;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     Gets the list.
    /// </summary>
    public IReadOnlyList<string> Items { get; private set; } = items;

    /// <summary>
    ///     Gets or sets the selected item. Only the view calls the setter. A test
    ///     changes the value with <see cref="Post" />.
    /// </summary>
    public string? Selected
    {
        get => this.selected;
        set
        {
            this.Writes.Add(item: value ?? "null");
            this.selected = value;
        }
    }

    /// <summary>
    ///     Gets each value that the view wrote back, in sequence. A null value shows
    ///     as "null".
    /// </summary>
    public List<string> Writes { get; } = [];

    /// <summary>
    ///     Makes changes in one item of the dispatcher, in the sequence of the
    ///     arguments.
    /// </summary>
    /// <remarks>
    ///     The model posts with the synchronization context of the thread. That is
    ///     what SodaFlow's SynchronizationContextBindingScheduler does with the
    ///     changes of one transaction.
    /// </remarks>
    /// <param name="changes">The changes.</param>
    public void Post(params IReadOnlyList<Change> changes) =>
        // ReSharper disable once NullableWarningSuppressionIsUsed - Avalonia puts a context on its thread, and the tests run there.
        SynchronizationContext.Current!.Post(
            d: _ =>
            {
                foreach (Change change in changes)
                {
                    change.Apply(model: this);
                }
            },
            state: null);

    /// <summary>
    ///     Gives the list a new value and announces it.
    /// </summary>
    /// <param name="items">The new list.</param>
    internal void SetItems(IReadOnlyList<string> items)
    {
        this.Items = items;
        this.PropertyChanged?.Invoke(sender: this, e: new PropertyChangedEventArgs(propertyName: nameof(this.Items)));
    }

    /// <summary>
    ///     Gives the selected item a new value and announces it.
    /// </summary>
    /// <param name="value">The new selected item.</param>
    internal void SetSelected(string value)
    {
        this.selected = value;

        this.PropertyChanged?.Invoke(
            sender: this,
            e: new PropertyChangedEventArgs(propertyName: nameof(this.Selected)));
    }
}
