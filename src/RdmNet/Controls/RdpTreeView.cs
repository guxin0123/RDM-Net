using System.ComponentModel;
using System.Runtime.InteropServices;
using RdmNet.Models;
using RdmNet.Utils;

namespace RdmNet.Controls;

/// <summary>条目右侧显示的延迟文本与颜色。</summary>
public readonly record struct LatencyBadge(string Text, Color Color);

public enum DropInsertMode
{
    None,

    Before,

    After,

    /// <summary>放入分组内部（作为最后一个子节点）。</summary>
    AsChild,
}

/// <summary>树节点承载的数据。</summary>
public sealed class TreeItem
{
    public TreeItem(ConnectionGroup group, ConnectionServer? server)
    {
        Group = group;
        Server = server;
    }

    public ConnectionGroup Group { get; }

    public ConnectionServer? Server { get; }

    public bool IsGroup => Server is null;
}

/// <summary>自绘 TreeView：在条目右侧右对齐显示延迟，并绘制拖放指示线。</summary>
public sealed class RdpTreeView : TreeView
{
    private const int LatencyColumnLogicalWidth = 72;
    private const int HostColumnLogicalWidth = 170;
    private const int GapLogicalWidth = 8;
    private const int RowRightMarginLogicalWidth = 4;
    private const int GwlStyle = -16;
    private const int WsVScroll = 0x00200000;
    private const int NodeIconLogicalSize = 16;
    private const string GroupImageKey = "group";
    private const string ServerImageKey = "server";

    private static readonly TextFormatFlags NameFormat =
        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

    private static readonly TextFormatFlags LatencyFormat =
        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

    private static readonly TextFormatFlags HostFormat =
        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

    private TreeNode? _dropNode;
    private DropInsertMode _dropMode = DropInsertMode.None;
    private SolidBrush? _rowBackBrush;

    public RdpTreeView()
    {
        DoubleBuffered = true;
        DrawMode = TreeViewDrawMode.OwnerDrawText;
        FullRowSelect = true;
        HideSelection = false;
        ShowNodeToolTips = true;
        LabelEdit = true;
        AllowDrop = true;
    }

    /// <summary>返回某个节点右侧要显示的延迟，null 表示不显示。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<TreeNode, LatencyBadge?>? LatencyProvider { get; set; }

    /// <summary>
    /// 按当前 DPI 构建（或重建）节点图标 ImageList。OwnerDrawText 模式下图标仍由原生控件绘制，
    /// 文字区域 e.Bounds 会自动右移，就地编辑标签时也能对齐。
    /// </summary>
    private void EnsureImageList()
    {
        int size = LogicalToDeviceUnits(NodeIconLogicalSize);
        if (ImageList is { ImageSize: { } existing } && existing.Width == size)
        {
            return;
        }

        var list = new ImageList
        {
            ColorDepth = ColorDepth.Depth32Bit,
            ImageSize = new Size(size, size),
        };

        Bitmap? folder = ToolbarIcons.Get(ToolbarIcons.Folder, size);
        Bitmap? server = ToolbarIcons.Get(ToolbarIcons.Server, size);
        if (folder is not null)
        {
            list.Images.Add(GroupImageKey, folder);
        }

        if (server is not null)
        {
            list.Images.Add(ServerImageKey, server);
        }

        ImageList? previous = ImageList;
        ImageList = list;
        previous?.Dispose();
    }

    /// <summary>为全部已有节点按类型分配图标；新增/重建节点后调用。</summary>
    public void ApplyNodeImages()
    {
        EnsureImageList();

        foreach (TreeNode root in Nodes)
        {
            ApplyNodeImages(root);
        }
    }

    private static void ApplyNodeImages(TreeNode node)
    {
        if (node.Tag is TreeItem item)
        {
            string key = item.IsGroup ? GroupImageKey : ServerImageKey;
            node.ImageKey = key;
            node.SelectedImageKey = key;
        }

        foreach (TreeNode child in node.Nodes)
        {
            ApplyNodeImages(child);
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        EnsureImageList();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        EnsureImageList();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);

        // 自绘的地址/延迟列位于原生文本区域之外，缩放时原生控件只重绘变化区域，
        // 旧位置的文字会残留形成残影；尺寸变化后强制整块失效重绘（DoubleBuffered 已开启，不会闪烁）。
        Invalidate();
    }

    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);
        _rowBackBrush?.Dispose();
        _rowBackBrush = null;
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _rowBackBrush?.Dispose();
        }

        base.Dispose(disposing);
    }

    public void SetDropIndicator(TreeNode? node, DropInsertMode mode)
    {
        if (ReferenceEquals(_dropNode, node) && _dropMode == mode)
        {
            return;
        }

        _dropNode = node;
        _dropMode = node is null ? DropInsertMode.None : mode;
        Invalidate();
    }

    public void ClearDropIndicator()
    {
        SetDropIndicator(null, DropInsertMode.None);
    }

    protected override void OnDrawNode(DrawTreeNodeEventArgs e)
    {
        if (e.Node is null)
        {
            return;
        }

        if (e.Node.Tag is not TreeItem item)
        {
            return;
        }

        bool selected = (e.State & TreeNodeStates.Selected) != 0;
        bool focused = (e.State & TreeNodeStates.Focused) != 0;
        Color textColor = selected && focused ? SystemColors.HighlightText : e.Node.ForeColor;

        // 分组行只绘制名称。
        if (item.IsGroup)
        {
            TextRenderer.DrawText(e.Graphics, e.Node.Text, Font, e.Bounds, textColor, NameFormat);
            DrawDropIndicator(e.Graphics, e.Node);
            return;
        }

        if (item.Server is not { } server)
        {
            TextRenderer.DrawText(e.Graphics, e.Node.Text, Font, e.Bounds, textColor, NameFormat);
            DrawDropIndicator(e.Graphics, e.Node);
            return;
        }

        LatencyBadge? badge = LatencyProvider?.Invoke(e.Node);

        int latencyWidth = LogicalToDeviceUnits(LatencyColumnLogicalWidth);
        int hostWidth = LogicalToDeviceUnits(HostColumnLogicalWidth);
        int gap = LogicalToDeviceUnits(GapLogicalWidth);
        int rightEdge = RowRightEdge;

        // 从右到左依次是延迟列、地址列，名称占据剩余空间。
        int trailingWidth = hostWidth + gap;
        if (badge is not null)
        {
            trailingWidth += latencyWidth + gap;
        }

        var nameBounds = new Rectangle(e.Bounds.Left, e.Bounds.Top, Math.Max(0, rightEdge - trailingWidth - e.Bounds.Left), e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, e.Node.Text, Font, nameBounds, textColor, NameFormat);

        // 地址/延迟列位于原生控件文本擦除范围之外，先用行背景色清空该区域，
        // 避免文字变短或缩放导致列位置移动后旧字形残留。
        int columnsLeft = nameBounds.Right;
        e.Graphics.FillRectangle(GetRowBackBrush(), columnsLeft, e.Bounds.Top, Math.Max(0, rightEdge - columnsLeft), e.Bounds.Height);

        // 地址列与延迟列都位于系统选中背景之外，地址固定灰色、延迟用语义颜色，
        // 避免节点获焦时白字落在白底上不可见。
        int hostRight = rightEdge - (badge is not null ? latencyWidth + gap : 0);
        var hostBounds = new Rectangle(hostRight - hostWidth, e.Bounds.Top, hostWidth, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, FormatHost(server), Font, hostBounds, SystemColors.GrayText, HostFormat);

        if (badge is not null)
        {
            var latencyBounds = new Rectangle(rightEdge - latencyWidth, e.Bounds.Top, latencyWidth, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, badge.Value.Text, Font, latencyBounds, badge.Value.Color, LatencyFormat);
        }

        DrawDropIndicator(e.Graphics, e.Node);
    }

    /// <summary>地址列文本：默认端口只显示地址，非默认端口附带端口号。</summary>
    private static string FormatHost(ConnectionServer server) =>
        server.Port == ConnectionServer.DefaultPort ? server.Host : $"{server.Host}:{server.Port}";

    /// <summary>行背景画刷（随 BackColor 变化重建）。</summary>
    private SolidBrush GetRowBackBrush() => _rowBackBrush ??= new SolidBrush(BackColor);

    /// <summary>
    /// OwnerDrawText 模式下 e.Bounds 只是文字区域而非整行，
    /// 因此按控件宽度（必要时扣除垂直滚动条）推算整行右边界。
    /// </summary>
    private int RowRightEdge
    {
        get
        {
            int right = ClientSize.Width;
            if ((GetWindowLong(Handle, GwlStyle) & WsVScroll) != 0)
            {
                right -= SystemInformation.VerticalScrollBarWidth;
            }

            return right - LogicalToDeviceUnits(RowRightMarginLogicalWidth);
        }
    }

    private void DrawDropIndicator(Graphics graphics, TreeNode node)
    {
        if (_dropMode == DropInsertMode.None || !ReferenceEquals(node, _dropNode))
        {
            return;
        }

        int thickness = LogicalToDeviceUnits(2);
        Rectangle bounds = node.Bounds;
        int rowRight = RowRightEdge;
        using var brush = new SolidBrush(SystemColors.Highlight);
        using var pen = new Pen(SystemColors.Highlight, thickness);
        switch (_dropMode)
        {
            case DropInsertMode.Before:
                graphics.FillRectangle(brush, bounds.Left, bounds.Top, Math.Max(0, rowRight - bounds.Left), thickness);
                break;
            case DropInsertMode.After:
                graphics.FillRectangle(brush, bounds.Left, bounds.Bottom - thickness, Math.Max(0, rowRight - bounds.Left), thickness);
                break;
            default:
                graphics.DrawRectangle(pen, bounds.Left, bounds.Top, Math.Max(0, bounds.Width - thickness), Math.Max(0, bounds.Height - thickness));
                break;
        }
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
}
