using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using MorseCode.Analyzers.CodeFixes;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Analyzers.Tests;

/// <summary>
///     Applies <see cref="PrimaryConstructorParameterCodeFixProvider" /> to
///     what <see cref="PrimaryConstructorParameterAnalyzer" /> reports.
/// </summary>
internal sealed class PrimaryConstructorParameterCodeFixTests
{
    private const string Mark =
        """
        [System.AttributeUsage(System.AttributeTargets.Parameter)]
        public sealed class MarkAttribute : System.Attribute;

        """;

    // A parameter with no modifier, one after an attribute, one on a line of its own, and one with scoped. Each
    // gets in at the position that C# wants, and the layout of the list does not change.
    [Test]
    public async Task TheFixWritesInBeforeTheTypeAndAfterScoped()
    {
        const string source =
            Mark
            + """
              public sealed class Plain(int value)
              {
                  public int Value { get; } = value;
              }

              public sealed class Marked([Mark] string name)
              {
                  public string Name { get; } = name;
              }

              public sealed class Wide(
                  int first)
              {
                  public int First { get; } = first;
              }

              public ref struct Window(scoped System.ReadOnlySpan<int> values)
              {
                  public int Length { get; } = values.Length;
              }
              """;

        IReadOnlyList<CodeFixHarness.Offer> offers =
            await CodeFixHarness.Offers(
                    analyzer: new PrimaryConstructorParameterAnalyzer(),
                    provider: new PrimaryConstructorParameterCodeFixProvider(),
                    source: source)
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(offers.Select(selector: static offer => offer.Title))
            .IsEquivalentTo(
                expected:
                [
                    "Make the parameter in", "Make the parameter in", "Make the parameter in", "Make the parameter in"
                ],
                ordering: CollectionOrdering.Matching);

        await Assert.That(offers.Select(selector: static offer => offer.Result))
            .IsEquivalentTo(
                expected:
                [
                    source.Replace(oldValue: "Plain(int value)", newValue: "Plain(in int value)"),
                    source.Replace(oldValue: "Marked([Mark] string name)", newValue: "Marked([Mark] in string name)"),
                    source.Replace(oldValue: "Wide(\n    int first)", newValue: "Wide(\n    in int first)"),
                    source.Replace(
                        oldValue: "Window(scoped System.ReadOnlySpan<int> values)",
                        newValue: "Window(scoped in System.ReadOnlySpan<int> values)")
                ],
                ordering: CollectionOrdering.Matching);
    }

    // The compiler refuses in where a member captures the parameter, where a lambda uses it, and where code
    // writes it. It also refuses in where a part of a partial type in a different document captures it. MC0003
    // reports each one, and the provider offers the fix for none.
    [Test]
    public async Task TheFixIsNotOfferedWhereInDoesNotCompile()
    {
        const string source =
            """
            public sealed class Captured(int value)
            {
                public int Read() => value;
            }

            public sealed class Lambda(int value)
            {
                public System.Func<int> Read { get; } = () => value;
            }

            public sealed class Written(int value)
            {
                public int Next { get; } = ++value;
            }

            public sealed partial class Joined(int value)
            {
                public int Value { get; } = value;
            }

            public sealed partial class Joined
            {
                public int Twice() => value * 2;
            }

            public sealed partial class Split(int value)
            {
                public int Value { get; } = value;
            }
            """;

        IReadOnlyList<CodeFixHarness.Offer> offers =
            await CodeFixHarness.Offers(
                    analyzer: new PrimaryConstructorParameterAnalyzer(),
                    provider: new PrimaryConstructorParameterCodeFixProvider(),
                    source: source,
                    others:
                    [
                        """
                        public sealed partial class Split
                        {
                            public int Twice() => value * 2;
                        }
                        """
                    ])
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(offers).IsEmpty();
    }

    // A fix of all reports in the document writes in where it compiles, and does not change the other parameters.
    [Test]
    public async Task AFixOfAllReportsWritesInWhereItCompiles()
    {
        const string source =
            """
            public sealed class Pair(int first, int second)
            {
                public int Sum { get; } = first + second;
            }

            public sealed class Captured(int value)
            {
                public int Read() => value;
            }

            public sealed class Single(string name)
            {
                public string Name { get; } = name;
            }
            """;

        string result =
            await CodeFixHarness.FixAll(
                    analyzer: new PrimaryConstructorParameterAnalyzer(),
                    provider: new PrimaryConstructorParameterCodeFixProvider(),
                    source: source,
                    equivalenceKey: "MC0003")
                .ConfigureAwait(continueOnCapturedContext: false);

        await Assert.That(result)
            .IsEqualTo(
                expected: source
                    .Replace(oldValue: "Pair(int first, int second)", newValue: "Pair(in int first, in int second)")
                    .Replace(oldValue: "Single(string name)", newValue: "Single(in string name)"));

        await Assert.That(
                new PrimaryConstructorParameterCodeFixProvider().GetFixAllProvider().GetSupportedFixAllScopes())
            .IsEquivalentTo(
                expected: [FixAllScope.Document, FixAllScope.Project, FixAllScope.Solution],
                ordering: CollectionOrdering.Any);
    }
}
