namespace RdmNet.Forms;

/// <summary>单行文本输入对话框（新增分组、重命名分组）。</summary>
public sealed class InputDialog : AntdUI.BaseForm
{
    private readonly AntLabel _label;
    private readonly AntInput _input;

    private InputDialog(string title, string labelText, string initialText)
    {
        _label = new AntLabel
        {
            Text = labelText,
            AutoSize = false,
            Location = new Point(24, 24),
            Size = new Size(336, 36),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _input = new AntInput
        {
            Text = initialText,
            Location = new Point(24, 66),
            Size = new Size(336, 36),
            PlaceholderText = labelText,
        };

        var okButton = new AntButton
        {
            Text = "确定",
            Location = new Point(184, 118),
            Size = new Size(88, 36),
            Shape = AntTShape.Round,
            Type = AntTTypeMini.Primary,
        };

        var cancelButton = new AntButton
        {
            Text = "取消",
            Location = new Point(280, 118),
            Size = new Size(80, 36),
            Shape = AntTShape.Round,
            DialogResult = DialogResult.Cancel,
        };

        okButton.Click += (_, _) =>
        {
            if (_input.Text.Trim().Length == 0)
            {
                AntMessage.warn(this, "名称不能为空。");
                return;
            }

            DialogResult = DialogResult.OK;
        };

        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        ClientSize = new Size(384, 170);
        MaximizeBox = false;
        MinimizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = title;
        AcceptButton = okButton;
        CancelButton = cancelButton;
        Controls.AddRange(new Control[] { _label, _input, okButton, cancelButton });
        _input.SelectAll();
    }

    /// <summary>返回用户输入的名称，取消时返回 null。</summary>
    public static string? Show(IWin32Window owner, string title, string labelText, string initialText)
    {
        using var dialog = new InputDialog(title, labelText, initialText);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog._input.Text : null;
    }
}