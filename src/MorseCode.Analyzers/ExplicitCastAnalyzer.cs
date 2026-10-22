using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

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
///         The analyzer reports each cast expression, whatever the conversion is. It
///         does not report <c>as</c>, <c>is</c>, a pattern, or a conversion that the
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
                        location: cast.GetLocation(),
                        messageArgs:
                        [
                            target.ToDisplayString(format: SymbolDisplayFormat.CSharpShortErrorMessageFormat),
                            Hint(
                                conversion: model.ClassifyConversion(expression: cast.Expression, destination: target),
                                source: source,
                                target: target)
                        ]));
            },
            syntaxKinds: SyntaxKind.CastExpression);
    }

    // The way out that suits the conversion. The order matters: a conversion that is implicit needs no cast at
    // all, and a reference conversion can be either direction.
    private static string Hint(Conversion conversion, ITypeSymbol? source, ITypeSymbol target)
    {
        if (conversion.IsIdentity || conversion.IsImplicit)
        {
            return "an implicit conversion exists, thus the cast is not needed";
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
}
