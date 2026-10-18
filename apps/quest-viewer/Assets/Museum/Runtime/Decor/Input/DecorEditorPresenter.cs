using System.Linq;

namespace FamilyMuseum.Decor
{
    // A detached value snapshot. Neither UI owns the session or reads a platform device.
    public sealed class DecorEditorViewState
    {
        public string sessionToken, selectedId, state, confirmationToken;
        public DecorInputSource source;
        public bool canEdit, preview, previewValid, snap, repeat, suspended, awaitingRelease;
        public bool canApply, canUndo, canRedo, canMove, canRotate, canStore;
        public int undoCount, redoCount;
    }
    public static class DecorEditorPresenter
    {
        public static DecorEditorViewState Read(DecorEditorSession session)
        {
            bool canEdit = session.CanEdit && !session.Input.Suspended && !session.Input.AwaitingRelease;
            var selected = session.Snapshot()?.instances.FirstOrDefault(i => i.id == session.SelectedId);
            return new DecorEditorViewState {
                sessionToken = session.Input.SessionToken, source = session.Input.Source,
                selectedId = session.SelectedId, state = session.State, confirmationToken = session.ConfirmationToken,
                canEdit = canEdit,
                canApply = canEdit && !session.HasPreview, canUndo = canEdit && session.UndoCount > 0,
                canRedo = canEdit && session.RedoCount > 0, canMove = canEdit && selected != null && !session.HasPreview,
                canRotate = canEdit && (session.HasPreview || selected != null && selected.location.kind != "storage"),
                canStore = canEdit && !session.HasPreview && selected != null && selected.location.kind != "storage",
                preview = session.HasPreview, previewValid = session.PreviewValid,
                snap = session.Snap, repeat = session.Repeat, undoCount = session.UndoCount, redoCount = session.RedoCount,
                suspended = session.Input.Suspended, awaitingRelease = session.Input.AwaitingRelease
            };
        }
    }
}
