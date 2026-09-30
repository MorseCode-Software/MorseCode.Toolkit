---
title: The Disposable helpers
---

# The Disposable helpers

`Disposable` is a static class in `MorseCode.Mvvm` with three ready-made `IDisposable` objects.
`ViewModelBase` uses them, and they are public because other code needs the same behavior.

## Disposable.FromAction

```csharp
IDisposable Disposable.FromAction(Action onDispose)
```

Makes a disposable that calls `onDispose` at the first `Dispose`. Each later call does nothing,
also when two threads call at the same time.

```csharp
IDisposable timerStop = Disposable.FromAction(() => timer.Stop());

timerStop.Dispose();   // calls timer.Stop()
timerStop.Dispose();   // does nothing
```

A null `onDispose` fails at the call to `FromAction`, and not later in `Dispose`.

## Disposable.Composite

```csharp
IDisposable Disposable.Composite(params IEnumerable<IDisposable> disposables)
```

Makes one disposable from a fixed list. The first `Dispose` disposes each entry one time, in the
order of the list. Each later call does nothing.

An exception from one entry does not stop the entries after it. When all entries finish:

- one exception goes to the caller unchanged, with its original stack trace;
- two or more go to the caller in one `AggregateException`, in the order of the list.

The composite keeps a copy of the list, so a later change to your collection has no effect. A null
entry fails at the call to `Composite` with an `ArgumentException` that names the index.

```csharp
IDisposable all = Disposable.Composite(first, second, third);
```

## Disposable.Empty

```csharp
public static readonly IDisposable Empty;
```

A disposable that does nothing. Use it where an `IDisposable` is necessary and there is nothing to
release.
