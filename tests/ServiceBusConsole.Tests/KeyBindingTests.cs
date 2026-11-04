using Microsoft.AspNetCore.Components.Web;

namespace ServiceBusConsole.Tests;

public sealed class KeyBindingTests
{
    private static KeyboardEventArgs Key(string key, bool ctrl = false) => new() { Key = key, CtrlKey = ctrl };

    [Test]
    public void Letter_MatchesItsKeyOrAnAlias_WithoutCtrl()
    {
        var delete = new KeyBinding("D", "Delete", () => { }) { Aliases = ["F9"] };

        delete.Matches(Key("D")).ShouldBeTrue();
        delete.Matches(Key("F9")).ShouldBeTrue();
        delete.Matches(Key("d")).ShouldBeFalse();
        delete.Matches(Key("D", ctrl: true)).ShouldBeFalse();
    }

    [Test]
    public void CtrlBinding_MatchesCtrlInEitherCase_AndItsAliasWithoutCtrl()
    {
        var refresh = new KeyBinding("r", "Refresh", () => { }) { Ctrl = true, Aliases = ["F5"] };

        refresh.Matches(Key("R", ctrl: true)).ShouldBeTrue();
        refresh.Matches(Key("r", ctrl: true)).ShouldBeTrue();
        refresh.Matches(Key("F5")).ShouldBeTrue();
        refresh.Matches(Key("r")).ShouldBeFalse();
        refresh.Display.ShouldBe("^r");
    }

    [Test]
    public void Dispatch_RunsTheFirstMatch_OrReportsNone()
    {
        var ran = "";
        KeyBinding[] keys = [new("p", "Peek", () => ran += "p"), new("Escape", "Back", () => ran += "esc")];

        KeyBinding.Dispatch(keys, Key("Escape")).ShouldBeTrue();
        KeyBinding.Dispatch(keys, Key("x")).ShouldBeFalse();
        ran.ShouldBe("esc");
        keys[1].Display.ShouldBe("esc");
    }

    [Test]
    public void Filter_SlashStartsEditing_TypingChangesIt_EnterKeepsIt_EscapeClearsIt()
    {
        var filter = new ListFilter();

        filter.Handle(Key("p")).ShouldBe(FilterKey.Ignored);
        filter.Handle(Key("/")).ShouldBe(FilterKey.Handled);
        filter.Handle(Key("p")).ShouldBe(FilterKey.Changed);
        filter.Handle(Key("-")).ShouldBe(FilterKey.Changed);
        filter.Handle(Key("Backspace")).ShouldBe(FilterKey.Changed);
        filter.Handle(Key("F5")).ShouldBe(FilterKey.Ignored);
        filter.Handle(Key("Enter")).ShouldBe(FilterKey.Handled);
        filter.Text.ShouldBe("p");
        filter.Editing.ShouldBeFalse();

        filter.Handle(Key("x")).ShouldBe(FilterKey.Ignored);
        filter.Handle(Key("Escape")).ShouldBe(FilterKey.Changed);
        filter.Text.ShouldBeNull();
        filter.Handle(Key("Escape")).ShouldBe(FilterKey.Ignored);
    }

    [Test]
    public void TypeToFilter_TakesLettersWithoutSlash()
    {
        var filter = new ListFilter { TypeToFilter = true };

        filter.Handle(Key("l")).ShouldBe(FilterKey.Changed);
        filter.Text.ShouldBe("l");
        filter.Handle(Key("Escape")).ShouldBe(FilterKey.Changed);
        filter.Handle(Key("Escape")).ShouldBe(FilterKey.Ignored);
    }

    [Test]
    public void RelativeTime_InTheLargestWholeUnit()
    {
        var now = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

        RelativeTime.Ago(now, now).ShouldBe("now");
        RelativeTime.Ago(now.AddSeconds(-42), now).ShouldBe("42 s ago");
        RelativeTime.Ago(now.AddMinutes(-12), now).ShouldBe("12 min ago");
        RelativeTime.Ago(now.AddHours(-3), now).ShouldBe("3 h ago");
        RelativeTime.Ago(now.AddDays(-9), now).ShouldBe("9 d ago");
    }
}
