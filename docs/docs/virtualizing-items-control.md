---
title: VirtualizingItemsControl
---

# VirtualizingItemsControl

`MorseCode.Avalonia.Controls.VirtualizingItemsControl` shows a long list whose rows are heavy to
build. Avalonia works out which rows are on screen. The control tells your view model, and the view
model builds a row only for those positions. A row that nobody has scrolled to is never built.

It is not a replacement for an Avalonia control. Use it where you would use an `ItemsControl` or a
`ListBox` over a SodaFlow `ReactiveCollection`, and the rows are view models that cost something to
make: they hold bindables, subscriptions, or a query of their own.

The control is made for SodaFlow. It has one property to bind, `Source`, and one helper that makes
the object to bind it to. The sections below show both.

## How it works

The control has a position for every item in the whole list. That gives you a scroll bar of the
right length, a scroll area of the right size, and a working `ScrollIntoView`, as with any list.

Avalonia's `VirtualizingStackPanel` makes containers only for the positions that are on screen.
The control watches those containers and reports two ranges in one value: the positions on screen,
and a wider range that it wants rows for. Your view model turns the wider range into rows and hands
them back: the rows, and the position of the first one. The control shows each row at its position.
A position that has no row yet shows the `PlaceholderTemplate` instead, until the rows arrive.

```text
 panel realizes containers ──► range ──► view model: Slice, then Map
          ▲                                              │
          └─────────────── rows (offset + rows)  ◄───────┘
```

## The source

The control reads and writes these values. They go in two pairs, and each pair only works when both
halves are there:

| Value | Direction | What it is |
| --- | --- | --- |
| `Rows` | view model to control | The rows for some of the positions, the position of the first, and the number of positions in the whole list. |
| `Range` | control to view model | The positions on screen, and the wider range that the control wants rows for. |
| `ScrollRequest` | view model to control | The position that the view model wants to show, or none. |
| `ScrollHandled` | control to view model | The position of the request that the control handled. |

The values live together in one object, an `IVirtualItemsSource`. The view model gives that object
to the view, and the view binds one property. The control attaches to all of the values when you set
`Source`, and detaches when you set it again or clear it.

The count of the whole list travels inside `Rows` rather than as a value of its own, so a change of
both reaches the control as one update and the control never shows a count with the rows of a
different instant.

The control follows the source only while it is in a visual tree. When the view leaves the tree, as
it does when a tab closes or a page navigates away, the control removes its handlers from the
source. A view model that outlives its view therefore does not keep the view alive. When the view
returns to a tree, the control reads the source again and catches up with whatever changed.

## The view model

`CreateVirtualItemsSource` makes the source from a `ReactiveCollection`. It slices the collection
with the range that the control wants rows for, builds the rows with `Map`, and counts the keys. It is an extension method of the `IOutput` that `ViewModelBase` gives your constructor,
in `MorseCode.Avalonia.Controls`:

```csharp
StreamSink<int> scrollTo = Stream.CreateSink<int>();

IVirtualItemsSource source = output.CreateVirtualItemsSource(
    collection: collection,
    project: key => new RowViewModel(key, /* bindables built from collection.StateCell(key) */),
    scrollRequests: scrollTo,
    onEvicted: static row => row.Dispose());

IBindableAction<int> scrollToAction = output.BindableFactory.CreateBindableAction(scrollTo);
```

Expose `source` on your view model interface.

`Map` builds a row for a key the first time the key is inside the slice. It keeps the row for a
key that has left until more than `retainedBeyondTheView` keys have left (512 by default), and
then it calls `onEvicted` for the oldest. Scrolling back over rows you have just seen is free, and
the number of live view models stays bounded however long the list is. The helper registers
the `MappedItems` with the output, so disposing of the view model releases the rows that are still
in use. You cannot forget it.

The count, the offset, and the rows come from one cell, so they always agree. The rows are the list
that `Map` gives, passed on without a copy. The control reads that list when it needs a row, and not when it
receives the value, so a list that you build yourself must never change after you hand it over.
Make a new list for each change.

### What the control reports

The control writes a `ReportedRange` to the source. Everything in it comes from the same instant.
It holds two ranges:

| Range | What it is |
| --- | --- |
| `Visible` | The positions on screen. A row that is only partly on screen counts. |
| `Constructed` | The positions that the control wants rows for: the visible ones, and more on each side, so that a scroll finds its rows ready. |

Both ranges stay inside the list. `CreateVirtualItemsSource` builds the rows of `Constructed` for
you. The source that it returns carries the type of your rows, and your view model can read the
report and the rows from it:

```csharp
IVirtualItemsSource<RowViewModel> source = output.CreateVirtualItemsSource(
    collection: collection,
    project: key => new RowViewModel(key),
    scrollRequests: scrollTo);

Cell<IReadOnlyList<RowViewModel>> onScreen = source.VisibleRows;
Cell<IReadOnlyList<RowViewModel>> built = source.ConstructedRows;
Cell<ReportedRange> reported = source.Reported;
```

| Member | What it is |
| --- | --- |
| `VisibleRows` | The rows on screen, in order. It changes only when a different row shows. |
| `ConstructedRows` | The rows that the control was given. It is the list in `Rows`, with the type of your rows. |
| `Reported` | The last `ReportedRange`. It changes at every step of a scroll. |

Expose the source to the view as an `IVirtualItemsSource`. The typed members are for the view model.
The source can hold more rows than `ConstructedRows`, because `Map` keeps the row of a key that has
scrolled out. If you need every row that exists, collect them in `project` and drop them in
`onEvicted`.

`Visible` is exact with the default panel. A panel that makes containers for rows that are not on
screen reports those too.

`VisibilityOf` tells how much of one row is on screen, from 0.0 for none of it to 1.0 for all of
it. It is the share of the row's area that nothing above the list clips, so a row that is half
scrolled out gives 0.5. A position that is not in `Visible` gives 0.0.

```csharp
Cell<double> shareOfRowSeven = source.Reported.Map(static range => range.VisibilityOf(position: 7));
```

Because that share changes with every pixel, the control reports at every step of a scroll. The two
ranges change far less often. `CreateVirtualItemsSource` on a collection sends no rows for a report
that leaves `Constructed` as it was.

### When the rows do not come from a collection

Another overload leaves the rows to you. You supply one `Cell<VirtualRows>` that
holds the count, the offset, and the rows, and a `CellSink<ReportedRange>` that receives what the
control reports. Make that cell from one lift, so the three always agree:

```csharp
CellSink<ReportedRange> reported = Cell.CreateSink(ReportedRange.Empty);

IVirtualItemsSource source = output.CreateVirtualItemsSource(
    rows: rows,
    reportedRange: reported,
    scrollRequests: scrollTo);
```

Build rows for `Constructed`, and stop at your own count. The range is inside the list as the
control knew it, and your list can have changed since. Calm it first, or you will build the rows
again at every step of a scroll:

```csharp
Cell<ItemRange> constructed = reported.Map(static range => range.Constructed).Calm();
```

Both overloads take an optional `Stream<Unit>` of clears. See
[Scroll from the view model](#scroll-from-the-view-model).

## The view

```xml
<UserControl xmlns:mc="clr-namespace:MorseCode.Avalonia.Controls;assembly=MorseCode.Avalonia.Controls">

    <mc:VirtualizingItemsControl Source="{Binding Source}">
        <mc:VirtualizingItemsControl.ItemTemplate>
            <DataTemplate x:DataType="vm:IRowViewModel">
                <local:RowView />
            </DataTemplate>
        </mc:VirtualizingItemsControl.ItemTemplate>

        <mc:VirtualizingItemsControl.PlaceholderTemplate>
            <DataTemplate>
                <Border Height="48" />
            </DataTemplate>
        </mc:VirtualizingItemsControl.PlaceholderTemplate>
    </mc:VirtualizingItemsControl>

</UserControl>
```

| Property | What it does |
| --- | --- |
| `Source` | The one object that the control reads from and reports to. |
| `PlaceholderTemplate` | What a position shows until its row arrives. |
| `ItemTemplate` | What a row shows, as on any items control. |

### Scroll from the view model

A view model asks for a scroll through the `ScrollRequest` of its source, and the view doesn't call
`ScrollIntoView` itself. The request is a value and not an event, so it isn't lost when the view
model asks before the view is bound, or before the list has laid out. The control keeps the request
until it can use it, scrolls, and then reports the position it handled in `ScrollHandled`. The
view model clears the request when it receives the report, and only if the request is still that
position.

`CreateVirtualItemsSource` takes a `Stream<int>` of positions. The request starts as `None`, each
position makes it `Some(position)`, and a report of that same position clears it. The graph behind
it is an `Accum` over the positions, the clears, and the reports. You don't make
the sink for the reports, because it is always the same.

Both overloads take an optional `Stream<Unit>` of clears. Each firing of it sets the request to
`None`, so the view model can withdraw a request that the control didn't use yet. When a request
and a clear come in one transaction, the request wins.

The control sets `ScrollHandled` to `Some(position)` and then straight back to `None`. The second
write makes the same position a change again, so a second request for it is reported too. The view
model ignores the `None`.

Code runs the action with `scrollToAction.Execute(position)`, and a view binds a `Command` to it as it
does to any `ICommand`.

The request is a position and not an item, because the control has a row only for the positions
that are on screen. The scroll brings the position into view, as `ScrollIntoView` does.

### Scroll to a key

A view model usually knows the key of the item it wants to show, not its position, and positions
move when the collection changes. `CreateVirtualItemsSourceByKey` takes a `Stream<TKey>` instead of
a `Stream<int>`, and looks the position up in the collection inside the transaction of the request:

```csharp
StreamSink<OrderId> scrollTo = Stream.CreateSink<OrderId>();

IVirtualItemsSource source = output.CreateVirtualItemsSourceByKey(
    collection: collection,
    project: key => new RowViewModel(key, /* ... */),
    scrollRequests: scrollTo,
    onEvicted: static row => row.Dispose());
```

A key that the collection doesn't have at that instant makes no request. Everything else is the
same as with positions: the request waits for the control, the control reports it, and `clears`
withdraw it. The method has its own name rather than being an overload, because a collection keyed
by `int` would make the two overloads indistinguishable.

- A request for a position beyond `TotalCount` waits until the count grows, and it stays until then.
  If the list is reloaded later and is long enough by then, the control scrolls to the old position
  at that point, which can surprise the user. Withdraw a request that has gone stale with the
  `clears` stream. A negative position can never apply, so the control reports it at once and does
  not scroll.
- A new request replaces one that is waiting. The last request wins.
- A request that the view model makes while a report is on its way is kept. The report names the
  position it handled, and the view model clears the request only if it is still that position. The
  one gap left is a new request for the same position inside that window, which the report clears.
  The position was just shown, so nothing is lost.
- The control scrolls once for each request. A change of `TotalCount` before the view model has
  cleared the request does not scroll again.
- The tests of the control use these methods with a real SodaFlow graph, including a request made
  before the view binds. The graph of the request has tests of its own.

### Bind the row view to its row

When the rows change, the control keeps the container at each position and hands it the new row.
It does not ask Avalonia for a new container. A XAML `DataTemplate` keeps the view it already
built and gives it the new row as its `DataContext`, so everything the view shows has to come from
a binding. That is how a XAML view normally works, so most views need no change.

The control does this to keep the list where it is. A panel that loses every container on screen
has to estimate its position from an average row height, and after a jump that estimate can be a
different part of the list.

It has these consequences:

- A `FuncDataTemplate` created with `supportsRecycling: true` reuses its view in the same way, so
  it must also read from the data context. One that sets values while it builds the view goes on
  showing the old row. With the default, `supportsRecycling: false`, the template builds a new view
  for every row, and values set while building are correct.
- State that lives in the view and is not bound stays with the position, not with the row. Focus is
  the common case: after a row is removed above it, the focused container shows the next row.
- `ContainerPrepared` fires when Avalonia makes a container for a position. It does not fire when
  the row at a position changes.

An item that is itself a `Control` is its own container, so the control cannot hand it a different
item. Avalonia replaces the container of such a position, as it does in any `ItemsControl`.

### Give the placeholder the height of a row

Avalonia fills the screen with whatever height the rows have. A placeholder with no height looks
like many short rows, so Avalonia would ask for far more positions than fit on the screen, and the
view model would build rows nobody can see. Make the placeholder about as high as a row.

If rows differ in height, use a typical height. Avalonia corrects its estimate as real rows
arrive.

### The control decides how far ahead to ask

The control asks for rows beyond the screen, so that a scroll finds them ready. It asks for one
screen of positions on each side, and never fewer than 10. A scroll shows no placeholder when its
step is no longer than that, so a step of one screen, such as Page Down, shows none at any window
size. There is no setting for it.

A row that is added at an end of the list that is on screen does not show as a placeholder first.
A source made from a collection delivers the row together with the new count, because its slice
reaches past the end of the list. A list that is read at its end while rows arrive, such as a log,
is the usual case. A view model that builds its own rows gets there in two steps: the control
reports again as soon as it sees the new count, and the view model answers with the row. With the
default panel that happens before the next layout.

### The control uses the style of `ListBox`

The control borrows the template of `ListBox`, which gives it a scroll viewer. As a result, a style
in your application with the selector `ListBox` also applies to the control. Give the control a
class or a name if you need to style it apart from your list boxes.

### Do not set `ItemsSource`

The control gives Avalonia its own list, one position per item. Setting `ItemsSource` throws an
`InvalidOperationException`. Set `Source` instead.

## What to expect

- **A fast scroll can show placeholders for a moment.** The range goes to the view model, the
  graph runs, and the rows come back through the binding scheduler. That takes a little time. The
  control asks one screen ahead, so only a step longer than a screen shows them.
- **Rows are built on the thread that sends the range.** The view sends it from the UI thread.
  `Map` runs inside the transaction, so a row that is heavy to build holds up that one transaction.
- **Selection is not part of the control.** It is an `ItemsControl` with no selected item. If you
  need selection, keep it in the view model, by key, and show it from the row.
- **An insert above the visible rows moves rows.** The control shows each row at its new position,
  in the container that is already there. The scroll position stays, and the rows on screen shift
  by one. Avalonia does not keep the scroll position on the row that was on screen.
- **A change of the count by more than 512 positions resets the list.** Avalonia clears its
  containers and makes them again, which is cheaper than an event for every position.

## Targets and dependencies

The control is in `MorseCode.Avalonia.Controls`, which targets `net8.0` and `net10.0`. It depends on
SodaFlow, `SodaFlow.Bindable.ObjectModel`, `SodaFlow.Collections`, and `MorseCode.Mvvm` 0.3.0 or
later. The source is made of SodaFlow bindable values, and `CreateVirtualItemsSource` works with the
`IOutput` of a `ViewModelBase`.
