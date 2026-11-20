---
title: Primary constructor parameter (MC0003)
---

# Primary constructor parameter (MC0003)

A primary constructor parameter without a modifier is a variable that every member of the type can
read and write, for the life of the object. A member that uses it turns it into a hidden field, and
nothing stops a later edit from assigning to it. `MorseCode.Analyzers` reports a primary constructor
parameter of a class or a struct that isn't written as `in`.

```csharp
public sealed class Holder(int value)       // MC0003 on value
{
    public int Value { get; } = value;
}

public sealed class Holder(in int value)    // No report.
{
    public int Value { get; } = value;
}
```

The message names the parameter and the type:

```text
warning MC0003: The primary constructor parameter value of Holder is not in: write in, or copy the value to a field
```

## Why `in`

`in` makes the parameter readonly, and the compiler refuses a member that captures it (CS9109), so
the value can only go into a field or property initializer, or a call to the base constructor. It
can never become mutable state that a reader has to hunt for. This is about immutability, not
performance.

A member that needs the value reads it from a field:

```csharp
public sealed class Greeter(in string name)
{
    private readonly string name = name;

    public string Greet() => $"Hello, {this.name}";
}
```

A lambda can't read an `in` parameter either (CS1628); the same field serves it. On a struct
parameter, call only `readonly` members, or the compiler makes a defensive copy.

## What it checks

- The primary constructor of a class and of a struct, including a class whose parameter goes to its
  base, as in `class Derived(int value) : Base(value)`.

It doesn't report:

- A record or a record struct. A positional parameter makes a property, and can't be `in`.
- A `params` parameter, which can't be `in`.
- A parameter of an attribute. C# won't apply an attribute whose constructor has an `in` parameter
  (CS8358), so `in` would break every use of it.
- A by-value `scoped` parameter, as in `ref struct Window(scoped ReadOnlySpan<int> values)`.
  `scoped in` scopes the reference and not the value, so no spelling with `in` keeps the value
  scoped, and the compiler already refuses to let a member use the parameter (CS9110).
- A `ref`, `out`, or `ref readonly` parameter. The compiler already refuses to let a member capture
  it (CS9109), a write through `ref` or `out` is meant to reach the caller's variable, and `in` would
  change what the parameter means to the caller.
- A type with an ordinary constructor.

The warning marks the name of the parameter.

## Installing it

```bash
dotnet add package MorseCode.Analyzers
```

The package is a development dependency, so NuGet adds it with `PrivateAssets="all"`, and it
doesn't flow to the packages of a project that uses it. To use it in every project of a repository,
add it once in `Directory.Build.props`:

```xml
<ItemGroup>
    <PackageReference Include="MorseCode.Analyzers" Version="0.1.2" PrivateAssets="all"/>
</ItemGroup>
```

MC0003 arrives in version 0.1.2. It needs Roslyn 4.4 or later: the .NET 7 SDK or later, or Visual
Studio 2022 17.4 or later.

## Fixing it

From version 0.1.3, an editor that offers the code fixes of Roslyn analyzers, such as Visual Studio,
offers **Make the parameter in**. It writes `in` before the type. On a parameter of a ref struct it
offers **Make the parameter scoped in** instead, and writes `scoped in`.

It's offered only where `in` compiles. The fix first compiles each part of the type with the change,
including a part in another file, and isn't offered when that adds an error: where a member captures
the parameter (CS9109), where a lambda or a local function uses it (CS1628), or where code writes
it. Those need the value copied to a field, as above, and the fix leaves that to you.

A ref struct needs `scoped in` because plain `in` lets the value the constructor makes hold the
reference. A caller that returns that value from one of its own parameters, from a constant, or from
an array then stops compiling (CS8347), and that caller is often in another type or project, where
the fix's check doesn't look. The warning on such a parameter says `write scoped in` for the same
reason.

It doesn't change a `ref`, `out`, or `ref readonly` parameter, which means something else. A fix of
all reports, in a document, a project, or a solution, writes `in` wherever it compiles.

## Changing its severity

MC0003 is a warning, so a build with `-warnaserror` fails on it. Change its severity in
`.editorconfig`, as for any analyzer:

```ini
[*.cs]
dotnet_diagnostic.MC0003.severity = error
```

`none` turns it off, for a folder of generated code, for example.
