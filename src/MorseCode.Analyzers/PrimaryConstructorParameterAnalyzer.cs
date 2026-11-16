using System.Collections.Generic;
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
///         <c>in</c> either. It does not report a parameter of an attribute, because
///         C# refuses a use of an attribute with an <c>in</c> parameter. It does
///         not report a by-value <c>scoped</c> parameter, because no spelling with
///         <c>in</c> keeps its value scoped.
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
            messageFormat: "The primary constructor parameter {0} of {1} is not in: write {2}, or copy the value to a field",
            category: "Style",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Write each primary constructor parameter of a class or a struct as in, so that it is "
                         + "readonly and no member can capture it. A member that needs the value reads it from a field. "
                         + "On a ref struct, write scoped in, so that what the constructor makes does not hold the "
                         + "reference and a caller can return it.",
            helpLinkUri: "https://morsecode-software.github.io/MorseCode.Toolkit/docs/primary-constructor-parameter.html");

    /// <summary>
    ///     The name of the property of a report that holds the modifiers to write.
    ///     MorseCode.Analyzers.CodeFixes reads it by this name.
    /// </summary>
    private const string InProperty = "In";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(item: Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(analysisMode: GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(
            action: static start =>
            {
                INamedTypeSymbol? attribute =
                    start.Compilation.GetTypeByMetadataName(fullyQualifiedMetadataName: "System.Attribute");

                // A record has its own syntax kinds, thus this registration does not see one.
                start.RegisterSyntaxNodeAction(
                    action: syntaxContext =>
                        Check(
                            context: syntaxContext,
                            type: (BaseTypeDeclarationSyntax)syntaxContext.Node,
                            attribute: attribute),
                    syntaxKinds: ImmutableArray.Create(
                        item1: SyntaxKind.ClassDeclaration,
                        item2: SyntaxKind.StructDeclaration));
            });
    }

    private static void Check(
        SyntaxNodeAnalysisContext context,
        BaseTypeDeclarationSyntax type,
        INamedTypeSymbol? attribute)
    {
        // Roslyn 4.4, the oldest compiler that runs the package, has no ParameterList on a class or a
        // struct. Their primary constructors came with C# 12. A newer compiler still puts the list in
        // the tree as a child node, and the type of that node exists in 4.4.
        ParameterListSyntax? parameters = type.ChildNodes().OfType<ParameterListSyntax>().FirstOrDefault();

        if (parameters is null)
        {
            return;
        }

        ParameterSyntax[] notIn = [.. parameters.Parameters.Where(predicate: IsNotIn)];

        // The analyzer reads the semantic model only for a type that has a parameter to report.
        if (notIn.Length == 0 || IsAttribute(context: context, type: type, attribute: attribute))
        {
            return;
        }

        // An in parameter of the constructor of a ref struct lets what the constructor makes hold the reference.
        // Then a caller cannot return it when the argument does not live after the call. Such an argument is a
        // parameter of the caller, a constant, or an element of an array. Scoped in keeps the reference in the
        // constructor. The code fix reads the modifiers from the report, thus it writes what the message says.
        string modifiers = IsRefStruct(type: type) ? "scoped in" : "in";

        ImmutableDictionary<string, string?> properties =
            ImmutableDictionary.CreateRange(items: [new KeyValuePair<string, string?>(key: InProperty, value: modifiers)]);

        foreach (ParameterSyntax parameter in notIn)
        {
            context.ReportDiagnostic(
                diagnostic: Diagnostic.Create(
                    descriptor: Rule,
                    location: parameter.Identifier.GetLocation(),
                    properties: properties,
                    messageArgs: [parameter.Identifier.Text, type.Identifier.Text, modifiers]));
        }
    }

    private static bool IsRefStruct(MemberDeclarationSyntax type) =>
        type is StructDeclarationSyntax && type.Modifiers.Any(kind: SyntaxKind.RefKeyword);

    // C# refuses a use of an attribute whose constructor has an in parameter (CS8358). Thus, a parameter of an
    // attribute cannot be in, and the rule does not report it. The code fix then breaks each use. A use is frequently
    // in a different project, and the build of this project does not show its errors. The check reads the chain of base
    // types, thus it finds an attribute that derives through a different attribute. No code can apply an abstract
    // attribute, but the rule leaves it out too, so that one rule covers each attribute.
    private static bool IsAttribute(
        SyntaxNodeAnalysisContext context,
        BaseTypeDeclarationSyntax type,
        INamedTypeSymbol? attribute)
    {
        if (attribute is null
            || context.SemanticModel.GetDeclaredSymbol(
                declarationSyntax: type,
                cancellationToken: context.CancellationToken) is not { } symbol)
        {
            return false;
        }

        for (INamedTypeSymbol? current = symbol.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(x: current, y: attribute))
            {
                return true;
            }
        }

        return false;
    }

    // The rule does not report a params parameter, because it cannot be in. It does not report a by-value scoped
    // parameter either. Scoped in limits the reference and not the value, thus no spelling with in keeps the value
    // scoped. The compiler refuses a member that uses a ref-like parameter (CS9110), and a field that a scoped value
    // initializes (CS8352). Thus, the parameter cannot become hidden state.
    private static bool IsNotIn(ParameterSyntax parameter) =>
        !parameter.Modifiers.Any(kind: SyntaxKind.InKeyword)
        && !parameter.Modifiers.Any(kind: SyntaxKind.ParamsKeyword)
        && !IsScopedByValue(parameter: parameter);

    private static bool IsScopedByValue(BaseParameterSyntax parameter) =>
        parameter.Modifiers.Any(kind: SyntaxKind.ScopedKeyword)
        && !parameter.Modifiers.Any(kind: SyntaxKind.RefKeyword)
        && !parameter.Modifiers.Any(kind: SyntaxKind.OutKeyword);
}
