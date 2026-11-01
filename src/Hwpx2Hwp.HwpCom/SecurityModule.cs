using System.Security;
using Microsoft.Win32;

namespace Hwpx2Hwp.HwpCom;

/// <summary>
/// F6 보안 모듈 등록(docs/design/baseline.md D-7).
/// exe 폴더에서 정해진 이름의 DLL 을 찾아 HKCU\Software\HNC\HwpAutomation\Modules 의
/// 문자열 값 FilePathCheckerModule 하나만 쓴다. 같으면 생략, 삭제 없음, HKLM 금지.
/// </summary>
internal static class SecurityModule
{
    internal const string ValueName = "FilePathCheckerModule";

    private const string ModulesKeyPath = @"Software\HNC\HwpAutomation\Modules";

    private const string NoticePrefix = "보안 모듈을 등록하지 못해 한/글 승인 팝업이 뜰 수 있음";

    // 찾을 DLL 이름 목록(이 배열 한 곳). 실제 배포 이름은 Windows 미확인(열린 문제 1):
    // 첫 후보는 한컴 예제 이름이다.
    private static readonly string[] DllNames =
    [
        "FilePathCheckerModuleExample.dll",
    ];

    /// <summary>
    /// DLL 을 찾아 레지스트리 값을 제자리에 둔다. 성공이면 true·notice null,
    /// DLL 없음·쓰기 실패면 false 와 안내 문구. 예외를 밖으로 던지지 않는다.
    /// </summary>
    internal static bool TryRegister(string exeDir, out string? notice)
    {
        ArgumentException.ThrowIfNullOrEmpty(exeDir);

        string? dllPath = FindDll(exeDir);
        if (dllPath is null)
        {
            notice = $"{NoticePrefix} (exe 폴더에 보안 모듈 DLL 없음)";
            return false;
        }

        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(ModulesKeyPath, writable: true);
            if (key.GetValue(ValueName) is string current
                && string.Equals(current, dllPath, StringComparison.OrdinalIgnoreCase))
            {
                notice = null;
                return true;
            }

            // 기존 값이 없거나 다른 DLL 경로면 이 exe 폴더의 DLL 경로로 덮어쓴다(D-7, 삭제는 하지 않음).
            key.SetValue(ValueName, dllPath, RegistryValueKind.String);
            notice = null;
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException
                                   or ArgumentException or NotSupportedException)
        {
            notice = $"{NoticePrefix} (레지스트리 쓰기 실패: {ex.GetType().Name})";
            return false;
        }
    }

    // 고정 이름만 exeDir 에 붙인다. 링크(ReparsePoint)인 파일은 건너뛴다.
    private static string? FindDll(string exeDir)
    {
        foreach (string name in DllNames)
        {
            try
            {
                string path = Path.GetFullPath(Path.Combine(exeDir, name));
                if (!File.Exists(path))
                {
                    continue;
                }

                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                return path;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException or NotSupportedException)
            {
                // 이 후보는 확인할 수 없으므로 다음 후보로 넘어간다(DLL 없음과 같게 처리).
                continue;
            }
        }

        return null;
    }
}
