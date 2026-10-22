0.2.0

New rule: MC0002 reports an explicit cast, such as (int)value or (Square)shape.
An explicit cast tells the compiler to trust the writer. The compiler does not
check it, so a wrong reference cast fails at run time and a wrong numeric cast
loses data without a sign. The rule reports each cast expression, whatever the
conversion is: a reference or unboxing cast, a numeric or enum conversion, a
nullable to its value, a conversion that the type declares, and a cast that an
implicit conversion makes unnecessary. The message names the target type and
gives a way out that suits the conversion. The rule does not report as, is, a
pattern, a parenthesized expression, or generated code.

MC0002 is a warning, so a build with -warnaserror fails on it. A cast that is
really needed carries a suppression with a reason, by #pragma warning disable or
by SuppressMessage. The documentation is at
https://morsecode-software.github.io/MorseCode.Toolkit/docs/explicit-cast.html

MC0001 did not change. A project that builds with -warnaserror and updates from
0.1.0 fails on each explicit cast that it has. Set
dotnet_diagnostic.MC0002.severity in .editorconfig to change that.

0.1.0

The first release. It contains one rule, MC0001, which reports a call whose
arguments are not in the order of the parameters of its method, when one or
more arguments have a name. C# accepts named arguments in any order, and
neither the compiler nor the usual analyzers report one. A call that follows
the definition can be compared with the method from top to bottom.

The rule examines calls of methods, local functions, and delegates, and the
calls of constructors by new, by base, and by the base type of a primary
constructor. It reads the arguments as the source has them, so an optional
parameter that a call leaves out, an empty params array, and a call that the
compiler makes for an interpolated string give no report. A value in a params
array counts as the params parameter. MC0001 is a warning, so a build with
-warnaserror fails on it. Its documentation is at
https://morsecode-software.github.io/MorseCode.Toolkit/docs/named-argument-order.html

---

About this package

Roslyn analyzers for the conventions that MorseCode Software writes C# with. It
is part of the MorseCode toolkit, which holds the conventions that MorseCode
Software builds its own applications with. The package is a development
dependency: NuGet adds it with PrivateAssets="all", and it does not flow to the
packages of a project that uses it.

Needs Roslyn 4.4 or later: the .NET 7 SDK or later, or Visual Studio 2022 17.4
or later. No dependencies.

Source: https://github.com/MorseCode-Software/MorseCode.Toolkit
