using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Threading;
using JetBrains.Annotations;

namespace MorseCode.Avalonia.Controls;

/// <summary>
///     A combo box that applies a new list and a new selected item together. The
///     two must arrive in one turn of the dispatcher.
/// </summary>
/// <remarks>
///     <para>
///         Bind <see cref="ItemsControl.ItemsSource" /> and
///         <see cref="SelectedItem" /> as on a usual combo box.
///     </para>
///     <para>
///         A usual <see cref="ComboBox" /> applies each change when it arrives. When
///         its list changes, it clears its selection, and then it selects the item
///         that it showed before, if the new list has that item. A two-way binding
///         writes each of these values back to the view model. A view model can
///         change the list and the selected item together. Then the combo box can
///         write back a value that the view model never chose. When the selected
///         item arrives before the list, the combo box cannot find it and loses it.
///     </para>
///     <para>
///         This control starts a batch at the first change of the list or of the
///         selected item, with <see cref="StyledElement.BeginInit" />. It ends the
///         batch with <see cref="StyledElement.EndInit" /> after the current work of
///         the dispatcher. The combo box applies the two values together at that
///         point, and writes nothing back. SodaFlow's
///         SynchronizationContextBindingScheduler sends all the changes of one
///         transaction in one item of the dispatcher. Thus, the changes of one
///         transaction always come in one batch.
///     </para>
///     <para>
///         A selection that the user makes goes back through the binding, as on a
///         usual combo box. A selected item that the list does not have selects no
///         item, and the binding writes null back, also as on a usual combo box.
///     </para>
/// </remarks>
[PublicAPI]
// ReSharper disable once InheritdocConsiderUsage - The summary of ComboBox does not say how this control applies its changes.
public class BatchingComboBox : ComboBox
{
    /// <summary>
    ///     Defines the <see cref="SelectedItem" /> property.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This property and the property of the base are the same property for
    ///         styles and notifications. A binding sets the value through the property
    ///         that it names. Thus, a binding must name this property to start a
    ///         batch.
    ///     </para>
    ///     <para>
    ///         XAML on this control names this property. A binding that code makes
    ///         with <see cref="SelectingItemsControl.SelectedItemProperty" /> goes to
    ///         the base and starts no batch. A set through a reference of the type
    ///         <see cref="ComboBox" /> does the same.
    ///     </para>
    /// </remarks>
    public new static readonly DirectProperty<BatchingComboBox, object?> SelectedItemProperty =
        SelectingItemsControl.SelectedItemProperty.AddOwner<BatchingComboBox>(
            getter: static box => box.SelectedItem,
            setter: static (box, value) => box.SelectedItem = value,
            unsetValue: null,
            defaultBindingMode: BindingMode.TwoWay,
            enableDataValidation: true);

    // True from the start of a batch until the end that the dispatcher runs.
    private bool isBatching;

    /// <summary>
    ///     Gets or sets the selected item.
    /// </summary>
    /// <remarks>
    ///     A change to this property and a change to
    ///     <see cref="ItemsControl.ItemsSource" /> that arrive in one turn of the
    ///     dispatcher apply together.
    /// </remarks>
    public new object? SelectedItem
    {
        get => base.SelectedItem;
        set
        {
            // The batch must start before the base gets the value, because the base looks for the
            // item in the list that it has now.
            this.StartBatch();
            base.SelectedItem = value;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     The themes style a combo box, and this control looks like one.
    /// </remarks>
    protected override Type StyleKeyOverride => typeof(ComboBox);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        // The batch must start before the base applies the list, because the base clears the
        // selection when it applies it.
        if (change.Property == ItemsSourceProperty)
        {
            this.StartBatch();
        }

        base.OnPropertyChanged(change: change);
    }

    // The end of the batch runs after the work that the dispatcher already has, at the same priority.
    // Each change that the current item gives thus joins the batch. Normal priority comes before
    // the render, thus no frame shows the values of half a batch.
    private void StartBatch()
    {
        if (this.isBatching)
        {
            return;
        }

        this.isBatching = true;
        this.BeginInit();
        this.Dispatcher.Post(action: this.EndBatch, priority: DispatcherPriority.Normal);
    }

    private void EndBatch()
    {
        try
        {
            this.EndInit();
        }
        finally
        {
            this.isBatching = false;
        }
    }
}
