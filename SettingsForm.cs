using System;
using System.Drawing;
using System.Windows.Forms;

namespace snapback_layout;

public class SettingsForm : Form
{
    private readonly CheckBox _chkAutoSave;
    private readonly ComboBox _cmbInterval;
    private readonly ComboBox _cmbHistoryLimit;
    private readonly CheckBox _chkDpiCorrection;
    private readonly CheckBox _chkStartWithWindows;
    private readonly HotkeyTextBox _txtSaveHotkey;
    private readonly HotkeyTextBox _txtRestoreHotkey;
    private readonly Button _btnOk;
    private readonly Button _btnCancel;

    public Settings UpdatedSettings { get; private set; }

    public SettingsForm(Settings currentSettings)
    {
        // Keep a copy as the initial state
        UpdatedSettings = new Settings
        {
            AutoSaveEnabled = currentSettings.AutoSaveEnabled,
            AutoSaveIntervalMinutes = currentSettings.AutoSaveIntervalMinutes,
            HistoryLimitMinutes = currentSettings.HistoryLimitMinutes,
            DpiCorrectionEnabled = currentSettings.DpiCorrectionEnabled,
            StartWithWindows = currentSettings.StartWithWindows,
            SaveHotkeyModifiers = currentSettings.SaveHotkeyModifiers,
            SaveHotkeyKey = currentSettings.SaveHotkeyKey,
            RestoreHotkeyModifiers = currentSettings.RestoreHotkeyModifiers,
            RestoreHotkeyKey = currentSettings.RestoreHotkeyKey
        };

        // Form setup
        this.Text = "snapback-layout Settings";
        this.Size = new Size(400, 480);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        this.BackColor = Color.White;

        // GroupBox 1: Layout Backup & Restore
        var grpBackup = new GroupBox
        {
            Text = "Layout Backup & Restore",
            Location = new Point(20, 15),
            Size = new Size(345, 175),
            FlatStyle = FlatStyle.Flat
        };

        // Auto-save checkbox
        _chkAutoSave = new CheckBox
        {
            Text = "Enable Auto-Save",
            Location = new Point(15, 25),
            Size = new Size(300, 24),
            Checked = UpdatedSettings.AutoSaveEnabled
        };

        // Interval label & combobox
        var lblInterval = new Label
        {
            Text = "Auto-Save Interval:",
            Location = new Point(35, 60),
            Size = new Size(130, 20)
        };

        _cmbInterval = new ComboBox
        {
            Location = new Point(185, 57),
            Size = new Size(135, 24),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Enabled = UpdatedSettings.AutoSaveEnabled
        };
        _cmbInterval.Items.AddRange(new object[] { "1 minute", "5 minutes", "10 minutes", "15 minutes", "30 minutes" });
        SelectIntervalIndex(UpdatedSettings.AutoSaveIntervalMinutes);

        _chkAutoSave.CheckedChanged += (s, e) =>
        {
            _cmbInterval.Enabled = _chkAutoSave.Checked;
        };

        // History limit label & combobox
        var lblHistoryLimit = new Label
        {
            Text = "History Limit:",
            Location = new Point(35, 95),
            Size = new Size(130, 20)
        };

        _cmbHistoryLimit = new ComboBox
        {
            Location = new Point(185, 92),
            Size = new Size(135, 24),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbHistoryLimit.Items.AddRange(new object[] { "15 minutes", "30 minutes", "60 minutes", "120 minutes", "240 minutes" });
        SelectHistoryLimitIndex(UpdatedSettings.HistoryLimitMinutes);

        // DPI Correction checkbox
        _chkDpiCorrection = new CheckBox
        {
            Text = "Enable DPI Correction (Recommended)",
            Location = new Point(15, 135),
            Size = new Size(300, 24),
            Checked = UpdatedSettings.DpiCorrectionEnabled
        };

        grpBackup.Controls.AddRange(new Control[] {
            _chkAutoSave,
            lblInterval,
            _cmbInterval,
            lblHistoryLimit,
            _cmbHistoryLimit,
            _chkDpiCorrection
        });

        // GroupBox 2: Keyboard Shortcuts
        var grpHotkeys = new GroupBox
        {
            Text = "Keyboard Shortcuts",
            Location = new Point(20, 205),
            Size = new Size(345, 115),
            FlatStyle = FlatStyle.Flat
        };

        // Hotkey controls
        var lblSaveHotkey = new Label
        {
            Text = "Save Layout:",
            Location = new Point(15, 30),
            Size = new Size(140, 20)
        };

        _txtSaveHotkey = new HotkeyTextBox
        {
            Location = new Point(185, 27),
            Size = new Size(135, 24)
        };
        _txtSaveHotkey.SetHotkey(UpdatedSettings.SaveHotkeyModifiers, (Keys)UpdatedSettings.SaveHotkeyKey);

        var lblRestoreHotkey = new Label
        {
            Text = "Restore Layout:",
            Location = new Point(15, 70),
            Size = new Size(140, 20)
        };

        _txtRestoreHotkey = new HotkeyTextBox
        {
            Location = new Point(185, 67),
            Size = new Size(135, 24)
        };
        _txtRestoreHotkey.SetHotkey(UpdatedSettings.RestoreHotkeyModifiers, (Keys)UpdatedSettings.RestoreHotkeyKey);

        grpHotkeys.Controls.AddRange(new Control[] {
            lblSaveHotkey,
            _txtSaveHotkey,
            lblRestoreHotkey,
            _txtRestoreHotkey
        });

        // GroupBox 3: System Integration
        var grpSystem = new GroupBox
        {
            Text = "System Integration",
            Location = new Point(20, 335),
            Size = new Size(345, 65),
            FlatStyle = FlatStyle.Flat
        };

        // Start with Windows checkbox
        _chkStartWithWindows = new CheckBox
        {
            Text = "Launch snapback-layout on Windows startup",
            Location = new Point(15, 25),
            Size = new Size(300, 24),
            Checked = UpdatedSettings.StartWithWindows
        };
        grpSystem.Controls.Add(_chkStartWithWindows);

        // Buttons
        var btnReset = new Button
        {
            Text = "Reset Defaults",
            Location = new Point(20, 412),
            Size = new Size(100, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(243, 244, 246),
            ForeColor = Color.FromArgb(55, 65, 81),
            UseVisualStyleBackColor = false
        };
        btnReset.FlatAppearance.BorderSize = 0;
        btnReset.Click += (s, e) =>
        {
            var defaults = new Settings();
            _chkAutoSave.Checked = defaults.AutoSaveEnabled;
            SelectIntervalIndex(defaults.AutoSaveIntervalMinutes);
            SelectHistoryLimitIndex(defaults.HistoryLimitMinutes);
            _chkDpiCorrection.Checked = defaults.DpiCorrectionEnabled;
            _chkStartWithWindows.Checked = defaults.StartWithWindows;
            _txtSaveHotkey.SetHotkey(defaults.SaveHotkeyModifiers, (Keys)defaults.SaveHotkeyKey);
            _txtRestoreHotkey.SetHotkey(defaults.RestoreHotkeyModifiers, (Keys)defaults.RestoreHotkeyKey);
        };

        _btnOk = new Button
        {
            Text = "OK",
            Location = new Point(165, 412),
            Size = new Size(90, 30),
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(124, 58, 237), // brand purple
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            UseVisualStyleBackColor = false
        };
        _btnOk.FlatAppearance.BorderSize = 0;
        _btnOk.Click += BtnOk_Click;

        _btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(275, 412),
            Size = new Size(90, 30),
            DialogResult = DialogResult.Cancel,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(243, 244, 246),
            ForeColor = Color.FromArgb(55, 65, 81),
            UseVisualStyleBackColor = false
        };
        _btnCancel.FlatAppearance.BorderSize = 0;

        // Add controls
        this.Controls.AddRange(new Control[] {
            grpBackup,
            grpHotkeys,
            grpSystem,
            btnReset,
            _btnOk,
            _btnCancel
        });

        this.AcceptButton = _btnOk;
        this.CancelButton = _btnCancel;
    }

    private void SelectIntervalIndex(int minutes)
    {
        switch (minutes)
        {
            case 1: _cmbInterval.SelectedIndex = 0; break;
            case 5: _cmbInterval.SelectedIndex = 1; break;
            case 10: _cmbInterval.SelectedIndex = 2; break;
            case 15: _cmbInterval.SelectedIndex = 3; break;
            case 30: _cmbInterval.SelectedIndex = 4; break;
            default: _cmbInterval.SelectedIndex = 1; break;
        }
    }

    private int GetIntervalMinutes()
    {
        return _cmbInterval.SelectedIndex switch
        {
            0 => 1,
            1 => 5,
            2 => 10,
            3 => 15,
            4 => 30,
            _ => 5
        };
    }

    private void SelectHistoryLimitIndex(int minutes)
    {
        switch (minutes)
        {
            case 15: _cmbHistoryLimit.SelectedIndex = 0; break;
            case 30: _cmbHistoryLimit.SelectedIndex = 1; break;
            case 60: _cmbHistoryLimit.SelectedIndex = 2; break;
            case 120: _cmbHistoryLimit.SelectedIndex = 3; break;
            case 240: _cmbHistoryLimit.SelectedIndex = 4; break;
            default: _cmbHistoryLimit.SelectedIndex = 2; break;
        }
    }

    private int GetHistoryLimitMinutes()
    {
        return _cmbHistoryLimit.SelectedIndex switch
        {
            0 => 15,
            1 => 30,
            2 => 60,
            3 => 120,
            4 => 240,
            _ => 60
        };
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        UpdatedSettings = new Settings
        {
            AutoSaveEnabled = _chkAutoSave.Checked,
            AutoSaveIntervalMinutes = GetIntervalMinutes(),
            HistoryLimitMinutes = GetHistoryLimitMinutes(),
            DpiCorrectionEnabled = _chkDpiCorrection.Checked,
            StartWithWindows = _chkStartWithWindows.Checked,
            SaveHotkeyModifiers = _txtSaveHotkey.HotkeyModifiers,
            SaveHotkeyKey = (uint)_txtSaveHotkey.HotkeyKey,
            RestoreHotkeyModifiers = _txtRestoreHotkey.HotkeyModifiers,
            RestoreHotkeyKey = (uint)_txtRestoreHotkey.HotkeyKey
        };
        this.Close();
    }
}
