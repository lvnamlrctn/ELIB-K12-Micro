using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;

namespace Elib.Identity.Application;

/// <summary>
/// CAPTCHA đăng nhập (monolith: CaptchaService). Mã 5 ký tự, dùng một lần, hạn 2 phút; đáp án nằm ở server (IDistributedCache).
/// Khác monolith: ảnh PNG vẽ từ bitmap font — không có thẻ &lt;text&gt; trong SVG để máy đọc thẳng ra đáp án.
/// </summary>
public sealed class CaptchaService(IDistributedCache cache)
{
    /// <summary>Bỏ 0/O, 1/I/L để người dùng không gõ nhầm.</summary>
    public const string Charset = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    public const int Length = 5;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    public sealed record Challenge(string Id, string PngBase64);

    public async Task<Challenge> CreateAsync(CancellationToken ct)
    {
        var code = new string(Enumerable.Range(0, Length).Select(_ => Charset[RandomNumberGenerator.GetInt32(Charset.Length)]).ToArray());
        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        await cache.SetStringAsync(Key(id), code, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Lifetime }, ct);
        return new Challenge(id, Convert.ToBase64String(CaptchaImage.Render(code)));
    }

    /// <summary>Đúng/sai đều xoá mã — không thử lại nhiều lần trên cùng một ảnh.</summary>
    public async Task<bool> ValidateAsync(string? id, string? answer, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 64) return false;
        var key = Key(id);
        var expected = await cache.GetStringAsync(key, ct);
        if (expected is null) return false;
        await cache.RemoveAsync(key, ct);
        var given = (answer ?? "").Trim().ToUpperInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(given));
    }

    public static string Key(string id) => "captcha:" + id;
}

/// <summary>Vẽ mã thành PNG: bitmap font 5×7, mỗi ký tự xoay/co giãn/lệch ngẫu nhiên, gợn sóng, đường và chấm nhiễu.</summary>
public static class CaptchaImage
{
    public const int Width = 180;
    public const int Height = 56;

    private static readonly Dictionary<char, string[]> Font = new()
    {
        ['A'] = [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
        ['B'] = ["####.", "#...#", "#...#", "####.", "#...#", "#...#", "####."],
        ['C'] = [".###.", "#...#", "#....", "#....", "#....", "#...#", ".###."],
        ['D'] = ["####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."],
        ['E'] = ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
        ['F'] = ["#####", "#....", "#....", "####.", "#....", "#....", "#...."],
        ['G'] = [".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###."],
        ['H'] = ["#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
        ['J'] = ["..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##.."],
        ['K'] = ["#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#"],
        ['M'] = ["#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#"],
        ['N'] = ["#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#"],
        ['P'] = ["####.", "#...#", "#...#", "####.", "#....", "#....", "#...."],
        ['Q'] = [".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#"],
        ['R'] = ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
        ['S'] = [".####", "#....", "#....", ".###.", "....#", "....#", "####."],
        ['T'] = ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
        ['U'] = ["#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
        ['V'] = ["#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.."],
        ['W'] = ["#...#", "#...#", "#...#", "#.#.#", "#.#.#", "#.#.#", ".#.#."],
        ['X'] = ["#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#"],
        ['Y'] = ["#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."],
        ['Z'] = ["#####", "....#", "...#.", "..#..", ".#...", "#....", "#####"],
        ['2'] = [".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####"],
        ['3'] = ["#####", "...#.", "..#..", "...#.", "....#", "#...#", ".###."],
        ['4'] = ["...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#."],
        ['5'] = ["#####", "#....", "####.", "....#", "....#", "#...#", ".###."],
        ['6'] = ["..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###."],
        ['7'] = ["#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..."],
        ['8'] = [".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###."],
        ['9'] = [".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.."],
    };

    private static readonly (byte R, byte G, byte B)[] Ink =
        [(30, 58, 138), (124, 45, 18), (20, 83, 45), (88, 28, 135), (127, 29, 29), (22, 78, 99)];

    public static IReadOnlyCollection<char> Glyphs => Font.Keys;

    public static byte[] Render(string code)
    {
        var rgb = new byte[Width * Height * 3];
        for (var i = 0; i < rgb.Length; i += 3) (rgb[i], rgb[i + 1], rgb[i + 2]) = ((byte)241, (byte)245, (byte)249);

        // Đường nhiễu phía sau chữ.
        for (var n = 0; n < 4; n++) Line(rgb, Rand(Width), Rand(Height), Rand(Width), Rand(Height), (148, 163, 184), 1);

        const int margin = 14; // chữ xoay không bị cắt ở mép ảnh
        var slot = (Width - 2 * margin) / (double)code.Length;
        var phase = RandomNumberGenerator.GetInt32(628) / 100.0;
        for (var i = 0; i < code.Length; i++)
        {
            var glyph = Font[code[i]];
            var color = Ink[Rand(Ink.Length)];
            var cx = margin + slot * i + slot / 2 + RandRange(-3, 3);
            var cy = Height / 2.0 + RandRange(-4, 4);
            var scale = 4.6 + RandRange(0, 7) / 10.0;
            var angle = RandRange(-22, 22) * Math.PI / 180;
            var (sin, cos) = Math.SinCos(angle);
            for (var y = 0; y < Height; y++)
            {
                for (var x = (int)Math.Max(0, cx - 30); x < Math.Min(Width, cx + 30); x++)
                {
                    var dx = x - cx + 1.8 * Math.Sin(y / 5.0 + phase);
                    var dy = y - cy;
                    var gx = (dx * cos + dy * sin) / scale + 2.5;
                    var gy = (-dx * sin + dy * cos) / scale + 3.5;
                    if (gx < 0 || gy < 0 || gx >= 5 || gy >= 7 || glyph[(int)gy][(int)gx] != '#') continue;
                    Set(rgb, x, y, color);
                }
            }
        }

        // Đường + chấm nhiễu đè lên chữ, cùng tông màu chữ.
        for (var n = 0; n < 3; n++) Line(rgb, Rand(Width), Rand(Height), Rand(Width), Rand(Height), Ink[Rand(Ink.Length)], 1);
        for (var n = 0; n < 220; n++) Set(rgb, Rand(Width), Rand(Height), Ink[Rand(Ink.Length)]);
        return EncodePng(rgb);
    }

    private static int Rand(int max) => RandomNumberGenerator.GetInt32(max);

    private static int RandRange(int min, int max) => RandomNumberGenerator.GetInt32(min, max + 1);

    private static void Set(byte[] rgb, int x, int y, (byte R, byte G, byte B) c)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;
        var i = (y * Width + x) * 3;
        (rgb[i], rgb[i + 1], rgb[i + 2]) = c;
    }

    private static void Line(byte[] rgb, int x0, int y0, int x1, int y1, (byte R, byte G, byte B) c, int thickness)
    {
        var steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
        for (var s = 0; s <= steps; s++)
        {
            var x = x0 + (x1 - x0) * s / Math.Max(1, steps);
            var y = y0 + (y1 - y0) * s / Math.Max(1, steps);
            for (var t = 0; t < thickness; t++) Set(rgb, x, y + t, c);
        }
    }

    /// <summary>PNG RGB 8-bit, không filter — đủ cho ảnh nhỏ, không cần thư viện ảnh.</summary>
    private static byte[] EncodePng(byte[] rgb)
    {
        using var raw = new MemoryStream();
        using (var z = new ZLibStream(raw, CompressionLevel.Fastest, leaveOpen: true))
        {
            for (var y = 0; y < Height; y++)
            {
                z.WriteByte(0);
                z.Write(rgb, y * Width * 3, Width * 3);
            }
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, Width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), Height);
        header[8] = 8; // bit depth
        header[9] = 2; // truecolor RGB
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", raw.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void Chunk(Stream png, string type, byte[] data)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, data.Length);
        png.Write(buffer);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        png.Write(typeBytes);
        png.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(buffer, Crc32(typeBytes, data));
        png.Write(buffer);
    }

    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    private static uint Crc32(byte[] type, byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in type) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (var b in data) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }
}
