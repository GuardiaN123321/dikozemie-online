"""Builds the Nexus Mods package of the arena: a plain ZIP.

Nexus Mods rules it follows (File Submission Guidelines, quarantine article):
no installer .exe and no archive inside the archive, no self-update (the client
DLL is built with -p:SelfUpdate=false), no files of the game. Every file that is
the same as a file of the installed game is left out and listed in
Tools/game_files.txt; Tools/copy_game_files.js takes it from the player's own
game while installing. Dedicated-server files, PDBs and the single-player module
are left out: a player does not need them.

Usage: py -3 Tools/build_nexus_package.py <unpacked public package> <client dll folder> <output zip>
"""
import hashlib
import re
import shutil
import sys
import zipfile
from collections import defaultdict
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
GAME = Path(r"C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord")
MODULE = "DikozemieOnline"
# These two arenas are the game's own scenes: the whole folder comes from the game.
GAME_SCENES = {
    "arena_empire_a": "Modules/SandBox/SceneObj/arena_empire_a",
    "arena_aserai_a": "Modules/SandBox/SceneObj/arena_aserai_a",
}
CULTURES = {"aserai": "aserai", "battania": "battania", "empire": "empire", "khuzait": "khuzait",
            "nords": "nord", "sturgia": "sturgia", "vlandia": "vlandia"}
TOOLS = (
    "install_multiplayer_client.cmd",
    "launch_arena_client.cmd",
    "prepare_singleplayer_catalog.js",
    "prepare_bannerlord_launch.js",
    "audit_arena_prices.js",
    "copy_game_files.js",
)


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def game_index() -> dict:
    index = defaultdict(list)
    for module in (GAME / "Modules").iterdir():
        if not module.is_dir() or module.name.startswith("Dikozemie"):
            continue
        for path in module.rglob("*"):
            if path.is_file():
                index[path.stat().st_size].append(path)
    return index


def main() -> int:
    source, dll, output = (Path(a) for a in sys.argv[1:4])
    stage = output.parent / "stage"
    if stage.exists():
        shutil.rmtree(stage)
    module = stage / "Modules" / MODULE
    shutil.copytree(
        source / "Modules" / MODULE, module,
        ignore=shutil.ignore_patterns("*.pdb", "Win64_Shipping_Server", "Linux64_Shipping_Server", "Server"))
    client = module / "bin" / "Win64_Shipping_Client"
    for name in ("Dikozemie.Online.dll", "Dikozemie.Online.deps.json"):
        shutil.copy2(dll / name, client / name)

    # The culture backdrops were cut from the game's character creation; the
    # package shows our own town paintings instead (same size, same channel swap).
    for culture, town in CULTURES.items():
        picture = Image.open(module / "GUI" / "Textures" / "Towns" / f"{town}-town.jpg")
        width = round(picture.width * 876 / picture.height)
        picture = picture.resize((width, 876), Image.LANCZOS)
        left = (width - 952) // 2
        picture.crop((left, 0, left + 952, 876)).convert("RGBA").save(
            module / "GUI" / "Textures" / "HeroCulture" / f"{culture}.png")

    # The mod restarts itself with the right modules when it was started from
    # the TaleWorlds launcher (DikozemieLaunchGuard); ArenaSetup puts these here.
    launcher = module / "Launcher"
    launcher.mkdir(exist_ok=True)
    shutil.copy2(ROOT / "Tools" / "ArenaSetup" / "launch_arena.js", launcher / "launch_arena.js")
    shutil.copy2(ROOT / "Tools" / "prepare_bannerlord_launch.js", launcher / "prepare_bannerlord_launch.js")

    entries = []
    for scene, where in GAME_SCENES.items():
        target = module / "SceneObj" / scene
        if target.exists():
            shutil.rmtree(target)
        for path in sorted((GAME / where).rglob("*")):
            if path.is_file():
                relative = path.relative_to(GAME / where).as_posix()
                entries.append((f"SceneObj/{scene}/{relative}", f"{where}/{relative}"))

    index = game_index()
    hashes = {}
    for path in sorted(p for p in module.rglob("*") if p.is_file()):
        size = path.stat().st_size
        if size < 64:
            continue
        mine = None
        for other in index.get(size, ()):
            mine = mine or digest(path)
            if other not in hashes:
                hashes[other] = digest(other)
            if hashes[other] == mine:
                entries.append((path.relative_to(module).as_posix(), other.relative_to(GAME).as_posix()))
                path.unlink()
                break

    tools = stage / "Tools"
    tools.mkdir(parents=True)
    for name in TOOLS:
        shutil.copy2(ROOT / "Tools" / name, tools / name)
    lines = ["# taken from the installed game by Tools\\copy_game_files.js"]
    lines += [f"{a}|{b}".replace("/", "\\") for a, b in entries]
    (tools / "game_files.txt").write_bytes(("\r\n".join(lines) + "\r\n").encode("ascii"))
    shutil.copy2(ROOT / "ARENA_VERSION", stage / "ARENA_VERSION")
    readme = ROOT / "Docs" / "NEXUS_README_EN.txt"
    if readme.exists():
        shutil.copy2(readme, stage / "README.txt")

    files = sorted(p for p in stage.rglob("*") if p.is_file())
    forbidden = [p for p in files if p.suffix.lower() in (".exe", ".zip", ".7z", ".rar", ".pdb", ".msi", ".bat")]
    if forbidden:
        print("forbidden files:", *forbidden, sep="\n  ")
        return 1
    # ReadMe and code name nothing of this PC.
    for path in files:
        if path.suffix.lower() in (".txt", ".js", ".cmd", ".xml", ".json", ".md"):
            text = path.read_text(encoding="utf-8", errors="replace")
            if re.search(r"C:\\Users\\|C:\\work|PowerPC", text):
                print("personal path in", path.relative_to(stage))
                return 1
    output.unlink(missing_ok=True)
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for path in files:
            archive.write(path, path.relative_to(stage).as_posix())
    print(f"{len(files)} files, {len(entries)} from the game, {output.stat().st_size / 1e6:.1f} MB, sha256 {digest(output)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
