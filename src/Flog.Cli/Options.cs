using System.Drawing;

class Options
{
    public bool FollowByDefault = true;
    public WrapType WrapByDefault = WrapType.None;

    public int TabWidth = 4;
    public int HorizScrollSize = 10;

    public Color StatusColor = Color.Goldenrod;

    public bool VirtualSpaceEnabled = true;
    public string VirtualSpaceText = "~";
    public Color VirtualSpaceColor = Color.FromArgb(100, 100, 100);

    public bool TruncateMarkerEnabled = true;
    public string TruncateMarkerText = "\u2026"; // ...
    public Color TruncateMarkerColor = Color.CornflowerBlue;
}
