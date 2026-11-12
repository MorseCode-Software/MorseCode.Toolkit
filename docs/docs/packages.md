---
title: Which package do I install?
---

# Which package do I install?

The packages version independently, and all of them are pre-1.0.

| Package | Install it when | Depends on |
| --- | --- | --- |
| `MorseCode.Mvvm` | You write view models on SodaFlow. | `SodaFlow.Bindable.ObjectModel`, `MorseCode.StagedConstruction` |
| `MorseCode.StagedConstruction` | You want to build immutable objects in ordered stages. | Nothing |
| `MorseCode.Controls.UI.Avalonia` | Your views are Avalonia, and bind to view models on SodaFlow. | `Avalonia`, `SodaFlow`, `SodaFlow.Bindable.ObjectModel`, `SodaFlow.Collections`, `SodaFlow.Functional`, `MorseCode.Mvvm` |
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

The package carries its own analyzer, which reports a continuation that doesn't return the result of
its handle. It runs in every project that has the package anywhere in its dependencies, so a project
that references `MorseCode.Mvvm` or `MorseCode.Controls.UI.Avalonia` gets it too, with nothing to
install. See [Continuation rules](continuation-rules.md).

## MorseCode.Controls.UI.Avalonia

```bash
dotnet add package MorseCode.Controls.UI.Avalonia
```

Avalonia controls for views that bind to view models on SodaFlow. Up to 0.2.1 this package was
`MorseCode.Avalonia.Controls`. Its id, its assembly, and its namespace changed in 0.3.0. It holds:

- `ComboBox`, a drop-in replacement for Avalonia's, which applies a new list and a new selected
  item together. Use it in its place. See [ComboBox](combo-box.md).
- `MaybeConverter` and `MaybeConverter<T, TSelf>`, which bind a control to a view model property of
  type `Maybe<T>`, one-way and two-way, turning the control's `null` into `None` and back. See
  [MaybeConverter](maybe-converter.md).
- `VirtualizingItemsControl`, which shows a long list over a SodaFlow `ReactiveCollection` and
  builds the heavy view model of a row only when the row is on screen. See
  [VirtualizingItemsControl](virtualizing-items-control.md).
- `CreateVirtualItemsSource`, an extension method of the `IOutput` of a `ViewModelBase`. It makes the
  object a view model gives to `VirtualizingItemsControl`. See
  [VirtualizingItemsControl](virtualizing-items-control.md).

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
- MC0003, which reports a primary constructor parameter of a class or a struct that isn't `in`.
  See [Primary constructor parameter](primary-constructor-parameter.md).

It's a development dependency: NuGet adds it with `PrivateAssets="all"`, and it doesn't flow to
the packages of a project that uses it. It needs the .NET 7 SDK or later, or Visual Studio 2022
17.4 or later.

## Trimming and native AOT

From 0.3.0, the .NET 8 builds of `MorseCode.StagedConstruction` and `MorseCode.Mvvm`, and both
builds of `MorseCode.Controls.UI.Avalonia`, are marked as compatible with trimming and native AOT. The
build of each package runs the .NET trim, AOT, and single-file analyzers and fails on any warning,
so code that needs reflection can't reach a release unnoticed. The .NET Framework, .NET 6, and .NET
Standard builds aren't marked, because those analyzers don't exist for them.

The mark covers the code of these packages, not SodaFlow's. A native AOT publish of an app that uses
them reports a few warnings from inside `SodaFlow.Core` 5, where it passes a value factory to
`Lazy<T>`. They come from SodaFlow, and this repository can't fix them.

## Version ranges

A package of this repository states its dependency on another package of this repository, and from
0.3.0 on its dependency on a SodaFlow package, as a range that ends before the next breaking
version. `MorseCode.Mvvm` depends on `MorseCode.StagedConstruction` this way, and
`MorseCode.Controls.UI.Avalonia` depends on `MorseCode.Mvvm` this way from 0.3.0 on.

On its own, NuGet never picks a version past the end of a range. If your project asks for one
directly, NuGet uses it and warns with NU1608, which a build that treats warnings as errors fails
on. Without the ceiling, that pairing would restore without a word and could fail at run time.

From 1.0.0 the next breaking version is the next major, so 1.4.2 gets `[1.4.2, 2.0.0)`. Below 1.0.0
it is the next minor, because a 0.x package takes its breaking changes in the minor version.
`MorseCode.Mvvm` 0.2.4, for example, depends on `MorseCode.StagedConstruction` `[0.2.1, 0.3.0)`. The
one exception is `MorseCode.Mvvm` 0.1.0, which was released with `[0.1.0, 1.0.0)` before this rule.

The dependency on Avalonia is a floor only, the version the package was built against. Before 0.3.0
the dependencies on SodaFlow were a floor only too: `MorseCode.Mvvm` 0.2.4, for example, depends on
`SodaFlow.Bindable.ObjectModel` 4.0.0 and later. With the ceiling, a SodaFlow major needs a new
release of each package here that depends on it, even when nothing here changes.

Until 1.0.0, the API of each package can change in a minor version. Read the release notes of a
package before you update it.
