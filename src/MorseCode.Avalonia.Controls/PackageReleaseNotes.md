0.3.0

Breaking: VirtualizingItemsControl is now bound through one property, Source, and the
public properties TotalCount, Rows, VisibleRange, and ScrollRequest are gone. The type of
Source is the new interface IVirtualItemsSource. It holds five SodaFlow bindable values:
TotalCount, Rows, ScrollRequest, VisibleRange, and ScrollHandled. The control attaches to all of
them when Source is set, so none of them can be left unbound. The two pairs, range with rows and
request with report, are made together.

New: CreateVirtualItemsSource, an extension method of the IOutput of a ViewModelBase from
MorseCode.Mvvm. One overload takes a
ReactiveCollection, a function that builds the row for a key, and a Stream of scroll positions.
It slices the collection with the range that the control reports, maps each key in the slice to a
row, and counts the keys. It returns an IVirtualItemsSource, and it registers the rows with the
output, so disposing of the view model releases them. The other overload takes the count, the rows, and the range sink
yourself. Both take an optional Stream of clears that withdraw a scroll request.

The scroll protocol is also new. The control no longer writes none to ScrollRequest. It reports
the position that it handled in ScrollHandled, and the view model clears a request only if it is
still that position. A request that the view model made while the report was on its way was
cleared with the old one before. The control also scrolls once for each request, so a change of
the count before the view model clears the request does not scroll again.

Moved: the helper CreateScrollRequest of MorseCode.Mvvm 0.2.4 is now inside this package and is
not public. CreateVirtualItemsSource uses it to make the request and the report.

The control follows its source only while it is in a visual tree. When it leaves the tree, it
removes its handlers from the values of the source, so a view model that outlives its view does
not keep the view alive. When it returns, it reads the source again. A control that is not in a
tree reads the source once when Source is set and does not follow it after that.

A container that Avalonia recycles after a reset or a removal no longer builds its row template
for no content. The base clears the content of a container before its template, and the
ContentPresenter of Avalonia 12 builds an explicit template also when the content is null. The
control now clears the template first.

The package now depends on SodaFlow, SodaFlow.Bindable.ObjectModel, SodaFlow.Collections, and
MorseCode.Mvvm 0.3.0 or later. To
move from 0.2.x: build the source in the view model, expose it as an IVirtualItemsSource, and
replace the bindings in the view with Source="{Binding Source}".

0.2.2

Changed: a large change of TotalCount now raises one reset of the list, where it raised an
add or remove event that held one item for each position. A change of 0 to 500,000 positions
took about 9 ms and allocated about 8 MB before. A change of 512 positions or fewer raises the
same events as before. VirtualizingItemsControl also no longer forces a layout pass each time
it reports VisibleRange. The reported ranges do not change.

The documentation of VirtualRows now says that Items must not change after it is handed over.
The documentation page says that the control uses the style of ListBox, and what happens to a
scroll request that is beyond TotalCount when the count grows later. No type or member changed,
and the package has no new dependency.

0.2.1

New: VirtualizingItemsControl.ScrollRequest, a Maybe<int> that lets a view model
scroll the list. The view model sets the position that it wants to show. The
control scrolls there as soon as it is loaded and has that many positions, and
then sets the property back to none. The property binds two-way by default. It is
a value and not an event, so a request that the view model makes before the view
is bound, or before the list has laid out, is not lost. A request beyond
TotalCount waits for the count. A negative position is cleared at once. A new
request replaces one that waits.

No existing type changed, and the package has no new dependency. The
documentation page for VirtualizingItemsControl shows the view model, with a
bindable action and a two-way bindable value, and says what the property does
not do: a second request that arrives in the instant between a scroll and the
write of none can be cleared with the first.

0.2.0

New: VirtualizingItemsControl, an items control for a long list whose rows are
heavy view models. Avalonia makes containers only for the positions on screen.
The control reports those positions in VisibleRange, and the view model answers
in Rows with the rows for them. A position that Rows does not cover shows
PlaceholderTemplate. In the view model, a Slice of a ReactiveCollection whose
offset and limit come from VisibleRange, followed by a Map, builds a row only
when the row is on screen. The control has no reference to SodaFlow's
collections, and the package has no new dependency.

The documentation page for VirtualizingItemsControl shows the view model and
the view. No existing type changed.

0.1.0

The first release. It contains ComboBox, MaybeConverter, and
MaybeConverter<T, TSelf>.

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

Both changes must arrive in one turn of the dispatcher. From
SodaFlow.Bindable.ObjectModel.Core 4.2.0, SynchronizationContextBindingScheduler
sends everything that one transaction posts as one item of the dispatcher.
SodaFlow.Bindable.ObjectModel 4.0.3 and later depend on that version. With an
earlier version, a list and a selected item that one transaction changes can
arrive in two items. Then a selected item that arrives before its list is lost,
as on the combo box of Avalonia.

The control declares its own SelectedItem property, and that property starts
the batch. A binding in XAML uses it. A binding in code must name
MorseCode.Avalonia.Controls.ComboBox.SelectedItemProperty. A binding that names
the property of the Avalonia ComboBox or of SelectingItemsControl, and a set
through a reference of the Avalonia type, go to the base and start no batch.

In C#, a file that imports both Avalonia.Controls and
MorseCode.Avalonia.Controls cannot write a plain ComboBox, because the name has
two meanings. Write global::Avalonia.Controls.ComboBox for the one of Avalonia,
or use a using alias.

MaybeConverter and MaybeConverter<T, TSelf> bind a control to a property of
the type Maybe<T>, which view models on SodaFlow use in place of null.

MaybeConverter is for a one-way binding. Some(value) gives the value, and None
gives null. One instance serves each Maybe<T>:

  Text="{Binding Note.Value, Converter={x:Static mc:MaybeConverter.Instance}}"

It cannot convert back, because it does not know which Maybe<T> to make. A
conversion back gives a binding error that names MaybeConverter<T, TSelf>, and
the binding does not write.

MaybeConverter<T, TSelf> is for a two-way binding. Close it with a sealed
subclass that has no members, and use the Instance that the base gives:

  public sealed class CourseOptionConverter
      : MaybeConverter<CourseOption, CourseOptionConverter>;

  SelectedItem="{Binding Course.Value, Mode=TwoWay,
                 Converter={x:Static app:CourseOptionConverter.Instance}}"

Toward the control, Some(value) gives the value and None gives null. Toward the
view model, null gives None and a value gives Some(value). It knows T when it
compiles, thus it uses no reflection, and trimming and compilation ahead of
time keep each part that it uses.

For each converter, a value of a different type gives a binding error, and the
binding does not write.

This package is pre-1.0. Its API can change in a minor version until 1.0.0.

---

About this package

Avalonia controls and converters for views that bind to view models written in
a functional reactive style on SodaFlow. It is part of the MorseCode toolkit,
which holds the conventions that MorseCode Software builds its own applications
with.

Targets net8.0 and net10.0, as Avalonia 12 does. Depends on Avalonia and
SodaFlow.Functional.

Source: https://github.com/MorseCode-Software/MorseCode.Toolkit
