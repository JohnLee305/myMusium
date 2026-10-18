using System;

namespace FamilyMuseum.Decor
{
    // Main-thread coordinator; adapters share a source-wide sequence, including both VR hands.
    public sealed class DecorInputCoordinator
    {
        readonly long[] sequences = { -1, -1 };
        public DecorInputSource Source { get; private set; }
        public string SessionToken { get; private set; }
        public bool Suspended { get; private set; }
        public bool AwaitingRelease { get; private set; }
        public string PreviewPointer { get; private set; }
        public DecorInputCoordinator(DecorInputSource source) { Source = source; Renew(); }
        void Renew() { SessionToken = Guid.NewGuid().ToString("N"); AwaitingRelease = true; PreviewPointer = null; }
        internal void Switch(DecorInputSource source) { Source = source; Suspended = false; Renew(); }
        internal void Suspend() { Suspended = true; Renew(); }
        internal void Resume() { Suspended = false; Renew(); }
        internal void ClearPreview() { PreviewPointer = null; }
        internal void OwnPreview(string pointer) { PreviewPointer = pointer; }
        internal string Admit(DecorEditIntent intent, bool uiConsumed, bool textEditing)
        {
            if (intent == null || !Enum.IsDefined(typeof(DecorInputSource), intent.source) ||
                !Enum.IsDefined(typeof(DecorInputPhase), intent.phase) || !Enum.IsDefined(typeof(DecorEditAction), intent.action)) return "INPUT_INVALID";
            if (intent.sessionToken != SessionToken) return "STALE_SESSION";
            if (intent.source != Source) return "INACTIVE_SOURCE";
            if (Suspended) return "INPUT_SUSPENDED";
            int index = (int)intent.source;
            if (intent.sequence < 0 || intent.sequence <= sequences[index]) return "DUPLICATE_INPUT";
            sequences[index] = intent.sequence;
            // ReleaseControls is an adapter observation: ALL controls were released, not a button command.
            if (intent.action == DecorEditAction.ReleaseControls)
            {
                if (intent.phase != DecorInputPhase.Performed) return "PHASE_INVALID";
                AwaitingRelease = false; return null;
            }
            if (AwaitingRelease) return "RELEASE_REQUIRED";
            if (string.IsNullOrEmpty(intent.pointerId)) return "POINTER_REQUIRED";
            if (uiConsumed) return "UI_CONSUMED";
            if (textEditing) return "TEXT_EDITING";
            if (intent.action == DecorEditAction.UpdateTarget)
            { if (intent.phase != DecorInputPhase.Changed) return "PHASE_INVALID"; }
            else if (intent.action == DecorEditAction.CancelPreview)
            { if (intent.phase != DecorInputPhase.Performed && intent.phase != DecorInputPhase.Cancelled) return "PHASE_INVALID"; }
            else if (intent.phase != DecorInputPhase.Performed) return "PHASE_INVALID";
            if (PreviewPointer != null && (intent.action == DecorEditAction.UpdateTarget || intent.action == DecorEditAction.ConfirmPlacement) &&
                PreviewPointer != intent.pointerId) return "PREVIEW_OWNED";
            return null;
        }
    }
}
