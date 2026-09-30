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
        StageContinuation<string, Constructor<AnimalValues>, TResult> continuation) =>
        continuation(name.ToUpperInvariant(), Constructor.From(new AnimalValues(name)));
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
a `Constructor<AnimalValues>`. `Dog.Create` uses the output to make its own values, and then calls
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
      StageContinuation<Output, Constructor<Values>, TResult> continuation)
  {
      // open a scope
      try
      {
          return continuation(output, Constructor.From(values));
      }
      finally
      {
          // close the scope
      }
  }
  ```

## Each handle is used once

A subclass calls `Constructor<T>.Construct` one time, and `Stage<,,>.Advance` one time. Every
handle enforces this: a second call throws an `InvalidOperationException` that names the method,
even when two threads call at the same moment.

The first call uses the handle up before it runs your constructor or stage. If that code throws,
the handle stays used, so a failed construction cannot be run a second time.

`Constructor<T>` and `Stage<,,>` are abstract classes, and there is no interface to implement
instead. `Construct` and `Advance` make the check, and then call the method you write:
`ConstructCore` or `AdvanceCore`. That method runs at most once, so it needs no check of its own.
The handles that `Constructor.From`, `Select`, and `Stage.From` make work the same way.

`Constructor<T>` also has a virtual `Select`. The default makes the usual handle, and you can
override it when your handle can do better.

## Handles do not convert between value types

A `Constructor<Dog>` is not a `Constructor<Animal>`, even when `Dog` derives from `Animal`. If a
helper must accept a handle for any kind of animal, make the helper generic in the values type:

```csharp
public static Pet MakePet<T>(string output, Constructor<T> animal)
    where T : AnimalValues =>
    animal.Construct(values => new Pet(values.Name, output));
```

You can pass this method, as a method group, wherever a callback for an animal base or a mammal
base is expected. A lambda cannot be generic, so a callback that you write as a lambda must name the
values type. To change the values type of a handle, call `Select`.

## The types

| Type | What it is |
| --- | --- |
| @MorseCode.StagedConstruction.StageContinuation`3 | The callback a subclass gives to a stage of its base. It gets the output of the stage and the handle for the next step. |
| @MorseCode.StagedConstruction.Constructor`1 | The handle for the step after the last stage. It gives the values of the base to the constructor of the subclass. Derive from it for a handle of your own. |
| @MorseCode.StagedConstruction.Stage`3 | The handle for a stage after the first. The subclass gives input to it. Derive from it for a stage of your own. |
| @MorseCode.StagedConstruction.Constructor | `Constructor.From(values)` makes the usual final handle. |
| @MorseCode.StagedConstruction.Stage | `Stage.From(body)` makes a stage from a function. |

A base with more than one stage, or a base that has a base of its own, uses more of these. See
[Bases with several stages](multi-stage.md).
