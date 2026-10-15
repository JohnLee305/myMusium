using System.Collections.Generic;
using UnityEngine;

namespace FamilyMuseum
{
    [CreateAssetMenu(menuName="Museum/Visitors/Appearance")]
    public sealed class VisitorAppearance : ScriptableObject
    {
        public string id;
        public string displayName;
        public GameObject prefab;
        public AnimationClip idle;
        public AnimationClip walk;
        [Min(.1f)] public float height=1.75f;
        public float forwardYaw;
        [Range(.2f,2f)] public float walkSpeed=.9f;
        [Range(.2f,2f)] public float animationSpeed=1f;
        [Min(0)] public float weight=1;
        public bool available=true;
        public string creator;
        public string sourceUrl;
        public string license;
        public bool IsUsable => available && !string.IsNullOrWhiteSpace(id) && prefab!=null &&
            prefab.GetComponentsInChildren<Animator>(true).Length==1 && idle!=null && walk!=null &&
            idle.length>0 && walk.length>0 && height>.1f && height<3f && walkSpeed>0 &&
            !float.IsNaN(walkSpeed) && !float.IsInfinity(walkSpeed) && animationSpeed>0 &&
            !float.IsNaN(animationSpeed) && !float.IsInfinity(animationSpeed) &&
            !float.IsNaN(forwardYaw) && !float.IsInfinity(forwardYaw) &&
            !float.IsNaN(weight) && !float.IsInfinity(weight) && weight>0;
    }
}
