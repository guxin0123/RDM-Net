using System.Collections.Concurrent;
using RdmNet.Models;
using RdmNet.Services;
using RdmNet.Utils;

namespace RdmNet.Forms;

public partial class MainForm : AntdUI.BaseForm
{
    private static readonly Color PendingColor = Color.FromArgb(0x8C, 0x8C, 0x8C);
    private static readonly Color OnlineColor = Color.FromArgb(0x52, 0xC4, 0x1A);
    private static readonly Color OfflineColor = Color.FromArgb(0xFF, 0x4D, 0x4F);
    private static readonly Color SubtleColor = Color.FromArgb(0x8C, 0x8C, 0x8C);

    private readonly StorageService _storage = new();
    private readonly RdpLauncher _launcher = new();
    private readonly ExportService _exportService = new();
    private readonly Dictionary<string, LatencyResult> _latency = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AntTreeItem> _serverNodes = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<LatencyUpdate> _latencyQueue = new();
    private readonly System.Windows.Forms.Timer _latencyTimer;

    private ConnectionsFile _data = new();
    private CancellationTokenSource? _scanCancellation;
    private int _scanGeneration;
    private AntTreeItem? _hoverItem;
    private Form? _latencyTip;
    private bool _closing;

    public MainForm()
    {
        InitializeComponent();
        ApplyToolbarIcons();

        _latencyTimer = new System.Windows.Forms.Timer { Interval = 200 };
        _latencyTimer.Tick += OnLatencyTimerTick;

        WireEvents();
        UpdateButtonStates();
    }

    /// <summary>工具栏按钮图标由 Segoe MDL2 字形运行时光栅化，不宜放在 InitializeComponent 中（设计器无法解析）。</summary>
    private void ApplyToolbarIcons()
    {
        AntButton[] buttons = { btnAddGroup, btnAddServer, btnEditServer, btnDeleteServer, btnConnect, btnRefresh, btnImport, btnExport };
        char[] glyphs = { ToolbarIcons.Folder, ToolbarIcons.Server, ToolbarIcons.Edit, ToolbarIcons.Delete, ToolbarIcons.Monitor, ToolbarIcons.Refresh, ToolbarIcons.Download, ToolbarIcons.Upload };

        for (int i = 0; i < buttons.Length; i++)
        {
            Bitmap? icon = CreateIcon(glyphs[i]);
            if (icon is not null)
            {
                buttons[i].Icon = icon;
            }
        }
    }

    /// <summary>树节点承载的数据：服务器节点同时持有所属分组。</summary>
    private sealed record NodeTag(ConnectionGroup Group, ConnectionServer? Server)
    {
        public bool IsGroup => Server is null;
    }

    private void WireEvents()
    {
        Load += OnFormLoad;
        FormClosing += OnFormClosing;

        btnAddGroup.Click += OnAddGroup;
        btnAddServer.Click += OnAddServer;
        btnEditServer.Click += OnEditServer;
        btnDeleteServer.Click += OnDeleteServer;
        btnConnect.Click += OnConnectClicked;
        btnRefresh.Click += (_, _) => RefreshLatency();
        btnImport.Click += OnImport;
        btnExport.Click += OnExport;

        tree.SelectChanged += (_, _) => UpdateButtonStates();
        tree.NodeMouseClick += OnNodeMouseClick;
        tree.NodeMouseDoubleClick += OnNodeMouseDoubleClick;
        tree.Drop += OnTreeDrop;
        tree.DropDone += OnTreeDropDone;
        tree.NodeMouseMove += OnNodeHover;
        tree.MouseLeave += (_, _) => CloseLatencyTip();
    }

    private void OnFormLoad(object? sender, EventArgs e)
    {
        try
        {
            _data = _storage.Load();
        }
        catch (Exception ex)
        {
            ShowError("读取连接数据失败", ex);
            _data = new ConnectionsFile();
            ConnectionData.EnsureDefaultGroup(_data);
        }

        RebuildTree();
        _latencyTimer.Start();

        // 清理上次异常退出遗留的系统凭据。
        _ = Task.Run(() => _launcher.CleanupLeftoversAsync());

        RefreshLatency();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        _latencyTimer.Stop();
        _scanCancellation?.Cancel();
        _latencyTip?.Close();

        try
        {
            Task.Run(() => _launcher.ReleaseAllAsync()).Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception)
        {
            // 退出阶段的清理失败不阻塞关闭。
        }
    }

    #region 列表构建

    private void RebuildTree()
    {
        string? keepServerId = SelectedItem?.Server?.Id;

        CloseLatencyTip();
        tree.Items.Clear();
        _serverNodes.Clear();

        foreach (ConnectionGroup group in _data.Groups)
        {
            var groupItem = new AntTreeItem(group.Name)
            {
                Icon = CreateIcon(ToolbarIcons.Folder),
                Tag = new NodeTag(group, null),
            };

            foreach (ConnectionServer server in group.Servers)
            {
                var serverItem = new AntTreeItem(server.Name)
                {
                    BadgeAlign = AntTAlign.Right,
                    BadgeOffsetX = BadgeRightMargin,
                    ForeSub = SubtleColor,
                    Icon = CreateIcon(ToolbarIcons.Server),
                    SubTitle = FormatHost(server),
                    Tag = new NodeTag(group, server),
                };
                ApplyLatency(serverItem, server.Id);
                _serverNodes[server.Id] = serverItem;
                groupItem.Sub.Add(serverItem);
            }

            tree.Items.Add(groupItem);
        }

        tree.ExpandAll(true);

        if (keepServerId is not null && _serverNodes.TryGetValue(keepServerId, out AntTreeItem? node))
        {
            tree.Select(node, true);
        }

        UpdateButtonStates();
    }

    /// <summary>按当前 DPI 生成节点图标位图（每次新建，避免被控件释放后复用失效）。</summary>
    private static Bitmap? CreateIcon(char glyph)
    {
        Bitmap? source = ToolbarIcons.Get(glyph, 16);
        return source is null ? null : (Bitmap)source.Clone();
    }

    /// <summary>按树上现有顺序回写模型并保存。</summary>
    private void PersistTreeOrder()
    {
        var groups = new List<ConnectionGroup>();
        foreach (AntTreeItem groupItem in tree.Items)
        {
            if (groupItem.Tag is not NodeTag { Group: { } group })
            {
                continue;
            }

            var servers = new List<ConnectionServer>();
            foreach (AntTreeItem serverItem in groupItem.Sub)
            {
                if (serverItem.Tag is NodeTag { Server: { } server })
                {
                    servers.Add(server);
                }
            }

            group.Servers = servers;
            groups.Add(group);
        }

        _data.Groups = groups;
        SaveData();
    }

    private void SaveData()
    {
        try
        {
            _storage.Save(_data);
        }
        catch (Exception ex)
        {
            ShowError("保存连接数据失败", ex);
        }
    }

    private NodeTag? SelectedItem => tree.SelectItem?.Tag as NodeTag;

    private void SelectServer(string serverId)
    {
        if (_serverNodes.TryGetValue(serverId, out AntTreeItem? item))
        {
            tree.Select(item, true);
        }
    }

    private void UpdateButtonStates()
    {
        bool isServer = SelectedItem?.Server is not null;
        btnEditServer.Enabled = isServer;
        btnDeleteServer.Enabled = isServer;
        btnConnect.Enabled = isServer;
    }

    #endregion

    #region 分组与服务器管理

    private void OnAddGroup(object? sender, EventArgs e)
    {
        string? input = InputDialog.Show(this, "新增分组", "分组名称：", string.Empty);
        if (input is null)
        {
            return;
        }

        string name = input.Trim();
        if (name.Length == 0)
        {
            Notify("分组名称不能为空。", AntTType.Warn);
            return;
        }

        if (ConnectionData.GroupNameExists(_data, name))
        {
            Notify($"分组“{name}”已存在。", AntTType.Warn);
            return;
        }

        _data.Groups.Add(new ConnectionGroup { Name = name });
        SaveData();
        RebuildTree();
    }

    private void OnRenameGroup(object? sender, EventArgs e)
    {
        if (SelectedItem is not { Group: { } group })
        {
            return;
        }

        string? input = InputDialog.Show(this, "重命名分组", "分组名称：", group.Name);
        if (input is null)
        {
            return;
        }

        string name = input.Trim();
        if (string.Equals(name, group.Name, StringComparison.Ordinal))
        {
            return;
        }

        if (ConnectionData.GroupNameExists(_data, name, group))
        {
            Notify($"分组“{name}”已存在。", AntTType.Warn);
            return;
        }

        group.Name = name;
        SaveData();
        RebuildTree();
    }

    private void OnDeleteGroup(object? sender, EventArgs e)
    {
        if (SelectedItem is not { Group: { } group })
        {
            return;
        }

        if (string.Equals(group.Name, ConnectionData.DefaultGroupName, StringComparison.Ordinal))
        {
            Notify("默认分组不能删除。", AntTType.Info);
            return;
        }

        string message = group.Servers.Count == 0
            ? $"确定删除分组“{group.Name}”吗？"
            : $"确定删除分组“{group.Name}”吗？组内 {group.Servers.Count} 台服务器将移入默认分组。";
        if (!Confirm("删除分组", message))
        {
            return;
        }

        ConnectionData.RemoveGroup(_data, group);
        SaveData();
        RebuildTree();
    }

    private void OnAddServer(object? sender, EventArgs e)
    {
        ConnectionGroup group = SelectedItem?.Group ?? ConnectionData.EnsureDefaultGroup(_data);
        using var dialog = new ServerEditForm(_data.Groups.Select(g => g.Name).ToList(), group.Name, null, null);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        string password = dialog.Password;
        var server = new ConnectionServer
        {
            Name = dialog.ServerName,
            Host = dialog.Host,
            Port = dialog.Port,
            Username = dialog.Username,
            Password = DpapiService.Protect(password),
            PasswordFormat = string.IsNullOrEmpty(password) ? null : PasswordFormats.Dpapi,
        };

        ResolveGroupByName(dialog.GroupName).Servers.Add(server);
        SaveData();
        RebuildTree();
        SelectServer(server.Id);
        RefreshLatency();
    }

    private void OnEditServer(object? sender, EventArgs e)
    {
        if (SelectedItem is not { Server: { } server, Group: { } group })
        {
            Notify("请先选择一个服务器。", AntTType.Info);
            return;
        }

        string? plainPassword = null;
        if (!string.IsNullOrEmpty(server.Password))
        {
            try
            {
                plainPassword = DpapiService.Unprotect(server.Password);
            }
            catch (Exception ex)
            {
                Notify($"无法解密已保存的密码，请重新输入。{ex.Message}", AntTType.Warn);
            }
        }

        using var dialog = new ServerEditForm(_data.Groups.Select(g => g.Name).ToList(), group.Name, server, plainPassword);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        string password = dialog.Password;
        server.Name = dialog.ServerName;
        server.Host = dialog.Host;
        server.Port = dialog.Port;
        server.Username = dialog.Username;
        server.Password = DpapiService.Protect(password);
        server.PasswordFormat = string.IsNullOrEmpty(password) ? null : PasswordFormats.Dpapi;
        _latency.Remove(server.Id);

        ConnectionGroup targetGroup = ResolveGroupByName(dialog.GroupName);
        if (!ReferenceEquals(targetGroup, group))
        {
            group.Servers.Remove(server);
            targetGroup.Servers.Add(server);
        }

        SaveData();
        RebuildTree();
        SelectServer(server.Id);
        RefreshLatency();
    }

    private void OnDeleteServer(object? sender, EventArgs e)
    {
        if (SelectedItem is not { Server: { } server, Group: { } group })
        {
            return;
        }

        if (!Confirm("删除服务器", $"确定删除服务器“{server.Name}”吗？"))
        {
            return;
        }

        group.Servers.Remove(server);
        _latency.Remove(server.Id);
        SaveData();
        RebuildTree();
    }

    private ConnectionGroup ResolveGroupByName(string name)
    {
        string trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return ConnectionData.EnsureDefaultGroup(_data);
        }

        ConnectionGroup? existing = ConnectionData.FindGroupByName(_data, trimmed);
        if (existing is not null)
        {
            return existing;
        }

        var group = new ConnectionGroup { Name = trimmed };
        _data.Groups.Add(group);
        return group;
    }

    #endregion

    #region 右键菜单

    private void OnNodeMouseClick(object? sender, AntdUI.TreeSelectEventArgs e)
    {
        if (e.Button != MouseButtons.Right || e.Item?.Tag is not NodeTag tag)
        {
            return;
        }

        tree.Select(e.Item, false);

        if (tag.IsGroup)
        {
            bool canDelete = !string.Equals(tag.Group.Name, ConnectionData.DefaultGroupName, StringComparison.Ordinal);
            AntContextMenuStrip.open(
                tree,
                OnGroupMenuClick,
                new AntdUI.IContextMenuStripItem[]
                {
                    CreateMenuItem("add-server", "新增服务器", ToolbarIcons.Server),
                    CreateMenuItem("rename-group", "重命名分组", ToolbarIcons.Edit),
                    CreateMenuItem("delete-group", "删除分组", ToolbarIcons.Delete, canDelete),
                },
                0);
        }
        else
        {
            AntContextMenuStrip.open(
                tree,
                OnServerMenuClick,
                new AntdUI.IContextMenuStripItem[]
                {
                    CreateMenuItem("connect", "连接", ToolbarIcons.Monitor),
                    CreateMenuItem("edit-server", "编辑服务器", ToolbarIcons.Edit),
                    CreateMenuItem("delete-server", "删除服务器", ToolbarIcons.Delete),
                },
                0);
        }
    }

    private static AntContextMenuItem CreateMenuItem(string id, string text, char glyph, bool enabled = true)
    {
        var item = new AntContextMenuItem(text) { Enabled = enabled, ID = id };
        Bitmap? icon = ToolbarIcons.Get(glyph, 16);
        if (icon is not null)
        {
            item.Icon = (Bitmap)icon.Clone();
        }

        return item;
    }

    private void OnGroupMenuClick(AntdUI.IContextMenuStrip item)
    {
        switch (item.ID)
        {
            case "add-server":
                OnAddServer(this, EventArgs.Empty);
                break;
            case "rename-group":
                OnRenameGroup(this, EventArgs.Empty);
                break;
            case "delete-group":
                OnDeleteGroup(this, EventArgs.Empty);
                break;
        }
    }

    private void OnServerMenuClick(AntdUI.IContextMenuStrip item)
    {
        switch (item.ID)
        {
            case "connect":
                OnConnectClicked(this, EventArgs.Empty);
                break;
            case "edit-server":
                OnEditServer(this, EventArgs.Empty);
                break;
            case "delete-server":
                OnDeleteServer(this, EventArgs.Empty);
                break;
        }
    }

    private void OnNodeMouseDoubleClick(object? sender, AntdUI.TreeSelectEventArgs e)
    {
        if (e.Item?.Tag is NodeTag { Server: { } server })
        {
            ConnectAsync(server);
        }
    }

    #endregion

    #region 连接

    private void OnConnectClicked(object? sender, EventArgs e)
    {
        if (SelectedItem is not { Server: { } server })
        {
            Notify("请先选择一个服务器。", AntTType.Info);
            return;
        }

        ConnectAsync(server);
    }

    private async void ConnectAsync(ConnectionServer server)
    {
        try
        {
            string? plainPassword = null;
            if (!string.IsNullOrEmpty(server.Password))
            {
                try
                {
                    plainPassword = DpapiService.Unprotect(server.Password);
                }
                catch (Exception ex)
                {
                    Notify($"无法解密已保存的密码，将改为手动输入。{ex.Message}", AntTType.Warn);
                }
            }

            ConnectOutcome outcome = await _launcher.ConnectAsync(server, plainPassword);
            if (!outcome.Started)
            {
                Notify(outcome.Message ?? "连接失败。", AntTType.Error);
            }
            else if (!string.IsNullOrEmpty(outcome.Message))
            {
                Notify(outcome.Message, AntTType.Warn);
            }
        }
        catch (Exception ex)
        {
            ShowError("连接失败", ex);
        }
    }

    #endregion

    #region 延迟检测

    private LatencyResult? GetLatency(string serverId)
    {
        return _latency.TryGetValue(serverId, out LatencyResult result) ? result : null;
    }

    /// <summary>把延迟探测结果写回节点右侧徽标。</summary>
    private void ApplyLatency(AntTreeItem item, string serverId)
    {
        if (!_latency.TryGetValue(serverId, out LatencyResult result))
        {
            return;
        }

        (string text, Color color) = result.State switch
        {
            LatencyState.Pending => ("…", PendingColor),
            LatencyState.Online => ($"{result.Milliseconds} ms", OnlineColor),
            _ => ("离线", OfflineColor),
        };

        item.SetBadge(text);
        item.SetBadgeBack(color);
        item.SetBadgeFore(Color.White);
    }

    /// <summary>地址列文本：默认端口只显示地址，非默认端口附带端口号。</summary>
    private static string FormatHost(ConnectionServer server) =>
        server.Port == ConnectionServer.DefaultPort ? server.Host : $"{server.Host}:{server.Port}";

    /// <summary>延迟徽标距行右边缘的间距（按 DPI 缩放），避免与滚动条重叠。</summary>
    private int BadgeRightMargin => (int)Math.Round(16 * (tree.DeviceDpi / 96.0));

    /// <summary>悬停到探测失败的服务器时，在光标处展示失败原因。</summary>
    private void OnNodeHover(object? sender, AntdUI.TreeHoverEventArgs e)
    {
        if (!e.Hover || e.Item?.Tag is not NodeTag { Server: not null } tag)
        {
            CloseLatencyTip();
            return;
        }

        if (ReferenceEquals(e.Item, _hoverItem))
        {
            return;
        }

        CloseLatencyTip();
        if (GetLatency(tag.Server.Id) is not { State: LatencyState.Offline, Detail: { Length: > 0 } detail })
        {
            return;
        }

        // 徽标固定在行右侧，锚定到该区域而不是整行，避免提示浮在名称上方。
        int anchorWidth = Math.Min(96, e.Rect.Width);
        var anchor = new Rectangle(e.Rect.Right - anchorWidth, e.Rect.Top, anchorWidth, e.Rect.Height);

        _hoverItem = e.Item;
        _latencyTip = AntdUI.Tooltip.open(tree, detail, anchor, AntTAlign.Top);
    }

    private void CloseLatencyTip()
    {
        _hoverItem = null;
        Form? tip = _latencyTip;
        _latencyTip = null;
        tip?.Close();
    }

    private void RefreshLatency()
    {
        _scanCancellation?.Cancel();
        _scanCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _scanCancellation = cancellation;
        int generation = ++_scanGeneration;

        CloseLatencyTip();

        List<ConnectionServer> servers = ConnectionData.AllServers(_data).ToList();
        foreach (ConnectionServer server in servers)
        {
            _latency[server.Id] = new LatencyResult(LatencyState.Pending, 0, null);
            if (_serverNodes.TryGetValue(server.Id, out AntTreeItem? item))
            {
                ApplyLatency(item, server.Id);
            }
        }

        if (servers.Count == 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await LatencyService.ScanAsync(servers, generation, (gen, server, result) => _latencyQueue.Enqueue(new LatencyUpdate(gen, server.Id, result)), cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                // 已被新一轮刷新或退出取消。
            }
        });
    }

    private void OnLatencyTimerTick(object? sender, EventArgs e)
    {
        bool changed = false;
        while (_latencyQueue.TryDequeue(out LatencyUpdate update))
        {
            if (update.Generation != _scanGeneration)
            {
                // 上一轮的结果，丢弃。
                continue;
            }

            _latency[update.ServerId] = update.Result;
            if (_serverNodes.TryGetValue(update.ServerId, out AntTreeItem? item))
            {
                ApplyLatency(item, update.ServerId);
            }

            changed = true;
        }

        if (changed)
        {
            tree.Invalidate();
        }
    }

    #endregion

    #region 拖拽排序

    private void OnTreeDrop(object? sender, AntdUI.TreeDropEventArgs e)
    {
        // 分组只能在顶层分组之间排序；服务器只能停留在某个分组内部。
        bool valid = e.DragNode?.Tag is NodeTag dragged
            && (dragged.IsGroup
                ? e.TargetParent is null
                : e.TargetParent?.Tag is NodeTag { IsGroup: true });
        e.Cancel = !valid;
    }

    private void OnTreeDropDone(object? sender, AntdUI.TreeDropDoneEventArgs e)
    {
        string? serverId = (e.DragNode?.Tag as NodeTag)?.Server?.Id;

        // 拖动后节点上的分组引用可能已过期，按当前顺序回写模型后重建，保证数据一致。
        PersistTreeOrder();
        RebuildTree();

        if (serverId is not null)
        {
            SelectServer(serverId);
        }
    }

    #endregion

    #region 导入导出

    private void OnImport(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "导入连接",
            Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            string? password = null;
            ExportDocument document = ExportService.ReadDocument(dialog.FileName);
            if (document.Encrypted)
            {
                using var prompt = PasswordPromptForm.ForImport();
                if (prompt.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                password = prompt.Password;
            }

            ImportResult result = _exportService.Import(dialog.FileName, password, _data, ResolveConflict);
            if (result.Canceled)
            {
                Notify("导入已中止，未保存任何更改。", AntTType.Info);
                return;
            }

            SaveData();
            RebuildTree();
            RefreshLatency();
            Notify($"导入完成：新增 {result.ServersAdded} 台，覆盖 {result.ServersOverwritten} 台，跳过 {result.ServersSkipped} 台。", AntTType.Success);
        }
        catch (Exception ex)
        {
            ShowError("导入失败", ex);
        }
    }

    private void OnExport(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Title = "导出连接",
            Filter = "JSON 文件 (*.json)|*.json",
            FileName = "connections-export.json",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        using var prompt = PasswordPromptForm.ForExport();
        if (prompt.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            ExportResult result = _exportService.Export(_data, dialog.FileName, prompt.Password);
            string message = result.ServersSkipped > 0
                ? $"导出完成。其中 {result.ServersSkipped} 台服务器的密码无法解密，未包含在导出文件中。"
                : "导出完成。";
            Notify(message, result.ServersSkipped > 0 ? AntTType.Warn : AntTType.Success);
        }
        catch (Exception ex)
        {
            ShowError("导出失败", ex);
        }
    }

    private ConflictAction ResolveConflict(ConnectionGroup group, ConnectionServer existing, ConnectionServer incoming)
    {
        DialogResult result = AntModal.open(new AntdUI.Modal.Config(
            this,
            "服务器重名",
            $"分组“{group.Name}”中已存在服务器“{existing.Name}”。\n\n覆盖：用导入的数据替换；跳过：保留现有数据；中止：停止导入且不保存。")
        {
            Btns = new[]
            {
                new AntdUI.Modal.Btn("overwrite", "覆盖", AntTTypeMini.Primary) { DialogResult = DialogResult.Yes },
                new AntdUI.Modal.Btn("skip", "跳过", AntTTypeMini.Default) { DialogResult = DialogResult.No },
                new AntdUI.Modal.Btn("abort", "中止导入", AntTTypeMini.Error) { DialogResult = DialogResult.Cancel },
            },
        });

        return result switch
        {
            DialogResult.Yes => ConflictAction.Overwrite,
            DialogResult.No => ConflictAction.Skip,
            _ => ConflictAction.Cancel,
        };
    }

    #endregion

    #region 提示

    private void Notify(string text, AntTType type)
    {
        switch (type)
        {
            case AntTType.Success:
                AntMessage.success(this, text);
                break;
            case AntTType.Warn:
                AntMessage.warn(this, text);
                break;
            case AntTType.Error:
                AntMessage.error(this, text);
                break;
            default:
                AntMessage.info(this, text);
                break;
        }
    }

    private bool Confirm(string title, string content) =>
        AntModal.open(this, title, content, AntTType.Warn) == DialogResult.OK;

    private void ShowError(string title, Exception exception)
    {
        Notify($"{title}：{exception.Message}", AntTType.Error);
    }

    #endregion

    private readonly record struct LatencyUpdate(int Generation, string ServerId, LatencyResult Result);
}
