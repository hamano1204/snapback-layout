using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace snapback_layout;

public static class HotkeyHelper
{
    public static string Format(uint modifiers, Keys key)
    {
        if (key == Keys.None) return "None";

        var parts = new List<string>();
        if ((modifiers & Win32.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifiers & Win32.MOD_ALT) != 0) parts.Add("Alt");
        if ((modifiers & Win32.MOD_SHIFT) != 0) parts.Add("Shift");
        if ((modifiers & Win32.MOD_WIN) != 0) parts.Add("Win");

        parts.Add(key.ToString());
        return string.Join(" + ", parts);
    }
}
