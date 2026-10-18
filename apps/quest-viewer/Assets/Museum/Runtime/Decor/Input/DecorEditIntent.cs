using System;

namespace FamilyMuseum.Decor
{
    public enum DecorInputSource { PC, VR }
    public enum DecorInputPhase { Started, Changed, Performed, Cancelled }
    public enum DecorEditAction
    {
        ReleaseControls, Select, BeginPlace, BeginMove, UpdateTarget, ConfirmPlacement,
        CancelPreview, ClearSelection, RotateStep, Store, SetVariant, SampleDefinition,
        Undo, Redo, SetSnap, SetRepeat, Apply, RequestDiscard, ConfirmDiscard, ResumeEdit, Reload
    }
    // Input generations are deliberately absent from the durable world DTO.
    public sealed class DecorEditIntent
    {
        public string sessionToken, pointerId, instanceId, definitionId, variantId, candidateToken, confirmationToken;
        public int definitionVersion, direction;
        public long sequence;
        public bool enabled;
        public DecorInputSource source;
        public DecorInputPhase phase = DecorInputPhase.Performed;
        public DecorEditAction action;
        public DecorPointerRay ray;
    }
    public sealed class DecorPointerRay
    {
        public double[] originM, direction;
        public double maxDistanceM;
        public bool tracked;
        public bool Valid
        {
            get
            {
                if (!tracked || originM == null || direction == null || originM.Length != 3 || direction.Length != 3 ||
                    !Finite(maxDistanceM) || maxDistanceM <= 0) return false;
                double square = 0;
                for (int i = 0; i < 3; i++)
                { if (!Finite(originM[i]) || !Finite(direction[i])) return false; square += direction[i] * direction[i]; }
                return Finite(square) && square > 1e-12;
            }
        }
        static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
    }
    public sealed class DecorPointerHit
    {
        public string instanceId, reasonCode;
        public ItemLocation target;
        public bool blocked;
    }
    // Queries must return the first visible surface, respecting distance/layers/UI occlusion.
    // Platform adapters convert screen/controller poses to this same world-ray contract.
    public interface IDecorWorldPointer
    {
        DecorPointerHit Query(DecorPointerRay ray, DecorWorld world, ItemInstance preview, bool snap);
    }
    public sealed class DecorIntentResult
    {
        public string status, reasonCode, candidateToken, confirmationToken;
        public bool commandAccepted;
    }
}
