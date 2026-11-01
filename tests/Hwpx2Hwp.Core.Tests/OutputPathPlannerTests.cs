using Hwpx2Hwp.Core.Model;
using Hwpx2Hwp.Core.Paths;

namespace Hwpx2Hwp.Core.Tests;

/// <summary>F3 출력 경로 계산 T3.1~T3.5. 테스트마다 새 임시 폴더를 만들고 끝나면 지운다.</summary>
public sealed class OutputPathPlannerTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "hwpx2hwp-tests-" + Guid.NewGuid().ToString("N"));

    public OutputPathPlannerTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Touch(params string[] parts)
    {
        var path = Path.Combine([_root, .. parts]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, []);
        return path;
    }

    private string Under(params string[] parts) => Path.Combine([_root, .. parts]);

    [Fact]
    public void T3_1_출력_폴더_없으면_원본과_같은_폴더()
    {
        var source = Touch("docs", "a.hwpx");
        var planner = new OutputPathPlanner(ConversionRoute.HwpxToHwp, null);

        var output = planner.Reserve(new InputItem(source, null));

        Assert.Equal(Under("docs", "a.hwp"), output);
        Assert.NotEqual(source, output, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void T3_2_출력_폴더_지정하면_하위_구조_유지()
    {
        var baseFolder = Under("in");
        var nested = Touch("in", "sub", "deep", "b.hwpx");
        var top = Touch("in", "c.hwpx");
        var single = Touch("elsewhere", "d.hwpx");
        var outFolder = Under("out");
        var planner = new OutputPathPlanner(ConversionRoute.HwpxToHwp, outFolder);

        Assert.Equal(Under("out", "sub", "deep", "b.hwp"), planner.Reserve(new InputItem(nested, baseFolder)));
        Assert.Equal(Under("out", "c.hwp"), planner.Reserve(new InputItem(top, baseFolder)));
        Assert.Equal(Under("out", "d.hwp"), planner.Reserve(new InputItem(single, null)));
        Assert.False(Directory.Exists(outFolder), "Reserve 는 디스크에 폴더를 만들지 않는다");
    }

    [Fact]
    public void T3_3_기존_a_있으면_a_1()
    {
        var source = Touch("a.hwpx");
        Touch("a.hwp");
        var planner = new OutputPathPlanner(ConversionRoute.HwpxToHwp, null);

        Assert.Equal(Under("a_1.hwp"), planner.Reserve(new InputItem(source, null)));
    }

    [Fact]
    public void T3_4_기존_a와_a_1_있으면_a_2()
    {
        var source = Touch("a.hwpx");
        Touch("a.hwp");
        Touch("a_1.hwp");
        var planner = new OutputPathPlanner(ConversionRoute.HwpxToHwp, null);

        Assert.Equal(Under("a_2.hwp"), planner.Reserve(new InputItem(source, null)));
    }

    [Fact]
    public void T3_5_같은_실행에서_같은_출력_이름이면_겹치지_않음()
    {
        var first = Touch("x", "a.hwpx");
        var second = Touch("y", "a.hwpx");
        var upper = Touch("z", "A.hwpx");
        var planner = new OutputPathPlanner(ConversionRoute.HwpxToHwp, Under("out"));

        Assert.Equal(Under("out", "a.hwp"), planner.Reserve(new InputItem(first, null)));
        Assert.Equal(Under("out", "a_1.hwp"), planner.Reserve(new InputItem(second, null)));
        Assert.Equal(Under("out", "A_2.hwp"), planner.Reserve(new InputItem(upper, null)));
    }

    [Fact]
    public void X_Paths_1_BaseFolder_밖_입력이면_ArgumentException()
    {
        var outside = Touch("other", "a.hwpx");
        var planner = new OutputPathPlanner(ConversionRoute.HwpxToHwp, Under("out"));

        Assert.Throws<ArgumentException>(() => planner.Reserve(new InputItem(outside, Under("in"))));
    }

    [Fact]
    public void X_Paths_2_같은_이름_폴더_있으면_a_1()
    {
        var source = Touch("a.hwpx");
        Directory.CreateDirectory(Under("a.hwp"));
        var planner = new OutputPathPlanner(ConversionRoute.HwpxToHwp, null);

        Assert.Equal(Under("a_1.hwp"), planner.Reserve(new InputItem(source, null)));
    }

    [Fact]
    public void X_Paths_3_이름이_점점으로_시작하는_하위_폴더는_정상_예약()
    {
        var source = Touch("in", "..foo", "a.hwpx");
        var planner = new OutputPathPlanner(ConversionRoute.HwpxToHwp, Under("out"));

        Assert.Equal(Under("out", "..foo", "a.hwp"), planner.Reserve(new InputItem(source, Under("in"))));
    }

    [Fact]
    public void X_Paths_4_수집_뒤_기준_폴더가_사라져도_경로만_계산()
    {
        // Reserve 는 경로 계산만 하고 디스크를 확인하지 않으므로(문서화된 예외 없음) 기준 폴더가 없어도 예외가 없다.
        var source = Touch("in", "sub", "a.hwpx");
        var item = new InputItem(source, Under("in"));
        Directory.Delete(Under("in"), recursive: true);
        var planner = new OutputPathPlanner(ConversionRoute.HwpxToHwp, Under("out"));

        Assert.Equal(Under("out", "sub", "a.hwp"), planner.Reserve(item));
    }
}
