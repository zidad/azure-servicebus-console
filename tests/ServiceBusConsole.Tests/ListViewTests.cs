namespace ServiceBusConsole.Tests;

public sealed class ListViewTests
{
    private static QueueInfo Queue(string name, long dlq = 0) => new() { Name = name, DeadLetterMessageCount = dlq };

    private static ListView<QueueInfo> View() => new(q => q.Name,
        new TableSort<QueueInfo>(qs => qs.OrderBy(q => q.Name)).Add("DLQ", q => q.DeadLetterMessageCount),
        (q, text) => q.Name.Contains(text, StringComparison.OrdinalIgnoreCase));

    [Test]
    public void KeepsTheSelectedRow_WhenTheOrderChanges()
    {
        var view = View();
        view.Update([Queue("a"), Queue("b"), Queue("c")]);
        view.Select(1);

        view.Update([Queue("0"), Queue("a"), Queue("b"), Queue("c")]);

        view.SelectedIndex.ShouldBe(2);
        view.Focused?.Name.ShouldBe("b");
    }

    [Test]
    public void Focus_SelectsTheRowWhenTheFirstRowsHaveIt()
    {
        var view = View();
        view.Focus("c");
        view.FocusedKey.ShouldBe("c");

        view.Update([Queue("a"), Queue("b"), Queue("c")]);

        view.Focused?.Name.ShouldBe("c");
    }

    [Test]
    public void Restore_WaitsForTheRow_WhileNothingIsSelected()
    {
        var view = View();
        view.Restore("c");
        view.FocusedKey.ShouldBeNull();

        view.Refilter([Queue("a"), Queue("b")]);
        view.Focused?.Name.ShouldBe("a");

        view.Filter.Edit();
        view.Filter.Handle(new() { Key = "c" });
        view.Refilter([Queue("a"), Queue("b"), Queue("c")]);

        view.Focused?.Name.ShouldBe("c");
    }

    [Test]
    public void Refilter_SelectsTheFirstMatch()
    {
        var view = View();
        view.Update([Queue("orders"), Queue("emails"), Queue("errors")]);
        view.Select(2);

        view.Filter.Edit();
        view.Filter.Handle(new() { Key = "e" });
        view.Filter.Handle(new() { Key = "r" });
        view.Refilter([Queue("orders"), Queue("emails"), Queue("errors")]);

        view.Items.Select(q => q.Name).ShouldBe(["errors", "orders"]);
        view.SelectedIndex.ShouldBe(0);
        view.FocusedKey.ShouldBe("errors");
    }

    [Test]
    public void Removed_SelectsTheRowInItsPlace()
    {
        var view = View();
        view.Update([Queue("a"), Queue("b"), Queue("c")]);
        view.Select(2);

        view.Removed([Queue("a"), Queue("b")]);

        view.SelectedIndex.ShouldBe(1);
        view.FocusedKey.ShouldBe("b");
    }

    [Test]
    public void WithoutMatches_IgnoresTheFilter()
    {
        var view = new ListView<QueueInfo>(q => q.Name, new TableSort<QueueInfo>(qs => qs));
        view.Filter.Edit();
        view.Filter.Handle(new() { Key = "x" });

        view.Update([Queue("a")]);

        view.Filterable.ShouldBeFalse();
        view.Items.Count.ShouldBe(1);
    }
}
