---
title: A combo box that keeps its selection
---

# A combo box that keeps its selection

`BatchingComboBox`, in `MorseCode.Avalonia.Controls`, is an Avalonia `ComboBox` that
applies a new list and a new selected item together. Use it wherever a view model can change both
in one update.

## The problem

A view model often changes a combo box's list and its selection at the same moment. Opening a
form to edit a record is the usual case: the form loads the list of choices and picks the
record's current choice in one SodaFlow transaction.

A plain Avalonia `ComboBox` applies each property as it arrives:

- When the list arrives first, the combo box clears its selection, because the old selected item
  belongs to the old list. Then it selects the item it showed before, if the new list has it. A
  two-way binding writes both of those values back to the view model: first `null`, then the old
  item. The view model takes them as the user's choices, and by the time the real selection
  arrives, the view model has already been changed.
- When the selection arrives first, the combo box looks for it in the old list, doesn't find it,
  and writes `null` back. The selection is lost.

Either way, the view model ends up with a value nobody chose.

## Using it

Use it where you would use a `ComboBox`, and bind `ItemsSource` and `SelectedItem` as usual:

```xml
<UserControl xmlns:controls="clr-namespace:MorseCode.Avalonia.Controls;assembly=MorseCode.Avalonia.Controls">

    <controls:BatchingComboBox ItemsSource="{Binding Courses.Value}"
                               SelectedItem="{Binding Course.Value, Mode=TwoWay}" />

</UserControl>
```

`SelectedItem` binds two-way by default, as on a `ComboBox`. Everything else about the control is
a `ComboBox` too, including its theme and template.

### Bind it in XAML, or name `BatchingComboBox.SelectedItemProperty`

`BatchingComboBox` declares its own `SelectedItem`, which starts the batch before the value reaches
the combo box. Avalonia treats it as the same property as the base's for styles and notifications,
but a binding sets the value through whichever of the two it was created with:

- A XAML binding on a `BatchingComboBox` uses the control's own property, so it is batched.
- In code, bind `BatchingComboBox.SelectedItemProperty`. A binding made with
  `ComboBox.SelectedItemProperty` or `SelectingItemsControl.SelectedItemProperty` goes straight to
  the base and is not batched.
- Setting `SelectedItem` through a variable typed as `ComboBox` also skips the batch, because the
  control's property hides the base's rather than overriding it.

## How it works

Avalonia's selecting controls can already hold a new list and a new selection and apply them as a
pair: that is what happens between `BeginInit()` and `EndInit()`, and it writes nothing back
through the bindings. `BatchingComboBox` uses that mechanism at run time.

When `ItemsSource` or `SelectedItem` changes, the control calls `BeginInit()`, if it hasn't
already, and posts `EndInit()` to the dispatcher at normal priority. That call runs after the work
the dispatcher is already doing, so any other change that arrives in the same piece of work joins
the batch. Normal priority comes before rendering, so no frame shows half a batch.

When the user picks an item, the binding writes it to the view model, as with any combo box. The
temporary changes the combo box makes while it applies a batch are never written back.

A selected item that the list doesn't contain selects nothing, and the binding writes `null` back,
as it does for a plain `ComboBox`.

## It needs both changes in one piece of dispatcher work

The batch covers what arrives before the dispatcher finishes its current work. Both changes must
therefore reach the UI thread together.

SodaFlow's `SynchronizationContextBindingScheduler` does this: everything one transaction posts
reaches the UI thread as a single piece of work, in the order it was posted. A view model that
changes the list and the selection in one transaction is enough.

A list and a selection that arrive in separate pieces of work still work when the list arrives
first. The selection is then the only change in its batch, and the list already has the item.

## What it does not cover

The control batches replacements of `ItemsSource`. A change inside the same collection, such as
removing the selected item from an `ObservableCollection`, goes through a different path in
Avalonia, and the combo box handles it as it always does.
