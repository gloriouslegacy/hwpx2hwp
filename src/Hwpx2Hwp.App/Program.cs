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

    [STAThread]
    private static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ReportCrash("ThreadException", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportCrash("UnhandledException", e.ExceptionObject);
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
    /// 로그 폴더에 crash-yyyyMMdd-HHmmss.log(같은 이름이 있으면 _1, _2…)를 새로 만들어 예외 전문을 쓴다.
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
            for (var n = 0; ; n++)
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
