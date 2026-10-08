using System.Collections.Generic;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     One change of a <see cref="SelectionModel" />.
/// </summary>
internal abstract record Change
{
    /// <summary>
    ///     Makes a change of the list.
    /// </summary>
    /// <param name="items">The new list.</param>
    /// <returns>The change.</returns>
    public static Change Items(params IReadOnlyList<string> items) => new ItemsChange(List: items);

    /// <summary>
    ///     Makes a change of the selected item.
    /// </summary>
    /// <param name="value">The new selected item.</param>
    /// <returns>The change.</returns>
    public static Change Selected(string value) => new SelectedChange(Value: value);

    /// <summary>
    ///     Applies the change to the model.
    /// </summary>
    /// <param name="model">The model.</param>
    public abstract void Apply(SelectionModel model);

    private sealed record ItemsChange(IReadOnlyList<string> List) : Change
    {
        public override void Apply(SelectionModel model) => model.SetItems(items: this.List);
    }

    private sealed record SelectedChange(string Value) : Change
    {
        public override void Apply(SelectionModel model) => model.SetSelected(value: this.Value);
    }
}
