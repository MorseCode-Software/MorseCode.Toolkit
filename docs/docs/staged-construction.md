---
title: Staged construction
---

# Staged construction

Staged construction builds an immutable object in ordered stages, and gives no code a way to
read the object before it is complete. `MorseCode.StagedConstruction` holds the types for it.

## The problem

A C# constructor that calls a base constructor has three problems:

1. The order of the two constructors is fixed. The base constructor runs first, and it cannot get
   anything from the subclass.
2. Each constructor can read `this` before the object is complete.
3. A virtual member that the base constructor calls can read a field of the subclass before the
   subclass sets it.

The third problem is the worst one, because nothing in the code shows it. The base compiles, the
subclass compiles, and the field is `null`.

## The pattern

Move the work out of the constructors:

- A static `Create` method builds each value in a local variable.
- A private constructor only assigns the finished values.
- A base does its part in a static `CreateBase` method. It gives its output to the subclass
  through a callback, together with a handle for the next step.

```csharp
public sealed record AnimalValues(string Name);

public class Animal(AnimalValues values)
{
    public AnimalValues Values { get; } = values;

    public static TResult CreateBase<TResult>(
        string name,
        StageContinuation<string, IConstruct<AnimalValues>, TResult> continuation) =>
        continuation(name.ToUpperInvariant(), Construct.From(new AnimalValues(name)));
}

public sealed class Dog : Animal
{
    private Dog(AnimalValues values, string bark) : base(values) => this.Bark = bark;

    public string Bark { get; }

    public static Dog Create(string name) =>
        Animal.CreateBase(name, (output, construct) =>
            construct.Construct(values => new Dog(values, bark: output + "!")));
}
```

`Animal.CreateBase` gives `Dog.Create` two things: an output (the upper-case name), and a handle,
an `IConstruct<AnimalValues>`. `Dog.Create` uses the output to make its own values, and then calls
`construct.Construct` with a function that calls the constructor. The handle passes the values of
the base to that function.

The compiler infers `TResult` from the lambdas, so a `Create` method writes no type arguments.

## What it guarantees

- **Nothing reads a partly built object.** Every value is a local variable until the last step.
  There is no `this` to read.
- **The order of the work is set by the types.** A subclass cannot reach the constructor before the
  base finishes, when the base keeps the constructor of its values private. The handle is then the
  only source of the values.
- **A base can wrap the work of its subclass.** The base calls the continuation. It can do work
  before the call, and after it, for example inside a transaction:

  ```csharp
  public static TResult CreateBase<TResult>(
      StageContinuation<Output, IConstruct<Values>, TResult> continuation)
  {
      // open a scope
      try
      {
          return continuation(output, Construct.From(values));
      }
      finally
      {
          // close the scope
      }
  }
  ```

## Each handle is used once

A subclass calls `IConstruct<T>.Construct` one time, and `IStage<,,>.Advance` one time. The handles
that `Construct.From`, `Select`, and `Stage.From` make enforce this: a second call throws an
`InvalidOperationException` that names the method, even when two threads call at the same moment.

The first call uses the handle up before it runs your constructor or stage. If that code throws,
the handle stays used, so a failed construction cannot be run a second time.

A base that implements `IConstruct<T>` or `IStage<,,>` by hand should make the same check. The
handle that `Select` makes checks for itself, so it refuses a second `Construct` even when its
source is a hand-written handle that does not.

## The types

| Type | What it is |
| --- | --- |
| @MorseCode.StagedConstruction.StageContinuation`3 | The callback a subclass gives to a stage of its base. It gets the output of the stage and the handle for the next step. |
| @MorseCode.StagedConstruction.IConstruct`1 | The handle for the step after the last stage. It gives the values of the base to the constructor of the subclass. |
| @MorseCode.StagedConstruction.IStage`3 | The handle for a stage after the first. The subclass gives input to it. |
| @MorseCode.StagedConstruction.Construct | `Construct.From(values)` makes the usual final handle. |
| @MorseCode.StagedConstruction.Stage | `Stage.From(body)` makes a stage from a function. |

A base with more than one stage, or a base that has a base of its own, uses more of these. See
[Bases with several stages](multi-stage.md).
