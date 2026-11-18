namespace FCT.Analysis;

/// <summary>
/// Peeks at the start of a .replay file (Unreal's local file header) without parsing the match: whether it is still
/// being recorded and whether its encryption key has been written. Opened with full sharing, since Fortnite keeps
/// writing the file during the match.
/// </summary>
public static class ReplayHeaderProbe
{
    /// <summary>
    /// True when the data cannot be read yet: an encrypted replay whose key is still all zeros (Fortnite writes the
    /// real key when the match ends). Also true for anything that does not look like a replay header.
    /// </summary>
    public static bool KeyMissing(string path)
    {
        var header = Read(path, throughKey: true);
        return header is not { } h || (h.Encrypted && h.KeyIsBlank);
    }

    /// <summary>The header's "live" flag; null when the file is not a readable replay.</summary>
    public static bool? IsLive(string path) => Read(path, throughKey: false)?.Live;

    /// <summary>
    /// Reads the Fortnite build/branch string (e.g. "++Fortnite+Release-43.10") from the replay's header chunk without
    /// decompressing or parsing match events. Null when the file cannot be read or is not a readable replay.
    /// </summary>
    public static string? ReadGameVersion(string path)
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new BinaryReader(file);
            if (reader.ReadUInt32() != Magic)
            {
                return null;
            }
            if (reader.ReadUInt32() >= 7) // custom versions table
            {
                var customVersions = reader.ReadInt32();
                if (customVersions is < 0 or > 64)
                {
                    return null;
                }
                file.Seek(20L * customVersions, SeekOrigin.Current);
            }
            file.Seek(3 * sizeof(uint), SeekOrigin.Current); // length in ms, network version, changelist
            SkipFString(reader);

            file.Seek(sizeof(uint), SeekOrigin.Current); // live flag
            file.Seek(sizeof(long) + sizeof(uint), SeekOrigin.Current); // timestamp, compressed flag
            var encrypted = reader.ReadUInt32() != 0;
            if (encrypted)
            {
                var keyLength = reader.ReadInt32();
                if (keyLength is <= 0 or > 256)
                {
                    return null;
                }
                file.Seek(keyLength, SeekOrigin.Current);
            }

            while (file.Position + 8 <= file.Length)
            {
                var chunkType = reader.ReadUInt32();
                var chunkSize = reader.ReadInt32();
                var chunkOffset = file.Position;
                if (chunkSize <= 0 || (long)chunkSize + chunkOffset > file.Length)
                {
                    return null;
                }
                if (chunkType == 0) // ReplayChunkType.Header
                {
                    var netMagic = reader.ReadUInt32();
                    if (netMagic != 0x2CF5A13D)
                    {
                        return null;
                    }
                    var netVersion = reader.ReadUInt32();
                    if (netVersion >= 19) // HISTORY_USE_CUSTOM_VERSION
                    {
                        var customCount = reader.ReadInt32();
                        if (customCount is < 0 or > 128)
                        {
                            return null;
                        }
                        file.Seek(20L * customCount, SeekOrigin.Current);
                    }
                    file.Seek(3 * sizeof(uint), SeekOrigin.Current); // checksum, engineNetVer, gameNetProtocolVer
                    if (netVersion >= 12) // HISTORY_HEADER_GUID
                    {
                        file.Seek(16, SeekOrigin.Current);
                    }
                    if (netVersion >= 11) // HISTORY_SAVE_FULL_ENGINE_VERSION
                    {
                        file.Seek(3 * sizeof(ushort) + sizeof(uint), SeekOrigin.Current); // major, minor, patch, changelist
                        return ReadFString(reader);
                    }
                    return null;
                }
                file.Seek(chunkOffset + chunkSize, SeekOrigin.Begin);
            }
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private const uint Magic = 0x1CA2E27F;

    private readonly record struct Header(bool Live, bool Encrypted, bool KeyIsBlank);

    /// <summary>Walks the header; stops after the live flag unless the key is wanted. Null on any inconsistency.</summary>
    private static Header? Read(string path, bool throughKey)
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new BinaryReader(file);
            if (reader.ReadUInt32() != Magic)
            {
                return null;
            }
            if (reader.ReadUInt32() >= 7) // file version 7 added a table of 20-byte custom versions
            {
                var customVersions = reader.ReadInt32();
                if (customVersions is < 0 or > 64)
                {
                    return null;
                }
                file.Seek(20L * customVersions, SeekOrigin.Current);
            }
            file.Seek(3 * sizeof(uint), SeekOrigin.Current); // length in ms, network version, changelist
            SkipFString(reader);

            var live = reader.ReadUInt32() != 0;
            if (!throughKey)
            {
                return new Header(live, false, false);
            }
            file.Seek(sizeof(long) + sizeof(uint), SeekOrigin.Current); // timestamp, compressed flag
            if (reader.ReadUInt32() == 0)
            {
                return new Header(live, Encrypted: false, KeyIsBlank: false);
            }
            var keyLength = reader.ReadInt32();
            if (keyLength is <= 0 or > 256)
            {
                return null;
            }
            var key = reader.ReadBytes(keyLength);
            return new Header(live, Encrypted: true, KeyIsBlank: Array.TrueForAll(key, b => b == 0));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Unreal string: a length, negative for UTF-16 (two bytes per character), then the characters.</summary>
    private static void SkipFString(BinaryReader reader)
    {
        var length = reader.ReadInt32();
        reader.BaseStream.Seek(length < 0 ? -2L * length : length, SeekOrigin.Current);
    }

    private static string? ReadFString(BinaryReader reader)
    {
        var length = reader.ReadInt32();
        if (length == 0)
        {
            return string.Empty;
        }
        if (length is < -2048 or > 2048)
        {
            return null;
        }
        var isUnicode = length < 0;
        var byteCount = isUnicode ? -2 * length : length;
        var bytes = reader.ReadBytes(byteCount);
        if (isUnicode)
        {
            if (bytes.Length >= 2 && bytes[^2] == 0 && bytes[^1] == 0)
            {
                bytes = bytes[..^2];
            }
            return System.Text.Encoding.Unicode.GetString(bytes).Trim();
        }
        else
        {
            if (bytes.Length >= 1 && bytes[^1] == 0)
            {
                bytes = bytes[..^1];
            }
            return System.Text.Encoding.UTF8.GetString(bytes).Trim();
        }
    }
}
