using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using SodaFlow.Functional;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     A view model with a list and a selected item that is a
///     <see cref="Maybe{T}" />, as a view model on SodaFlow has. It records each
///     value that the view writes back.
/// </summary>
/// <param name="items">The first list.</param>
/// <param name="selected">The first selected item.</param>
// ReSharper disable once InheritdocConsiderUsage - The summary of INotifyPropertyChanged does not say what this model records.
internal sealed class MaybeSelectionModel(in IReadOnlyList<string> items, in Maybe<string> selected)
    : INotifyPropertyChanged
{
    private Maybe<string> selected = selected;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     Gets the list.
    /// </summary>
    public IReadOnlyList<string> Items { get; private set; } = items;

    /// <summary>
    ///     Gets or sets the selected item. Only the view calls the setter. A test
    ///     changes the value with <see cref="SetSelected" />.
    /// </summary>
    public Maybe<string> Selected
    {
        get => this.selected;
        set
        {
            this.Writes.Add(item: value);
            this.selected = value;
        }
    }

    /// <summary>
    ///     Gets each value that the view wrote back, in sequence.
    /// </summary>
    public List<Maybe<string>> Writes { get; } = [];

    /// <summary>
    ///     Makes a change in one item of the dispatcher, as SodaFlow's
    ///     SynchronizationContextBindingScheduler posts the changes of one
    ///     transaction.
    /// </summary>
    /// <param name="change">The change, which gets this model.</param>
    public void Post(Action<MaybeSelectionModel> change) =>
        // ReSharper disable once NullableWarningSuppressionIsUsed - Avalonia puts a context on its thread, and the tests run there.
        SynchronizationContext.Current!.Post(d: _ => change(obj: this), state: null);

    /// <summary>
    ///     Gives the list a new value and announces it.
    /// </summary>
    /// <param name="items">The new list.</param>
    public void SetItems(IReadOnlyList<string> items)
    {
        this.Items = items;
        this.PropertyChanged?.Invoke(sender: this, e: new PropertyChangedEventArgs(propertyName: nameof(this.Items)));
    }

    /// <summary>
    ///     Gives the selected item a new value and announces it.
    /// </summary>
    /// <param name="value">The new selected item.</param>
    public void SetSelected(Maybe<string> value)
    {
        this.selected = value;

        this.PropertyChanged?.Invoke(
            sender: this,
            e: new PropertyChangedEventArgs(propertyName: nameof(this.Selected)));
    }
}
