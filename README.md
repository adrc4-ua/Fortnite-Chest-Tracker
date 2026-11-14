<div align="center">

<img src="docs/images/banner-en.png" alt="Fortnite Chest Tracker" width="100%">

[![English](https://img.shields.io/badge/English-e8b04b?style=for-the-badge)](README.md) [![Español](https://img.shields.io/badge/Espa%C3%B1ol-121215?style=for-the-badge)](README.es.md)

[![version](https://img.shields.io/github/v/release/adrc4-ua/Fortnite-Chest-Tracker?style=for-the-badge&label=version&color=e8b04b&labelColor=121215)](../../releases/latest) ![Windows 10 and 11](https://img.shields.io/badge/Windows-10%20%7C%2011-e8b04b?style=for-the-badge&labelColor=121215)

<a href="../../releases/latest"><img src="docs/images/download-en.png" alt="Download FCT" width="260"></a>

<sub>One file · nothing to install · free · English and Spanish</sub>

<img src="docs/images/main-en.png" alt="FCT main window" width="680">

</div>

## What it does

FCT counts **every chest you open in Fortnite**, match after match. You play; it keeps count and saves it on your PC.

<table>
<tr>
<td align="center" width="33%"><img src="docs/images/icon-chests.png" width="44" alt=""><br><b>Your chests</b><br>Total, today and average per match.</td>
<td align="center" width="33%"><img src="docs/images/icon-records.png" width="44" alt=""><br><b>Your records</b><br>Best match, best day and wins.</td>
<td align="center" width="33%"><img src="docs/images/icon-overlay.png" width="44" alt=""><br><b>Overlay</b><br>On top of the game and ready for OBS.</td>
</tr>
<tr>
<td align="center"><img src="docs/images/icon-stats.png" width="44" alt=""><br><b>Stats</b><br>Charts and every match you played.</td>
<td align="center"><img src="docs/images/icon-treasures.png" width="44" alt=""><br><b>Treasures</b><br>The ones you get and this week's.</td>
<td align="center"><img src="docs/images/icon-teammates.png" width="44" alt=""><br><b>Teammates</b><br>Who you play with and what each one opens.</td>
</tr>
</table>

## Getting started

1. **Download [FCT](../../releases/latest)** and open it. Nothing gets installed.
2. **In Fortnite, turn on replays:** *Settings → Game → Replays → Record Replays*. FCT counts from them.
3. **Play.** FCT opens by itself with Fortnite and adds up your chests at the end of every match.

> [!NOTE]
> The first time, Windows may show a blue warning, *"Windows protected your PC"*. It is common for new programs with few downloads: click **More info → Run anyway**.

## The overlay

<p align="center">
<img src="docs/images/overlay-live-en.png" alt="Overlay during a match" width="340">&nbsp;&nbsp;<img src="docs/images/overlay-between-en.png" alt="Overlay between matches" width="340">
<br><sub>During a match, live · Between matches</sub>
</p>

- It only shows while Fortnite is open, on the screen and spot you choose.
- To move it, open its **padlock** and drag it. It stays out of your way in a match: the mouse and cursor remain the game's.
- Turn it on and off with FCT's **Overlay** button.
- Fortnite has to be in **Windowed Fullscreen** for it to show on top.
- **Streaming?** *Settings* has the address to add it to OBS as a browser source.

## Stats

The **Stats** button opens them in your browser, and they update by themselves while you play.

<img src="docs/images/stats-summary-en.png" alt="Stats overview" width="100%">

**💎 Treasures** · the ones you got, where they came from and this week's prizes.

<img src="docs/images/stats-treasures-en.png" alt="Treasures" width="100%">

**Teammates** · who you play with, your wins together and who opens the most chests.

<img src="docs/images/stats-teammates-en.png" alt="Teammates" width="100%">

## Settings

<img src="docs/images/settings-en.png" alt="Settings" width="460" align="right">

Changes are saved right away.

- **Open with Fortnite:** FCT opens and closes with the game.
- **Teammates:** their chests in the window and, if you like, added to your total.
- **Also count** ranked matches, ammo boxes, food boxes or loot cannons.
- **Everything is shared with the stats:** what you change in the app changes on the web page, and the other way round.
- **Chests before FCT:** works out the ones you opened before using it.
- **Automatic updates**, or ask first.
- **Language**, **overlay for OBS** and **uninstall**.

<br clear="right">

## How accurate is it?

**Chests: all of them, no doubt about it.** Every chest you open (or break) is in the replay, and FCT counts each one.

**Treasures: almost.** The featured ones (outfits, cars, the Pirate Shop Access…) should always be counted. Treasure gizmos are trickier: the way Fortnite records its replays does not always say which gizmo you got, or that you got one at all, so now and then one may slip through or show up as a nameless "Treasure gizmo". It is a small margin of error, and we keep narrowing it down with each update.

## Is it safe?

Yes. FCT reads the **replays** Fortnite saves on your PC, just like opening them yourself:

- It does not touch the game or its memory, and does not interact with the anti-cheat.
- At the end of a match it looks at the treasure screen to see which ones you got. That image is dropped at once and never saved.
- No accounts, no login and no ads.
- Nothing leaves your PC. It only connects to look for updates and, if you ask, to work out your chests from before FCT.
- Very light: it barely does anything between matches.

> [!TIP]
> **Rather check it yourself?** All the code is in this repository. Give it to the AI you use (ChatGPT, Claude, Gemini…) and ask whether it does anything odd. FCT was made for one thing only: knowing how many chests you open.

## FAQ

<details>
<summary><b>My matches are not counted</b></summary>

Check that replays are turned on in Fortnite (*Settings → Game → Replays*). Only Battle Royale matches count.
</details>

<details>
<summary><b>Why are my teammates' chests low?</b></summary>

Fortnite only records what happens near you. When a teammate goes far away, their chests do not show up; that is why they are shown as a minimum (≥).
</details>

<details>
<summary><b>Do I lose my history when Fortnite deletes the replays?</b></summary>

No. Each match is saved as soon as it is read, in `%APPDATA%\FCT`. If you change PC, copy that folder.
</details>

<details>
<summary><b>What about matches I play with FCT closed?</b></summary>

They are counted the next time you open it, as long as Fortnite has not deleted those replays. With **Open with Fortnite** on you do not have to remember.
</details>

<details>
<summary><b>Does it update by itself?</b></summary>

Yes, between matches and without losing your data. Change it in *Settings → Updates*.
</details>

<details>
<summary><b>How do I uninstall it?</b></summary>

*Settings → Uninstall…* FCT removes itself from the PC and leaves nothing behind. Your data stays in `%APPDATA%\FCT` in case you come back; if you do not want it, delete that folder.
</details>

<details>
<summary><b>Something does not work or you have a suggestion</b></summary>

Submit a [problem or suggestion](../../issues/new?template=01_report_en.yml) saying what happens and, if you can, attach the `errors.log` file from `%APPDATA%\FCT`.
</details>

## License and credits

**Fortnite Chest Tracker** · Copyright © 2026 Adrián Ruiz. Free software under the [GPL-3.0](LICENSE) license with the attribution terms in [NOTICE.md](NOTICE.md): you can use, change and share it, but if you publish something based on it you must keep this license, credit the author and this repository, and mark your changes. Versions up to 4.4.1 were released under the MIT license.

**Thanks to**

- [ChestCounter](https://github.com/xdoofy92/ChestCounter), by xdoofy92: the project FCT started from and where the idea came from.
- [FortniteReplayDecompressor](https://github.com/Shiqan/FortniteReplayDecompressor), by Shiqan (MIT): the library that reads the replays.
- [Fortnite-API](https://fortnite-api.com): where your season's matches come from when you work out your chests from before FCT. [fortnite.gg](https://fortnite.gg/stats) is suggested to look them up by hand if needed.
- Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by [SignPath Foundation](https://signpath.org/) ([code signing policy](CODE_SIGNING_POLICY.md)).

Third-party code and its licenses: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) · [Privacy policy](CODE_SIGNING_POLICY.md#privacy-policy)

<sub>FCT is an independent fan project, not affiliated with or endorsed by Epic Games, Inc. Fortnite and Epic Games are trademarks or registered trademarks of Epic Games, Inc. Fortnite-API and fortnite.gg are unofficial services, not related to this project.</sub>
