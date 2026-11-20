using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MorseCode.StagedConstruction.Analyzers.Benchmarks;

/// <summary>
///     What the continuation analyzer adds to the compile of a file with many
///     continuations.
/// </summary>
/// <remarks>
///     <para>
///         The baseline compiles a new copy of the compilation and gets its
///         diagnostics. The other benchmark does the same with the analyzer. Thus,
///         the difference is the time and the memory of the analyzer, and the ratio
///         compares them with the compile.
///     </para>
///     <para>
///         <see cref="Shape" /> selects the code of each continuation. Typical
///         returns the result of <c>Construct</c> directly, as most code does.
///         LocalsAndBranches returns a local variable that a loop, branches, a
///         conditional expression, and a <c>try</c> statement write. That is the
///         most work for an analysis that finds the values of a local variable.
///     </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(runtimeMoniker: RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 3, iterationCount: 15)]
// Not sealed, and not private to this file: BenchmarkDotNet derives from this and finds it by
// reflection.
// ReSharper disable once ClassCanBeSealed.Global
// ReSharper disable once MemberCanBeFileLocal
public class ContinuationAnalyzerBenchmarks
{
    private const string Preamble =
        """
        using System;
        using MorseCode.StagedConstruction;
        using SodaFlow;

        public sealed class Values;

        public sealed class Thing(in Values values)
        {
            public Values Values { get; } = values;
        }

        public static class Base
        {
            public static TResult Create<TResult>(StageContinuation<string, Constructor<Values>, TResult> continuation) =>
                continuation(output: "output", next: Constructor.From(values: new Values())).Value;
        }

        """;

    private readonly ImmutableArray<DiagnosticAnalyzer> analyzers = [new ContinuationAnalyzer()];

    private CSharpCompilation compilation = CSharpCompilation.Create(assemblyName: "Empty");

    /// <summary>The code of each continuation.</summary>
    public enum ContinuationShape
    {
        /// <summary>A continuation that returns the result of <c>Construct</c> directly.</summary>
        Typical,

        /// <summary>
        ///     A continuation that returns a local variable with many writes. See
        ///     the remarks of the class.
        /// </summary>
        LocalsAndBranches
    }

    /// <summary>The code of each continuation.</summary>
    [ParamsAllValues]
    public ContinuationShape Shape { get; [UsedImplicitly] set; }

    /// <summary>The number of continuations in the file.</summary>
    [Params(100, 400)]
    public int Continuations { get; [UsedImplicitly] set; }

    /// <summary>
    ///     Makes the compilation of the file. Each benchmark compiles a new copy of it, thus no
    ///     iteration reads what a different iteration bound.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     The shape has no code, or the generated code does not compile.
    /// </exception>
    [GlobalSetup]
    public void Setup()
    {
        StringBuilder source = new(value: Preamble);
        source.AppendLine(value: "public static class Uses");
        source.AppendLine(value: "{");

        for (int index = 0; index < this.Continuations; index++)
        {
            source.AppendLine(
                value: this.Shape switch
                {
                    ContinuationShape.Typical => Typical(index: index),
                    ContinuationShape.LocalsAndBranches => LocalsAndBranches(index: index),
                    _ => throw new InvalidOperationException(message: $"No code for the shape {this.Shape}.")
                });
        }

        source.AppendLine(value: "}");

        this.compilation =
            CSharpCompilation.Create(
                assemblyName: "Benchmark",
                syntaxTrees: [CSharpSyntaxTree.ParseText(text: source.ToString())],
                references: PlatformReferences(),
                options: new CSharpCompilationOptions(
                    outputKind: OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContextOptions.Enable));

        Diagnostic[] errors =
        [
            .. this.compilation.GetDiagnostics()
                .Where(predicate: static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        ];

        if (errors.Length > 0)
        {
            throw new InvalidOperationException(message: $"The generated code does not compile: {errors[0]}");
        }
    }

    /// <summary>Compiles the file, with no analyzer.</summary>
    /// <returns>The number of diagnostics, so that the work is not removed.</returns>
    [Benchmark(Baseline = true, Description = "compile")]
    public int Compile() => this.compilation.Clone().GetDiagnostics().Length;

    /// <summary>Compiles the file, with the continuation analyzer.</summary>
    /// <returns>The number of diagnostics, so that the work is not removed.</returns>
    [Benchmark(Description = "compile and analyze")]
    public int CompileAndAnalyze() =>
        this.compilation.Clone()
            .WithAnalyzers(analyzers: this.analyzers)
            .GetAllDiagnosticsAsync()
            .GetAwaiter()
            .GetResult()
            .Length;

    // A continuation that returns the result of Construct directly.
    private static string Typical(int index) =>
        $"""
             public static Thing Typical{index}() =>
                 Base.Create(continuation: static (output, construct) => construct.Construct(constructor: static values => new Thing(values: values)));
         """;

    // A continuation that returns a local variable, which a loop, branches, a conditional expression, and a
    // try statement write. Each value comes from the handle, thus the analyzer reports nothing.
    private static string LocalsAndBranches(int index) =>
        $$"""
              public static Thing Branches{{index}}(bool flag, int count) =>
                  Base.Create(
                      continuation: (output, construct) =>
                      {
                          Constructed<Thing> made = construct.Construct(constructor: static values => new Thing(values: values));

                          for (int step = 0; step < count; step++)
                          {
                              if (flag && step % 3 == {{index % 3}})
                              {
                                  made = construct.Construct(constructor: static values => new Thing(values: values));
                              }
                              else if (step % 5 == 0)
                              {
                                  Constructed<Thing> other = construct.Construct(constructor: static values => new Thing(values: values));
                                  made = other;
                              }
                          }

                          try
                          {
                              made = flag ? made : construct.Construct(constructor: static values => new Thing(values: values));
                          }
                          catch (InvalidOperationException)
                          {
                              made = construct.Construct(constructor: static values => new Thing(values: values));
                          }

                          Constructed<Thing> result = made;

                          return result;
                      });
          """;

    // The assemblies of the runtime that runs the benchmark, which the generated code compiles against. They
    // include the references of this project.
    private static ImmutableArray<MetadataReference> PlatformReferences() =>
    [
        .. (AppContext.GetData(name: "TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty)
            .Split(separator: Path.PathSeparator, options: StringSplitOptions.RemoveEmptyEntries)
            .Select(selector: static path => MetadataReference.CreateFromFile(path: path))
    ];
}
