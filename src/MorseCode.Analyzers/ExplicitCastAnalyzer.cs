using System.Collections.Immutable;
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
///         expression. It does not report <c>as</c>, <c>is</c>, a pattern, or a conversion that the
///         compiler makes without a cast. The message names the target type and says
///         which way out suits the kind of conversion.
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
