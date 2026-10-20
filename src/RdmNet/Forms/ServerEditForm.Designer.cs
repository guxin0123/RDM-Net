namespace RdmNet.Forms;

partial class ServerEditForm
{
    private System.ComponentModel.IContainer components = null!;
    private AntLabel lblGroup = null!;
    private AntInput txtGroup = null!;
    private AntDropdown ddGroup = null!;
    private AntLabel lblName = null!;
    private AntInput txtName = null!;
    private AntLabel lblHost = null!;
    private AntInput txtHost = null!;
    private AntLabel lblPort = null!;
    private AntInputNumber numPort = null!;
    private AntLabel lblUser = null!;
    private AntInput txtUser = null!;
    private AntLabel lblPassword = null!;
    private AntInput txtPassword = null!;
    private AntButton btnOk = null!;
    private AntButton btnCancel = null!;
    private ToolTip tips = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        lblGroup = new AntLabel();
        txtGroup = new AntInput();
        ddGroup = new AntDropdown();
        lblName = new AntLabel();
        txtName = new AntInput();
        lblHost = new AntLabel();
        txtHost = new AntInput();
        lblPort = new AntLabel();
        numPort = new AntInputNumber();
        lblUser = new AntLabel();
        txtUser = new AntInput();
        lblPassword = new AntLabel();
        txtPassword = new AntInput();
        btnOk = new AntButton();
        btnCancel = new AntButton();
        tips = new ToolTip(components);
        SuspendLayout();

        lblGroup.AutoSize = false;
        lblGroup.Location = new Point(24, 24);
        lblGroup.Size = new Size(96, 36);
        lblGroup.Text = "分组名称：";
        lblGroup.TextAlign = ContentAlignment.MiddleLeft;

        lblName.AutoSize = false;
        lblName.Location = new Point(24, 70);
        lblName.Size = new Size(96, 36);
        lblName.Text = "服务器名称：";
        lblName.TextAlign = ContentAlignment.MiddleLeft;

        lblHost.AutoSize = false;
        lblHost.Location = new Point(24, 116);
        lblHost.Size = new Size(96, 36);
        lblHost.Text = "IP 地址或域名：";
        lblHost.TextAlign = ContentAlignment.MiddleLeft;

        lblPort.AutoSize = false;
        lblPort.Location = new Point(24, 162);
        lblPort.Size = new Size(96, 36);
        lblPort.Text = "端口号：";
        lblPort.TextAlign = ContentAlignment.MiddleLeft;

        lblUser.AutoSize = false;
        lblUser.Location = new Point(24, 208);
        lblUser.Size = new Size(96, 36);
        lblUser.Text = "用户名：";
        lblUser.TextAlign = ContentAlignment.MiddleLeft;

        lblPassword.AutoSize = false;
        lblPassword.Location = new Point(24, 254);
        lblPassword.Size = new Size(96, 36);
        lblPassword.Text = "密码：";
        lblPassword.TextAlign = ContentAlignment.MiddleLeft;

        txtGroup.PlaceholderText = "留空则使用默认分组";
        txtGroup.Location = new Point(124, 24);
        txtGroup.Size = new Size(264, 36);

        ddGroup.AutoSize = false;
        ddGroup.IconSize = new Size(14, 14);
        ddGroup.IconSvg = AntdUI.SvgDb.IcoDown;
        ddGroup.Location = new Point(392, 24);
        ddGroup.Radius = 6;
        ddGroup.Shape = AntTShape.Round;
        ddGroup.Size = new Size(44, 36);
        ddGroup.Text = string.Empty;
        tips.SetToolTip(ddGroup, "从已有分组中选择");

        txtName.Location = new Point(124, 70);
        txtName.Size = new Size(312, 36);

        txtHost.Location = new Point(124, 116);
        txtHost.Size = new Size(312, 36);

        numPort.Location = new Point(124, 162);
        numPort.Maximum = 65535;
        numPort.Minimum = 1;
        numPort.Size = new Size(312, 36);
        numPort.Value = 3389;

        txtUser.Location = new Point(124, 208);
        txtUser.Size = new Size(312, 36);

        txtPassword.Location = new Point(124, 254);
        txtPassword.Size = new Size(312, 36);
        txtPassword.UseSystemPasswordChar = true;
        txtPassword.PasswordCopy = false;
        txtPassword.PasswordPaste = false;

        btnOk.Location = new Point(272, 306);
        btnOk.Size = new Size(88, 36);
        btnOk.Shape = AntTShape.Round;
        btnOk.Text = "确定";
        btnOk.Type = AntTTypeMini.Primary;

        btnCancel.DialogResult = DialogResult.Cancel;
        btnCancel.Location = new Point(368, 306);
        btnCancel.Shape = AntTShape.Round;
        btnCancel.Size = new Size(88, 36);
        btnCancel.Text = "取消";

        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        ClientSize = new Size(480, 366);
        MaximizeBox = false;
        MinimizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "服务器";
        AcceptButton = btnOk;
        CancelButton = btnCancel;
        Controls.Add(lblGroup);
        Controls.Add(txtGroup);
        Controls.Add(ddGroup);
        Controls.Add(lblName);
        Controls.Add(txtName);
        Controls.Add(lblHost);
        Controls.Add(txtHost);
        Controls.Add(lblPort);
        Controls.Add(numPort);
        Controls.Add(lblUser);
        Controls.Add(txtUser);
        Controls.Add(lblPassword);
        Controls.Add(txtPassword);
        Controls.Add(btnOk);
        Controls.Add(btnCancel);
        ResumeLayout(false);
    }
}
