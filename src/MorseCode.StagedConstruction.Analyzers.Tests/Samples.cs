namespace MorseCode.StagedConstruction.Analyzers.Tests;

/// <summary>
///     The code that each sample of <see cref="ContinuationAnalyzerTests" /> starts
///     with, and the frame around one continuation.
/// </summary>
internal static class Samples
{
    // A base with one stage, a base with a stage before its handle, and the helpers that the samples
    // call. Base.Create reads Value and calls Constructor.From, as each base does. Neither is in a
    // continuation, thus no rule applies to them.
    private const string Preamble =
        """
        using System;
        using JetBrains.Annotations;
        using MorseCode.StagedConstruction;
        using SodaFlow;

        public sealed class Values;

        public sealed class Thing(in Values values)
        {
            public Values Values { get; } = values;
        }

        public static class Base
        {
            public static TResult Create<TResult>(StageContinuation<string, Constructor<Values>, TResult> continuation) =>
                continuation(output: "output", next: Constructor.From(values: new Values())).Value;
        }

        public static class Staged
        {
            public static TResult Create<TResult>(
                StageContinuation<string, Stage<int, string, Constructor<Values>>, TResult> continuation) =>
                continuation(
                        output: "output",
                        next: Stage.From<int, string, Constructor<Values>>(
                            body: static input => (Output: $"{input}", Next: Constructor.From(values: new Values()))))
                    .Value;
        }

        public static class Done
        {
            public static Constructed<T> Of<T>(T value) => Constructor.From(values: value).Construct(constructor: static made => made);
        }

        public static class Helpers
        {
            public static Constructor<Values>? Kept;

            public static T Now<T>([InstantHandle] Func<T> f) => f();

            public static T Later<T>(Func<T> f) => f();

            public static Constructed<Thing> Finish(Constructor<Values> handle) =>
                handle.Construct(constructor: static values => new Thing(values: values));
        }

        public sealed class Holder(in Constructor<Values> handle)
        {
            public Constructor<Values> Handle { get; } = handle;
        }

        public sealed class OwnStage(in Constructor<Values> handle) : Stage<int, string, Constructor<Values>>
        {
            private readonly Constructor<Values> handle = handle;

            protected override Constructed<TResult> AdvanceCore<TResult>(
                int input,
                StageContinuation<string, Constructor<Values>, TResult> continuation) =>
                continuation(output: $"{input}", next: this.handle);
        }

        """;

    /// <summary>
    ///     The usual function for <c>Construct</c>, which reads the values of the base.
    /// </summary>
    public const string Make = "constructor: static values => new Thing(values: values)";

    /// <summary>
    ///     A <c>Constructed</c> from a handle that the code makes with
    ///     <c>Constructor.From</c>.
    /// </summary>
    public const string Foreign =
        "Constructor.From(values: new Thing(values: new Values())).Construct(constructor: static thing => thing)";

    /// <summary>
    ///     Gives a sample in which <paramref name="continuation" /> is the
    ///     continuation of a base whose handle is a <c>Constructor</c>.
    /// </summary>
    /// <param name="continuation">The lambda.</param>
    /// <returns>The sample.</returns>
    public static string OfBase(string continuation) =>
        Preamble
        + $$"""
            public static class Use
            {
                public static bool Flag;

                public static Thing Go() => Base.Create(continuation: {{continuation}});
            }
            """;

    /// <summary>
    ///     Gives a sample in which <paramref name="continuation" /> is the
    ///     continuation of a base whose handle is a <c>Stage</c>.
    /// </summary>
    /// <param name="continuation">The lambda.</param>
    /// <returns>The sample.</returns>
    public static string OfStaged(string continuation) =>
        Preamble
        + $$"""
            public static class Use
            {
                public static Thing Go() => Staged.Create(continuation: {{continuation}});
            }
            """;

    /// <summary>
    ///     Gives a sample that is the preamble and <paramref name="code" />.
    /// </summary>
    /// <param name="code">The code after the preamble.</param>
    /// <returns>The sample.</returns>
    public static string With(string code) => Preamble + code;
}
