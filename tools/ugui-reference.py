import functools
import http.server
import threading

from playwright.sync_api import sync_playwright


def main():
    handler = functools.partial(http.server.SimpleHTTPRequestHandler, directory="web")
    server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), handler)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    try:
        with sync_playwright() as playwright:
            browser = playwright.chromium.launch()
            page = browser.new_page(viewport={"width": 1280, "height": 720}, device_scale_factor=1)
            page.goto("http://127.0.0.1:%d/" % server.server_port, wait_until="load")
            page.locator('#desk [data-app="notes"]').wait_for()
            page.wait_for_timeout(2000)
            page.screenshot(path="reference-desktop.png")
            page.locator('#desk [data-app="notes"]').dblclick()
            page.locator('.window[data-app="notes"] textarea').wait_for()
            page.screenshot(path="reference-notepad.png")
            browser.close()
    finally:
        server.shutdown()
        server.server_close()


if __name__ == "__main__":
    main()
