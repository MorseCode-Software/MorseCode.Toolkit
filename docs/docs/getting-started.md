---
title: Getting started
---

# Getting started

## Install

Most people want `MorseCode.Mvvm`. It brings `MorseCode.StagedConstruction` with it, and it
depends on SodaFlow's `SodaFlow.Bindable.ObjectModel` package.

```bash
dotnet add package MorseCode.Mvvm
```

If you want only the construction pattern, without view models or SodaFlow, install
`MorseCode.StagedConstruction` alone. [Which package do I install?](packages.md) has the
details.

Both packages target `net10.0`.

## Your first view model

A view model built on `MorseCode.Mvvm` has a private constructor that only assigns, and a
static `Create` method that does the work. This one counts button clicks.

```csharp
using System.ComponentModel;
using MorseCode.Mvvm;
using MorseCode.StagedConstruction;
using SodaFlow;
using SodaFlow.Bindable.ObjectModel;
using SodaFlow.Functional;

public sealed class CounterViewModel : IViewModel
{
    private readonly ViewModelBase viewModelBase;

    private CounterViewModel(
        ViewModelBase viewModelBase,
        IOneWayBindableValue<int> count,
        IBindableAction increment)
    {
        this.viewModelBase = viewModelBase;
        this.Count = count;
        this.Increment = increment;
    }

    public IOneWayBindableValue<int> Count { get; }

    public IBindableAction Increment { get; }

    public static CounterViewModel Create(IBindingScheduler bindingScheduler) =>
        Transaction.Run(() =>
            ViewModelBase.CreateBase(bindingScheduler, (output, construct) =>
            {
                StreamSink<Unit> increments = Stream.CreateSink<Unit>();
                Cell<int> count = increments.Accum(0, (_, total) => total + 1);

                IOneWayBindableValue<int> countValue = output.BindableFactory.CreateOneWay(count);
                IBindableAction increment = output.BindableFactory.CreateBindableAction(increments);

                return construct.Construct(viewModelBase =>
                    new CounterViewModel(viewModelBase, countValue, increment));
            }));

    public void Dispose() => this.viewModelBase.Dispose();

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add => this.viewModelBase.PropertyChanged += value;
        remove => this.viewModelBase.PropertyChanged -= value;
    }
}
```

Three things to see in it:

- The graph is built in local variables inside `Create`. Nothing reads a field of a
  `CounterViewModel` that does not exist yet, because none does.
- Every bindable value goes through `output.BindableFactory`, so the base registers it. When the
  view model is disposed, each one is released.
- `construct.Construct` closes the registrations and hands the finished base to the
  constructor. After that, `output` refuses any further registration.

The [view models](view-models.md) page explains each part. To create the view model, capture
the binding scheduler on the UI thread:

```csharp
IBindingScheduler scheduler = SynchronizationContextBindingScheduler.Capture();
CounterViewModel viewModel = CounterViewModel.Create(scheduler);
```

A test passes `BindingScheduler.Immediate` instead.

## Where to go next

- [Staged construction](staged-construction.md) explains the pattern the base is built with, and
  why a constructor chain cannot give the same guarantee.
- [Disposal and lifetimes](disposal.md) explains what `Dispose` does and does not do.
