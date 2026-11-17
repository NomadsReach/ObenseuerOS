namespace ObenseuerOS
{
    internal sealed class WindowVisibility
    {
        internal bool Visible { get; private set; }
        internal bool Update(bool web, bool toolkit)
        {
            bool next = web || toolkit;
            if (Visible == next) return false;
            Visible = next;
            return true;
        }
    }
}
