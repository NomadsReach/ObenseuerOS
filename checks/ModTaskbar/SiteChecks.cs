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
        ToolkitSite manager = new ToolkitSite(content);
        OsApp app = new OsApp { Id = "native-site" };
        app.BuildSite = (root, suffix) =>
        {
            built++; path = suffix; closeDuringBuild = manager.CanClose();
            root.Add(new Label("page"));
            return new OsSitePage { CanClose = () => allow, OnClosed = forced => { if (forced) aborted++; else closed++; } };
        };
        Os.Apps.Add(app);
        Check(manager.Transition(app, "site.ts/first?q=1#top", () => valid, () => committed++));
        Check(path == "/first?q=1#top" && !closeDuringBuild && built == 1 && committed == 1 && content.childCount == 1);
        Check(!manager.Transition(app, "site.ts/second", () => valid, () => committed++) && built == 1 && committed == 1 && closed == 0);
        allow = true;
        Check(!manager.Transition(app, "site.ts/second", () => valid, () => { throw new InvalidOperationException(); }));
        Check(built == 2 && aborted == 1 && closed == 0 && manager.IsCurrent("site.ts/first?q=1#top"));
        Check(manager.Transition(app, "site.ts/second", () => valid, () => committed++));
        Check(built == 3 && closed == 1 && committed == 2 && manager.IsCurrent("site.ts/second"));
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
