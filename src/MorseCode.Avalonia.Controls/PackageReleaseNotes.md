0.1.0

The first release. It contains ComboBox.

The controls of this package are drop-in replacements for the Avalonia controls
of the same names, for views that bind to view models on SodaFlow. Use each one
in all places that need that control. In XAML, add the namespace with a prefix,
such as mc, and write <mc:ComboBox> in place of <ComboBox>.

The ComboBox of Avalonia applies each change to ItemsSource and to SelectedItem
when it arrives. When its list changes, it clears its selection, and then it
selects the item that it showed before, if the new list has that item. A
two-way binding writes each of those values back to the view model. Thus, when
a view model changes the list and the selected item in one update, the combo
box can write back a value that the view model never chose. When the selected
item arrives before the list, the combo box cannot find it and loses it.

The ComboBox of this package applies the two together. Bind ItemsSource and
SelectedItem as usual:

  <mc:ComboBox ItemsSource="{Binding Courses.Value}"
               SelectedItem="{Binding Course.Value, Mode=TwoWay}" />

The first change to either property starts a batch with BeginInit, and the
control ends it with EndInit after the current work of the dispatcher. The
combo box applies both values at that point and writes nothing back. A
selection that the user makes goes back through the binding, as on the combo
box of Avalonia. A selected item that the list does not have selects no item,
and the binding writes null back, also as on the combo box of Avalonia.

The control declares its own SelectedItem property, and that property starts
the batch. A binding in XAML uses it. A binding in code must name
MorseCode.Avalonia.Controls.ComboBox.SelectedItemProperty. A binding that names
the property of the Avalonia ComboBox or of SelectingItemsControl, and a set
through a reference of the Avalonia type, go to the base and start no batch.

In C#, a file that imports both Avalonia.Controls and
MorseCode.Avalonia.Controls cannot write a plain ComboBox, because the name has
two meanings. Write global::Avalonia.Controls.ComboBox for the one of Avalonia,
or use a using alias.

Both changes must arrive in one turn of the dispatcher. SodaFlow's
SynchronizationContextBindingScheduler sends everything that one transaction
posts as one item of the dispatcher, in the release that adds that behavior.

This package is pre-1.0. Its API can change in a minor version until 1.0.0.

---

About this package

Avalonia controls for views that bind to view models written in a functional
reactive style on SodaFlow. It is part of the MorseCode toolkit, which holds the
conventions that MorseCode Software builds its own applications with.

Targets net8.0 and net10.0, as Avalonia 12 does. Depends on Avalonia.

Source: https://github.com/MorseCode-Software/MorseCode.Toolkit
