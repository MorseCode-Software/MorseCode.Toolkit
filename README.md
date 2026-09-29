# MorseCode.Toolkit

[![Build status](https://github.com/MorseCode-Software/MorseCode.Toolkit/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/MorseCode-Software/MorseCode.Toolkit/actions/workflows/build.yml)
[![Coverage Status](https://coveralls.io/repos/github/MorseCode-Software/MorseCode.Toolkit/badge.svg)](https://coveralls.io/github/MorseCode-Software/MorseCode.Toolkit)

The MorseCode toolkit for building applications in a functional reactive style on
[SodaFlow](https://github.com/MorseCode-Software/SodaFlow).

SodaFlow is a general-purpose library: it gives you cells, streams, and behaviors and leaves the
shape of your application to you. This toolkit is the opposite. It holds the opinions MorseCode
Software builds its own applications with — how a view model is put together, how its graph is
constructed, and how it releases what it built — packaged so that anyone who wants to write
software the same way can.

The packages are published under the `MorseCode.*` prefix and version independently. They are
pre-1.0, and their APIs will change as the applications built on them teach us what they need.

## Packages

- **MorseCode.Mvvm**: building blocks for view models. It starts with `CompositeDisposable`, which
  a view model uses to release the subscriptions its construction made: every entry once, in
  order, and without one failure leaking the rest.<br>
  [![Latest Stable Version](https://img.shields.io/nuget/v/MorseCode.Mvvm.svg)](https://www.nuget.org/packages/MorseCode.Mvvm/)
  [![Total Downloads](https://img.shields.io/nuget/dt/MorseCode.Mvvm.svg)](https://www.nuget.org/packages/MorseCode.Mvvm/)
- **MorseCode.StagedConstruction**: construction of immutable objects in ordered stages. A static
  `Create` method builds every value in local variables and hands the finished values to a
  constructor that only assigns them. A base type's `CreateBase` method trades values with its
  subclass one stage at a time, so nothing can read a partly built object and the order of the
  work is set by the types. It has no dependency on SodaFlow, though it was written for objects
  whose properties are SodaFlow graphs.<br>
  [![Latest Stable Version](https://img.shields.io/nuget/v/MorseCode.StagedConstruction.svg)](https://www.nuget.org/packages/MorseCode.StagedConstruction/)
  [![Total Downloads](https://img.shields.io/nuget/dt/MorseCode.StagedConstruction.svg)](https://www.nuget.org/packages/MorseCode.StagedConstruction/)

## License

BSD 3-Clause. See [LICENSE](LICENSE).
