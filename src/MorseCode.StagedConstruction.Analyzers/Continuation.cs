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
    private readonly Func<ControlFlowGraph> graph;
    private readonly CancellationToken cancellationToken;

    // Most continuations return the result of Construct directly and read no local variable. Thus, the
    // graph and its analysis occur only at the first read.
    private readonly Lazy<LocalValues> values;

    private readonly Dictionary<IOperation, bool> handleReads = [];

    private Continuation(
        IAnonymousFunctionOperation lambda,
        IParameterSymbol handle,
        KnownSymbols symbols,
        ImmutableArray<IOperation> operations,
        Func<ControlFlowGraph> graph,
        CancellationToken cancellationToken)
    {
        this.Lambda = lambda;
        this.Handle = handle;
        this.Symbols = symbols;
        this.Operations = operations;
        this.graph = graph;
        this.cancellationToken = cancellationToken;

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
    /// <param name="cancellationToken">Stops the analysis when the compiler cancels it.</param>
    /// <returns>
    ///     The continuation. Null if the lambda does not become a
    ///     <c>StageContinuation</c>, or if its second parameter is not a handle. A
    ///     stage can give a value of a different type to its continuation, and no
    ///     rule applies to that continuation.
    /// </returns>
    public static Continuation? From(
        IAnonymousFunctionOperation lambda,
        KnownSymbols symbols,
        Func<ControlFlowGraph> graph,
        CancellationToken cancellationToken)
    {
        if (lambda.Parent is not IDelegateCreationOperation creation
            || !symbols.IsContinuation(type: creation.Type)
            || lambda.Symbol.Parameters.Length != 2
            || !symbols.IsHandle(type: lambda.Symbol.Parameters[1].Type))
        {
            return null;
        }

        return new Continuation(
            lambda: lambda,
            handle: lambda.Symbol.Parameters[1],
            symbols: symbols,
            operations: lambda.Body.Descendants().ToImmutableArray(),
            graph: graph,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    ///     Gives true if <paramref name="operation" /> is the handle of this
    ///     continuation: the parameter, a read of a local variable whose values
    ///     are all the handle, or a <c>Select</c> of one of these.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns>True for the handle.</returns>
    public bool IsHandle(IOperation? operation) => this.IsHandle(operation: operation, assumed: []);

    /// <summary>
    ///     Gives true if <paramref name="operation" /> reads the parameter of the
    ///     handle or a local variable that holds it. A write to the variable does
    ///     not read it.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns>True for a read of the handle by its name.</returns>
    public bool IsHandleName(IOperation operation) =>
        operation switch
        {
            IParameterReferenceOperation => this.IsHandle(operation: operation),
            ILocalReferenceOperation local => LocalValues.IsRead(reference: local) && this.IsHandle(operation: local),
            _ => false
        };

    /// <summary>
    ///     Gives true if each read of <paramref name="local" /> that the write at
    ///     <paramref name="write" /> can get to is the handle. Then the rules follow
    ///     the handle that the write stores.
    /// </summary>
    /// <param name="local">A local variable that a write gives the handle.</param>
    /// <param name="write">The syntax of the declarator or of the assignment.</param>
    /// <returns>True if the handle stays the handle at each read.</returns>
    public bool KeepsTheHandle(ILocalSymbol local, SyntaxNode write) =>
        this.Operations.OfType<ILocalReferenceOperation>()
            .Where(
                predicate: read =>
                    SymbolEqualityComparer.Default.Equals(x: read.Local, y: local) && LocalValues.IsRead(reference: read))
            .All(
                predicate: read =>
                    this.values.Value.WritesTo(read: read, cancellationToken: this.cancellationToken) is { } writes
                    && (!writes.Contains(item: write) || this.IsHandle(operation: read)));

    /// <summary>
    ///     Gives each value that <paramref name="read" /> can get. See
    ///     <see cref="LocalValues" />.
    /// </summary>
    /// <param name="read">A read of a local variable in the lambda.</param>
    /// <returns>
    ///     The values without conversions. None is a local variable. Empty if the
    ///     analyzer cannot know the value.
    /// </returns>
    public ImmutableArray<IOperation> ValuesOf(ILocalReferenceOperation read) =>
        this.values.Value.Of(read: read, cancellationToken: this.cancellationToken).Values;

    /// <summary>
    ///     Gives true if <paramref name="function" /> is a continuation with a handle
    ///     of its own. The analyzer examines that lambda by itself.
    /// </summary>
    /// <param name="function">A lambda in the body of this continuation.</param>
    /// <returns>True for a continuation.</returns>
    public bool IsContinuation(IAnonymousFunctionOperation function) =>
        From(lambda: function, symbols: this.Symbols, graph: this.graph, cancellationToken: this.cancellationToken)
            is not null;

    private bool IsHandle(IOperation? operation, HashSet<IOperation> assumed) =>
        OperationTree.Unwrap(operation: operation) switch
        {
            IParameterReferenceOperation parameter => SymbolEqualityComparer.Default.Equals(
                x: parameter.Parameter,
                y: this.Handle),
            ILocalReferenceOperation read => this.IsHandleRead(read: read, assumed: assumed),
            IInvocationOperation invocation =>
                this.Symbols.IsSelect(method: invocation.TargetMethod)
                && this.IsHandle(operation: invocation.Instance, assumed: assumed),
            _ => false
        };

    // A read is the handle when the analyzer knows each value that can get to it, and each value is the
    // handle. Only a variable of a handle type can be the handle, thus no graph is made for other types.
    // A Select in a loop can read the value that it gave before. The search takes such a read as the handle
    // while it is in that read. Thus, the other values give the result. A result that used that assumption is not kept,
    // except for the read where the search started.
    private bool IsHandleRead(ILocalReferenceOperation read, HashSet<IOperation> assumed)
    {
        if (!this.Symbols.IsHandle(type: read.Local.Type))
        {
            return false;
        }

        if (this.handleReads.TryGetValue(key: read, value: out bool known))
        {
            return known;
        }

        if (!assumed.Add(item: read))
        {
            return true;
        }

        LocalRead found = this.values.Value.Of(read: read, cancellationToken: this.cancellationToken);

        bool isHandle =
            found is { IsComplete: true, Values.Length: > 0 }
            && found.Values.All(predicate: value => this.IsHandle(operation: value, assumed: assumed));

        assumed.Remove(item: read);

        if (assumed.Count == 0)
        {
            this.handleReads.Add(key: read, value: isHandle);
        }

        return isHandle;
    }
}
