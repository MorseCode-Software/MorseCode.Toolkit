using Microsoft.CodeAnalysis;

namespace MorseCode.StagedConstruction.Analyzers;

/// <summary>
///     The rules that <see cref="ContinuationAnalyzer" /> reports.
/// </summary>
/// <remarks>
///     The release tracking of the Roslyn rules (RS2002) finds a rule only in a
///     field, made by a new that names its type.
/// </remarks>
internal static class ContinuationRules
{
    private const string Category = "StagedConstruction";

    private const string HelpLink =
        "https://morsecode-software.github.io/MorseCode.Toolkit/docs/continuation-rules.html";

    /// <summary>
    ///     <c>Value</c> is read directly from a call to <c>Construct</c> or
    ///     <c>Advance</c> on the handle.
    /// </summary>
    public static readonly DiagnosticDescriptor ValueReadFromCall =
        // ReSharper disable once ArrangeObjectCreationWhenTypeEvident - The release tracking of the Roslyn 4.4 rules (RS2002) finds a rule only where the new names its type.
        new DiagnosticDescriptor(
            id: "MC1001",
            title: "Value is read directly from the result of the handle in a continuation",
            messageFormat:
            "Value is read directly from the call to {0}, so the continuation cannot return the result of that call: "
            + "return the result, or keep it in a variable and return that",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description:
            "A continuation must return the Constructed that Construct or Advance gives on its handle. A read of "
            + "Value directly from the call does not keep that Constructed, so the continuation cannot return it.",
            helpLinkUri: HelpLink + "#mc1001");

    /// <summary>
    ///     The result of <c>Construct</c> or <c>Advance</c> on the handle is not used.
    /// </summary>
    public static readonly DiagnosticDescriptor ResultNotUsed =
        // ReSharper disable once ArrangeObjectCreationWhenTypeEvident - The release tracking of the Roslyn 4.4 rules (RS2002) finds a rule only where the new names its type.
        new DiagnosticDescriptor(
            id: "MC1002",
            title: "The result of the handle is not used in a continuation",
            messageFormat: "The result of the call to {0} is not used: return it from the continuation",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description:
            "A continuation must return the Constructed that Construct or Advance gives on its handle. A handle "
            + "can be used one time only, so a result that is not kept cannot be made again.",
            helpLinkUri: HelpLink + "#mc1002");

    /// <summary>
    ///     The continuation returns null.
    /// </summary>
    public static readonly DiagnosticDescriptor NullReturned =
        // ReSharper disable once ArrangeObjectCreationWhenTypeEvident - The release tracking of the Roslyn 4.4 rules (RS2002) finds a rule only where the new names its type.
        new DiagnosticDescriptor(
            id: "MC1003",
            title: "A continuation returns null",
            messageFormat: "The continuation returns null: return the result of its handle {0}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description:
            "A continuation must return the Constructed that its handle gives. A null return has no value for "
            + "the base to give to its caller.",
            helpLinkUri: HelpLink + "#mc1003");

    /// <summary>
    ///     The continuation returns the result of a handle that it made with
    ///     <c>Constructor.From</c>.
    /// </summary>
    public static readonly DiagnosticDescriptor ForeignHandleReturned =
        // ReSharper disable once ArrangeObjectCreationWhenTypeEvident - The release tracking of the Roslyn 4.4 rules (RS2002) finds a rule only where the new names its type.
        new DiagnosticDescriptor(
            id: "MC1004",
            title: "A continuation returns the result of a handle that it made",
            messageFormat:
            "The continuation returns the result of a handle from Constructor.From, and not the result of its handle {0}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description:
            "A continuation must return the Constructed that its own handle gives. The result of a handle that "
            + "the continuation made with Constructor.From does not hold the values of the base.",
            helpLinkUri: HelpLink + "#mc1004");

    /// <summary>
    ///     The function given to <c>Construct</c> does not read its parameter.
    /// </summary>
    public static readonly DiagnosticDescriptor BaseValuesNotUsed =
        // ReSharper disable once ArrangeObjectCreationWhenTypeEvident - The release tracking of the Roslyn 4.4 rules (RS2002) finds a rule only where the new names its type.
        new DiagnosticDescriptor(
            id: "MC1005",
            title: "The function given to Construct does not use the values of the base",
            messageFormat:
            "The function given to Construct does not read its parameter {0}, so its result does not hold the values of the base",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description:
            "Construct gives the values of the base to its function, and the handle is their only source. A "
            + "function that does not read them returns a value that the base did not help to make.",
            helpLinkUri: HelpLink + "#mc1005");

    /// <summary>
    ///     The continuation does not use its handle.
    /// </summary>
    public static readonly DiagnosticDescriptor HandleNotUsed =
        // ReSharper disable once ArrangeObjectCreationWhenTypeEvident - The release tracking of the Roslyn 4.4 rules (RS2002) finds a rule only where the new names its type.
        new DiagnosticDescriptor(
            id: "MC1006",
            title: "A continuation does not use its handle",
            messageFormat:
            "The continuation returns a value and does not use its handle {0}: call Construct or Advance on the handle and return the result",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description:
            "The handle of a continuation is the only source of the Constructed that the continuation must "
            + "return. A continuation that returns a value and does not use the handle returns a different value.",
            helpLinkUri: HelpLink + "#mc1006");

    /// <summary>
    ///     The handle goes to code that the analyzer cannot follow.
    /// </summary>
    public static readonly DiagnosticDescriptor HandleLeaves =
        // ReSharper disable once ArrangeObjectCreationWhenTypeEvident - The release tracking of the Roslyn 4.4 rules (RS2002) finds a rule only where the new names its type.
        new DiagnosticDescriptor(
            id: "MC1007",
            title: "The handle of a continuation goes to code that the analyzer cannot follow",
            messageFormat: "The handle {0} {1}, and the analyzer cannot follow it there",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description:
            "The other rules examine what a continuation does with its handle. They stop where the handle goes to "
            + "a method, to a type that is not a stage or a handle, to a field, or into a lambda that can run "
            + "after the continuation returns.",
            helpLinkUri: HelpLink + "#mc1007");
}
