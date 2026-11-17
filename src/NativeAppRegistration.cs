namespace ObenseuerOS
{
    internal static class NativeAppRegistration
    {
        internal static bool Valid(bool page, bool launcher, bool native, bool nativeCallbacks)
        {
            return (page ? 1 : 0) + (launcher ? 1 : 0) + (native ? 1 : 0) == 1 && (!nativeCallbacks || native);
        }
    }
}
