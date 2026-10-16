using System.Collections.Concurrent;
using RdmNet.Controls;
using RdmNet.Models;
using RdmNet.Services;

namespace RdmNet.Forms;

public partial class MainForm : Form
{
    private readonly StorageService _storage = new();
    private readonly RdpLauncher _launcher = new();
    private readonly ExportService _exportService = new();
    private readonly Dictionary<string, LatencyResult> _latency = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TreeNode> _serverNodes = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<LatencyUpdate> _latencyQueue = new();
    private readonly System.Windows.Forms.Timer _latencyTimer;

    private ConnectionsFile _data = new();
    private CancellationTokenSource? _scanCancellation;
    private int _scanGeneration;
    private TreeNode? _dragNode;
    private TreeNode? _dropNode;
    private DropInsertMode _dropMode = DropInsertMode.None;
    private bool _closing;

    public MainForm()
    {
        InitializeComponent();

        _latencyTimer = new System.Windows.Forms.Timer { Interval = 200 };
        _latencyTimer.Tick += OnLatencyTimerTick;

        tree.LatencyProvider = GetLatencyBadge;

        WireEvents();
        UpdateButtonStates();
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

        miConnect.Click += OnConnectClicked;
        miEditServer.Click += OnEditServer;
        miDeleteServer.Click += OnDeleteServer;
        miAddServer.Click += OnAddServer;
        miRenameGroup.Click += OnRenameGroup;
        miDeleteGroup.Click += OnDeleteGroup;

        tree.AfterSelect += (_, _) => UpdateButtonStates();
        tree.NodeMouseClick += OnNodeMouseClick;
        tree.NodeMouseDoubleClick += OnNodeMouseDoubleClick;
        tree.BeforeLabelEdit += OnBeforeLabelEdit;
        tree.AfterLabelEdit += OnAfterLabelEdit;
        tree.ItemDrag += OnItemDrag;
        tree.DragOver += OnDragOver;
        tree.DragDrop += OnDragDrop;
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
        string? keepServerId = (tree.SelectedNode?.Tag as TreeItem)?.Server?.Id;

        tree.BeginUpdate();
        try
        {
            tree.Nodes.Clear();
            _serverNodes.Clear();

            foreach (ConnectionGroup group in _data.Groups)
            {
                var groupNode = new TreeNode(group.Name) { Tag = new TreeItem(group, null) };
                foreach (ConnectionServer server in group.Servers)
                {
                    var serverNode = new TreeNode(server.Name) { Tag = new TreeItem(group, server) };
                    ApplyLatencyTooltip(serverNode, GetLatency(server.Id));
                    _serverNodes[server.Id] = serverNode;
                    groupNode.Nodes.Add(serverNode);
                }

                tree.Nodes.Add(groupNode);
            }

            tree.ApplyNodeImages();
            tree.ExpandAll();
        }
        finally
        {
            tree.EndUpdate();
        }

        if (keepServerId is not null && _serverNodes.TryGetValue(keepServerId, out TreeNode? node))
        {
            tree.SelectedNode = node;
        }

        tree.Invalidate();
        UpdateButtonStates();
    }

    /// <summary>按树上现有顺序回写模型并保存。</summary>
    private void PersistTreeOrder()
    {
        var groups = new List<ConnectionGroup>();
        foreach (TreeNode groupNode in tree.Nodes)
        {
            if (groupNode.Tag is not TreeItem item)
            {
                continue;
            }

            item.Group.Servers = groupNode.Nodes
                .OfType<TreeNode>()
                .Select(n => (n.Tag as TreeItem)?.Server)
                .Where(s => s is not null)
                .Cast<ConnectionServer>()
                .ToList();
            groups.Add(item.Group);
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

    private TreeItem? SelectedItem => tree.SelectedNode?.Tag as TreeItem;

    private void SelectServer(string serverId)
    {
        if (_serverNodes.TryGetValue(serverId, out TreeNode? node))
        {
            tree.SelectedNode = node;
            node.EnsureVisible();
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
            MessageBox.Show(this, "分组名称不能为空。", "新增分组", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (ConnectionData.GroupNameExists(_data, name))
        {
            MessageBox.Show(this, $"分组“{name}”已存在。", "新增分组", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _data.Groups.Add(new ConnectionGroup { Name = name });
        SaveData();
        RebuildTree();
    }

    private void OnRenameGroup(object? sender, EventArgs e)
    {
        if (SelectedItem is { IsGroup: true })
        {
            tree.SelectedNode?.BeginEdit();
        }
    }

    private void OnDeleteGroup(object? sender, EventArgs e)
    {
        if (SelectedItem is not { Group: { } group })
        {
            return;
        }

        if (string.Equals(group.Name, ConnectionData.DefaultGroupName, StringComparison.Ordinal))
        {
            MessageBox.Show(this, "默认分组不能删除。", "删除分组", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string message = group.Servers.Count == 0
            ? $"确定删除分组“{group.Name}”吗？"
            : $"确定删除分组“{group.Name}”吗？组内 {group.Servers.Count} 台服务器将移入默认分组。";
        if (MessageBox.Show(this, message, "删除分组", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
        {
            return;
        }

        ConnectionData.RemoveGroup(_data, group);
        SaveData();
        RebuildTree();
    }

    private void OnBeforeLabelEdit(object? sender, NodeLabelEditEventArgs e)
    {
        if (e.Node?.Tag is not TreeItem { IsGroup: true })
        {
            e.CancelEdit = true;
        }
    }

    private void OnAfterLabelEdit(object? sender, NodeLabelEditEventArgs e)
    {
        if (e.Label is null || e.Node?.Tag is not TreeItem { Group: { } group })
        {
            e.CancelEdit = true;
            return;
        }

        string name = e.Label.Trim();
        if (name.Length == 0)
        {
            e.CancelEdit = true;
            MessageBox.Show(this, "分组名称不能为空。", "重命名分组", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (ConnectionData.GroupNameExists(_data, name, group))
        {
            e.CancelEdit = true;
            MessageBox.Show(this, $"分组“{name}”已存在。", "重命名分组", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        group.Name = name;
        SaveData();
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
            MessageBox.Show(this, "请先选择一个服务器。", "编辑服务器", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show(this, $"无法解密已保存的密码，请重新输入。\n{ex.Message}", "编辑服务器", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        if (MessageBox.Show(this, $"确定删除服务器“{server.Name}”吗？", "删除服务器", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
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

    private void OnNodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Button != MouseButtons.Right || e.Node is null)
        {
            return;
        }

        tree.SelectedNode = e.Node;
        if (e.Node.Tag is not TreeItem item)
        {
            return;
        }

        if (item.IsGroup)
        {
            miDeleteGroup.Enabled = !string.Equals(item.Group.Name, ConnectionData.DefaultGroupName, StringComparison.Ordinal);
            groupMenu.Show(tree, e.Location);
        }
        else
        {
            serverMenu.Show(tree, e.Location);
        }
    }

    private void OnNodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node?.Tag is TreeItem { Server: { } server })
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
            MessageBox.Show(this, "请先选择一个服务器。", "连接", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                    MessageBox.Show(this, $"无法解密已保存的密码，将改为手动输入。\n{ex.Message}", "连接", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            ConnectOutcome outcome = await _launcher.ConnectAsync(server, plainPassword);
            if (!outcome.Started)
            {
                MessageBox.Show(this, outcome.Message ?? "连接失败。", "连接失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else if (!string.IsNullOrEmpty(outcome.Message))
            {
                MessageBox.Show(this, outcome.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

    private LatencyBadge? GetLatencyBadge(TreeNode node)
    {
        if (node.Tag is not TreeItem { Server: { } server })
        {
            return null;
        }

        return _latency.TryGetValue(server.Id, out LatencyResult result)
            ? result.State switch
            {
                LatencyState.Pending => new LatencyBadge("…", SystemColors.GrayText),
                LatencyState.Online => new LatencyBadge($"{result.Milliseconds} ms", SystemColors.ControlText),
                LatencyState.Offline => new LatencyBadge("离线", Color.Firebrick),
                _ => null,
            }
            : null;
    }

    private static void ApplyLatencyTooltip(TreeNode node, LatencyResult? result)
    {
        node.ToolTipText = result is { State: LatencyState.Offline, Detail: not null } value ? value.Detail : string.Empty;
    }

    private void RefreshLatency()
    {
        _scanCancellation?.Cancel();
        _scanCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _scanCancellation = cancellation;
        int generation = ++_scanGeneration;

        List<ConnectionServer> servers = ConnectionData.AllServers(_data).ToList();
        foreach (ConnectionServer server in servers)
        {
            _latency[server.Id] = new LatencyResult(LatencyState.Pending, 0, null);
            if (_serverNodes.TryGetValue(server.Id, out TreeNode? node))
            {
                node.ToolTipText = string.Empty;
            }
        }

        tree.Invalidate();
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
            if (_serverNodes.TryGetValue(update.ServerId, out TreeNode? node))
            {
                ApplyLatencyTooltip(node, update.Result);
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

    private void OnItemDrag(object? sender, ItemDragEventArgs e)
    {
        if (e.Item is not TreeNode node)
        {
            return;
        }

        _dragNode = node;
        try
        {
            tree.DoDragDrop(node, DragDropEffects.Move);
        }
        finally
        {
            _dragNode = null;
            _dropNode = null;
            _dropMode = DropInsertMode.None;
            tree.ClearDropIndicator();
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.Effect = DragDropEffects.None;
        if (_dragNode is null)
        {
            return;
        }

        Point point = tree.PointToClient(new Point(e.X, e.Y));
        TreeNode? target = tree.GetNodeAt(point);
        DropInsertMode mode = ResolveDropMode(_dragNode, target, point);
        if (mode == DropInsertMode.None || target is null)
        {
            _dropNode = null;
            _dropMode = DropInsertMode.None;
            tree.ClearDropIndicator();
            return;
        }

        _dropNode = target;
        _dropMode = mode;
        e.Effect = DragDropEffects.Move;
        tree.SetDropIndicator(target, mode);
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        TreeNode? dragged = _dragNode;
        TreeNode? target = _dropNode;
        DropInsertMode mode = _dropMode;

        _dropNode = null;
        _dropMode = DropInsertMode.None;
        tree.ClearDropIndicator();

        if (dragged is null || target is null || mode == DropInsertMode.None)
        {
            return;
        }

        if (mode == DropInsertMode.AsChild)
        {
            dragged.Remove();
            target.Nodes.Add(dragged);
            target.Expand();
        }
        else
        {
            TreeNodeCollection siblings = target.Parent is null ? tree.Nodes : target.Parent.Nodes;
            bool sameParent = ReferenceEquals(target.Parent, dragged.Parent);
            int index = target.Index + (mode == DropInsertMode.After ? 1 : 0);
            if (sameParent && dragged.Index < target.Index)
            {
                index--;
            }

            dragged.Remove();
            siblings.Insert(index, dragged);
        }

        PersistTreeOrder();
        tree.SelectedNode = dragged;
        dragged.EnsureVisible();
    }

    private static DropInsertMode ResolveDropMode(TreeNode dragged, TreeNode? target, Point point)
    {
        if (target is null || ReferenceEquals(target, dragged))
        {
            return DropInsertMode.None;
        }

        if (dragged.Tag is not TreeItem draggedItem || target.Tag is not TreeItem targetItem)
        {
            return DropInsertMode.None;
        }

        // 分组只能在分组之间并列排序，不能放入分组内部。
        if (draggedItem.IsGroup)
        {
            if (!targetItem.IsGroup)
            {
                return DropInsertMode.None;
            }

            return point.Y < target.Bounds.Top + (target.Bounds.Height / 2) ? DropInsertMode.Before : DropInsertMode.After;
        }

        // 服务器拖到分组上：放入该分组末尾。
        if (targetItem.IsGroup)
        {
            return DropInsertMode.AsChild;
        }

        // 服务器之间：在目标所在分组内排序，或移动到目标所在分组。
        DropInsertMode vertical = point.Y < target.Bounds.Top + (target.Bounds.Height / 2) ? DropInsertMode.Before : DropInsertMode.After;
        if (ReferenceEquals(target.Parent, dragged.Parent))
        {
            int draggedIndex = dragged.Index;
            int targetIndex = target.Index;
            if (targetIndex == draggedIndex
                || (vertical == DropInsertMode.Before && targetIndex == draggedIndex + 1)
                || (vertical == DropInsertMode.After && targetIndex == draggedIndex - 1))
            {
                return DropInsertMode.None;
            }
        }

        return vertical;
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
                MessageBox.Show(this, "导入已中止，未保存任何更改。", "导入连接", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SaveData();
            RebuildTree();
            RefreshLatency();
            MessageBox.Show(
                this,
                $"导入完成：新增 {result.ServersAdded} 台，覆盖 {result.ServersOverwritten} 台，跳过 {result.ServersSkipped} 台。",
                "导入连接",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
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
                ? $"导出完成。\n其中 {result.ServersSkipped} 台服务器的密码无法解密，未包含在导出文件中。"
                : "导出完成。";
            MessageBox.Show(this, message, "导出连接", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowError("导出失败", ex);
        }
    }

    private ConflictAction ResolveConflict(ConnectionGroup group, ConnectionServer existing, ConnectionServer incoming)
    {
        DialogResult result = MessageBox.Show(
            this,
            $"分组“{group.Name}”中已存在服务器“{existing.Name}”。\n\n是：用导入的数据覆盖\n否：跳过该服务器\n取消：中止导入",
            "服务器重名",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);

        return result switch
        {
            DialogResult.Yes => ConflictAction.Overwrite,
            DialogResult.No => ConflictAction.Skip,
            _ => ConflictAction.Cancel,
        };
    }

    #endregion

    private void ShowError(string title, Exception exception)
    {
        MessageBox.Show(this, exception.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private readonly record struct LatencyUpdate(int Generation, string ServerId, LatencyResult Result);
}
