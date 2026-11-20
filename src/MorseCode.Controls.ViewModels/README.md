# MorseCode.Controls.ViewModels

What a view model on SodaFlow gives to a control of the MorseCode toolkit, and the methods that
build it. It has no dependency on a UI framework.

```bash
dotnet add package MorseCode.Controls.ViewModels
```

Reference it from the project that holds your view models. `CreateVirtualItemsSource` and
`CreateVirtualItemsSourceByKey` build the object that `VirtualizingItemsControl` binds to, from a
SodaFlow `ReactiveCollection`, from a list, or from rows the view model makes itself.
MorseCode.Controls.UI.Avalonia depends on this package, so a project of views gets it without
asking.

## Documentation

- [VirtualizingItemsControl](https://morsecode-software.github.io/MorseCode.Toolkit/docs/virtualizing-items-control.html)
- [Which package do I install?](https://morsecode-software.github.io/MorseCode.Toolkit/docs/packages.html)
- [API reference](https://morsecode-software.github.io/MorseCode.Toolkit/api/index.html)

The source is at <https://github.com/MorseCode-Software/MorseCode.Toolkit>, under the BSD 3-Clause
license.
