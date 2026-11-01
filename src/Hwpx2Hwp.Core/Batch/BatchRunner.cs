using Hwpx2Hwp.Core.Engine;
using Hwpx2Hwp.Core.Logging;
using Hwpx2Hwp.Core.Model;
using Hwpx2Hwp.Core.Paths;
using Hwpx2Hwp.Core.Probe;

namespace Hwpx2Hwp.Core.Batch;

/// <summary>
/// F4 일괄 실행. 파일마다: 원본 존재 확인 → HwpxProbe → Planner.Reserve → 출력 폴더 생성 → 엔진 변환 → 로그 1줄 → 진행 이벤트(D-5).
/// 파일 단위 실패(엔진 예외 포함)는 그 파일만 실패로 기록하고 계속한다. 취소는 파일 사이에서만 확인한다.
/// 원본 파일에는 쓰거나 지우지 않는다. 엔진 Dispose 는 호출자 몫.
/// </summary>
public sealed class BatchRunner
{
    private const string CancelledSkipReason = "취소됨";
    private const string EngineDownSkipReason = "변환 엔진을 시작하지 못함";
    private const string NotHwpxDetail = "hwpx(zip) 형식이 아니거나 손상됨";

    private readonly IConversionEngine _engine;
    private readonly string _logDir;
    private readonly TimeProvider _time;

    public BatchRunner(IConversionEngine engine, string logDir, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(logDir);
        ArgumentNullException.ThrowIfNull(time);
        _engine = engine;
        _logDir = logDir;
        _time = time;
    }

    /// <summary>
    /// items 를 순서대로 변환한다. 호출 스레드를 막지 않고(스레드 풀에서 실행) 진행은 progress 로만 알린다.
    /// 엔진을 쓸 수 없거나 실행 전·엔진 시작 중·파일 사이에 취소되면 남은 파일은 Pending 으로 남는다.
    /// 출력 폴더 경로가 잘못되면 엔진을 시작하지 않고 모든 파일을 SaveFailed 로 기록한다.
    /// </summary>
    /// <remarks>
    /// 로그 END 줄은 어떤 경로로 끝나도 남는다. progress 구현이 던지는 예외는 호출자 책임이며 잡지 않고 그대로 전파된다.
    /// </remarks>
    /// <exception cref="IOException">로그 파일을 만들 수 없을 때(파일 처리 전, RunLog.Create 의 예외 그대로).</exception>
    public Task<BatchReport> RunAsync(
        IReadOnlyList<InputItem> items, BatchOptions options, IProgress<BatchEvent>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(options);
        return Task.Run(() => RunCoreAsync(items, options, progress, ct));
    }

    private async Task<BatchReport> RunCoreAsync(
        IReadOnlyList<InputItem> items, BatchOptions options, IProgress<BatchEvent>? progress, CancellationToken ct)
    {
        using var log = RunLog.Create(_logDir, _time);
        var notices = new List<string>();
        var failures = new List<BatchFailure>();
        var succeeded = 0;
        var failed = 0;
        var cancelled = ct.IsCancellationRequested;
        string? skipReason = null; // 예상 밖 예외로 끝나면 SKIP 이유는 비운다("-").

        // 파일 1건의 결과를 로그·집계·진행 이벤트에 반영한다. 처리한 파일 수 = succeeded + failed = 다음 파일 인덱스.
        void Record(InputItem item, string? outputPath, ConversionOutcome outcome)
        {
            log.FileResult(succeeded + failed + 1, item.SourcePath, outputPath, outcome);
            if (outcome.Succeeded)
            {
                succeeded++;
            }
            else
            {
                failed++;
                failures.Add(new BatchFailure(item.SourcePath, outcome.FailureReason ?? FailureReason.Unexpected, outcome.Detail));
            }

            var state = outcome.Succeeded ? JobState.Succeeded : JobState.Failed;
            progress?.Report(new BatchEvent(succeeded + failed, item, state, outputPath, outcome, succeeded + failed, items.Count));
        }

        try
        {
            log.Start(options.EngineName, items.Count, options.OutputFolder);
            if (!cancelled)
            {
                OutputPathPlanner? planner = null;
                string? plannerError = null;
                try
                {
                    planner = new OutputPathPlanner(options.Route, options.OutputFolder);
                }
                catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
                {
                    // 출력 폴더 경로 자체가 잘못됨: 엔진을 띄우지 않고 모든 파일을 SaveFailed 로 기록한다.
                    plannerError = ex.GetType().Name + ": " + ex.Message;
                }

                if (planner is null)
                {
                    var outcome = ConversionOutcome.Failure(FailureReason.SaveFailed, plannerError);
                    foreach (var item in items)
                    {
                        Record(item, null, outcome);
                    }
                }
                else
                {
                    var started = false;
                    try
                    {
                        started = await StartEngineAsync(log, notices, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        cancelled = true;
                    }

                    skipReason = started ? null : EngineDownSkipReason;
                    while (started && succeeded + failed < items.Count)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            cancelled = true;
                            break;
                        }

                        var item = items[succeeded + failed];
                        var completed = succeeded + failed;
                        progress?.Report(new BatchEvent(completed + 1, item, JobState.Running, null, null, completed, items.Count));
                        var (outputPath, outcome) = await ConvertOneAsync(item, planner, options.Route).ConfigureAwait(false);
                        Record(item, outputPath, outcome);
                    }
                }
            }
        }
        finally
        {
            var processed = succeeded + failed;
            var reason = cancelled ? CancelledSkipReason : skipReason;
            for (var i = processed; i < items.Count; i++)
            {
                log.Skip(i + 1, items[i].SourcePath, reason);
            }

            log.End(succeeded, failed, items.Count - processed, cancelled);
        }

        return new BatchReport(succeeded, failed, items.Count - succeeded - failed, failures, log.Path, notices, cancelled);
    }

    /// <summary>
    /// 엔진을 시작한다. 안내는 로그 NOTE 와 notices 에 담는다. 쓸 수 없거나 예외면 false.
    /// ct 가 취소된 상태의 OperationCanceledException 은 엔진 오류가 아니라 취소이므로 호출자에게 그대로 넘긴다.
    /// </summary>
    private async Task<bool> StartEngineAsync(RunLog log, List<string> notices, CancellationToken ct)
    {
        EngineStart start;
        try
        {
            start = await _engine.StartAsync(ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // 엔진 시작의 어떤 예외도 "엔진을 쓸 수 없음"(전부 Pending)으로 번역한다. D-3 외부 I/O 경계.
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
#pragma warning restore CA1031
        {
            log.Error("엔진 시작 예외: " + ex.GetType().Name + ": " + ex.Message);
            AddNotice(log, notices, EngineDownSkipReason);
            return false;
        }

        foreach (var notice in start.Notices)
        {
            AddNotice(log, notices, notice);
        }

        if (!start.Available)
        {
            AddNotice(log, notices, start.UnavailableMessage ?? EngineDownSkipReason);
            return false;
        }

        return true;
    }

    private static void AddNotice(RunLog log, List<string> notices, string message)
    {
        notices.Add(message);
        log.Note(message);
    }

    /// <summary>파일 1건을 처리한다. 출력 경로를 정하기 전에 실패하면 OutputPath 는 null.</summary>
    private async Task<(string? OutputPath, ConversionOutcome Outcome)> ConvertOneAsync(
        InputItem item, OutputPathPlanner planner, ConversionRoute route)
    {
        if (!File.Exists(item.SourcePath))
        {
            return (null, ConversionOutcome.Failure(FailureReason.InputMissing));
        }

        ProbeResult probe;
        try
        {
            probe = HwpxProbe.Inspect(item.SourcePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (null, ConversionOutcome.Failure(FailureReason.OpenFailed, ex.Message));
        }

        if (probe == ProbeResult.Encrypted)
        {
            return (null, ConversionOutcome.Failure(FailureReason.Encrypted));
        }

        if (probe == ProbeResult.NotHwpx)
        {
            return (null, ConversionOutcome.Failure(FailureReason.OpenFailed, NotHwpxDetail));
        }

        // 예약·출력 폴더 생성·저장 직전 재확인·재예약을 한 경계로 묶는다. 실패하면 그 파일만 SaveFailed.
        string? outputPath = null;
        try
        {
            outputPath = planner.Reserve(item);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            // 저장 직전 재확인: 예약 뒤에 같은 이름의 파일·폴더가 생겼으면 새 이름을 받는다(기존 파일 덮어쓰기 금지).
            if (File.Exists(outputPath) || Directory.Exists(outputPath))
            {
                outputPath = planner.Reserve(item);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return (outputPath, ConversionOutcome.Failure(FailureReason.SaveFailed, ex.GetType().Name + ": " + ex.Message));
        }

        var job = new ConversionJob(item, outputPath, route);
        try
        {
            // 진행 중 파일은 끊지 않는다(D-5 취소 규칙): 엔진에는 취소 토큰을 넘기지 않는다.
            var outcome = await _engine.ConvertAsync(job, CancellationToken.None).ConfigureAwait(false);
            return (outputPath, outcome);
        }
#pragma warning disable CA1031 // 실패 격리(D-5 Q4): 엔진이 던진 어떤 예외도 그 파일의 Unexpected 로 번역하고 다음 파일로 간다.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return (outputPath, ConversionOutcome.Failure(FailureReason.Unexpected, ex.GetType().Name));
        }
    }
}
