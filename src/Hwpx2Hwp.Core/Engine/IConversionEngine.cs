using Hwpx2Hwp.Core.Model;

namespace Hwpx2Hwp.Core.Engine;

/// <summary>변환 엔진 계약(외부 I/O 경계). docs/design/baseline.md D-3.</summary>
public interface IConversionEngine : IAsyncDisposable
{
    /// <summary>설치 확인·보안 모듈·인스턴스 생성. 여러 번 불러도 실제 시작은 1회.</summary>
    Task<EngineStart> StartAsync(CancellationToken ct);

    /// <summary>파일 1개 변환. 파일 단위 실패는 예외가 아니라 Outcome 으로 돌려준다.</summary>
    Task<ConversionOutcome> ConvertAsync(ConversionJob job, CancellationToken ct);
}
