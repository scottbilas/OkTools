class Int2Tests
{
    [Test]
    public void Basics()
    {
        void Check(Int2 i, int x, int y)
        {
            // direct
            i.X.ShouldBe(x);
            i.Y.ShouldBe(y);

            // indexed
            i[0].ShouldBe(x);
            i[1].ShouldBe(y);

            // decomposed
            var (ix, iy) = i;
            ix.ShouldBe(x);
            iy.ShouldBe(y);
        }

        // default
        var idef = new Int2();
        Check(idef, 0, 0);

        // different components
        var idiff = new Int2(1, 2);
        Check(idiff, 1, 2);

        // duplicated component
        var idup = new Int2(3);
        Check(idup, 3, 3);

        // tuple
        var itup = new Int2((4, 5));
        Check(itup, 4, 5);

        // assign direct
        itup.X = 6;
        itup.Y = 7;
        Check(itup, 6, 7);

        // assign tuple
        itup = (8, 9);
        Check(itup, 8, 9);
    }

    [Test]
    public void Indexer_WithOutOfRange_Throws()
    {
        var i = new Int2();
        Should.Throw<IndexOutOfRangeException>(() => _ = i[-1]);
        Should.Throw<IndexOutOfRangeException>(() => _ = i[-99]);
        Should.Throw<IndexOutOfRangeException>(() => _ = i[2]);
        Should.Throw<IndexOutOfRangeException>(() => _ = i[50]);
    }
}
