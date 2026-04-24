using System;
using System.IO;

public static class Raw16HeightmapReader
{
    public static float[,] Read(
        string path,
        int resolution,
        bool littleEndian,
        bool flipHorizontally,
        bool flipVertically)
    {
        var expectedSize = resolution * resolution * sizeof(ushort);
        var fileInfo = new FileInfo(path);

        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("RAW file not found.", path);
        }

        if (fileInfo.Length != expectedSize)
        {
            throw new InvalidDataException(
                $"Unexpected RAW size for '{path}'. Expected {expectedSize} bytes for {resolution}x{resolution}x16-bit, got {fileInfo.Length} bytes.");
        }

        var heights = new float[resolution, resolution];

        using (var stream = File.OpenRead(path))
        using (var reader = new BinaryReader(stream))
        {
            for (var sourceY = 0; sourceY < resolution; sourceY++)
            {
                var targetY = flipVertically ? (resolution - 1 - sourceY) : sourceY;

                for (var sourceX = 0; sourceX < resolution; sourceX++)
                {
                    var targetX = flipHorizontally ? (resolution - 1 - sourceX) : sourceX;
                    var raw = ReadUInt16(reader, littleEndian);
                    heights[targetY, targetX] = raw / 65535f;
                }
            }
        }

        return heights;
    }

    private static ushort ReadUInt16(BinaryReader reader, bool littleEndian)
    {
        var bytes = reader.ReadBytes(sizeof(ushort));
        if (bytes.Length < sizeof(ushort))
        {
            throw new EndOfStreamException("Unexpected end of RAW file while reading 16-bit height data.");
        }

        if (BitConverter.IsLittleEndian != littleEndian)
        {
            Array.Reverse(bytes);
        }

        return BitConverter.ToUInt16(bytes, 0);
    }
}
