using UnityEngine;

public static class ColorUtil
{
    public static Color clearWhite => new(1, 1, 1, 0);
    public static Color GetClearColorOf(Color c) => new(c.r, c.g, c.b, 0);
}