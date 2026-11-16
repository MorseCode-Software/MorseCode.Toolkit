using System.Reflection;
using BenchmarkDotNet.Running;

namespace MorseCode.StagedConstruction.Analyzers.Benchmarks;

/// <summary>
///     The entry point of the benchmarks.
/// </summary>
/// <remarks>
///     <para>
///         A switcher, as in the benchmarks of SodaFlow. Thus, a new class is a new
///         benchmark, and this file does not name it.
///     </para>
///     <para>
///         Run all of them with
///         <c>
///             dotnet run -c Release --project
///             src/MorseCode.StagedConstruction.Analyzers.Benchmarks -- --filter *
///         </c>
///         . BenchmarkDotNet does not measure a debug build.
///     </para>
/// </remarks>
// ReSharper disable once MemberCanBeFileLocal - The entry point, which the runtime finds, and not a caller in this file.
internal static class Program
{
    private static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(assembly: Assembly.GetExecutingAssembly()).Run(args: args);
}
