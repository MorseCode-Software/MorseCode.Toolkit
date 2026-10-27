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
    static (first, items) => new VirtualRows(first, items));

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

Pass `items` to `VirtualRows` as it is. A list of rows is an `IReadOnlyList<IRowViewModel>`, and
that is accepted where an `IReadOnlyList<object?>` is expected, so no copy is needed. The control
reads the list when it needs a row, not when it receives the value, so the list must never change
after you hand it over. The list that `Map` gives for each version is a new list that nothing else
holds. If you build the list yourself, make a new one for each change.

Expose `rowsAtOffset` and the count of the collection through your usual bindables.

## The view

```xml
<UserControl xmlns:mc="clr-namespace:MorseCode.Avalonia.Controls;assembly=MorseCode.Avalonia.Controls">

    <mc:VirtualizingItemsControl TotalCount="{Binding Count.Value}"
                                 Rows="{Binding RowsAtOffset.Value}"
                                 VisibleRange="{Binding VisibleRange, Mode=OneWayToSource}"
                                 ScrollRequest="{Binding ScrollRequest.Value}"
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
| `ScrollRequest` | The position that the view model wants to show, as a `Maybe<int>`. The control scrolls there and sets it back to none. Binds two-way. |
| `Buffer` | How many positions to add on each side of `VisibleRange`. The default is 10. |
| `PlaceholderTemplate` | What a position shows until its row arrives. |
| `ItemTemplate` | What a row shows, as on any items control. |

### Scroll from the view model

A view model asks for a scroll through `ScrollRequest`, and the view doesn't call `ScrollIntoView`
itself. The request is a value and not an event, so it isn't lost when the view model asks before
the view is bound, or before the list has laid out. The control keeps the request until it can use
it, scrolls, and then writes `Maybe.None` back.

In the view model, an action takes the position, and `CreateScrollRequest` makes the two-way
bindable value that holds the request. It is an extension method of `IBindableFactory` in
`MorseCode.Mvvm`, from version 0.2.4:

```csharp
StreamSink<int> scrollTo = Stream.CreateSink<int>();

IBindableAction<int> scrollToAction = output.BindableFactory.CreateBindableAction(scrollTo);
ITwoWayBindableValue<Maybe<int>> scrollRequest = output.BindableFactory.CreateScrollRequest(scrollTo);
```

`CreateScrollRequest` takes a `Stream<int>` of positions. The value starts as `None`, each position
makes it `Some(position)`, and the write of `None` by the control clears it. The graph behind it is
a `Hold` over the positions merged with the writes of the view. You don't make the sink for the
writes of the view, because it is always the same.

An overload also takes a `Stream<Unit>` of clears. Each firing of it sets the value to `None`, so the
view model can withdraw a request that the control didn't use yet. When a request and a clear come in
one transaction, the request wins.

Code runs the action with `scrollToAction.Execute(position)`, and a view binds a `Command` to it as it
does to any `ICommand`. To scroll to an item, find its position first, for example with
`collection.IndexOf(key)`.

The request is a position and not an item, because the control has a row only for the positions
that are on screen. The scroll brings the position into view, as `ScrollIntoView` does.

- A request for a position beyond `TotalCount` waits until the count grows, and it stays until then.
  If the list is reloaded later and is long enough by then, the control scrolls to the old position
  at that point, which can surprise the user. Withdraw a request that has gone stale with the
  `clears` overload. A negative position can never apply, so the control clears it at once.
- A new request replaces one that is waiting. The last request wins.
- A second request that the view model makes in the instant between the control's scroll and the
  arrival of its `None` is cleared with the first, because the `None` doesn't say which request the
  control scrolled to. Two requests in one turn of the dispatcher are fine, and so are two requests
  that are a moment apart.
- The tests of the control use this method with a real SodaFlow graph, including a request made
  before the view binds. The tests of `CreateScrollRequest` itself are in `MorseCode.Mvvm`.

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
