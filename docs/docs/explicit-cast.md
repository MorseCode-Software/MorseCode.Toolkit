---
title: Explicit cast (MC0002)
---

# Explicit cast (MC0002)

An explicit cast, such as `(int)value`, tells the compiler to trust you. The compiler doesn't check
it. A wrong reference cast fails at run time with an `InvalidCastException`, and a wrong numeric
cast loses data without a sign. `MorseCode.Analyzers` reports every explicit cast as a warning, so
the few that are really needed stand out, and the rest are written another way.

```csharp
object boxed = 3;
long big = 3;

int a = (int)boxed;           // MC0002
byte b = (byte)big;           // MC0002
string? s = boxed as string;  // No report.
int c = boxed is int number ? number : 0;   // No report.
```

The message names the type the cast converts to, and a way out that suits the kind of conversion:

```text
warning MC0002: Avoid the explicit cast to byte: keep the value in a type of the right size, or convert it in one place with a name
```

## What it reports

Every cast expression, whatever the conversion is:

- A cast down to a derived type, or out of a box: `(Square)shape`, `(int)boxed`.
- A numeric conversion: `(byte)big`.
- An enum to or from its number: `(int)color`, `(Color)1`.
- A nullable to its value: `(int)maybe`.
- A conversion that you wrote: `(Money)1.5m`.
- A cast that the compiler does without: `(object)big`. The message then says that an implicit
  conversion exists, and the cast isn't needed.

It doesn't report `as`, `is`, a pattern, a conversion the compiler makes without a cast, a
parenthesized expression such as `(big) + 1`, or code in a generated file.

## How to avoid a cast

- **A reference or an unboxing cast.** Match the type: `value is Square square`, or a `switch` with
  type patterns. Better, keep the value in a variable of the type you need, so the question never
  comes up. `as` with `?.` is fine in a test, where a wrong type should make the assertion fail.
- **A numeric or enum cast.** Keep the value in a type of the right size from the start. If a
  conversion is really needed, do it in one place, in a method with a name that says what it does,
  and suppress the rule there.
- **A conversion you wrote.** Call a method with a name, such as `Money.FromDecimal(1.5m)`, so a
  reader sees the conversion without hunting for the operator.

## When a cast is needed

A few casts can't be avoided. When one is needed, suppress the rule at that line, and write the
reason next to it. A suppression with a reason tells the next reader that the cast was thought
about.

```csharp
#pragma warning disable MC0002 // The wire format stores the level in one byte, and Level is checked above.
byte level = (byte)checkedLevel;
#pragma warning restore MC0002
```

An attribute works for a whole method:

```csharp
[SuppressMessage("Style", "MC0002", Justification = "The generic caller proved the type.")]
```

## Installing it

```bash
dotnet add package MorseCode.Analyzers
```

The package is a development dependency, so NuGet adds it with `PrivateAssets="all"`, and it
doesn't flow to the packages of a project that uses it. To use it in every project of a repository,
add it once in `Directory.Build.props`:

```xml
<ItemGroup>
    <PackageReference Include="MorseCode.Analyzers" Version="0.2.0" PrivateAssets="all"/>
</ItemGroup>
```

MC0002 arrives in version 0.2.0. It needs Roslyn 4.4 or later: the .NET 7 SDK or later, or Visual
Studio 2022 17.4 or later.

## Changing its severity

MC0002 is a warning, so a build with `-warnaserror` fails on it. Change its severity in
`.editorconfig`, as for any analyzer:

```ini
[*.cs]
dotnet_diagnostic.MC0002.severity = error
```

`none` turns it off, for a folder where casts are normal, for example.
