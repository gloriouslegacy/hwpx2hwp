using System.Diagnostics.CodeAnalysis;
using Hwpx2Hwp.Core.Engine;
using Hwpx2Hwp.Core.Model;
using Hwpx2Hwp.Core.Tests.Fakes;

namespace Hwpx2Hwp.Core.Tests;

/// <summary>
/// T1.4 중 엔진 계약 몫: 가짜 엔진을 IConversionEngine 으로만 불러 기대한 결과가 나오는지.
/// 흐름 전체(BatchRunner)는 별도 테스트에서 확인한다.
/// </summary>
[SuppressMessage("Performance", "CA1859", Justification = "엔진을 IConversionEngine 으로만 부르는 것이 이 테스트의 목적")]
public sealed class EngineContractTests
{
    private static ConversionJob Job(int n) =>
        new(new InputItem($"in/{n}.hwpx", null), $"out/{n}.hwp", ConversionRoute.HwpxToHwp);

    private static async Task<List<ConversionOutcome>> ConvertAll(IConversionEngine engine, int count)
    {
        var outcomes = new List<ConversionOutcome>();
        for (var n = 1; n <= count; n++)
        {
            outcomes.Add(await engine.ConvertAsync(Job(n), CancellationToken.None));
        }

        return outcomes;
    }

    [Fact]
    public async Task T1_4_기본_시작은_사용_가능_안내_없음()
    {
        var fake = new FakeConversionEngine();
        IConversionEngine engine = fake;

        var start = await engine.StartAsync(CancellationToken.None);

        Assert.True(start.Available);
        Assert.Null(start.UnavailableMessage);
        Assert.Empty(start.Notices);
    }

    [Fact]
    public async Task T1_4_StartAsync_여러_번_불러도_실제_시작은_1회()
    {
        var fake = new FakeConversionEngine();
        IConversionEngine engine = fake;

        var first = await engine.StartAsync(CancellationToken.None);
        var second = await engine.StartAsync(CancellationToken.None);

        Assert.Equal(1, fake.StartCount);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task T1_4_미설치면_Available_false와_안내_문구()
    {
        var fake = new FakeConversionEngine { UnavailableMessage = "한/글이 설치되어 있지 않습니다." };
        IConversionEngine engine = fake;

        var start = await engine.StartAsync(CancellationToken.None);

        Assert.False(start.Available);
        Assert.Equal("한/글이 설치되어 있지 않습니다.", start.UnavailableMessage);
    }

    [Fact]
    public async Task T1_4_Notice_목록이_시작_결과에_그대로_나온다()
    {
        var fake = new FakeConversionEngine { Notices = ["승인 팝업이 뜰 수 있음"] };
        IConversionEngine engine = fake;

        var start = await engine.StartAsync(CancellationToken.None);

        Assert.True(start.Available);
        Assert.Equal(["승인 팝업이 뜰 수 있음"], start.Notices);
    }

    [Fact]
    public async Task T1_4_실패_설정_없으면_모두_성공하고_받은_작업을_순서대로_기록()
    {
        var fake = new FakeConversionEngine();
        IConversionEngine engine = fake;
        await engine.StartAsync(CancellationToken.None);

        var outcomes = await ConvertAll(engine, 3);

        Assert.All(outcomes, o => Assert.True(o.Succeeded));
        Assert.Equal([Job(1), Job(2), Job(3)], fake.Jobs);
    }

    [Fact]
    public async Task T1_4_지정_번째_변환만_지정_이유로_실패()
    {
        var fake = new FakeConversionEngine { FailAt = 2, FailureReason = FailureReason.SaveFailed };
        IConversionEngine engine = fake;
        await engine.StartAsync(CancellationToken.None);

        var outcomes = await ConvertAll(engine, 3);

        Assert.True(outcomes[0].Succeeded);
        Assert.False(outcomes[1].Succeeded);
        Assert.Equal(FailureReason.SaveFailed, outcomes[1].FailureReason);
        Assert.True(outcomes[2].Succeeded);
    }

    [Fact]
    public async Task T1_4_지정_번째_변환은_예외_다음_변환은_계속된다()
    {
        var fake = new FakeConversionEngine { ThrowAt = 2 };
        IConversionEngine engine = fake;
        await engine.StartAsync(CancellationToken.None);

        var first = await engine.ConvertAsync(Job(1), CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.ConvertAsync(Job(2), CancellationToken.None));
        var third = await engine.ConvertAsync(Job(3), CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(third.Succeeded);
        Assert.Equal(3, fake.Jobs.Count);
    }

    [Fact]
    public async Task T1_4_DisposeAsync_여부가_노출된다()
    {
        var fake = new FakeConversionEngine();
        IConversionEngine engine = fake;
        Assert.False(fake.Disposed);

        await engine.DisposeAsync();

        Assert.True(fake.Disposed);
    }
}
