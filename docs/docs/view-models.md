---
title: View models
---

# View models

A view model that SodaFlow builds holds subscriptions into the graph: one for each bindable value,
command, and listener that it exposes. It must release all of them when it is disposed, and it must
not read a part of itself before that part exists. `MorseCode.Mvvm` does both, with
[staged construction](staged-construction.md).

## The shape

Every view model has the same shape:

1. A static `Create` method calls `ViewModelBase.CreateBase`. It builds every value in local
   variables, and registers the subscriptions it makes.
2. `Create` calls `construct.Construct` with a function that calls the private constructor.
3. The constructor gives the `ViewModelBase` it gets to the protected constructor of
   `ViewModelBase`, and then only assigns.

[Getting started](getting-started.md) has a complete example.

## CreateBase

```csharp
public static TResult CreateBase<TResult>(
    IBindingScheduler bindingScheduler,
    StageContinuation<IOutput, Constructor<ViewModelBase>, TResult> continuation)
```

The base has one stage. It gives the view model two things:

- An `IOutput`, for registrations.
- A `Constructor<ViewModelBase>`, the handle that makes the base.

`bindingScheduler` is the scheduler for the bindable values that the output's `BindableFactory`
makes. Capture it on the UI thread with `SynchronizationContextBindingScheduler.Capture()`. A test
passes `BindingScheduler.Immediate`. A null scheduler fails at the call to `CreateBase` with an
`ArgumentNullException`.

Call `CreateBase` inside `Transaction.Run` when the graph you build must be created in one
transaction, as in the examples here.

## IOutput

`IOutput` is how the view model registers what it makes. It has three members:

| Member | What it registers |
| --- | --- |
| `AddListener(listener)` | A SodaFlow weak listener. `Dispose` calls `Unlisten` on it. |
| `AddDisposable(disposable)` | Any `IDisposable`. `Dispose` disposes it. |
| `BindableFactory` | Makes bindable values and actions, and registers each one it makes. |

`AddListener` and `AddDisposable` return their argument, so you can register in the expression that
makes the object:

```csharp
IListener listener = output.AddListener(count.Listen(Console.WriteLine));
```

`AddListener` keeps a reference to the listener. The garbage collector therefore does not remove a
weak listener while the view model is alive.

### BindableFactory

`output.BindableFactory` has the same methods as SodaFlow's `IBindableFactory`:

- `CreateOneWay`, `CreateTwoWay`, and `CreateOneWayToSource` for bindable values.
- `CreateBindableAction` for commands, with or without a parameter, and with an optional cell
  that says whether the command is enabled.

The methods differ from a plain factory in one way: each one registers what it makes. You never
call `AddDisposable` for a bindable value.

## Registration closes when construction ends

`construct.Construct` gives the finished base to the constructor, and the registrations stay open
while that constructor runs. The constructor of a view model that derives from `ViewModelBase` can
still add a subscription of its own to `output`, and `Dispose` releases it with the rest.

The registrations close in one of three ways:

- The constructor returns.
- The constructor throws. The base releases every registration made so far, and the exception goes to
  the caller unchanged.
- `Dispose` runs, including a call from inside the constructor. It releases the registrations made
  so far.

After that, each member of `IOutput` fails with an `InvalidOperationException`. The
`BindableFactory` methods check this before they make the object, so a refused call attaches
nothing to the graph. A second call to `Construct` also fails with an `InvalidOperationException`,
because two view models cannot own the same registrations.

Together these mean nothing the construction made can be left out of `Dispose`, and nothing can be
added to it later.

## IViewModel

```csharp
public interface IViewModel : IDisposable, INotifyPropertyChanged;
```

`IViewModel` is what a view model shows to its view. A view model that derives from
`ViewModelBase` implements it through the base: the protected constructor takes the registrations
of the base that `Construct` gives, and `Dispose` on the view model releases them.

One view model only can take a base. A second call to the protected constructor with the same base
fails with an `InvalidOperationException`, because two view models cannot own the same
registrations. A null base fails with an `ArgumentNullException`.

A view model that must derive from a different class can keep the base in a field instead, and
implement `IViewModel` itself by sending its `Dispose` and `PropertyChanged` to the kept base.

### Why is INotifyPropertyChanged there?

`PropertyChanged` never occurs, and the base keeps no handler. The properties of a view model do
not change, and each bindable value sends its own notifications.

The event is there because of WPF. For a binding source that does not implement
`INotifyPropertyChanged`, WPF watches the source through a `PropertyDescriptor`, and that watcher
keeps the source alive after the view closes. Implementing the interface prevents the leak.
**Do not remove the interface or the event.**
