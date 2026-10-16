namespace RdmNet.Forms;

partial class ServerEditForm
{
    private System.ComponentModel.IContainer components = null!;
    private Label lblGroup = null!;
    private ComboBox cboGroup = null!;
    private Label lblName = null!;
    private TextBox txtName = null!;
    private Label lblHost = null!;
    private TextBox txtHost = null!;
    private Label lblPort = null!;
    private NumericUpDown numPort = null!;
    private Label lblUser = null!;
    private TextBox txtUser = null!;
    private Label lblPassword = null!;
    private TextBox txtPassword = null!;
    private Button btnOk = null!;
    private Button btnCancel = null!;

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
        lblGroup = new Label();
        cboGroup = new ComboBox();
        lblName = new Label();
        txtName = new TextBox();
        lblHost = new Label();
        txtHost = new TextBox();
        lblPort = new Label();
        numPort = new NumericUpDown();
        lblUser = new Label();
        txtUser = new TextBox();
        lblPassword = new Label();
        txtPassword = new TextBox();
        btnOk = new Button();
        btnCancel = new Button();
        ((System.ComponentModel.ISupportInitialize)numPort).BeginInit();
        SuspendLayout();

        Label[] labels = { lblGroup, lblName, lblHost, lblPort, lblUser, lblPassword };
        Control[] inputs = { cboGroup, txtName, txtHost, numPort, txtUser, txtPassword };
        string[] captions = { "分组名称：", "服务器名称：", "IP 地址或域名：", "端口号：", "用户名：", "密码：" };
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i].Text = captions[i];
            labels[i].Location = new Point(20, 27 + (i * 38));
            labels[i].Size = new Size(95, 20);
            labels[i].TextAlign = ContentAlignment.MiddleLeft;
            inputs[i].Location = new Point(120, 24 + (i * 38));
            inputs[i].Size = new Size(310, 25);
        }

        cboGroup.DropDownStyle = ComboBoxStyle.DropDown;
        numPort.Minimum = 1;
        numPort.Maximum = 65535;
        numPort.Value = 3389;
        txtPassword.UseSystemPasswordChar = true;

        btnOk.Text = "确定";
        btnOk.Location = new Point(258, 258);
        btnOk.Size = new Size(80, 28);

        btnCancel.Text = "取消";
        btnCancel.Location = new Point(350, 258);
        btnCancel.Size = new Size(80, 28);
        btnCancel.DialogResult = DialogResult.Cancel;

        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(450, 300);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "服务器";
        Controls.AddRange(new Control[]
        {
            lblGroup, cboGroup, lblName, txtName, lblHost, txtHost, lblPort, numPort, lblUser, txtUser, lblPassword, txtPassword, btnOk, btnCancel,
        });
        CancelButton = btnCancel;
        ((System.ComponentModel.ISupportInitialize)numPort).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }
}
