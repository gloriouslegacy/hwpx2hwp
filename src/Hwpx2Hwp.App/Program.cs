using System.Diagnostics;
using System.Globalization;
using System.Text;
using Hwpx2Hwp.Core.Batch;
using Hwpx2Hwp.Core.Logging;
using Hwpx2Hwp.HwpCom;

namespace Hwpx2Hwp.App;

/// <summary>
/// 진입점·조립 지점: 엔진·BatchRunner·로그 폴더를 만들어 MainForm 에 넘기고, 전역 예외 처리기를 등록하고,
/// 종료 때 엔진을 정리한다(한/글 프로세스 종료). docs/design/baseline.md D-8, D-11.
/// </summary>
internal static class Program
{
    private const string Caption = "hwpx2hwp";
    private const int MaxCrashLogAttempts = 100;
    private static readonly TimeSpan CrashDisposeTimeout = TimeSpan.FromSeconds(5);

    /// <summary>프로세스가 죽을 때 처리기가 정리할 엔진. Main 이 만든 직후 넣는다.</summary>
    private static HwpComEngine? s_engine;

    [STAThread]
    private static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ReportCrash("ThreadException", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => OnUnhandledException(e);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            e.SetObserved();
            // 종료자 스레드를 안내 창으로 막지 않도록 안내는 스레드 풀에서 띄운다.
            var path = WriteCrashLog("UnobservedTaskException", e.Exception);
            _ = Task.Run(() => ShowCrashMessage(path));
        };

        // csproj ApplicationHighDpiMode=PerMonitorV2 가 여기서 적용된다.
        ApplicationConfiguration.Initialize();

        var engine = HwpComEngine.Create(AppContext.BaseDirectory);
        Volatile.Write(ref s_engine, engine);
        try
        {
            var runner = new BatchRunner(engine, AppPaths.LogDirectory, TimeProvider.System);
            using var form = new MainForm(runner, nameof(HwpComEngine));
            Application.Run(form);
        }
        finally
        {
            // UI 동기화 문맥에 묶이지 않게 스레드 풀에서 끝까지 기다린다.
            Task.Run(() => engine.DisposeAsync().AsTask()).GetAwaiter().GetResult();
        }
    }

    private static void ReportCrash(string source, object? error)
    {
        ShowCrashMessage(WriteCrashLog(source, error));
    }

    /// <summary>
    /// 처리되지 않은 예외로 프로세스가 끝나면 Main 의 finally 가 돌지 않아 한/글 프로세스가 남을 수 있다.
    /// 그래서 crash 로그 → 엔진 정리(최대 5초) → 안내 순서로 처리한다. 안내 창을 기다리는 동안 사용자가
    /// 프로세스를 끊어도 한/글은 이미 정리됐다.
    /// 중복 정리: 프로세스가 끝나지 않는 경우(IsTerminating=false)에도 정리하면 뒤에 Main finally 가 다시
    /// DisposeAsync 를 부른다. HwpComEngine.DisposeAsync 는 반복 호출에 안전하므로 두 번째 호출은 아무 일도 하지 않는다.
    /// </summary>
    private static void OnUnhandledException(UnhandledExceptionEventArgs e)
    {
        var path = WriteCrashLog("UnhandledException", e.ExceptionObject);
        DisposeEngineBestEffort();
        ShowCrashMessage(path);
    }

    /// <summary>처리기 안이므로 던지지 않는다. 시간 안에 끝나지 않거나 실패하면 기록만 하고 넘어간다.</summary>
    private static void DisposeEngineBestEffort()
    {
        var engine = Volatile.Read(ref s_engine);
        if (engine is null)
        {
            return;
        }

        try
        {
            // 예외가 엔진 STA 스레드에서 났으면 정리가 멈출 수 있으므로 시간 상한을 둔다.
            var dispose = Task.Run(() => engine.DisposeAsync().AsTask());
            if (!dispose.Wait(CrashDisposeTimeout))
            {
                Trace.TraceError("crash engine dispose timed out: timeout_s={0}", CrashDisposeTimeout.TotalSeconds);
            }
        }
#pragma warning disable CA1031 // 전역 예외 처리기: 정리 실패도 던지지 않고 기록으로 종단한다.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Trace.TraceError("crash engine dispose failed: type={0} message={1}", ex.GetType().Name, ex.Message);
        }
    }

    /// <summary>
    /// 로그 폴더에 crash-yyyyMMdd-HHmmss.log(같은 이름이 있으면 _1, _2… 최대 100번 시도)를 새로 만들어 예외 전문을 쓴다.
    /// 처리기 안이므로 던지지 않는다. 실패하면 null.
    /// </summary>
    private static string? WriteCrashLog(string source, object? error)
    {
        try
        {
            var now = TimeProvider.System.GetLocalNow();
            var dir = AppPaths.LogDirectory;
            Directory.CreateDirectory(dir);
            var stem = "crash-" + now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            for (var n = 0; n < MaxCrashLogAttempts; n++)
            {
                var path = Path.Combine(dir, n == 0 ? stem + ".log" : stem + "_" + n.ToString(CultureInfo.InvariantCulture) + ".log");
                FileStream stream;
                try
                {
                    stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                }
                catch (IOException) when (File.Exists(path))
                {
                    continue; // 복구: 같은 이름이 이미 있으면 다음 번호로.
                }

                using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                writer.WriteLine(now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture) + "\tCRASH\t" + source);
                writer.WriteLine(error?.ToString() ?? "(예외 정보 없음)");
                return path;
            }

            Trace.TraceError("crash log name exhausted: attempts={0}", MaxCrashLogAttempts);
            return null;
        }
#pragma warning disable CA1031 // 전역 예외 처리기: 기록 실패도 던지지 않고 null 로 종단한다.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Trace.TraceError("crash log write failed: type={0} message={1}", ex.GetType().Name, ex.Message);
            return null;
        }
    }

    private static void ShowCrashMessage(string? logPath)
    {
        try
        {
            var text = logPath is null
                ? "예상하지 못한 오류가 났습니다. 오류 기록을 남기지 못했습니다."
                : "예상하지 못한 오류가 났습니다. 오류 기록:\n" + logPath;
            MessageBox.Show(text, Caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
#pragma warning disable CA1031 // 전역 예외 처리기: 안내 실패도 던지지 않는다.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Trace.TraceError("crash message failed: type={0} message={1}", ex.GetType().Name, ex.Message);
        }
    }
}
