using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace MorseCode.Analyzers.Tests;

/// <summary>
///     Puts a sample of code in a workspace, as an editor does. It then
///     applies the fixes of a code fix provider to what an analyzer reports.
/// </summary>
/// <remarks>
///     Each sample must compile, and so must each result of a fix. Thus, a
///     fix that makes code that does not compile fails the test that applies it.
/// </remarks>
internal static class CodeFixHarness
{
    /// <summary>
    ///     Gives each fix that the provider offers, for each report of the
    ///     analyzer on the sample, in the order of the source.
    /// </summary>
    /// <param name="analyzer">The analyzer.</param>
    /// <param name="provider">The code fix provider.</param>
    /// <param name="source">The sample, which the fixes change.</param>
    /// <param name="others">
    ///     The sources of more documents of the same project, or
    ///     null. They are the other files of a project, such as a different part of a
    ///     partial type.
    /// </param>
    /// <returns>The fixes, each with the sample as the fix leaves it.</returns>
    public static async Task<IReadOnlyList<Offer>> Offers(
        DiagnosticAnalyzer analyzer,
        CodeFixProvider provider,
        string source,
        IReadOnlyList<string>? others = null)
    {
        (Document document, ImmutableArray<Diagnostic> reports) =
            await Open(analyzer: analyzer, source: source, others: others)
                .ConfigureAwait(continueOnCapturedContext: false);

        List<Offer> offers = [];

        foreach (Diagnostic report in reports)
        {
            List<CodeAction> actions = [];

            await provider.RegisterCodeFixesAsync(
                    context: new CodeFixContext(
                        document: document,
                        diagnostic: report,
                        registerCodeFix: (action, _) => actions.Add(item: action),
                        cancellationToken: CancellationToken.None))
                .ConfigureAwait(continueOnCapturedContext: false);

            foreach (CodeAction action in actions)
            {
                offers.Add(
                    item: new Offer(
                        title: action.Title,
                        key: action.EquivalenceKey,
                        result: await Apply(document: document.Id, action: action)
                            .ConfigureAwait(continueOnCapturedContext: false)));
            }
        }

        return offers;
    }

    /// <summary>
    ///     Applies the fix of all reports in the sample, as an editor does for
    ///     one document. It uses the equivalence key of the fix that the person chose.
    /// </summary>
    /// <param name="analyzer">The analyzer.</param>
    /// <param name="provider">The code fix provider.</param>
    /// <param name="source">The sample, which the fix changes.</param>
    /// <param name="equivalenceKey">The equivalence key of the chosen fix.</param>
    /// <returns>The sample as the fix leaves it.</returns>
    public static async Task<string> FixAll(
        DiagnosticAnalyzer analyzer,
        CodeFixProvider provider,
        string source,
        string equivalenceKey)
    {
        (Document document, ImmutableArray<Diagnostic> reports) =
            await Open(analyzer: analyzer, source: source, others: null)
                .ConfigureAwait(continueOnCapturedContext: false);

        FixAllProvider fixAll =
            provider.GetFixAllProvider()
            ?? throw new InvalidOperationException(message: "The provider has no fix of all reports.");

        CodeAction action =
            await fixAll.GetFixAsync(
                    fixAllContext: new FixAllContext(
                        document: document,
                        codeFixProvider: provider,
                        scope: FixAllScope.Document,
                        codeActionEquivalenceKey: equivalenceKey,
                        diagnosticIds: provider.FixableDiagnosticIds,
                        fixAllDiagnosticProvider: new Reports(found: reports),
                        cancellationToken: CancellationToken.None))
                .ConfigureAwait(continueOnCapturedContext: false)
            ?? throw new InvalidOperationException(message: "The fix of all reports made no change.");

        return await Apply(document: document.Id, action: action).ConfigureAwait(continueOnCapturedContext: false);
    }

    private static async Task<(Document Document, ImmutableArray<Diagnostic> Reports)> Open(
        DiagnosticAnalyzer analyzer,
        string source,
        IReadOnlyList<string>? others)
    {
        AdhocWorkspace workspace = new();

        Project project =
            workspace.AddProject(
                projectInfo: ProjectInfo.Create(
                    id: ProjectId.CreateNewId(),
                    version: VersionStamp.Default,
                    name: "Sample",
                    assemblyName: "Sample",
                    language: LanguageNames.CSharp,
                    compilationOptions: new CSharpCompilationOptions(
                        outputKind: OutputKind.DynamicallyLinkedLibrary,
                        nullableContextOptions: NullableContextOptions.Enable),
                    parseOptions: new CSharpParseOptions(languageVersion: LanguageVersion.Preview),
                    metadataReferences: PlatformReferences()));

        Document sample = project.AddDocument(name: "Sample.cs", text: SourceText.From(text: source));
        project = sample.Project;

        foreach ((string other, int index) in (others ?? []).Select(selector: static (other, index) => (other, index)))
        {
            project = project.AddDocument(name: $"Other{index}.cs", text: SourceText.From(text: other)).Project;
        }

        Document document =
            project.GetDocument(documentId: sample.Id)
            ?? throw new InvalidOperationException(message: "The project lost the sample.");

        Compilation compilation =
            await document.Project.GetCompilationAsync().ConfigureAwait(continueOnCapturedContext: false)
            ?? throw new InvalidOperationException(message: "The sample has no compilation.");

        ThrowOnErrors(diagnostics: compilation.GetDiagnostics(), what: "The sample");

        SyntaxTree tree =
            await document.GetSyntaxTreeAsync().ConfigureAwait(continueOnCapturedContext: false)
            ?? throw new InvalidOperationException(message: "The sample has no syntax tree.");

        ImmutableArray<Diagnostic> found =
            await compilation.WithAnalyzers(analyzers: [analyzer])
                .GetAnalyzerDiagnosticsAsync()
                .ConfigureAwait(continueOnCapturedContext: false);

        return (document,
        [
            .. found
                .Where(predicate: diagnostic => diagnostic.Location.SourceTree == tree)
                .OrderBy(keySelector: static diagnostic => diagnostic.Location.SourceSpan.Start)
        ]);
    }

    // The text of the document after the fix. The changed project must compile.
    private static async Task<string> Apply(DocumentId document, CodeAction action)
    {
        ImmutableArray<CodeActionOperation> operations =
            await action.GetOperationsAsync(cancellationToken: CancellationToken.None)
                .ConfigureAwait(continueOnCapturedContext: false);

        Solution changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;

        Document result =
            changed.GetDocument(documentId: document)
            ?? throw new InvalidOperationException(message: "The fix removed the document.");

        Compilation compilation =
            await result.Project.GetCompilationAsync().ConfigureAwait(continueOnCapturedContext: false)
            ?? throw new InvalidOperationException(message: "The result has no compilation.");

        ThrowOnErrors(diagnostics: compilation.GetDiagnostics(), what: $"The result of '{action.Title}'");

        SourceText text = await result.GetTextAsync().ConfigureAwait(continueOnCapturedContext: false);

        return text.ToString();
    }

    private static void ThrowOnErrors(ImmutableArray<Diagnostic> diagnostics, string what)
    {
        if (diagnostics.FirstOrDefault(predicate: static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            is { } error)
        {
            throw new InvalidOperationException(message: $"{what} does not compile: {error}");
        }
    }

    // The assemblies of the runtime that runs the tests, which the samples compile against.
    private static IEnumerable<MetadataReference> PlatformReferences() =>
        (AppContext.GetData(name: "TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty)
        .Split(separator: Path.PathSeparator, options: StringSplitOptions.RemoveEmptyEntries)
        .Select(selector: static path => MetadataReference.CreateFromFile(path: path));

    /// <summary>A fix that a provider offers for one report.</summary>
    /// <param name="title">The title that an editor shows.</param>
    /// <param name="key">
    ///     The equivalence key, which groups the fix with others for a
    ///     fix of all.
    /// </param>
    /// <param name="result">The sample as the fix leaves it.</param>
    internal sealed class Offer(in string title, in string? key, in string result)
    {
        /// <summary>Gets the title that an editor shows.</summary>
        public string Title { get; } = title;

        /// <summary>Gets the equivalence key.</summary>
        public string? Key { get; } = key;

        /// <summary>Gets the sample as the fix leaves it.</summary>
        public string Result { get; } = result;
    }

    // The reports that a fix of all reports takes, which are those of the analyzer on the sample.
    private sealed class Reports(in ImmutableArray<Diagnostic> found) : FixAllContext.DiagnosticProvider
    {
        private readonly ImmutableArray<Diagnostic> found = found;

        public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(
            Document document,
            CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>(result: this.found);

        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(
            Project project,
            CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>(result: []);

        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(
            Project project,
            CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>(result: this.found);
    }
}
