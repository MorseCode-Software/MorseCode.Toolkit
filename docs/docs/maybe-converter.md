---
title: MaybeConverter
---

# MaybeConverter

View models on SodaFlow use `Maybe<T>` rather than `null` for a value that can be missing. Controls
use `null`: a combo box with no selected item reports `null`, for example. `MorseCode.Avalonia.Controls`
has two converters that bridge the gap. Which one to use depends on the direction of the binding.

| Binding | Converter |
| --- | --- |
| One-way, view model to control | `MaybeConverter.Instance` |
| Two-way | A subclass of `MaybeConverter<T, TSelf>` for the type of the value |

## One-way bindings: MaybeConverter

```xml
<UserControl xmlns:mc="clr-namespace:MorseCode.Avalonia.Controls;assembly=MorseCode.Avalonia.Controls">

    <TextBlock Text="{Binding Note.Value, Converter={x:Static mc:MaybeConverter.Instance}}" />

</UserControl>
```

`Some(value)` gives `value`, and `None` gives `null`. One instance serves every `Maybe<T>`.

It can't convert back, because nothing tells it which `Maybe<T>` to build. On a two-way binding, a
write from the control returns a binding error that names `MaybeConverter<T, TSelf>`, and nothing
reaches the view model.

## Two-way bindings: MaybeConverter&lt;T, TSelf&gt;

Close the converter for the value's type with a sealed subclass that has no members, next to the
views that use it:

```csharp
public sealed class CourseOptionConverter : MaybeConverter<CourseOption, CourseOptionConverter>;
```

Then use the subclass's `Instance`, which the base class provides:

```xml
<UserControl xmlns:mc="clr-namespace:MorseCode.Avalonia.Controls;assembly=MorseCode.Avalonia.Controls"
             xmlns:app="clr-namespace:MyApp.Views">

    <mc:ComboBox ItemsSource="{Binding Courses.Value}"
                 SelectedItem="{Binding Course.Value, Mode=TwoWay,
                                Converter={x:Static app:CourseOptionConverter.Instance}}" />

</UserControl>
```

Toward the control, `Some(value)` gives `value` and `None` gives `null`. Toward the view model,
`null` gives `None` and a value gives `Some(value)`. For example, a combo box writes `null` when its
selected item isn't in its list, and the view model gets `None`.

The converter knows `T` when it is compiled, so it uses no reflection, and trimming and
ahead-of-time compilation keep everything it needs. The subclass is what lets XAML name it, since
XAML can't name a generic type with `x:Static`.

## Errors

A value of the wrong type is a mistake in the view, not a missing value, so the converters don't
hide it. They return a binding error instead, and the binding shows the error and writes nothing.
That happens when:

- the view model's value isn't a `Maybe<T>`, or for `MaybeConverter<T, TSelf>`, isn't a
  `Maybe<T>` of its `T`;
- the property being written isn't a `Maybe<T>` of the converter's `T`;
- the control gives a value that isn't a `T`;
- `MaybeConverter` is asked to convert back.

## What they don't do

The converters only add and remove the `Maybe`. A control that needs a different type, such as a
date picker that wants a `DateTime` for a view model's `Maybe<LocalDate>`, still needs a converter
of its own.
