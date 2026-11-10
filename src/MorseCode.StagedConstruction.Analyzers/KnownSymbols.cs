using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace MorseCode.StagedConstruction.Analyzers;

/// <summary>
///     The types of MorseCode.StagedConstruction in one compilation, and the
///     members of other libraries that the analyzer knows by name.
/// </summary>
/// <remarks>
///     The analyzer finds each type by its metadata name. Thus, it has no
///     reference to the library, and it reports nothing in a project that does not
///     reference the library.
/// </remarks>
internal sealed class KnownSymbols
{
    // A member that calls its function before it returns, as a type name and a member name. The
    // InstantHandle attribute says the same thing, but SodaFlow does not compile the attribute into
    // its assemblies, thus the analyzer cannot read it there.
    private static readonly ImmutableArray<(string Type, string Member)> InstantMembers =
        ImmutableArray.Create(
            ("SodaFlow.Transaction", "Run"),
            ("SodaFlow.Transaction", "RunVoid"),
            ("SodaFlow.ForwardReference`1", "WithCaptures"),
            ("SodaFlow.ForwardReference`1", "WithoutCaptures"),
            ("SodaFlow.CellLooper`1", "WithCaptures"),
            ("SodaFlow.CellLooper`1", "WithoutCaptures"),
            ("SodaFlow.StreamLooper`1", "WithCaptures"),
            ("SodaFlow.StreamLooper`1", "WithoutCaptures"),
            ("SodaFlow.BehaviorLooper`1", "WithCaptures"),
            ("SodaFlow.BehaviorLooper`1", "WithoutCaptures"));

    private readonly INamedTypeSymbol continuation;
    private readonly INamedTypeSymbol constructor;
    private readonly INamedTypeSymbol stage;
    private readonly INamedTypeSymbol constructed;
    private readonly INamedTypeSymbol constructorFactory;
    private readonly INamedTypeSymbol stageFactory;
    private readonly ImmutableArray<(INamedTypeSymbol Type, string Member)> instantMembers;

    private KnownSymbols(
        INamedTypeSymbol continuation,
        INamedTypeSymbol constructor,
        INamedTypeSymbol stage,
        INamedTypeSymbol constructed,
        INamedTypeSymbol constructorFactory,
        INamedTypeSymbol stageFactory,
        ImmutableArray<(INamedTypeSymbol Type, string Member)> instantMembers)
    {
        this.continuation = continuation;
        this.constructor = constructor;
        this.stage = stage;
        this.constructed = constructed;
        this.constructorFactory = constructorFactory;
        this.stageFactory = stageFactory;
        this.instantMembers = instantMembers;
    }

    /// <summary>
    ///     Finds the types in <paramref name="compilation" />.
    /// </summary>
    /// <param name="compilation">The compilation.</param>
    /// <returns>
    ///     The types, or null if the compilation does not reference
    ///     MorseCode.StagedConstruction.
    /// </returns>
    public static KnownSymbols? Find(Compilation compilation)
    {
        const string library = "MorseCode.StagedConstruction.";

        INamedTypeSymbol? continuation =
            compilation.GetTypeByMetadataName(fullyQualifiedMetadataName: library + "StageContinuation`3");

        INamedTypeSymbol? constructor =
            compilation.GetTypeByMetadataName(fullyQualifiedMetadataName: library + "Constructor`1");

        INamedTypeSymbol? stage = compilation.GetTypeByMetadataName(fullyQualifiedMetadataName: library + "Stage`3");

        INamedTypeSymbol? constructed =
            compilation.GetTypeByMetadataName(fullyQualifiedMetadataName: library + "Constructed`1");

        INamedTypeSymbol? constructorFactory =
            compilation.GetTypeByMetadataName(fullyQualifiedMetadataName: library + "Constructor");

        INamedTypeSymbol? stageFactory =
            compilation.GetTypeByMetadataName(fullyQualifiedMetadataName: library + "Stage");

        if (continuation is null
            || constructor is null
            || stage is null
            || constructed is null
            || constructorFactory is null
            || stageFactory is null)
        {
            return null;
        }

        // A project that does not reference SodaFlow has none of its types, and then no call can match.
        ImmutableArray<(INamedTypeSymbol Type, string Member)> instantMembers =
            (from name in InstantMembers
                from type in Found(type: compilation.GetTypeByMetadataName(fullyQualifiedMetadataName: name.Type))
                select (Type: type, name.Member))
            .ToImmutableArray();

        return new KnownSymbols(
            continuation: continuation,
            constructor: constructor,
            stage: stage,
            constructed: constructed,
            constructorFactory: constructorFactory,
            stageFactory: stageFactory,
            instantMembers: instantMembers);
    }

    /// <summary>
    ///     Gives true if <paramref name="type" /> is <c>StageContinuation</c>.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>True for the delegate of a continuation.</returns>
    public bool IsContinuation(ITypeSymbol? type) => Same(first: type?.OriginalDefinition, second: this.continuation);

    /// <summary>
    ///     Gives true if <paramref name="type" /> is a <c>Constructor</c> or a
    ///     <c>Stage</c>, or derives from one.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>True for a handle.</returns>
    public bool IsHandle(ITypeSymbol? type)
    {
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (Same(first: current.OriginalDefinition, second: this.constructor)
                || Same(first: current.OriginalDefinition, second: this.stage))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Gives true if <paramref name="type" /> is <c>Constructed</c>.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>True for the result of a continuation.</returns>
    public bool IsConstructed(ITypeSymbol? type) => Same(first: type?.OriginalDefinition, second: this.constructed);

    /// <summary>
    ///     Gives true if <paramref name="method" /> is <c>Construct</c> of a handle.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <returns>True for <c>Construct</c>.</returns>
    public bool IsConstruct(IMethodSymbol method) =>
        IsMember(method: method, type: this.constructor, name: "Construct");

    /// <summary>
    ///     Gives true if <paramref name="method" /> is <c>Advance</c> of a stage.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <returns>True for <c>Advance</c>.</returns>
    public bool IsAdvance(IMethodSymbol method) => IsMember(method: method, type: this.stage, name: "Advance");

    /// <summary>
    ///     Gives true if <paramref name="method" /> is <c>Select</c> of a handle.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <returns>True for <c>Select</c>.</returns>
    public bool IsSelect(IMethodSymbol method) => IsMember(method: method, type: this.constructor, name: "Select");

    /// <summary>
    ///     Gives true if <paramref name="method" /> is <c>Constructor.From</c>.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <returns>True for <c>Constructor.From</c>.</returns>
    public bool IsConstructorFrom(IMethodSymbol method) =>
        IsMember(method: method, type: this.constructorFactory, name: "From");

    /// <summary>
    ///     Gives true if <paramref name="method" /> is <c>Stage.From</c>.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <returns>True for <c>Stage.From</c>.</returns>
    public bool IsStageFrom(IMethodSymbol method) => IsMember(method: method, type: this.stageFactory, name: "From");

    /// <summary>
    ///     Gives true if <paramref name="property" /> is <c>Value</c> of
    ///     <c>Constructed</c>.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <returns>True for <c>Constructed.Value</c>.</returns>
    public bool IsConstructedValue(IPropertySymbol property) =>
        property.Name == "Value" && Same(first: property.ContainingType?.OriginalDefinition, second: this.constructed);

    /// <summary>
    ///     Gives true if <paramref name="method" /> is on the list of members that
    ///     call their function before they return.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <returns>True for a member on the list.</returns>
    public bool IsInstantMember(IMethodSymbol method) =>
        this.instantMembers.Any(predicate: member => IsMember(method: method, type: member.Type, name: member.Member));

    private static bool IsMember(ISymbol method, ISymbol type, string name) =>
        method.Name == name && Same(first: method.ContainingType?.OriginalDefinition, second: type);

    // The System.Collections.Immutable of Roslyn 4.4 has no builder for a collection expression.
    private static ImmutableArray<INamedTypeSymbol> Found(INamedTypeSymbol? type) =>
        type is null ? ImmutableArray<INamedTypeSymbol>.Empty : ImmutableArray.Create(item: type);

    private static bool Same(ISymbol? first, ISymbol second) =>
        SymbolEqualityComparer.Default.Equals(x: first, y: second);
}
