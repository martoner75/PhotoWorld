using System.Buffers.Binary;
using System.Text;
using SkiaSharp;

namespace PhotoWorld.Api.Tests;

/// <summary>Builds test images in memory, optionally with an EXIF block (camera settings, orientation, GPS).</summary>
internal static class TestImages
{
    public static byte[] Jpeg(int width, int height, byte[]? exif = null)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.SteelBlue);
            // Red marker in the top-left corner, to check orientation.
            canvas.DrawRect(0, 0, width / 4f, height / 4f, new SKPaint { Color = SKColors.Red });
        }
        using var image = SKImage.FromBitmap(bitmap);
        var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 90).ToArray();
        return exif is null ? jpeg : InsertApp1(jpeg, exif);
    }

    public static byte[] Png(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.Transparent);
        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    /// <summary>A 1×1 GIF: a real image, but not a format PhotoWorld accepts.</summary>
    public static byte[] Gif() => Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");

    public static byte[] CameraExif(ushort orientation = 1, bool withGps = true)
    {
        var ifd0 = new Ifd()
            .Ascii(0x010F, "Fujifilm")
            .Ascii(0x0110, "X-T5")
            .Short(0x0112, orientation);
        var exif = new Ifd()
            .Rational(0x829A, 1, 250)        // ExposureTime
            .Rational(0x829D, 28, 10)        // FNumber
            .Short(0x8827, 400)              // ISO
            .Ascii(0x9003, "2026:05:01 18:42:07") // DateTimeOriginal
            .Rational(0x920A, 35, 1)         // FocalLength
            .Ascii(0xA434, "XF35mmF1.4 R");  // LensModel
        var gps = withGps
            ? new Ifd().Ascii(0x0001, "N").Rationals(0x0002, (51, 1), (30, 1), (2600, 100))
            : null;
        return Tiff(ifd0, exif, gps);
    }

    private static byte[] InsertApp1(byte[] jpeg, byte[] tiff)
    {
        var payload = Encoding.ASCII.GetBytes("Exif\0\0").Concat(tiff).ToArray();
        var segment = new byte[4 + payload.Length];
        segment[0] = 0xFF; segment[1] = 0xE1;
        BinaryPrimitives.WriteUInt16BigEndian(segment.AsSpan(2), (ushort)(payload.Length + 2));
        payload.CopyTo(segment, 4);
        // Right after the SOI marker (FF D8).
        return jpeg[..2].Concat(segment).Concat(jpeg[2..]).ToArray();
    }

    /// <summary>Little-endian TIFF with IFD0 → Exif sub-IFD (+ optional GPS IFD).</summary>
    private static byte[] Tiff(Ifd ifd0, Ifd exif, Ifd? gps)
    {
        // Pointer entries are fixed size, so add them first and fill in offsets once the layout is known.
        ifd0.Long(0x8769, 0);
        if (gps is not null) ifd0.Long(0x8825, 0);

        var ifd0Offset = 8;
        var exifOffset = ifd0Offset + ifd0.Size;
        var gpsOffset = exifOffset + exif.Size;
        ifd0.Long(0x8769, (uint)exifOffset);
        if (gps is not null) ifd0.Long(0x8825, (uint)gpsOffset);

        var output = new List<byte> { (byte)'I', (byte)'I', 0x2A, 0x00, 8, 0, 0, 0 };
        output.AddRange(ifd0.Write(ifd0Offset));
        output.AddRange(exif.Write(exifOffset));
        if (gps is not null) output.AddRange(gps.Write(gpsOffset));
        return output.ToArray();
    }

    private sealed class Ifd
    {
        private readonly SortedDictionary<ushort, (ushort Type, uint Count, byte[] Data)> _entries = [];

        public int Size => 2 + 12 * _entries.Count + 4 + _entries.Values.Sum(e => e.Data.Length > 4 ? Pad(e.Data.Length) : 0);

        public Ifd Ascii(ushort tag, string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value + "\0");
            _entries[tag] = (2, (uint)bytes.Length, bytes);
            return this;
        }

        public Ifd Short(ushort tag, ushort value)
        {
            var bytes = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
            _entries[tag] = (3, 1, bytes);
            return this;
        }

        public Ifd Long(ushort tag, uint value)
        {
            var bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            _entries[tag] = (4, 1, bytes);
            return this;
        }

        public Ifd Rational(ushort tag, uint numerator, uint denominator) => Rationals(tag, (numerator, denominator));

        public Ifd Rationals(ushort tag, params (uint Numerator, uint Denominator)[] values)
        {
            var bytes = new byte[8 * values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 8), values[i].Numerator);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 8 + 4), values[i].Denominator);
            }
            _entries[tag] = (5, (uint)values.Length, bytes);
            return this;
        }

        public byte[] Write(int offset)
        {
            var header = new List<byte>();
            var data = new List<byte>();
            var dataOffset = offset + 2 + 12 * _entries.Count + 4;

            header.AddRange(BitConverter.GetBytes((ushort)_entries.Count));
            foreach (var (tag, (type, count, bytes)) in _entries)
            {
                header.AddRange(BitConverter.GetBytes(tag));
                header.AddRange(BitConverter.GetBytes(type));
                header.AddRange(BitConverter.GetBytes(count));
                if (bytes.Length <= 4)
                {
                    header.AddRange(bytes.Concat(new byte[4 - bytes.Length]));
                }
                else
                {
                    header.AddRange(BitConverter.GetBytes((uint)(dataOffset + data.Count)));
                    data.AddRange(bytes);
                    if (bytes.Length % 2 == 1) data.Add(0);
                }
            }
            header.AddRange(BitConverter.GetBytes(0u)); // no next IFD
            return header.Concat(data).ToArray();
        }

        private static int Pad(int length) => length + length % 2;
    }
}
