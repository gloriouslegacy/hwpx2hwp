using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Hwpx2Hwp.Core.Probe;

namespace Hwpx2Hwp.Core.Tests;

/// <summary>
/// 암호 사전 검사(명세 T 아님 → X 번호). D-5. 합성 zip 은 테스트 안에서 만들고 바이너리 샘플은 두지 않는다.
/// 테스트마다 새 임시 폴더를 만들고 끝나면 지운다.
/// </summary>
public sealed class HwpxProbeTests : IDisposable
{
    private const string OdfManifestNs = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";

    private static readonly string PlainManifest =
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <manifest:manifest xmlns:manifest="{OdfManifestNs}">
          <manifest:file-entry manifest:full-path="/" manifest:media-type="application/hwp+zip"/>
          <manifest:file-entry manifest:full-path="Contents/section0.xml" manifest:media-type="application/xml"/>
        </manifest:manifest>
        """;

    private static readonly string EncryptedManifest =
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <manifest:manifest xmlns:manifest="{OdfManifestNs}">
          <manifest:file-entry manifest:full-path="/" manifest:media-type="application/hwp+zip"/>
          <manifest:file-entry manifest:full-path="Contents/section0.xml" manifest:media-type="application/xml">
            <manifest:encryption-data manifest:checksum-type="SHA1" manifest:checksum="AAAA">
              <manifest:algorithm manifest:algorithm-name="Blowfish CFB"/>
            </manifest:encryption-data>
          </manifest:file-entry>
        </manifest:manifest>
        """;

    private readonly string _dir;

    public HwpxProbeTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "hwpx2hwp-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string MakeZip(string name, string? manifestXml)
    {
        var path = Path.Combine(_dir, name);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            WriteEntry(zip, "mimetype", "application/hwp+zip");
            WriteEntry(zip, "Contents/section0.xml", "<sec/>");
            if (manifestXml is not null)
            {
                WriteEntry(zip, "META-INF/manifest.xml", manifestXml);
            }
        }

        return path;
    }

    private string MakeZipWithEntries(string name, params (string EntryName, string Content)[] entries)
    {
        var path = Path.Combine(_dir, name);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            foreach (var (entryName, content) in entries)
            {
                WriteEntry(zip, entryName, content);
            }
        }

        return path;
    }

    private static void WriteEntry(ZipArchive zip, string entryName, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(entryName).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    [Fact]
    public void X_Probe_1_정상_hwpx_는_Ok()
    {
        var path = MakeZip("a.hwpx", PlainManifest);

        Assert.Equal(ProbeResult.Ok, HwpxProbe.Inspect(path));
    }

    [Fact]
    public void X_Probe_2_zip_아니면_NotHwpx()
    {
        var path = Path.Combine(_dir, "b.hwpx");
        File.WriteAllText(path, "이건 zip 이 아니다");

        Assert.Equal(ProbeResult.NotHwpx, HwpxProbe.Inspect(path));
    }

    [Fact]
    public void X_Probe_3_manifest_에_encryption_data_있으면_Encrypted()
    {
        var path = MakeZip("c.hwpx", EncryptedManifest);

        Assert.Equal(ProbeResult.Encrypted, HwpxProbe.Inspect(path));
    }

    [Fact]
    public void X_Probe_4_encryption_data_는_네임스페이스_무관()
    {
        var path = MakeZip("d.hwpx", """<m xmlns="urn:other"><f><encryption-data/></f></m>""");

        Assert.Equal(ProbeResult.Encrypted, HwpxProbe.Inspect(path));
    }

    [Fact]
    public void X_Probe_5_manifest_없으면_Ok()
    {
        var path = MakeZip("e.hwpx", null);

        Assert.Equal(ProbeResult.Ok, HwpxProbe.Inspect(path));
    }

    [Fact]
    public void X_Probe_6_manifest_가_깨진_XML_이면_NotHwpx()
    {
        var path = MakeZip("f.hwpx", "<manifest><file-entry>");

        Assert.Equal(ProbeResult.NotHwpx, HwpxProbe.Inspect(path));
    }

    [Fact]
    public void X_Probe_7_DTD_가_든_manifest_는_NotHwpx()
    {
        var path = MakeZip("g.hwpx", """<!DOCTYPE m [<!ENTITY x "y">]><m>&x;</m>""");

        Assert.Equal(ProbeResult.NotHwpx, HwpxProbe.Inspect(path));
    }

    [Fact]
    public void X_Probe_8_검사_후_원본_바이트_그대로()
    {
        var path = MakeZip("h.hwpx", EncryptedManifest);
        var before = File.ReadAllBytes(path);
        var writeTime = File.GetLastWriteTimeUtc(path);

        HwpxProbe.Inspect(path);

        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(writeTime, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void X_Probe_9_1MiB_넘는_manifest_는_NotHwpx()
    {
        // 압축이 잘 되는 반복 문자열이라 zip 은 작고, 판정은 엔트리 Length 만 보고 끝난다.
        var huge = "<m>" + new string('a', 1024 * 1024) + "</m>";
        var path = MakeZip("i.hwpx", huge);

        Assert.Equal(ProbeResult.NotHwpx, HwpxProbe.Inspect(path));
    }

    [Fact]
    public void X_Probe_10_manifest_이름_대소문자가_달라도_암호면_Encrypted()
    {
        var path = MakeZipWithEntries(
            "j.hwpx",
            ("mimetype", "application/hwp+zip"),
            ("meta-inf/Manifest.xml", EncryptedManifest));

        Assert.Equal(ProbeResult.Encrypted, HwpxProbe.Inspect(path));
    }

    [Fact]
    public void X_Probe_11_대소문자만_다른_manifest_2개면_NotHwpx()
    {
        var path = MakeZipWithEntries(
            "k.hwpx",
            ("mimetype", "application/hwp+zip"),
            ("META-INF/manifest.xml", PlainManifest),
            ("meta-inf/MANIFEST.xml", EncryptedManifest));

        Assert.Equal(ProbeResult.NotHwpx, HwpxProbe.Inspect(path));
    }

    [Fact]
    public void X_Probe_12_없는_경로는_FileNotFoundException()
    {
        var path = Path.Combine(_dir, "없음.hwpx");

        var ex = Assert.Throws<FileNotFoundException>(() => HwpxProbe.Inspect(path));
        Assert.IsAssignableFrom<IOException>(ex);
    }

    [Fact]
    public void X_Probe_13_중앙_디렉터리_크기를_크게_위조한_manifest_는_NotHwpx()
    {
        var path = MakeZip("l.hwpx", PlainManifest);
        ForgeCentralUncompressedSize(path, "META-INF/manifest.xml", 0x7FFF_FFFF);

        Assert.Equal(ProbeResult.NotHwpx, HwpxProbe.Inspect(path));
    }

    [Fact]
    public void X_Probe_14_역슬래시_manifest_이름도_인식()
    {
        var path = MakeZipWithEntries(
            "m.hwpx",
            ("mimetype", "application/hwp+zip"),
            ("META-INF\\manifest.xml", EncryptedManifest));

        Assert.Equal(ProbeResult.Encrypted, HwpxProbe.Inspect(path));
    }

    // 중앙 디렉터리 항목(시그니처 PK\1\2)의 압축 전 크기(오프셋 24, 4바이트)만 바꾼다. 실제 데이터는 그대로다.
    private static void ForgeCentralUncompressedSize(string path, string entryName, uint size)
    {
        var bytes = File.ReadAllBytes(path);
        var name = Encoding.UTF8.GetBytes(entryName);
        for (var i = 0; i + 46 + name.Length <= bytes.Length; i++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i)) == 0x02014B50
                && BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i + 28)) == name.Length
                && bytes.AsSpan(i + 46, name.Length).SequenceEqual(name))
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i + 24), size);
                File.WriteAllBytes(path, bytes);
                return;
            }
        }

        throw new InvalidOperationException($"중앙 디렉터리 항목을 찾지 못했다: {entryName}");
    }
}
