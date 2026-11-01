using System.ComponentModel;
using System.Diagnostics;

namespace Hwpx2Hwp.App;

/// <summary>
/// F7 중복 실행 차단: 같은 사용자 세션 범위(Local\)의 이름 있는 Mutex 로 첫 실행만 통과시키고,
/// 두 번째 실행은 기존 인스턴스의 주 창을 앞으로 가져온다.
/// </summary>
internal static class SingleInstance
{
    private const string MutexName = @"Local\hwpx2hwp-7f3c9e2a-5b41-4d8e-9a06-1c2e8b4f6d17";

    /// <summary>
    /// 첫 실행이면 소유한 Mutex 를, 이미 실행 중이면 null 을 돌려준다.
    /// 이전 소유 프로세스가 비정상 종료해 버려진 Mutex(AbandonedMutexException)는 획득 성공으로 본다.
    /// </summary>
    internal static Mutex? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: false, MutexName);
        bool owned;
        try
        {
            owned = mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            owned = true; // 복구: 버려진 Mutex 도 이 스레드가 소유하게 된다.
        }

        if (owned)
        {
            return mutex;
        }

        mutex.Dispose();
        return null;
    }

    /// <summary>
    /// 같은 실행 파일 경로·같은 세션 ID 의 다른 프로세스 주 창을 복원하고 앞으로 가져온다.
    /// 못 찾거나 실패해도 던지지 않는다(두 번째 실행은 조용히 끝난다).
    /// </summary>
    internal static void ActivateExisting()
    {
        try
        {
            var window = FindExistingWindow();
            if (window == IntPtr.Zero)
            {
                Trace.TraceInformation("single instance: existing window not found");
                return;
            }

            if (NativeMethods.IsIconic(window))
            {
                NativeMethods.ShowWindow(window, NativeMethods.SwRestore);
            }

            var activated = NativeMethods.SetForegroundWindow(window);
            Trace.TraceInformation("single instance: activate existing window activated={0}", activated);
        }
#pragma warning disable CA1031 // 두 번째 실행 종단: 어떤 실패도 크래시 창 없이 기록만 하고 조용히 끝낸다.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Trace.TraceWarning("single instance: activate failed type={0} message={1}", ex.GetType().Name, ex.Message);
        }
    }

    private static IntPtr FindExistingWindow()
    {
        using var current = Process.GetCurrentProcess();
        var exePath = Environment.ProcessPath;
        if (exePath is null)
        {
            return IntPtr.Zero;
        }

        var others = Process.GetProcessesByName(current.ProcessName);
        try
        {
            foreach (var other in others)
            {
                if (other.Id == current.Id)
                {
                    continue;
                }

                try
                {
                    if (other.SessionId == current.SessionId
                        && string.Equals(other.MainModule?.FileName, exePath, StringComparison.OrdinalIgnoreCase)
                        && other.MainWindowHandle != IntPtr.Zero)
                    {
                        return other.MainWindowHandle;
                    }
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // 복구: 접근 거부·이미 끝난 프로세스는 건너뛴다.
                    Trace.TraceWarning("single instance: skip process pid={0} type={1}", other.Id, ex.GetType().Name);
                }
            }

            return IntPtr.Zero;
        }
        finally
        {
            foreach (var other in others)
            {
                other.Dispose();
            }
        }
    }
}
