using System.Collections.Generic;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.StagedConstruction.Tests;

// Each test writes its bases as local functions, so the bases can write to the list that the test examines.
public sealed class StagedConstructionTests
{
    [Test]
    public async Task OneStageGivesTheValuesOfTheBaseToTheConstructorUnchanged()
    {
        List<string> log = [];
        List<AnimalValues> made = [];

        Dog dog = CreateAnimal(
            name: "Rex",
            continuation: (output, construct) =>
            {
                log.Add(item: "subclass");

                return construct.Construct(
                    constructor: animal =>
                    {
                        log.Add(item: "constructor");

                        return new Dog(animal: animal, bark: $"{output}!");
                    });
            });

        await Assert.That(made.Count).IsEqualTo(expected: 1);
        await Assert.That(dog.Animal).IsSameReferenceAs(expected: made[0]);
        await Assert.That(dog.Animal.Name).IsEqualTo(expected: "Rex");
        await Assert.That(dog.Bark).IsEqualTo(expected: "REX!");
        await Assert.That(log).IsEquivalentTo(expected: ["base", "subclass", "constructor"], ordering: CollectionOrdering.Matching);

        return;

        TResult CreateAnimal<TResult>(
            string name,
            StageContinuation<string, IConstruct<AnimalValues>, TResult> continuation)
        {
            log.Add(item: "base");
            AnimalValues values = new(Name: name);
            made.Add(item: values);

            return continuation(output: name.ToUpperInvariant(), next: Construct.From(values: values));
        }
    }

    // Three stages: the type of the continuation of CreateMachine shows each stage in its sequence.
    // Each stage gets a value from the subclass and gives a value back.
    [Test]
    public async Task StagesAndTheSubclassRunInTurnAndPassValuesBothWays()
    {
        List<string> log = [];

        Robot robot = CreateMachine(
            seed: 3,
            continuation: (first, second) =>
            {
                log.Add(item: $"subclass 1 got {first}");

                return second.Advance(
                    input: first + 1,
                    continuation: (secondOutput, third) =>
                    {
                        log.Add(item: $"subclass 2 got {secondOutput}");

                        return third.Advance(
                            input: $"label {secondOutput}",
                            continuation: (thirdOutput, construct) =>
                            {
                                log.Add(item: $"subclass 3 got {thirdOutput}");

                                return construct.Construct(
                                    constructor: machine =>
                                    {
                                        log.Add(item: "constructor");

                                        return new Robot(machine: machine, summary: thirdOutput);
                                    });
                            });
                    });
            });

        await Assert.That(robot.Machine.Seed).IsEqualTo(expected: 3);
        await Assert.That(robot.Machine.Adjusted).IsEqualTo(expected: 7);
        await Assert.That(robot.Machine.Label).IsEqualTo(expected: "label 14");
        await Assert.That(robot.Summary).IsEqualTo(expected: "LABEL 14");

        await Assert
            .That(log)
            .IsEquivalentTo(
                expected:
                [
                    "base 1 got 3", "subclass 1 got 6",
                    "base 2 got 7", "subclass 2 got 14",
                    "base 3 got label 14", "subclass 3 got LABEL 14",
                    "constructor"
                ],
                ordering: CollectionOrdering.Matching);

        return;

        TResult CreateMachine<TResult>(
            int seed,
            StageContinuation<int, IStage<int, int, IStage<string, string, IConstruct<MachineValues>>>, TResult> continuation)
        {
            log.Add(item: $"base 1 got {seed}");

            return continuation(
                output: seed * 2,
                next: Stage.From(
                    body: (int adjusted) =>
                    {
                        log.Add(item: $"base 2 got {adjusted}");

                        return (
                            Output: adjusted * 2,
                            Next: Stage.From(
                                body: (string label) =>
                                {
                                    log.Add(item: $"base 3 got {label}");

                                    return (
                                        Output: label.ToUpperInvariant(),
                                        Next: Construct.From(values: new MachineValues(Seed: seed, Adjusted: adjusted, Label: label)));
                                }));
                    }));
        }
    }

    [Test]
    public async Task BaseWithABaseOfItsOwnAddsItsValuesWithSelect()
    {
        List<string> log = [];

        Cat cat = CreateMammal(
            name: "Tom",
            continuation: (output, construct) =>
            {
                log.Add(item: "subclass");

                return construct.Construct(
                    constructor: mammal =>
                    {
                        log.Add(item: "constructor");

                        return new Cat(mammal: mammal, greeting: $"{output} purrs");
                    });
            });

        await Assert.That(cat.Mammal.Animal.Name).IsEqualTo(expected: "Tom");
        await Assert.That(cat.Mammal.Furry).IsTrue();
        await Assert.That(cat.Greeting).IsEqualTo(expected: "TOM purrs");

        // The selector runs when the subclass calls Construct, and not when the mammal base calls Select.
        await Assert
            .That(log)
            .IsEquivalentTo(
                expected: ["animal base", "mammal base", "subclass", "selector", "constructor"],
                ordering: CollectionOrdering.Matching);

        return;

        TResult CreateAnimal<TResult>(
            string name,
            StageContinuation<string, IConstruct<AnimalValues>, TResult> continuation)
        {
            log.Add(item: "animal base");

            return continuation(output: name.ToUpperInvariant(), next: Construct.From(values: new AnimalValues(Name: name)));
        }

        TResult CreateMammal<TResult>(
            string name,
            StageContinuation<string, IConstruct<MammalValues>, TResult> continuation) =>
            CreateAnimal(
                name: name,
                continuation: (output, construct) =>
                {
                    log.Add(item: "mammal base");

                    return continuation(
                        output: output,
                        next: construct.Select(
                            selector: animal =>
                            {
                                log.Add(item: "selector");

                                return new MammalValues(Animal: animal, Furry: true);
                            }));
                });
    }

    // A base can do work before and after it calls the continuation of the subclass. That work surrounds
    // all of the subsequent steps. A SodaFlow base does this with a transaction.
    [Test]
    public async Task BaseCanDoWorkBeforeAndAfterAllOfTheSubsequentSteps()
    {
        List<string> log = [];

        Dog dog = CreateScopedAnimal(
            name: "Rex",
            continuation: (output, construct) =>
            {
                log.Add(item: "subclass");

                return construct.Construct(
                    constructor: animal =>
                    {
                        log.Add(item: "constructor");

                        return new Dog(animal: animal, bark: output);
                    });
            });

        await Assert.That(dog.Animal.Name).IsEqualTo(expected: "Rex");
        await Assert.That(log).IsEquivalentTo(expected: ["open", "subclass", "constructor", "close"], ordering: CollectionOrdering.Matching);

        return;

        TResult CreateScopedAnimal<TResult>(
            string name,
            StageContinuation<string, IConstruct<AnimalValues>, TResult> continuation)
        {
            log.Add(item: "open");

            try
            {
                return continuation(output: name, next: Construct.From(values: new AnimalValues(Name: name)));
            }
            finally
            {
                log.Add(item: "close");
            }
        }
    }

    // The problem that staged construction removes. A base constructor that calls a virtual member
    // gets a field of the subclass before the constructor of the subclass sets it.
    [Test]
    public async Task ConstructorChainGivesTheBaseAFieldThatIsNotSet()
    {
        NamedByConstructor named = new(name: "Rex");

        await Assert.That(named.NameSeenByBase).IsNull();
    }

    // The same base in staged construction. The base gets the name as a value from the subclass, thus
    // the base cannot get it before the subclass makes it.
    [Test]
    public async Task StagedConstructionGivesTheBaseOnlyValuesThatAreSet()
    {
        NamedByStages named = CreateNamedBase(
            name: "Rex",
            continuation: static (_, construct) =>
                construct.Construct(constructor: static nameSeenByBase => new NamedByStages(nameSeenByBase: nameSeenByBase)));

        await Assert.That(named.NameSeenByBase).IsEqualTo(expected: "Rex");

        return;

        static TResult CreateNamedBase<TResult>(
            string name,
            StageContinuation<string, IConstruct<string>, TResult> continuation) =>
            continuation(output: name, next: Construct.From(values: name));
    }

    private sealed record AnimalValues(string Name);

    private sealed record MammalValues(AnimalValues Animal, bool Furry);

    private sealed record MachineValues(int Seed, int Adjusted, string Label);

    private sealed class Dog(AnimalValues animal, string bark)
    {
        public AnimalValues Animal { get; } = animal;

        public string Bark { get; } = bark;
    }

    private sealed class Cat(MammalValues mammal, string greeting)
    {
        public MammalValues Mammal { get; } = mammal;

        public string Greeting { get; } = greeting;
    }

    private sealed class Robot(MachineValues machine, string summary)
    {
        public MachineValues Machine { get; } = machine;

        public string Summary { get; } = summary;
    }

    private sealed class NamedByStages(string nameSeenByBase)
    {
        public string NameSeenByBase { get; } = nameSeenByBase;
    }

    private abstract class ConstructorBase
    {
        // ReSharper disable once VirtualMemberCallInConstructor - This call is the defect that the test shows.
        protected ConstructorBase() => this.NameSeenByBase = this.Name;

        public string? NameSeenByBase { get; }

        protected abstract string Name { get; }
    }

    private sealed class NamedByConstructor : ConstructorBase
    {
        // ReSharper disable once ConvertToPrimaryConstructor - A primary constructor sets Name before the base constructor runs, and so removes the defect that the test shows.
        public NamedByConstructor(string name) => this.Name = name;

        protected override string Name { get; }
    }
}
