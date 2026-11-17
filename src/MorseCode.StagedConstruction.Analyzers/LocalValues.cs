using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace MorseCode.StagedConstruction.Analyzers;

/// <summary>
///     Finds the values that a local variable of a continuation can hold where
///     the code reads the variable.
/// </summary>
/// <remarks>
///     <para>
///         The search is an analysis of reaching definitions over the control
///         flow graph of the compiler. A write replaces each earlier write on its
///         path. Each path through a branch, a loop, or a try statement adds its
///         own writes. If a value is a different local variable, the search
///         also follows that variable.
///     </para>
///     <para>
///         The analyzer reports only what it can prove. Thus, a write whose value
///         the analyzer cannot know, such as an out argument, gives no value. A
///         variable gives no values at all if code can change it out of the order
///         of the graph. That is a write in a different function, a write in a
///         finally block, or a reference to the variable.
///     </para>
///     <para>
///         Each value is an operation of the tree of the continuation, and not of
///         the graph. Thus, the rules read the values as they read the code.
///     </para>
/// </remarks>
internal sealed class LocalValues
{
    private readonly Func<ControlFlowGraph> rootGraph;
    private readonly Dictionary<IMethodSymbol, SyntaxNode> functions;
    private readonly Dictionary<(SyntaxNode Syntax, OperationKind Kind), IOperation> tree;
    private readonly HashSet<ILocalSymbol> excluded;
    private readonly Dictionary<IMethodSymbol, FunctionGraph?> graphs;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LocalValues" /> class.
    /// </summary>
    /// <param name="lambda">The continuation.</param>
    /// <param name="operations">Each operation in the body of the continuation.</param>
    /// <param name="rootGraph">
    ///     Gives the graph of the method body or the initializer that holds the
    ///     continuation.
    /// </param>
    public LocalValues(
        IAnonymousFunctionOperation lambda,
        ImmutableArray<IOperation> operations,
        Func<ControlFlowGraph> rootGraph)
    {
        this.rootGraph = rootGraph;
        this.functions = new Dictionary<IMethodSymbol, SyntaxNode>(comparer: SymbolEqualityComparer.Default);
        this.tree = new Dictionary<(SyntaxNode Syntax, OperationKind Kind), IOperation>();
        this.excluded = new HashSet<ILocalSymbol>(comparer: SymbolEqualityComparer.Default);
        this.graphs = new Dictionary<IMethodSymbol, FunctionGraph?>(comparer: SymbolEqualityComparer.Default);

        this.functions[key: lambda.Symbol] = lambda.Syntax;

        foreach (IOperation operation in operations)
        {
            switch (operation)
            {
                case IAnonymousFunctionOperation function:
                    this.functions[key: function.Symbol] = function.Syntax;
                    break;
                case ILocalFunctionOperation function:
                    this.functions[key: function.Symbol] = function.Syntax;
                    break;
                case ILocalReferenceOperation reference when IsChangedOutOfOrder(reference: reference):
                    this.excluded.Add(item: reference.Local);
                    break;
            }

            // The analyzer keeps the first operation for each pair of syntax and OperationKind. A conversion
            // can have the syntax of its operand, but its OperationKind is different.
            (SyntaxNode Syntax, OperationKind Kind) key = (operation.Syntax, operation.Kind);

            if (!this.tree.ContainsKey(key: key))
            {
                this.tree.Add(key: key, value: operation);
            }
        }
    }

    /// <summary>
    ///     Gives each value that <paramref name="read" /> can get.
    /// </summary>
    /// <param name="read">A read of a local variable in the continuation.</param>
    /// <param name="cancellationToken">Stops the search when the compiler cancels the analysis.</param>
    /// <returns>
    ///     The values, without conversions, in the tree of the continuation, and
    ///     if they are all of the values.
    /// </returns>
    public LocalRead Of(ILocalReferenceOperation read, CancellationToken cancellationToken)
    {
        if (this.Start(read: read, cancellationToken: cancellationToken) is not ({ } graph, { } start))
        {
            return new LocalRead(values: ImmutableArray<IOperation>.Empty, isComplete: false);
        }

        Search search = new(cancellationToken: cancellationToken);
        this.Follow(graph: graph, value: start, search: search);

        return new LocalRead(values: search.Found.ToImmutableArray(), isComplete: search.IsComplete);
    }

    /// <summary>
    ///     Gives the syntax of each write that can get to <paramref name="read" />.
    ///     That is the syntax of the declarator or of the assignment for a write
    ///     with a value.
    /// </summary>
    /// <param name="read">A read of a local variable in the continuation.</param>
    /// <param name="cancellationToken">Stops the search when the compiler cancels the analysis.</param>
    /// <returns>The syntax of each write, or null if the analyzer does not follow the variable.</returns>
    public ImmutableArray<SyntaxNode>? WritesTo(ILocalReferenceOperation read, CancellationToken cancellationToken) =>
        this.Start(read: read, cancellationToken: cancellationToken) is ({ } graph, { } start)
        && this.Reaching(graph: graph, read: start, cancellationToken: cancellationToken) is { } reaching
            ? reaching.Definitions.Select(selector: static definition => definition.Syntax).ToImmutableArray()
            : null;

    /// <summary>
    ///     Gives true if <paramref name="reference" /> reads the variable. The target
    ///     of an assignment or of a deconstruction, and an out argument, only
    ///     write it.
    /// </summary>
    /// <param name="reference">A reference to a local variable.</param>
    /// <returns>True for a read.</returns>
    public static bool IsRead(ILocalReferenceOperation reference)
    {
        IOperation child = reference;
        IOperation? parent = reference.Parent;

        while (parent is IParenthesizedOperation or IDeclarationExpressionOperation or ITupleOperation)
        {
            child = parent;
            parent = parent.Parent;
        }

        return parent switch
        {
            ISimpleAssignmentOperation assignment => !ReferenceEquals(objA: assignment.Target, objB: child),
            IDeconstructionAssignmentOperation assignment => !ReferenceEquals(objA: assignment.Target, objB: child),
            IArgumentOperation { Parameter.RefKind: RefKind.Out } => false,
            _ => true
        };
    }

    // A write in a different function can occur at any time. A reference to the variable can write it at
    // any time. In the two cases, the order of the graph does not give the value.
    private static bool IsChangedOutOfOrder(ILocalReferenceOperation reference)
    {
        IOperation child = reference;
        IOperation? parent = reference.Parent;

        // A deconstruction writes each variable in its tuple, and out var declares the variable it writes.
        while (parent is IParenthesizedOperation or IDeclarationExpressionOperation or ITupleOperation)
        {
            child = parent;
            parent = parent.Parent;
        }

        bool isWrite =
            parent switch
            {
                ISimpleAssignmentOperation assignment => ReferenceEquals(objA: assignment.Target, objB: child),
                IDeconstructionAssignmentOperation assignment => ReferenceEquals(objA: assignment.Target, objB: child),
                ICompoundAssignmentOperation assignment => ReferenceEquals(objA: assignment.Target, objB: child),
                ICoalesceAssignmentOperation assignment => ReferenceEquals(objA: assignment.Target, objB: child),
                IIncrementOrDecrementOperation => true,
                IArgumentOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out } => true,
                _ => false
            };

        bool isReference =
            parent switch
            {
                ISimpleAssignmentOperation { IsRef: true } assignment =>
                    ReferenceEquals(objA: assignment.Value, objB: child),
                IVariableInitializerOperation
                {
                    Parent: IVariableDeclaratorOperation { Symbol.RefKind: not RefKind.None }
                } => true,
                IConditionalOperation { IsRef: true } or IAddressOfOperation => true,
                _ => false
            };

        return isReference
               || reference.Local.RefKind != RefKind.None
               || (isWrite
                   && !SymbolEqualityComparer.Default.Equals(
                       x: OwnerOf(operation: reference),
                       y: reference.Local.ContainingSymbol));
    }

    private static IMethodSymbol? Symbol(IOperation function) =>
        function switch
        {
            IAnonymousFunctionOperation lambda => lambda.Symbol,
            ILocalFunctionOperation local => local.Symbol,
            _ => null
        };

    // The graph of the function in the root graph. The search goes only into a function whose syntax holds
    // the syntax of the function to find.
    private static ControlFlowGraph? Find(
        ControlFlowGraph graph,
        ISymbol function,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // The graph does not hold the body of a lambda. Thus, at most one lambda in the graph holds the span.
        IFlowAnonymousFunctionOperation? lambda =
            graph.Blocks.SelectMany(selector: Roots)
                .SelectMany(selector: static root => root.DescendantsAndSelf())
                .OfType<IFlowAnonymousFunctionOperation>()
                .FirstOrDefault(predicate: candidate => candidate.Syntax.Span.Contains(span: span));

        if (lambda is not null)
        {
            ControlFlowGraph inner =
                graph.GetAnonymousFunctionControlFlowGraph(
                    anonymousFunction: lambda,
                    cancellationToken: cancellationToken);

            return SymbolEqualityComparer.Default.Equals(x: lambda.Symbol, y: function)
                ? inner
                : Find(graph: inner, function: function, span: span, cancellationToken: cancellationToken);
        }

        IMethodSymbol? local =
            graph.LocalFunctions.FirstOrDefault(
                predicate: candidate =>
                    candidate.DeclaringSyntaxReferences.FirstOrDefault()?.Span.Contains(span: span) == true);

        if (local is null)
        {
            return null;
        }

        ControlFlowGraph body =
            graph.GetLocalFunctionControlFlowGraph(localFunction: local, cancellationToken: cancellationToken);

        return SymbolEqualityComparer.Default.Equals(x: local, y: function)
            ? body
            : Find(graph: body, function: function, span: span, cancellationToken: cancellationToken);
    }

    // The operations of a block that have no parent: its statements and the value of its branch.
    private static IEnumerable<IOperation> Roots(BasicBlock block) =>
        block.BranchValue is { } value ? block.Operations.Append(element: value) : block.Operations;

    private static IMethodSymbol? OwnerOf(IOperation operation) =>
        OperationTree.Owner(operation: operation) is { } owner ? Symbol(function: owner) : null;

    // The graph of the function that holds the read, and the read in that graph.
    private (FunctionGraph Graph, ILocalReferenceOperation Read)? Start(
        ILocalReferenceOperation read,
        CancellationToken cancellationToken) =>
        OwnerOf(operation: read) is { } owner
        && this.Graph(function: owner, cancellationToken: cancellationToken) is { } graph
        && graph.Read(syntax: read.Syntax, local: read.Local) is { } start
            ? (graph, start)
            : null;

    private FunctionGraph? Graph(IMethodSymbol function, CancellationToken cancellationToken)
    {
        if (this.graphs.TryGetValue(key: function, value: out FunctionGraph? known))
        {
            return known;
        }

        FunctionGraph? graph =
            this.functions.TryGetValue(key: function, value: out SyntaxNode? syntax)
            && Find(
                    graph: this.rootGraph(),
                    function: function,
                    span: syntax.Span,
                    cancellationToken: cancellationToken) is { } found
                ? new FunctionGraph(graph: found)
                : null;

        this.graphs.Add(key: function, value: graph);

        return graph;
    }

    private void Follow(FunctionGraph graph, IOperation? value, Search search)
    {
        search.CancellationToken.ThrowIfCancellationRequested();

        switch (OperationTree.Unwrap(operation: value))
        {
            case null:
                return;

            // A conditional, a null coalescing, and a switch expression put each of their values in one
            // capture.
            case IFlowCaptureReferenceOperation capture:
                if (search.Captures.Add(item: (graph, capture.Id)))
                {
                    foreach (IFlowCaptureOperation write in graph.Captures(id: capture.Id))
                    {
                        this.Follow(graph: graph, value: write.Value, search: search);
                    }
                }

                return;
            case ILocalReferenceOperation read:
                if (!search.Reads.Add(item: read))
                {
                    return;
                }

                // No write at all, or a write with a value that the analyzer cannot know, leaves a value out.
                if (this.Reaching(graph: graph, read: read, cancellationToken: search.CancellationToken) is not
                    { Definitions.Count: > 0 } reaching)
                {
                    search.IsComplete = false;

                    return;
                }

                foreach (Definition definition in reaching.Definitions)
                {
                    if (definition.Value is null)
                    {
                        search.IsComplete = false;
                    }
                    else
                    {
                        this.Follow(graph: reaching.Graph, value: definition.Value, search: search);
                    }
                }

                return;
            case var leaf:
                OperationKind kind =
                    leaf.Kind == OperationKind.FlowAnonymousFunction ? OperationKind.AnonymousFunction : leaf.Kind;

                if (!this.tree.TryGetValue(key: (leaf.Syntax, kind), value: out IOperation? operation))
                {
                    search.IsComplete = false;
                }
                else if (search.Seen.Add(item: operation))
                {
                    search.Found.Add(item: operation);
                }

                return;
        }
    }

    // The writes that can get to a read, and the graph that holds them. Null if the analyzer does not follow
    // the variable. A variable that a different function declares can get its value at any time. Thus, the
    // analyzer follows it only when it has one write.
    private (FunctionGraph Graph, IReadOnlyList<Definition> Definitions)? Reaching(
        FunctionGraph graph,
        ILocalReferenceOperation read,
        CancellationToken cancellationToken)
    {
        ILocalSymbol local = read.Local;

        if (this.excluded.Contains(item: local)
            || local.ContainingSymbol is not IMethodSymbol declaring
            || this.Graph(function: declaring, cancellationToken: cancellationToken) is not { } where
            || where.IsWrittenInFinally(local: local))
        {
            return null;
        }

        if (ReferenceEquals(objA: where, objB: graph))
        {
            return (graph, [.. graph.Reaching(local: local, read: read, cancellationToken: cancellationToken)]);
        }

        IReadOnlyList<Definition> definitions = where.Definitions(local: local);

        return definitions.Count == 1 ? (where, definitions) : null;
    }

    // The state of one search. The sets stop the search at a value that it found before, thus a loop
    // ends.
    private sealed class Search(in CancellationToken cancellationToken)
    {
        public CancellationToken CancellationToken { get; } = cancellationToken;

        public HashSet<(FunctionGraph Graph, CaptureId Id)> Captures { get; } = [];

        public HashSet<IOperation> Reads { get; } = [];

        public HashSet<IOperation> Seen { get; } = [];

        public List<IOperation> Found { get; } = [];

        // False when a write with a value that the analyzer cannot know gets to a read.
        public bool IsComplete { get; set; } = true;
    }

    // One write of a local variable. A null value is a write whose value the analyzer cannot know.
    private sealed class Definition(in int block, in int position, in IOperation? value, in SyntaxNode syntax)
    {
        public SyntaxNode Syntax { get; } = syntax;

        public int Block { get; } = block;

        public int Position { get; } = position;

        public IOperation? Value { get; } = value;
    }

    // The graph of one function, with the position of each operation in it. In a block, the operations are in
    // the order that they run: each operation comes after its operands.
    private sealed class FunctionGraph
    {
        private readonly ControlFlowGraph graph;
        private readonly Dictionary<IOperation, (int Block, int Position)> places = [];
        private readonly Dictionary<CaptureId, List<IFlowCaptureOperation>> captures = [];
        private readonly Dictionary<SyntaxNode, List<ILocalReferenceOperation>> reads = [];

        private readonly Dictionary<ILocalSymbol, List<Definition>> definitions =
            new(comparer: SymbolEqualityComparer.Default);

        private readonly HashSet<ILocalSymbol> writtenInFinally = new(comparer: SymbolEqualityComparer.Default);

        private readonly Dictionary<ILocalSymbol, HashSet<Definition>[]> atEntry =
            new(comparer: SymbolEqualityComparer.Default);

        public FunctionGraph(ControlFlowGraph graph)
        {
            this.graph = graph;

            foreach (BasicBlock block in graph.Blocks)
            {
                int position = 0;
                IEnumerable<IOperation> operations = Roots(block: block).SelectMany(selector: InOrder);

                foreach (IOperation operation in operations)
                {
                    this.places.Add(key: operation, value: (block.Ordinal, position));
                    this.Record(operation: operation, block: block, position: position);
                    position++;
                }
            }
        }

        public IEnumerable<IFlowCaptureOperation> Captures(CaptureId id) =>
            this.captures.TryGetValue(key: id, value: out List<IFlowCaptureOperation>? found) ? found : [];

        public ILocalReferenceOperation? Read(SyntaxNode syntax, ISymbol local) =>
            this.reads.TryGetValue(key: syntax, value: out List<ILocalReferenceOperation>? found)
                ? found.FirstOrDefault(
                    predicate: read => SymbolEqualityComparer.Default.Equals(x: read.Local, y: local))
                : null;

        public IReadOnlyList<Definition> Definitions(ILocalSymbol local) =>
            this.definitions.TryGetValue(key: local, value: out List<Definition>? found) ? found : [];

        public bool IsWrittenInFinally(ILocalSymbol local) => this.writtenInFinally.Contains(item: local);

        // The writes that can get to the read. That is the last write before the read in its block. If the
        // block has no such write, it is each write that can get to the start of the block.
        public IEnumerable<Definition> Reaching(
            ILocalSymbol local,
            IOperation read,
            CancellationToken cancellationToken)
        {
            if (!this.places.TryGetValue(key: read, value: out (int Block, int Position) place))
            {
                return [];
            }

            Definition? last =
                this.Definitions(local: local)
                    .Where(
                        predicate: definition =>
                            definition.Block == place.Block && definition.Position < place.Position)
                    .OrderByDescending(keySelector: static definition => definition.Position)
                    .FirstOrDefault();

            return last is not null
                ? [last]
                : this.AtEntry(local: local, cancellationToken: cancellationToken)[place.Block];
        }

        // Each operation of a tree before the operation that uses it. A lambda has no operations in this
        // graph, because it has a graph of its own.
        private static IEnumerable<IOperation> InOrder(IOperation root) =>
            root.ChildOperations.SelectMany(selector: InOrder).Append(element: root);

        // The try region that a catch, a filter, or a finally region follows, if the block starts one. An
        // exception can occur at each operation in the try region, thus each write in it can get to the
        // handler. A handler after a filter also gets the writes of the filter.
        private static IEnumerable<ControlFlowRegion> Protected(BasicBlock block)
        {
            for (ControlFlowRegion? region = block.EnclosingRegion;
                 region is not null && region.FirstBlockOrdinal == block.Ordinal;
                 region = region.EnclosingRegion)
            {
                if (region.Kind is not (ControlFlowRegionKind.Catch
                    or ControlFlowRegionKind.Filter
                    or ControlFlowRegionKind.Finally))
                {
                    continue;
                }

                ControlFlowRegion? owner = region.EnclosingRegion;

                if (owner is { Kind: ControlFlowRegionKind.FilterAndHandler })
                {
                    if (region.Kind == ControlFlowRegionKind.Catch)
                    {
                        yield return owner.NestedRegions[0];
                    }

                    owner = owner.EnclosingRegion;
                }

                if (owner is { Kind: ControlFlowRegionKind.TryAndCatch or ControlFlowRegionKind.TryAndFinally })
                {
                    yield return owner.NestedRegions[0];
                }
            }
        }

        // A branch can occur if its source can run, and if a constant condition does not prevent it. The
        // graph keeps the exit of while (true) as a branch, but the code leaves that loop only at a break.
        private static bool IsTaken(ControlFlowBranch branch)
        {
            BasicBlock source = branch.Source;

            if (!source.IsReachable)
            {
                return false;
            }

            if (source.ConditionKind == ControlFlowConditionKind.None
                || source.BranchValue?.ConstantValue is not { HasValue: true, Value: bool condition })
            {
                return true;
            }

            bool isConditionalTaken = condition == (source.ConditionKind == ControlFlowConditionKind.WhenTrue);

            return branch.IsConditionalSuccessor == isConditionalTaken;
        }

        private static bool IsInFinally(BasicBlock block)
        {
            for (ControlFlowRegion? region = block.EnclosingRegion; region is not null; region = region.EnclosingRegion)
            {
                if (region.Kind == ControlFlowRegionKind.Finally)
                {
                    return true;
                }
            }

            return false;
        }

        // Each write whose value the analyzer cannot know. A deconstruction writes each variable in its
        // target.
        private static IEnumerable<ILocalSymbol> UnknownWrites(IOperation operation) =>
            operation switch
            {
                ICompoundAssignmentOperation { Target: ILocalReferenceOperation target } => [target.Local],
                ICoalesceAssignmentOperation { Target: ILocalReferenceOperation target } => [target.Local],
                IIncrementOrDecrementOperation { Target: ILocalReferenceOperation target } => [target.Local],
                IArgumentOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out } argument =>
                    argument.Value switch
                    {
                        ILocalReferenceOperation target => [target.Local],
                        IDeclarationExpressionOperation { Expression: ILocalReferenceOperation target } =>
                            [target.Local],
                        _ => []
                    },
                IDeconstructionAssignmentOperation deconstruction =>
                    deconstruction.Target.DescendantsAndSelf()
                        .OfType<ILocalReferenceOperation>()
                        .Select(selector: static target => target.Local),
                IDeclarationPatternOperation { DeclaredSymbol: ILocalSymbol local } pattern
                    when !HoldsTheTestedValue(pattern: pattern) => [local],
                IRecursivePatternOperation { DeclaredSymbol: ILocalSymbol local } pattern
                    when !HoldsTheTestedValue(pattern: pattern) => [local],
                IListPatternOperation { DeclaredSymbol: ILocalSymbol local } pattern
                    when !HoldsTheTestedValue(pattern: pattern) => [local],
                _ => []
            };

        // A variable of a pattern at the top of an is gets the value that the is tests. Each side of an and at
        // the top is also at the top. The graph makes each arm of a switch an is of its own, thus this also
        // holds for a switch.
        private static bool HoldsTheTestedValue(IOperation pattern)
        {
            IOperation child = pattern;
            IOperation? parent = pattern.Parent;

            while (parent is IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.And })
            {
                child = parent;
                parent = parent.Parent;
            }

            return parent is IIsPatternOperation test && ReferenceEquals(objA: test.Pattern, objB: child);
        }

        private void Record(IOperation operation, BasicBlock block, int position)
        {
            switch (operation)
            {
                case IFlowCaptureOperation capture:
                    Add(key: capture.Id, item: capture, to: this.captures);
                    break;
                case ILocalReferenceOperation read:
                    Add(key: read.Syntax, item: read, to: this.reads);
                    break;
                case ISimpleAssignmentOperation { IsRef: false, Target: ILocalReferenceOperation target } assignment:
                    this.Define(
                        local: target.Local,
                        block: block,
                        position: position,
                        value: assignment.Value,
                        syntax: operation.Syntax);

                    break;
                case IIsPatternOperation test:
                    foreach ((ILocalSymbol local, IPatternOperation pattern) in OperationTree.TestedValueVariables(
                                 pattern: test.Pattern))
                    {
                        this.Define(
                            local: local,
                            block: block,
                            position: position,
                            value: test.Value,
                            syntax: pattern.Syntax);
                    }

                    break;
                default:
                    foreach (ILocalSymbol local in UnknownWrites(operation: operation))
                    {
                        this.Define(
                            local: local,
                            block: block,
                            position: position,
                            value: null,
                            syntax: operation.Syntax);
                    }

                    break;
            }
        }

        private static void Add<TKey, TItem>(TKey key, TItem item, IDictionary<TKey, List<TItem>> to)
            where TKey : notnull
        {
            if (!to.TryGetValue(key: key, value: out List<TItem>? list))
            {
                list = [];
                to.Add(key: key, value: list);
            }

            list.Add(item: item);
        }

        private void Define(
            ILocalSymbol local,
            BasicBlock block,
            int position,
            IOperation? value,
            SyntaxNode syntax)
        {
            if (IsInFinally(block: block))
            {
                this.writtenInFinally.Add(item: local);
            }

            if (!this.definitions.TryGetValue(key: local, value: out List<Definition>? list))
            {
                list = [];
                this.definitions.Add(key: local, value: list);
            }

            list.Add(item: new Definition(block: block.Ordinal, position: position, value: value, syntax: syntax));
        }

        // The writes that can get to the start of each block. The analysis repeats until no set changes. A
        // set only gets larger, thus the analysis ends.
        private HashSet<Definition>[] AtEntry(ILocalSymbol local, CancellationToken cancellationToken)
        {
            if (this.atEntry.TryGetValue(key: local, value: out HashSet<Definition>[]? known))
            {
                return known;
            }

            ImmutableArray<BasicBlock> blocks = this.graph.Blocks;
            IReadOnlyList<Definition> all = this.Definitions(local: local);

            ILookup<int, Definition> inBlock = all.ToLookup(keySelector: static definition => definition.Block);

            Definition?[] last =
            [
                .. blocks.Select(
                    selector: block =>
                        inBlock[key: block.Ordinal]
                            .OrderByDescending(keySelector: static definition => definition.Position)
                            .FirstOrDefault())
            ];

            HashSet<Definition>[] entry = [.. blocks.Select(selector: static _ => new HashSet<Definition>())];

            bool changed = true;

            while (changed)
            {
                cancellationToken.ThrowIfCancellationRequested();
                changed = false;

                foreach (BasicBlock block in blocks.Where(predicate: static block => block.IsReachable))
                {
                    HashSet<Definition> reaching = [];

                    foreach (ControlFlowBranch branch in block.Predecessors.Where(predicate: IsTaken))
                    {
                        reaching.UnionWith(
                            other: last[branch.Source.Ordinal] is { } definition
                                ? [definition]
                                : entry[branch.Source.Ordinal]);
                    }

                    foreach (ControlFlowRegion region in Protected(block: block))
                    {
                        for (int ordinal = region.FirstBlockOrdinal; ordinal <= region.LastBlockOrdinal; ordinal++)
                        {
                            reaching.UnionWith(other: entry[ordinal]);
                            reaching.UnionWith(other: inBlock[key: ordinal]);
                        }
                    }

                    if (!reaching.SetEquals(other: entry[block.Ordinal]))
                    {
                        entry[block.Ordinal] = reaching;
                        changed = true;
                    }
                }
            }

            this.atEntry.Add(key: local, value: entry);

            return entry;
        }
    }
}
