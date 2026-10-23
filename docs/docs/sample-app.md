---
title: Sample application
---

# Sample application

`samples/Workbench` is a small Avalonia app that uses three parts of this toolkit
together. It shows a list of 500,000 items. Each row has a view model that is costly to build, and
the app builds one only for a row that you can see.

The window has two counters above the list:

- **Built** is how many row view models the app has made since it started.
- **Alive** is how many exist now.

Scroll the list and watch both. Click **Scroll far down the list** to jump over hundreds of
thousands of rows. Built grows by the rows of one screen, not by the distance of the jump. Alive
stays small. The list keeps the last 64 rows that left the screen, and it disposes an older row
when a newer one leaves.

Run it from the root of the repository:

```bash
dotnet run --project samples/Workbench/MorseCode.Toolkit.Samples.Workbench
```

> [!NOTE]
> The sample uses the published packages of this repository (`MorseCode.Mvvm`,
> `MorseCode.StagedConstruction`, `MorseCode.Avalonia.Controls`, and `MorseCode.Analyzers`) at a
> pinned version, and not the projects under `src/`. It has its own solution,
> `samples/Workbench/MorseCode.Toolkit.Samples.Workbench.slnx`, and its own workflow. A change under
> `src/` does not affect it until someone changes the version that it pins.

## What to read, and in what order

| File | What it shows |
| --- | --- |
| `Program.cs`, `CompositionRoot.cs`, and `App.axaml.cs` | Pure dependency injection. There is no container. One place builds the view model and gives it what it needs, and the scheduler is captured on the UI thread. `App` gives the view model to the window as its data context, and disposes it when the application exits. The window builds nothing. |
| `WorkbenchViewModel.cs` | The view model of the window: `ViewModelBase`, and the recipe of `VirtualizingItemsControl`. |
| `TrackedViewModelBase.cs` | A base with two stages. |
| `RowViewModel.cs` | A row, built on that base. |
| `RowTracker.cs` | The two counters, as folds over what the rows report. |
| `MainWindow.axaml` | The view. It holds only markup, and it binds the control and the button to the view model that it receives. |

## ViewModelBase and the lifecycle

`WorkbenchViewModel.Create` calls `ViewModelBase.CreateBase`, which gives it an `IOutput`. Every
subscription and bindable value the view model makes goes through that output:

- `output.AddDisposable` registers the `MappedItems` that builds the rows.
- `output.BindableFactory` makes the bindable values for the rows, the range, and the two counters.

Nothing is released by hand. When the application exits, `App` calls `Dispose` on the view model, and that releases all of it.
It releases the last registration first. The `MappedItems` is registered before the bindable
values that read it, so the bindable values stop first and the rows stop last. The same rule runs
in each row: `TrackedViewModelBase` registers the "this row is gone" notice first, so it runs
after every bindable value of the row has stopped.

The registrations close when the constructor returns. The tests check that disposing the view model
disposes every row. They do not start the application, so they do not check the call in `App`.

## Staged construction

A row must register a bindable value for its title, but only the row knows which cell holds the
title, and only the base can register the value. One exchange cannot do both, so
`TrackedViewModelBase` has two stages:

1. `CreateBase` calls `ViewModelBase.CreateBase`, registers the counting, and gives the row an
   `IOutput` and a `Stage<Cell<string>, IOneWayBindableValue<string>, Constructor<Values>>`.
2. The row calls `Advance` on the stage with the cell of its title. The stage makes the bindable
   value, registers it, and gives it back with a `Constructor` for the last step.
3. The row makes its other bindable values, and calls `Construct`, which runs the private
   constructor of the row.

Every value is a local variable until `Construct` runs, so nothing can read a row that is only
partly built. `construct.Select` adds the values of the second stage to the `ViewModelBase` of the
first. [Staged construction](staged-construction.md) and
[Bases with several stages](multi-stage.md) explain the pattern.

## VirtualizingItemsControl

The view model follows the recipe on the [VirtualizingItemsControl](virtualizing-items-control.md)
page:

- The window binds the `VisibleRange` of the control, one way to the source, to a bindable value.
  That value writes a `CellSink<ItemRange>`.
- Two cells, the offset and the limit, are derived from it. Both change in the same transaction.
- `collection.Slice(offsetCell, limitCell)` keeps only the keys in the range, and `Map` builds a
  `RowViewModel` for each one. `Map` keeps a row that left the range, up to a bound, and then
  calls `onEvicted` to dispose it.
- The `Rows` that the control reads is a `VirtualRows` that lifts the offset with
  `MappedItems.Items`, so the offset and the rows always come from the same transaction.

The placeholder is as high as a row. If it were not, Avalonia would ask for more rows than fit on
the screen.

## Scrolling from the view model

The button **Scroll far down the list** has no code in the window. It binds its `Command` to the
`Jump` action of the view model, which is an `IBindableAction`. The view model decides where to go:

- `Jump` fires a stream. A `Map` turns each firing into `Maybe.Some(position)`, 90 percent of the way
  down the list.
- That stream is merged with the edits that the view writes, and `Hold` keeps the last value, which
  starts at `Maybe.None`.
- The result is the `ScrollRequest` bindable value, a two-way `ITwoWayBindableValue<Maybe<int>>`. The
  list binds its `ScrollRequest` property to it.
- The list scrolls to the position, and writes `Maybe.None` back. That edit clears the request.

The request is a value and not an event, so a jump that the view model asks for before the window
has laid out is not lost. The list keeps it until it can scroll. The control page explains the
rules, including the one case that can lose a request. See
[VirtualizingItemsControl](virtualizing-items-control.md).

## The tests

`samples/Workbench/MorseCode.Toolkit.Samples.Workbench.Tests` checks the behavior, not the layout:

- With no UI, a range builds exactly the rows in it, starting at the offset.
- A row that leaves the range is disposed only after more rows than the bound have left.
- Disposing the view model disposes every row.
- The `Jump` action sets the request, and the write of `None` clears it.
- With the real window and a headless Avalonia, the window builds a handful of rows. A click on the
  button, with the pointer events of Avalonia, scrolls the list hundreds of thousands of positions
  and builds only the rows it lands on.
