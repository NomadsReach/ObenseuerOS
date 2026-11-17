using System;
using ObenseuerOS;
using UnityEngine.UIElements;

internal static class Program
{
    private static void Check(bool value) { if (!value) throw new Exception("Native taskbar lifecycle check failed"); }
    private static void Main()
    {
        bool valid = true, allowClose = false;
        VisualElement windows = new VisualElement();
        ToolkitTaskbar taskbar = new ToolkitTaskbar(windows, name => null);
        ToolkitModWindows mods = new ToolkitModWindows(windows, () => valid); mods.SetTaskbar(taskbar);
        OsApp app = new OsApp { Id = "one", Title = "One", BuildUi = content => content.Add(new Label("content")), CanClose = () => allowClose };
        Os.Apps.Add(app);
        Check(mods.Open(app) && mods.Has(app) && Os.OpenedCount == 1 && taskbar.Items.childCount == 1);
        VisualElement frame = windows[0];
        Button maximize = (Button)frame[0][2]; maximize.Press();
        Check(frame.style.left.number == 0 && frame.style.top.number == 0 && frame.style.width.keyword == StyleKeyword.Auto && frame.style.height.keyword == StyleKeyword.Auto);
        PointerDownEvent drag = new PointerDownEvent { button = 0, target = frame[0][0] }; frame[0].Dispatch(drag); Check(drag.stopped);
        PointerDownEvent control = new PointerDownEvent { button = 0, target = maximize }; frame[0].Dispatch(control); Check(!control.stopped);
        ((Button)frame[0][1]).Press();
        Check(frame.style.display == DisplayStyle.None && mods.Has(app) && Os.ClosedCount == 0);
        Check(mods.Open(app) && frame.style.display == DisplayStyle.Flex && Os.OpenedCount == 1 && frame.style.width.keyword == StyleKeyword.Auto);
        maximize.Press(); Check(frame.style.left.number == 160 && frame.style.top.number == 40 && frame.style.width.number == 600 && frame.style.height.number == 450);
        PointerDownEvent restored = new PointerDownEvent { button = 0, target = frame[0][0] }; frame[0].Dispatch(restored); Check(!restored.stopped);
        mods.Close(app, false); Check(mods.Has(app) && taskbar.Items.childCount == 1 && Os.ClosedCount == 0);
        allowClose = true; mods.Close(app, false);
        Check(!mods.Has(app) && taskbar.Items.childCount == 0 && windows.childCount == 0 && Os.ClosedCount == 1);
        OsApp reentrant = new OsApp { Id = "two", Title = "Two" };
        reentrant.BuildUi = content => { Os.Apps.Remove(reentrant); mods.Close(reentrant, true); };
        Os.Apps.Add(reentrant);
        Check(!mods.Open(reentrant) && !mods.Has(reentrant) && taskbar.Items.childCount == 0 && Os.OpenedCount == 1 && Os.ClosedCount == 1);
        OsApp broken = new OsApp { Id = "bad", Title = "Bad", BuildUi = content => { throw new InvalidOperationException("failed"); } };
        Os.Apps.Add(broken);
        Check(!mods.Open(broken) && Os.CrashCount == 1 && windows.childCount == 0 && taskbar.Items.childCount == 0);
        allowClose = false; Check(mods.Open(app)); valid = false; mods.Reconcile();
        Check(!mods.Has(app) && windows.childCount == 0 && taskbar.Items.childCount == 0 && Os.ClosedCount == 2);
        Console.WriteLine("Native taskbar lifecycle checks passed");
    }
}
