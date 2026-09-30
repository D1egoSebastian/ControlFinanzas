using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using SkiaSharp;

namespace IconRenderer;

/// <summary>
/// Renders the ControlDeFinanzasIA icon set from <c>app-icon.svg</c> (and <c>app-icon-small.svg</c> for 16/24 px).
/// Usage: <c>IconRenderer &lt;iconFolder&gt;</c>. Outputs sized PNGs, <c>app-icon.png</c> (512), <c>app-icon.ico</c>
/// (16/24/32/48/64/256, PNG-compressed entries) and <c>app-icon.icns</c> (ic07..ic10).
/// </summary>
internal static class Program
{
    private static readonly int[] _pngSizes = [16, 24, 32, 48, 64, 128, 256, 512, 1024];
    private static readonly int[] _icoSizes = [16, 24, 32, 48, 64, 256];
    private static readonly (string Type, int Size)[] _icnsChunks = [("ic07", 128), ("ic08", 256), ("ic09", 512), ("ic10", 1024)];

    private static int Main(string[] args)
    {
        if (args.Length != 1 || !Directory.Exists(args[0]))
        {
            Console.Error.WriteLine("Usage: IconRenderer <icon folder containing app-icon.svg>");
            return 2;
        }

        string folder = args[0];
        List<Shape> full = Parse(Path.Combine(folder, "app-icon.svg"));
        List<Shape> small = Parse(Path.Combine(folder, "app-icon-small.svg"));

        var pngs = new Dictionary<int, byte[]>();
        foreach (int size in _pngSizes)
        {
            byte[] bytes = Render(size <= 24 ? small : full, size);
            pngs[size] = bytes;
            File.WriteAllBytes(Path.Combine(folder, $"app-icon-{size}.png"), bytes);
        }

        File.WriteAllBytes(Path.Combine(folder, "app-icon.png"), pngs[512]);
        File.WriteAllBytes(Path.Combine(folder, "app-icon.ico"), PackIco(pngs));
        File.WriteAllBytes(Path.Combine(folder, "app-icon.icns"), PackIcns(pngs));
        Console.WriteLine($"Icon set written to {folder}");
        return 0;
    }

    private static List<Shape> Parse(string path)
    {
        XDocument doc = XDocument.Load(path);
        var shapes = new List<Shape>();
        foreach (XElement e in doc.Root!.Descendants())
        {
            switch (e.Name.LocalName)
            {
                case "rect":
                    shapes.Add(new Shape(
                        SKColor.Parse((string)e.Attribute("fill")!),
                        new SKRect(F(e, "x"), F(e, "y"), F(e, "x") + F(e, "width"), F(e, "y") + F(e, "height")),
                        e.Attribute("rx") is null ? 0 : F(e, "rx"),
                        null));
                    break;
                case "path":
                    shapes.Add(new Shape(
                        SKColor.Parse((string)e.Attribute("fill")!),
                        default,
                        0,
                        SKPath.ParseSvgPathData((string)e.Attribute("d")!)
                            ?? throw new InvalidDataException($"Invalid path data in {path}")));
                    break;
                case "title":
                    break;
                default:
                    throw new InvalidDataException($"Unsupported SVG element <{e.Name.LocalName}> (subset: rect, path).");
            }
        }

        return shapes;
    }

    private static float F(XElement e, string name) =>
        float.Parse((string)e.Attribute(name)!, CultureInfo.InvariantCulture);

    private static byte[] Render(List<Shape> shapes, int size)
    {
        using SKSurface surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(size / 512f);

        foreach (Shape s in shapes)
        {
            using var paint = new SKPaint { Color = s.Fill, IsAntialias = true, Style = SKPaintStyle.Fill };
            if (s.Path is not null)
            {
                canvas.DrawPath(s.Path, paint);
            }
            else
            {
                canvas.DrawRoundRect(s.Rect, s.Radius, s.Radius, paint);
            }
        }

        using SKImage image = surface.Snapshot();
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] PackIco(Dictionary<int, byte[]> pngs)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((ushort)0);
        w.Write((ushort)1);
        w.Write((ushort)_icoSizes.Length);

        int offset = 6 + (16 * _icoSizes.Length);
        foreach (int size in _icoSizes)
        {
            byte dim = (byte)(size >= 256 ? 0 : size);
            w.Write(dim);
            w.Write(dim);
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((ushort)1);
            w.Write((ushort)32);
            w.Write((uint)pngs[size].Length);
            w.Write((uint)offset);
            offset += pngs[size].Length;
        }

        foreach (int size in _icoSizes)
        {
            w.Write(pngs[size]);
        }

        w.Flush();
        return ms.ToArray();
    }

    private static byte[] PackIcns(Dictionary<int, byte[]> pngs)
    {
        int total = 8 + _icnsChunks.Sum(c => 8 + pngs[c.Size].Length);
        byte[] result = new byte[total];
        Encoding.ASCII.GetBytes("icns").CopyTo(result, 0);
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(4), (uint)total);

        int pos = 8;
        foreach ((string type, int size) in _icnsChunks)
        {
            byte[] png = pngs[size];
            Encoding.ASCII.GetBytes(type).CopyTo(result, pos);
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(pos + 4), (uint)(8 + png.Length));
            png.CopyTo(result, pos + 8);
            pos += 8 + png.Length;
        }

        return result;
    }

    private sealed record Shape(SKColor Fill, SKRect Rect, float Radius, SKPath? Path);
}
