namespace RdmNet.Forms;

/// <summary>导入 / 导出时输入密码。导出时可选择不使用密码。</summary>
public sealed class PasswordPromptForm : AntdUI.BaseForm
{
    private readonly bool _isExport;
    private readonly AntCheckbox? _usePasswordCheckBox;
    private readonly AntInput _passwordInput;
    private readonly AntInput? _confirmInput;
    private readonly AntLabel _passwordLabel;
    private readonly AntLabel? _confirmLabel;

    private PasswordPromptForm(bool isExport)
    {
        _isExport = isExport;
        int y = 24;

        if (isExport)
        {
            _usePasswordCheckBox = new AntCheckbox
            {
                Text = "使用密码加密导出的密码字段",
                AutoSize = false,
                Location = new Point(24, y),
                Size = new Size(360, 24),
            };
            y += 36;
        }

        _passwordLabel = new AntLabel
        {
            Text = isExport ? "导出密码：" : "导出时使用的密码：",
            AutoSize = false,
            Location = new Point(24, y),
            Size = new Size(140, 36),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _passwordInput = new AntInput
        {
            Location = new Point(172, y),
            Size = new Size(212, 36),
            UseSystemPasswordChar = true,
            PasswordCopy = false,
            PasswordPaste = false,
            Enabled = !isExport,
        };
        y += 46;

        if (isExport)
        {
            _confirmLabel = new AntLabel
            {
                Text = "确认密码：",
                AutoSize = false,
                Location = new Point(24, y),
                Size = new Size(140, 36),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            _confirmInput = new AntInput
            {
                Location = new Point(172, y),
                Size = new Size(212, 36),
                UseSystemPasswordChar = true,
                PasswordCopy = false,
                PasswordPaste = false,
                Enabled = false,
            };
            y += 46;
        }

        var okButton = new AntButton
        {
            Text = "确定",
            Location = new Point(200, y + 8),
            Size = new Size(88, 36),
            Shape = AntTShape.Round,
            Type = AntTTypeMini.Primary,
        };
        var cancelButton = new AntButton
        {
            Text = "取消",
            Location = new Point(296, y + 8),
            Size = new Size(88, 36),
            Shape = AntTShape.Round,
            DialogResult = DialogResult.Cancel,
        };

        if (_usePasswordCheckBox is not null)
        {
            _usePasswordCheckBox.CheckedChanged += (_, _) =>
            {
                bool enabled = _usePasswordCheckBox.Checked;
                _passwordInput.Enabled = enabled;
                if (_confirmInput is not null)
                {
                    _confirmInput.Enabled = enabled;
                }
            };
        }

        okButton.Click += OnOk;

        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        ClientSize = new Size(408, y + 60);
        MaximizeBox = false;
        MinimizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = isExport ? "导出连接" : "导入连接";
        AcceptButton = okButton;
        CancelButton = cancelButton;

        var controls = new List<Control> { _passwordLabel, _passwordInput, okButton, cancelButton };
        if (_usePasswordCheckBox is not null)
        {
            controls.Add(_usePasswordCheckBox);
        }

        if (_confirmLabel is not null && _confirmInput is not null)
        {
            controls.Add(_confirmLabel);
            controls.Add(_confirmInput);
        }

        Controls.AddRange(controls.ToArray());
    }

    /// <summary>导出时返回用户输入的密码；未启用加密时返回 null。</summary>
    public string? Password
    {
        get
        {
            if (_isExport && _usePasswordCheckBox?.Checked != true)
            {
                return null;
            }

            return _passwordInput.Text.Length == 0 ? null : _passwordInput.Text;
        }
    }

    public static PasswordPromptForm ForExport() => new(isExport: true);

    public static PasswordPromptForm ForImport() => new(isExport: false);

    private void OnOk(object? sender, EventArgs e)
    {
        if (_isExport)
        {
            if (_usePasswordCheckBox?.Checked == true)
            {
                if (_passwordInput.Text.Length == 0)
                {
                    AntMessage.warn(this, "请输入导出密码。");
                    return;
                }

                if (_confirmInput is not null && _confirmInput.Text != _passwordInput.Text)
                {
                    AntMessage.warn(this, "两次输入的密码不一致。");
                    return;
                }
            }
        }
        else if (_passwordInput.Text.Length == 0)
        {
            AntMessage.warn(this, "请输入导出时使用的密码。");
            return;
        }

        DialogResult = DialogResult.OK;
    }
}