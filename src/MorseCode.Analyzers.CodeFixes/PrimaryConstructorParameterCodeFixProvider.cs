using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace MorseCode.Analyzers.CodeFixes;

/// <summary>
///     Writes <c>in</c> on a primary constructor parameter that MC0003
///     reports.
/// </summary>
/// <remarks>
///     <para>
///         The compiler refuses an <c>in</c> parameter that a member captures
///         (CS9109), that a lambda or a local function uses (CS1628), or that code
///         writes. Thus, the fix compiles each part of the type with <c>in</c>
///         first, and the provider offers it only when that adds no error. The
///         rule then wants a copy of the value in a field, which is a change that
///         the person makes.
///     </para>
///     <para>
///         The fix writes <c>in</c> only on a parameter with no modifier, or
///         with only <c>scoped</c>. A <c>ref</c>, <c>out</c>, or
///         <c>ref readonly</c> parameter has a different meaning, and the fix does
///         not change it.
///     </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PrimaryConstructorParameterCodeFixProvider))]
[Shared]
// ReSharper disable once InheritdocConsiderUsage - The summary of CodeFixProvider does not say which rule this provider fixes.
public sealed class PrimaryConstructorParameterCodeFixProvider : CodeFixProvider
{
    // The identifier of the rule. The tests run the analyzer and this provider together, thus a difference
    // fails them.
    private const string RuleId = "MC0003";

    private const string EquivalenceKey = "MC0003";

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(item: RuleId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => FixAllProvider.Create(fixAllAsync: FixAllAsync);

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root =
            await context.Document.GetSyntaxRootAsync(cancellationToken: context.CancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

        if (root is null)
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            if (Find(root: root, diagnostic: diagnostic) is not { } parameter
                || WithIn(parameter: parameter) is not { } withIn
                || !await CompilesWithInAsync(
                        document: context.Document,
                        root: root,
                        parameter: parameter,
                        withIn: withIn,
                        cancellationToken: context.CancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false))
            {
                continue;
            }

            Document document = context.Document;

            context.RegisterCodeFix(
                action: CodeAction.Create(
                    title: "Make the parameter in",
                    createChangedDocument: _ =>
                        Task.FromResult(
                            result: document.WithSyntaxRoot(
                                root: root.ReplaceNode(oldNode: parameter, newNode: withIn))),
                    equivalenceKey: EquivalenceKey),
                diagnostic: diagnostic);
        }
    }

    // A fix of all reports in a document. The fix checks each parameter alone against the source as it is, and
    // each parameter that compiles gets in. The in of one parameter does not change if a different one can have
    // it.
    private static async Task<Document?> FixAllAsync(
        FixAllContext context,
        Document document,
        ImmutableArray<Diagnostic> diagnostics)
    {
        SyntaxNode? root =
            await document.GetSyntaxRootAsync(cancellationToken: context.CancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

        if (root is null)
        {
            return null;
        }

        Dictionary<ParameterSyntax, ParameterSyntax> changes = [];

        foreach (Diagnostic diagnostic in diagnostics)
        {
            if (Find(root: root, diagnostic: diagnostic) is { } parameter
                && WithIn(parameter: parameter) is { } withIn
                && await CompilesWithInAsync(
                        document: document,
                        root: root,
                        parameter: parameter,
                        withIn: withIn,
                        cancellationToken: context.CancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false))
            {
                changes[parameter] = withIn;
            }
        }

        return document.WithSyntaxRoot(
            root: root.ReplaceNodes(nodes: changes.Keys, computeReplacementNode: (original, _) => changes[original]));
    }

    // The report is on the identifier of the parameter.
    private static ParameterSyntax? Find(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindToken(position: diagnostic.Location.SourceSpan.Start).Parent as ParameterSyntax;

    // The parameter with in, or null when it has a modifier other than scoped. Scoped comes before in. With no
    // modifier, in takes the leading trivia of the type, which is the space after an attribute or the indent of
    // a line.
    private static ParameterSyntax? WithIn(ParameterSyntax parameter)
    {
        SyntaxToken @in =
            SyntaxFactory.Token(kind: SyntaxKind.InKeyword).WithTrailingTrivia(trivia: SyntaxFactory.Space);

        return parameter switch
        {
            { Modifiers.Count: 0, Type: { } type } => parameter
                .WithType(type: type.WithLeadingTrivia())
                .WithModifiers(
                    modifiers: SyntaxFactory.TokenList(token: @in.WithLeadingTrivia(trivia: type.GetLeadingTrivia()))),
            { Modifiers: { Count: 1 } modifiers } when modifiers[index: 0].IsKind(kind: SyntaxKind.ScopedKeyword) =>
                parameter.AddModifiers(items: @in),
            _ => null
        };
    }

    // True when each part of the type compiles with no new error after the change. A part in a different document,
    // such as a part that a generator makes, can capture the parameter too, thus the check compiles each part.
    private static async Task<bool> CompilesWithInAsync(
        Document document,
        SyntaxNode root,
        SyntaxNode parameter,
        SyntaxNode withIn,
        CancellationToken cancellationToken)
    {
        SemanticModel? model =
            await document.GetSemanticModelAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

        if (model is null
            || parameter.Parent?.Parent is not TypeDeclarationSyntax type
            || model.GetDeclaredSymbol(declarationSyntax: type, cancellationToken: cancellationToken) is not { } symbol)
        {
            return false;
        }

        SyntaxNode[] parts =
        [
            .. symbol.DeclaringSyntaxReferences.Select(
                selector: reference => reference.GetSyntax(cancellationToken: cancellationToken))
        ];

        SyntaxAnnotation mark = new();

        SyntaxNode changedRoot =
            root.ReplaceNodes(
                nodes: parts.Where(predicate: part => part.SyntaxTree == model.SyntaxTree),
                computeReplacementNode: (original, _) =>
                    (original == type ? original.ReplaceNode(oldNode: parameter, newNode: withIn) : original)
                    .WithAdditionalAnnotations(annotations: mark));

        Document changed = document.WithSyntaxRoot(root: changedRoot);

        Compilation? compilation =
            await document.Project.GetCompilationAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

        Compilation? changedCompilation =
            await changed.Project.GetCompilationAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

        SyntaxTree? changedTree =
            await changed.GetSyntaxTreeAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

        if (compilation is null || changedCompilation is null || changedTree is null)
        {
            return false;
        }

        List<string> before = [];
        List<string> after = [];

        foreach (SyntaxNode part in parts)
        {
            before.AddRange(
                collection: ErrorsIn(
                    model: compilation.GetSemanticModel(syntaxTree: part.SyntaxTree),
                    span: part.Span));

            if (part.SyntaxTree == model.SyntaxTree)
            {
                continue;
            }

            // A document that did not change keeps its tree, but a generated document is made again, thus the
            // tree is found by its path.
            if (changedCompilation.SyntaxTrees.FirstOrDefault(
                    predicate: tree => tree.FilePath == part.SyntaxTree.FilePath) is not { } tree)
            {
                return false;
            }

            after.AddRange(
                collection: ErrorsIn(model: changedCompilation.GetSemanticModel(syntaxTree: tree), span: part.Span));
        }

        SemanticModel changedModel = changedCompilation.GetSemanticModel(syntaxTree: changedTree);

        SyntaxNode changedTreeRoot =
            await changedTree.GetRootAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

        foreach (SyntaxNode part in changedTreeRoot.GetAnnotatedNodes(syntaxAnnotation: mark))
        {
            after.AddRange(collection: ErrorsIn(model: changedModel, span: part.Span));
        }

        // An error that was there before does not stop the fix, but one more of any type does.
        Dictionary<string, int> counts =
            before
                .GroupBy(keySelector: static id => id)
                .ToDictionary(keySelector: static group => group.Key, elementSelector: static group => group.Count());

        return after
            .GroupBy(keySelector: static id => id)
            .All(
                predicate: group =>
                    group.Count() <= (counts.TryGetValue(key: group.Key, value: out int count) ? count : 0));
    }

    private static IEnumerable<string> ErrorsIn(SemanticModel model, TextSpan span) =>
        model.GetDiagnostics(span: span)
            .Where(predicate: static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(selector: static diagnostic => diagnostic.Id);
}
