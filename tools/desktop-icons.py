import base64
from pathlib import Path

from playwright.sync_api import sync_playwright


ICONS = (
    "mail", "world", "folder-filled", "trash", "settings", "chart-line",
    "terminal-2", "notes", "building-bank", "news", "message-circle",
    "shopping-cart", "device-gamepad-2", "x", "viewports",
)


def main():
    output = Path("prototype-icons")
    output.mkdir(exist_ok=True)
    (output / "LICENSE-tabler.txt").write_text(Path("web/icons/LICENSE-tabler.txt").read_text(encoding="utf-8"), encoding="utf-8")
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch()
        page = browser.new_page()
        for name in ICONS:
            source = Path("web/brand/viewports.svg") if name == "viewports" else Path("web/icons") / (name + ".svg")
            svg = source.read_text(encoding="utf-8").replace("currentColor", "#ffffff")
            encoded = page.evaluate("""async (svg) => {
                const image = new Image();
                image.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg);
                await image.decode();
                const canvas = document.createElement('canvas');
                canvas.width = canvas.height = 96;
                const context = canvas.getContext('2d');
                context.drawImage(image, 0, 0, 96, 96);
                const pixels = context.getImageData(0, 0, 96, 96).data;
                if (!pixels.some((value, index) => index % 4 === 3 && value !== 0)) throw new Error('Empty icon');
                return canvas.toDataURL('image/png').split(',')[1];
            }""", svg)
            (output / (name + ".png")).write_bytes(base64.b64decode(encoded))
        browser.close()


if __name__ == "__main__":
    main()
