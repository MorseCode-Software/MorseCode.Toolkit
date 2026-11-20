using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace MorseCode.Analyzers;

/// <summary>
///     Reports an explicit cast, such as <c>(int)value</c>.
/// </summary>
/// <remarks>
///     <para>
///         An explicit cast tells the compiler to trust the writer. The compiler does
///         not check it, and a wrong cast fails at run time, or loses data without a
///         sign. MorseCode Software keeps the type of a value known where the code
///         can. Thus, a cast is a smell, and the few cases that need one carry a
///         suppression with a reason.
///     </para>
///     <para>
///         The analyzer reports each cast expression, whatever the conversion is. The
///         report marks the type in parentheses, such as <c>(int)</c>, and not the
///         value after it. Thus, a cast on a long expression does not mark the whole
///         expression. It does not report <c>as</c>, <c>is</c>, a pattern, or an
///         implicit conversion that the compiler makes without a cast. The message names
///         the target type and says which way out suits the kind of conversion.
///     </para>
///     <para>
///         A <c>foreach</c> with a type converts each element to that type, with no
///         cast in the source. A range variable of a query with a type does the same.
///         The analyzer reports them too, when the conversion is explicit, and marks
///         the type.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
// ReSharper disable once InheritdocConsiderUsage - The summary of DiagnosticAnalyzer does not say which rule this analyzer reports.
public sealed class ExplicitCastAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     The rule that this analyzer reports. The release tracking of the Roslyn
    ///     rules (RS2002) finds a rule only in a field, made by a new that names its
    ///     type.
    /// </summary>
    private static readonly DiagnosticDescriptor Rule =
        // ReSharper disable once ArrangeObjectCreationWhenTypeEvident - The release tracking of the Roslyn 4.4 rules (RS2002) finds a rule only where the new names its type.
        new DiagnosticDescriptor(
            id: "MC0002",
            title: "Avoid an explicit cast",
            messageFormat: "Avoid the explicit cast to {0}: {1}",
            category: "Style",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "An explicit cast tells the compiler to trust the writer. Keep the type of a value known, "
                         + "or suppress the rule at the few places that need a cast, and give the reason.",
            helpLinkUri: "https://morsecode-software.github.io/MorseCode.Toolkit/docs/explicit-cast.html");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(item: Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(analysisMode: GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(
            action: static nodeContext =>
            {
                CastExpressionSyntax cast = (CastExpressionSyntax)nodeContext.Node;
                SemanticModel model = nodeContext.SemanticModel;

                ITypeSymbol? target = model.GetTypeInfo(expression: cast.Type).Type;
                ITypeSymbol? source = model.GetTypeInfo(expression: cast.Expression).Type;

                if (target is null or IErrorTypeSymbol)
                {
                    return;
                }

                nodeContext.ReportDiagnostic(
                    diagnostic: Diagnostic.Create(
                        descriptor: Rule,
                        location:
                        Location.Create(
                            syntaxTree: cast.SyntaxTree,
                            textSpan: TextSpan.FromBounds(
                                start: cast.OpenParenToken.SpanStart,
                                end: cast.CloseParenToken.Span.End)),
                        messageArgs:
                        [
                            target.ToDisplayString(format: SymbolDisplayFormat.CSharpShortErrorMessageFormat),
                            Hint(
                                conversion: model.ClassifyConversion(expression: cast.Expression, destination: target),
                                source: source,
                                target: target,
                                cast: cast,
                                model: model,
                                cancellationToken: nodeContext.CancellationToken)
                        ]));
            },
            syntaxKinds: SyntaxKind.CastExpression);

        context.RegisterSyntaxNodeAction(action: CheckForEach, syntaxKinds: SyntaxKind.ForEachStatement);

        context.RegisterSyntaxNodeAction(
            action: static nodeContext =>
            {
                FromClauseSyntax from = (FromClauseSyntax)nodeContext.Node;
                CheckRangeVariable(context: nodeContext, type: from.Type, sequence: from.Expression);
            },
            syntaxKinds: SyntaxKind.FromClause);

        context.RegisterSyntaxNodeAction(
            action: static nodeContext =>
            {
                JoinClauseSyntax join = (JoinClauseSyntax)nodeContext.Node;
                CheckRangeVariable(context: nodeContext, type: join.Type, sequence: join.InExpression);
            },
            syntaxKinds: SyntaxKind.JoinClause);
    }

    // A foreach with a type converts each element to that type. The compiler says which conversion it makes.
    private static void CheckForEach(SyntaxNodeAnalysisContext context)
    {
        ForEachStatementSyntax loop = (ForEachStatementSyntax)context.Node;
        ForEachStatementInfo info = context.SemanticModel.GetForEachStatementInfo(forEachStatement: loop);

        if (!info.ElementConversion.IsExplicit || info.ElementType is not { } element)
        {
            return;
        }

        ReportEachElement(
            context: context,
            type: loop.Type,
            conversion: info.ElementConversion,
            element: element,
            construct: "the foreach casts each element");
    }

    // A range variable with a type makes the query call Cast. The compiler calls it for each type, thus the
    // analyzer classifies the conversion from the type of the elements itself.
    private static void CheckRangeVariable(
        SyntaxNodeAnalysisContext context,
        TypeSyntax? type,
        ExpressionSyntax sequence)
    {
        if (type is null
            || context.SemanticModel.GetTypeInfo(expression: type, cancellationToken: context.CancellationToken).Type
                is not { } target
            || ElementType(
                    sequence: context.SemanticModel.GetTypeInfo(
                            expression: sequence,
                            cancellationToken: context.CancellationToken)
                        .Type,
                    compilation: context.SemanticModel.Compilation) is not { } element)
        {
            return;
        }

        Conversion conversion =
            context.SemanticModel.Compilation.ClassifyConversion(source: element, destination: target);

        if (conversion.IsExplicit)
        {
            ReportEachElement(
                context: context,
                type: type,
                conversion: conversion,
                element: element,
                construct: "the query casts each element with Cast");
        }
    }

    // The type of each element of a sequence: T for an IEnumerable<T>, and object for an IEnumerable that is not
    // generic. Null when the type has more than one IEnumerable<T>, because then the element is not known.
    private static ITypeSymbol? ElementType(ITypeSymbol? sequence, Compilation compilation)
    {
        if (sequence is null or IErrorTypeSymbol)
        {
            return null;
        }

        INamedTypeSymbol generic =
            compilation.GetSpecialType(specialType: SpecialType.System_Collections_Generic_IEnumerable_T);

        INamedTypeSymbol[] enumerables =
        [
            .. sequence.AllInterfaces.Prepend(element: sequence as INamedTypeSymbol)
                .OfType<INamedTypeSymbol>()
                .Where(
                    predicate: candidate =>
                        SymbolEqualityComparer.Default.Equals(x: candidate.OriginalDefinition, y: generic))
                .Distinct(comparer: SymbolEqualityComparer.Default)
                .OfType<INamedTypeSymbol>()
        ];

        return enumerables.Length switch
        {
            0 => compilation.GetSpecialType(specialType: SpecialType.System_Object),
            1 => enumerables[0].TypeArguments[0],
            _ => null
        };
    }

    private static void ReportEachElement(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax type,
        Conversion conversion,
        ITypeSymbol element,
        string construct)
    {
        if (context.SemanticModel.GetTypeInfo(expression: type, cancellationToken: context.CancellationToken).Type is
            not { } target
            || target is IErrorTypeSymbol)
        {
            return;
        }

        context.ReportDiagnostic(
            diagnostic: Diagnostic.Create(
                descriptor: Rule,
                location: type.GetLocation(),
                messageArgs:
                [
                    target.ToDisplayString(format: SymbolDisplayFormat.CSharpShortErrorMessageFormat),
                    $"{construct}; {EachElement(conversion: conversion, element: element, target: target)}"
                ]));
    }

    // The way out for a conversion that a foreach or a query makes for each element.
    private static string EachElement(Conversion conversion, ITypeSymbol element, ITypeSymbol target)
    {
        if (conversion.IsUnboxing || conversion.IsReference || (!element.IsValueType && !target.IsValueType))
        {
            return "use a sequence of that type, or filter it with OfType";
        }

        if (conversion.IsNumeric || conversion.IsEnumeration || conversion.IsNullable)
        {
            return "keep the elements in a type of the right size, or convert them in one place with a name";
        }

        return conversion.IsUserDefined
            ? "call a method that converts each element, with a name that says what it does"
            : "change the code so that the conversion is not necessary";
    }

    // The way out that suits the conversion. The order matters. A cast from dynamic runs an explicit conversion
    // when the code runs. The code without the cast runs an implicit one, thus no other advice is correct for it.
    // An implicit conversion makes a cast redundant only where the value goes to a destination of its type. A
    // reference conversion can be either direction.
    private static string Hint(
        Conversion conversion,
        ITypeSymbol? source,
        ITypeSymbol target,
        SyntaxNode cast,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (source is { TypeKind: TypeKind.Dynamic })
        {
            return "change the code so that the conversion is not necessary";
        }

        if (conversion.IsIdentity || conversion.IsImplicit)
        {
            return GoesToItsType(
                cast: cast,
                conversion: conversion,
                target: target,
                model: model,
                cancellationToken: cancellationToken)
                ? "an implicit conversion exists, thus the cast is not needed"
                : "an implicit conversion exists: put the value in a variable of that type in place of the cast";
        }

        if (conversion.IsUnboxing
            || conversion.IsReference
            || (source is not null && !source.IsValueType && !target.IsValueType))
        {
            return "match the type with a pattern, or keep the value in a variable of the type that you need";
        }

        if (conversion.IsNumeric || conversion.IsEnumeration || conversion.IsNullable)
        {
            return "keep the value in a type of the right size, or convert it in one place with a name";
        }

        return conversion.IsUserDefined
            ? "call a method that converts the value, with a name that says what it does"
            : "change the code so that the conversion is not necessary";
    }

    // True when the value of the cast goes directly to a destination of the same type as the cast. Then the code
    // without the cast makes the same implicit conversion to the same type. One destination is the initializer
    // of a variable or a property with a declared type. The others are a simple assignment, and the return of a
    // method or a local function. A variable with var gets its type from the cast, and so can a lambda and an
    // async method. A discard takes each type, thus there the cast changes nothing, unless it runs a conversion
    // that the user wrote.
    private static bool GoesToItsType(
        SyntaxNode cast,
        Conversion conversion,
        ITypeSymbol target,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        SyntaxNode value = cast;

        while (value.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            value = parenthesized;
        }

        ITypeSymbol? destination =
            value.Parent switch
            {
                EqualsValueClauseSyntax
                    {
                        Parent: VariableDeclaratorSyntax
                        {
                            Parent: VariableDeclarationSyntax { Type: { IsVar: false } type }
                        }
                    } =>
                    model.GetTypeInfo(expression: type, cancellationToken: cancellationToken).Type,
                EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property } =>
                    model.GetTypeInfo(expression: property.Type, cancellationToken: cancellationToken).Type,
                AssignmentExpressionSyntax assignment
                    when assignment.IsKind(kind: SyntaxKind.SimpleAssignmentExpression)
                         && assignment.Right == value =>
                    model.GetSymbolInfo(expression: assignment.Left, cancellationToken: cancellationToken).Symbol
                        is IDiscardSymbol
                        ? conversion.IsUserDefined ? null : target
                        : model.GetTypeInfo(expression: assignment.Left, cancellationToken: cancellationToken).Type,
                ReturnStatementSyntax or ArrowExpressionClauseSyntax =>
                    model.GetEnclosingSymbol(position: value.SpanStart, cancellationToken: cancellationToken) is
                        IMethodSymbol
                        {
                            MethodKind: not MethodKind.AnonymousFunction,
                            IsAsync: false,
                            ReturnsByRef: false,
                            ReturnsByRefReadonly: false
                        } method
                        ? method.ReturnType
                        : null,
                _ => null
            };

        return destination is not null && SymbolEqualityComparer.Default.Equals(x: destination, y: target);
    }
}
