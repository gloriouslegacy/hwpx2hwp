namespace Hwpx2Hwp.Core.Probe;

/// <summary>hwpx 사전 검사 결과. D-5 암호 사전 검사.</summary>
public enum ProbeResult
{
    /// <summary>zip 으로 열리고 암호화 항목이 없다.</summary>
    Ok,

    /// <summary>zip 이 아니거나 깨졌거나 manifest 를 읽을 수 없다.</summary>
    NotHwpx,

    /// <summary>manifest 에 암호화 항목이 있다.</summary>
    Encrypted,
}
