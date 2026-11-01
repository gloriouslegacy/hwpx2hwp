using Hwpx2Hwp.Core.Model;

namespace Hwpx2Hwp.Core.Paths;

/// <summary>
/// F3 출력 경로 계산. 디스크에 있는 파일과 이 인스턴스가 이미 예약한 경로를 피해 `이름_N` 을 고른다(D1).
/// 인스턴스 1개 = 실행 1회. 디스크에는 아무것도 만들지 않는다(폴더 생성은 BatchRunner 몫). D-5.
/// </summary>
public sealed class OutputPathPlanner
{
    private readonly ConversionRoute _route;
    private readonly string? _outputFolder;
    private readonly HashSet<string> _reserved = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="route">대상 확장자를 정하는 변환 경로.</param>
    /// <param name="outputFolder">출력 폴더. null 이면 원본과 같은 폴더.</param>
    public OutputPathPlanner(ConversionRoute route, string? outputFolder)
    {
        ArgumentNullException.ThrowIfNull(route);
        _route = route;
        _outputFolder = outputFolder is null ? null : Path.GetFullPath(outputFolder);
    }

    /// <summary>
    /// item 의 출력 경로를 정하고 예약한다. 결과는 기존 파일·이미 예약한 경로·입력 경로와 같지 않다(Q2).
    /// </summary>
    public string Reserve(InputItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var source = Path.GetFullPath(item.SourcePath);
        var folder = TargetFolder(source, item.BaseFolder);
        var name = Path.GetFileNameWithoutExtension(source);

        for (var n = 0; ; n++)
        {
            var fileName = (n == 0 ? name : $"{name}_{n}") + _route.TargetExtension;
            var candidate = Path.Combine(folder, fileName);
            if (!IsTaken(candidate, source))
            {
                _reserved.Add(candidate);
                return candidate;
            }
        }
    }

    private string TargetFolder(string source, string? baseFolder)
    {
        var sourceFolder = Path.GetDirectoryName(source)!;
        if (_outputFolder is null)
        {
            return sourceFolder;
        }

        if (baseFolder is null)
        {
            return _outputFolder;
        }

        var relative = Path.GetRelativePath(Path.GetFullPath(baseFolder), sourceFolder);
        var firstSegment = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        if (firstSegment == ".." || Path.IsPathRooted(relative))
        {
            // Q2 불변식: 결과가 출력 폴더 밖을 가리키면 안 된다. BaseFolder 는 SourcePath 의 상위여야 한다.
            throw new ArgumentException(
                $"BaseFolder 가 SourcePath 의 상위 폴더가 아니다. relative={relative}", nameof(baseFolder));
        }

        return Path.GetFullPath(Path.Combine(_outputFolder, relative));
    }

    private bool IsTaken(string candidate, string source) =>
        _reserved.Contains(candidate)
        || string.Equals(candidate, source, StringComparison.OrdinalIgnoreCase)
        || File.Exists(candidate)
        || Directory.Exists(candidate);
}
