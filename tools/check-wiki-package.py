import shutil
import subprocess
import tempfile
import zipfile
from pathlib import Path


root = Path(__file__).resolve().parent.parent
with tempfile.TemporaryDirectory(prefix="wiki-package-check-") as temporary:
    project = Path(temporary)
    shutil.copy2(root / "ObenseuerOS.csproj", project / "ObenseuerOS.csproj")
    shutil.copy2(root / "Directory.Build.props", project / "Directory.Build.props")
    for name in ("bin/ObenseuerOS.dll", "bin/be5/ObenseuerOS.dll", "web/index.html", "wiki-offline/pages/example.page", "wiki-offline/LICENSE.txt"):
        target = project / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text("fixture-" + name, encoding="utf-8")
    result = subprocess.run(["dotnet", "msbuild", str(project / "ObenseuerOS.csproj"), "-nologo", "-target:Package", "-p:ZipVersion=ci-fixture"], cwd=project, text=True, capture_output=True)
    if result.returncode:
        raise RuntimeError(result.stdout + result.stderr)
    archive = project / "dist" / "ObenseuerOS-ci-fixture.zip"
    with zipfile.ZipFile(archive) as package:
        names = set(package.namelist())
        prefix = "BepInEx/plugins/ObenseuerOS/"
        expected = {prefix + "wiki-offline/pages/example.page", prefix + "wiki-offline/LICENSE.txt"}
        if not expected.issubset(names):
            raise RuntimeError("Offline wiki files missing from package: " + repr(sorted(expected - names)))
        if package.read(prefix + "wiki-offline/pages/example.page") != b"fixture-wiki-offline/pages/example.page":
            raise RuntimeError("Bundled article was changed during packaging")
    print("Offline wiki package layout passed.")
