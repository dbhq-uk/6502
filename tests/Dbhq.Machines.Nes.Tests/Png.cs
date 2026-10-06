using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// Writes a frame as a PNG file, for a person to look at: 8-bit RGBA, no filtering, one zlib
/// stream (the PNG specification, sections 11.2 and 12). Only the boot recorder uses it.
/// </summary>
public static class Png
{
    /// <summary>Writes <paramref name="pixels"/>, <c>0xAABBGGRR</c> each, row by row, to <paramref name="path"/>.</summary>
    public static void Write(string path, uint[] pixels, int width, int height)
    {
        byte[] rgba = BootCheck.FrameBytes(pixels);
        using var raw = new MemoryStream();
        for (int y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            raw.Write(rgba, y * width * 4, width * 4);
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            raw.Position = 0;
            raw.CopyTo(zlib);
        }

        byte[] header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = 6;

        using FileStream file = File.Create(path);
        file.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        Chunk(file, "IHDR", header);
        Chunk(file, "IDAT", compressed.ToArray());
        Chunk(file, "IEND", []);
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
    private static uint Crc32(byte[] bytes)
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
