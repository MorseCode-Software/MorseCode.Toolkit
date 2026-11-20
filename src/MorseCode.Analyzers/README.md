# MorseCode.Analyzers

Roslyn analyzers for the conventions that MorseCode Software writes C# with, such as named arguments
in the order of the parameters.

```bash
dotnet add package MorseCode.Analyzers
```

The analyzers run in the compiler, so the build reports what they find, and an editor offers a fix
next to the warning where a rule has one. The package is a development dependency: NuGet adds it
with `PrivateAssets="all"`, and it doesn't flow to the packages of a project that uses it. It needs
the .NET 7 SDK or later, or Visual Studio 2022 17.4 or later.

## Documentation

The documentation site has a page for each rule, under Analyzers, with what it reports and why:

- [Named argument order (MC0001)](https://morsecode-software.github.io/MorseCode.Toolkit/docs/named-argument-order.html)
- [Which package do I install?](https://morsecode-software.github.io/MorseCode.Toolkit/docs/packages.html),
  which lists the rules of this package

The source is at <https://github.com/MorseCode-Software/MorseCode.Toolkit>, under the BSD 3-Clause
license.
