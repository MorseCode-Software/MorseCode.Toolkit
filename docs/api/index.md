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

## How the API is split across packages

`MorseCode.StagedConstruction` has no dependencies. It holds the types for construction in
ordered stages, and it knows nothing about view models or SodaFlow.

`MorseCode.Mvvm` uses those types in @MorseCode.Mvvm.ViewModelBase, and depends on
`MorseCode.StagedConstruction` and on
[SodaFlow](https://github.com/MorseCode-Software/SodaFlow). See
[Which package do I install?](../docs/packages.md).
