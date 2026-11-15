import os
import re
import subprocess
import sys

def main():
    tag = os.environ.get("TAG", "").strip()
    repo = os.environ.get("GITHUB_REPOSITORY", "").strip()

    if not tag or not repo:
        print("Missing TAG or GITHUB_REPOSITORY environment variable. Skipping release formatting.")
        return 0

    ver = tag.lstrip("v")
    title = f"Fortnite Chest Tracker v{ver}"

    # Compute anchor in CHANGELOG.md (e.g. 593---2026-11-15 or 594---2026-11-15)
    anchor = ver.replace(".", "")
    if os.path.exists("CHANGELOG.md"):
        try:
            with open("CHANGELOG.md", "r", encoding="utf-8") as f:
                cl = f.read()
            m = re.search(r'##\s+\[v?' + re.escape(ver) + r'\]\s*-\s*(\d{4}-\d{2}-\d{2})', cl)
            if m:
                date = m.group(1)
                anchor = f"{ver.replace('.', '')}---{date}"
        except Exception as e:
            print(f"Warning: Could not read CHANGELOG.md: {e}")

    changelog_url = f"https://github.com/{repo}/blob/main/CHANGELOG.md#{anchor}"

    new_notes = f"""### ⬇️ Download / Descarga

**English:**
To use Fortnite Chest Tracker, just download **`FCT.exe`** from the **Assets** section below and open it. That's it!
* **No installation needed:** A single portable file — nothing gets installed on your PC.
* **Requirements:** Windows 10 or 11. In Fortnite, turn on *Record Replays* (*Settings → Game → Replays*).
* If Windows SmartScreen shows *"Windows protected your PC"*, click **More info → Run anyway** (common for new software).

**Español:**
Para usar Fortnite Chest Tracker, simplemente descarga **`FCT.exe`** en la sección **Assets** de abajo y ábrelo con doble clic. ¡Y listo!
* **Sin instalación:** Es un único archivo portátil, no instala nada en tu PC.
* **Requisitos:** Windows 10 u 11. En Fortnite, activa *Grabar repeticiones* (*Ajustes → Juego → Repeticiones*).
* Si Windows muestra *"Windows protegió su PC"*, pulsa **Más información → Ejecutar de todas formas** (habitual en programas nuevos).
---
<sub>**Changelog:** [{tag}]({changelog_url})</sub>
<sub>Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by [SignPath Foundation](https://signpath.org/) · [Code signing policy](https://github.com/{repo}/blob/main/CODE_SIGNING_POLICY.md) · [Third-party notices](https://github.com/{repo}/blob/main/THIRD_PARTY_NOTICES.md) · Not affiliated with Epic Games.</sub>
"""

    notes_file = "release_notes.md"
    with open(notes_file, "w", encoding="utf-8") as f:
        f.write(new_notes)

    print(f"Updating release {tag} with title '{title}'...")
    subprocess.run(["gh", "release", "edit", tag, "--repo", repo, "--title", title, "--notes-file", notes_file], check=True)
    print(f"Release {tag} ('{title}') updated successfully.")
    return 0

if __name__ == "__main__":
    sys.exit(main())
