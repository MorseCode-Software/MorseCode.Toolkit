# MorseCode.Mvvm

Building blocks for view models written in a functional reactive style on SodaFlow.

```bash
dotnet add package MorseCode.Mvvm
```

`ViewModelBase` is the base a view model builds on with MorseCode.StagedConstruction. The view model
registers every subscription and bindable it makes during construction, and one `Dispose` releases
them all, once, in order, and without one failure leaking the rest. Registration closes when the
view model is constructed, so nothing can be left out or slipped in later.

The package also holds `IViewModel`, the interface a view model shows to its view, and the
`Disposable` helpers that `ViewModelBase` uses.

## Documentation

- [View models](https://morsecode-software.github.io/MorseCode.Toolkit/docs/view-models.html)
- [Disposal and lifetimes](https://morsecode-software.github.io/MorseCode.Toolkit/docs/disposal.html)
- [The Disposable helpers](https://morsecode-software.github.io/MorseCode.Toolkit/docs/disposable.html)
- [API reference](https://morsecode-software.github.io/MorseCode.Toolkit/api/index.html)

The source is at <https://github.com/MorseCode-Software/MorseCode.Toolkit>, under the BSD 3-Clause
license.
