namespace ObenseuerOS
{
    internal static class NativeAppRegistration
    {
        internal static bool Valid(bool page, bool launcher, bool native, bool closeGuard)
        {
            return (page ? 1 : 0) + (launcher ? 1 : 0) + (native ? 1 : 0) == 1 && (!closeGuard || native);
        }
    }
}
