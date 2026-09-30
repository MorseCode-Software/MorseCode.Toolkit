---
title: Disposal and lifetimes
---

# Disposal and lifetimes

A SodaFlow subscription lasts until someone stops it. A view model that makes subscriptions and
does not release them stays in memory, and its listeners keep running, after its view is gone.
`ViewModelBase` gives a view model one call that releases everything.

## What Dispose does

`ViewModelBase.Dispose` releases each registration:

- **Last registered, first released.** Listeners and disposables share one list, and `Dispose`
  walks it from the end, whatever kind each entry is.
- **One time.** The first call releases. Each later call does nothing, also when two threads call
  at the same time.
- **Without one failure hiding the rest.** When a registration throws, the others still release.
  After all of them finish, a single exception reaches the caller unchanged. Two or more reach the
  caller in one `AggregateException`, in the order they were released.

```csharp
public void Dispose() => this.viewModelBase.Dispose();
```

## What gets registered

Anything you pass to `output.AddListener` or `output.AddDisposable`, and every bindable value and
action that `output.BindableFactory` makes. Anything you do not register is your job to release.

Construction order usually takes care of release order. You can only register something that
already exists, so anything a registration uses was normally registered before it, and `Dispose`
stops the registration before it releases what it uses. For example, a listener registered after a
disposable it writes to is stopped first, so it cannot fire into that disposable once it is gone.

The exception is a loop. `ForwardReference` lets an earlier registration refer to one made later,
and no fixed order suits every such graph. If release order matters there, check it with a test like
the one below.

## Null and late registrations

Both are refused at the call, where the mistake is on the stack:

| Mistake | Result |
| --- | --- |
| A null listener or disposable | `ArgumentNullException` at the `Add...` call. |
| A registration after `Construct` | `InvalidOperationException` at the `Add...` call. |
| A second `Construct` | `InvalidOperationException`. |

A null entry that reached `Dispose` would stop the disposal of every entry after it, so it is
better refused early. A refused registration does not disturb the others: `Dispose` still releases
the entries that were accepted.

## Testing disposal

Register a recording disposable in the construction, dispose the view model, and check what the
recording saw. A test passes `BindingScheduler.Immediate`, so no UI thread is involved:

```csharp
List<string> log = [];

ViewModelBase viewModel = ViewModelBase.CreateBase(
    BindingScheduler.Immediate,
    (output, construct) =>
    {
        output.AddDisposable(Disposable.FromAction(() => log.Add("A")));
        output.AddDisposable(Disposable.FromAction(() => log.Add("B")));

        return construct.Construct(viewModelBase => viewModelBase);
    });

viewModel.Dispose();
viewModel.Dispose();   // does nothing

// log is ["B", "A"]: each entry once, last registered first.
```

Because a second `Dispose` does nothing, a test can call it as many times as it needs.
