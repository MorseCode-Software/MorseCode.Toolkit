using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace MorseCode.StagedConstruction.Analyzers;

/// <summary>
///     One continuation: a lambda that becomes a <c>StageContinuation</c> and whose
///     second parameter is a handle.
/// </summary>
internal sealed class Continuation
{
    private readonly ImmutableHashSet<ILocalSymbol> aliases;
    private readonly Func<ControlFlowGraph> graph;

    // Most continuations return the result of Construct directly and read no local variable. Thus, the
    // graph and its analysis occur only at the first read.
    private readonly Lazy<LocalValues> values;

    private Continuation(
        IAnonymousFunctionOperation lambda,
        IParameterSymbol handle,
        KnownSymbols symbols,
        ImmutableArray<IOperation> operations,
        ImmutableHashSet<ILocalSymbol> aliases,
        Func<ControlFlowGraph> graph)
    {
        this.Lambda = lambda;
        this.Handle = handle;
        this.Symbols = symbols;
        this.Operations = operations;
        this.aliases = aliases;
        this.graph = graph;

        this.values = new Lazy<LocalValues>(
            valueFactory: () => new LocalValues(lambda: lambda, operations: operations, rootGraph: graph));
    }

    /// <summary>
    ///     Gets the lambda.
    /// </summary>
    public IAnonymousFunctionOperation Lambda { get; }

    /// <summary>
    ///     Gets the parameter that receives the handle.
    /// </summary>
    public IParameterSymbol Handle { get; }

    /// <summary>
    ///     Gets the types of the library.
    /// </summary>
    public KnownSymbols Symbols { get; }

    /// <summary>
    ///     Gets each operation in the body of the lambda, at each depth.
    /// </summary>
    public ImmutableArray<IOperation> Operations { get; }

    /// <summary>
    ///     Makes a continuation from <paramref name="lambda" />, if the lambda is one.
    /// </summary>
    /// <param name="lambda">The lambda.</param>
    /// <param name="symbols">The types of the library.</param>
    /// <param name="graph">
    ///     Gives the control flow graph of the method body or the initializer that
    ///     holds the lambda.
    /// </param>
    /// <returns>
    ///     The continuation. Null if the lambda does not become a
    ///     <c>StageContinuation</c>, or if its second parameter is not a handle. A
    ///     stage can give a value of a different type to its continuation, and no
    ///     rule applies to that continuation.
    /// </returns>
    public static Continuation? From(
        IAnonymousFunctionOperation lambda,
        KnownSymbols symbols,
        Func<ControlFlowGraph> graph)
    {
        if (lambda.Parent is not IDelegateCreationOperation creation
            || !symbols.IsContinuation(type: creation.Type)
            || lambda.Symbol.Parameters.Length != 2
            || !symbols.IsHandle(type: lambda.Symbol.Parameters[1].Type))
        {
            return null;
        }

        IParameterSymbol handle = lambda.Symbol.Parameters[1];
        ImmutableArray<IOperation> operations = lambda.Body.Descendants().ToImmutableArray();

        return new Continuation(
            lambda: lambda,
            handle: handle,
            symbols: symbols,
            operations: operations,
            aliases: FindAliases(handle: handle, symbols: symbols, operations: operations),
            graph: graph);
    }

    /// <summary>
    ///     Gives true if <paramref name="operation" /> is the handle of this
    ///     continuation: the parameter, a local variable that holds it, or a
    ///     <c>Select</c> of one of these.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns>True for the handle.</returns>
    public bool IsHandle(IOperation? operation) =>
        IsHandle(operation: operation, handle: this.Handle, symbols: this.Symbols, aliases: this.aliases);

    /// <summary>
    ///     Gives true if <paramref name="operation" /> reads the parameter of the
    ///     handle or a local variable that holds it.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns>True for a read of the handle by its name.</returns>
    public bool IsHandleName(IOperation operation) =>
        operation is IParameterReferenceOperation or ILocalReferenceOperation && this.IsHandle(operation: operation);

    /// <summary>
    ///     Gives each value that <paramref name="read" /> can get. See
    ///     <see cref="LocalValues" />.
    /// </summary>
    /// <param name="read">A read of a local variable in the lambda.</param>
    /// <param name="cancellationToken">Stops the search when the compiler cancels the analysis.</param>
    /// <returns>
    ///     The values without conversions. None is a local variable. Empty if the
    ///     analyzer cannot know the value.
    /// </returns>
    public ImmutableArray<IOperation> ValuesOf(ILocalReferenceOperation read, CancellationToken cancellationToken) =>
        this.values.Value.Of(read: read, cancellationToken: cancellationToken);

    /// <summary>
    ///     Gives true if <paramref name="function" /> is a continuation with a handle
    ///     of its own. The analyzer examines that lambda by itself.
    /// </summary>
    /// <param name="function">A lambda in the body of this continuation.</param>
    /// <returns>True for a continuation.</returns>
    public bool IsContinuation(IAnonymousFunctionOperation function) =>
        From(lambda: function, symbols: this.Symbols, graph: this.graph) is not null;

    // A local variable holds the handle when its declaration gives it the handle. A second variable
    // can get its value from the first, thus the search repeats until it finds no more.
    private static ImmutableHashSet<ILocalSymbol> FindAliases(
        ISymbol handle,
        KnownSymbols symbols,
        ImmutableArray<IOperation> operations)
    {
        ImmutableHashSet<ILocalSymbol> aliases =
            ImmutableHashSet.Create<ILocalSymbol>(equalityComparer: SymbolEqualityComparer.Default);

        IReadOnlyList<IVariableDeclaratorOperation>
            declarators = [.. operations.OfType<IVariableDeclaratorOperation>()];

        while (true)
        {
            ImmutableHashSet<ILocalSymbol> known = aliases;

            ImmutableHashSet<ILocalSymbol> found =
                known.Union(
                    other: declarators
                        .Where(
                            predicate: declarator =>
                                IsHandle(
                                    operation: declarator.GetVariableInitializer()?.Value,
                                    handle: handle,
                                    symbols: symbols,
                                    aliases: known))
                        .Select(selector: static declarator => declarator.Symbol));

            if (found.Count == known.Count)
            {
                return found;
            }

            aliases = found;
        }
    }

    private static bool IsHandle(
        IOperation? operation,
        ISymbol handle,
        KnownSymbols symbols,
        ImmutableHashSet<ILocalSymbol> aliases) =>
        OperationTree.Unwrap(operation: operation) switch
        {
            IParameterReferenceOperation parameter => SymbolEqualityComparer.Default.Equals(
                x: parameter.Parameter,
                y: handle),
            ILocalReferenceOperation local => aliases.Contains(item: local.Local),
            IInvocationOperation invocation =>
                symbols.IsSelect(method: invocation.TargetMethod)
                && IsHandle(operation: invocation.Instance, handle: handle, symbols: symbols, aliases: aliases),
            _ => false
        };
}
