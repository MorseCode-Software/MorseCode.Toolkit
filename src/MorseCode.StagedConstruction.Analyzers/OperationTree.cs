using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace MorseCode.StagedConstruction.Analyzers;

/// <summary>
///     Reads the tree of operations that the compiler makes for a method body.
/// </summary>
internal static class OperationTree
{
    /// <summary>
    ///     Gives <paramref name="operation" /> without the conversions around it.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns>The operation that the conversions hold, or null for null.</returns>
    public static IOperation? Unwrap(IOperation? operation)
    {
        IOperation? current = operation;

        while (current is IConversionOperation or IParenthesizedOperation)
        {
            current =
                current switch
                {
                    IConversionOperation conversion => conversion.Operand,
                    IParenthesizedOperation parenthesized => parenthesized.Operand,
                    _ => current
                };
        }

        return current;
    }

    /// <summary>
    ///     Gives the operation that uses <paramref name="operation" />, without the
    ///     conversions between the two.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns>The first parent that is not a conversion.</returns>
    public static IOperation? User(IOperation operation)
    {
        IOperation? parent = operation.Parent;

        while (parent is IConversionOperation or IParenthesizedOperation)
        {
            parent = parent.Parent;
        }

        return parent;
    }

    /// <summary>
    ///     Gives true if the two operations are the same without their conversions.
    /// </summary>
    /// <param name="first">The first operation.</param>
    /// <param name="second">The second operation.</param>
    /// <returns>True for one operation.</returns>
    public static bool Same(IOperation? first, IOperation? second) =>
        Unwrap(operation: first) is { } operation && ReferenceEquals(objA: operation, objB: Unwrap(operation: second));

    /// <summary>
    ///     Gives the lambda or the local function that holds
    ///     <paramref name="operation" />.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns>The nearest function around the operation, or null.</returns>
    public static IOperation? Owner(IOperation operation)
    {
        IOperation? parent = operation.Parent;

        while (parent is not null and not IAnonymousFunctionOperation and not ILocalFunctionOperation)
        {
            parent = parent.Parent;
        }

        return parent;
    }

    /// <summary>
    ///     Gives each value that <paramref name="value" /> can be. A conditional has
    ///     two results, a null coalescing has two sides, and a switch expression has
    ///     its arms.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The values, without conversions.</returns>
    public static IEnumerable<IOperation> Leaves(IOperation? value) =>
        Unwrap(operation: value) switch
        {
            null => [],
            IConditionalOperation conditional =>
                Leaves(value: conditional.WhenTrue).Concat(second: Leaves(value: conditional.WhenFalse)),
            ICoalesceOperation coalesce =>
                Leaves(value: coalesce.Value).Concat(second: Leaves(value: coalesce.WhenNull)),
            ISwitchExpressionOperation choice =>
                choice.Arms.SelectMany(selector: static arm => Leaves(value: arm.Value)),
            var leaf => [leaf]
        };

    /// <summary>
    ///     Gives true if the function returns a value on at least one path. A
    ///     function that only throws returns none.
    /// </summary>
    /// <param name="function">The lambda.</param>
    /// <returns>True if a return has a value that is not a throw.</returns>
    public static bool ReturnsValue(IAnonymousFunctionOperation function) =>
        function.Body.Descendants()
            .OfType<IReturnOperation>()
            .Where(predicate: result => ReferenceEquals(objA: Owner(operation: result), objB: function))
            .SelectMany(selector: static result => Leaves(value: result.ReturnedValue))
            .Any(predicate: static leaf => leaf is not IThrowOperation);

    /// <summary>
    ///     Gives the location of the member name in a call or a member access. For a
    ///     different node, gives the location of all of the syntax.
    /// </summary>
    /// <param name="operation">The operation of a call or a member access.</param>
    /// <returns>The location of the name.</returns>
    public static Location NameLocation(IOperation operation) =>
        operation.Syntax switch
        {
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } => access.Name.GetLocation(),
            MemberAccessExpressionSyntax access => access.Name.GetLocation(),
            var syntax => syntax.GetLocation()
        };
}
