using OkTools.Terminal;

class KeyEventTests
{
    [Test]
    public void ToString_Basics()
    {
        new KeyEvent('a').ToString().ShouldBe("a");
        new KeyEvent('a', ctrl: true).ToString().ShouldBe("<^a>");

        new KeyEvent(ConsoleKey.A).ToString().ShouldBe("<A>");
        new KeyEvent(ConsoleKey.MediaPlay).ToString().ShouldBe("<MediaPlay>");
    }

    [TestCase(false, false, false,     "a" ,    "<PageUp>")]
    [TestCase(false, false,  true,   "<^a>",   "<^PageUp>")]
    [TestCase(false,  true, false,   "<+a>",   "<+PageUp>")]
    [TestCase(false,  true,  true,  "<+^a>",  "<+^PageUp>")]
    [TestCase( true, false, false,   "<!a>",   "<!PageUp>")]
    [TestCase( true, false,  true,  "<!^a>",  "<!^PageUp>")]
    [TestCase( true,  true, false,  "<!+a>",  "<!+PageUp>")]
    [TestCase( true,  true,  true, "<!+^a>", "<!+^PageUp>")]
    public void ToString_Modifiers(bool alt, bool shift, bool ctrl, string expectedChar, string expectedKey)
    {
        new KeyEvent('a', alt, shift, ctrl).ToString().ShouldBe(expectedChar);
        new KeyEvent(ConsoleKey.PageUp, alt, shift, ctrl).ToString().ShouldBe(expectedKey);
    }
}
