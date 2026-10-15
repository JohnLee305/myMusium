using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace FamilyMuseum
{
    public sealed class VRExhibitInteraction : MonoBehaviour
    {
        public struct HandInput
        {
            public Vector3 position;
            public Quaternion rotation;
            public bool tracked, grip, reset, putBack;
        }
        sealed class Hand
        {
            public Transform transform;
            public HandInput input, previous;
            public ArtifactDisplay focus;
            public ExhibitReplica outline;
            public LineRenderer ray;
            public TextMesh label;
        }
        readonly Hand[] hands = { new Hand(), new Hand() };
        readonly List<Renderer> hidden = new List<Renderer>();
        MuseumSession session;
        GalleryDisplay gallery;
        Material outlineMaterial, lineMaterial, labelBackground;
        Font font;
        GameObject visuals;
        ExhibitReplica held;
        ArtifactDisplay source;
        LineRenderer originMarker;
        int owner=-1, second=-1;
        Vector3 offset, twoVector, twoMid, twoPosition;
        Quaternion rotationOffset, twoRotation;
        float defaultScale, twoScale, returnTime=-1, pullUntil;
        Vector3 returnPosition, returnScale;
        Quaternion returnRotation;
        bool simulation;
        public bool Busy => held != null;
        public bool SmokePassed { get; private set; }
        public float SizeMultiplier => held == null ? 1 : held.transform.localScale.x/defaultScale;

        void Awake()
        {
            session=GetComponent<MuseumSession>();
            outlineMaterial=new Material(Resources.Load<Shader>("FocusOutline"));
            lineMaterial=new Material(Resources.Load<Shader>("WorldText"));
            labelBackground=new Material(Resources.Load<Shader>("WorldText"));
            labelBackground.SetColor("_Color",new Color(.025f,.045f,.055f,.92f));
            // Use the same depth-aware, stereo-compatible world text shader as the gallery.
            font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},48);
            font.material.shader=Resources.Load<Shader>("WorldText");
            visuals=new GameObject("VR exhibit controls");
            for(int i=0;i<2;i++)
            {
                hands[i].transform=session.xrRig.transform.Find("Camera offset/"+(i==0?"LeftHand":"RightHand"));
                hands[i].ray=MakeLine("Controller pointer",.002f);
                var label=new GameObject("Grab instructions"); label.transform.SetParent(visuals.transform,false);
                hands[i].label=label.AddComponent<TextMesh>();
                var text=hands[i].label; text.font=font; text.fontSize=48; text.characterSize=.003f;
                text.anchor=TextAnchor.MiddleCenter; text.alignment=TextAlignment.Center;
                text.GetComponent<Renderer>().sharedMaterial=font.material;
                var background=GameObject.CreatePrimitive(PrimitiveType.Quad);
                background.name="Instruction background"; background.transform.SetParent(label.transform,false);
                background.transform.localPosition=new Vector3(0,0,.002f);
                background.transform.localScale=new Vector3(.42f,.065f,1);
                Destroy(background.GetComponent<Collider>());
                background.GetComponent<Renderer>().sharedMaterial=labelBackground;
            }
            originMarker=MakeLine("Exhibit return location",.006f);
            visuals.SetActive(false);
        }
        LineRenderer MakeLine(string name,float width)
        {
            var obj=new GameObject(name); obj.transform.SetParent(visuals.transform,false);
            var line=obj.AddComponent<LineRenderer>(); line.sharedMaterial=lineMaterial;
            line.useWorldSpace=true; line.startWidth=width; line.endWidth=width;
            line.startColor=line.endColor=new Color(.2f,.9f,1f); line.positionCount=0;
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }
        void LateUpdate()
        {
            if(simulation) return;
            gallery ??=GetComponent<GalleryDisplay>();
            if(!session.xrRig.activeInHierarchy || gallery==null || !gallery.IsFinished || gallery.IsLoading)
            { Cancel(); visuals.SetActive(false); return; }
            visuals.SetActive(true);
            Step(ReadHand(0),ReadHand(1),Time.unscaledDeltaTime);
        }
        HandInput ReadHand(int i)
        {
            var device=InputDevices.GetDeviceAtXRNode(i==0?XRNode.LeftHand:XRNode.RightHand);
            var pose=hands[i].transform;
            var input=new HandInput { position=pose.position,rotation=pose.rotation };
            input.tracked=device.isValid && device.TryGetFeatureValue(CommonUsages.isTracked,out bool tracked) && tracked;
            device.TryGetFeatureValue(CommonUsages.gripButton,out input.grip);
            device.TryGetFeatureValue(CommonUsages.primaryButton,out input.reset);
            device.TryGetFeatureValue(CommonUsages.secondaryButton,out input.putBack);
            return input;
        }
        void Pulse(int i)
        {
            if(simulation) return;
            var device=InputDevices.GetDeviceAtXRNode(i==0?XRNode.LeftHand:XRNode.RightHand);
            if(device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse) device.SendHapticImpulse(0,.18f,.035f);
        }
        void Step(HandInput left,HandInput right,float dt)
        {
            hands[0].input=left; hands[1].input=right;
            if(held!=null && (source==null || owner>=0 && !hands[owner].input.tracked || second>=0 && !hands[second].input.tracked)) Cancel();
            if(held!=null && returnTime>=0)
            {
                returnTime+=dt;
                if(returnTime>=.7f) Cancel();
                else if(returnTime>.25f)
                {
                    float t=Mathf.SmoothStep(0,1,(returnTime-.25f)/.45f);
                    held.transform.position=Vector3.Lerp(returnPosition,held.SourceBounds.center,t);
                    held.transform.rotation=Quaternion.Slerp(returnRotation,Quaternion.identity,t);
                    held.transform.localScale=Vector3.Lerp(returnScale,Vector3.one,t);
                }
            }
            for(int i=0;i<2;i++)
            {
                var hand=hands[i]; var input=hand.input;
                FindFocus(i);
                if(!input.tracked) continue;
                if(held!=null && input.putBack && !hand.previous.putBack) BeginReturn();
                if(held!=null && returnTime<0 && input.reset && !hand.previous.reset)
                {
                    held.transform.localScale=Vector3.one*defaultScale;
                    held.transform.rotation=Quaternion.identity;
                    Rebase();
                }
                if(input.grip && !hand.previous.grip && returnTime<0)
                {
                    if(held==null && hand.focus!=null) Grab(hand.focus,i);
                    else if(held!=null && i!=owner && second<0 && HitsHeld(input)) { second=i; Rebase(); Pulse(i); }
                }
                if(!input.grip && hand.previous.grip && held!=null && returnTime<0)
                {
                    if(i==second) { second=-1; Rebase(); }
                    else if(i==owner)
                    {
                        if(second>=0 && hands[second].input.grip) { owner=second; second=-1; Rebase(); }
                        else BeginReturn();
                    }
                }
            }
            if(held!=null && returnTime<0)
            {
                if(second>=0)
                {
                    Vector3 span=hands[second].input.position-hands[owner].input.position;
                    float multiplier=Mathf.Clamp(twoScale*span.magnitude/Mathf.Max(.0001f,twoVector.magnitude)/defaultScale,.25f,3f);
                    held.transform.localScale=Vector3.one*(defaultScale*multiplier);
                    var turn=span.sqrMagnitude>.0025f?Quaternion.FromToRotation(twoVector,span):Quaternion.identity;
                    held.transform.rotation=turn*twoRotation;
                    held.transform.position=(hands[owner].input.position+hands[second].input.position)*.5f + turn*(twoPosition-twoMid);
                }
                else
                {
                    var input=hands[owner].input;
                    Vector3 target=input.position+input.rotation*offset;
                    Quaternion rotation=input.rotation*rotationOffset;
                    float blend=Time.unscaledTime<pullUntil?1-Mathf.Exp(-12f*dt):1;
                    held.transform.position=Vector3.Lerp(held.transform.position,target,blend);
                    held.transform.rotation=Quaternion.Slerp(held.transform.rotation,rotation,blend);
                }
            }
            for(int i=0;i<2;i++) { DrawHand(i); hands[i].previous=hands[i].input; }
        }
        void FindFocus(int i)
        {
            var hand=hands[i]; ArtifactDisplay found=null;
            if(hand.input.tracked && held==null)
            {
                float distance=.14f;
                foreach(var collider in Physics.OverlapSphere(hand.input.position,.14f,~0,QueryTriggerInteraction.Collide))
                {
                    var slot=ModelHit(collider);
                    float d=Vector3.Distance(collider.ClosestPoint(hand.input.position),hand.input.position);
                    if(slot!=null && d<=distance) { found=slot; distance=d; }
                }
                if(found==null && Physics.Raycast(hand.input.position,hand.input.rotation*Vector3.forward,out var hit,10f,~0,QueryTriggerInteraction.Collide)) found=ModelHit(hit.collider);
            }
            if(hand.focus==found) return;
            if(hand.outline!=null) hand.outline.Release();
            hand.outline=null; hand.focus=found;
            if(found!=null) { hand.outline=ExhibitReplica.Create(found,outlineMaterial); Pulse(i); }
        }
        static ArtifactDisplay ModelHit(Collider collider)
        {
            var slot=collider.GetComponentInParent<ArtifactDisplay>();
            return slot!=null && slot.managedSlot && slot.Loaded && collider.transform.IsChildOf(slot.modelSlot)?slot:null;
        }
        bool HitsHeld(HandInput input)
        {
            if(held==null) return false;
            Bounds bounds=default; bool first=true;
            foreach(var renderer in held.GetComponentsInChildren<Renderer>())
            { if(first) { bounds=renderer.bounds; first=false; } else bounds.Encapsulate(renderer.bounds); }
            bounds.Expand(.12f);
            if(bounds.Contains(input.position)) return true;
            if(!bounds.IntersectRay(new Ray(input.position,input.rotation*Vector3.forward),out float distance) || distance>10) return false;
            return !Physics.Raycast(input.position,input.rotation*Vector3.forward,distance,~0,QueryTriggerInteraction.Collide);
        }
        bool Grab(ArtifactDisplay slot,int i)
        {
            held=ExhibitReplica.Create(slot);
            if(held==null) return false;
            source=slot; owner=i; second=-1; returnTime=-1;
            foreach(var hand in hands) { if(hand.outline!=null) hand.outline.Release(); hand.outline=null; hand.focus=null; }
            foreach(var renderer in source.modelSlot.GetComponentsInChildren<Renderer>())
                if(renderer.enabled) { hidden.Add(renderer); renderer.enabled=false; }
            Vector3 size=held.SourceBounds.size;
            defaultScale=.28f/Mathf.Max(size.x,size.y,size.z);
            held.transform.localScale=Vector3.one*defaultScale;
            offset=new Vector3(0,0,.22f); rotationOffset=Quaternion.Inverse(hands[i].input.rotation);
            pullUntil=Time.unscaledTime+.5f;
            Vector3 p=held.SourceBounds.center;
            originMarker.positionCount=5;
            for(int n=0;n<5;n++) { float a=(n%4)*Mathf.PI*.5f+Mathf.PI*.25f; originMarker.SetPosition(n,p+new Vector3(Mathf.Cos(a)*.12f,-size.y*.5f+.005f,Mathf.Sin(a)*.12f)); }
            Pulse(i); return true;
        }
        void Rebase()
        {
            if(held==null || owner<0) return;
            pullUntil=0;
            var input=hands[owner].input;
            offset=Quaternion.Inverse(input.rotation)*(held.transform.position-input.position);
            rotationOffset=Quaternion.Inverse(input.rotation)*held.transform.rotation;
            if(second>=0)
            {
                twoVector=hands[second].input.position-input.position;
                if(twoVector.magnitude<.0001f) twoVector=input.rotation*Vector3.right*.0001f;
                twoMid=(input.position+hands[second].input.position)*.5f;
                twoPosition=held.transform.position; twoRotation=held.transform.rotation; twoScale=held.transform.localScale.x;
            }
        }
        void BeginReturn()
        {
            if(held==null || returnTime>=0) return;
            returnTime=0; owner=second=-1;
            returnPosition=held.transform.position; returnRotation=held.transform.rotation; returnScale=held.transform.localScale;
        }
        void DrawHand(int i)
        {
            var hand=hands[i]; var input=hand.input;
            hand.label.gameObject.SetActive(input.tracked && (held==null || owner==i || returnTime>=0 && i==0));
            hand.ray.positionCount=input.tracked?2:0;
            if(!input.tracked) return;
            Vector3 end=input.position+input.rotation*Vector3.forward*3;
            if(Physics.Raycast(input.position,input.rotation*Vector3.forward,out var hit,10,~0,QueryTriggerInteraction.Collide)) end=hit.point;
            hand.ray.SetPosition(0,input.position); hand.ray.SetPosition(1,end);
            if(held!=null && owner==i) hand.ray.positionCount=0;
            hand.label.text=held!=null ? (returnTime>=0?"전시대로 돌아가는 중": "양손으로 크기 조절\nA/X 초기화 · B/Y 돌려놓기") :
                hand.focus!=null ? ShortTitle(hand.focus.Data.title)+"\n옆면 그립을 눌러 살펴보세요" : "전시품을 가리키고 옆면 그립을 눌러 주세요";
            hand.label.transform.position=input.position+Vector3.down*.15f;
            var camera=Camera.main;
            if(camera!=null) hand.label.transform.rotation=Quaternion.LookRotation(hand.label.transform.position-camera.transform.position);
        }
        static string ShortTitle(string title) => title.Length>22?title.Substring(0,22)+"…":title;
        public void Cancel()
        {
            if(held!=null) held.Release(); held=null; source=null;
            foreach(var renderer in hidden) if(renderer!=null) renderer.enabled=true;
            hidden.Clear(); owner=second=-1; returnTime=-1;
            if(originMarker!=null) originMarker.positionCount=0;
            foreach(var hand in hands) { if(hand.outline!=null) hand.outline.Release(); hand.outline=null; hand.focus=null; }
        }
        void OnApplicationFocus(bool focus) { if(!focus && !simulation) Cancel(); }
        void OnApplicationPause(bool pause) { if(pause) Cancel(); }
        void OnDisable() { Cancel(); }
        void OnDestroy()
        {
            Cancel(); if(visuals!=null) Destroy(visuals);
            if(outlineMaterial!=null) Destroy(outlineMaterial); if(lineMaterial!=null) Destroy(lineMaterial);
            if(labelBackground!=null) Destroy(labelBackground);
            if(font!=null) { Destroy(font.material); Destroy(font); }
        }

        public IEnumerator RunSmoke()
        {
            simulation=true; visuals.SetActive(true); SmokePassed=outlineMaterial.shader.isSupported;
            gallery=GetComponent<GalleryDisplay>();
            Physics.SyncTransforms();
            foreach(var slot in FindObjectsByType<ArtifactDisplay>(FindObjectsSortMode.None))
            {
                if(!slot.managedSlot) continue;
                if(!slot.Loaded) { SmokePassed &= ExhibitReplica.Create(slot)==null; continue; }
                var position=slot.modelSlot.position; var rotation=slot.modelSlot.rotation; var scale=slot.modelSlot.localScale;
                int imports=ArtifactDisplay.ActiveImports;
                var collider=slot.modelSlot.GetComponent<BoxCollider>();
                var rendererStates=new Dictionary<Renderer,bool>();
                foreach(var renderer in slot.modelSlot.GetComponentsInChildren<Renderer>()) rendererStates.Add(renderer,renderer.enabled);
                foreach(var part in slot.GetComponentsInChildren<Collider>())
                    if(!part.transform.IsChildOf(slot.modelSlot)) SmokePassed &= ModelHit(part)==null;
                Vector3 center=collider.bounds.center;
                var left=new HandInput { tracked=true,position=center-Vector3.forward*2,rotation=Quaternion.identity };
                var right=new HandInput { tracked=true,position=left.position+Vector3.right*.3f,rotation=Quaternion.identity };
                var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);
                blocker.transform.position=left.position+Vector3.forward*.5f;
                blocker.transform.localScale=Vector3.one*.3f; Physics.SyncTransforms();
                Step(left,right,.016f); SmokePassed &= hands[0].focus==null;
                blocker.SetActive(false); Destroy(blocker); Physics.SyncTransforms();
                Step(left,right,.016f); SmokePassed &= hands[0].focus==slot && hands[0].outline!=null;
                var args=System.Environment.GetCommandLineArgs();
                if(System.Array.IndexOf(args,"-museum-vr-interaction-preview")>=0)
                {
                    int pathIndex=System.Array.IndexOf(args,"-museum-screenshot");
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(System.IO.Path.ChangeExtension(args[pathIndex+1],"focus.png"));
                    yield return null;
                }
                left.grip=true; Step(left,right,.016f); SmokePassed &= Busy && hidden.Count>0;
                if(!Busy) { SmokePassed=false; continue; }
                pullUntil=0; Step(left,right,.016f);
                SmokePassed &= Vector3.Distance(held.transform.position,left.position+Vector3.forward*.22f)<.001f;
                right.position=held.transform.position+Vector3.right*.03f;
                right.grip=true; Step(left,right,.016f); SmokePassed &= second==1;
                right.position=left.position+(right.position-left.position)*20; Step(left,right,.016f);
                SmokePassed &= Mathf.Abs(SizeMultiplier-3)<.001f;
                right.position=left.position+Vector3.right*.001f; Step(left,right,.016f);
                SmokePassed &= Mathf.Abs(SizeMultiplier-.25f)<.001f;
                right.reset=true; Step(left,right,.016f); SmokePassed &= Mathf.Abs(SizeMultiplier-1)<.001f;
                var before=held.transform.position; left.grip=false; Step(left,right,.016f);
                SmokePassed &= owner==1 && second==-1 && Vector3.Distance(before,held.transform.position)<.001f;
                right.rotation=Quaternion.Euler(20,40,0); Step(left,right,.016f);
                SmokePassed &= Quaternion.Angle(held.transform.rotation,Quaternion.identity)>1;
                right.grip=false; Step(left,right,.016f); Step(left,right,.8f);
                SmokePassed &= !Busy && hidden.Count==0;
                // Near grab, explicit return, and tracking loss must restore original renderers.
                left.position=center; left.grip=false; right.reset=false; Step(left,right,.016f);
                left.grip=true; Step(left,right,.016f); SmokePassed &= Busy;
                left.putBack=true; Step(left,right,.016f); Step(left,right,.8f); SmokePassed &= !Busy;
                left.grip=false; left.putBack=false; Step(left,right,.016f);
                left.grip=true; Step(left,right,.016f); SmokePassed &= Busy;
                left.tracked=false; Step(left,right,.016f); SmokePassed &= !Busy;
                SmokePassed &= slot.modelSlot.position==position && slot.modelSlot.rotation==rotation && slot.modelSlot.localScale==scale && ArtifactDisplay.ActiveImports==imports;
                foreach(var pair in rendererStates) SmokePassed &= pair.Key.enabled==pair.Value;
                Cancel(); yield return null;
                SmokePassed &= FindObjectsByType<ExhibitReplica>(FindObjectsSortMode.None).Length==0;
                Debug.Log("VR_INTERACTION_CHECK: "+slot.Data.id+" passed="+SmokePassed);
            }
            if(gallery.RoomCount>1)
            {
                foreach(var slot in FindObjectsByType<ArtifactDisplay>(FindObjectsSortMode.None))
                    if(slot.managedSlot && slot.Loaded) { Grab(slot,0); break; }
                var change=gallery.SwitchRoomAsync((gallery.CurrentRoom+1)%gallery.RoomCount);
                while(!change.IsCompleted) yield return null;
                yield return null;
                SmokePassed &= !change.IsFaulted && !Busy && FindObjectsByType<ExhibitReplica>(FindObjectsSortMode.None).Length==0;
                change=gallery.SwitchRoomAsync(0); while(!change.IsCompleted) yield return null;
                SmokePassed &= !change.IsFaulted;
            }
            Cancel(); visuals.SetActive(false); simulation=false;
            Debug.Log(SmokePassed?"VR_INTERACTION_SMOKE_PASS":"VR_INTERACTION_SMOKE_FAIL");
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-museum-vr-interaction-preview")>=0)
            {
                // Screenshot-only virtual hands: no OpenXR hardware or tracking is claimed by this preview.
                simulation=true; visuals.SetActive(true);
                var camera=Camera.main;
                foreach(var slot in FindObjectsByType<ArtifactDisplay>(FindObjectsSortMode.None))
                {
                    if(!slot.managedSlot || !slot.Loaded) continue;
                    hands[0].input=new HandInput {tracked=true,grip=true,position=camera.transform.TransformPoint(new Vector3(-.12f,-.12f,.65f)),rotation=camera.transform.rotation};
                    Grab(slot,0); pullUntil=0;
                    Step(hands[0].input,default,.016f);
                    held.transform.localScale*=1.5f;
                    held.transform.rotation=Quaternion.Euler(15,35,0);
                    break;
                }
            }
        }
    }
}
