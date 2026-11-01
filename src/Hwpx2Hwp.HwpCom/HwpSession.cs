using System.Globalization;
using System.Runtime.InteropServices;

namespace Hwpx2Hwp.HwpCom;

/// <summary>
/// 한/글 인스턴스 1개(late binding). dynamic 은 이 파일에만 둔다. 한/글 2014 에 있는 API 만 쓴다.
/// 모든 메서드는 StaDispatcher 스레드에서만 부른다. D-6.
/// </summary>
internal sealed class HwpSession
{
    private dynamic? _hwp;

    private HwpSession(object hwp)
    {
        _hwp = hwp;
    }

    private dynamic Hwp => _hwp ?? throw new ObjectDisposedException(nameof(HwpSession));

    /// <summary>ProgID 로 얻은 Type 으로 한/글 인스턴스를 만든다.</summary>
    internal static HwpSession Create(Type hwpType)
    {
        var instance = Activator.CreateInstance(hwpType)
            ?? throw new InvalidOperationException("한/글 인스턴스를 만들지 못했다.");
        return new HwpSession(instance);
    }

    /// <summary>첫 창을 숨긴다(XHwpWindows.Item(0).Visible = false).</summary>
    internal void HideWindow()
    {
        object? windows = null;
        object? window = null;
        try
        {
            windows = Hwp.XHwpWindows;
            window = ((dynamic)windows).Item(0);
            ((dynamic)window).Visible = false;
        }
        finally
        {
            ReleaseIfCom(window);
            ReleaseIfCom(windows);
        }
    }

    internal bool RegisterModule(string moduleType, string moduleData) =>
        ToBool(Hwp.RegisterModule(moduleType, moduleData));

    internal void SetMessageBoxMode(int mode) => Hwp.SetMessageBoxMode(mode);

    internal bool Open(string path, string format, string arg) => ToBool(Hwp.Open(path, format, arg));

    internal bool SaveAs(string path, string format) => ToBool(Hwp.SaveAs(path, format, ""));

    /// <summary>현재 문서를 변경 버림으로 닫는다(Clear(1)). 원본 잠금을 푼다.</summary>
    internal void Clear() => Hwp.Clear(1);

    /// <summary>한/글을 끝내고 COM 개체를 해제한다. Quit 가 실패해도 해제는 한다.</summary>
    internal void Quit()
    {
        try
        {
            Hwp.Quit();
        }
        finally
        {
            Release();
        }
    }

    /// <summary>Quit 없이 COM 개체만 해제한다(서버가 이미 죽었을 때).</summary>
    internal void Release()
    {
        var hwp = (object?)_hwp;
        _hwp = null;
        ReleaseIfCom(hwp);
    }

    // 반환형이 bool 또는 정수(VARIANT_BOOL·BOOL)여도 같은 뜻으로 읽는다.
    private static bool ToBool(object? value) => Convert.ToBoolean(value, CultureInfo.InvariantCulture);

    private static void ReleaseIfCom(object? comObject)
    {
        if (comObject is not null && Marshal.IsComObject(comObject))
        {
            Marshal.FinalReleaseComObject(comObject);
        }
    }
}
