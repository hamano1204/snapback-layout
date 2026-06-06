using System;
using System.Windows.Forms;

namespace snapback_layout;

public class HotkeyWindow : NativeWindow, IDisposable
{
    private readonly Action _onSave;
    private readonly Action _onRestore;

    public bool IsRegisteredSuccessfully { get; private set; } = true;

    public HotkeyWindow(Action onSave, Action onRestore, Settings settings)
    {
        _onSave = onSave;
        _onRestore = onRestore;
        
        // Create handle for the window to receive messages
        var cp = new CreateParams();
        this.CreateHandle(cp);

        UpdateHotkeys(settings.SaveHotkeyModifiers, settings.SaveHotkeyKey, settings.RestoreHotkeyModifiers, settings.RestoreHotkeyKey);
    }

    public bool UpdateHotkeys(uint saveModifiers, uint saveKey, uint restoreModifiers, uint restoreKey)
    {
        Win32.UnregisterHotKey(this.Handle, 1);
        Win32.UnregisterHotKey(this.Handle, 2);

        bool saveOk = true;
        bool restoreOk = true;

        if (saveKey != 0)
        {
            saveOk = Win32.RegisterHotKey(this.Handle, 1, saveModifiers, saveKey);
        }
        if (restoreKey != 0)
        {
            restoreOk = Win32.RegisterHotKey(this.Handle, 2, restoreModifiers, restoreKey);
        }

        IsRegisteredSuccessfully = saveOk && restoreOk;
        return IsRegisteredSuccessfully;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Win32.WM_HOTKEY)
        {
            int id = m.WParam.ToInt32();
            if (id == 1) _onSave();
            else if (id == 2) _onRestore();
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        Win32.UnregisterHotKey(this.Handle, 1);
        Win32.UnregisterHotKey(this.Handle, 2);
        this.DestroyHandle();
        GC.SuppressFinalize(this);
    }
}
