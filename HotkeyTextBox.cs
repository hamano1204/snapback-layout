using System;
using System.Drawing;
using System.Windows.Forms;

namespace snapback_layout;

public class HotkeyTextBox : TextBox
{
    public uint HotkeyModifiers { get; private set; }
    public Keys HotkeyKey { get; private set; }

    public HotkeyTextBox()
    {
        this.ReadOnly = true;
        this.Cursor = Cursors.Hand;
        this.BackColor = Color.White;
        this.Text = "None";
    }

    public void SetHotkey(uint modifiers, Keys key)
    {
        HotkeyModifiers = modifiers;
        HotkeyKey = key;
        UpdateText();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        this.BackColor = Color.FromArgb(245, 243, 255); // Light violet
        this.Text = "Press keys...";
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        this.BackColor = Color.White;
        UpdateText();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.SuppressKeyPress = true; // Stop default textbox behavior
        e.Handled = true;

        Keys key = e.KeyCode;
        
        // Check physical state of Left/Right Windows keys via Win32 API
        bool winDown = (Win32.GetKeyState(Win32.VK_LWIN) & 0x8000) != 0 || (Win32.GetKeyState(Win32.VK_RWIN) & 0x8000) != 0;

        // If it's only modifier keys, don't finalize yet
        if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu || key == Keys.LWin || key == Keys.RWin)
        {
            return;
        }

        if (key == Keys.Back || key == Keys.Escape)
        {
            HotkeyModifiers = 0;
            HotkeyKey = Keys.None;
            UpdateText();
            this.FindForm()?.Focus();
            return;
        }

        uint modifiers = 0;
        if (e.Control) modifiers |= Win32.MOD_CONTROL;
        if (e.Alt) modifiers |= Win32.MOD_ALT;
        if (e.Shift) modifiers |= Win32.MOD_SHIFT;
        if (winDown) modifiers |= Win32.MOD_WIN;

        HotkeyModifiers = modifiers;
        HotkeyKey = key;
        UpdateText();
        this.FindForm()?.Focus();
    }

    private void UpdateText()
    {
        this.Text = HotkeyHelper.Format(HotkeyModifiers, HotkeyKey);
    }
}
