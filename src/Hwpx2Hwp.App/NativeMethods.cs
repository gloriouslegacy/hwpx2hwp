using System.Runtime.InteropServices;

namespace Hwpx2Hwp.App;

/// <summary>user32 창 활성화 P/Invoke. F7 중복 실행 때 기존 창을 앞으로 가져오는 데만 쓴다.</summary>
internal static class NativeMethods
{
    /// <summary>최소화·최대화된 창을 원래 크기·위치로 되돌린다.</summary>
    internal const int SwRestore = 9;

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);
}
