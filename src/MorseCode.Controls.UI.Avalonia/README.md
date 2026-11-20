# MorseCode.Controls.UI.Avalonia

Avalonia controls and converters for views that bind to view models written in a functional
reactive style on SodaFlow.

```bash
dotnet add package MorseCode.Controls.UI.Avalonia
```

Reference it from the project that holds your views. It holds:

- `ComboBox`, a drop-in replacement for Avalonia's that applies a new list and a new selected item
  together, so a view model that changes both in one update keeps the selection it chose.
- `MaybeConverter` and `MaybeConverter<T, TSelf>`, which bind a control to a view model's
  `Maybe<T>`, one-way and two-way, turning the control's `null` into `None` and back.
- `VirtualizingItemsControl`, which shows a long list and builds the view model of a row only when
  the row is on screen. The view model builds what it binds to with MorseCode.Controls.ViewModels.

## Documentation

- [ComboBox](https://morsecode-software.github.io/MorseCode.Toolkit/docs/combo-box.html)
- [MaybeConverter](https://morsecode-software.github.io/MorseCode.Toolkit/docs/maybe-converter.html)
- [VirtualizingItemsControl](https://morsecode-software.github.io/MorseCode.Toolkit/docs/virtualizing-items-control.html)
- [API reference](https://morsecode-software.github.io/MorseCode.Toolkit/api/index.html)

The source is at <https://github.com/MorseCode-Software/MorseCode.Toolkit>, under the BSD 3-Clause
license.
