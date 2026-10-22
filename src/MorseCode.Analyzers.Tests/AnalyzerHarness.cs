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
    ///     Compiles the source, and gives what the analyzer reports, in the order of the source. The sample
    ///     must compile, because an analyzer does not examine code that does not bind.
    /// </summary>
    /// <param name="analyzer">The analyzer.</param>
    /// <param name="source">The sample.</param>
    /// <param name="library">
    ///     The source of a second assembly that the sample references, or null. It stands for a package: the
    ///     compiler reads its members from metadata.
    /// </param>
    /// <returns>The diagnostics of the analyzer.</returns>
    public static async Task<IReadOnlyList<Diagnostic>> Analyze(
        DiagnosticAnalyzer analyzer,
        string source,
        string? library = null)
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

        ImmutableArray<Diagnostic> errors =
        [
            .. compilation.GetDiagnostics()
                .Concat(second: libraryCompilation?.GetDiagnostics() ?? [])
                .Where(predicate: static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        ];

        if (errors.Length > 0)
        {
            throw new InvalidOperationException(message: $"The sample does not compile: {errors[0]}");
        }

        ImmutableArray<Diagnostic> found =
            await compilation.WithAnalyzers(analyzers: [analyzer])
                .GetAnalyzerDiagnosticsAsync()
                .ConfigureAwait(continueOnCapturedContext: false);

        return [.. found.OrderBy(keySelector: static diagnostic => diagnostic.Location.SourceSpan.Start)];
    }

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
                nullableContextOptions: NullableContextOptions.Enable));

    // The assemblies of the runtime that runs the tests, which the samples compile against.
    private static IEnumerable<MetadataReference> PlatformReferences() =>
        ((string?)AppContext.GetData(name: "TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
        .Split(separator: Path.PathSeparator, options: StringSplitOptions.RemoveEmptyEntries)
        .Select(selector: static path => MetadataReference.CreateFromFile(path: path));
}
