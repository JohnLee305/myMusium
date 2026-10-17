namespace FamilyMuseum.Decor
{
    // Presentation only: never serialized into DecorWorld.
    public sealed class EditorPresentationSettings
    {
        public readonly float toolbarHeight = 56, catalogHeight = 260, shortCatalogHeight = 232, detailsWidth = 260;
        public readonly float cardWidth = 144, cardHeight = 126, bodySize = 16, captionSize = 14;
        public readonly float compactWidth = 1150, defaultDistance = 9, minDistance = 1.2f, maxDistance = 42;
        public readonly float minPitch = 20, maxPitch = 80, initialPitch = 48, initialYaw = -35;
        public readonly float panSpeed = .65f, orbitSpeed = .16f, zoomSpeed = .16f;
        public readonly string background = "#F3F1E9", ink = "#263B35", accent = "#285C4B", muted = "#62736B", gold = "#A98342";
        public readonly string floor = "#DBD5C6", wall = "#D1D6C8", sky = "#3D4F45";
        public readonly string fontResource = "ThoughtFont";
    }
    public static class DecorInputPolicy
    {
        public static bool KeyboardAllowed(bool textEditing) { return !textEditing; }
        public static bool PointerAllowed(bool overUI, bool textEditing) { return !overUI && !textEditing; }
    }
}
