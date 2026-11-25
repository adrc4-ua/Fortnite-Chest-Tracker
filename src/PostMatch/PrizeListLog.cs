using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FCT.Analysis;

namespace FCT.PostMatch;

/// <summary>
/// Reads, from the game's logs (read only), the list of featured prizes the player owns: when a match loads, the game
/// lists one quest per owned prize ("Quest:quest_dpid_item21"; item 6 is the Pirate Shop Access). Each list goes into
/// <see cref="TrackerData.PrizeLists"/>; a prize that appears in it was won since the previous list. Since update 43.11
/// the game no longer writes that list; what it does write, in the same second the server gives the player a new
/// cosmetic, is a change of the player's account pushed during the match ("Delta profile update … profileId=athena
/// from notifications"): those moments go into <see cref="TrackerData.AccountGrants"/> (see <see cref="AccountGrantMerge"/>).
/// Only what is new in each log file is read: the first time every log still on disk, then a few kilobytes at a time.
/// </summary>
public static class PrizeListLog
{
    // [2026.11.15-18.37.12:345][  7]LogFortQuest: Warning: Failed to find a category to organize the quest {Quest:quest_dpid_item21} under!
    private static readonly Regex QuestLine = new(@"^\[(\d{4}\.\d{2}\.\d{2}-\d{2}\.\d{2}\.\d{2}):\d{3}\].*?quest_dpid_item(\d+)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // [2026.11.25-00.55.05:721][177]LogProfileSys: MCP-Profile: Delta profile update (revision 9437 [14303]) for Name accountId=MCP:… profileId=athena from notifications
    private static readonly Regex AccountLine = new(@"^\[(\d{4}\.\d{2}\.\d{2}-\d{2}\.\d{2}\.\d{2}:\d{3})\].*?Delta profile update\b.*?\bprofileId=athena from notifications",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Account changes older than this are of no use any more (their matches were matched long ago).</summary>
    private static readonly TimeSpan KeepGrants = TimeSpan.FromDays(60);

    /// <summary>Quest lines this close together are one list (the game writes it in a burst).</summary>
    private static readonly TimeSpan Burst = TimeSpan.FromSeconds(30);

    private static readonly object Gate = new();
    private static readonly Dictionary<string, long> Read = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The prize id behind a quest number.</summary>
    public static string PrizeIdOf(int item) => item == 6 ? Prizes.PirateShopId : $"DelilahPrize:dpid_item{item}";

    /// <summary>Reads what is new in the logs and files the lists and account changes into the data. True when anything changed.</summary>
    /// <param name="folder">The logs' folder (the game's by default; FCT_GAME_LOG points it elsewhere in tests).</param>
    public static bool Update(TrackerData data, string? folder = null)
    {
        folder ??= Path.GetDirectoryName(GameLogWatcher.DefaultPath);
        if (folder == null || !Directory.Exists(folder))
        {
            return false;
        }
        lock (Gate)
        {
            var listsSince = data.PrizeLists.Count > 0 ? data.PrizeLists[^1].LastSeenUtc : DateTime.MinValue;
            var grantsSince = data.AccountGrants.Count > 0 ? data.AccountGrants.Max() : DateTime.MinValue;
            var since = listsSince < grantsSince ? listsSince : grantsSince;
            var lines = new List<(DateTime Utc, string Id)>();
            var grants = new List<DateTime>();
            var files = new DirectoryInfo(folder).GetFiles("FortniteGame*.log").OrderBy(f => f.LastWriteTimeUtc).ToList();
            foreach (var file in files)
            {
                var from = Read.GetValueOrDefault(file.FullName);
                if (file.Length < from)
                {
                    from = 0; // the game started this file again
                }
                if (file.LastWriteTimeUtc < since)
                {
                    Read[file.FullName] = file.Length; // all older than what is filed
                    continue;
                }
                Read[file.FullName] = ReadQuests(file.FullName, from, listsSince, grantsSince, lines, grants);
            }
            var changed = FileGrants(data, grants);
            foreach (var burst in Bursts(lines))
            {
                changed |= FileList(data, burst.First, burst.Last, burst.Ids);
            }
            return changed;
        }
    }

    /// <summary>
    /// The quest lines after <paramref name="since"/> and the account changes after <paramref name="grantsSince"/>, from a
    /// byte position, in blocks; only whole lines are taken (one still being written is read again next time). Returns
    /// the position after the last whole line.
    /// </summary>
    private static long ReadQuests(string path, long from, DateTime since, DateTime grantsSince, List<(DateTime, string)> found, List<DateTime> grants)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            stream.Seek(from, SeekOrigin.Begin);
            var position = from;
            var carry = Array.Empty<byte>();
            var block = new byte[8 << 20];
            int read;
            while ((read = stream.Read(block, 0, block.Length)) > 0)
            {
                var bytes = carry.Length == 0 ? block.AsSpan(0, read) : carry.Concat(block.Take(read)).ToArray().AsSpan();
                var end = bytes.LastIndexOf((byte)'\n');
                if (end < 0)
                {
                    carry = bytes.ToArray();
                    continue;
                }
                foreach (var line in Encoding.UTF8.GetString(bytes[..end]).Split('\n'))
                {
                    if (line.Contains("quest_dpid_item", StringComparison.OrdinalIgnoreCase) && QuestLine.Match(line) is { Success: true } m
                        && DateTime.TryParseExact(m.Groups[1].Value, "yyyy.MM.dd-HH.mm.ss", CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc) && utc > since)
                    {
                        found.Add((utc, PrizeIdOf(int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture))));
                    }
                    else if (ParseAccountLine(line) is { } grant && grant > grantsSince)
                    {
                        grants.Add(grant);
                    }
                }
                position += end + 1;
                carry = bytes[(end + 1)..].ToArray();
            }
            return position;
        }
        catch (IOException)
        {
            return from; // the game is rotating its logs: next time
        }
    }

    /// <summary>The moment (UTC, to the millisecond) of an account change pushed during play, or null for any other line.</summary>
    internal static DateTime? ParseAccountLine(string line) =>
        line.Contains("Delta profile update", StringComparison.Ordinal) && AccountLine.Match(line) is { Success: true } m
        && DateTime.TryParseExact(m.Groups[1].Value, "yyyy.MM.dd-HH.mm.ss:fff", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc)
            ? utc
            : null;

    /// <summary>Files new account changes (each moment once) and forgets the old ones. True when one was added.</summary>
    internal static bool FileGrants(TrackerData data, IEnumerable<DateTime> grants)
    {
        var added = false;
        foreach (var utc in grants)
        {
            if (!data.AccountGrants.Contains(utc))
            {
                data.AccountGrants.Add(utc);
                added = true;
            }
        }
        if (added)
        {
            data.AccountGrants.Sort();
            var oldest = data.AccountGrants[^1] - KeepGrants;
            data.AccountGrants.RemoveAll(g => g < oldest);
        }
        return added;
    }

    /// <summary>Quest lines grouped into the lists the game wrote (lines less than <see cref="Burst"/> apart).</summary>
    private static IEnumerable<(DateTime First, DateTime Last, HashSet<string> Ids)> Bursts(List<(DateTime Utc, string Id)> lines)
    {
        (DateTime First, DateTime Last, HashSet<string> Ids)? open = null;
        foreach (var (utc, id) in lines.OrderBy(l => l.Utc))
        {
            if (open is { } current && utc - current.Last <= Burst)
            {
                current.Ids.Add(id);
                open = (current.First, utc, current.Ids);
                continue;
            }
            if (open is { } done)
            {
                yield return done;
            }
            open = (utc, utc, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { id });
        }
        if (open is { } last)
        {
            yield return last;
        }
    }

    /// <summary>
    /// Files one list: the continuation of the last one if it is part of the same burst, the same list seen again
    /// (only its time moves on), or a new entry when it changed.
    /// </summary>
    internal static bool FileList(TrackerData data, DateTime first, DateTime last, IReadOnlyCollection<string> ids)
    {
        var lists = data.PrizeLists;
        var previous = lists.Count > 0 ? lists[^1] : null;
        if (previous != null && first - previous.LastSeenUtc <= Burst)
        {
            var before = previous.Ids.Count;
            previous.Ids = previous.Ids.Union(ids, StringComparer.OrdinalIgnoreCase).OrderBy(Order).ToList();
            previous.LastSeenUtc = last > previous.LastSeenUtc ? last : previous.LastSeenUtc;
            return previous.Ids.Count != before;
        }
        if (previous != null && previous.Ids.Count == ids.Count && ids.All(id => previous.Ids.Contains(id, StringComparer.OrdinalIgnoreCase)))
        {
            previous.LastSeenUtc = last;
            return false;
        }
        lists.Add(new PrizeList { FirstSeenUtc = first, LastSeenUtc = last, Ids = ids.OrderBy(Order).ToList() });
        return true;
    }

    /// <summary>Lists in item order (item1, item2… the GUID last), for a readable data.json.</summary>
    private static string Order(string id)
    {
        var digits = Regex.Match(id, @"(\d+)$");
        return digits.Success && id.StartsWith("DelilahPrize:", StringComparison.OrdinalIgnoreCase) ? digits.Groups[1].Value.PadLeft(4, '0') : "zzzz" + id;
    }
}
