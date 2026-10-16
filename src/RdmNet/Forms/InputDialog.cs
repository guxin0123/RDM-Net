namespace RdmNet.Forms;

/// <summary>单行文本输入对话框（新增分组、重命名分组）。</summary>
public sealed class InputDialog : Form
{
    private readonly TextBox _textBox;

    private InputDialog(string title, string labelText, string initialText)
    {
        var label = new Label
        {
            Text = labelText,
            Location = new Point(18, 20),
            Size = new Size(360, 20),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _textBox = new TextBox
        {
            Text = initialText,
            Location = new Point(18, 48),
            Size = new Size(370, 25),
        };

        var okButton = new Button
        {
            Text = "确定",
            Location = new Point(218, 90),
            Size = new Size(80, 28),
        };

        var cancelButton = new Button
        {
            Text = "取消",
            Location = new Point(308, 90),
            Size = new Size(80, 28),
            DialogResult = DialogResult.Cancel,
        };

        okButton.Click += (_, _) =>
        {
            if (_textBox.Text.Trim().Length == 0)
            {
                MessageBox.Show(this, "名称不能为空。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
        };

        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(405, 130);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = title;
        Controls.AddRange(new Control[] { label, _textBox, okButton, cancelButton });
        AcceptButton = okButton;
        CancelButton = cancelButton;
        _textBox.SelectAll();
    }

    /// <summary>返回用户输入的名称，取消时返回 null。</summary>
    public static string? Show(IWin32Window owner, string title, string labelText, string initialText)
    {
        using var dialog = new InputDialog(title, labelText, initialText);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog._textBox.Text : null;
    }
}
