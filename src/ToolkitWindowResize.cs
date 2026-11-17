using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitWindowResize
    {
        private readonly VisualElement window;
        private readonly Label grip;
        private readonly Func<bool> allowed;
        private int pointer = -1;
        private Vector2 start;
        private float width, height;

        internal ToolkitWindowResize(VisualElement window, Func<bool> allowed)
        {
            this.window = window; this.allowed = allowed;
            grip = new Label("◢"); grip.enableRichText = false; grip.tooltip = "Resize";
            grip.style.position = Position.Absolute; grip.style.right = 0; grip.style.bottom = 0;
            grip.style.width = 16; grip.style.height = 16; grip.style.color = (Color)new Color32(110, 130, 150, 255);
            grip.style.unityTextAlign = TextAnchor.MiddleCenter;
            grip.RegisterCallback<PointerDownEvent>(Down);
            grip.RegisterCallback<PointerMoveEvent>(Move);
            grip.RegisterCallback<PointerUpEvent>(Up);
            grip.RegisterCallback<PointerCancelEvent>(CancelEvent);
            grip.RegisterCallback<PointerCaptureOutEvent>(CaptureLost);
            window.Add(grip);
        }
        private void Down(PointerDownEvent e)
        {
            if (e.button != 0 || pointer >= 0 || !allowed() || window.parent == null || window.layout.width <= 0 || window.layout.height <= 0) return;
            pointer = e.pointerId; start = window.parent.WorldToLocal(new Vector2(e.position.x, e.position.y));
            width = window.layout.width; height = window.layout.height;
            window.BringToFront(); grip.CapturePointer(pointer); e.StopPropagation();
        }
        private void Move(PointerMoveEvent e)
        {
            if (e.pointerId != pointer || window.parent == null || !allowed()) return;
            Vector2 current = window.parent.WorldToLocal(new Vector2(e.position.x, e.position.y));
            window.style.width = Math.Max(360f, width + current.x - start.x);
            window.style.height = Math.Max(240f, height + current.y - start.y);
            e.StopPropagation();
        }
        private void Up(PointerUpEvent e) { if (e.pointerId == pointer) { Cancel(); e.StopPropagation(); } }
        private void CancelEvent(PointerCancelEvent e) { if (e.pointerId == pointer) Cancel(); }
        private void CaptureLost(PointerCaptureOutEvent e) { if (e.pointerId == pointer) pointer = -1; }
        internal void Cancel()
        {
            int id = pointer; pointer = -1;
            if (id >= 0 && grip.HasPointerCapture(id)) grip.ReleasePointer(id);
        }
        internal void Release() { Cancel(); grip.RemoveFromHierarchy(); }
    }
}
