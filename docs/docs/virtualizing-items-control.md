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
The control watches those containers and reports their positions, widened by `Buffer` positions on
each side. Your view model turns that range into rows and hands them back: the rows, and the
position of the first one. The control shows each row at its position. A position that has no row
yet shows the `PlaceholderTemplate` instead, until the rows arrive.

```text
 panel realizes containers ──► range ──► view model: Slice, then Map
          ▲                                              │
          └─────────────── rows (offset + rows)  ◄───────┘
```

## The source

The control reads and writes five values. They go in two pairs, and each pair only works when both
halves are there:

| Value | Direction | What it is |
| --- | --- | --- |
| `TotalCount` | view model to control | The number of positions in the whole list. |
| `Rows` | view model to control | The rows for some of the positions, and the position of the first. |
| `VisibleRange` | control to view model | The positions on screen, with `Buffer` more on each side. |
| `ScrollRequest` | view model to control | The position that the view model wants to show, or none. |
| `ScrollHandled` | control to view model | The position of the request that the control handled. |

The values live together in one object, an `IVirtualItemsSource`. The view model gives that object
to the view, and the view binds one property. The control attaches to all five values when you set
`Source`, and detaches when you set it again or clear it.

## The view model

`CreateVirtualItemsSource` makes the source from a `ReactiveCollection`. It slices the collection
with the range that the control reports, builds the rows with `Map`, and counts the keys for
`TotalCount`. It is an extension method of `IBindableFactory`, in `MorseCode.Avalonia.Controls`:

```csharp
StreamSink<int> scrollTo = Stream.CreateSink<int>();

VirtualItemsSource source = output.AddDisposable(
    output.BindableFactory.CreateVirtualItemsSource(
        collection: collection,
        project: key => new RowViewModel(key, /* bindables built from collection.StateCell(key) */),
        scrollRequests: scrollTo,
        onEvicted: static row => row.Dispose()));

IBindableAction<int> scrollToAction = output.BindableFactory.CreateBindableAction(scrollTo);
```

Expose `source` on your view model interface as an `IVirtualItemsSource`.

`Map` builds a row for a key the first time the key is inside the slice. It keeps the row for a
key that has left until more than `retainedBeyondTheView` keys have left (512 by default), and
then it calls `onEvicted` for the oldest. Scrolling back over rows you have just seen is free, and
the number of live view models stays bounded however long the list is. The source owns the
`MappedItems`, so disposing of the source with the view model releases the rows that are still in
use.

The offset and the rows come from one cell, so they always agree. The rows are the list that `Map`
gives, passed on without a copy. The control reads that list when it needs a row, and not when it
receives the value, so a list that you build yourself must never change after you hand it over.
Make a new list for each change.

### When the rows do not come from a collection

Another overload takes the cells and the sink yourself. You supply the count, the rows, and a
`CellSink<ItemRange>` that receives the range. Make the rows and their offset from one cell, so they
agree:

```csharp
CellSink<ItemRange> range = Cell.CreateSink(ItemRange.Empty);

VirtualItemsSource source = output.BindableFactory.CreateVirtualItemsSource(
    totalCount: count,
    rows: rowsAtOffset,
    visibleRange: range,
    scrollRequests: scrollTo);
```

Both overloads take an optional `Stream<Unit>` of clears. See
[Scroll from the view model](#scroll-from-the-view-model).

## The view

```xml
<UserControl xmlns:mc="clr-namespace:MorseCode.Avalonia.Controls;assembly=MorseCode.Avalonia.Controls">

    <mc:VirtualizingItemsControl Source="{Binding Source}"
                                 Buffer="10">
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
| `Buffer` | How many positions to add on each side of the range that it reports. The default is 10. |
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
it is the one that `CreateScrollRequest` makes, an extension method of `IBindableFactory` in the same
namespace. Call it alone if you bind the values yourself.

Both overloads take an optional `Stream<Unit>` of clears. Each firing of it sets the request to
`None`, so the view model can withdraw a request that the control didn't use yet. When a request
and a clear come in one transaction, the request wins.

The control sets `ScrollHandled` to `Some(position)` and then straight back to `None`. The second
write makes the same position a change again, so a second request for it is reported too. The view
model ignores the `None`.

Code runs the action with `scrollToAction.Execute(position)`, and a view binds a `Command` to it as it
does to any `ICommand`. To scroll to an item, find its position first, for example with
`collection.IndexOf(key)`.

The request is a position and not an item, because the control has a row only for the positions
that are on screen. The scroll brings the position into view, as `ScrollIntoView` does.

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
  before the view binds. `CreateScrollRequest` has tests of its own.

### Give the placeholder the height of a row

Avalonia fills the screen with whatever height the rows have. A placeholder with no height looks
like many short rows, so Avalonia would ask for far more positions than fit on the screen, and the
view model would build rows nobody can see. Make the placeholder about as high as a row.

If rows differ in height, use a typical height. Avalonia corrects its estimate as real rows
arrive.

### The control uses the style of `ListBox`

The control borrows the template of `ListBox`, which gives it a scroll viewer. As a result, a style
in your application with the selector `ListBox` also applies to the control. Give the control a
class or a name if you need to style it apart from your list boxes.

### Do not set `ItemsSource`

The control gives Avalonia its own list, one position per item. Setting `ItemsSource` throws an
`InvalidOperationException`. Set `Source` instead.

## What to expect

- **A fast scroll can show placeholders for a moment.** The range goes to the view model, the
  graph runs, and the rows come back through the binding scheduler. That takes a little time. A
  larger `Buffer` asks for rows further ahead and hides it.
- **Rows are built on the thread that sends the range.** The view sends it from the UI thread.
  `Map` runs inside the transaction, so a row that is heavy to build holds up that one transaction.
- **Selection is not part of the control.** It is an `ItemsControl` with no selected item. If you
  need selection, keep it in the view model, by key, and show it from the row.
- **An insert above the visible rows moves rows.** The control shows each row at its new position, as
  a replacement of the rows, so Avalonia does not keep the scroll position on the row that was on
  screen.
- **A change of the count by more than 512 positions resets the list.** Avalonia clears its
  containers and makes them again, which is cheaper than an event for every position.

## Targets and dependencies

The control is in `MorseCode.Avalonia.Controls`, which targets `net8.0` and `net10.0`. It depends on
SodaFlow, `SodaFlow.Bindable.ObjectModel`, and `SodaFlow.Collections`, because the source is made of
SodaFlow bindable values.
