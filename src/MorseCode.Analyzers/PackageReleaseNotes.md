0.1.4

New rule: MC0003 reports a primary constructor parameter of a class or a struct that is not
written as in, such as class Holder(int value). A parameter without a modifier is a variable
that every member of the type can read and write, and a member that captures it turns it into
hidden mutable state. The in modifier makes it readonly, and the compiler refuses a member that
captures it, so the value can go only into an initializer or a base call. A member that needs
the value reads it from a field. The warning marks the name of the parameter. The rule does not
report a record, whose positional parameters make properties and cannot be in, or a params
parameter, which cannot be in either.

MC0003 is a warning, so a build with -warnaserror fails on it. The documentation is at
https://morsecode-software.github.io/MorseCode.Toolkit/docs/primary-constructor-parameter.html

Changed: MC0001 now marks only the argument list of a call, such as (second: 2, first: 1), and
not the whole call. A call on a long receiver, or a new with an initializer, used to underline
all of it. The rule reports the same calls, the same message, and the same id, as MC0002 did in
0.1.3. A suppression with a pragma or an attribute works as before.

MC0002 did not change. This is a patch version because no API changed, the package adds a rule,
as 0.1.1 did, and no rule changed its id. A project that builds with -warnaserror and updates from
0.1.3 fails on each primary constructor parameter without in. Set
dotnet_diagnostic.MC0003.severity in .editorconfig to change that.

0.1.3

Changed: MC0002 now marks only the type in parentheses, such as (int), and not the whole cast
expression. A cast on a long expression, such as (int)Math.Abs(value: (long)obj), used to
underline all of it, and a cast inside the value had a mark inside the first one. The rule
reports the same casts, the same message, and the same id. Only the span of the mark changed.
A suppression with a pragma or an attribute works as before.

The tests now cover a cast to a tuple type, a pointer cast, and a cast from dynamic. The rule
reports each of them. A conversion from dynamic is implicit for the compiler, so its message
says the cast is not needed. That is true of the code, because a variable of the type takes
the value, and the value is still checked only when the code runs. This is a patch version
because no API changed and no rule changed its id.

0.1.2

Fix: MC0001 no longer reports a call whose named arguments are in order when
an argument is written with the null-forgiving operator, such as
Create(collection: collection, project: null!, scrollRequests: requests). The
compiler gives such an argument a different syntax node, and the rule did not
find its parameter. It took the last parameter instead, so a suppressed
argument before the last one looked out of order. The rule now finds the
parameter of an argument with the operator, in any position. A call that is
really out of order is still reported, with or without the operator.

MC0002 did not change. This is a patch version because no API changed and no
rule changed its id. A project that put a suppression of MC0001 on such a call
can remove it.

0.1.1

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

MC0001 did not change. This is a patch version because no API changed and the
package adds a rule. A project that builds with -warnaserror and updates from
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
