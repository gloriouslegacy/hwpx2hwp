using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Hwpx2Hwp.Core.Batch;
using Hwpx2Hwp.Core.Collect;
using Hwpx2Hwp.Core.Logging;
using Hwpx2Hwp.Core.Model;

namespace Hwpx2Hwp.App;

/// <summary>
/// 메인 창: 끌어다 놓기·[추가]·[변환]·[취소]·[로그 열기] 를 Core 에 연결하고 진행 이벤트를 표시한다(F2~F6, D-8).
/// 목록은 InputCollector, 파일별 상태는 마지막으로 받은 BatchEvent 사본으로만 그린다.
/// </summary>
internal sealed class MainForm : Form
{
    private const string Caption = "hwpx2hwp";
    private const string DefaultOutputText = "원본과 같은 폴더";

    private readonly BatchRunner _runner;
    private readonly string _engineName;
    private readonly MainFormLayout _ui = new();
    private readonly InputCollector _collector = new(ConversionRoute.HwpxToHwp);
    private readonly Dictionary<InputItem, BatchEvent> _lastEvents = [];
    private readonly Dictionary<InputItem, ListViewItem> _rows = [];

    private string? _outputFolder;
    private string? _lastLogPath;
    private CancellationTokenSource? _cts;
    private bool _collecting;
    private bool _closeRequested;

    public MainForm(BatchRunner runner, string engineName)
    {
        _runner = runner;
        _engineName = engineName;

        Text = Caption;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        ClientSize = new Size(840, 520);
        MinimumSize = new Size(560, 360);
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(_ui.Root);

        _ui.AddFilesButton.Click += OnAddFilesClick;
        _ui.AddFolderButton.Click += OnAddFolderClick;
        _ui.RemoveButton.Click += OnRemoveClick;
        _ui.ClearButton.Click += OnClearClick;
        _ui.BrowseOutputButton.Click += OnBrowseOutputClick;
        _ui.DefaultOutputButton.Click += (_, _) => SetOutputFolder(null);
        _ui.ConvertButton.Click += OnConvertClick;
        _ui.CancelButton.Click += (_, _) => _cts?.Cancel();
        _ui.OpenLogButton.Click += OnOpenLogClick;
        EnableDrop(this);

        SetOutputFolder(null);
        UpdateControls();
    }

    private bool Running => _cts is not null;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _ui.SizeColumns(LogicalToDeviceUnits);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (!Running && !_collecting)
        {
            return;
        }

        // 실행·수집 중에는 바로 닫지 않는다. 변환이면 확인 후 취소를 요청하고, 끝난 뒤 닫는다.
        e.Cancel = true;
        if (_closeRequested)
        {
            return;
        }

        if (Running && MessageBox.Show(
                this, "변환 중입니다. 지금 파일까지만 처리하고 멈춘 뒤 닫을까요?", Caption,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        _closeRequested = true;
        _cts?.Cancel();
        _ui.StatusLabel.Text = "끝나는 대로 닫습니다…";
    }

    private void EnableDrop(Control control)
    {
        control.AllowDrop = true;
        control.DragEnter += OnDragEnter;
        control.DragDrop += OnDragDrop;
        foreach (Control child in control.Controls)
        {
            EnableDrop(child);
        }
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        var accept = !Running && !_collecting && e.Data?.GetDataPresent(DataFormats.FileDrop) == true;
        e.Effect = accept ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private async void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
        {
            await CollectAsync(paths);
        }
    }

    private async void OnAddFilesClick(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "한/글 hwpx 문서 (*.hwpx)|*.hwpx|모든 파일 (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            await CollectAsync(dialog.FileNames);
        }
    }

    private async void OnAddFolderClick(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog { Description = "hwpx 파일을 찾을 폴더(하위 폴더 포함)" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            await CollectAsync([dialog.SelectedPath]);
        }
    }

    /// <summary>수집은 UI 스레드 밖에서 한다(D-8). 수집 중에는 목록을 바꾸는 조작을 막는다.</summary>
    private async Task CollectAsync(IReadOnlyList<string> paths)
    {
        if (Running || _collecting)
        {
            return;
        }

        _collecting = true;
        UpdateControls();
        _ui.StatusLabel.Text = "파일을 모으는 중…";
        var skipped = false;
        try
        {
            await Task.Run(() => _collector.Add(paths));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException
                                       or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // 복구(방어선): 이상한 경로 하나로 앱이 crash 처리되지 않게, 그때까지 모은 목록을 두고 계속한다.
            // PathTooLongException 은 IOException 에 포함된다. 경로 문자열은 기록하지 않는다.
            Trace.TraceWarning("collect skipped unreadable path: type={0}", ex.GetType().Name);
            skipped = true;
        }
        finally
        {
            _collecting = false;
            RefreshList();
            _ui.StatusLabel.Text = string.Format(CultureInfo.CurrentCulture, "목록 {0}개", _collector.Items.Count)
                + (skipped ? " — 읽을 수 없는 경로가 있어 건너뜀" : string.Empty);
            UpdateControls();
            CloseIfRequested();
        }
    }

    private void OnRemoveClick(object? sender, EventArgs e)
    {
        foreach (ListViewItem row in _ui.FileList.SelectedItems)
        {
            if (row.Tag is InputItem item)
            {
                _collector.Remove(item);
                _lastEvents.Remove(item);
            }
        }

        RefreshList();
        UpdateControls();
    }

    private void OnClearClick(object? sender, EventArgs e)
    {
        _collector.Clear();
        _lastEvents.Clear();
        RefreshList();
        UpdateControls();
    }

    private void OnBrowseOutputClick(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog { Description = "변환한 hwp 를 저장할 폴더" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SetOutputFolder(dialog.SelectedPath);
        }
    }

    private void SetOutputFolder(string? folder)
    {
        _outputFolder = folder;
        _ui.OutputFolderText.Text = folder ?? DefaultOutputText;
        UpdateControls();
    }

    private async void OnConvertClick(object? sender, EventArgs e)
    {
        if (Running || _collecting || _collector.Items.Count == 0)
        {
            return;
        }

        // 다시 [변환]하면 마지막 이벤트가 성공인 항목은 빼고 나머지(실패·대기)만 넘긴다(F4).
        // 행 매핑은 InputItem 기준(_rows)이라 JobNumber 가 이번 실행 기준이어도 꼬이지 않는다.
        var items = _collector.Items.Where(item => !IsSucceeded(item)).ToList();
        if (items.Count == 0)
        {
            _ui.StatusLabel.Text = "변환할 파일이 없음(모두 성공)";
            return;
        }

        foreach (var item in items)
        {
            _lastEvents.Remove(item);
        }

        RefreshList();
        _ui.Progress.Maximum = items.Count;
        _ui.Progress.Value = 0;
        _ui.StatusLabel.Text = string.Format(CultureInfo.CurrentCulture, "변환 중 0/{0}", items.Count);
        using var cts = new CancellationTokenSource();
        _cts = cts;
        UpdateControls();

        var options = new BatchOptions(ConversionRoute.HwpxToHwp, _outputFolder, _engineName);
        var progress = new Progress<BatchEvent>(OnBatchEvent);
        try
        {
            var report = await _runner.RunAsync(items, options, progress, cts.Token);
            ShowReport(report);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 번역: 로그 파일을 만들지 못하면 변환을 시작하지 않는다(RunAsync 계약). 사용자에게 이유를 알린다.
            _ui.StatusLabel.Text = "로그 파일을 만들지 못해 변환하지 않았습니다.";
            MessageBox.Show(this, "로그 파일을 만들지 못해 변환하지 않았습니다.\n" + ex.Message, Caption,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _cts = null;
            UpdateControls();
            CloseIfRequested();
        }
    }

    private bool IsSucceeded(InputItem item) =>
        _lastEvents.TryGetValue(item, out var ev) && ev.State == JobState.Succeeded;

    private void OnBatchEvent(BatchEvent ev)
    {
        _lastEvents[ev.Input] = ev;
        if (_rows.TryGetValue(ev.Input, out var row))
        {
            ApplyEvent(row, ev);
            if (ev.State == JobState.Running)
            {
                row.EnsureVisible();
            }
        }

        _ui.Progress.Maximum = Math.Max(1, ev.Total);
        _ui.Progress.Value = Math.Clamp(ev.Completed, 0, _ui.Progress.Maximum);
        _ui.StatusLabel.Text = string.Format(CultureInfo.CurrentCulture, "변환 중 {0}/{1}", ev.Completed, ev.Total);
    }

    private void ShowReport(BatchReport report)
    {
        _lastLogPath = report.LogPath;
        var summary = string.Format(
            CultureInfo.CurrentCulture, "성공 {0} · 실패 {1} · 대기 {2}{3}",
            report.Succeeded, report.Failed, report.Pending, report.Cancelled ? " (취소됨)" : string.Empty);
        if (report.Failed > 0)
        {
            summary += string.Format(CultureInfo.CurrentCulture, " — 실패 {0}건은 목록과 로그 참고", report.Failed);
        }

        _ui.StatusLabel.Text = summary;

        // 닫기를 요청해 취소한 경우에는 안내 창을 띄우지 않고 닫는다(안내는 로그에 이미 있다).
        if (report.Notices.Count > 0 && !_closeRequested)
        {
            MessageBox.Show(this, string.Join("\n\n", report.Notices), Caption,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void OnOpenLogClick(object? sender, EventArgs e)
    {
        string target;
        if (_lastLogPath is not null && File.Exists(_lastLogPath))
        {
            target = _lastLogPath;
        }
        else if (Directory.Exists(AppPaths.LogDirectory))
        {
            target = AppPaths.LogDirectory;
        }
        else
        {
            MessageBox.Show(this, "아직 로그가 없습니다. 변환을 한 번 실행하면 생깁니다.", Caption,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Win32Exception ex)
        {
            // 번역: 연결 프로그램이 없거나 셸이 거부하면 경로를 보여 주고 끝낸다.
            MessageBox.Show(this, "로그를 열지 못했습니다.\n" + target + "\n" + ex.Message, Caption,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void RefreshList()
    {
        var list = _ui.FileList;
        list.BeginUpdate();
        try
        {
            list.Items.Clear();
            _rows.Clear();
            foreach (var item in _collector.Items)
            {
                var row = new ListViewItem([item.SourcePath, string.Empty, string.Empty]) { Tag = item };
                if (_lastEvents.TryGetValue(item, out var ev))
                {
                    ApplyEvent(row, ev);
                }
                else
                {
                    row.SubItems[1].Text = StateText(JobState.Pending);
                }

                _rows[item] = row;
                list.Items.Add(row);
            }
        }
        finally
        {
            list.EndUpdate();
        }
    }

    private static void ApplyEvent(ListViewItem row, BatchEvent ev)
    {
        row.SubItems[1].Text = StateText(ev.State);
        row.SubItems[2].Text = ev.State switch
        {
            JobState.Succeeded => ev.OutputPath ?? string.Empty,
            JobState.Failed => FailureText(ev.Outcome),
            _ => string.Empty,
        };
    }

    private static string FailureText(ConversionOutcome? outcome)
    {
        var text = FailureReasonText.Get(outcome?.FailureReason ?? FailureReason.Unexpected);
        return string.IsNullOrEmpty(outcome?.Detail) ? text : text + " (" + outcome.Detail + ")";
    }

    private static string StateText(JobState state) => state switch
    {
        JobState.Pending => "대기",
        JobState.Running => "진행",
        JobState.Succeeded => "성공",
        JobState.Failed => "실패",
        _ => state.ToString(),
    };

    private void UpdateControls()
    {
        var idle = !Running && !_collecting;
        _ui.AddFilesButton.Enabled = idle;
        _ui.AddFolderButton.Enabled = idle;
        _ui.RemoveButton.Enabled = idle && _collector.Items.Count > 0;
        _ui.ClearButton.Enabled = idle && _collector.Items.Count > 0;
        _ui.BrowseOutputButton.Enabled = idle;
        _ui.DefaultOutputButton.Enabled = idle && _outputFolder is not null;
        _ui.ConvertButton.Enabled = idle && _collector.Items.Count > 0;
        _ui.CancelButton.Enabled = Running;
    }

    private void CloseIfRequested()
    {
        if (_closeRequested && !Running && !_collecting)
        {
            BeginInvoke(Close);
        }
    }
}
