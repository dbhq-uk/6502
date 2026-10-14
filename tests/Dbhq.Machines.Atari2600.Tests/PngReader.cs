using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// A picture: its width and height, and its pixels row by row as <c>0xAABBGGRR</c>, the order
/// <see cref="FrameBuffer.ToRgba"/> uses.
/// </summary>
internal sealed record Picture(int Width, int Height, uint[] Pixels);

/// <summary>
/// Reads the suite's reference pictures, and writes a picture for a person to look at. The
/// references are all 8-bit RGBA (colour type 6, <c>docs/atari-2600/facts/suite.md</c> section 4),
/// so 8-bit RGBA and RGB are read, not interlaced, with any of the five row filters (the PNG
/// specification, sections 9 and 11.2); anything else is refused with a plain message. The NES's
/// <c>Png.cs</c> only writes.
/// </summary>
internal static class PngReader
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>The picture in <paramref name="png"/>.</summary>
    /// <exception cref="InvalidDataException">The file is not a PNG this reads.</exception>
    public static Picture Read(byte[] png)
    {
        if (png.Length < Signature.Length || !png.AsSpan(0, Signature.Length).SequenceEqual(Signature))
        {
            throw new InvalidDataException("The file is not a PNG: its first eight bytes are not the PNG signature.");
        }

        int width = 0, height = 0, channels = 0;
        using var compressed = new MemoryStream();
        int at = Signature.Length;
        while (true)
        {
            if (at + 12 > png.Length)
            {
                throw new InvalidDataException("The PNG ends before its IEND chunk.");
            }

            int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(at));
            if (length < 0 || at + 12 + length > png.Length)
            {
                throw new InvalidDataException($"A PNG chunk at byte {at} runs past the end of the file.");
            }

            ReadOnlySpan<byte> typeAndData = png.AsSpan(at + 4, 4 + length);
            if (BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(at + 8 + length)) != Crc32(typeAndData))
            {
                throw new InvalidDataException($"A PNG chunk at byte {at} fails its CRC.");
            }

            string type = Encoding.ASCII.GetString(typeAndData[..4]);
            ReadOnlySpan<byte> data = typeAndData[4..];
            at += 12 + length;
            if (type == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(data);
                height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                byte depth = data[8], colourType = data[9], interlace = data[12];
                channels = colourType switch { 6 => 4, 2 => 3, _ => 0 };
                if (depth != 8 || channels == 0 || interlace != 0 || width <= 0 || height <= 0)
                {
                    throw new InvalidDataException($"The PNG is {depth}-bit colour type {colourType}, interlace {interlace}; only 8-bit RGBA or RGB, not interlaced, is read.");
                }
            }
            else if (type == "IDAT")
            {
                compressed.Write(data);
            }
            else if (type == "IEND")
            {
                break;
            }
        }

        if (channels == 0)
        {
            throw new InvalidDataException("The PNG has no IHDR chunk.");
        }

        int stride = width * channels;
        byte[] raw = new byte[(stride + 1) * height];
        compressed.Position = 0;
        using (var zlib = new ZLibStream(compressed, CompressionMode.Decompress))
        {
            int read = 0;
            while (read < raw.Length)
            {
                int got = zlib.Read(raw, read, raw.Length - read);
                if (got == 0)
                {
                    throw new InvalidDataException($"The PNG's image data ends after {read} of {raw.Length} bytes.");
                }

                read += got;
            }
        }

        var pixels = new uint[width * height];
        byte[] previous = new byte[stride];
        byte[] row = new byte[stride];
        for (int y = 0; y < height; y++)
        {
            byte filter = raw[y * (stride + 1)];
            Array.Copy(raw, (y * (stride + 1)) + 1, row, 0, stride);
            Unfilter(filter, row, previous, channels);
            for (int x = 0; x < width; x++)
            {
                int p = x * channels;
                uint alpha = channels == 4 ? row[p + 3] : 0xFFu;
                pixels[(y * width) + x] = (alpha << 24) | ((uint)row[p + 2] << 16) | ((uint)row[p + 1] << 8) | row[p];
            }

            (previous, row) = (row, previous);
        }

        return new Picture(width, height, pixels);
    }

    /// <summary>
    /// Writes <paramref name="picture"/> as an 8-bit RGBA PNG at <paramref name="path"/>, for a
    /// person to look at, every row with <paramref name="filter"/> (0 to 4; the tests use the others).
    /// </summary>
    public static void Write(string path, Picture picture, byte filter = 0)
    {
        int stride = picture.Width * 4;
        byte[] previous = new byte[stride];
        byte[] row = new byte[stride];
        using var raw = new MemoryStream();
        for (int y = 0; y < picture.Height; y++)
        {
            for (int x = 0; x < picture.Width; x++)
            {
                uint p = picture.Pixels[(y * picture.Width) + x];
                row[x * 4] = (byte)p;
                row[(x * 4) + 1] = (byte)(p >> 8);
                row[(x * 4) + 2] = (byte)(p >> 16);
                row[(x * 4) + 3] = (byte)(p >> 24);
            }

            raw.WriteByte(filter);
            for (int i = 0; i < stride; i++)
            {
                int left = i >= 4 ? row[i - 4] : 0;
                int up = previous[i];
                int upLeft = i >= 4 ? previous[i - 4] : 0;
                raw.WriteByte((byte)(row[i] - Predict(filter, left, up, upLeft)));
            }

            (previous, row) = (row, previous);
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            raw.Position = 0;
            raw.CopyTo(zlib);
        }

        byte[] header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, picture.Width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), picture.Height);
        header[8] = 8;
        header[9] = 6;
        using FileStream file = File.Create(path);
        file.Write(Signature);
        Chunk(file, "IHDR", header);
        Chunk(file, "IDAT", compressed.ToArray());
        Chunk(file, "IEND", []);
    }

    // The five filters of the PNG specification, section 9.2, undone in place.
    internal static void Unfilter(byte filter, byte[] row, byte[] previous, int bpp)
    {
        if (filter > 4)
        {
            throw new InvalidDataException($"A PNG row has filter {filter}; only 0 to 4 exist.");
        }

        for (int i = 0; i < row.Length; i++)
        {
            int left = i >= bpp ? row[i - bpp] : 0;
            int upLeft = i >= bpp ? previous[i - bpp] : 0;
            row[i] += Predict(filter, left, previous[i], upLeft);
        }
    }

    // What a filter predicts a byte from its neighbours (section 9.2).
    private static byte Predict(byte filter, int left, int up, int upLeft) => filter switch
    {
        1 => (byte)left,
        2 => (byte)up,
        3 => (byte)((left + up) / 2),
        4 => Paeth(left, up, upLeft),
        _ => 0,
    };

    private static byte Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return (byte)(pa <= pb && pa <= pc ? a : pb <= pc ? b : c);
    }

    private static void Chunk(Stream file, string type, byte[] data)
    {
        byte[] typeAndData = [.. Encoding.ASCII.GetBytes(type), .. data];
        byte[] word = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(word, data.Length);
        file.Write(word);
        file.Write(typeAndData);
        BinaryPrimitives.WriteUInt32BigEndian(word, Crc32(typeAndData));
        file.Write(word);
    }

    // The CRC the PNG specification gives in its annex D, bit by bit.
    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in bytes)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
            }
        }

        return ~crc;
    }
}
