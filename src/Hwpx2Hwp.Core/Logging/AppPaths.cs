namespace Hwpx2Hwp.Core.Logging;

/// <summary>앱이 쓰는 고정 경로. D-11, F5.</summary>
public static class AppPaths
{
    /// <summary>
    /// 로그 폴더: %LOCALAPPDATA%\hwpx2hwp\logs. 폴더 생성은 RunLog.Create 가 한다.
    /// LocalApplicationData 를 못 얻으면(GetFolderPath 가 빈 문자열) 현재 폴더 기준 상대 경로가 되지 않게
    /// 임시 폴더(Path.GetTempPath()) 아래 hwpx2hwp\logs 를 쓴다.
    /// </summary>
    public static string LogDirectory => LogDirectoryUnder(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    /// <summary>LogDirectory 의 계산부. internal 인 이유: 빈 문자열 대체를 테스트하려고.</summary>
    internal static string LogDirectoryUnder(string localAppData) =>
        Path.Combine(
            string.IsNullOrEmpty(localAppData) ? Path.GetTempPath() : localAppData,
            "hwpx2hwp",
            "logs");
}
