0.3.0

The first release. This package holds what a view model gives to a control of the MorseCode
toolkit, and the methods that build it. It has no dependency on a UI framework, so the project that
holds your view models references it and no UI package. MorseCode.Controls.UI.Avalonia, which has
VirtualizingItemsControl, depends on it, so a project of views gets it too. Its first version is
0.3.0 so that the two packages have the same version.

Moved: IVirtualItemsSource, VirtualRows, ReportedRange, ItemRange, CreateVirtualItemsSource, and
CreateVirtualItemsSourceByKey are here, in the namespace MorseCode.Controls.ViewModels. VirtualRows
and ItemRange were in MorseCode.Avalonia.Controls 0.2.x, in the namespace of that package. The
others are new in 0.3.0 and no other package released them. To move from 0.2.x: reference this
package from the project of the view models, and change each using of MorseCode.Avalonia.Controls
there to MorseCode.Controls.ViewModels. The notes of MorseCode.Controls.UI.Avalonia 0.3.0 say what
changed in the control, and in the values that it reads and writes.

New: CreateVirtualItemsSource, an extension method of the IOutput of a ViewModelBase from
MorseCode.Mvvm. One overload takes a ReactiveCollection, a function that builds the row for a key,
and a Stream of scroll positions. It slices the collection with the range that the control wants
rows for, maps each key in the slice to a row, and counts the keys. It returns an
IVirtualItemsSource, and it registers the rows with the output, so disposing of the view model
releases them. Another overload is for a view model that makes its own rows. It takes a Cell of
VirtualRows, which carries their count, a CellSink that receives what the control reports, and a
Stream of scroll positions. Each takes an optional Stream of clears that withdraw a scroll request.

New: CreateVirtualItemsSourceByKey, which takes a Stream of keys instead of a Stream of positions.
It finds the position of a key in the collection in the transaction of the request, so a view
model does not keep positions, which move when the collection changes. A key that the collection
does not have at that instant makes no request. It has its own name because a collection with int
keys would make an overload by key the same as the overload by position.

Moved: the helper CreateScrollRequest of MorseCode.Mvvm 0.2.4 is now inside this package and is
not public. CreateVirtualItemsSource uses it to make the request and the report.

New: the overloads of CreateVirtualItemsSource for a collection return an IVirtualItemsSource of
the row type. It is an IVirtualItemsSource, so the view binds to it as before, and it gives the view
model these cells. VisibleRows is the rows on screen, and it changes only when a different row
shows. FirstVisibilityAmount and LastVisibilityAmount say how much of the first and of the last of
those rows is on screen. ConstructedRows is the rows that the control was given, which is the list
in Rows with the type of the rows. Reported is the last ReportedRange. Before, a view model could
read its rows only as a list of object, and had to work out the visible ones itself.

New: an overload of CreateVirtualItemsSource for a list whose rows all exist, for a view model that
wants only the virtualization of the row views. It takes a Cell of an IReadOnlyList of rows, where
the type of a row is a class, so the list goes into VirtualRows without a copy, and an optional
Stream of scroll positions and of clears. The count is the count of the list, and the rows start at
position zero. It returns an IVirtualItemsSource of the row type, as the overloads for a collection
do. VisibleRows is the rows of the list on screen. ConstructedRows is the rows of the list at the
positions of Constructed, which here is a part of the list in Rows. Each changes only when a
different row object is at one of its positions, so an edit elsewhere in the list changes neither.
With 0.2.x, an app bound TotalCount and Rows to such a list. Without this overload, 0.3.0 would make
it pass a sink of the report and a stream of positions that never fires.

New: ReportedRange.Ahead, the number of positions after the end of the list that the control also
wants rows for, once the list has them. Both ranges of a ReportedRange stay inside the list, so
neither can hold those positions. A source made from a collection adds the number to its slice, so
a row that is added at an end on screen arrives together with the new count. The control sets it,
and it is zero when the control sets nothing.

The package targets net472, net6.0, net8.0, and netstandard2.0, as MorseCode.Mvvm does, so a project
of view models keeps the targets that it has. It depends on MorseCode.Mvvm 0.3.0, and on SodaFlow
[5.0.1, 6.0.0), SodaFlow.Bindable.ObjectModel [4.0.0, 5.0.0), SodaFlow.Collections [2.0.0, 3.0.0),
and SodaFlow.Functional [3.0.2, 4.0.0). Each range ends before the next breaking version, as the
dependencies between the packages of this repository do. A project that asks for a SodaFlow major
past the ceiling gets NuGet warning NU1608 at restore. A SodaFlow major therefore needs a release
of this package, even when nothing here changes.

New: the net8.0 build is marked as compatible with trimming and native AOT. The build of the
package runs the trim, AOT, and single-file analyzers on it and fails on a report, so code that
needs reflection cannot be released by mistake. The other builds are not marked, because those
analyzers do not exist for them.

This package is pre-1.0. Its API can change in a minor version until 1.0.0.

---

About this package

What a view model on SodaFlow gives to a control of the MorseCode toolkit, and the methods that
build it, with no dependency on a UI framework. It is part of the MorseCode toolkit, which holds
the conventions that MorseCode Software builds its own applications with.

Targets net472, net6.0, net8.0, and netstandard2.0. Depends on MorseCode.Mvvm, SodaFlow,
SodaFlow.Bindable.ObjectModel, SodaFlow.Collections, and SodaFlow.Functional.

Source: https://github.com/MorseCode-Software/MorseCode.Toolkit
