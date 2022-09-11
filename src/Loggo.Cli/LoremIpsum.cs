static class LoremIpsum
{
    public static void Fill(ref CharSpanBuilder csb, Random? rng = null)
    {
        rng ??= Random.Shared;

        for (;;)
        {
            var firstWord = s_words[rng.Next(s_words.Length)];
            if (!csb.TryAppend(char.ToUpper(firstWord[0])))
                return;
            if (!csb.TryAppend(firstWord[1..]))
                return;

            for (var i = rng.Next(5, 15); i >= 0; --i)
            {
                if (!csb.TryAppend(' '))
                    return;
                if (!csb.TryAppend(s_words[rng.Next(s_words.Length)]))
                    return;
            }

            if (!csb.TryAppend(". "))
                return;
        }
    }

    // ReSharper disable StringLiteralTypo
    static readonly string[] s_words =
    {
        "accumsan", "accusam", "accusam", "ad", "adipiscing", "adipiscing", "aliquam", "aliquam", "aliquip", "aliquyam",
        "amet", "amet", "assum", "at", "at", "augue", "autem", "blandit", "clita", "clita", "commodo", "congue",
        "consectetuer", "consequat", "consetetur", "cum", "delenit", "delenit", "diam", "diam", "dignissim", "dolor",
        "dolore", "dolores", "dolores", "doming", "duis", "duis", "duo", "ea", "ea", "eirmod", "eirmod", "eleifend",
        "elit", "elitr", "enim", "eos", "erat", "eros", "esse", "est", "et", "et", "eu", "euismod", "eum", "ex",
        "exerci", "facer", "facilisi", "facilisis", "feugait", "feugiat", "gubergren", "hendrerit", "id", "illum",
        "imperdiet", "in", "invidunt", "ipsum", "iriure", "iusto", "justo", "kasd", "labore", "labore", "laoreet",
        "laoreet", "liber", "lobortis", "lorem", "luptatum", "luptatum", "magna", "mazim", "minim", "molestie",
        "molestie", "nam", "nibh", "nihil", "nisl", "no", "no", "nobis", "nonummy", "nonumy", "nostrud", "nulla", "odio",
        "option", "placerat", "possim", "praesent", "qui", "quis", "quis", "quod", "rebum", "sadipscing", "sanctus",
        "sea", "sea", "sed", "sed", "sit", "sit", "soluta", "stet", "suscipit", "takimata", "takimata", "tation",
        "tation", "te", "tempor", "tempor", "tincidunt", "ullamcorper", "ut", "ut", "vel", "velit", "veniam",
        "vero", "voluptua", "voluptua", "volutpat", "vulputate", "vulputate", "wisi", "zzril"
    };
    // ReSharper restore StringLiteralTypo
}
