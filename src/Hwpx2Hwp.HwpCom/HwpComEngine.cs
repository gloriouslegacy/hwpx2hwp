using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Hwpx2Hwp.Core.Engine;
using Hwpx2Hwp.Core.Model;

namespace Hwpx2Hwp.HwpCom;

/// <summary>
/// 한/글 COM 변환 엔진(F1). 인스턴스는 STA 스레드에서 첫 StartAsync 때 1번 만들고 DisposeAsync 까지 다시 쓴다.
/// 서버 사망이면 그 파일 EngineCrashed + 1회 재생성. D-3, D-6, D-7.
/// </summary>
public sealed class HwpComEngine : IConversionEngine
{
    private const string ProgId = "HWPFrame.HwpObject";
    private const string OpenArgs = "forceopen:true;suspendpassword:true;versionwarning:false";
    private const string SecurityModuleType = "FilePathCheckDLL";
    private const string TempSuffix = ".tmp";

    // SetMessageBoxMode 값(상수 1곳). 비트 의미는 한/글 오토메이션 문서 기준이며 한/글 2014 동작은 Windows 미확인(열린 문제 3).
    // 확인(MB_OK) 0x1 | 취소(MB_OKCANCEL) 0x20 | 중지(MB_ABORTRETRYIGNORE) 0x100
    // | 취소(MB_YESNOCANCEL) 0x4000 | 아니오(MB_YESNO) 0x20000 | 취소(MB_RETRYCANCEL) 0x200000
    // — 덮어쓰기·저장 같은 진행 쪽 답을 피하고 닫는 쪽 답을 고른다.
    private const int MessageBoxMode = 0x00224121;

    private const int RpcEServerDied = unchecked((int)0x80010007);
    private const int RpcEServerDiedDne = unchecked((int)0x80010012);
    private const int RpcSServerUnavailable = unchecked((int)0x800706BA);
    private const int RpcEDisconnected = unchecked((int)0x80010108);
    private const int RpcSCallFailed = unchecked((int)0x800706BE);

    private const string NotInstalledMessage = "한/글 2014 이상이 설치되어 있지 않아 변환할 수 없습니다. 한/글을 설치한 뒤 다시 실행하세요.";
    private const string CreateFailedMessage = "한/글을 시작하지 못해 변환할 수 없습니다.";
    private const string ApprovalPopupNotice = "보안 모듈을 등록하지 못해 파일마다 한/글 승인 팝업이 뜰 수 있습니다.";

    private readonly string _exeDir;
    private readonly StaDispatcher _sta = new();
    private readonly Lock _startGate = new();
    private Task<EngineStart>? _start;
    private int _disposed;

    // 아래 필드는 STA 스레드에서만 읽고 쓴다.
    private Type? _hwpType;
    private HwpSession? _session;
    private bool _securityModuleRegistered;

    private HwpComEngine(string exeDir)
    {
        _exeDir = exeDir;
    }

    /// <summary>공개 진입점. exeDir 는 보안 모듈 DLL 을 찾을 exe 폴더.</summary>
    public static HwpComEngine Create(string exeDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(exeDir);
        return new HwpComEngine(exeDir);
    }

    /// <inheritdoc />
    public Task<EngineStart> StartAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        lock (_startGate)
        {
            _start ??= StartCoreAsync();
            return _start.WaitAsync(ct);
        }
    }

    /// <inheritdoc />
    public Task<ConversionOutcome> ConvertAsync(ConversionJob job, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(job);
        ct.ThrowIfCancellationRequested();

        var start = Volatile.Read(ref _start);
        if (Volatile.Read(ref _disposed) != 0 || start is null || !start.IsCompletedSuccessfully || !start.Result.Available)
        {
            return Task.FromResult(ConversionOutcome.Failure(FailureReason.Unexpected, "한/글 엔진이 시작되지 않았다."));
        }

        try
        {
            return _sta.InvokeAsync(() => ConvertOnSta(job));
        }
        catch (InvalidOperationException ex)
        {
            // Dispose 경합: 큐가 CompleteAdding(InvalidOperationException) 또는 Dispose(ObjectDisposedException,
            // InvalidOperationException 의 하위형)된 뒤의 Add. 파일 단위 실패로 번역한다.
            return Task.FromResult(ConversionOutcome.Failure(FailureReason.Unexpected, $"한/글 엔진이 이미 종료됐다. ({Describe(ex)})"));
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await _sta.InvokeAsync(QuitOnSta).ConfigureAwait(false);
        }
        finally
        {
            _sta.Dispose();
        }
    }

    /// <summary>
    /// 시작 경계: 동기 예외(ProgID 조회 실패, Dispose 경합으로 큐에 못 넣음 등)도 던지지 않고 시작 실패 결과로 바꾼다.
    /// 실패 결과도 성공과 같이 _start 에 캐시한다 — 실제 시작은 엔진 수명에 1회이고, 이후 StartAsync 는 같은 결과를 돌려준다.
    /// </summary>
    private Task<EngineStart> StartCoreAsync()
    {
        try
        {
            var hwpType = Type.GetTypeFromProgID(ProgId);
            if (hwpType is null)
            {
                return Task.FromResult(new EngineStart(false, NotInstalledMessage, []));
            }

            return _sta.InvokeAsync(() => StartOnSta(hwpType));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new EngineStart(false, $"{CreateFailedMessage} ({Describe(ex)})", []));
        }
    }

    private EngineStart StartOnSta(Type hwpType)
    {
        _hwpType = hwpType;
        try
        {
            _session = CreateSession(hwpType);
            var notice = RegisterSecurityModule(_session);
            return new EngineStart(true, null, notice is null ? [] : [notice]);
        }
        catch (Exception ex)
        {
            // 작업 단위 경계: 시작 중 어떤 예외든 세션을 정리하고(남기지 않음) 시작 실패(Available=false)로 번역한다.
            QuitOnSta();
            return new EngineStart(false, $"{CreateFailedMessage} ({Describe(ex)})", []);
        }
    }

    /// <summary>레지스트리 등록 + RegisterModule. 안내는 합쳐서 최대 1건(없으면 null).</summary>
    private string? RegisterSecurityModule(HwpSession session)
    {
        bool registered;
        string? registryNotice;
        try
        {
            registered = SecurityModule.TryRegister(_exeDir, out registryNotice);
        }
        catch (Exception)
        {
            // 등록 실패는 변환을 막지 않는다(F6): 예상 밖 예외도 안내 1건으로 번역.
            return ApprovalPopupNotice;
        }

        if (!registered)
        {
            return registryNotice ?? ApprovalPopupNotice;
        }

        _securityModuleRegistered = TryRegisterModule(session);
        return _securityModuleRegistered ? null : ApprovalPopupNotice;
    }

    private static bool TryRegisterModule(HwpSession session)
    {
        try
        {
            return session.RegisterModule(SecurityModuleType, SecurityModule.ValueName);
        }
        catch (Exception)
        {
            // 등록 실패는 변환을 막지 않는다(F6): COM·dynamic 바인더 등 어떤 예외든 false(안내)로 번역.
            return false;
        }
    }

    private static HwpSession CreateSession(Type hwpType)
    {
        var session = HwpSession.Create(hwpType);
        var ready = false;
        try
        {
            session.HideWindow();
            session.SetMessageBoxMode(MessageBoxMode);
            ready = true;
            return session;
        }
        finally
        {
            if (!ready)
            {
                // 정리 실패가 원래 예외를 가리지 않게 Quit 예외는 삼킨다.
                TryQuit(session);
            }
        }
    }

    private ConversionOutcome ConvertOnSta(ConversionJob job)
    {
        var session = _session;
        if (session is null)
        {
            return ConversionOutcome.Failure(FailureReason.EngineCrashed, "한/글 인스턴스 재생성에 실패해 변환할 수 없다.");
        }

        try
        {
            return ConvertWith(session, job);
        }
        catch (COMException ex) when (IsServerDied(ex.HResult))
        {
            var recreate = RecreateSession(session);
            return ConversionOutcome.Failure(FailureReason.EngineCrashed, $"{Describe(ex)}; {recreate}");
        }
        catch (Exception ex)
        {
            // 작업 단위 경계: 파일 단위 실패는 예외가 아니라 Outcome 으로 번역한다.
            return ConversionOutcome.Failure(FailureReason.Unexpected, Describe(ex));
        }
    }

    private static ConversionOutcome ConvertWith(HwpSession session, ConversionJob job)
    {
        var source = Path.GetFullPath(job.Input.SourcePath);
        var output = Path.GetFullPath(job.OutputPath);

        // 1차 방어: 문자열 비교라 심볼릭·하드 링크, 8.3 짧은 이름 같은 별칭은 못 잡는다.
        // 2차 방어: SaveAs 는 새 임시 이름에만 하고 File.Move(overwrite:false) 로 옮기므로, 별칭 때문에 출력이
        // 사실상 원본(=이미 있는 파일)이어도 Move 가 실패해 원본·기존 파일은 덮이지 않는다(F3).
        if (SamePath(source, output))
        {
            return ConversionOutcome.Failure(FailureReason.SaveFailed, "출력 경로가 원본 경로와 같음");
        }

        var sourceFormat = HwpFormatMap.ToHwpFormat(job.Route.SourceExtension);
        var targetFormat = HwpFormatMap.ToHwpFormat(job.Route.TargetExtension);
        var temp = NewTempPath(source, output);
        var moved = false;
        try
        {
            var saved = false;
            string? clearDetail = null;
            try
            {
                if (!session.Open(source, sourceFormat, OpenArgs))
                {
                    return ConversionOutcome.Failure(FailureReason.OpenFailed);
                }

                // 빠른 실패용 확인. 확인~저장 사이 틈은 아래 Move(overwrite:false) 가 막는다.
                if (OutputExists(output))
                {
                    return ConversionOutcome.Failure(FailureReason.SaveFailed, "출력 파일이 이미 있음");
                }

                saved = session.SaveAs(temp, targetFormat);
                if (!saved)
                {
                    return ConversionOutcome.Failure(FailureReason.SaveFailed);
                }
            }
            finally
            {
                // Clear(1) 는 Move 전에 한다: SaveAs 뒤 한/글의 현재 문서는 임시 파일이라, 닫기 전에는 임시 파일(과 원본)이
                // 잠겨 있을 수 있어 Move 가 공유 위반으로 실패한다. 닫은 뒤 옮기면 두 파일 모두 잠금이 풀려 있다.
                clearDetail = ClearDocument(session, saved);
            }

            var outcome = MoveIntoPlace(temp, output, clearDetail);
            moved = outcome.Succeeded;
            return outcome;
        }
        finally
        {
            if (!moved)
            {
                TryDeleteOwnTemp(temp, source, output);
            }
        }
    }

    private static bool OutputExists(string path) =>
        File.Exists(path) || Directory.Exists(path) || new FileInfo(path).LinkTarget is not null;

    private static bool SamePath(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>출력과 같은 폴더의 새 임시 이름 `&lt;이름&gt;.&lt;랜덤8자&gt;.tmp&lt;확장자&gt;`. 지금 없고 원본·출력과 다른 경로만 돌려준다.</summary>
    private static string NewTempPath(string source, string output)
    {
        var dir = Path.GetDirectoryName(output)!;
        var name = Path.GetFileNameWithoutExtension(output);
        var suffix = TempSuffix + Path.GetExtension(output);
        while (true)
        {
            var random = Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
            var temp = Path.Combine(dir, $"{name}.{random}{suffix}");
            if (!OutputExists(temp) && !SamePath(temp, source) && !SamePath(temp, output))
            {
                return temp;
            }
        }
    }

    /// <summary>저장 성공 뒤 Clear 의 비사망 예외는 성공을 뒤집지 않고 Detail 문구로 돌려준다. 사망 예외·저장 전 예외는 그대로 올린다.</summary>
    private static string? ClearDocument(HwpSession session, bool saved)
    {
        try
        {
            session.Clear();
            return null;
        }
        catch (Exception ex) when (saved && !(ex is COMException com && IsServerDied(com.HResult)))
        {
            return $"Clear 실패: {Describe(ex)}";
        }
    }

    /// <summary>임시 파일을 출력 이름으로 옮긴다. 대상이 이미 있으면 덮어쓰지 않고 SaveFailed(F3).</summary>
    private static ConversionOutcome MoveIntoPlace(string temp, string output, string? clearDetail)
    {
        try
        {
            File.Move(temp, output, overwrite: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 확인~저장 틈에 대상이 생겼으면 "이미 있음", 그 밖의 IO·권한 실패(잠금·접근 거부 등)는 원문을 남긴다.
            var detail = clearDetail is null ? Describe(ex) : $"{Describe(ex)}; {clearDetail}";
            return ConversionOutcome.Failure(FailureReason.SaveFailed, OutputExists(output) ? "출력 파일이 이미 있음" : detail);
        }

        return new ConversionOutcome(true, null, clearDetail);
    }

    /// <summary>
    /// 우리가 만든 임시 파일만 지운다: NewTempPath 가 없음을 확인한 경로이고, 원본·출력과 다르고, 임시 접미사로 끝날 때만.
    /// 정리 실패는 변환 결과를 바꾸지 않는다(임시 파일이 남을 수 있음).
    /// </summary>
    private static bool TryDeleteOwnTemp(string temp, string source, string output)
    {
        if (SamePath(temp, source) || SamePath(temp, output)
            || !temp.EndsWith(TempSuffix + Path.GetExtension(output), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            File.Delete(temp);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>죽은 인스턴스를 해제하고 1회 다시 만든다. 실패하면 _session 은 null 로 남아 이후 파일은 EngineCrashed.</summary>
    private string RecreateSession(HwpSession dead)
    {
        dead.Release();
        _session = null;
        try
        {
            var session = CreateSession(_hwpType!);
            _session = session;
            return !_securityModuleRegistered || TryRegisterModule(session)
                ? "한/글 재시작됨"
                : "한/글 재시작됨(보안 모듈 재등록 실패: 승인 팝업이 뜰 수 있음)";
        }
        catch (Exception ex)
        {
            // 복구 실패를 기록으로 번역: 이후 파일은 ConvertOnSta 에서 EngineCrashed.
            return $"한/글 재시작 실패({Describe(ex)})";
        }
    }

    private bool QuitOnSta()
    {
        var session = _session;
        _session = null;
        return session is null || TryQuit(session);
    }

    private static bool TryQuit(HwpSession session)
    {
        try
        {
            session.Quit();
            return true;
        }
        catch (Exception)
        {
            // 종료 경계: 서버가 이미 끝났거나 Quit 가 실패해도 할 일이 없다(COM 개체 해제는 Quit 의 finally 에서 끝남).
            return false;
        }
    }

    private static bool IsServerDied(int hresult) =>
        hresult is RpcEServerDied or RpcEServerDiedDne or RpcSServerUnavailable or RpcEDisconnected or RpcSCallFailed;

    private static string Describe(Exception ex) =>
        $"{ex.GetType().Name} HRESULT=0x{ex.HResult:X8}: {ex.Message}";
}
