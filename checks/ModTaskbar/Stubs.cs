using System;
using System.Collections.Generic;

namespace UnityEngine
{
    internal struct Color
    {
        public static Color black { get { return new Color(); } }
        public static Color white { get { return new Color(); } }
        public static Color clear { get { return new Color(); } }
    }
    internal struct Color32
    {
        public Color32(byte r, byte g, byte b, byte a) { }
        public static implicit operator Color(Color32 color) { return new Color(); }
    }
    internal enum TextAnchor { MiddleLeft, MiddleCenter }
    internal enum ScaleMode { ScaleToFit }
    internal sealed class Texture2D { }
}
namespace UnityEngine.UIElements
{
    using UnityEngine;
    internal enum Position { Absolute }
    internal enum FlexDirection { Row }
    internal enum DisplayStyle { Flex, None }
    internal enum Align { Center }
    internal enum ScrollViewMode { Horizontal }
    internal enum ScrollerVisibility { Auto, Hidden }
    internal sealed class Style
    {
        internal DisplayStyle display = DisplayStyle.Flex;
        internal object position, left, top, width, height, paddingLeft, paddingRight, paddingBottom, backgroundColor;
        internal object flexDirection, flexGrow, flexShrink, color, unityTextAlign, marginTop, minWidth, alignSelf;
    }
    internal sealed class Scheduled { internal Scheduled Every(long value) { return this; } }
    internal sealed class Scheduler { internal Scheduled Execute(Action action) { return new Scheduled(); } }
    internal class VisualElement
    {
        private readonly List<VisualElement> children = new List<VisualElement>();
        private VisualElement parent;
        internal readonly Style style = new Style();
        internal readonly Scheduler schedule = new Scheduler();
        internal int childCount { get { return children.Count; } }
        internal VisualElement this[int index] { get { return children[index]; } }
        internal void Add(VisualElement child) { child.RemoveFromHierarchy(); children.Add(child); child.parent = this; }
        internal void Insert(int index, VisualElement child) { child.RemoveFromHierarchy(); children.Insert(index, child); child.parent = this; }
        internal void RemoveFromHierarchy() { if (parent != null) { parent.children.Remove(this); parent = null; } }
        internal void BringToFront() { if (parent != null) { VisualElement owner = parent; RemoveFromHierarchy(); owner.Add(this); } }
    }
    internal sealed class ScrollView : VisualElement
    {
        internal ScrollView(ScrollViewMode mode) { }
        internal ScrollerVisibility horizontalScrollerVisibility, verticalScrollerVisibility;
        internal VisualElement contentContainer { get { return this; } }
    }
    internal sealed class Button : VisualElement
    {
        private readonly Action action;
        internal Button(Action action) { this.action = action; }
        internal string text, tooltip;
        internal void Press() { if (action != null) action(); }
    }
    internal sealed class Image : VisualElement { internal Texture2D image; internal ScaleMode scaleMode; }
    internal sealed class Label : VisualElement
    {
        internal Label(string value) { text = value; }
        internal string text;
        internal bool enableRichText;
    }
}
namespace ObenseuerCore.Ui
{
    using UnityEngine.UIElements;
    internal static class ToolkitUi { internal static void MakeDraggable(VisualElement window, VisualElement bar) { } }
}
namespace ObenseuerOS
{
    using UnityEngine.UIElements;
    internal sealed class OsApp
    {
        internal string Id, Title, Icon;
        internal int Width, Height;
        internal Action<VisualElement> BuildUi;
        internal Func<bool> CanClose;
    }
    internal static class Os
    {
        internal static readonly List<OsApp> Apps = new List<OsApp>();
        internal static readonly HashSet<string> Open = new HashSet<string>();
        internal static int OpenedCount, ClosedCount, CrashCount;
        internal static void Opened(string id) { if (Open.Add(id)) OpenedCount++; }
        internal static void Closed(string id) { if (Open.Remove(id)) ClosedCount++; }
        internal static void Crash(string where, Exception error) { CrashCount++; }
    }
}
