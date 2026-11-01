using System.Collections.Concurrent;

namespace Hwpx2Hwp.HwpCom;

/// <summary>
/// STA 전용 스레드 1개 + 작업 큐. 모든 한/글 COM 호출은 이 스레드에서 한다.
/// Hwp.exe 는 out-of-proc 서버이고 이벤트를 받지 않으므로 메시지 루프는 두지 않는다. D-6.
/// </summary>
internal sealed class StaDispatcher : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;

    internal StaDispatcher()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "HwpCom STA" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    /// <summary>작업을 STA 스레드에서 실행한다. 작업의 예외는 돌려준 Task 로 전달된다.</summary>
    internal Task<T> InvokeAsync<T>(Func<T> work)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(() =>
        {
            // 작업 단위 경계: 예외는 여기서 잡아 호출자의 Task 로 넘긴다(STA 스레드를 죽이지 않음).
            try
            {
                tcs.SetResult(work());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    /// <summary>큐를 닫고 남은 작업을 마친 뒤 스레드가 끝날 때까지 기다린다.</summary>
    public void Dispose()
    {
        _queue.CompleteAdding();
        if (Thread.CurrentThread != _thread)
        {
            _thread.Join();
        }

        _queue.Dispose();
    }

    private void Run()
    {
        foreach (var work in _queue.GetConsumingEnumerable())
        {
            work();
        }
    }
}
