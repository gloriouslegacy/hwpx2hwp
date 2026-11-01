using System.IO.Compression;
using System.Xml;

namespace Hwpx2Hwp.Core.Probe;

/// <summary>
/// hwpx(OWPML zip) 를 읽기 전용으로 열어 암호 여부를 미리 본다. D-5 "암호 사전 검사(승인)".
/// 원본은 FileAccess.Read·FileShare.Read 로만 연다.
/// </summary>
public static class HwpxProbe
{
    // 판정 규칙은 여기 상수들에만 둔다. 실제 한/글 암호 문서의 manifest 표기는
    // Windows 확인 대기(baseline 열린 문제 2) — 확인되면 이 줄들만 고친다.
    private const string ManifestEntryName = "META-INF/manifest.xml";
    private const string EncryptionElementLocalName = "encryption-data";

    // manifest 크기 상한(엔트리 Length 바이트, XML 문자 수 모두). 거대 manifest 로 검사가 늘어지는 것을 막는다.
    private const int MaxManifestSize = 1024 * 1024;

    /// <summary>
    /// zip 이 아니거나 깨졌으면 NotHwpx, manifest 에 암호화 항목(로컬 이름 encryption-data, 네임스페이스 무관)이
    /// 있으면 Encrypted, 없거나 manifest 가 없으면 Ok. manifest 이름은 '\'→'/' 후 대소문자 무시로 찾고,
    /// 후보가 2개 이상이거나 크기 상한을 넘으면 NotHwpx(판정 우회 방지).
    /// </summary>
    /// <exception cref="ArgumentException">path 가 null·빈 문자열·공백일 때.</exception>
    /// <exception cref="IOException">파일이 없거나 열 수 없을 때(존재 확인은 호출자 몫, D-5 파일별 순서).</exception>
    /// <exception cref="UnauthorizedAccessException">파일 읽기 권한이 없을 때(파일 단위 번역은 호출자 몫).</exception>
    public static ProbeResult Inspect(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            using var zip = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: true);
            var candidates = zip.Entries
                .Where(e => string.Equals(e.FullName.Replace('\\', '/'), ManifestEntryName, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToList();
            if (candidates.Count == 0)
            {
                return ProbeResult.Ok;
            }

            var manifest = candidates[0];
            if (candidates.Count > 1 || manifest.Length > MaxManifestSize)
            {
                return ProbeResult.NotHwpx;
            }

            using var stream = manifest.Open();
            return HasEncryptionElement(stream) ? ProbeResult.Encrypted : ProbeResult.Ok;
        }
        catch (InvalidDataException)
        {
            // zip 이 아니거나 압축 데이터가 깨졌다: 판정 결과로 번역한다.
            return ProbeResult.NotHwpx;
        }
        catch (XmlException)
        {
            // manifest 가 올바른 XML 이 아니다(DTD 금지 위반 포함): 판정 결과로 번역한다.
            return ProbeResult.NotHwpx;
        }
    }

    private static bool HasEncryptionElement(Stream manifest)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxManifestSize,
            IgnoreComments = true,
            IgnoreWhitespace = true,
            IgnoreProcessingInstructions = true,
        };

        using var reader = XmlReader.Create(manifest, settings);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element
                && string.Equals(reader.LocalName, EncryptionElementLocalName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
