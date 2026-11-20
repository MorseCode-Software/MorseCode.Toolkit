0.1.3

Fixed: MC0003 reported the primary constructor parameter of an attribute, such as
class MarkAttribute(int level) : Attribute. C# refuses to apply an attribute whose constructor has an
in parameter (CS8358), so the change the rule asked for broke every use of the attribute, and in a
library those uses are in other projects, where the library's own build does not see them. The rule
now leaves out a type that derives from System.Attribute, directly or through another attribute,
abstract or not, as it already left out a params parameter.

Fixed: MC0003 reported a by-value scoped parameter, such as
ref struct Window(scoped ReadOnlySpan<int> values). No spelling with in keeps such a value scoped:
scoped in limits the reference, and the value is then not scoped, so a caller in another type that
returns what the constructor made no longer compiles (CS8347). The other advice of the message, to
copy the value to a field, is what the compiler refuses for a scoped value (CS8352). The compiler
also refuses a member that uses a ref-like parameter (CS9110), so such a parameter cannot become the
hidden state that the rule is about. The rule no longer reports it.

Changed: on a parameter of a ref struct, MC0003 now asks for scoped in, and its message says so. An
in parameter of the constructor of a ref struct lets what the constructor makes hold the reference,
so a caller can no longer return the value when its argument does not outlive the call: a parameter
of the caller, a constant, or an element of an array. Such a caller fails with CS8347, in whatever
project it is. Scoped in keeps the reference in the constructor and leaves every such caller as it
was. The rule's id and severity are unchanged.

Changed: MC0001 now also checks the arguments of an indexer, such as grid[column: 2, row: 1], and
the constructor arguments of an attribute, such as [Range(maximum: 9, minimum: 1)]. Before, it
checked calls of methods, local functions, delegates, and constructors only, so named arguments out
of order in these two places gave no report. An indexer in an object initializer, such as
new Grid { [column: 2, row: 1] = 3 }, is checked too. A name with an equals sign in an attribute
sets a property and is not part of the order. The rule, its message, and its id are as before.
Because it now reports more, a build with -warnaserror can fail where it passed with 0.1.2.

Fixed: MC0001 got the position of an argument wrong when the argument was in parentheses, in
checked(...) or unchecked(...), or in parentheses with the null-forgiving operator, such as
Three(a: (x), b: 2, c: 3). Such an argument has no operation of its own, so the compiler gives its
operation the syntax of the expression inside, and the rule found the argument only for a plain
null-forgiving operator. Each other one counted as the last parameter. A call in order gave a report,
such as Three(a: (x), b: 2, c: 3) and Three(a: checked(x * 2), b: 2, c: 3), and a call out of order
could give none, such as Three(a: 1, c: unchecked(3), b: ((2))). The rule now goes up from the syntax
of each argument's operation to the argument of the list that holds it, so any such wrapper finds its
argument. The new code fix reads the order from the rule, so it no longer puts a call that is in order
out of order, or puts a positional argument after a named one, which does not compile.

Fixed: MC0002 said that a cast is not needed wherever an implicit conversion exists, but a cast can
choose something even then. (double)sum / count divides doubles and not integers, (object)a ==
(object)b compares references and not text, and ((IShape)square).Sides reaches a member that the
class implements explicitly; without the cast, each does something else or does not compile. The
message now says the cast is not needed only where the value goes straight to a destination of the
cast's type: a variable or property with that declared type, a simple assignment, or the return of a
method of that type. Elsewhere it says to put the value in a variable of that type in place of the
cast. A cast from dynamic no longer says it is not needed at all: the cast runs an explicit
conversion when the code runs and the code without it an implicit one, so a long in a dynamic
converts to int with the cast and throws without it. The rule still reports every cast.

Changed: MC0002 now also reports a foreach, and a range variable of a query, with a type that each
element converts to explicitly: foreach (string text in objects) over an IEnumerable<object>,
foreach (int number in longs) over an IEnumerable<long>, and from string text in objects, which
calls Cast<string>(). The compiler makes those conversions with no cast in the source, and they fail
or lose data as a cast does. The report marks the type of the variable. A loop with var, or with a
type that the elements convert to implicitly, gives no report. Because the rule now reports more, a
build with -warnaserror can fail where it passed with 0.1.2.

New: code fixes for MC0001 and MC0003, in a second assembly, MorseCode.Analyzers.CodeFixes, beside
the analyzer under analyzers/dotnet/cs. An editor loads it, and the compiler does not, because a
code fix needs the Roslyn workspaces. For MC0001, the fix puts the arguments in the order of the
parameters, and each position of the list keeps its line. C# evaluates arguments in the order of
the source, thus a new order can change what runs first. Where it cannot, because no argument has
an effect, or one has and each other argument is a constant, the title of the fix says only that it
puts the arguments in order. In each other case, the title says that it changes the order in which
they run, and the fix has a different equivalence key, so a fix of all reports with the first key
never moves an effect. The fix is not offered when one argument declares a variable that a
different argument names. For MC0003, the fix writes in before the type, or scoped in on a parameter of a ref struct. It first
compiles each part of the type with the change, and is offered only when that adds no error, so it
is not offered for a parameter that a member captures, that a lambda uses, or that code writes. It
does not change a ref, out, or ref readonly parameter. Each fix has a fix of all reports for a
document, a project, and a solution.

In the MC0001 fix, each argument takes its comments with it while each position keeps its line. The
fix is not offered where a directive such as #if is between the arguments, or where a comment to the
end of a line would come to rest before a closing parenthesis on its line. For its title, an
explicit cast that can throw, such as an unbox, counts as an effect. So does a delegate from a
method of an instance that can be null, also when it is written with new or a cast, because it
throws ArgumentException where a field read would throw NullReferenceException. A delegate from a
static method, from this, or from a value does not count, also as a method group.

0.1.2

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

Changed: MC0002 now marks only the type in parentheses, such as (int), and not the whole cast
expression. A cast on a long expression, such as (int)Math.Abs(value: (long)obj), used to
underline all of it, and a cast inside the value had a mark inside the first one. The rule
reports the same casts, the same message, and the same id. Only the span of the mark changed.
A suppression with a pragma or an attribute works as before.

The tests of MC0002 now cover a cast to a tuple type, a pointer cast, and a cast from dynamic.
The rule reports each of them. A conversion from dynamic is implicit for the compiler, so its
message says the cast is not needed. That is true of the code, because a variable of the type
takes the value, and the value is still checked only when the code runs.

Changed: MC0001 now marks only the argument list of a call, such as (second: 2, first: 1), and
not the whole call. A call on a long receiver, or a new with an initializer, used to underline
all of it. The rule reports the same calls, the same message, and the same id, as MC0002 does
in this version. A suppression with a pragma or an attribute works as before.

Fix: MC0001 no longer reports a call whose named arguments are in order when an argument is
written with the null-forgiving operator, such as
Create(collection: collection, project: null!, scrollRequests: requests). The compiler gives
such an argument a different syntax node, and the rule did not find its parameter. It took the
last parameter instead, so a suppressed argument before the last one looked out of order. The
rule now finds the parameter of an argument with the operator, in any position. A call that is
really out of order is still reported, with or without the operator. A project that put a
suppression of MC0001 on such a call can remove it.

This is a patch version because no API changed, no rule changed its id, and the package adds a
rule, as 0.1.1 did. A project that builds with -warnaserror and updates from 0.1.1 fails on each
primary constructor parameter without in. Set dotnet_diagnostic.MC0003.severity in .editorconfig
to change that.

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
