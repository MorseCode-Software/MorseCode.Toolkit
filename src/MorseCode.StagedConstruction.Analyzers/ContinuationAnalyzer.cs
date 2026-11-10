using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace MorseCode.StagedConstruction.Analyzers;

/// <summary>
///     Reports a continuation that does not return the result of its own handle.
/// </summary>
/// <remarks>
///     <para>
///         A base gives a continuation a handle, and the continuation must return
///         the <c>Constructed</c> that <c>Construct</c> or <c>Advance</c> gives on
///         that handle. The compiler checks only the type of the result. A base can
///         check the value when it runs, as <c>ViewModelBase.CreateBase</c> does.
///         These rules find the usual mistakes before the code runs.
///     </para>
///     <para>
///         The analyzer examines each lambda that becomes a
///         <c>StageContinuation</c> and whose second parameter is a
///         <c>Constructor</c> or a <c>Stage</c>. Six rules are warnings, and each
///         reports only what it can prove. The seventh is information: it reports
///         where the handle goes to code that the other rules cannot follow.
///     </para>
///     <para>
///         The analyzer uses nullable types and not Maybe, because it runs in the
///         compiler, which loads only Roslyn and the analyzer.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
// ReSharper disable once InheritdocConsiderUsage - The summary of DiagnosticAnalyzer does not say which rules this analyzer reports.
public sealed class ContinuationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            ContinuationRules.ValueReadFromCall,
            ContinuationRules.ResultNotUsed,
            ContinuationRules.NullReturned,
            ContinuationRules.ForeignHandleReturned,
            ContinuationRules.BaseValuesNotUsed,
            ContinuationRules.HandleNotUsed,
            ContinuationRules.HandleLeaves);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(analysisMode: GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(
            action: static start =>
            {
                // A project that does not reference the library has no continuation to examine.
                if (KnownSymbols.Find(compilation: start.Compilation) is not { } symbols)
                {
                    return;
                }

                start.RegisterOperationAction(
                    action: operationContext => Check(context: operationContext, symbols: symbols),
                    operationKinds: ImmutableArray.Create(item: OperationKind.AnonymousFunction));
            });
    }

    private static void Check(OperationAnalysisContext context, KnownSymbols symbols)
    {
        if (context.Operation is not IAnonymousFunctionOperation lambda
            || Continuation.From(lambda: lambda, symbols: symbols) is not { } continuation)
        {
            return;
        }

        IEnumerable<Diagnostic> found =
            HandleNotUsed(continuation: continuation)
                .Concat(second: ReturnedValues(continuation: continuation))
                .Concat(second: Calls(continuation: continuation))
                .Concat(second: HandleLeaves(continuation: continuation));

        foreach (Diagnostic diagnostic in found)
        {
            context.ReportDiagnostic(diagnostic: diagnostic);
        }
    }

    // MC1006. A continuation that only throws returns no value, thus it has no result to get wrong.
    private static IEnumerable<Diagnostic> HandleNotUsed(Continuation continuation)
    {
        bool isUsed =
            continuation.Operations.OfType<IParameterReferenceOperation>()
                .Any(
                    predicate: reference =>
                        SymbolEqualityComparer.Default.Equals(x: reference.Parameter, y: continuation.Handle));

        if (isUsed || !OperationTree.ReturnsValue(function: continuation.Lambda))
        {
            return [];
        }

        return
        [
            Diagnostic.Create(
                descriptor: ContinuationRules.HandleNotUsed,
                location: continuation.Handle.Locations.FirstOrDefault() ?? continuation.Lambda.Syntax.GetLocation(),
                messageArgs: [continuation.Handle.Name])
        ];
    }

    // MC1003 and MC1004, for each value that the continuation can return.
    private static IEnumerable<Diagnostic> ReturnedValues(Continuation continuation) =>
        from result in continuation.Operations.OfType<IReturnOperation>()
        where IsResultOf(continuation: continuation, function: OperationTree.Owner(operation: result))
        from leaf in OperationTree.Leaves(value: result.ReturnedValue)
        from diagnostic in ReturnedValue(continuation: continuation, leaf: leaf)
        select diagnostic;

    // A return in the lambda itself gives the result of the continuation. So does a return in a lambda
    // in its body that returns a Constructed, such as the function of Transaction.Run. The analyzer
    // examines by itself a lambda in the body that is a continuation with its own handle.
    private static bool IsResultOf(Continuation continuation, IOperation? function) =>
        ReferenceEquals(objA: function, objB: continuation.Lambda)
        || (function is IAnonymousFunctionOperation inner
            && continuation.Symbols.IsConstructed(type: inner.Symbol.ReturnType)
            && !continuation.IsContinuation(function: inner));

    private static IEnumerable<Diagnostic> ReturnedValue(Continuation continuation, IOperation leaf)
    {
        if (leaf is IDefaultValueOperation || leaf.ConstantValue is { HasValue: true, Value: null })
        {
            return
            [
                Diagnostic.Create(
                    descriptor: ContinuationRules.NullReturned,
                    location: leaf.Syntax.GetLocation(),
                    messageArgs: [continuation.Handle.Name])
            ];
        }

        // A variable that holds the result stands for the call that made it.
        IOperation? value =
            leaf is ILocalReferenceOperation local ? continuation.InitialValue(local: local.Local) : leaf;

        if (value is IInvocationOperation call
            && continuation.Symbols.IsConstruct(method: call.TargetMethod)
            && Source(continuation: continuation, handle: call.Instance) is IInvocationOperation source
            && continuation.Symbols.IsConstructorFrom(method: source.TargetMethod))
        {
            return
            [
                Diagnostic.Create(
                    descriptor: ContinuationRules.ForeignHandleReturned,
                    location: leaf.Syntax.GetLocation(),
                    messageArgs: [continuation.Handle.Name])
            ];
        }

        return [];
    }

    // The operation that made a handle. The search goes back through each Select and through each local
    // variable.
    private static IOperation? Source(Continuation continuation, IOperation? handle)
    {
        IOperation? current = OperationTree.Unwrap(operation: handle);

        while (true)
        {
            switch (current)
            {
                case IInvocationOperation select when continuation.Symbols.IsSelect(method: select.TargetMethod):
                    current = OperationTree.Unwrap(operation: select.Instance);
                    break;
                case ILocalReferenceOperation local when continuation.InitialValue(local: local.Local) is { } initial:
                    current = initial;
                    break;
                default:
                    return current;
            }
        }
    }

    // MC1001, MC1002, and MC1005, for each call to Construct or Advance on the handle.
    private static IEnumerable<Diagnostic> Calls(Continuation continuation) =>
        from call in continuation.Operations.OfType<IInvocationOperation>()
        let isConstruct = continuation.Symbols.IsConstruct(method: call.TargetMethod)
        where isConstruct || continuation.Symbols.IsAdvance(method: call.TargetMethod)
        where continuation.IsHandle(operation: call.Instance)
        from diagnostic in Call(continuation: continuation, call: call, isConstruct: isConstruct)
        select diagnostic;

    private static IEnumerable<Diagnostic> Call(Continuation continuation, IInvocationOperation call, bool isConstruct)
    {
        switch (OperationTree.User(operation: call))
        {
            case IPropertyReferenceOperation property
                when continuation.Symbols.IsConstructedValue(property: property.Property):
                yield return Diagnostic.Create(
                    descriptor: ContinuationRules.ValueReadFromCall,
                    location: OperationTree.NameLocation(operation: property),
                    messageArgs: [call.TargetMethod.Name]);

                break;

            // A statement that is only the call, and an assignment of the call to a discard.
            case IExpressionStatementOperation or ISimpleAssignmentOperation { Target: IDiscardOperation }:
                yield return Diagnostic.Create(
                    descriptor: ContinuationRules.ResultNotUsed,
                    location: OperationTree.NameLocation(operation: call),
                    messageArgs: [call.TargetMethod.Name]);

                break;
        }

        if (isConstruct && UnusedBaseValues(call: call) is { } parameter)
        {
            yield return Diagnostic.Create(
                descriptor: ContinuationRules.BaseValuesNotUsed,
                location: parameter.Locations.FirstOrDefault() ?? call.Syntax.GetLocation(),
                messageArgs: [parameter.Name]);
        }
    }

    // The parameter of the lambda that Construct gets, if that lambda returns a value and does not read
    // the parameter. A method group, and a lambda that only throws, give null.
    private static IParameterSymbol? UnusedBaseValues(IInvocationOperation call)
    {
        if (call.Arguments.Length != 1
            || OperationTree.Unwrap(operation: call.Arguments[0].Value) is not IDelegateCreationOperation
            {
                Target: IAnonymousFunctionOperation { Symbol.Parameters.Length: 1 } function
            })
        {
            return null;
        }

        IParameterSymbol parameter = function.Symbol.Parameters[0];

        bool isRead =
            function.Body.Descendants()
                .OfType<IParameterReferenceOperation>()
                .Any(
                    predicate: reference =>
                        SymbolEqualityComparer.Default.Equals(x: reference.Parameter, y: parameter));

        return isRead || !OperationTree.ReturnsValue(function: function) ? null : parameter;
    }

    // MC1007, for each place that names the handle.
    private static IEnumerable<Diagnostic> HandleLeaves(Continuation continuation) =>
        from reference in continuation.Operations
        where continuation.IsHandleName(operation: reference)
        let reason =
            Destination(continuation: continuation, reference: reference)
            ?? Captor(continuation: continuation, reference: reference)
        where reason is not null
        select Diagnostic.Create(
            descriptor: ContinuationRules.HandleLeaves,
            location: reference.Syntax.GetLocation(),
            messageArgs: [continuation.Handle.Name, reason]);

    // Where the handle goes, as text for the message, or null if the analyzer can follow it there.
    private static string? Destination(Continuation continuation, IOperation reference)
    {
        // A Select of the handle is the handle, thus the use of the last Select is the use to examine.
        IOperation handle = reference;

        while (OperationTree.User(operation: handle) is IInvocationOperation select
               && continuation.Symbols.IsSelect(method: select.TargetMethod)
               && OperationTree.Same(first: select.Instance, second: handle))
        {
            handle = select;
        }

        return OperationTree.User(operation: handle) switch
        {
            // A member of the handle. Construct and Advance are the usual ones, and the other rules examine them.
            IInvocationOperation call when OperationTree.Same(first: call.Instance, second: handle) => null,
            IMemberReferenceOperation member when OperationTree.Same(first: member.Instance, second: handle) => null,

            IArgumentOperation { Parent: IInvocationOperation call } => ArgumentTo(
                continuation: continuation,
                call: call),
            IArgumentOperation { Parent: IObjectCreationOperation creation } =>
                continuation.Symbols.IsHandle(type: creation.Type)
                    ? null
                    : $"is given to the constructor of {creation.Type?.Name}",

            // The declaration of a local variable. Continuation follows that variable as the handle.
            IVariableInitializerOperation => null,

            ITupleOperation or IReturnOperation => IsInStageBody(continuation: continuation, operation: handle)
                ? null
                : "is returned from a lambda",

            // A comparison, a type test, and a nameof do not move the handle.
            IBinaryOperation or IIsPatternOperation or IIsTypeOperation or INameOfOperation => null,
            ISimpleAssignmentOperation { Target: IDiscardOperation } => null,
            ISimpleAssignmentOperation assignment => $"is stored in {assignment.Target.Syntax}",

            _ => "is used in an expression"
        };
    }

    // The next step of a base gets the handle as the argument of a continuation, which is the purpose of
    // the handle. A different method is code that the analyzer does not read.
    private static string? ArgumentTo(Continuation continuation, IInvocationOperation call) =>
        call.TargetMethod.MethodKind == MethodKind.DelegateInvoke
        && continuation.Symbols.IsContinuation(type: call.TargetMethod.ContainingType)
            ? null
            : $"is given to {call.TargetMethod.Name}";

    // Stage.From keeps its function and calls it in Advance, and the function returns the handle for
    // the subsequent step. That is how a base with a base of its own makes a stage.
    private static bool IsInStageBody(Continuation continuation, IOperation operation) =>
        OperationTree.Owner(operation: operation) is IAnonymousFunctionOperation function
        && !ReferenceEquals(objA: function, objB: continuation.Lambda)
        && Receiver(function: function) is (var method, _)
        && continuation.Symbols.IsStageFrom(method: method);

    // The first lambda around the reference that can run after the continuation returns, as text for
    // the message. Null if each lambda around the reference runs before its call returns.
    private static string? Captor(Continuation continuation, IOperation reference)
    {
        for (IOperation? function = OperationTree.Owner(operation: reference);
             function is not null && !ReferenceEquals(objA: function, objB: continuation.Lambda);
             function = OperationTree.Owner(operation: function))
        {
            // The code around a local function calls it by its name, thus it is not a value that leaves.
            if (function is not IAnonymousFunctionOperation lambda)
            {
                continue;
            }

            if (Receiver(function: lambda) is not (var method, var parameter))
            {
                return "is in a lambda that is not an argument";
            }

            if (!RunsBeforeReturn(continuation: continuation, method: method, parameter: parameter))
            {
                return
                    $"is in a lambda that is given to {method.Name}, which can call it after the continuation returns";
            }
        }

        return null;
    }

    // The method and the parameter that get a lambda as an argument, or null if the lambda is not an
    // argument.
    private static (IMethodSymbol Method, IParameterSymbol Parameter)? Receiver(IOperation function) =>
        OperationTree.User(operation: function) is IDelegateCreationOperation creation
        && OperationTree.User(operation: creation) is IArgumentOperation { Parameter: { } parameter } argument
            ? argument.Parent switch
            {
                IInvocationOperation call => (call.TargetMethod, parameter),
                IObjectCreationOperation { Constructor: { } constructor } => (constructor, parameter),
                _ => null
            }
            : null;

    // A lambda that holds the handle is safe when its receiver calls it before the receiver returns.
    // Such a receiver is a parameter with InstantHandle, a continuation, the function of Construct, or
    // a member on the list of names. Stage.From keeps its function, but that function is the documented
    // place for a handle, thus this method accepts it.
    private static bool RunsBeforeReturn(Continuation continuation, IMethodSymbol method, IParameterSymbol parameter) =>
        HasInstantHandle(method: method, parameter: parameter)
        || continuation.Symbols.IsContinuation(type: parameter.Type)
        || continuation.Symbols.IsConstruct(method: method)
        || continuation.Symbols.IsStageFrom(method: method)
        || continuation.Symbols.IsInstantMember(method: method);

    // An override does not repeat the attribute of the member that it overrides, thus the search goes
    // up through the overridden members.
    private static bool HasInstantHandle(IMethodSymbol method, IParameterSymbol parameter)
    {
        for (IMethodSymbol? current = method.OriginalDefinition;
             current is not null;
             current = current.OverriddenMethod)
        {
            if (parameter.Ordinal < current.Parameters.Length
                && current.Parameters[parameter.Ordinal].GetAttributes().Any(predicate: IsInstantHandle))
            {
                return true;
            }
        }

        return false;
    }

    // The attribute by its name, because the analyzer has no reference to JetBrains.Annotations, and a
    // project can declare the attribute in its own code.
    private static bool IsInstantHandle(AttributeData attribute) =>
        attribute.AttributeClass is
        {
            Name: "InstantHandleAttribute",
            ContainingNamespace:
            {
                Name: "Annotations",
                ContainingNamespace: { Name: "JetBrains", ContainingNamespace.IsGlobalNamespace: true }
            }
        };
}
