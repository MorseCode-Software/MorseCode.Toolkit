---
title: Continuation rules (MC1001 to MC1007)
---

# Continuation rules (MC1001 to MC1007)

A base gives its continuation a handle, and the continuation has to return the
`Constructed<TResult>` that `Construct` or `Advance` gives on that handle. The compiler checks only
the type of what comes back. `ViewModelBase.CreateBase` also checks the value when it runs. These
rules report the usual ways to get it wrong in the build, before anything runs.

The analyzer ships inside the `MorseCode.StagedConstruction` package, so there is nothing to
install. A project that references that package gets the rules. So does a project that references
`MorseCode.Mvvm` or `MorseCode.Controls.UI.Avalonia`, because both depend on it.

It examines each lambda that becomes a `StageContinuation` and whose second parameter is a
`Constructor<T>` or a `Stage<,,>`. That parameter is the handle. A stage can hand its continuation
something that isn't a handle, and no rule applies to that continuation.

| Rule | Severity | Reports |
| --- | --- | --- |
| [MC1001](#mc1001) | Warning | `Value` read directly from the call on the handle |
| [MC1002](#mc1002) | Warning | The result of the call on the handle isn't used |
| [MC1003](#mc1003) | Warning | The continuation returns null |
| [MC1004](#mc1004) | Warning | The continuation returns the result of a handle it made with `Constructor.From` |
| [MC1005](#mc1005) | Warning | The function given to `Construct` doesn't read the values of the base |
| [MC1006](#mc1006) | Warning | The continuation doesn't use its handle |
| [MC1007](#mc1007) | Info | The handle goes to code the analyzer can't follow |

Each warning reports only what the analyzer can prove from the code in front of it. MC1007 is
different: it marks the place where the other rules stop.

## MC1001

**`Value` is read directly from the call on the handle.** The read throws away the `Constructed`,
so the continuation has nothing left to return.

```csharp
(output, construct) =>
{
    ViewModelBase taken = construct.Construct(viewModelBase => viewModelBase).Value;   // MC1001

    return Constructor.From(taken).Construct(values => new ClockViewModel(values));    // MC1004
}
```

This is the shape `CreateBase` refuses at run time: it takes the base out with one handle and gives
it to the view model through another. Return the result of the call instead. A base of your own
adds its values with `Select`, as [Bases with several stages](multi-stage.md) shows.

Reading `Value` from a `Constructed` that you keep is fine, and the rule doesn't report it:

```csharp
Constructed<ClockViewModel> made =
    construct.Construct(viewModelBase => new ClockViewModel(viewModelBase));

return (made.Value, made);   // No report: made is still there to return.
```

That is what `Construct` inside a `ForwardReference<T>.WithCaptures` looks like. It works, but a
view model with a loop is simpler with the forward reference outside `CreateBase`: see [View
models](view-models.md#a-view-model-with-a-loop).

## MC1002

**The result of the call on the handle isn't used.** A handle can be used one time only, so a
result that isn't kept can't be made again.

```csharp
(output, construct) =>
{
    _ = construct.Construct(viewModelBase => new ClockViewModel(viewModelBase));   // MC1002

    return somethingElse;
}
```

It reports a statement that is only the call, and an assignment of the call to a discard.

## MC1003

**The continuation returns null.** `null!` and `default!` both get past the compiler, and neither
is a value the base can give to its caller.

```csharp
(output, construct) => ready ? construct.Construct(...) : null!   // MC1003 on null
```

## MC1004

**The continuation returns the result of a handle it made with `Constructor.From`.** That value
doesn't hold the values of the base.

```csharp
(output, construct) =>
{
    try
    {
        return construct.Construct(viewModelBase => new ClockViewModel(viewModelBase));
    }
    catch (InvalidOperationException)
    {
        return Constructor.From(fallback).Construct(clock => clock);   // MC1004
    }
}
```

The rule follows the returned value back through local variables and `Select` to the call that
made its handle. It reports only when that call is `Constructor.From` inside the continuation. A
value that comes out of a helper method is not reported, because the analyzer can't see where it
came from.

## MC1005

**The function given to `Construct` doesn't read the values of the base.** The handle is the only
source of those values, so a function that ignores them returns something the base didn't help to
make.

```csharp
construct.Construct(_ => new ClockViewModel())               // MC1005 on _
construct.Construct(viewModelBase => new ClockViewModel())   // MC1005 on viewModelBase
```

A function that only throws returns nothing, and isn't reported.

## MC1006

**The continuation doesn't use its handle.** The handle is the only way to make the `Constructed`
the continuation has to return, so a continuation that returns a value without touching it returns
the wrong one.

```csharp
(output, _) => Constructor.From("other").Construct(text => text)   // MC1006 on _, and MC1004
```

A continuation that only throws returns nothing, and isn't reported.

## MC1007

**The handle goes to code the analyzer can't follow.** This one is information, not a warning. The
code may be correct. The rule says only that the other rules can't check it past this point.

The analyzer follows the handle through these uses, and reports none of them:

- A call to `Construct`, `Select`, or `Advance` on it.
- A local variable that holds it.
- Passing it to a continuation, which is how a base hands it to its subclass.
- Passing it to the constructor of a type that derives from `Stage<,,>` or `Constructor<T>`, which
  is how you write a stage or a handle of your own.
- Returning it from the function of `Stage.From`.

It reports the handle anywhere else: passed to another method, passed to the constructor of
another type, or stored in a field or a property.

```csharp
(output, construct) => Finish(construct)   // MC1007: is given to Finish
```

It also reports a lambda that holds the handle, unless the lambda is passed to something that runs
it before returning. A lambda that can run later may still hold the handle after the continuation
has returned. These run it before returning, and aren't reported:

- `Transaction.Run` and `Transaction.RunVoid`.
- `ForwardReference<T>.WithCaptures` and `WithoutCaptures`.
- The `WithCaptures` and `WithoutCaptures` blocks of `Cell.Loop`, `Stream.Loop`, and
  `Behavior.Loop`.
- The function given to `Construct`, and any continuation.
- A parameter marked `[InstantHandle]`, from `JetBrains.Annotations`.

```csharp
static T Later<T>(Func<T> f) => f();

(output, construct) => Transaction.Run(() => construct.Construct(...))   // No report.

(output, construct) => Later(() => construct.Construct(...))             // MC1007
```

Nothing says that `Later` runs the lambda before it returns, so it may keep it. Marking the
parameter, as in `Later<T>([InstantHandle] Func<T> f)`, removes that report.

`[InstantHandle]` works on a method in the same project as the continuation. It usually doesn't
work on a method from a compiled library: the attribute is conditional, and a library that doesn't
define `JETBRAINS_ANNOTATIONS` doesn't carry it in its assembly. That is why the analyzer knows the
SodaFlow members above by name.

## Turning a rule off or up

Set the severity of a rule in `.editorconfig`:

```ini
dotnet_diagnostic.MC1007.severity = none      # or: warning
```

For one place, use a `#pragma` with the reason beside it:

```csharp
#pragma warning disable MC1007 // Finish calls Construct on the handle and returns the result.
return Finish(construct);
#pragma warning restore MC1007
```

## What it doesn't see

- A continuation that is a method group and not a lambda.
- What a helper method, a field, or a type of your own does with the handle once it has it. MC1007
  marks where that happens.
- A handle of your own type whose `ConstructCore` changes the value on its way back.

`ViewModelBase.CreateBase` still checks the value at run time, so a mistake the rules miss there
fails the first time the view model is constructed. See [View models](view-models.md).
