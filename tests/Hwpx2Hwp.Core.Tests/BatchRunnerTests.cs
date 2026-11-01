using System.IO.Compression;
using System.Text;
using Hwpx2Hwp.Core.Batch;
using Hwpx2Hwp.Core.Engine;
using Hwpx2Hwp.Core.Model;
using Hwpx2Hwp.Core.Tests.Fakes;

namespace Hwpx2Hwp.Core.Tests;

/// <summary>
/// F4 일괄 실행(T4.1)과 파일별 흐름(X_Batch). D-5. 가짜 엔진 + 임시 폴더의 합성 hwpx.
/// 테스트마다 새 임시 폴더를 만들고 끝나면 지운다.
/// </summary>
public sealed class BatchRunnerTests : IDisposable
{
    private const string OdfManifestNs = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";

    private static readonly string PlainManifest =
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <manifest:manifest xmlns:manifest="{OdfManifestNs}">
          <manifest:file-entry manifest:full-path="/" manifest:media-type="application/hwp+zip"/>
        </manifest:manifest>
        """;

    private static readonly string EncryptedManifest =
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <manifest:manifest xmlns:manifest="{OdfManifestNs}">
          <manifest:file-entry manifest:full-path="Contents/section0.xml" manifest:media-type="application/xml">
            <manifest:encryption-data manifest:checksum-type="SHA1" manifest:checksum="AAAA"/>
          </manifest:file-entry>
        </manifest:manifest>
        """;

    private static readonly BatchOptions Options = new(ConversionRoute.HwpxToHwp, null, "fake");

    private readonly string _dir;
    private readonly string _logDir;
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 9, 30, 0, TimeSpan.Zero));

    public BatchRunnerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "hwpx2hwp-batch-" + Guid.NewGuid().ToString("N"));
        _logDir = Path.Combine(_dir, "logs");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task T4_1_5개_중_3번째_실패해도_나머지_4개_성공()
    {
        var items = MakeItems("a", "b", "c", "d", "e");
        var engine = new FakeConversionEngine { FailAt = 3 };

        var (report, events) = await RunAsync(engine, items);

        Assert.Equal(4, report.Succeeded);
        Assert.Equal(1, report.Failed);
        Assert.Equal(0, report.Pending);
        Assert.False(report.Cancelled);
        var failure = Assert.Single(report.Failures);
        Assert.Equal(items[2].SourcePath, failure.SourcePath);
        Assert.Equal(5, engine.Jobs.Count);

        var failedEvent = Assert.Single(events, e => e.State == JobState.Failed);
        Assert.Equal(3, failedEvent.JobNumber);
        Assert.Equal(4, events.Count(e => e.State == JobState.Succeeded));
        Assert.Equal(5, events.Count(e => e.State == JobState.Running));
        Assert.Equal(5, events[^1].Completed);
        Assert.True(File.Exists(report.LogPath));
    }

    [Fact]
    public async Task X_Batch_1_암호_hwpx_는_엔진_호출_없이_Encrypted_나머지_계속()
    {
        var items = new List<InputItem>
        {
            Item(MakeHwpx("a", PlainManifest)),
            Item(MakeHwpx("locked", EncryptedManifest)),
            Item(MakeHwpx("c", PlainManifest)),
        };
        var engine = new FakeConversionEngine();

        var (report, _) = await RunAsync(engine, items);

        Assert.Equal(2, report.Succeeded);
        var failure = Assert.Single(report.Failures);
        Assert.Equal(FailureReason.Encrypted, failure.Reason);
        Assert.Equal(items[1].SourcePath, failure.SourcePath);
        Assert.Equal(2, engine.Jobs.Count);
        Assert.DoesNotContain(engine.Jobs, j => j.Input == items[1]);
    }

    [Fact]
    public async Task X_Batch_2_원본이_없으면_InputMissing()
    {
        var items = new List<InputItem> { Item(Path.Combine(_dir, "gone.hwpx")), Item(MakeHwpx("b", PlainManifest)) };
        var engine = new FakeConversionEngine();

        var (report, _) = await RunAsync(engine, items);

        Assert.Equal(1, report.Succeeded);
        Assert.Equal(FailureReason.InputMissing, Assert.Single(report.Failures).Reason);
        Assert.Single(engine.Jobs);
    }

    [Fact]
    public async Task X_Batch_3_엔진_예외는_그_파일만_Unexpected_나머지_성공()
    {
        var items = MakeItems("a", "b", "c");
        var engine = new FakeConversionEngine { ThrowAt = 2 };

        var (report, events) = await RunAsync(engine, items);

        Assert.Equal(2, report.Succeeded);
        var failure = Assert.Single(report.Failures);
        Assert.Equal(FailureReason.Unexpected, failure.Reason);
        Assert.Equal(nameof(InvalidOperationException), failure.Detail);
        Assert.Equal(items[1].SourcePath, failure.SourcePath);
        Assert.Equal(2, Assert.Single(events, e => e.State == JobState.Failed).JobNumber);
    }

    [Fact]
    public async Task X_Batch_4_엔진_사용_불가면_변환_0회_전부_Pending_안내()
    {
        var items = MakeItems("a", "b", "c");
        var engine = new FakeConversionEngine { UnavailableMessage = "한/글이 설치되어 있지 않음" };

        var (report, events) = await RunAsync(engine, items);

        Assert.Empty(engine.Jobs);
        Assert.Equal(3, report.Pending);
        Assert.Equal(0, report.Succeeded);
        Assert.Equal(0, report.Failed);
        Assert.False(report.Cancelled);
        Assert.Contains("한/글이 설치되어 있지 않음", report.Notices);
        Assert.Empty(events);
        Assert.Contains("NOTE\t한/글이 설치되어 있지 않음", File.ReadAllText(report.LogPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task X_Batch_5_기존_a_hwp_가_있으면_a_1_hwp_로_작업하고_기존_파일은_그대로()
    {
        var items = MakeItems("a");
        var existing = Path.Combine(_dir, "a.hwp");
        File.WriteAllText(existing, "old");
        var engine = new FakeConversionEngine();

        var (report, _) = await RunAsync(engine, items);

        Assert.Equal(1, report.Succeeded);
        Assert.Equal(Path.Combine(_dir, "a_1.hwp"), Assert.Single(engine.Jobs).OutputPath);
        Assert.Equal("old", File.ReadAllText(existing));
    }

    [Fact]
    public async Task X_Batch_6_실행_전에_취소되면_변환_0회_전부_Pending_Cancelled()
    {
        var items = MakeItems("a", "b");
        var engine = new FakeConversionEngine();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var (report, _) = await RunAsync(engine, items, ct: cts.Token);

        Assert.Empty(engine.Jobs);
        Assert.Equal(2, report.Pending);
        Assert.True(report.Cancelled);
    }

    [Fact]
    public async Task X_Batch_7_원본_hwpx_바이트는_실행_전후_같음()
    {
        var items = new List<InputItem>
        {
            Item(MakeHwpx("a", PlainManifest)),
            Item(MakeHwpx("locked", EncryptedManifest)),
            Item(MakeHwpx("c", PlainManifest)),
        };
        var before = items.Select(i => File.ReadAllBytes(i.SourcePath)).ToList();
        var engine = new FakeConversionEngine { FailAt = 2 };

        await RunAsync(engine, items);

        for (var i = 0; i < items.Count; i++)
        {
            Assert.Equal(before[i], File.ReadAllBytes(items[i].SourcePath));
        }
    }

    [Fact]
    public async Task X_Batch_8_출력_폴더_경로가_잘못되면_예외_없이_전부_SaveFailed_엔진_시작_안_함()
    {
        var items = MakeItems("a", "b");
        var engine = new FakeConversionEngine();
        var options = Options with { OutputFolder = Path.Combine(_dir, "bad\0dir") };

        var (report, events) = await RunAsync(engine, items, options: options);

        Assert.Equal(2, report.Failed);
        Assert.Equal(0, report.Succeeded);
        Assert.Equal(0, report.Pending);
        Assert.All(report.Failures, f => Assert.Equal(FailureReason.SaveFailed, f.Reason));
        Assert.All(report.Failures, f => Assert.False(string.IsNullOrEmpty(f.Detail)));
        Assert.Equal(0, engine.StartCount);
        Assert.Empty(engine.Jobs);
        Assert.Equal(2, events.Count(e => e.State == JobState.Failed));
        Assert.Contains("\tEND\t", File.ReadAllText(report.LogPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task X_Batch_9_손상_zip_과_텍스트_파일은_엔진_호출_없이_OpenFailed()
    {
        var broken = Path.Combine(_dir, "broken.hwpx");
        File.WriteAllBytes(broken, [0x50, 0x4B, 0x03, 0x04, 0x01, 0x02, 0x03]);
        var text = Path.Combine(_dir, "text.hwpx");
        File.WriteAllText(text, "hwpx 가 아닌 텍스트");
        var items = new List<InputItem> { Item(broken), Item(text) };
        var engine = new FakeConversionEngine();

        var (report, _) = await RunAsync(engine, items);

        Assert.Equal(2, report.Failed);
        Assert.All(report.Failures, f => Assert.Equal(FailureReason.OpenFailed, f.Reason));
        Assert.Empty(engine.Jobs);
    }

    [Fact]
    public async Task X_Batch_10_엔진_시작_예외면_전부_Pending_안내_ERROR_END()
    {
        var items = MakeItems("a", "b", "c");
        var engine = new StartOnlyEngine(_ => throw new InvalidOperationException("시작 실패"));

        var (report, events) = await RunAsync(engine, items);

        Assert.Equal(3, report.Pending);
        Assert.Equal(0, report.Succeeded + report.Failed);
        Assert.False(report.Cancelled);
        Assert.NotEmpty(report.Notices);
        Assert.Equal(0, engine.ConvertCount);
        Assert.Empty(events);
        var log = File.ReadAllText(report.LogPath);
        Assert.Contains("\tERROR\t", log, StringComparison.Ordinal);
        Assert.Contains("\tEND\t", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task X_Batch_11_엔진_시작_중_취소되면_Cancelled_안내_없음()
    {
        var items = MakeItems("a", "b");
        using var cts = new CancellationTokenSource();
        var engine = new StartOnlyEngine(async ct =>
        {
            await cts.CancelAsync();
            ct.ThrowIfCancellationRequested();
            return new EngineStart(true, null, []);
        });

        var (report, _) = await RunAsync(engine, items, ct: cts.Token);

        Assert.True(report.Cancelled);
        Assert.Equal(2, report.Pending);
        Assert.Empty(report.Notices);
        Assert.Equal(0, engine.ConvertCount);
        var log = File.ReadAllText(report.LogPath);
        Assert.DoesNotContain("\tERROR\t", log, StringComparison.Ordinal);
        Assert.Contains("SKIP\t#0001\t" + items[0].SourcePath + "\t-\t취소됨", log, StringComparison.Ordinal);
        Assert.Contains("cancelled=true", log, StringComparison.Ordinal);
    }

    private async Task<(BatchReport Report, IReadOnlyList<BatchEvent> Events)> RunAsync(
        IConversionEngine engine, IReadOnlyList<InputItem> items, BatchOptions? options = null, CancellationToken ct = default)
    {
        var progress = new ListProgress();
        var runner = new BatchRunner(engine, _logDir, _time);
        var report = await runner.RunAsync(items, options ?? Options, progress, ct);
        return (report, progress.Snapshot());
    }

    private List<InputItem> MakeItems(params string[] names) =>
        names.Select(n => Item(MakeHwpx(n, PlainManifest))).ToList();

    private static InputItem Item(string path) => new(path, null);

    private string MakeHwpx(string name, string manifestXml)
    {
        var path = Path.Combine(_dir, name + ".hwpx");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            WriteEntry(zip, "mimetype", "application/hwp+zip");
            WriteEntry(zip, "Contents/section0.xml", "<sec/>");
            WriteEntry(zip, "META-INF/manifest.xml", manifestXml);
        }

        return path;
    }

    private static void WriteEntry(ZipArchive zip, string entryName, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(entryName).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    /// <summary>동기 수집 IProgress. Progress&lt;T&gt; 는 비동기로 보고해 순서·완료 시점이 흔들린다.</summary>
    private sealed class ListProgress : IProgress<BatchEvent>
    {
        private readonly Lock _gate = new();
        private readonly List<BatchEvent> _events = [];

        public void Report(BatchEvent value)
        {
            lock (_gate)
            {
                _events.Add(value);
            }
        }

        public IReadOnlyList<BatchEvent> Snapshot()
        {
            lock (_gate)
            {
                return [.. _events];
            }
        }
    }

    /// <summary>StartAsync 동작만 주입하는 엔진(시작 예외·시작 중 취소 재현용). 변환은 항상 성공하고 횟수만 센다.</summary>
    private sealed class StartOnlyEngine(Func<CancellationToken, Task<EngineStart>> start) : IConversionEngine
    {
        public int ConvertCount { get; private set; }

        public Task<EngineStart> StartAsync(CancellationToken ct) => start(ct);

        public Task<ConversionOutcome> ConvertAsync(ConversionJob job, CancellationToken ct)
        {
            ConvertCount++;
            return Task.FromResult(ConversionOutcome.Success());
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>고정 시각 TimeProvider. 지역 시간대는 UTC 로 고정한다.</summary>
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
