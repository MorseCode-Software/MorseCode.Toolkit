using System;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     A <see cref="ComboBox" /> in a window, bound to a
///     <see cref="MaybeSelectionModel" />, with the first values of the model.
/// </summary>
// ReSharper disable once InheritdocConsiderUsage - The summary of IDisposable does not say what this closes.
internal sealed class ShownMaybe : IDisposable
{
    private readonly Window window;

    private ShownMaybe(ComboBox box, Control content, MaybeSelectionModel model)
    {
        this.Box = box;
        this.window = new Window { Content = content, Width = 300, Height = 200 };
        this.window.Show();
        this.Settle();
        model.Writes.Clear();
    }

    /// <summary>
    ///     Gets the combo box.
    /// </summary>
    public ComboBox Box { get; }

    /// <summary>
    ///     Shows the combo box of <see cref="MaybeComboBoxView" />, which binds its
    ///     selected item two-way through <see cref="StringMaybeConverter" />.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The shown combo box.</returns>
    public static ShownMaybe InXaml(MaybeSelectionModel model)
    {
        MaybeComboBoxView view = new() { DataContext = model };

        return new ShownMaybe(box: view.Box, content: view, model: model);
    }

    /// <summary>
    ///     Shows a combo box whose selected item binds two-way through a converter.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="converter">The converter.</param>
    /// <returns>The shown combo box.</returns>
    public static ShownMaybe TwoWayInCode(MaybeSelectionModel model, IValueConverter converter)
    {
        ComboBox box = new() { DataContext = model };
        box.Bind(property: ItemsControl.ItemsSourceProperty, binding: new ReflectionBinding(path: "Items"));

        box.Bind(
            property: ComboBox.SelectedItemProperty,
            binding: new ReflectionBinding(path: "Selected") { Mode = BindingMode.TwoWay, Converter = converter });

        return new ShownMaybe(box: box, content: box, model: model);
    }

    /// <summary>
    ///     Runs the work of the dispatcher, which includes the posted changes and the
    ///     end of a batch.
    /// </summary>
    public void Settle() => this.window.Dispatcher.RunJobs();

    /// <inheritdoc />
    public void Dispose() => this.window.Close();
}
