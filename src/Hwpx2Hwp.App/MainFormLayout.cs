namespace Hwpx2Hwp.App;

/// <summary>
/// 메인 창의 컨트롤 생성·배치(디자이너 파일 없이 코드로만). 이벤트 연결은 MainForm 이 한다. D-8.
/// 고DPI: 크기는 AutoSize·Dock·백분율 행/열로 정하고 고정 픽셀은 목록 열 너비(논리 단위)뿐이다.
/// </summary>
internal sealed class MainFormLayout
{
    /// <summary>목록 열 너비(96 DPI 논리 단위): 원본 파일 / 상태 / 결과.</summary>
    private static readonly int[] LogicalColumnWidths = [360, 70, 360];

    public MainFormLayout()
    {
        AddFilesButton = NewButton("파일 추가");
        AddFolderButton = NewButton("폴더 추가");
        RemoveButton = NewButton("빼기");
        ClearButton = NewButton("비우기");

        FileList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = true,
        };
        FileList.Columns.Add("원본 파일");
        FileList.Columns.Add("상태");
        FileList.Columns.Add("결과");

        OutputFolderText = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Anchor = AnchorStyles.Left | AnchorStyles.Right };
        BrowseOutputButton = NewButton("찾아보기");
        DefaultOutputButton = NewButton("기본으로");

        Progress = new ProgressBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 1 };
        StatusLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Text = "파일이나 폴더를 끌어다 놓으세요." };
        ConvertButton = NewButton("변환");
        CancelButton = NewButton("취소");
        OpenLogButton = NewButton("로그 열기");

        Root = BuildRoot();
    }

    public TableLayoutPanel Root { get; }

    public Button AddFilesButton { get; }

    public Button AddFolderButton { get; }

    public Button RemoveButton { get; }

    public Button ClearButton { get; }

    public ListView FileList { get; }

    public TextBox OutputFolderText { get; }

    public Button BrowseOutputButton { get; }

    public Button DefaultOutputButton { get; }

    public ProgressBar Progress { get; }

    public Label StatusLabel { get; }

    public Button ConvertButton { get; }

    public Button CancelButton { get; }

    public Button OpenLogButton { get; }

    /// <summary>목록 열 너비를 현재 DPI 로 맞춘다. toDevice 는 Control.LogicalToDeviceUnits.</summary>
    public void SizeColumns(Func<int, int> toDevice)
    {
        for (var i = 0; i < LogicalColumnWidths.Length; i++)
        {
            FileList.Columns[i].Width = toDevice(LogicalColumnWidths[i]);
        }
    }

    private static Button NewButton(string text) => new()
    {
        Text = text,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Padding = new Padding(6, 2, 6, 2),
    };

    private static FlowLayoutPanel NewButtonRow(params Control[] buttons)
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = Padding.Empty,
        };
        row.Controls.AddRange(buttons);
        return row;
    }

    private TableLayoutPanel BuildRoot()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(8),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(NewButtonRow(AddFilesButton, AddFolderButton, RemoveButton, ClearButton), 0, 0);
        root.Controls.Add(FileList, 0, 1);
        root.Controls.Add(BuildOutputRow(), 0, 2);
        root.Controls.Add(BuildBottom(), 0, 3);
        return root;
    }

    private TableLayoutPanel BuildOutputRow()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            RowCount = 1,
            Margin = Padding.Empty,
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        row.Controls.Add(new Label { Text = "출력 폴더:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        row.Controls.Add(OutputFolderText, 1, 0);
        row.Controls.Add(BrowseOutputButton, 2, 0);
        row.Controls.Add(DefaultOutputButton, 3, 0);
        return row;
    }

    private TableLayoutPanel BuildBottom()
    {
        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 2,
            Margin = Padding.Empty,
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        bottom.Controls.Add(Progress, 0, 0);
        bottom.SetColumnSpan(Progress, 2);
        bottom.Controls.Add(StatusLabel, 0, 1);
        bottom.Controls.Add(NewButtonRow(ConvertButton, CancelButton, OpenLogButton), 1, 1);
        return bottom;
    }
}
