0.3.0

Breaking: VirtualizingItemsControl is now bound through one property, Source, and the
public properties TotalCount, Rows, VisibleRange, and ScrollRequest are gone. The type of
Source is the new interface IVirtualItemsSource. It holds the SodaFlow bindable values Rows,
ScrollRequest, Range, and ScrollHandled. The control attaches to all of them when Source is
set, so none of them can be left unbound. The two pairs, range with rows and request with report,
are made together.

Breaking: VirtualRows now carries the count of the whole list in TotalCount, next to Offset and
Items, and there is no separate count. A change of the count and the rows reaches the control as
one value, so the control applies both in one update and never shows a count with the rows of a
different instant. Before, the two arrived one after the other, and the list was updated twice.

New: CreateVirtualItemsSource, an extension method of the IOutput of a ViewModelBase from
MorseCode.Mvvm. One overload takes a ReactiveCollection, a function that builds the row for a key,
and a Stream of scroll positions. It slices the collection with the range that the control wants
rows for, maps each key in the slice to a row, and counts the keys. It returns an
IVirtualItemsSource, and it registers the rows with the output, so disposing of the view model
releases them. The other overload is for a view model that makes its own rows. It takes a Cell of
VirtualRows, which carries their count, a CellSink that receives what the control reports, and a
Stream of scroll positions. Both take an optional Stream of clears that withdraw a scroll request.

The scroll protocol is also new. The control no longer writes none to ScrollRequest. It reports
the position that it handled in ScrollHandled, and the view model clears a request only if it is
still that position. A request that the view model made while the report was on its way was
cleared with the old one before. The control also scrolls once for each request, so a change of
the count before the view model clears the request does not scroll again.

New: CreateVirtualItemsSourceByKey, which takes a Stream of keys instead of a Stream of positions.
It finds the position of a key in the collection in the transaction of the request, so a view
model does not keep positions, which move when the collection changes. A key that the collection
does not have at that instant makes no request. It has its own name because a collection with int
keys would make an overload by key the same as the overload by position.

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

Fixed: a change of a row on screen could make the control and the view model exchange ranges and
rows without end. The panel of Avalonia recycles the container of a replaced item at once and makes
it again at the next layout. The control measured between the two, found fewer containers, and
reported a smaller range. The view model dropped the rows outside it, the layout showed
placeholders for them, and the control asked for the rows again. With Buffer at 0, a scroll by
three rows, a removal on screen, or an addition at an end on screen never settled, because the
dispatcher always had a report to run. With the default Buffer of 10, a removal on screen brought
the rows three times. The control now reads the first and the last position from the
VirtualizingStackPanel, which keeps the place of a recycled container. With another panel in
ItemsPanel, the control waits for the layout of the panel before it reads the containers.

The control now finds the range that shows without a list. With the default panel, it reads two
numbers from the panel. With another panel, it makes one pass over the realized containers. With
ten containers, one report took between about 0.15 and 0.45 microseconds and 24 bytes with the
default panel, and about 1.5 microseconds and 96 bytes with the pass. It took about 4.1
microseconds and 392 bytes before. All were measured through reflection in a headless test.

An update of the rows after a jump no longer takes time in proportion to the distance of the jump.
The control compared the old and the new item at every position from the first of the old and new
rows to the last. After a jump, that is every position between the two, and each of those holds
the placeholder before and after. The control now compares only the positions of the old rows and
of the new rows. With 30 old rows and 30 new rows, one update took about 1.2 to 1.6 milliseconds
when they were 450,000 positions apart, and about 17 to 19 milliseconds at 9,000,000 apart. It now
takes about 4 microseconds at any distance, and it finds the same changed positions.

Fixed: after a jump, an edit could send the list to a different part and back, and a view model
that makes new row objects never settled. The list raised a Replace for each position whose item
changed, and the panel of Avalonia then built each of those containers again. A panel with no
container left on screen estimates its position from an average height. When the rows and the
placeholders differ in height, that estimate is a different part of the list. With rows of 40 and
placeholders of 20, a removal above the rows on screen took four layouts and four values of the
rows, and two of the layouts showed only placeholders. A view model on the overload for its own
rows that made a new row object for each position got a different range after each answer, without
end. The control now keeps the container of each position and gives it the new row. The same
removal takes one layout and one value, and the view model with new row objects settles. A jump
also stays at the position that agrees with the scroll bar, where before it went on to a
different position.

Changed: because the container stays, a row template that reuses its view gets the new row only
as the data context of that view. Each XAML DataTemplate reuses its view, and a FuncDataTemplate
does so with supportsRecycling: true. Such a view must read its values through bindings. A view
that sets its values when the template builds it shows the row from before, with no error. A
FuncDataTemplate with the default supportsRecycling: false builds a view for each row and is not
affected. The focus stays in its container, at the position, where before it left the list.
ContainerPrepared is raised when Avalonia makes a container for a position, and no longer when
the row of a position changes. An item that is a Control, and a container that is not a
ContentPresenter or a ContentControl, are replaced through the panel as before. A change of more
than 512 rows is no longer a reset. Only a change of the count by more than 512 positions is.

Breaking: the Buffer property is gone. The control now decides how far ahead of the screen it asks
for rows: one screen of positions on each side, and never fewer than 10. A scroll shows no
placeholder when its step is no longer than the positions on one side, so a fixed number was right
for one window size only. With 20 rows on screen and Buffer at 10, every step of one screen showed
placeholders. Remove Buffer from your XAML.

Breaking: the control reports a ReportedRange where it reported an ItemRange, and the value of the
source is named Range, where it was VisibleRange. A ReportedRange holds two ranges from the same
instant. Visible is the positions on screen, where a row that is only partly on screen counts, so a
view model can now tell what the user sees. Constructed is the positions that the control wants
rows for. Both stay inside the list. On the overload for a view model with its own rows, the
parameter visibleRange is now reportedRange, a CellSink of ReportedRange, and the view model builds
rows for Constructed. The overloads for a collection take an optional reportedRange, for a view
model that wants to read Visible.

A row that is added at an end of the list that is on screen no longer shows as a placeholder first.
A source made from a collection slices past the end of the list by the positions that the control
wants there, so the row arrives together with the new count. That includes the first row of an
empty list, and a list that is read at its end while rows arrive, such as a log. For a view model
with its own rows, the control now reports as soon as it sees a new count, where it waited for a
container before. With the default panel the row then arrives before the next layout.

New: ReportedRange has FirstVisibilityAmount and LastVisibilityAmount, which say how much of the
first and of the last visible row is on screen, from 0.0 for none of it to 1.0 for all of it. Every
row between those two is fully on screen, because the rows sit in one line along the scroll
direction. An amount is measured along that direction, against the part of the list that nothing
above it clips. The control reports at every step of a scroll for that, where it reported once for
each row before. A source made from a collection sends no rows for a report that leaves Constructed
as it was. A view model with its own rows should calm Constructed before it builds rows from it.
Measured with no screen, a scroll step that brings no new row takes about 0.07 milliseconds and
4 KB more than before, and a step that brings a row takes the same time as before.

The package now depends on SodaFlow, SodaFlow.Bindable.ObjectModel, SodaFlow.Collections, and
MorseCode.Mvvm 0.3.0 or later. To move from 0.2.x: build the source in the view model, expose it as
an IVirtualItemsSource, and replace the bindings in the view with Source="{Binding Source}".

Changed: the dependencies on SodaFlow packages now have a ceiling at their next major version, as
the dependencies between the packages of this repository already did: SodaFlow [5.0.1, 6.0.0),
SodaFlow.Bindable.ObjectModel [4.0.0, 5.0.0), SodaFlow.Collections [2.0.0, 3.0.0), and
SodaFlow.Functional [3.0.2, 4.0.0). Before, each was a floor only. The dependency on Avalonia is
still a floor. A project that asks for a SodaFlow major past the ceiling now gets NuGet warning
NU1608 at restore, where before it got nothing and could fail at run time. A SodaFlow major
therefore needs a release of this package, even when nothing here changes.

New: the net8.0 and net10.0 builds are marked as compatible with trimming and native AOT. The build
of the package runs the trim, AOT, and single-file analyzers on them and fails on a report, so code
that needs reflection cannot be released by mistake. The assembly carries the metadata IsTrimmable
and IsAotCompatible, so an app that trims only the assemblies so marked, with TrimMode partial, now
trims this one too. A native AOT publish that roots the assembly reports nothing in its code or in
Avalonia. It reports IL2091 inside SodaFlow.Core 5, which this package depends on, where
SodaFlow.Core gives a value factory to Lazy<T>. That is SodaFlow code, which the mark of this
package does not cover.

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
