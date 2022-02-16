using DocoptNet;

static class Extensions
{
    public static IEnumerable<string> AsStrings(this ValueObject @this) =>
        @this.AsList.Cast<string>();


}
