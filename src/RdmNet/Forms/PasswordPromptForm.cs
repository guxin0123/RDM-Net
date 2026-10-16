namespace RdmNet.Forms;

/// <summary>导入 / 导出时输入密码。导出时可选择不使用密码。</summary>
public sealed class PasswordPromptForm : Form
{
    private readonly bool _isExport;
    private readonly CheckBox? _usePasswordCheckBox;
    private readonly TextBox _passwordTextBox;
    private readonly TextBox? _confirmTextBox;

    private PasswordPromptForm(bool isExport)
    {
        _isExport = isExport;
        int y = 18;

        if (isExport)
        {
            _usePasswordCheckBox = new CheckBox
            {
                Text = "使用密码加密导出的密码字段",
                Location = new Point(18, y),
                Size = new Size(360, 22),
            };
            y += 30;
        }

        var passwordLabel = new Label
        {
            Text = isExport ? "导出密码：" : "导出时使用的密码：",
            Location = new Point(18, y + 3),
            Size = new Size(140, 20),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _passwordTextBox = new TextBox
        {
            Location = new Point(162, y),
            Size = new Size(226, 25),
            UseSystemPasswordChar = true,
            Enabled = !isExport,
        };
        y += 36;

        if (isExport)
        {
            var confirmLabel = new Label
            {
                Text = "确认密码：",
                Location = new Point(18, y + 3),
                Size = new Size(140, 20),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            _confirmTextBox = new TextBox
            {
                Location = new Point(162, y),
                Size = new Size(226, 25),
                UseSystemPasswordChar = true,
                Enabled = false,
            };
            y += 36;

            Controls.AddRange(new Control[] { confirmLabel, _confirmTextBox });
        }

        var okButton = new Button
        {
            Text = "确定",
            Location = new Point(218, y + 6),
            Size = new Size(80, 28),
        };
        var cancelButton = new Button
        {
            Text = "取消",
            Location = new Point(308, y + 6),
            Size = new Size(80, 28),
            DialogResult = DialogResult.Cancel,
        };

        if (_usePasswordCheckBox is not null)
        {
            _usePasswordCheckBox.CheckedChanged += (_, _) =>
            {
                bool enabled = _usePasswordCheckBox.Checked;
                _passwordTextBox.Enabled = enabled;
                if (_confirmTextBox is not null)
                {
                    _confirmTextBox.Enabled = enabled;
                }
            };
        }

        okButton.Click += OnOk;

        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(405, y + 46);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = isExport ? "导出连接" : "导入连接";
        Controls.AddRange(new Control[]
        {
            passwordLabel, _passwordTextBox, okButton, cancelButton,
        });
        if (_usePasswordCheckBox is not null)
        {
            Controls.Add(_usePasswordCheckBox);
        }

        AcceptButton = okButton;
        CancelButton = cancelButton;
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

            return _passwordTextBox.Text.Length == 0 ? null : _passwordTextBox.Text;
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
                if (_passwordTextBox.Text.Length == 0)
                {
                    MessageBox.Show(this, "请输入导出密码。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (_confirmTextBox is not null && _confirmTextBox.Text != _passwordTextBox.Text)
                {
                    MessageBox.Show(this, "两次输入的密码不一致。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
        }
        else if (_passwordTextBox.Text.Length == 0)
        {
            MessageBox.Show(this, "请输入导出时使用的密码。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
    }
}
