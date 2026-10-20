using RdmNet.Models;

namespace RdmNet.Forms;

public partial class ServerEditForm : AntdUI.BaseForm
{
    /// <summary>供设计器使用。</summary>
    public ServerEditForm()
    {
        InitializeComponent();
    }

    public ServerEditForm(IReadOnlyList<string> groupNames, string groupName, ConnectionServer? server, string? plainPassword)
    {
        InitializeComponent();

        ddGroup.Items.AddRange(groupNames.Cast<object>().ToArray());
        ddGroup.SelectedValueChanged += (_, e) =>
        {
            if (e.Value?.ToString() is { Length: > 0 } name)
            {
                txtGroup.Text = name;
            }
        };

        txtGroup.Text = groupName;

        if (server is null)
        {
            Text = "新增服务器";
            numPort.Value = ConnectionServer.DefaultPort;
        }
        else
        {
            Text = "编辑服务器";
            txtName.Text = server.Name;
            txtHost.Text = server.Host;
            numPort.Value = server.Port is >= 1 and <= 65535 ? server.Port : ConnectionServer.DefaultPort;
            txtUser.Text = server.Username;
            txtPassword.Text = plainPassword ?? string.Empty;
        }

        btnOk.Click += OnOk;
    }

    public string GroupName
    {
        get
        {
            string name = txtGroup.Text.Trim();
            return name.Length == 0 ? ConnectionData.DefaultGroupName : name;
        }
    }

    public string ServerName => txtName.Text.Trim();

    public string Host => txtHost.Text.Trim();

    public int Port => (int)numPort.Value;

    public string Username => txtUser.Text.Trim();

    public string Password => txtPassword.Text;

    private void OnOk(object? sender, EventArgs e)
    {
        if (Host.Length == 0)
        {
            AntMessage.warn(this, "请输入 IP 地址或域名。");
            return;
        }

        // 名称留空时自动使用服务器地址作为名称。
        if (ServerName.Length == 0)
        {
            txtName.Text = Host;
        }

        DialogResult = DialogResult.OK;
    }
}
