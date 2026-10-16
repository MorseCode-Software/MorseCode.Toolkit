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

The control has no reference to SodaFlow's collections. It talks in positions, so it works with any
view model that can answer "give me the rows for positions *a* to *b*". The recipe below uses
`Slice` and `Map`, which is the case it was made for.

## How it works

The control has a position for every item in the whole list. That gives you a scroll bar of the
right length, a scroll area of the right size, and a working `ScrollIntoView`, as with any list.

Avalonia's `VirtualizingStackPanel` makes containers only for the positions that are on screen.
The control watches those containers and reports their positions, widened by `Buffer` positions on
each side, in `VisibleRange`.

Your view model turns that range into rows and hands them back in `Rows`: the rows, and the
position of the first one. The control shows each row at its position. A position that `Rows`
does not cover yet shows the `PlaceholderTemplate` instead, until the rows arrive.

```text
 panel realizes containers ──► VisibleRange ──► view model: Slice, then Map
          ▲                                                      │
          └──────────────── Rows (offset + rows)   ◄─────────────┘
```

## The view model

Keep the range in a cell, slice the collection with it, and build the rows with `Map`:

```csharp
CellSink<int> offset = Cell.CreateSink(0);
CellSink<int> limit = Cell.CreateSink(0);

MappedItems<IRowViewModel> rows = collection
    .Slice(offsetCell: offset, limitCell: limit)
    .Map(
        project: key => new RowViewModel(key, /* bindables built from collection.StateCell(key) */),
        onEvicted: static row => row.Dispose());

// The offset and the rows come from the same transaction, so they always agree.
Cell<VirtualRows> rowsAtOffset = offset.Lift(
    rows.Items,
    static (first, items) => new VirtualRows(first, items.Cast<object?>().ToList()));

// The view writes this property. It sends both values in one transaction.
public ItemRange VisibleRange
{
    set => Transaction.RunVoid(() =>
    {
        offset.Send(value.First);
        limit.Send(value.Count);
    });
}
```

`Map` builds a row for a key the first time the key is inside the slice. It keeps the row for a
key that has left until more than `retainedBeyondTheView` keys have left (512 by default), and
then it calls `onEvicted` for the oldest. Scrolling back over rows you have just seen is free, and
the number of live view models stays bounded however long the list is. Dispose the `MappedItems`
with the view model, so it releases the rows that are still in use.

Expose `rowsAtOffset` and the count of the collection through your usual bindables.

## The view

```xml
<UserControl xmlns:mc="clr-namespace:MorseCode.Avalonia.Controls;assembly=MorseCode.Avalonia.Controls">

    <mc:VirtualizingItemsControl TotalCount="{Binding Count.Value}"
                                 Rows="{Binding RowsAtOffset.Value}"
                                 VisibleRange="{Binding VisibleRange, Mode=OneWayToSource}"
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
| `TotalCount` | The number of positions in the whole list. |
| `Rows` | The rows for some of the positions, and the position of the first. |
| `VisibleRange` | The positions on screen, with `Buffer` more on each side. The control sets it. Bind it one way to the source. |
| `Buffer` | How many positions to add on each side of `VisibleRange`. The default is 10. |
| `PlaceholderTemplate` | What a position shows until its row arrives. |
| `ItemTemplate` | What a row shows, as on any items control. |

### Give the placeholder the height of a row

Avalonia fills the screen with whatever height the rows have. A placeholder with no height looks
like many short rows, so Avalonia would ask for far more positions than fit on the screen, and the
view model would build rows nobody can see. Make the placeholder about as high as a row.

If rows differ in height, use a typical height. Avalonia corrects its estimate as real rows
arrive.

### Do not set `ItemsSource`

The control gives Avalonia its own list, one position per item. Setting `ItemsSource` throws an
`InvalidOperationException`. Bind `TotalCount` and `Rows` instead.

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

## Targets and dependencies

The control is in `MorseCode.Avalonia.Controls`, which targets `net8.0` and `net10.0`. It adds no
dependency. The recipe above needs `SodaFlow.Collections`, which your view model project already
has if it uses a `ReactiveCollection`.
