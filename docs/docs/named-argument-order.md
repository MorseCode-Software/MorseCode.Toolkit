---
title: Named argument order (MC0001)
---

# Named argument order (MC0001)

C# accepts named arguments in any order. `MorseCode.Analyzers` reports a call whose arguments aren't
in the order of the method's parameters, when at least one argument has a name. A call written in
the order of the definition can be read against the method from top to bottom, and a parameter
added later lands in the same place at every call.

```csharp
public static Pair Create(int first, int second) => new(first, second);

Create(first: 1, second: 2);   // No report.
Create(1, second: 2);          // No report.
Create(second: 2, first: 1);   // MC0001
```

The message names the method and the order of its parameters:

```text
warning MC0001: The arguments of Pairs.Create(int, int) are not in the order of its parameters: first, second
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
    <PackageReference Include="MorseCode.Analyzers" Version="0.1.0" PrivateAssets="all"/>
</ItemGroup>
```

It needs Roslyn 4.4 or later: the .NET 7 SDK or later, or Visual Studio 2022 17.4 or later. An older
compiler doesn't run it.

## What it checks

- Calls of methods, extension methods, local functions, and delegates.
- Calls of constructors: `new Pair(...)`, `new(...)`, `base(...)`, `this(...)`, and the base type of
  a primary constructor, as in `class Named(int value) : Pair(...)`.

It reads the argument list as the source has it, and asks the compiler which parameter each
argument goes to. So an optional parameter that a call leaves out, and an empty `params` array,
never cause a report, and neither does a call that the compiler makes for an interpolated string. A
value that goes into a `params` array counts as the `params` parameter, which is always the last.

It doesn't check the arguments of attributes.

## Changing its severity

MC0001 is a warning, so a build with `-warnaserror` fails on it. Change its severity in
`.editorconfig`, as for any analyzer:

```ini
[*.cs]
dotnet_diagnostic.MC0001.severity = error
```

`none` turns it off, for a folder of generated code, for example.
