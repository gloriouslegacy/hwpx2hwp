using Hwpx2Hwp.Core.Collect;
using Hwpx2Hwp.Core.Model;

namespace Hwpx2Hwp.Core.Tests;

/// <summary>F2 입력 수집(T2.1~T2.5). 테스트마다 새 임시 폴더를 만들고 끝나면 지운다.</summary>
public sealed class InputCollectorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "hwpx2hwp-tests", Guid.NewGuid().ToString("N"));

    public InputCollectorTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Touch(params string[] parts)
    {
        var path = Path.Combine([_root, .. parts]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        return path;
    }

    private static InputCollector NewCollector() => new(ConversionRoute.HwpxToHwp);

    [Fact]
    public void T2_1_하위_폴더_2단계까지_수집하고_기준_폴더를_남김()
    {
        var top = Touch("in", "top.hwpx");
        var level1 = Touch("in", "a", "one.hwpx");
        var level2 = Touch("in", "a", "b", "two.hwpx");
        var folder = Path.Combine(_root, "in");
        var collector = NewCollector();

        collector.Add([folder]);

        Assert.Equal(
            new[] { top, level1, level2 }.Order(StringComparer.Ordinal),
            collector.Items.Select(i => i.SourcePath).Order(StringComparer.Ordinal));
        Assert.All(collector.Items, i => Assert.Equal(folder, i.BaseFolder));
    }

    [Fact]
    public void T2_2_대문자_HWPX_도_수집()
    {
        var upper = Touch("in", "UPPER.HWPX");
        var mixed = Touch("in", "sub", "Mixed.HwPx");
        var collector = NewCollector();

        collector.Add([Path.Combine(_root, "in")]);

        Assert.Equal(
            new[] { upper, mixed }.Order(StringComparer.Ordinal),
            collector.Items.Select(i => i.SourcePath).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void T2_3_같은_파일_두_번이면_하나만_남김()
    {
        var file = Touch("in", "a.hwpx");
        var collector = NewCollector();

        collector.Add([file]);
        collector.Add([file, Path.Combine(_root, "in", ".", "a.hwpx")]);
        collector.Add([Path.Combine(_root, "in")]);

        var item = Assert.Single(collector.Items);
        Assert.Equal(new InputItem(file, null), item);
    }

    [Fact]
    public void T2_5_뺀_파일은_다시_추가되고_비우면_목록이_빔()
    {
        var a = Touch("a.hwpx");
        var b = Touch("b.hwpx");
        var collector = NewCollector();
        collector.Add([a, b]);

        collector.Remove(collector.Items[0]);
        Assert.Equal([new InputItem(b, null)], collector.Items);

        collector.Add([a]);
        Assert.Equal([new InputItem(b, null), new InputItem(a, null)], collector.Items);

        collector.Clear();
        Assert.Empty(collector.Items);
        collector.Add([a]);
        Assert.Single(collector.Items);
    }

    [Fact]
    public void T2_4_hwp_txt_와_없는_경로는_제외()
    {
        var hwpx = Touch("in", "keep.hwpx");
        var hwp = Touch("in", "skip.hwp");
        Touch("in", "sub", "skip.txt");
        Touch("in", "sub", "skip.hwpx.bak");
        var collector = NewCollector();

        collector.Add([Path.Combine(_root, "in"), hwp, Path.Combine(_root, "missing.hwpx"), Path.Combine(_root, "nope")]);

        var item = Assert.Single(collector.Items);
        Assert.Equal(hwpx, item.SourcePath);
    }

    [Fact]
    public void X_Collect_1_상위를_가리키는_폴더_링크는_따라가지_않음()
    {
        var file = Touch("in", "a.hwpx");
        var folder = Path.Combine(_root, "in");
        Directory.CreateSymbolicLink(Path.Combine(folder, "loop"), folder);
        var collector = NewCollector();

        collector.Add([folder]);

        var item = Assert.Single(collector.Items);
        Assert.Equal(new InputItem(file, folder), item);
    }

    [Fact]
    public void X_Collect_2_잘못된_형식_경로는_건너뛰고_나머지는_추가()
    {
        var single = Touch("single.hwpx");
        var inFolder = Touch("in", "a.hwpx");
        var folder = Path.Combine(_root, "in");
        var collector = NewCollector();

        collector.Add([single, Path.Combine(_root, "bad\0name.hwpx"), "bad\0folder", folder]);

        Assert.Equal([new InputItem(single, null), new InputItem(inFolder, folder)], collector.Items);
    }
}
