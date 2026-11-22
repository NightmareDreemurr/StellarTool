"""Build the Windows launcher bundle with an authorized jacket archive."""

import argparse
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
from zipfile import ZIP_DEFLATED, ZIP_STORED, ZipFile


ROOT = Path(__file__).resolve().parents[1]
RUNTIME_VERSION = "8.0.34"
BUNDLE = "StellarTool.Web-win-x64"
JACKET_PATH = re.compile(r"assets/jackets/UI_Jacket_[0-9]{6}\.png")
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"


def build_jackets(source, destination):
    with ZipFile(source) as originals, ZipFile(destination, "w") as jackets:
        entries = [
            entry for entry in originals.infolist()
            if JACKET_PATH.fullmatch(entry.filename)
        ]
        names = {entry.filename for entry in entries}
        if not entries or len(names) != len(entries):
            raise ValueError("The input ZIP needs unique assets/jackets/UI_Jacket_NNNNNN.png entries")
        for entry in sorted(entries, key=lambda item: item.filename):
            with originals.open(entry) as image:
                signature = image.read(len(PNG_SIGNATURE))
                if signature != PNG_SIGNATURE or entry.file_size > 10 * 1024 * 1024:
                    raise ValueError(f"Unsupported jacket image {entry.filename}")
                with jackets.open(entry.filename, "w") as output:
                    output.write(signature)
                    shutil.copyfileobj(image, output)
    return len(entries)


def copy_runtime_notices(destination):
    packages = Path(os.environ.get("NUGET_PACKAGES", Path.home() / ".nuget/packages"))
    destination.mkdir(exist_ok=True)
    for package, label in [
        ("microsoft.aspnetcore.app.runtime.win-x64", "AspNetCore"),
        ("microsoft.netcore.app.runtime.win-x64", "DotNet"),
        ("microsoft.netcore.app.host.win-x64", "DotNet-Host"),
    ]:
        directory = packages / package / RUNTIME_VERSION
        notices = sorted(
            path for path in directory.iterdir()
            if path.is_file() and path.name.upper() in {"LICENSE.TXT", "THIRD-PARTY-NOTICES.TXT"}
        )
        if not notices:
            raise FileNotFoundError(f"Runtime notices are missing from {directory}")
        for notice in notices:
            shutil.copyfile(notice, destination / (label + "-" + notice.name))


def package(source, output_directory):
    output_directory.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(dir=output_directory) as temporary:
        work = Path(temporary)
        publish = work / BUNDLE
        publish.mkdir()
        count = build_jackets(source, publish / "jackets.zip")
        subprocess.run(["sh", "build.sh"], cwd=ROOT, check=True)
        subprocess.run([
            "dotnet", "publish", "src/StellarTool.Host", "-c", "Release",
            "-r", "win-x64", "--self-contained", "true",
            f"-p:RuntimeFrameworkVersion={RUNTIME_VERSION}",
            "--source", "https://api.nuget.org/v3/index.json", "-o", str(publish),
        ], cwd=ROOT, check=True)
        mod = ROOT / "dist/mod/StellarTool.Mod.dll"
        (publish / "mod").mkdir()
        shutil.copyfile(mod, publish / "mod" / mod.name)
        for name in ("scripts/Start-StellarTool.cmd", "README.md", "LICENSE", "docs/local-assets.md"):
            destination = publish / (name if name.startswith("docs/") else Path(name).name)
            destination.parent.mkdir(exist_ok=True)
            shutil.copyfile(ROOT / name, destination)
        copy_runtime_notices(publish / "licenses")
        archive = work / (BUNDLE + ".zip")
        with ZipFile(archive, "w", compression=ZIP_DEFLATED) as bundle:
            for member in sorted(publish.rglob("*")):
                if member.is_file() and member.suffix.lower() != ".pdb":
                    compression = ZIP_STORED if member.name == "jackets.zip" else ZIP_DEFLATED
                    bundle.write(member, member.relative_to(work).as_posix(), compress_type=compression)
        archive.replace(output_directory / archive.name)
        shutil.copyfile(mod, output_directory / mod.name)
        print(f"Packaged {count} jacket PNGs in {output_directory / archive.name}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, help="source ZIP with authorized jacket PNGs")
    parser.add_argument("output_directory", type=Path, help="directory for the Web ZIP and Mod DLL")
    args = parser.parse_args()
    package(args.source.resolve(strict=True), args.output_directory.resolve())


if __name__ == "__main__":
    main()
