using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Hwpx2Hwp.Core.Model;

namespace Hwpx2Hwp.Core.Logging;

/// <summary>
/// 실행 1회 = 로그 파일 1개(F5, D-11). 탭 구분, 1항목 1줄, UTF-8 BOM.
/// 줄: <c>HH:mm:ss.fff TAB 종류 TAB 필드…</c>. 여러 스레드에서 불러도 줄이 섞이지 않는다.
/// Dispose 뒤의 쓰기는 예외 대신 무시한다 — 진행 중 취소·종료 경합에서 로그 쓰기가 흐름을 깨지 않게 하려는 선택.
/// </summary>
public sealed class RunLog : IDisposable
{
    private const char Tab = '\t';
    private const string None = "-";

    private readonly Lock _gate = new();
    private readonly StreamWriter _writer;
    private readonly TimeProvider _time;
    private bool _disposed;
    private bool _writeFailed;

    /// <summary>stream 은 RunLog 가 소유하고 Dispose 때 닫는다. internal 인 이유: 테스트가 쓰기 실패 스트림을 넣을 수 있게.</summary>
    internal RunLog(string path, Stream stream, TimeProvider time)
    {
        Path = path;
        _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true))
        {
            AutoFlush = true,
        };
        _time = time;
    }

    /// <summary>로그 파일 전체 경로.</summary>
    public string Path { get; }

    /// <summary>
    /// 줄 쓰기(또는 닫기)가 IOException(디스크 가득·잠김 등)으로 한 번이라도 실패했으면 true.
    /// 로그 실패가 일괄 변환을 멈추지 않도록 예외는 삼키고 이 값만 세운다. 이후 쓰기도 계속 시도한다.
    /// </summary>
    public bool WriteFailed
    {
        get
        {
            lock (_gate)
            {
                return _writeFailed;
            }
        }
    }

    /// <summary>
    /// logDir 를 만들고 <c>yyyyMMdd-HHmmss.log</c> 를 새로 만든다. 같은 이름이 있으면 <c>_1</c>, <c>_2</c> … 를 붙인다(기존 로그 덮어쓰기 금지).
    /// </summary>
    /// <exception cref="IOException">폴더·파일을 만들 수 없을 때(경로가 파일과 겹침·디스크 오류 등).</exception>
    /// <exception cref="UnauthorizedAccessException">폴더·파일을 만들 권한이 없을 때.</exception>
    public static RunLog Create(string logDir, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(logDir);
        ArgumentNullException.ThrowIfNull(time);

        Directory.CreateDirectory(logDir);
        var stamp = time.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        for (var n = 0; ; n++)
        {
            var name = n == 0 ? stamp : stamp + "_" + n.ToString(CultureInfo.InvariantCulture);
            var path = System.IO.Path.Combine(logDir, name + ".log");
            if (TryCreateNew(path, out var stream))
            {
                return new RunLog(path, stream, time);
            }
        }
    }

    /// <summary>머리 줄: <c>START TAB engine=… TAB inputs=… TAB output=…|-</c>. outputFolder null 은 원본과 같은 폴더.</summary>
    public void Start(string engineName, int inputCount, string? outputFolder) =>
        WriteLine("START",
            "engine=" + Clean(engineName),
            "inputs=" + inputCount.ToString(CultureInfo.InvariantCulture),
            "output=" + Clean(outputFolder));

    /// <summary>
    /// 파일 결과 줄: <c>OK|FAIL TAB #0003 TAB 원본 TAB 출력|- TAB 이유|-</c>.
    /// FAIL 이유는 FailureReasonText 문구이고 Detail 이 있으면 <c>": "</c> 뒤에 덧붙인다.
    /// </summary>
    public void FileResult(int jobNumber, string sourcePath, string? outputPath, ConversionOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        var kind = outcome.Succeeded ? "OK" : "FAIL";
        WriteLine(kind, JobId(jobNumber), Clean(sourcePath), Clean(outputPath), Clean(ReasonOf(outcome)));
    }

    /// <summary>건너뛴 파일 줄: <c>SKIP TAB #0003 TAB 원본 TAB - TAB 이유|-</c>.</summary>
    public void Skip(int jobNumber, string sourcePath, string? reason) =>
        WriteLine("SKIP", JobId(jobNumber), Clean(sourcePath), None, Clean(reason));

    /// <summary>안내 줄: <c>NOTE TAB 문구</c>.</summary>
    public void Note(string message) => WriteLine("NOTE", Clean(message));

    /// <summary>오류 줄: <c>ERROR TAB 문구</c>.</summary>
    public void Error(string message) => WriteLine("ERROR", Clean(message));

    /// <summary>꼬리 줄: <c>END TAB succeeded=… TAB failed=… TAB pending=… TAB cancelled=true|false</c>.</summary>
    public void End(int succeeded, int failed, int pending, bool cancelled) =>
        WriteLine("END",
            "succeeded=" + succeeded.ToString(CultureInfo.InvariantCulture),
            "failed=" + failed.ToString(CultureInfo.InvariantCulture),
            "pending=" + pending.ToString(CultureInfo.InvariantCulture),
            "cancelled=" + (cancelled ? "true" : "false"));

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                _writer.Dispose();
            }
            catch (IOException)
            {
                // 종단: 닫을 때 남은 버퍼 쓰기가 실패해도 호출자(일괄 변환 종료)로 새지 않게 플래그만 세운다.
                _writeFailed = true;
            }
        }
    }

    private static string? ReasonOf(ConversionOutcome outcome)
    {
        if (outcome.Succeeded)
        {
            return null;
        }

        var text = FailureReasonText.Get(outcome.FailureReason ?? FailureReason.Unexpected);
        return string.IsNullOrEmpty(outcome.Detail) ? text : text + ": " + outcome.Detail;
    }

    private static string JobId(int jobNumber) => "#" + jobNumber.ToString("D4", CultureInfo.InvariantCulture);

    /// <summary>
    /// null·빈 값은 "-". 탭·CR·LF 등 제어문자, U+2028·U+2029(줄·문단 구분), Format 범주(U+202E 등 양방향 제어)는
    /// 공백으로 바꿔 1항목이 항상 1줄이고 보이는 순서가 뒤집히지 않게 한다.
    /// </summary>
    private static string Clean(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return None;
        }

        return string.Create(value.Length, value, static (span, src) =>
        {
            for (var i = 0; i < src.Length; i++)
            {
                span[i] = IsUnsafe(src[i]) ? ' ' : src[i];
            }
        });
    }

    private static bool IsUnsafe(char c) =>
        char.IsControl(c) || char.GetUnicodeCategory(c) is UnicodeCategory.LineSeparator
            or UnicodeCategory.ParagraphSeparator or UnicodeCategory.Format;

    private void WriteLine(string kind, params string[] fields)
    {
        var line = new StringBuilder();
        line.Append(_time.GetLocalNow().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture));
        line.Append(Tab).Append(kind);
        foreach (var field in fields)
        {
            line.Append(Tab).Append(field);
        }

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _writer.WriteLine(line.ToString());
            }
            catch (IOException)
            {
                // 종단: 로그 한 줄 실패로 일괄 변환이 멈추면 안 된다(F5 는 부가 기록). 플래그로만 알린다.
                _writeFailed = true;
            }
        }
    }

    private static bool TryCreateNew(string path, [NotNullWhen(true)] out FileStream? stream)
    {
        try
        {
            stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            return true;
        }
        catch (IOException) when (File.Exists(path))
        {
            // 복구: 같은 이름이 이미 있으면 다음 번호로 넘어간다.
            stream = null;
            return false;
        }
    }
}
