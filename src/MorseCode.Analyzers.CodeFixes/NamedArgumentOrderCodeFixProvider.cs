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
///         the arguments to the parameters as the rule does. Each argument takes
///         its comments with it, and each position keeps its whitespace and its
///         line breaks. Thus, the layout of the list stays as it was.
///     </para>
///     <para>
///         The provider offers no fix where a directive is between the arguments,
///         because the directive guards the argument at its position. It offers
///         no fix where the move puts a comment to the end of a line before a
///         token on the same line.
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
                || !KeepsTrivia(target: target)
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
                        KeepsTrivia(target: target)
                        && CanMove(model: model, list: target.List) is { } keepsEvaluation
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
                        arguments: Rearrange(arguments: list.Arguments, order: orders[original]).Arguments),
                    AttributeArgumentListSyntax list => list.WithArguments(
                        arguments: Rearrange(arguments: list.Arguments, order: orders[original]).Arguments),
                    _ => rewritten
                });
    }

    // False when the new order puts a directive or a comment in an incorrect position. A directive between the
    // arguments guards the argument at its position, thus a move gives it to a different argument. A comment to
    // the end of a line must have a line break after it, also at its new position.
    private static bool KeepsTrivia(Target target) =>
        target.List switch
        {
            BaseArgumentListSyntax list => KeepsTrivia(list: list, arguments: list.Arguments, order: target.Order),
            AttributeArgumentListSyntax list => KeepsTrivia(list: list, arguments: list.Arguments, order: target.Order),
            _ => false
        };

    private static bool KeepsTrivia<TArgument>(
        SyntaxNode list,
        SeparatedSyntaxList<TArgument> arguments,
        IReadOnlyList<int> order)
        where TArgument : SyntaxNode =>
        !arguments
            .SelectMany(
                selector: static argument => argument.GetLeadingTrivia().Concat(second: argument.GetTrailingTrivia()))
            .Concat(
                second: arguments.GetSeparators()
                    .SelectMany(selector: static comma => comma.LeadingTrivia.Concat(second: comma.TrailingTrivia)))
            .Concat(second: list.GetFirstToken().TrailingTrivia)
            .Concat(second: list.GetLastToken().LeadingTrivia)
            .Any(predicate: static trivia => trivia.IsDirective || trivia.IsKind(kind: SyntaxKind.DisabledTextTrivia))
        && Rearrange(arguments: arguments, order: order).KeepsComments;

    // The arguments in the new order. Each argument takes its comments with it. Those are the comments on the
    // lines above it, the comments after it, and the comment after its comma. Each position keeps its
    // whitespace and its line breaks. An order shorter than the list covers the constructor arguments of an
    // attribute, which come before the assignments of its properties. Those assignments stay where they are.
    // KeepsComments is false when a comment to the end of a line has no line break after it at its new position.
    private static (SeparatedSyntaxList<TArgument> Arguments, bool KeepsComments) Rearrange<TArgument>(
        SeparatedSyntaxList<TArgument> arguments,
        IReadOnlyList<int> order)
        where TArgument : SyntaxNode
    {
        SyntaxToken[] commas = [.. arguments.GetSeparators()];
        List<TArgument> nodes = [];
        List<SyntaxToken> separators = [];
        bool keepsComments = true;

        for (int position = 0; position < arguments.Count; position++)
        {
            TArgument here = arguments[index: position];

            if (position >= order.Count)
            {
                nodes.Add(item: here);

                if (position < commas.Length)
                {
                    separators.Add(item: commas[position]);
                }

                continue;
            }

            int from = order[position];
            TArgument moved = arguments[index: from];

            SyntaxTriviaList leading =
                LayoutBeforeComments(trivia: here.GetLeadingTrivia())
                    .AddRange(trivia: CommentsAndAfter(trivia: moved.GetLeadingTrivia()));

            SyntaxTriviaList movedAfter = BeforeLineBreak(trivia: moved.GetTrailingTrivia());
            SyntaxTriviaList hereLineBreak = FromLineBreak(trivia: here.GetTrailingTrivia());

            SyntaxTriviaList movedCommaComment =
                from < commas.Length
                    ? CommentAfterComma(trivia: commas[from].TrailingTrivia)
                    : SyntaxFactory.TriviaList();

            SyntaxTriviaList trailing;

            if (position < commas.Length)
            {
                trailing = movedAfter.AddRange(trivia: hereLineBreak);

                SyntaxTriviaList commaTrailing =
                    movedCommaComment.AddRange(trivia: LayoutAfterComma(trivia: commas[position].TrailingTrivia));

                separators.Add(item: commas[position].WithTrailingTrivia(trivia: commaTrailing));
                keepsComments &= EndsEachLineComment(trivia: commaTrailing);
            }
            else
            {
                trailing = movedAfter.AddRange(trivia: movedCommaComment).AddRange(trivia: hereLineBreak);
            }

            keepsComments &= EndsEachLineComment(trivia: trailing);
            nodes.Add(item: moved.WithLeadingTrivia(trivia: leading).WithTrailingTrivia(trivia: trailing));
        }

        return (SyntaxFactory.SeparatedList(nodes: nodes, separators: separators), keepsComments);
    }

    private static bool IsLayout(SyntaxTrivia trivia) =>
        trivia.IsKind(kind: SyntaxKind.WhitespaceTrivia) || trivia.IsKind(kind: SyntaxKind.EndOfLineTrivia);

    // The whitespace and the line breaks before the first comment, or all of the trivia when it has no comment.
    private static SyntaxTriviaList LayoutBeforeComments(SyntaxTriviaList trivia) =>
        SyntaxFactory.TriviaList(trivias: trivia.TakeWhile(predicate: IsLayout));

    // The trivia from the first comment to the end, or nothing when it has no comment.
    private static SyntaxTriviaList CommentsAndAfter(SyntaxTriviaList trivia) =>
        SyntaxFactory.TriviaList(trivias: trivia.SkipWhile(predicate: IsLayout));

    private static SyntaxTriviaList BeforeLineBreak(SyntaxTriviaList trivia) =>
        SyntaxFactory.TriviaList(
            trivias: trivia.TakeWhile(predicate: static item => !item.IsKind(kind: SyntaxKind.EndOfLineTrivia)));

    private static SyntaxTriviaList FromLineBreak(SyntaxTriviaList trivia) =>
        SyntaxFactory.TriviaList(
            trivias: trivia.SkipWhile(predicate: static item => !item.IsKind(kind: SyntaxKind.EndOfLineTrivia)));

    // The comment on the line of a comma, after the comma, without the whitespace after it.
    private static SyntaxTriviaList CommentAfterComma(SyntaxTriviaList trivia)
    {
        SyntaxTrivia[] beforeLineBreak = [.. BeforeLineBreak(trivia: trivia)];

        return beforeLineBreak.All(predicate: IsLayout)
            ? SyntaxFactory.TriviaList()
            : SyntaxFactory.TriviaList(
                trivias: beforeLineBreak.Reverse().SkipWhile(predicate: IsLayout).Reverse());
    }

    // The whitespace after a comma when the comma has no comment. With a comment, the line break, or one space
    // when the line does not end there.
    private static SyntaxTriviaList LayoutAfterComma(SyntaxTriviaList trivia)
    {
        if (CommentAfterComma(trivia: trivia).Count == 0)
        {
            return trivia;
        }

        SyntaxTriviaList lineBreak = FromLineBreak(trivia: trivia);

        return lineBreak.Count > 0 ? lineBreak : SyntaxFactory.TriviaList(trivia: SyntaxFactory.Space);
    }

    // True when a line break comes after the last comment to the end of a line.
    private static bool EndsEachLineComment(SyntaxTriviaList trivia)
    {
        int lastComment = -1;
        int lastLineBreak = -1;

        for (int index = 0; index < trivia.Count; index++)
        {
            if (trivia[index: index].IsKind(kind: SyntaxKind.SingleLineCommentTrivia))
            {
                lastComment = index;
            }
            else if (trivia[index: index].IsKind(kind: SyntaxKind.EndOfLineTrivia))
            {
                lastLineBreak = index;
            }
        }

        return lastComment < lastLineBreak || lastComment < 0;
    }

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
