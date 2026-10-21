using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace MorseCode.Analyzers;

/// <summary>
///     Reports a call whose arguments are not in the order of the parameters of
///     the method, when one or more arguments have a name.
/// </summary>
/// <remarks>
///     <para>
///         C# accepts named arguments in any order. MorseCode Software writes them in
///         the order of the definition, thus a reader can compare a call with the
///         method from top to bottom.
///     </para>
///     <para>
///         The analyzer examines calls of methods, local functions, and delegates. It
///         also examines the calls of constructors by new, by base, and by the base
///         type of a primary constructor. It reads the argument list of the source.
///         Thus, an optional parameter that a call leaves out, and a call that the
///         compiler makes, such as for an interpolated string, give no report.
///     </para>
///     <para>
///         The analyzer uses nullable types and not Maybe, because it runs in the
///         compiler, which loads only Roslyn and the analyzer.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
// ReSharper disable once InheritdocConsiderUsage - The summary of DiagnosticAnalyzer does not say which rule this analyzer reports.
public sealed class NamedArgumentOrderAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     The rule that this analyzer reports. The release tracking of the Roslyn
    ///     rules (RS2002) finds a rule only in a field, made by a new that names its
    ///     type.
    /// </summary>
    private static readonly DiagnosticDescriptor Rule =
        // ReSharper disable once ArrangeObjectCreationWhenTypeEvident - The release tracking of the Roslyn 4.4 rules (RS2002) finds a rule only where the new names its type.
        new DiagnosticDescriptor(
            id: "MC0001",
            title: "Named arguments are not in the order of the parameters",
            messageFormat: "The arguments of {0} are not in the order of its parameters: {1}",
            category: "Style",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Write the arguments of a call in the order of the parameters of its method, also when "
                         + "the arguments have names.",
            helpLinkUri: "https://morsecode-software.github.io/MorseCode.Toolkit/docs/named-argument-order.html");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(item: Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(analysisMode: GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterOperationAction(
            action: static operationContext =>
            {
                IInvocationOperation invocation = (IInvocationOperation)operationContext.Operation;
                Check(context: operationContext, method: invocation.TargetMethod, arguments: invocation.Arguments);
            },
            operationKinds: ImmutableArray.Create(item: OperationKind.Invocation));

        context.RegisterOperationAction(
            action: static operationContext =>
            {
                IObjectCreationOperation creation = (IObjectCreationOperation)operationContext.Operation;

                if (creation.Constructor is { } constructor)
                {
                    Check(context: operationContext, method: constructor, arguments: creation.Arguments);
                }
            },
            operationKinds: ImmutableArray.Create(item: OperationKind.ObjectCreation));
    }

    private static void Check(
        OperationAnalysisContext context,
        IMethodSymbol method,
        ImmutableArray<IArgumentOperation> arguments)
    {
        // The arguments as the source has them. A call that the compiler makes, such as for an interpolated
        // string, has the syntax of a different node, which has no argument list.
        ArgumentListSyntax? list =
            context.Operation.Syntax switch
            {
                InvocationExpressionSyntax invocation => invocation.ArgumentList,
                BaseObjectCreationExpressionSyntax creation => creation.ArgumentList,
                ConstructorInitializerSyntax initializer => initializer.ArgumentList,
                PrimaryConstructorBaseTypeSyntax baseType => baseType.ArgumentList,
                _ => null
            };

        if (list is null || !list.Arguments.Any(predicate: static argument => argument.NameColon is not null))
        {
            return;
        }

        int[] ordinals = [.. list.Arguments.Select(selector: Ordinal)];

        bool inOrder =
            ordinals.Zip(second: ordinals.Skip(count: 1), resultSelector: static (first, next) => first <= next)
                .All(predicate: static ordered => ordered);

        if (!inOrder)
        {
            context.ReportDiagnostic(
                diagnostic: Diagnostic.Create(
                    descriptor: Rule,
                    location: context.Operation.Syntax.GetLocation(),
                    messageArgs:
                    [
                        method.ToDisplayString(format: SymbolDisplayFormat.CSharpShortErrorMessageFormat),
                        string.Join(
                            separator: ", ",
                            values: method.Parameters.Select(selector: static parameter => parameter.Name))
                    ]));
        }

        return;

        // The position of the parameter that the compiler binds an argument to. A value that goes into a params
        // array has no operation of its own, thus it takes the last parameter, which is the params parameter.
        int Ordinal(ArgumentSyntax argument) =>
            arguments.FirstOrDefault(predicate: operation => operation.Syntax == argument)?.Parameter?.Ordinal
            ?? method.Parameters.Length - 1;
    }
}
