---
title: API Reference
---

# API Reference

These pages are generated from the XML documentation comments in the source, so they track
the code exactly. Pick a namespace from the sidebar, or start with the types you will use most:

| Type | Namespace | What it is |
| --- | --- | --- |
| @MorseCode.StagedConstruction.Constructor | `MorseCode.StagedConstruction` | Makes the final handle a base gives its subclass. |
| @MorseCode.StagedConstruction.Stage | `MorseCode.StagedConstruction` | Makes a stage from a function. |
| @MorseCode.StagedConstruction.Constructor`1 | `MorseCode.StagedConstruction` | The handle that gives a base's values to the subclass constructor. |
| @MorseCode.StagedConstruction.Stage`3 | `MorseCode.StagedConstruction` | The handle for a stage after the first. |
| @MorseCode.Mvvm.ViewModelBase | `MorseCode.Mvvm` | The base a view model builds on. |
| @MorseCode.Mvvm.Disposable | `MorseCode.Mvvm` | Composite, action, and empty disposables. |
| @MorseCode.Controls.ViewModels.VirtualItemsSourceExtensions | `MorseCode.Controls.ViewModels` | Makes the source a view model gives to `VirtualizingItemsControl`. |
| @MorseCode.Controls.ViewModels.IVirtualItemsSource | `MorseCode.Controls.ViewModels` | What the control binds to: the rows, the scroll request, and the two actions it reports through. |
| @MorseCode.Controls.UI.Avalonia.VirtualizingItemsControl | `MorseCode.Controls.UI.Avalonia` | A list that asks the view model for rows only where the user can see them. |
| @MorseCode.Controls.UI.Avalonia.ComboBox | `MorseCode.Controls.UI.Avalonia` | A `ComboBox` that applies a new list and a new selected item together. |
| @MorseCode.Controls.UI.Avalonia.MaybeConverter | `MorseCode.Controls.UI.Avalonia` | Binds a control to a view model property of type `Maybe<T>`. |

## How the API is split across packages

`MorseCode.StagedConstruction` has no dependencies. It holds the types for construction in
ordered stages, and it knows nothing about view models or SodaFlow.

`MorseCode.Mvvm` uses those types in @MorseCode.Mvvm.ViewModelBase, and depends on
`MorseCode.StagedConstruction` and on
[SodaFlow](https://github.com/MorseCode-Software/SodaFlow).

`MorseCode.Controls.ViewModels` holds what a view model gives to a control of this toolkit, and
the methods that build it. It depends on `MorseCode.Mvvm` and SodaFlow, and on no UI framework, so
the project of your view models can reference it.

`MorseCode.Controls.UI.Avalonia` holds the Avalonia controls. It depends on
`MorseCode.Controls.ViewModels`, so a project of views gets that package too.

`MorseCode.Analyzers` has no API to call, so it has no pages here. Its rules have pages of their
own. See [Which package do I install?](../docs/packages.md).
