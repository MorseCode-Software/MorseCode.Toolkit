---
title: Which package do I install?
---

# Which package do I install?

The packages version independently, and all of them are pre-1.0.

| Package | Install it when | Depends on |
| --- | --- | --- |
| `MorseCode.Mvvm` | You write view models on SodaFlow. | `SodaFlow.Bindable.ObjectModel`, `MorseCode.StagedConstruction` |
| `MorseCode.StagedConstruction` | You want to build immutable objects in ordered stages. | Nothing |
| `MorseCode.Avalonia.Controls` | Your views are Avalonia, and bind to view models on SodaFlow. | `Avalonia`, `SodaFlow`, `SodaFlow.Bindable.ObjectModel`, `SodaFlow.Collections` |
| `MorseCode.Analyzers` | You want the build to report code that breaks the conventions MorseCode writes C# with. | Nothing |

## MorseCode.Mvvm

```bash
dotnet add package MorseCode.Mvvm
```

Building blocks for view models. It holds:

- `ViewModelBase`, which a view model builds on. It registers every subscription and bindable the
  view model makes during construction, and one `Dispose` releases them. See
  [View models](view-models.md).
- `IViewModel`, the interface a view model shows to its view.
- The `Disposable` helpers that `ViewModelBase` uses: `Composite`, `FromAction`, and `Empty`. See
  [The Disposable helpers](disposable.md).

## MorseCode.StagedConstruction

```bash
dotnet add package MorseCode.StagedConstruction
```

Construction of immutable objects in ordered stages. It has no dependency on SodaFlow, though it
was written for objects whose properties are SodaFlow graphs. See
[Staged construction](staged-construction.md).

## MorseCode.Avalonia.Controls

```bash
dotnet add package MorseCode.Avalonia.Controls
```

Avalonia controls for views that bind to view models on SodaFlow. It holds:

- `ComboBox`, a drop-in replacement for Avalonia's, which applies a new list and a new selected
  item together. Use it in its place. See [ComboBox](combo-box.md).
- `MaybeConverter` and `MaybeConverter<T, TSelf>`, which bind a control to a view model property of
  type `Maybe<T>`, one-way and two-way, turning the control's `null` into `None` and back. See
  [MaybeConverter](maybe-converter.md).
- `VirtualizingItemsControl`, which shows a long list over a SodaFlow `ReactiveCollection` and
  builds the heavy view model of a row only when the row is on screen. See
  [VirtualizingItemsControl](virtualizing-items-control.md).
- `CreateVirtualItemsSource` and `CreateScrollRequest`, extension methods of `IBindableFactory` that
  make the object a view model gives to `VirtualizingItemsControl`, and the values that ask it to
  scroll. See [VirtualizingItemsControl](virtualizing-items-control.md).

It targets `net8.0` and `net10.0`, as Avalonia 12 does, rather than the older targets of the other
packages.

## MorseCode.Analyzers

```bash
dotnet add package MorseCode.Analyzers
```

Roslyn analyzers that run in the compiler, so the build reports what they find. It holds these
rules:

- MC0001, which reports a call whose arguments aren't in the order of the method's parameters,
  when at least one argument has a name. See [Named argument order](named-argument-order.md).
- MC0002, which reports an explicit cast, such as `(int)value`, so the few that are needed stand
  out. See [Explicit cast](explicit-cast.md).

It's a development dependency: NuGet adds it with `PrivateAssets="all"`, and it doesn't flow to
the packages of a project that uses it. It needs the .NET 7 SDK or later, or Visual Studio 2022
17.4 or later.

## Version ranges

`MorseCode.Mvvm` states its dependency on `MorseCode.StagedConstruction` as a range that ends
before the next major version. NuGet then refuses a pairing across a breaking change, and does not
resolve it and fail at run time.

Until 1.0.0, the API of each package can change in a minor version. Read the release notes of a
package before you update it.
