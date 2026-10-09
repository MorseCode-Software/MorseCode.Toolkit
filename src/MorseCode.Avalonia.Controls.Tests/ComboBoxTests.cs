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
///     Checks that a <see cref="ComboBox" /> applies a new list and a new
///     selected item together, and writes nothing back while it applies them.
/// </summary>
public sealed class ComboBoxTests
{
    [Test]
    public Task AListAndThenItsSelectedItemInOneItemApplyTogether() =>
        TestApplication.Run(
            test: static async () =>
            {
                SelectionModel model = new(items: ["None"], selected: "None");
                using Shown shown = Shown.InCode(model: model);

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
                using Shown shown = Shown.InCode(model: model);

                model.Post(Change.Selected(value: "B"), Change.Items("None", "A", "B"));
                shown.Settle();

                await Assert.That(model.Writes).IsEmpty();
                await Assert.That(shown.Box.SelectedItem).IsEqualTo(expected: "B");
                await Assert.That(model.Selected).IsEqualTo(expected: "B");
            });

    // A binding sets the value through the property that it names. Thus, a binding that names the
    // property of the base goes to the base, and starts no batch. The docs tell you to name the property
    // of this control in code. If this test fails, a binding now finds the property of the control, and
    // the docs must change.
    [Test]
    public Task ABindingThatNamesThePropertyOfTheBaseDoesNotBatch() =>
        TestApplication.Run(
            test: static async () =>
            {
                SelectionModel model = new(items: ["None"], selected: "None");
                using Shown shown = Shown.InCodeThroughTheBase(model: model);

                model.Post(Change.Selected(value: "B"), Change.Items("None", "A", "B"));
                shown.Settle();

                await Assert.That(model.Writes)
                    .IsEquivalentTo(expected: ["null"], ordering: CollectionOrdering.Matching);

                await Assert.That(shown.Box.SelectedItem).IsNull();
            });

    [Test]
    public Task ABindingInXamlBatches() =>
        TestApplication.Run(
            test: static async () =>
            {
                SelectionModel model = new(items: ["None"], selected: "None");
                using Shown shown = Shown.InXaml(model: model);

                model.Post(Change.Selected(value: "B"), Change.Items("None", "A", "B"));
                shown.Settle();

                await Assert.That(model.Writes).IsEmpty();
                await Assert.That(shown.Box.SelectedItem).IsEqualTo(expected: "B");
            });

    [Test]
    public Task ANewListThatHasTheSelectedItemKeepsItAndWritesNothing() =>
        TestApplication.Run(
            test: static async () =>
            {
                SelectionModel model = new(items: ["None", "A", "B"], selected: "B");
                using Shown shown = Shown.InCode(model: model);

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
                using Shown shown = Shown.InCode(model: model);

                model.Post(Change.Items("None", "A", "B"));
                shown.Settle();
                model.Post(Change.Selected(value: "B"));
                shown.Settle();

                await Assert.That(model.Writes).IsEmpty();
                await Assert.That(shown.Box.SelectedItem).IsEqualTo(expected: "B");
            });

    // A usual combo box does the same.
    [Test]
    public Task ASelectedItemThatTheListDoesNotHaveSelectsNothingAndWritesNull() =>
        TestApplication.Run(
            test: static async () =>
            {
                SelectionModel model = new(items: ["None", "A", "B"], selected: "B");
                using Shown shown = Shown.InCode(model: model);

                model.Post(Change.Selected(value: "Z"));
                shown.Settle();

                await Assert.That(model.Writes)
                    .IsEquivalentTo(expected: ["null"], ordering: CollectionOrdering.Matching);

                await Assert.That(shown.Box.SelectedItem).IsNull();
                await Assert.That(model.Selected).IsNull();
            });

    [Test]
    public Task ASelectionOfTheUserGoesToTheViewModel() =>
        TestApplication.Run(
            test: static async () =>
            {
                SelectionModel model = new(items: ["None", "A", "B"], selected: "B");
                using Shown shown = Shown.InCode(model: model);

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
                using Shown shown = Shown.InCode(model: model);

                await Assert.That(shown.Box.Template).IsNotNull();
            });

    // This is the behavior that the ComboBox of this package prevents. If an update of Avalonia changes it, this
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

        private Shown(global::Avalonia.Controls.ComboBox box, SelectionModel model, Control? content = null)
        {
            this.Box = box;
            this.window = new Window { Content = content ?? box, Width = 300, Height = 200 };
            this.window.Show();
            this.Settle();
            model.Writes.Clear();
        }

        public global::Avalonia.Controls.ComboBox Box { get; }

        public static Shown InCode(SelectionModel model)
        {
            ComboBox box = new() { DataContext = model };
            box.Bind(property: ItemsControl.ItemsSourceProperty, binding: new ReflectionBinding(path: "Items"));

            box.Bind(
                property: ComboBox.SelectedItemProperty,
                binding: new ReflectionBinding(path: "Selected") { Mode = BindingMode.TwoWay });

            return new Shown(box: box, model: model);
        }

        public static Shown InCodeThroughTheBase(SelectionModel model)
        {
            ComboBox box = new() { DataContext = model };
            box.Bind(property: ItemsControl.ItemsSourceProperty, binding: new ReflectionBinding(path: "Items"));

            box.Bind(
                property: SelectingItemsControl.SelectedItemProperty,
                binding: new ReflectionBinding(path: "Selected") { Mode = BindingMode.TwoWay });

            return new Shown(box: box, model: model);
        }

        public static Shown InXaml(SelectionModel model)
        {
            ComboBoxView view = new() { DataContext = model };

            return new Shown(box: view.Box, model: model, content: view);
        }

        public static Shown Usual(SelectionModel model)
        {
            global::Avalonia.Controls.ComboBox box = new() { DataContext = model };
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
