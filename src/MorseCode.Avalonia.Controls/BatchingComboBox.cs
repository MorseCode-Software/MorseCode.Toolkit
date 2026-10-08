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
///         <see cref="BoundSelectedItem" />. Do not bind
///         <see cref="SelectingItemsControl.SelectedItem" />.
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
///         A selection that the user makes goes to
///         <see cref="BoundSelectedItem" />, and the binding writes it back. A
///         change that this control makes itself never goes to it.
///     </para>
/// </remarks>
[PublicAPI]
// ReSharper disable once InheritdocConsiderUsage - The summary of ComboBox does not say how this control applies its changes.
public class BatchingComboBox : ComboBox
{
    /// <summary>
    ///     Defines the <see cref="BoundSelectedItem" /> property.
    /// </summary>
    public static readonly StyledProperty<object?> BoundSelectedItemProperty =
        AvaloniaProperty.Register<BatchingComboBox, object?>(
            name: nameof(BoundSelectedItem),
            defaultValue: null,
            inherits: false,
            defaultBindingMode: BindingMode.TwoWay);

    // True from the start of a batch until the end that the dispatcher runs.
    private bool isBatching;

    // True while this control writes BoundSelectedItem. That write is not a change to apply.
    private bool isWritingBoundSelectedItem;

    /// <summary>
    ///     Gets or sets the selected item, for a two-way binding to a view model.
    /// </summary>
    /// <remarks>
    ///     A change to this property and a change to
    ///     <see cref="ItemsControl.ItemsSource" /> that arrive in one turn of the
    ///     dispatcher apply together. A value that the list does not have selects
    ///     no item, and this property keeps the value.
    /// </remarks>
    public object? BoundSelectedItem
    {
        get => this.GetValue(property: BoundSelectedItemProperty);
        set => this.SetValue(property: BoundSelectedItemProperty, value: value);
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
            base.OnPropertyChanged(change: change);

            return;
        }

        base.OnPropertyChanged(change: change);

        if (change.Property == BoundSelectedItemProperty)
        {
            if (!this.isWritingBoundSelectedItem)
            {
                this.StartBatch();
                this.SelectedItem = change.GetNewValue<object?>();
            }
        }
        else if (change.Property == SelectedItemProperty && !this.isBatching)
        {
            // Outside a batch, only the user changes the selection.
            this.isWritingBoundSelectedItem = true;

            try
            {
                this.SetCurrentValue(property: BoundSelectedItemProperty, value: this.SelectedItem);
            }
            finally
            {
                this.isWritingBoundSelectedItem = false;
            }
        }
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
