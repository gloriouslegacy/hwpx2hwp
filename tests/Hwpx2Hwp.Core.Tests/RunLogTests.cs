using System.Text;
using Hwpx2Hwp.Core.Logging;
using Hwpx2Hwp.Core.Model;

namespace Hwpx2Hwp.Core.Tests;

/// <summary>F5 로그 형식(D-11)·FailureReasonText(D-4). 테스트마다 새 임시 폴더를 만들고 끝나면 지운다.</summary>
public sealed class RunLogTests : IDisposable
{
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 21, 9, 8, 7, 123, TimeSpan.Zero);

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "hwpx2hwp-tests-" + Guid.NewGuid().ToString("N"));

    private readonly FixedTimeProvider _time = new(FixedNow);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string LogDir => Path.Combine(_root, "logs", "nested");

    private static string[] ReadLines(string path) => File.ReadAllLines(path, Encoding.UTF8);

    private static string[] LinesOfKind(string[] lines, string kind) =>
        lines.Where(l => l.Split('\t')[1] == kind).ToArray();

    [Fact]
    public void X_Log_1_폴더를_만들고_파일명은_yyyyMMdd_HHmmss()
    {
        using var log = RunLog.Create(LogDir, _time);

        Assert.True(Directory.Exists(LogDir));
        Assert.Equal(Path.Combine(LogDir, "20261021-090807.log"), log.Path);
        Assert.True(File.Exists(log.Path));
    }

    [Fact]
    public void X_Log_2_같은_초에_두_번_만들면_두_파일이고_기존_내용_보존()
    {
        string firstPath;
        using (var first = RunLog.Create(LogDir, _time))
        {
            firstPath = first.Path;
            first.Note("첫 실행");
        }

        var before = File.ReadAllBytes(firstPath);

        using var second = RunLog.Create(LogDir, _time);
        second.Note("두 번째 실행");
        using var third = RunLog.Create(LogDir, _time);

        Assert.Equal(Path.Combine(LogDir, "20261021-090807_1.log"), second.Path);
        Assert.Equal(Path.Combine(LogDir, "20261021-090807_2.log"), third.Path);
        Assert.Equal(before, File.ReadAllBytes(firstPath));
        Assert.Equal(3, Directory.GetFiles(LogDir).Length);
    }

    [Fact]
    public void X_Log_3_UTF8_BOM으로_시작()
    {
        string path;
        using (var log = RunLog.Create(LogDir, _time))
        {
            path = log.Path;
            log.Note("한글");
        }

        var bytes = File.ReadAllBytes(path);

        Assert.True(bytes.Length > 3);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
    }

    [Fact]
    public void X_Log_4_OK_FAIL_줄은_필드_6개와_값()
    {
        string path;
        using (var log = RunLog.Create(LogDir, _time))
        {
            path = log.Path;
            log.FileResult(3, @"C:\in\a.hwpx", @"C:\out\a.hwp", ConversionOutcome.Success());
            log.FileResult(12, @"C:\in\b.hwpx", null, ConversionOutcome.Failure(FailureReason.OpenFailed, "0x80004005"));
            log.FileResult(13, @"C:\in\c.hwpx", null, ConversionOutcome.Failure(FailureReason.SaveFailed));
        }

        var lines = ReadLines(path);

        Assert.Equal(3, lines.Length);
        Assert.Equal(["09:08:07.123", "OK", "#0003", @"C:\in\a.hwpx", @"C:\out\a.hwp", "-"], lines[0].Split('\t'));
        var openFailed = FailureReasonText.Get(FailureReason.OpenFailed) + ": 0x80004005";
        Assert.Equal(["09:08:07.123", "FAIL", "#0012", @"C:\in\b.hwpx", "-", openFailed], lines[1].Split('\t'));
        Assert.Equal(
            ["09:08:07.123", "FAIL", "#0013", @"C:\in\c.hwpx", "-", FailureReasonText.Get(FailureReason.SaveFailed)],
            lines[2].Split('\t'));
    }

    [Fact]
    public void X_Log_5_FAIL_2건이면_FAIL_줄_2개()
    {
        string path;
        using (var log = RunLog.Create(LogDir, _time))
        {
            path = log.Path;
            log.Start("가짜 엔진", 4, null);
            log.Note("승인 팝업이 뜰 수 있음");
            log.FileResult(1, "a.hwpx", "a.hwp", ConversionOutcome.Success());
            log.FileResult(2, "b.hwpx", null, ConversionOutcome.Failure(FailureReason.Encrypted));
            log.FileResult(3, "c.hwpx", "c.hwp", ConversionOutcome.Success());
            log.FileResult(4, "d.hwpx", null, ConversionOutcome.Failure(FailureReason.Unexpected, "boom"));
            log.End(2, 2, 0, cancelled: false);
        }

        var lines = ReadLines(path);

        Assert.Equal(7, lines.Length);
        Assert.Equal(2, LinesOfKind(lines, "FAIL").Length);
        Assert.Equal(2, LinesOfKind(lines, "OK").Length);
        Assert.Equal(["09:08:07.123", "START", "engine=가짜 엔진", "inputs=4", "output=-"], lines[0].Split('\t'));
        Assert.Equal(
            ["09:08:07.123", "END", "succeeded=2", "failed=2", "pending=0", "cancelled=false"],
            lines[^1].Split('\t'));
    }

    [Fact]
    public void X_Log_6_탭_CR_LF_제어문자는_공백으로_바꿔_1줄_유지()
    {
        string path;
        using (var log = RunLog.Create(LogDir, _time))
        {
            path = log.Path;
            log.FileResult(1, "a\tb\r\nc\u0001.hwpx", "out\n.hwp",
                ConversionOutcome.Failure(FailureReason.OpenFailed, "line1\r\nline2\tend"));
            log.Note("안내\r\n둘째");
            log.Error("오류\t\n");
        }

        var lines = ReadLines(path);

        Assert.Equal(3, lines.Length);
        var fields = lines[0].Split('\t');
        Assert.Equal(6, fields.Length);
        Assert.Equal("a b  c .hwpx", fields[3]);
        Assert.Equal("out .hwp", fields[4]);
        Assert.EndsWith("line1  line2 end", fields[5], StringComparison.Ordinal);
        Assert.Equal(["09:08:07.123", "NOTE", "안내  둘째"], lines[1].Split('\t'));
        Assert.Equal(["09:08:07.123", "ERROR", "오류  "], lines[2].Split('\t'));
    }

    [Fact]
    public void X_Log_7_Encrypted_문구에_암호_포함()
    {
        Assert.Contains("암호", FailureReasonText.Get(FailureReason.Encrypted), StringComparison.Ordinal);
    }

    [Fact]
    public void X_Log_8_모든_FailureReason_값과_알_수_없는_값에_빈_문구_없음()
    {
        foreach (var reason in Enum.GetValues<FailureReason>())
        {
            Assert.False(string.IsNullOrWhiteSpace(FailureReasonText.Get(reason)), reason.ToString());
        }

        Assert.False(string.IsNullOrWhiteSpace(FailureReasonText.Get((FailureReason)999)));
    }

    [Fact]
    public void X_Log_9_Dispose_뒤_쓰기는_무시()
    {
        var log = RunLog.Create(LogDir, _time);
        log.Note("before");
        log.Dispose();

        log.Note("after");
        log.Dispose();

        Assert.Single(ReadLines(log.Path));
    }

    [Fact]
    public void X_Log_10_여러_스레드에서_써도_줄이_섞이지_않음()
    {
        string path;
        using (var log = RunLog.Create(LogDir, _time))
        {
            path = log.Path;
            Parallel.For(1, 201, i =>
                log.FileResult(i, new string('x', 200) + i, null, ConversionOutcome.Failure(FailureReason.Unexpected)));
        }

        var lines = ReadLines(path);

        Assert.Equal(200, lines.Length);
        Assert.All(lines, l => Assert.Equal(6, l.Split('\t').Length));
    }

    [Fact]
    public void X_Log_11_로그_폴더는_LocalApplicationData_아래_hwpx2hwp_logs()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "hwpx2hwp", "logs");

        Assert.Equal(expected, AppPaths.LogDirectory);
    }

    [Fact]
    public void X_Log_12_쓰기_IOException은_삼키고_WriteFailed만_세움()
    {
        using (var ok = RunLog.Create(LogDir, _time))
        {
            ok.Note("정상");
            Assert.False(ok.WriteFailed);
        }

        // 생성 때 BOM 은 써지고, 그 뒤 디스크가 찬 상황.
        var stream = new FailingStream();
        var log = new RunLog("full.log", stream, _time);
        Assert.False(log.WriteFailed);
        stream.Fail = true;

        log.Start("가짜 엔진", 1, null);
        log.FileResult(1, "a.hwpx", null, ConversionOutcome.Failure(FailureReason.Unexpected));
        log.End(0, 1, 0, cancelled: false);
        log.Dispose();
        log.Note("after");

        Assert.True(log.WriteFailed);
    }

    [Fact]
    public void X_Log_13_줄_문단_구분자와_Format_범주는_공백으로()
    {
        string path;
        using (var log = RunLog.Create(LogDir, _time))
        {
            path = log.Path;
            // 소스에 문자를 직접 넣지 않으려고 코드값으로 만든다(U+2028 은 C# 소스에서 줄바꿈으로 읽힌다).
            log.Note(string.Join(string.Empty,
                "a", (char)0x2028, "b", (char)0x2029, "c", (char)0x202E, "d", (char)0x200E, "e", (char)0xFEFF, "f"));
        }

        var lines = ReadLines(path);

        Assert.Single(lines);
        Assert.Equal(["09:08:07.123", "NOTE", "a b c d e f"], lines[0].Split('\t'));
    }

    [Fact]
    public void X_Log_14_LocalApplicationData가_빈_문자열이면_임시_폴더_아래()
    {
        var expected = Path.Combine(Path.GetTempPath(), "hwpx2hwp", "logs");

        Assert.Equal(expected, AppPaths.LogDirectoryUnder(string.Empty));
        Assert.True(Path.IsPathRooted(AppPaths.LogDirectoryUnder(string.Empty)));
        Assert.Equal(Path.Combine("base", "hwpx2hwp", "logs"), AppPaths.LogDirectoryUnder("base"));
    }

    /// <summary>디스크 가득을 흉내 내는 스트림: Fail 을 켠 뒤부터 Write·Flush 마다 IOException.</summary>
    private sealed class FailingStream : Stream
    {
        public bool Fail { get; set; }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => ThrowIfFailing();

        public override void Write(byte[] buffer, int offset, int count) => ThrowIfFailing();

        private void ThrowIfFailing()
        {
            if (Fail)
            {
                throw new IOException("disk full");
            }
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }

    /// <summary>고정 시각 TimeProvider. 지역 시간대는 UTC 로 고정해 환경과 무관하게 만든다.</summary>
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
