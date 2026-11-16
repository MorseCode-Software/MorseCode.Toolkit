using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace MorseCode.Analyzers.CodeFixes;

/// <summary>Puts the arguments that MC0001 reports in the order of the parameters.</summary>
/// <remarks>
///     <para>
///         C# evaluates the arguments of a call in the order of the source, also
///         when they have names. Thus, a new order can change when each argument
///         runs. The fix keeps the evaluation where no argument has an effect, or
///         where one argument has an effect and each other argument is a constant.
///         There, its title says only that it puts the arguments in order. In each
///         other case, its title says that it changes the order in which the
///         arguments run, and it has a different equivalence key. Thus, a fix of
///         all reports with the first key never moves an effect.
///     </para>
///     <para>
///         The analyzer puts the new order into each report. Thus, the fix maps
///         the arguments to the parameters as the rule does. Each position of the
///         list keeps its own trivia, thus the layout of the list stays as it was.
///     </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NamedArgumentOrderCodeFixProvider))]
[Shared]
// ReSharper disable once InheritdocConsiderUsage - The summary of CodeFixProvider does not say which rule this provider fixes.
public sealed class NamedArgumentOrderCodeFixProvider : CodeFixProvider
{
    // The identifier of the rule. The tests run the analyzer and this provider together, thus a difference
    // fails them.
    private const string RuleId = "MC0001";

    // The property of a report that holds the new order, as NamedArgumentOrderAnalyzer writes it.
    private const string OrderProperty = "Order";

    private const string KeepsEvaluationKey = "MC0001";

    private const string ChangesEvaluationKey = "MC0001.ChangesEvaluationOrder";

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

        SemanticModel? model =
            await context.Document.GetSemanticModelAsync(cancellationToken: context.CancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

        if (root is null || model is null)
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            if (Find(root: root, diagnostic: diagnostic) is not { } target
                || CanMove(model: model, list: target.List) is not { } keepsEvaluation)
            {
                continue;
            }

            Document document = context.Document;

            context.RegisterCodeFix(
                action: CodeAction.Create(
                    title: keepsEvaluation
                        ? "Put the arguments in the order of the parameters"
                        : "Put the arguments in the order of the parameters, which changes the order in which they run",
                    createChangedDocument: _ =>
                        Task.FromResult(result: document.WithSyntaxRoot(root: Reorder(root: root, targets: [target]))),
                    equivalenceKey: keepsEvaluation ? KeepsEvaluationKey : ChangesEvaluationKey),
                diagnostic: diagnostic);
        }
    }

    // A fix of all reports in a document. The key of the fix that the person chose sets the reports that it
    // takes. The key that keeps the evaluation takes only the reports that keep it. The other key takes all. One
    // argument list can be in a different list, thus one replacement rewrites the lists, from the innermost out.
    private static async Task<Document?> FixAllAsync(
        FixAllContext context,
        Document document,
        ImmutableArray<Diagnostic> diagnostics)
    {
        SyntaxNode? root =
            await document.GetSyntaxRootAsync(cancellationToken: context.CancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

        SemanticModel? model =
            await document.GetSemanticModelAsync(cancellationToken: context.CancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

        if (root is null || model is null)
        {
            return null;
        }

        bool onlyWhereEvaluationStays = context.CodeActionEquivalenceKey == KeepsEvaluationKey;

        Target[] targets =
        [
            .. diagnostics
                .Select(selector: diagnostic => Find(root: root, diagnostic: diagnostic))
                .OfType<Target>()
                .Where(
                    predicate: target =>
                        CanMove(model: model, list: target.List) is { } keepsEvaluation
                        && (keepsEvaluation || !onlyWhereEvaluationStays))
        ];

        return document.WithSyntaxRoot(root: Reorder(root: root, targets: targets));
    }

    // The argument list of a report, and the order that the report holds. Null when the report does not have a
    // correct order for the list that it marks.
    private static Target? Find(SyntaxNode root, Diagnostic diagnostic)
    {
        SyntaxNode node = root.FindNode(span: diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

        int count =
            node switch
            {
                BaseArgumentListSyntax list => list.Arguments.Count,
                AttributeArgumentListSyntax list => list.Arguments.Count,
                _ => -1
            };

        if (count < 0
            || !diagnostic.Properties.TryGetValue(key: OrderProperty, value: out string? text)
            || text is null)
        {
            return null;
        }

        int[] order =
        [
            .. text.Split(separator: ',')
                .Select(selector: static index => int.TryParse(s: index, result: out int value) ? value : -1)
        ];

        bool isPermutation =
            order.Length <= count
            && order.OrderBy(keySelector: static index => index)
                .SequenceEqual(second: Enumerable.Range(start: 0, count: order.Length));

        return isPermutation ? new Target(list: node, order: order) : null;
    }

    // Null when the fix cannot move the arguments. That occurs when a different argument names a variable that
    // one argument declares, because a new order can put the use before the declaration. Otherwise, true when the
    // new order keeps the evaluation of the arguments.
    private static bool? CanMove(SemanticModel model, SyntaxNode list)
    {
        // The constructor arguments of an attribute are constants, typeof, and arrays of them, thus their
        // order has no effect.
        if (list is not BaseArgumentListSyntax arguments)
        {
            return true;
        }

        if (arguments.Arguments.Any(
                predicate: argument => DeclaresANameOfAnother(argument: argument, arguments: arguments.Arguments)))
        {
            return null;
        }

        Evaluation[] evaluations =
        [
            .. arguments.Arguments.Select(selector: argument => EvaluationOf(model: model, argument: argument))
        ];

        int effects = evaluations.Count(predicate: static evaluation => evaluation == Evaluation.Effect);

        // With no effect, each argument gives the same value in any order. With one effect, a constant cannot
        // see it, but a read of a variable can.
        return effects == 0
               || (effects == 1 && evaluations.All(predicate: static evaluation => evaluation != Evaluation.Read));
    }

    // True when the argument declares a variable, and a different argument of the list has its name.
    private static bool DeclaresANameOfAnother(SyntaxNode argument, SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        HashSet<string> declared =
        [
            .. argument.DescendantNodes()
                .OfType<SingleVariableDesignationSyntax>()
                .Select(selector: static designation => designation.Identifier.ValueText)
        ];

        return declared.Count > 0
               && arguments
                   .Where(predicate: other => other != argument)
                   .SelectMany(selector: static other => other.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
                   .Any(predicate: name => declared.Contains(item: name.Identifier.ValueText));
    }

    private static Evaluation EvaluationOf(SemanticModel model, ArgumentSyntax argument)
    {
        IOperation? operation = model.GetOperation(node: argument.Expression);

        // A conversion that the user defines calls a method during the evaluation of the argument.
        bool convertsByMethod =
            operation?.Parent is IConversionOperation { IsImplicit: true, OperatorMethod: not null }
                or IArgumentOperation { InConversion.MethodSymbol: not null };

        return operation is null || convertsByMethod ? Evaluation.Effect : EvaluationOf(operation: operation);
    }

    // What evaluation of an operation can do. Each case that is not known is an effect.
    private static Evaluation EvaluationOf(IOperation operation)
    {
        if (operation.ConstantValue.HasValue)
        {
            return Evaluation.Constant;
        }

        return operation switch
        {
            ITypeOfOperation
                or IDefaultValueOperation
                or ISizeOfOperation
                or INameOfOperation
                or IAnonymousFunctionOperation
                or IDiscardOperation
                or IDeclarationExpressionOperation =>
                Evaluation.Constant,
            IDelegateCreationOperation
                {
                    Target: IAnonymousFunctionOperation or IMethodReferenceOperation { Instance: null }
                } =>
                Evaluation.Constant,
            IDelegateCreationOperation { Target: IMethodReferenceOperation { Instance: { } instance } } =>
                Stronger(first: Evaluation.Read, second: EvaluationOf(operation: instance)),
            ILocalReferenceOperation or IParameterReferenceOperation or IInstanceReferenceOperation =>
                Evaluation.Read,
            IFieldReferenceOperation { Instance: null } => Evaluation.Read,
            IFieldReferenceOperation { Instance: { } instance } =>
                Stronger(first: Evaluation.Read, second: EvaluationOf(operation: instance)),
            IConversionOperation { OperatorMethod: null } conversion => EvaluationOf(operation: conversion.Operand),
            _ => Evaluation.Effect
        };
    }

    private static Evaluation Stronger(Evaluation first, Evaluation second) => first > second ? first : second;

    // The argument lists of the targets, each in its order. The replacement changes an inner list first, and the
    // outer list then moves the changed list. The count and the positions of the arguments of a list do not
    // change, thus its order applies to the changed list too.
    private static SyntaxNode Reorder(SyntaxNode root, IEnumerable<Target> targets)
    {
        Dictionary<SyntaxNode, int[]> orders =
            targets.ToDictionary(
                keySelector: static target => target.List,
                elementSelector: static target => target.Order);

        return root.ReplaceNodes(
            nodes: orders.Keys,
            computeReplacementNode: (original, rewritten) =>
                rewritten switch
                {
                    BaseArgumentListSyntax list => list.WithArguments(
                        arguments: Reorder(arguments: list.Arguments, order: orders[original])),
                    AttributeArgumentListSyntax list => list.WithArguments(
                        arguments: Reorder(arguments: list.Arguments, order: orders[original])),
                    _ => rewritten
                });
    }

    // An order shorter than the list covers the constructor arguments of an attribute, which come before the
    // assignments of its properties. Those assignments stay where they are.
    private static SeparatedSyntaxList<TArgument> Reorder<TArgument>(
        SeparatedSyntaxList<TArgument> arguments,
        IReadOnlyList<int> order)
        where TArgument : SyntaxNode =>
        SyntaxFactory.SeparatedList(
            nodes: arguments.Select(
                selector: (argument, position) =>
                    position < order.Count
                        ? arguments[index: order[position]]
                            .WithLeadingTrivia(trivia: argument.GetLeadingTrivia())
                            .WithTrailingTrivia(trivia: argument.GetTrailingTrivia())
                        : argument),
            separators: arguments.GetSeparators());

    private enum Evaluation
    {
        // The value is always the same, and the evaluation has no effect.
        Constant,

        // The evaluation has no effect, but the value can change: a variable, a parameter, or a field.
        Read,

        // The evaluation can have an effect, or nothing here knows that it cannot.
        Effect
    }

    // An argument list with its new order, as the index of the argument that goes to each position.
    private sealed class Target(in SyntaxNode list, in int[] order)
    {
        public SyntaxNode List { get; } = list;

        public int[] Order { get; } = order;
    }
}
