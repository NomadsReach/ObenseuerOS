namespace ObenseuerOS
{
    internal static class VirusPopupRules
    {
        internal static bool Room(int count) { return count < 4; }
        internal static bool Dodge(int dodges, double roll) { return dodges < 2 && roll < 0.35; }
        internal static bool Spawn(int count, bool spawned, double roll) { return !spawned && roll < 0.15 && count + 2 <= 4; }
    }
}
