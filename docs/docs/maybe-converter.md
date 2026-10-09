---
title: MaybeConverter
---

# MaybeConverter

`MaybeConverter`, in `MorseCode.Avalonia.Controls`, lets a control bind to a view model property of
type `Maybe<T>`. View models on SodaFlow use `Maybe<T>` rather than `null` for a value that can be
missing. Controls use `null`: a combo box with no selected item reports `null`, for example. Put
the converter on every binding where the control can give `null`.

## Using it

```xml
<UserControl xmlns:mc="clr-namespace:MorseCode.Avalonia.Controls;assembly=MorseCode.Avalonia.Controls">

    <mc:ComboBox ItemsSource="{Binding Courses.Value}"
                 SelectedItem="{Binding Course.Value, Mode=TwoWay,
                                Converter={x:Static mc:MaybeConverter.Instance}}" />

</UserControl>
```

Here `Course.Value` is a `Maybe<CourseOption>`. One instance serves every `Maybe<T>`, so there is
no converter to write for each type.

## What it converts

Toward the control:

- `Some(value)` gives `value`.
- `None` gives `null`.

Toward the view model, the binding tells the converter the type of the property, such as
`Maybe<CourseOption>`:

- `null` gives `None` of that type.
- A value gives `Some(value)` of that type, also when the value is of a type derived from `T`.

## Errors

A value of the wrong type is a mistake in the view, not a missing value, so the converter doesn't
hide it. It returns a binding error instead, and the binding shows the error and writes nothing.
That happens when:

- the view model's value isn't a `Maybe<T>`;
- the property being written isn't a `Maybe<T>`;
- the control gives a value that isn't a `T`.

## What it doesn't do

The converter only adds and removes the `Maybe`. A control that needs a different type, such as a
date picker that wants a `DateTime` for a view model's `Maybe<LocalDate>`, still needs a converter
of its own.
