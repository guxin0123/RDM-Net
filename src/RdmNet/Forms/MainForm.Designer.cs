#nullable enable

namespace RdmNet.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;
    private AntPanel topBar = null!;
    private AntButton btnAddGroup = null!;
    private AntButton btnAddServer = null!;
    private AntButton btnEditServer = null!;
    private AntButton btnDeleteServer = null!;
    private AntButton btnConnect = null!;
    private AntButton btnRefresh = null!;
    private AntButton btnImport = null!;
    private AntButton btnExport = null!;
    private AntTree tree = null!;
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
        topBar = new AntPanel();
        tree = new AntTree();
        tips = new ToolTip(components);
        btnAddGroup = new AntButton();
        btnAddServer = new AntButton();
        btnEditServer = new AntButton();
        btnDeleteServer = new AntButton();
        btnConnect = new AntButton();
        btnRefresh = new AntButton();
        btnImport = new AntButton();
        btnExport = new AntButton();
        topBar.SuspendLayout();
        SuspendLayout();

        // 工具栏（按钮图标由 ApplyToolbarIcons 运行时加载）
        btnAddGroup.AutoSize = false;
        btnAddGroup.Location = new Point(16, 14);
        btnAddGroup.Shape = AntTShape.Round;
        btnAddGroup.Radius = 6;
        btnAddGroup.Size = new Size(104, 36);
        btnAddGroup.IconSize = new Size(16, 16);
        btnAddGroup.Text = "新增分组";
        tips.SetToolTip(btnAddGroup, "新增分组");

        btnAddServer.AutoSize = false;
        btnAddServer.Location = new Point(128, 14);
        btnAddServer.Shape = AntTShape.Round;
        btnAddServer.Radius = 6;
        btnAddServer.Size = new Size(116, 36);
        btnAddServer.IconSize = new Size(16, 16);
        btnAddServer.Text = "新增服务器";
        tips.SetToolTip(btnAddServer, "在选中分组中新增服务器");

        btnEditServer.AutoSize = false;
        btnEditServer.Location = new Point(252, 14);
        btnEditServer.Shape = AntTShape.Round;
        btnEditServer.Radius = 6;
        btnEditServer.Size = new Size(116, 36);
        btnEditServer.IconSize = new Size(16, 16);
        btnEditServer.Text = "编辑服务器";
        tips.SetToolTip(btnEditServer, "编辑选中的服务器");

        btnDeleteServer.AutoSize = false;
        btnDeleteServer.Location = new Point(376, 14);
        btnDeleteServer.Shape = AntTShape.Round;
        btnDeleteServer.Radius = 6;
        btnDeleteServer.Size = new Size(116, 36);
        btnDeleteServer.IconSize = new Size(16, 16);
        btnDeleteServer.Text = "删除服务器";
        tips.SetToolTip(btnDeleteServer, "删除选中的服务器");

        btnConnect.AutoSize = false;
        btnConnect.Location = new Point(500, 14);
        btnConnect.Shape = AntTShape.Round;
        btnConnect.Radius = 6;
        btnConnect.Size = new Size(80, 36);
        btnConnect.IconSize = new Size(16, 16);
        btnConnect.Text = "连接";
        btnConnect.Type = AntTTypeMini.Primary;
        tips.SetToolTip(btnConnect, "连接到选中的服务器");

        btnRefresh.AutoSize = false;
        btnRefresh.Location = new Point(588, 14);
        btnRefresh.Shape = AntTShape.Round;
        btnRefresh.Radius = 6;
        btnRefresh.Size = new Size(80, 36);
        btnRefresh.IconSize = new Size(16, 16);
        btnRefresh.Text = "刷新";
        tips.SetToolTip(btnRefresh, "重新探测全部服务器延迟");

        btnImport.AutoSize = false;
        btnImport.Location = new Point(676, 14);
        btnImport.Shape = AntTShape.Round;
        btnImport.Radius = 6;
        btnImport.Size = new Size(80, 36);
        btnImport.IconSize = new Size(16, 16);
        btnImport.Text = "导入";
        tips.SetToolTip(btnImport, "从文件导入连接");

        btnExport.AutoSize = false;
        btnExport.Location = new Point(764, 14);
        btnExport.Shape = AntTShape.Round;
        btnExport.Radius = 6;
        btnExport.Size = new Size(80, 36);
        btnExport.IconSize = new Size(16, 16);
        btnExport.Text = "导出";
        tips.SetToolTip(btnExport, "导出连接到文件");

        topBar.Back = Color.FromArgb(0xF7, 0xF8, 0xFA);
        topBar.Dock = DockStyle.Top;
        topBar.Height = 64;
        topBar.Name = "topBar";
        topBar.Controls.Add(btnAddGroup);
        topBar.Controls.Add(btnAddServer);
        topBar.Controls.Add(btnEditServer);
        topBar.Controls.Add(btnDeleteServer);
        topBar.Controls.Add(btnConnect);
        topBar.Controls.Add(btnRefresh);
        topBar.Controls.Add(btnImport);
        topBar.Controls.Add(btnExport);

        // 连接列表
        // BlockNode 让节点占满整行宽度，延迟徽标才能对齐到行右边缘。
        tree.BackColor = Color.White;
        tree.BlockNode = true;
        tree.Dock = DockStyle.Fill;
        tree.Draggable = true;
        tree.EmptyText = "暂无服务器，点击上方「新增服务器」添加";
        tree.Name = "tree";
        tree.Round = false;
        tree.TabIndex = 1;

        // 窗体
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        ClientSize = new Size(920, 600);
        MinimumSize = new Size(868, 460);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "RdmNet - 远程桌面连接管理";
        Controls.Add(tree);
        Controls.Add(topBar);
        topBar.ResumeLayout(false);
        ResumeLayout(false);
    }
}
