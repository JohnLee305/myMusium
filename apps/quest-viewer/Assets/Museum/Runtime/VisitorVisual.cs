using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FamilyMuseum
{
    // Each appearance supplies its own matching rig and clips. Behavior never names bones or imported states.
    public sealed class VisitorVisual : MonoBehaviour
    {
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        float blend;
        public VisitorAppearance Appearance { get; private set; }
        public Renderer[] Renderers { get; private set; }
        public bool Initialize(VisitorAppearance appearance)
        {
            if(appearance==null || !appearance.IsUsable) return false;
            Appearance=appearance;
            var alignment=new GameObject("Appearance alignment").transform;
            alignment.SetParent(transform,false);
            var model=Instantiate(appearance.prefab,alignment,false);
            model.name="Visitor appearance: "+appearance.id;
            Renderers=model.GetComponentsInChildren<Renderer>(true);
            bool any=false; Bounds bounds=default;
            foreach(var renderer in Renderers)
            {
                if(!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if(!any) { bounds=renderer.bounds; any=true; } else bounds.Encapsulate(renderer.bounds);
                if(renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen=true;
            }
            if(!any || bounds.size.y<.01f) { Destroy(model); return false; }
            alignment.localRotation=Quaternion.Euler(0,appearance.forwardYaw,0);
            foreach(var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
            var animator=model.GetComponentInChildren<Animator>(true);
            animator.runtimeAnimatorController=null; animator.applyRootMotion=false;
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            graph=PlayableGraph.Create("Visitor "+appearance.id);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer=AnimationMixerPlayable.Create(graph,2);
            var idle=AnimationClipPlayable.Create(graph,appearance.idle);
            var walk=AnimationClipPlayable.Create(graph,appearance.walk);
            idle.SetApplyFootIK(false); walk.SetApplyFootIK(false);
            idle.SetSpeed(appearance.animationSpeed); walk.SetSpeed(appearance.animationSpeed);
            graph.Connect(idle,0,mixer,0); graph.Connect(walk,0,mixer,1);
            mixer.SetInputWeight(0,1); mixer.SetInputWeight(1,0);
            AnimationPlayableOutput.Create(graph,"Appearance",animator).SetSourcePlayable(mixer);
            graph.Play(); graph.Evaluate(0);
            // Imported master rigs can start at an authored offset. Align the evaluated pose, rather than the import bind pose.
            bounds=PoseBounds;
            if(bounds.size.y<.01f){Close();Destroy(model);return false;}
            float scale=appearance.height/bounds.size.y;
            alignment.localScale=Vector3.one*scale;
            alignment.localPosition+=transform.InverseTransformVector(new Vector3(transform.position.x-bounds.center.x,transform.position.y-bounds.min.y,transform.position.z-bounds.center.z))*scale;
            return true;
        }
        public Bounds PoseBounds
        {
            get
            {
                Bounds bounds=default;bool first=true;
                foreach(var renderer in Renderers)
                {
                    if(!(renderer is SkinnedMeshRenderer skin))continue;
                    var mesh=new Mesh();skin.BakeMesh(mesh,true);
                    foreach(var vertex in mesh.vertices){var point=skin.transform.TransformPoint(vertex);if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);}
                    Destroy(mesh);
                }
                return bounds;
            }
        }
        public void Tick(bool moving,float dt,float movementSpeed=1)
        {
            TickLocomotion(moving?1:0,dt,movementSpeed);
        }
        public void TickLocomotion(float speedRatio,float dt,float movementSpeed=1)
        {
            if(!graph.IsValid() || !gameObject.activeInHierarchy) return;
            speedRatio=Mathf.Clamp01(speedRatio);
            blend=Mathf.MoveTowards(blend,speedRatio,dt*4);
            mixer.SetInputWeight(0,1-blend); mixer.SetInputWeight(1,blend);
            mixer.GetInput(1).SetSpeed(Appearance.animationSpeed*movementSpeed*Mathf.Max(.15f,speedRatio));
            graph.Evaluate(dt);
        }
        public Bounds Bounds
        {
            get
            {
                Bounds bounds=default; bool first=true;
                if(Renderers!=null) foreach(var renderer in Renderers)
                { if(first) { bounds=renderer.bounds; first=false; } else bounds.Encapsulate(renderer.bounds); }
                return bounds;
            }
        }
        public void Close() { if(graph.IsValid()) graph.Destroy(); }
        void OnDestroy() { Close(); }
    }
}
