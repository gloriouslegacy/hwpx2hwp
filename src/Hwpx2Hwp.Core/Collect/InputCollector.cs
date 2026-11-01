using Hwpx2Hwp.Core.Model;

namespace Hwpx2Hwp.Core.Collect;

/// <summary>
/// F2 입력 수집. 파일은 그대로, 폴더는 하위 폴더까지 재귀로 Route.SourceExtension 파일을 모은다.
/// 중복은 전체 경로 정규화 후 OrdinalIgnoreCase 로 판정한다. 없는 경로·접근 불가 폴더는 건너뛴다. D-4, D-5.
/// </summary>
public sealed class InputCollector
{
    private static readonly EnumerationOptions RecursiveOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        // 기본값(Hidden|System)에 ReparsePoint 를 더해 심볼릭 링크·정션 재귀(순환)를 막는다.
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
    };

    private readonly ConversionRoute _route;
    private readonly List<InputItem> _items = [];
    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

    public InputCollector(ConversionRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        _route = route;
        Items = _items.AsReadOnly();
    }

    /// <summary>추가 순서대로의 수집 목록(읽기 전용).</summary>
    public IReadOnlyList<InputItem> Items { get; }

    /// <summary>파일·폴더 경로를 넣는다. 대상 확장자가 아니거나 이미 있는 파일, 없는·잘못된 형식·접근 불가 경로는 조용히 건너뛴다.</summary>
    public void Add(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        foreach (var path in paths)
        {
            try
            {
                if (File.Exists(path))
                {
                    TryAdd(path, null);
                }
                else if (Directory.Exists(path))
                {
                    AddFolder(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or ArgumentException or NotSupportedException)
            {
                // 복구: 잘못된 형식·너무 긴 경로, Exists 확인 뒤 사라졌거나 접근이 막힌 폴더는 그 항목만 건너뛴다.
                // 이미 모은 항목은 유지하고 나머지 경로는 계속 추가한다(PathTooLongException 은 IOException).
            }
        }
    }

    /// <summary>목록에서 항목 하나를 뺀다. 뺀 파일은 다시 추가할 수 있다.</summary>
    public void Remove(InputItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (_items.Remove(item))
        {
            _seen.Remove(item.SourcePath);
        }
    }

    /// <summary>목록을 비운다.</summary>
    public void Clear()
    {
        _items.Clear();
        _seen.Clear();
    }

    private void AddFolder(string folder)
    {
        foreach (var file in Directory.EnumerateFiles(folder, "*", RecursiveOptions))
        {
            TryAdd(file, folder);
        }
    }

    private void TryAdd(string file, string? baseFolder)
    {
        if (!string.Equals(Path.GetExtension(file), _route.SourceExtension, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var fullPath = Path.GetFullPath(file);
        if (_seen.Add(fullPath))
        {
            _items.Add(new InputItem(fullPath, baseFolder));
        }
    }
}
