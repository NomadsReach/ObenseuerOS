using System;
using System.Collections.Generic;

namespace UnityEngine
{
    internal struct Color
    {
        internal float r, g, b, a;
        public static Color black { get { return new Color { a = 1 }; } }
        public static Color white { get { return new Color { r = 1, g = 1, b = 1, a = 1 }; } }
        public static Color clear { get { return new Color(); } }
    }
    internal struct Color32
    {
        private readonly byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static implicit operator Color(Color32 color) { return new Color { r = color.r / 255f, g = color.g / 255f, b = color.b / 255f, a = color.a / 255f }; }
    }
    internal struct Vector2
    {
        internal float x, y;
        internal Vector2(float x, float y) { this.x = x; this.y = y; }
    }
    internal struct Vector3 { internal float x, y, z; internal Vector3(float x, float y) { this.x = x; this.y = y; z = 0; } }
    internal struct Rect { internal float x, y, width, height; internal Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; } }
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
    internal enum StyleKeyword { Undefined, Auto }
    internal enum TrickleDown { TrickleDown }
    internal struct StyleLength
    {
        internal float number;
        internal StyleKeyword keyword;
        public static implicit operator StyleLength(int value) { return new StyleLength { number = value }; }
        public static implicit operator StyleLength(float value) { return new StyleLength { number = value }; }
        public static implicit operator StyleLength(StyleKeyword value) { return new StyleLength { keyword = value }; }
    }
    internal class PointerEvent
    {
        internal int pointerId;
        internal Vector3 position;
        internal bool stopped;
        internal void StopPropagation() { stopped = true; }
        internal void StopImmediatePropagation() { stopped = true; }
    }
    internal sealed class PointerDownEvent : PointerEvent { internal int button; internal object target; }
    internal sealed class PointerMoveEvent : PointerEvent { }
    internal sealed class PointerUpEvent : PointerEvent { }
    internal sealed class PointerCancelEvent : PointerEvent { }
    internal sealed class PointerCaptureOutEvent : PointerEvent { }
    internal sealed class Style
    {
        internal DisplayStyle display = DisplayStyle.Flex;
        internal StyleLength left, top, right, bottom, width, height;
        internal object position, paddingLeft, paddingRight, paddingBottom, backgroundColor;
        internal object flexDirection, flexGrow, flexShrink, color, unityTextAlign, marginTop, minWidth, alignSelf;
    }
    internal sealed class Scheduled { internal Scheduled Every(long value) { return this; } }
    internal sealed class Scheduler { internal Scheduled Execute(Action action) { return new Scheduled(); } }
    internal class VisualElement
    {
        private readonly List<VisualElement> children = new List<VisualElement>();
        internal VisualElement parent;
        internal bool focusable;
        private readonly Dictionary<Type, Delegate> callbacks = new Dictionary<Type, Delegate>();
        private int captured = -1;
        internal string tooltip;
        internal Rect layout;
        internal readonly Style style = new Style();
        internal readonly Scheduler schedule = new Scheduler();
        internal int childCount { get { return children.Count; } }
        internal VisualElement this[int index] { get { return children[index]; } }
        internal void RegisterCallback<T>(Action<T> callback) { callbacks[typeof(T)] = callback; }
        internal void RegisterCallback<T>(Action<T> callback, TrickleDown phase) { RegisterCallback(callback); }
        internal void UnregisterCallback<T>(Action<T> callback) { callbacks.Remove(typeof(T)); }
        internal void Dispatch<T>(T e) { Delegate callback; if (callbacks.TryGetValue(typeof(T), out callback)) ((Action<T>)callback)(e); }
        internal Vector2 WorldToLocal(Vector2 value) { return value; }
        internal void CapturePointer(int id) { captured = id; }
        internal bool HasPointerCapture(int id) { return captured == id; }
        internal void ReleasePointer(int id) { if (captured == id) captured = -1; }
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
        internal string text;
        internal void Press() { if (action != null) action(); }
    }
    internal sealed class Image : VisualElement { internal Texture2D image; internal ScaleMode scaleMode; internal Color tintColor; }
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
    internal static class ToolkitUi
    {
        internal static void MakeDraggable(VisualElement window, VisualElement bar) { }
        internal static ToolkitResizeHandle MakeResizable(VisualElement window, VisualElement grip, float minWidth, float minHeight, Func<bool> allowed)
        {
            return new ToolkitResizeHandle(window, grip, minWidth, minHeight, allowed);
        }
    }
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
        internal Action<bool> OnClosed;
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
