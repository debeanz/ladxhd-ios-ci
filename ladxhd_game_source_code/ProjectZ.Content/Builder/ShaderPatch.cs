using System.Buffers.Binary;
using System.Text;

public static class ShaderPatch
{
    // Strings to search for within or write to the shader bytes.
    private static readonly byte[] Marker  = Encoding.ASCII.GetBytes("precision mediump float;");
    private static readonly byte[] Patched = Encoding.ASCII.GetBytes("GL_FRAGMENT_PRECISION_HIGH");
    private static readonly byte[] EndIf   = Encoding.ASCII.GetBytes("#endif");
    private static readonly byte[] Insert  = Encoding.ASCII.GetBytes("#ifdef GL_FRAGMENT_PRECISION_HIGH\r\nprecision highp float;\r\n#endif\r\n");

    public static void PatchDirectory(string shaderPath)
    {
        if (!Directory.Exists(shaderPath))
            return;

        foreach (var file in Directory.EnumerateFiles(shaderPath, "*.xnb", SearchOption.AllDirectories))
        {
            // Get the PixelGrid shader.
            var shader = Path.GetFileNameWithoutExtension(file);
            var exists = shader.Equals("PixelGrid", StringComparison.OrdinalIgnoreCase);

            // If this isn't it keep searching.
            if (!exists)
                continue;

            // Try to patch the shader and report if it succeeds or fails.
            if (PatchFile(file))
                Console.WriteLine("Patching \"PixelGrid.xnb\" with \"highp\" floating point precision succeeded!");
            else
                Console.WriteLine("Shader \"PixelGrid.xnb\" failed patching with \"highp\" floating point precision!");
        }
    }

    private static bool PatchFile(string path)
    {
        // Get the file as a byte array.
        var bytes = File.ReadAllBytes(path);

        // Check the first 3 header bytes to see if it's an "XNB" file and check
        // if the file has already been patched or it's not a GLSL shader.
        var notXNBFile = bytes[0] != (byte)'X' || bytes[1] != (byte)'N' || bytes[2] != (byte)'B';
        var isPatched = IndexOf(bytes, Patched, 0) >= 0 || IndexOf(bytes, Marker, 0) < 0;

        // If either returns true then don't try to patch.
        if (notXNBFile || isPatched)
            return false;

        // Track whether anything was actually inserted, and where to resume searching.
        var changed = false;
        var searchFrom = 0;

        // An XNB can hold more than one shader blob, so keep going until none are left.
        while (true)
        {
            // Find the next "precision mediump float;" line.
            var marker = IndexOf(bytes, Marker, searchFrom);
            if (marker < 0)
                break;

            // Find the shader blob this line belongs to. Its length prefix has to be
            // corrected after the insert, so exit early if it can't be located.
            if (!TryFindBlob(bytes, marker, out var blobStart, out var blobLength))
                throw new InvalidDataException($"Could not locate the shader blob header in '{path}'.");

            // Insert after the "#endif" that closes the existing GL_ES precision block.
            var endIf = IndexOf(bytes, EndIf, marker);
            if (endIf < 0 || endIf > blobStart + blobLength)
                throw new InvalidDataException($"Malformed precision block in '{path}'.");

            // Step past the "#endif" and any line breaks after it to find the insert point.
            var insertAt = endIf + EndIf.Length;
            while (insertAt < bytes.Length && (bytes[insertAt] == (byte)'\r' || bytes[insertAt] == (byte)'\n'))
                insertAt++;

            // Build a bigger array: everything before the insert point, the new block, then the rest.
            var result = new byte[bytes.Length + Insert.Length];
            Buffer.BlockCopy(bytes, 0, result, 0, insertAt);
            Buffer.BlockCopy(Insert, 0, result, insertAt, Insert.Length);
            Buffer.BlockCopy(bytes, insertAt, result, insertAt + Insert.Length, bytes.Length - insertAt);

            // Grow the blob's length prefix by exactly what we inserted.
            BinaryPrimitives.WriteInt32LittleEndian(
                result.AsSpan(blobStart - 4), blobLength + Insert.Length);

            bytes = result;
            changed = true;
            searchFrom = blobStart + blobLength + Insert.Length;
        }
        // Only rewrite the file if something was actually inserted.
        if (changed)
            File.WriteAllBytes(path, bytes);

        // Report back whether the file was patched.
        return changed;
    }

    private static bool TryFindBlob(byte[] data, int near, out int start, out int length)
    {
        // Each blob starts with a 4-byte length followed by the GLSL source, so scan
        // backwards from the marker looking for that header. 400 bytes is far enough.
        var limit = Math.Max(4, near - 400);
        for (var i = near; i >= limit; i--)
        {
            // GLSL source begins with a '#' (the "#ifdef GL_ES" line), so skip anything else.
            if (data[i] != (byte)'#')
                continue;

            // Read the 4 bytes just before it. A sane length means this is the real header.
            var candidate = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(i - 4));
            if (candidate > 64 && candidate <= data.Length - i)
            {
                start = i;
                length = candidate;
                return true;
            }
        }
        // Nothing found, so report failure.
        start = 0;
        length = 0;
        return false;
    }

    private static int IndexOf(byte[] sourceArray, byte[] searchFor, int from)
    {
        // Fail early on edge cases.
        if (sourceArray == null || searchFor == null) return -1;
        if (from < 0 || from > sourceArray.Length) return -1;
        if (searchFor.Length == 0) return from;

        // Convert arrays to "Spans".
        ReadOnlySpan<byte> sourceSpan = sourceArray;
        ReadOnlySpan<byte> searchSpan = searchFor;

        // Slice the source span starting from the specified index.
        ReadOnlySpan<byte> slice = sourceSpan.Slice(from);

        // Perform the high-performance hardware-accelerated search.
        int sliceIndex = slice.IndexOf(searchSpan);

        // If found, adjust the index back to the coordinate space of the original array.
        return sliceIndex == -1 ? -1 : from + sliceIndex;
    }
}
