using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MorseCode.Analyzers;

/// <summary>
///     Reports a primary constructor parameter of a class or a struct that is not
///     <c>in</c>.
/// </summary>
/// <remarks>
///     <para>
///         A primary constructor parameter without a modifier is a mutable variable
///         that each member of the type can read and write. The <c>in</c> modifier
///         makes it readonly, and the compiler refuses a member that captures it
///         (CS9109). Thus, the value can go only into an initializer or a base call,
///         and it cannot become hidden mutable state. A member that needs the value
///         reads it from a field.
///     </para>
///     <para>
///         The analyzer examines classes and structs. It does not examine records,
///         because a positional parameter of a record makes a property and cannot be
///         <c>in</c>. It does not report a <c>params</c> parameter, which cannot be
///         <c>in</c> either.
///     </para>
///     <para>
///         The analyzer uses nullable types and not Maybe, because it runs in the
///         compiler, which loads only Roslyn and the analyzer.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
// ReSharper disable once InheritdocConsiderUsage - The summary of DiagnosticAnalyzer does not say which rule this analyzer reports.
public sealed class PrimaryConstructorParameterAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///     The rule that this analyzer reports. The release tracking of the Roslyn
    ///     rules (RS2002) finds a rule only in a field, made by a new that names its
    ///     type.
    /// </summary>
    private static readonly DiagnosticDescriptor Rule =
        // ReSharper disable once ArrangeObjectCreationWhenTypeEvident - The release tracking of the Roslyn 4.4 rules (RS2002) finds a rule only where the new names its type.
        new DiagnosticDescriptor(
            id: "MC0003",
            title: "A primary constructor parameter is not in",
            messageFormat: "The primary constructor parameter {0} of {1} is not in: write in, or copy the value to a field",
            category: "Style",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Write each primary constructor parameter of a class or a struct as in, so that it is "
                         + "readonly and no member can capture it. A member that needs the value reads it from a field.",
            helpLinkUri: "https://morsecode-software.github.io/MorseCode.Toolkit/docs/primary-constructor-parameter.html");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(item: Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(analysisMode: GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // A record has its own syntax kinds, thus this registration does not see one.
        context.RegisterSyntaxNodeAction(
            action: static syntaxContext =>
                Check(context: syntaxContext, type: (BaseTypeDeclarationSyntax)syntaxContext.Node),
            syntaxKinds: ImmutableArray.Create(item1: SyntaxKind.ClassDeclaration, item2: SyntaxKind.StructDeclaration));
    }

    private static void Check(SyntaxNodeAnalysisContext context, BaseTypeDeclarationSyntax type)
    {
        // Roslyn 4.4, the oldest compiler that runs the package, has no ParameterList on a class or a
        // struct. Their primary constructors came with C# 12. A newer compiler still puts the list in
        // the tree as a child node, and the type of that node exists in 4.4.
        ParameterListSyntax? parameters = type.ChildNodes().OfType<ParameterListSyntax>().FirstOrDefault();

        if (parameters is null)
        {
            return;
        }

        foreach (ParameterSyntax parameter in parameters.Parameters.Where(predicate: IsNotIn))
        {
            context.ReportDiagnostic(
                diagnostic: Diagnostic.Create(
                    descriptor: Rule,
                    location: parameter.Identifier.GetLocation(),
                    messageArgs: [parameter.Identifier.Text, type.Identifier.Text]));
        }
    }

    // A params parameter cannot be in, thus it is not reported.
    private static bool IsNotIn(ParameterSyntax parameter) =>
        !parameter.Modifiers.Any(kind: SyntaxKind.InKeyword) && !parameter.Modifiers.Any(kind: SyntaxKind.ParamsKeyword);
}
