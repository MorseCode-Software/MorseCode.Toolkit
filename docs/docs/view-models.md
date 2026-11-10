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

The continuation returns the `Constructed<TResult>` that `construct.Construct` gives, and
`CreateBase` returns the value inside it. Only `Construct` makes a `Constructed<TResult>`, so a
continuation that does not call it does not compile.

`CreateBase` also checks, at run time, that the value it returns is the value the constructor
returned: the same instance for a class, and an equal value for a struct. A continuation that calls
`Construct` and then returns something else fails with an `InvalidOperationException`. That is why a
base of your own that sits between `ViewModelBase` and a view model adds its values with
`construct.Select`, as [Multi-stage construction](multi-stage.md) shows. `Select` hands the
constructor's value back unchanged. A continuation that takes the `ViewModelBase` out with one
`Construct` call, and gives it to the view model through a second handle, returns a value the base
never saw, so it fails. The build reports the usual forms of this mistake before the code runs: see
[Continuation rules](continuation-rules.md).

`bindingScheduler` is the scheduler for the bindable values that the output's `BindableFactory`
makes. Capture it on the UI thread with `SynchronizationContextBindingScheduler.Capture()`. A test
passes `BindingScheduler.Immediate`. A null scheduler fails at the call to `CreateBase` with an
`ArgumentNullException`.

Call `CreateBase` inside `Transaction.Run` when the graph you build must be created in one
transaction, as in the examples here. A forward reference around `CreateBase` does the same job:
see the next section.

## A view model with a loop

Some graphs feed themselves: a cell is built from a member of the view model that is still being
made. SodaFlow's `ForwardReference<T>` gives you that view model as a looped cell. Put the forward
reference outside `CreateBase`:

```csharp
public static CounterViewModel Create(IBindingScheduler bindingScheduler) =>
    ForwardReference<CounterViewModel>.WithoutCaptures(viewModelLoop =>
        ViewModelBase.CreateBase(bindingScheduler, (output, construct) =>
        {
            // The view model does not exist yet. This cell reads its member when the loop closes.
            Cell<int> step = viewModelLoop.Map(viewModel => viewModel.Step).SwitchC();

            // ... the rest of the graph ...

            return construct.Construct(
                viewModelBase => new CounterViewModel(viewModelBase, /* ... */));
        }));
```

The function you give to `WithoutCaptures` has to return the view model itself. `Construct` returns
a `Constructed<T>`, so it can't be the last call in that function: returning what `Construct` gives
doesn't compile there. `CreateBase` returns the view model, so it can be.

This shape needs no `Transaction.Run`. A forward reference builds one transaction of its own, and
the continuation runs inside it, so the whole graph still starts from one instant. A view model
without a loop has nothing else to open a transaction, and keeps its `Transaction.Run`.

Inside the loop, the looped cell is a promise about the view model and not the view model. Build
cells from it, as `step` is built above, but don't read its value before the loop closes: SodaFlow
throws if you do. Its [documentation](https://morsecode-software.github.io/SodaFlow/) has the rules
for loops.

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

The registrations close in one of these ways:

- The constructor returns.
- The constructor throws. The base releases every registration made so far, and the exception goes to
  the caller unchanged. If a registration also throws as it is released, the caller gets an
  `AggregateException` that holds the exception of the constructor first.
- `Dispose` runs, including a call from inside the constructor. It releases the registrations made
  so far.
- The continuation throws, or returns something that is not the result of its `Construct` call:
  `null`, or the result of a handle the base did not give it. The caller gets no view model that
  owns the registrations then, so the base releases them. Such a return fails with an
  `InvalidOperationException`; an exception goes to the caller unchanged, with the same
  `AggregateException` rule as above.

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
