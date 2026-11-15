namespace FCT.Analysis;

/// <summary>
/// The --diagnose report of one match: the independent signals of the replay side by side, and where they disagree.
/// </summary>
internal static class MatchDiagnosis
{
    public static void Write(MatchAssembly match, List<string> lines)
    {
        var replay = match.Replay;
        var signals = match.Signals;
        var me = match.Target?.PlayerName ?? "yo";

        if (replay.Info is { } info)
        {
            var started = replay.GameData?.UtcTimeStartedMatch ?? info.Timestamp;
            var local = DateTime.SpecifyKind(started, DateTimeKind.Utc).ToLocalTime();
            lines.Add(L.F("Replay: {0} de duración{1}", "Replay: {0} long{1}", Pretty.Clock(info.LengthInMs / 1000f),
                L.F(" · partida iniciada a las {0:HH:mm:ss}, replay hasta las {1:HH:mm:ss}", " · match started at {0:HH:mm:ss}, replay until {1:HH:mm:ss}",
                    local, local.AddMilliseconds(info.LengthInMs))));
        }

        var chestSearches = match.Interactions.Where(i => ChestKinds.IsChest(i.Kind));
        lines.Add(L.F("Cofres: {0} tuyos ({1} destruidos, {2} reabiertos tras rellenarse) · otros contenedores: {3}",
            "Chests: {0} yours ({1} destroyed, {2} reopened after a refill) · other containers: {3}",
            match.Chests.Count, match.Chests.Count(c => c.Destroyed), chestSearches.GroupBy(i => i.Key).Sum(g => g.Count() - 1), match.Others.Sum(o => o.Value)));

        var unclaimed = signals.Searches.Count(s => !s.Stale && s.Pawn == null && ChestKinds.IsChest(ChestKinds.Classify(s.Label)));
        if (unclaimed > 0)
        {
            lines.Add(L.F("  ⚠ {0} cofre(s) abiertos cerca de ti sin jugador en el replay: podrían ser tuyos y no se cuentan.",
                "  ⚠ {0} chest(s) opened near you with no player in the replay: they could be yours and are not counted.", unclaimed));
        }
        foreach (var mate in match.Teammates)
        {
            lines.Add(L.F("  Compañero {0}: ≥ {1} cofres abiertos", "  Teammate {0}: ≥ {1} chests opened", mate.Name, mate.ChestsSeen));
        }

        var squadPayouts = match.Treasures.Count(t => t.FromPrizeChest);
        lines.Add(L.F("Señales de tesoro: {0} concesión(es) del premio a ti · {1} tesoro(s) destacado(s) del escuadrón · {2} de rivales (no cuentan)",
            "Treasure signals: {0} prize grant(s) to you · {1} featured treasure(s) of the squad · {2} of opponents (not counted)",
            match.HandlerGrants.Count, squadPayouts, signals.Payouts.Count - squadPayouts));
        if (!match.RecorderIsTarget)
        {
            lines.Add(L.T("  ⚠ El replay no lo grabó el jugador contado: las concesiones del premio no se ven, solo los tesoros destacados.",
                "  ⚠ The replay was not recorded by the counted player: prize grants are not visible, only featured treasures."));
        }

        foreach (var treasure in match.Treasures)
        {
            var evidence = !treasure.FromPrizeChest ? L.T("solo concesión", "grant only")
                : match.ConfirmedPayouts.Contains(treasure) ? L.T("tesoro destacado + concesión", "featured treasure + grant")
                : L.T("solo tesoro destacado", "featured treasure only");
            var finder = treasure.Finder switch
            {
                TreasureFind.FinderSelf => me,
                TreasureFind.FinderTeammate => treasure.FinderName,
                _ => L.T("Desconocido", "Unknown"),
            };
            var source = treasure.SourceCertain ? treasure.SourceLabel : $"{treasure.SourceLabel} ({Pretty.Likely})";
            lines.Add($"  ★ {Pretty.Clock(treasure.ObtainedAtSeconds ?? 0)}  {treasure.DisplayName}  ·  {L.T("por", "by")} {finder}  ·  {source}  ·  {evidence}");
            // everything searched in the seconds around it (the source is the closest squad search, of any kind)
            if (treasure.ObtainedAtSeconds is { } at)
            {
                foreach (var near in signals.Searches.Where(x => Math.Abs(x.Time - at) <= 8).OrderBy(x => x.Time))
                {
                    var who = near.Pawn is { } p ? Who(p) : L.T("sin jugador", "no player");
                    lines.Add($"      {near.Time - at,6:+0.0;-0.0} s  {L.Name(ChestKinds.Classify(near.Label))}{(near.Destroyed ? " (" + L.T("destruido", "destroyed") + ")" : "")} · {who}");
                }
            }
        }

        // Every golden chest of a featured treasure the replay saw, squad's or not: which prize id it carried, whether
        // the catalogue knows it, and who opened it (opponents' ones show that featured treasures are read at all).
        var matchLocal = DateTime.SpecifyKind(replay.GameData?.UtcTimeStartedMatch ?? replay.Info?.Timestamp ?? DateTime.UtcNow, DateTimeKind.Utc).ToLocalTime();
        foreach (var payout in signals.Payouts.OrderBy(p => p.Time))
        {
            var prize = Prizes.ById(payout.PrizeId, Prizes.WeekOf(matchLocal));
            var opener = payout.Pawn is { } pawn && pawn != 0
                ? match.TargetPawns.Contains(pawn) ? me : match.MatePawns.TryGetValue(pawn, out var mate) ? mate : L.T("rival", "opponent")
                : L.T("desconocido", "unknown");
            lines.Add($"  ◆ {Pretty.Clock(payout.Time)}  {L.T("cofre de premio destacado", "featured treasure chest")}: {payout.PrizeId} → "
                      + $"{(prize != null ? $"{prize.Name} ({Prizes.TypeName(prize.Type)})" : L.T("no está en el catálogo", "not in the catalogue"))} · {opener} · {payout.Container}");
        }

        // A featured treasure without its grant, while grants are visible: the two signals disagree.
        var unconfirmed = match.Treasures.Count(t => t.FromPrizeChest && !match.ConfirmedPayouts.Contains(t));
        if (match.RecorderIsTarget && unconfirmed > 0 && signals.Grants.Count > 0)
        {
            lines.Add(L.F("  ⚠ {0} tesoro(s) destacado(s) sin su concesión: las dos señales no coinciden.", "  ⚠ {0} featured treasure(s) without a grant: the two signals disagree.", unconfirmed));
        }

        // Gizmos seen in use: for information only (kept between matches, they prove nothing about this one).
        string Who(uint pawn) => match.TargetPawns.Contains(pawn) ? me : match.MatePawns.TryGetValue(pawn, out var name) ? name : L.T("rival", "opponent");
        foreach (var use in signals.Equips.Where(e => e.Item.StartsWith("WID_", StringComparison.Ordinal) && !MatchAssembly.EventLoot.IsMatch(e.Item)
                     && (match.TargetPawns.Contains(e.Pawn) || match.MatePawns.ContainsKey(e.Pawn))))
        {
            lines.Add($"  · {Pretty.Clock(use.Time)}  {L.T("artilugio usado (no cuenta)", "gizmo used (not counted)")}: {TreasureNames.GizmoName(use.Item)} · {Who(use.Pawn)}");
        }
    }
}
