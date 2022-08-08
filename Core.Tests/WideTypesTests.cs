class Int2Tests
{
    [Test]
    public void Basics()
    {
        void Check(Int2 i, int x, int y)
        {
            i.X.ShouldBe(x);
            i[0].ShouldBe(x);
            i.Y.ShouldBe(y);
            i[1].ShouldBe(y);

            var (ix, iy) = i;
            ix.ShouldBe(x);
            iy.ShouldBe(y);
        }

        var i0 = new Int2(1, 2);
        Check(i0, 1, 2);

        var i1 = new Int2(3);
        Check(i1, 3, 3);

        var i2 = new Int2((4, 5));
        Check(i2, 4, 5);
        i2.X = 6;
        i2.Y = 7;
        Check(i2, 6, 7);
    }
}
