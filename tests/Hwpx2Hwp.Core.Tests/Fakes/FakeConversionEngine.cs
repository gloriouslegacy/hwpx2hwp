using Hwpx2Hwp.Core.Engine;
using Hwpx2Hwp.Core.Model;

namespace Hwpx2Hwp.Core.Tests.Fakes;

/// <summary>
/// 테스트용 가짜 엔진. 파일 시스템을 건드리지 않는다.
/// 변환 번호는 ConvertAsync 호출 순서(1부터)이며 예외를 던진 호출도 센다.
/// </summary>
public sealed class FakeConversionEngine : IConversionEngine
{
    private readonly List<ConversionJob> _jobs = [];

    /// <summary>이 번째 변환을 FailureReason 으로 실패시킨다(1부터). null 이면 없음.</summary>
    public int? FailAt { get; init; }

    /// <summary>FailAt 번째 변환의 실패 이유.</summary>
    public FailureReason FailureReason { get; init; } = FailureReason.OpenFailed;

    /// <summary>이 번째 변환에서 InvalidOperationException 을 던진다(1부터). null 이면 없음.</summary>
    public int? ThrowAt { get; init; }

    /// <summary>
    /// 변환마다 결과를 정하기 전에 변환 번호(1부터)로 부른다(예: N번째 변환 중에 취소). null 이면 없음.
    /// 부른 뒤 받은 ct 가 취소 상태면 OperationCanceledException 을 던진다.
    /// </summary>
    public Action<int>? OnConvert { get; init; }

    /// <summary>값이 있으면 미설치로 시작한다(Available=false, 이 문구).</summary>
    public string? UnavailableMessage { get; init; }

    /// <summary>시작 결과에 담을 안내 목록.</summary>
    public IReadOnlyList<string> Notices { get; init; } = [];

    /// <summary>실제 시작 횟수. StartAsync 를 여러 번 불러도 1.</summary>
    public int StartCount { get; private set; }

    /// <summary>DisposeAsync 가 불렸는지.</summary>
    public bool Disposed { get; private set; }

    /// <summary>받은 작업(호출 순서).</summary>
    public IReadOnlyList<ConversionJob> Jobs => _jobs;

    private EngineStart? _start;

    public Task<EngineStart> StartAsync(CancellationToken ct)
    {
        if (_start is null)
        {
            StartCount++;
            _start = new EngineStart(UnavailableMessage is null, UnavailableMessage, Notices);
        }

        return Task.FromResult(_start);
    }

    public Task<ConversionOutcome> ConvertAsync(ConversionJob job, CancellationToken ct)
    {
        _jobs.Add(job);
        var number = _jobs.Count;
        OnConvert?.Invoke(number);
        ct.ThrowIfCancellationRequested();

        if (number == ThrowAt)
        {
            throw new InvalidOperationException($"가짜 엔진 예외: {number}번째 변환");
        }

        var outcome = number == FailAt
            ? ConversionOutcome.Failure(FailureReason, $"가짜 실패: {number}번째 변환")
            : ConversionOutcome.Success();
        return Task.FromResult(outcome);
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
