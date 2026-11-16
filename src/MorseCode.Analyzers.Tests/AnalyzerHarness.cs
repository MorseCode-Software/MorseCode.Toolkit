using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MorseCode.Analyzers.Tests;

/// <summary>
///     Compiles a sample of code with one analyzer, and reads what it reports.
/// </summary>
internal static class AnalyzerHarness
{
    /// <summary>
    ///     Compiles the source, and gives what the analyzer reports, in the order of the source. A sample
    ///     without <paramref name="errors" /> must compile. A sample with them stands for code that an
    ///     editor analyzes before it compiles, and it must have those errors and no others.
    /// </summary>
    /// <param name="analyzer">The analyzer.</param>
    /// <param name="source">The sample.</param>
    /// <param name="library">
    ///     The source of a second assembly that the sample references, or null. It stands for a package: the
    ///     compiler reads its members from metadata.
    /// </param>
    /// <param name="errors">
    ///     The identifiers of the compiler errors that the sample must have, in the order of the source, or
    ///     null for a sample that compiles. The library must always compile.
    /// </param>
    /// <returns>
    ///     The diagnostics of the analyzer. If the analyzer throws, this includes the AD0001 that tells it.
    /// </returns>
    public static async Task<IReadOnlyList<Diagnostic>> Analyze(
        DiagnosticAnalyzer analyzer,
        string source,
        string? library = null,
        IReadOnlyList<string>? errors = null)
    {
        List<MetadataReference> references = [.. PlatformReferences()];
        CSharpCompilation? libraryCompilation = null;

        if (library is not null)
        {
            libraryCompilation = Compile(assemblyName: "Library", text: library, references: references);

            using MemoryStream image = new();
            _ = libraryCompilation.Emit(peStream: image);
            references.Add(item: MetadataReference.CreateFromImage(peImage: [.. image.ToArray()]));
        }

        CSharpCompilation compilation = Compile(assemblyName: "Sample", text: source, references: references);

        ImmutableArray<Diagnostic> libraryErrors = ErrorsOf(compilation: libraryCompilation);

        if (libraryErrors.Length > 0)
        {
            throw new InvalidOperationException(message: $"The library does not compile: {libraryErrors[0]}");
        }

        ImmutableArray<Diagnostic> sampleErrors = ErrorsOf(compilation: compilation);
        string[] sampleErrorIds = [.. sampleErrors.Select(selector: static diagnostic => diagnostic.Id)];

        if (errors is null && sampleErrors.Length > 0)
        {
            throw new InvalidOperationException(message: $"The sample does not compile: {sampleErrors[0]}");
        }

        if (errors is not null && !sampleErrorIds.SequenceEqual(second: errors))
        {
            throw new InvalidOperationException(
                message:
                $"The sample has the errors {string.Join(separator: ", ", value: sampleErrorIds)}, and not "
                + $"{string.Join(separator: ", ", values: errors)}.");
        }

        ImmutableArray<Diagnostic> found =
            await compilation.WithAnalyzers(analyzers: [analyzer])
                .GetAnalyzerDiagnosticsAsync()
                .ConfigureAwait(continueOnCapturedContext: false);

        return [.. found.OrderBy(keySelector: static diagnostic => diagnostic.Location.SourceSpan.Start)];
    }

    // The errors of a compilation, in the order of the source.
    private static ImmutableArray<Diagnostic> ErrorsOf(Compilation? compilation) =>
    [
        .. (compilation?.GetDiagnostics() ?? [])
            .Where(predicate: static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .OrderBy(keySelector: static diagnostic => diagnostic.Location.SourceSpan.Start)
    ];

    private static CSharpCompilation Compile(
        string assemblyName,
        string text,
        IEnumerable<MetadataReference> references) =>
        CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees:
            [
                CSharpSyntaxTree.ParseText(
                    text: text,
                    options: new CSharpParseOptions(languageVersion: LanguageVersion.Preview))
            ],
            references: references,
            options: new CSharpCompilationOptions(
                outputKind: OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable));

    // The assemblies of the runtime that runs the tests, which the samples compile against.
    private static IEnumerable<MetadataReference> PlatformReferences() =>
        (AppContext.GetData(name: "TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty)
        .Split(separator: Path.PathSeparator, options: StringSplitOptions.RemoveEmptyEntries)
        .Select(selector: static path => MetadataReference.CreateFromFile(path: path));
}
