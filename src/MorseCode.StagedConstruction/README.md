# MorseCode.StagedConstruction

Construction of immutable objects in ordered stages, with no access to a partly built object.

```bash
dotnet add package MorseCode.StagedConstruction
```

A static `Create` method builds every value in local variables and hands the finished values to a
constructor that only assigns them. A base type's `CreateBase` method trades values with its
subclass one stage at a time, so nothing can read a partly built object, and the types set the order
of the work.

The package has no dependency on SodaFlow, though it was written for objects whose properties are
SodaFlow graphs. It carries its own analyzer, which reports a continuation that doesn't return the
result of its handle. The analyzer runs in every project that has the package anywhere in its
dependencies.

## Documentation

- [Staged construction](https://morsecode-software.github.io/MorseCode.Toolkit/docs/staged-construction.html)
- [Bases with several stages](https://morsecode-software.github.io/MorseCode.Toolkit/docs/multi-stage.html)
- [Continuation rules](https://morsecode-software.github.io/MorseCode.Toolkit/docs/continuation-rules.html),
  the rules of the analyzer
- [API reference](https://morsecode-software.github.io/MorseCode.Toolkit/api/index.html)

The source is at <https://github.com/MorseCode-Software/MorseCode.Toolkit>, under the BSD 3-Clause
license.
