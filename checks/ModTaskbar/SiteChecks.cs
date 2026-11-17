using System;
using ObenseuerOS;
using UnityEngine.UIElements;

internal static class SiteChecks
{
    private static void Check(bool value) { if (!value) throw new Exception("Native site lifecycle check failed"); }
    internal static void Run()
    {
        bool allow = false, valid = true, closeDuringBuild = true;
        int built = 0, closed = 0, aborted = 0, committed = 0;
        string path = null;
        VisualElement content = new VisualElement();
        string navigated = null;
        ToolkitSite manager = new ToolkitSite(content, target => { navigated = target; return true; });
        OsSitePage mounted = null;
        OsApp app = new OsApp { Id = "native-site" };
        app.BuildSite = (root, suffix) =>
        {
            built++; path = suffix; closeDuringBuild = manager.CanClose();
            root.Add(new Label("page"));
            mounted = new OsSitePage { CanClose = () => allow, OnClosed = forced => { if (forced) aborted++; else closed++; } };
            return mounted;
        };
        Os.Apps.Add(app);
        Check(manager.Transition(app, "site.ts/first?q=1#top", () => valid, () => committed++));
        Check(path == "/first?q=1#top" && !closeDuringBuild && built == 1 && committed == 1 && content.childCount == 1);
        OsSitePage oldPage = mounted;
        Check(oldPage.Navigate("/next?x=2#part") && navigated == "site.ts/next?x=2#part");
        Check(!oldPage.Navigate("//outside.test") && !oldPage.Navigate("javascript:bad") && !oldPage.Navigate("site.ts:80/path"));
        Check(oldPage.Navigate("?new=1") && navigated == "site.ts/first?new=1");
        Check(!manager.Transition(app, "site.ts/second", () => valid, () => committed++) && built == 1 && committed == 1 && closed == 0);
        allow = true;
        var builder = app.BuildSite;
        app.BuildSite = (root, suffix) => oldPage;
        Check(!manager.Transition(app, "site.ts/reuse", () => valid, () => committed++) && manager.IsCurrent("site.ts/first?q=1#top") && aborted == 0);
        app.BuildSite = builder;
        Check(!manager.Transition(app, "site.ts/second", () => valid, () => { throw new InvalidOperationException(); }));
        Check(built == 2 && aborted == 1 && closed == 0 && manager.IsCurrent("site.ts/first?q=1#top"));
        Check(manager.Transition(app, "site.ts/second", () => valid, () => committed++));
        Check(built == 3 && closed == 1 && committed == 2 && manager.IsCurrent("site.ts/second"));
        Check(!oldPage.Navigate("/stale") && mounted.Navigate("#section") && navigated == "site.ts/second#section");
        Check(OsSitePage.Resolve("site.ts/path#fragment?old=1", "?new=2") == "site.ts/path?new=2");
        app.BuildSite = (root, suffix) => oldPage;
        Check(!manager.Transition(app, "site.ts/reuse-closed", () => valid, () => committed++) && !oldPage.Navigate("/stale"));
        app.BuildSite = builder;
        allow = false; Check(!manager.Dismiss() && manager.IsCurrent("site.ts/second"));
        allow = true; Check(manager.Dismiss() && closed == 2 && content.childCount == 0);
        Check(manager.Transition(app, "site.ts/third", () => valid, () => committed++));
        Os.Apps.Remove(app); manager.ForceClose(null);
        Check(aborted == 2 && content.childCount == 0);
        Check(!manager.Transition(app, "site.ts/other", () => valid, () => committed++));
        Os.Apps.Add(app); allow = true;
        Check(manager.Transition(app, "site.ts/before-removal", () => valid, () => committed++));
        int oldClosed = closed, oldAborted = aborted;
        Check(!manager.Transition(app, "site.ts/removed", () => valid, () => { committed++; Os.Apps.Remove(app); }));
        Check(closed == oldClosed + 1 && aborted == oldAborted + 1 && !manager.IsSite(app) && content.childCount == 0);
        OsApp first = new OsApp { Id = "old-site" }, destination = new OsApp { Id = "destination-site" };
        int firstClosed = 0, destinationAborted = 0;
        first.BuildSite = (root, suffix) => new OsSitePage { OnClosed = forced => { firstClosed++; Os.Apps.Remove(destination); } };
        destination.BuildSite = (root, suffix) => new OsSitePage { OnClosed = forced => { if (forced) destinationAborted++; } };
        Os.Apps.Add(first); Os.Apps.Add(destination);
        Check(manager.Transition(first, "first.ts", () => valid, () => committed++));
        Check(!manager.Transition(destination, "dest.ts", () => valid, () => committed++));
        Check(firstClosed == 1 && destinationAborted == 1 && !manager.IsSite(first) && !manager.IsSite(destination) && content.childCount == 0);
        OsApp reentrant = new OsApp { Id = "reentrant-site" }; Os.Apps.Add(reentrant);
        bool reopened = true;
        reentrant.BuildSite = (root, suffix) => new OsSitePage { OnClosed = forced => { reopened = manager.Transition(reentrant, "site.ts/reopen", () => valid, () => committed++); } };
        Check(manager.Transition(reentrant, "site.ts/original", () => valid, () => committed++));
        manager.ForceClose(() => content.Clear());
        Check(!reopened && !manager.IsSite(reentrant) && content.childCount == 0);
    }
}
