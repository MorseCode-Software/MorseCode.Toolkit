using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MorseCode.Avalonia.Controls.Tests;

/// <summary>
///     Checks that a <see cref="BatchingComboBox" /> applies a new list and a new
///     selected item together. It writes back only the selections of the user.
/// </summary>
public sealed class BatchingComboBoxTests
{
    [Test]
    public Task AListAndThenItsSelectedItemInOneItemApplyTogether() =>
        TestApplication.Run(
            test: static async () =>
            {
                SelectionModel model = new(items: ["None"], selected: "None");
                using Shown shown = Shown.Batching(model: model);

                model.Post(Change.Items("None", "A", "B"), Change.Selected(value: "B"));
                shown.Settle();

                await Assert.That(model.Writes).IsEmpty();
                await Assert.That(shown.Box.SelectedItem).IsEqualTo(expected: "B");
                await Assert.That(model.Selected).IsEqualTo(expected: "B");
            });

    [Test]
    public Task ASelectedItemBeforeItsListInOneItemIsKept() =>
        TestApplication.Run(
            test: static async () =>
            {
                SelectionModel model = new(items: ["None"], selected: "None");
                using Shown shown = Shown.Batching(model: model);

                model.Post(Change.Selected(value: "B"), Change.Items("None", "A", "B"));
                shown.Settle();

                await Assert.That(model.Writes).IsEmpty();
                await Assert.That(shown.Box.SelectedItem).IsEqualTo(expected: "B");
                await Assert.That(model.Selected).IsEqualTo(expected: "B");
            });

    [Test]
    public Task ANewListThatHasTheSelectedItemKeepsItAndWritesNothing() =>
        TestApplication.Run(
            test: static async () =>
            {
                SelectionModel model = new(items: ["None", "A", "B"], selected: "B");
                using Shown shown = Shown.Batching(model: model);

                model.Post(Change.Items("None", "B", "A"));
                shown.Settle();

                await Assert.That(model.Writes).IsEmpty();
                await Assert.That(shown.Box.SelectedItem).IsEqualTo(expected: "B");
            });

    [Test]
    public Task AListAndThenItsSelectedItemInTwoItemsApplyWithNoWrite() =>
        TestApplication.Run(
            test: static async () =>
            {
                SelectionModel model = new(items: ["None"], selected: "None");
                using Shown shown = Shown.Batching(model: model);

                model.Post(Change.Items("None", "A", "B"));
                shown.Settle();
                model.Post(Change.Selected(value: "B"));
                shown.Settle();

                await Assert.That(model.Writes).IsEmpty();
                await Assert.That(shown.Box.SelectedItem).IsEqualTo(expected: "B");
            });

    [Test]
    public Task ASelectedItemThatTheListDoesNotHaveSelectsNothingAndWritesNothing() =>
        TestApplication.Run(
            test: static async () =>
            {
                SelectionModel model = new(items: ["None", "A", "B"], selected: "B");
                using Shown shown = Shown.Batching(model: model);

                model.Post(Change.Selected(value: "Z"));
                shown.Settle();

                await Assert.That(model.Writes).IsEmpty();
                await Assert.That(shown.Box.SelectedItem).IsNull();
                await Assert.That(model.Selected).IsEqualTo(expected: "Z");
            });

    [Test]
    public Task ASelectionOfTheUserGoesToTheViewModel() =>
        TestApplication.Run(
            test: static async () =>
            {
                SelectionModel model = new(items: ["None", "A", "B"], selected: "B");
                using Shown shown = Shown.Batching(model: model);

                shown.Box.SelectedIndex = 1;
                shown.Settle();
                shown.Box.SelectedIndex = 0;
                shown.Settle();

                await Assert.That(model.Writes)
                    .IsEquivalentTo(expected: ["A", "None"], ordering: CollectionOrdering.Matching);

                await Assert.That(model.Selected).IsEqualTo(expected: "None");
            });

    [Test]
    public Task TheControlHasTheTemplateOfAComboBox() =>
        TestApplication.Run(
            test: static async () =>
            {
                SelectionModel model = new(items: ["None"], selected: "None");
                using Shown shown = Shown.Batching(model: model);

                await Assert.That(shown.Box.Template).IsNotNull();
            });

    // This is the behavior that BatchingComboBox prevents. If an update of Avalonia changes it, this
    // test fails and tells you to look at the control again.
    [Test]
    public Task AUsualComboBoxLosesASelectedItemThatArrivesBeforeItsList() =>
        TestApplication.Run(
            test: static async () =>
            {
                SelectionModel model = new(items: ["None"], selected: "None");
                using Shown shown = Shown.Usual(model: model);

                model.Post(Change.Selected(value: "B"), Change.Items("None", "A", "B"));
                shown.Settle();

                await Assert.That(model.Writes)
                    .IsEquivalentTo(expected: ["null"], ordering: CollectionOrdering.Matching);

                await Assert.That(shown.Box.SelectedItem).IsNull();
            });

    // A combo box in a window. It binds to a model and has the first values of the model.
    private sealed class Shown : System.IDisposable
    {
        private readonly Window window;

        private Shown(ComboBox box, SelectionModel model)
        {
            this.Box = box;
            this.window = new Window { Content = box, Width = 300, Height = 200 };
            this.window.Show();
            this.Settle();
            model.Writes.Clear();
        }

        public ComboBox Box { get; }

        public static Shown Batching(SelectionModel model)
        {
            BatchingComboBox box = new() { DataContext = model };
            box.Bind(property: ItemsControl.ItemsSourceProperty, binding: new ReflectionBinding(path: "Items"));

            box.Bind(
                property: BatchingComboBox.BoundSelectedItemProperty,
                binding: new ReflectionBinding(path: "Selected") { Mode = BindingMode.TwoWay });

            return new Shown(box: box, model: model);
        }

        public static Shown Usual(SelectionModel model)
        {
            ComboBox box = new() { DataContext = model };
            box.Bind(property: ItemsControl.ItemsSourceProperty, binding: new ReflectionBinding(path: "Items"));

            box.Bind(
                property: SelectingItemsControl.SelectedItemProperty,
                binding: new ReflectionBinding(path: "Selected") { Mode = BindingMode.TwoWay });

            return new Shown(box: box, model: model);
        }

        // Runs the work of the dispatcher, which includes the posted changes and the end of a batch.
        public void Settle() => this.window.Dispatcher.RunJobs();

        public void Dispose() => this.window.Close();
    }
}
