import json
import subprocess
import tempfile
from pathlib import Path


ICONS = (
    "mail", "world", "folder-filled", "trash", "settings", "chart-line",
    "terminal-2", "notes", "building-bank", "news", "message-circle",
    "shopping-cart", "device-gamepad-2", "x", "viewports",
)


def msbuild(*args):
    return subprocess.run(["dotnet", "msbuild", "ObenseuerOS.csproj", "-nologo", *args], text=True, capture_output=True)


def main():
    result = msbuild("-getItem:EmbeddedResource")
    if result.returncode:
        raise RuntimeError(result.stdout + result.stderr)
    data = json.loads(result.stdout)
    names = {item.get("LogicalName") for item in data["Items"]["EmbeddedResource"]}
    expected = {"desktop-icons/" + icon + ".png" for icon in ICONS}
    if not expected.issubset(names):
        raise RuntimeError("Missing resource entries: " + repr(sorted(expected - names)))

    present = msbuild("-target:RequireToolkitIcons")
    if present.returncode:
        raise RuntimeError("Required icons were rejected: " + present.stdout + present.stderr)

    icon = Path("prototype-icons/mail.png")
    with tempfile.TemporaryDirectory(dir=icon.parent) as temporary:
        held = Path(temporary) / icon.name
        icon.rename(held)
        try:
            missing = msbuild("-target:RequireToolkitIcons")
            if missing.returncode == 0 or "Missing Toolkit icon mail" not in missing.stdout + missing.stderr:
                raise RuntimeError("The OS project did not reject a missing mail icon: " + missing.stdout + missing.stderr)
        finally:
            held.rename(icon)
    print("OS project icon entries and missing-file guard passed.")


if __name__ == "__main__":
    main()
