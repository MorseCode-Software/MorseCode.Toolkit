---
title: Which package do I install?
---

# Which package do I install?

The packages version independently, and all of them are pre-1.0.

| Package | Install it when | Depends on |
| --- | --- | --- |
| `MorseCode.Mvvm` | You write view models on SodaFlow. | `SodaFlow.Bindable.ObjectModel`, `MorseCode.StagedConstruction` |
| `MorseCode.StagedConstruction` | You want to build immutable objects in ordered stages. | Nothing |
| `MorseCode.Avalonia.Controls` | Your views are Avalonia, and bind to view models on SodaFlow. | `Avalonia` |

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

Avalonia controls for views that bind to view models on SodaFlow. Each one is a drop-in
replacement for the Avalonia control of the same name, and should be used in its place. It holds
`ComboBox`, which applies a new list and a new selected item together. See [ComboBox](combo-box.md).

It targets `net8.0` and `net10.0`, as Avalonia 12 does, rather than the older targets of the other
packages.

## Version ranges

`MorseCode.Mvvm` states its dependency on `MorseCode.StagedConstruction` as a range that ends
before the next major version. NuGet then refuses a pairing across a breaking change, and does not
resolve it and fail at run time.

Until 1.0.0, the API of each package can change in a minor version. Read the release notes of a
package before you update it.
