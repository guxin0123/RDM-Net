using RdmNet.Utils;

namespace RdmNet.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;
    private ToolStrip toolStrip = null!;
    private ToolStripButton btnAddGroup = null!;
    private ToolStripButton btnAddServer = null!;
    private ToolStripButton btnEditServer = null!;
    private ToolStripButton btnDeleteServer = null!;
    private ToolStripButton btnConnect = null!;
    private ToolStripButton btnRefresh = null!;
    private ToolStripButton btnImport = null!;
    private ToolStripButton btnExport = null!;
    private Controls.RdpTreeView tree = null!;
    private ContextMenuStrip serverMenu = null!;
    private ContextMenuStrip groupMenu = null!;
    private ToolStripMenuItem miConnect = null!;
    private ToolStripMenuItem miEditServer = null!;
    private ToolStripMenuItem miDeleteServer = null!;
    private ToolStripMenuItem miAddServer = null!;
    private ToolStripMenuItem miRenameGroup = null!;
    private ToolStripMenuItem miDeleteGroup = null!;

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
        toolStrip = new ToolStrip();
        btnAddGroup = new ToolStripButton();
        btnAddServer = new ToolStripButton();
        btnEditServer = new ToolStripButton();
        btnDeleteServer = new ToolStripButton();
        btnConnect = new ToolStripButton();
        btnRefresh = new ToolStripButton();
        btnImport = new ToolStripButton();
        btnExport = new ToolStripButton();
        tree = new Controls.RdpTreeView();
        serverMenu = new ContextMenuStrip(components);
        groupMenu = new ContextMenuStrip(components);
        miConnect = new ToolStripMenuItem();
        miEditServer = new ToolStripMenuItem();
        miDeleteServer = new ToolStripMenuItem();
        miAddServer = new ToolStripMenuItem();
        miRenameGroup = new ToolStripMenuItem();
        miDeleteGroup = new ToolStripMenuItem();
        toolStrip.SuspendLayout();
        serverMenu.SuspendLayout();
        groupMenu.SuspendLayout();
        SuspendLayout();

        // 工具栏按钮
        ToolStripButton[] buttons = { btnAddGroup, btnAddServer, btnEditServer, btnDeleteServer, btnConnect, btnRefresh, btnImport, btnExport };
        string[] captions = { "新增分组", "新增服务器", "编辑服务器", "删除服务器", "连接到选中服务器", "刷新服务器列表", "导入连接", "导出连接" };
        char[] glyphs = { ToolbarIcons.Folder, ToolbarIcons.Server, ToolbarIcons.Edit, ToolbarIcons.Delete, ToolbarIcons.Monitor, ToolbarIcons.Refresh, ToolbarIcons.Download, ToolbarIcons.Upload };
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].Text = captions[i];
            buttons[i].AutoSize = true;
            buttons[i].TextImageRelation = TextImageRelation.ImageBeforeText;
            var icon = ToolbarIcons.Get(glyphs[i]);
            if (icon is not null)
            {
                buttons[i].Image = icon;
                buttons[i].ImageScaling = ToolStripItemImageScaling.SizeToFit;
                buttons[i].DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            }
            else
            {
                buttons[i].DisplayStyle = ToolStripItemDisplayStyle.Text;
            }
        }

        toolStrip.GripStyle = ToolStripGripStyle.Hidden;
        toolStrip.Items.AddRange(new ToolStripItem[]
        {
            btnAddGroup,
            btnAddServer,
            btnEditServer,
            btnDeleteServer,
            new ToolStripSeparator(),
            btnConnect,
            new ToolStripSeparator(),
            btnRefresh,
            new ToolStripSeparator(),
            btnImport,
            btnExport,
        });
        toolStrip.Location = new Point(0, 0);
        toolStrip.Name = "toolStrip";
        toolStrip.Padding = new Padding(6, 2, 6, 2);
        toolStrip.Size = new Size(900, 29);
        toolStrip.TabIndex = 0;

        // 树
        tree.Dock = DockStyle.Fill;
        tree.Location = new Point(0, 29);
        tree.Name = "tree";
        tree.Size = new Size(900, 571);
        tree.TabIndex = 1;

        // 服务器右键菜单
        serverMenu.Items.AddRange(new ToolStripItem[] { miConnect, miEditServer, miDeleteServer });
        serverMenu.Name = "serverMenu";
        serverMenu.Size = new Size(160, 70);
        miConnect.Text = "连接";
        miConnect.Image = ToolbarIcons.Get(ToolbarIcons.Monitor);
        miEditServer.Text = "编辑服务器";
        miEditServer.Image = ToolbarIcons.Get(ToolbarIcons.Edit);
        miDeleteServer.Text = "删除服务器";
        miDeleteServer.Image = ToolbarIcons.Get(ToolbarIcons.Delete);

        // 分组右键菜单
        groupMenu.Items.AddRange(new ToolStripItem[] { miAddServer, miRenameGroup, miDeleteGroup });
        groupMenu.Name = "groupMenu";
        groupMenu.Size = new Size(160, 70);
        miAddServer.Text = "新增服务器";
        miAddServer.Image = ToolbarIcons.Get(ToolbarIcons.Server);
        miRenameGroup.Text = "重命名分组";
        miRenameGroup.Image = ToolbarIcons.Get(ToolbarIcons.Edit);
        miDeleteGroup.Text = "删除分组";
        miDeleteGroup.Image = ToolbarIcons.Get(ToolbarIcons.Delete);

        // 窗体
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(900, 600);
        MinimumSize = new Size(600, 400);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "RdmNet - 远程桌面连接管理";
        Controls.Add(tree);
        Controls.Add(toolStrip);
        toolStrip.ResumeLayout(false);
        toolStrip.PerformLayout();
        serverMenu.ResumeLayout(false);
        groupMenu.ResumeLayout(false);
        ResumeLayout(false);
        PerformLayout();
    }
}
