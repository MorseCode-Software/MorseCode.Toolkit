---
title: Bases with several stages
---

# Bases with several stages

The [basic pattern](staged-construction.md) has one exchange: the base gives output, and the
subclass builds. Two things need more: a base whose subclass must give it input, and a base that
has a base of its own.

## Stages

Sometimes the base needs a value that only the subclass can make, and the subclass needs a value
that only the base can make. Each exchange is a **stage**. The first stage is the static
`CreateBase` method. Each stage after it is an `IStage<TInput, TOutput, TNext>`:

- `TInput` is what the subclass gives to the stage.
- `TOutput` is what the stage gives back.
- `TNext` is the handle for the step after: another `IStage`, or an `IConstruct` after the last
  stage.

The type of the first continuation lists every stage in order:

```csharp
public static TResult CreateBase<TResult>(
    int seed,
    StageContinuation<
        int,                                          // the output of stage 1
        IStage<int, int,                              // stage 2: input int, output int
            IStage<string, string,                    // stage 3: input string, output string
                IConstruct<MachineValues>>>,          // then construct
        TResult> continuation)
{
    return continuation(
        output: seed * 2,
        next: Stage.From(
            body: (int adjusted) =>
            (
                Output: adjusted * 2,
                Next: Stage.From(
                    body: (string label) =>
                    (
                        Output: label.ToUpperInvariant(),
                        Next: Construct.From(new MachineValues(seed, adjusted, label)))))));
}
```

The subclass runs the stages in turn. It calls `Advance` on each stage handle with its input, and
the stage calls back with the output and the next handle:

```csharp
public static Robot Create(int seed) =>
    Machine.CreateBase(seed, (first, second) =>
        second.Advance(first + 1, (secondOutput, third) =>
            third.Advance($"label {secondOutput}", (thirdOutput, construct) =>
                construct.Construct(machine => new Robot(machine, summary: thirdOutput)))));
```

`Stage.From(body)` makes a stage from a function. The function gets the input of the subclass and
returns the output and the next handle in a tuple. It gets the values of the previous stages from
the closure around it.

`Stage.From` runs `body` when the subclass calls `Advance`, and not when the stage is made. It does
no work after the subclass continues. A stage that must do work around the later steps, for example
in a transaction, implements `IStage<,,>` directly, and should refuse a second `Advance` the way
the stage from `Stage.From` does. See [Each handle is used once](staged-construction.md#each-handle-is-used-once).

`Stage.From` refuses a null `body` when you call it, and not later in `Advance`.

## A base with a base of its own

A base that has its own base adds its values to the values of that base. `IConstruct<T>.Select`
does it:

```csharp
public static TResult CreateBase<TResult>(
    string name,
    StageContinuation<string, IConstruct<MammalValues>, TResult> continuation) =>
    Animal.CreateBase(name, (output, construct) =>
        continuation(
            output,
            construct.Select(animal => new MammalValues(animal, Furry: true))));
```

`Select` gives a new handle. When the subclass calls `Construct` on it, the handle calls the
selector with the values of the animal, and gives the result to the constructor. The selector runs
at that time, and not when `Select` is called. A null selector fails at the call to `Select`. Like
the other handles, the new one refuses a second `Construct`.

The order of the work is then: the animal base, the mammal base, the subclass, the selector, and the
constructor.
