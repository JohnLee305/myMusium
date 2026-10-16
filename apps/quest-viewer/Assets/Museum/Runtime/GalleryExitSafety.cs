using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace FamilyMuseum
{
    public sealed partial class GalleryDisplay
    {
        public bool IsOutside {get;private set;}
        bool completedTour,boundarySmoke;
        public bool BoundarySmokePassed {get;private set;}
        static bool CrossedDoor(Vector3 position,MuseumDoor door)
        {
            var inward=TourNavigation.Inward(door);var delta=position-TourNavigation.Door(door);delta.y=0;
            return Vector3.Dot(delta,inward)<.15f&&Mathf.Abs(Vector3.Dot(delta,Vector3.Cross(Vector3.up,inward)))<1f;
        }
        void CheckDoorTransition(Vector3 position)
        {
            if(IsOutside||IsLoading||!IsFinished||Time.unscaledTime<portalReadyAt)return;
            var nav=TourNavigation.Get(rooms[CurrentRoom]);
            if(CrossedDoor(position,nav.entrance))
            {
                if(CurrentRoom>0)_=SwitchRoomAsync(CurrentRoom-1);else ShowOutside(false);
            }
            else if(CrossedDoor(position,nav.exit))
            {
                if(CurrentRoom+1<RoomCount)_=SwitchRoomAsync(CurrentRoom+1);else ShowOutside(true);
            }
        }
        void ShowOutside(bool completed)
        {
            IsOutside=true;completedTour=completed;
            GetComponent<MuseumSession>().desktopRig.GetComponent<DesktopNavigator>().enabled=false;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            Debug.Log(completed?"MUSEUM_EXIT_REACHED":"MUSEUM_ENTRANCE_OUTSIDE");
        }
        public void ReturnInside()
        {
            IsOutside=false;ResetVisitor();portalReadyAt=Time.unscaledTime+2;
            GetComponent<MuseumSession>().desktopRig.GetComponent<DesktopNavigator>().enabled=true;
        }
        async Task EnterMuseumAsync()
        {
            IsOutside=false;
            if(CurrentRoom!=0)await SwitchRoomAsync(0);
            ReturnInside();
        }
        static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
        bool SafeFootprint(Vector3 p)
        {
            if(p.x>=-8.55f&&p.x<=8.55f&&p.z>=-5.35f&&p.z<=14.35f)return true;
            var nav=TourNavigation.Get(rooms[CurrentRoom]);
            foreach(var door in new[]{nav.entrance,nav.exit})
            {
                var inward=TourNavigation.Inward(door);var delta=p-TourNavigation.Door(door);
                float forward=Vector3.Dot(delta,inward),side=Vector3.Dot(delta,Vector3.Cross(Vector3.up,inward));
                if(forward>=-3.8f&&forward<=-.25f&&Mathf.Abs(side)<=1.45f)return true;
            }
            return false;
        }
        bool RecoverUnsafePlayer()
        {
            if(CurrentRoom<0||!IsFinished||IsLoading)return false;
            var session=GetComponent<MuseumSession>();
            var p=session.desktopRig.activeInHierarchy?session.desktopRig.transform.position:Camera.main==null?Vector3.zero:Camera.main.transform.position;
            if(Finite(p.x)&&Finite(p.y)&&Finite(p.z)&&p.y>=-2&&SafeFootprint(p))return false;
            GetComponent<VRExhibitInteraction>()?.Cancel();ReturnInside();
            Debug.Log("PLAYER_BOUNDARY_RECOVERED");return true;
        }
        void LateUpdate(){RecoverUnsafePlayer();}
        void DrawExitPanel()
        {
            var old=GUI.color;GUI.color=new Color(.025f,.055f,.045f,.93f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=old;
            var title=new GUIStyle(GUI.skin.label){font=font,fontSize=28,alignment=TextAnchor.MiddleCenter};
            var body=new GUIStyle(title){fontSize=17,wordWrap=true};var button=new GUIStyle(GUI.skin.button){font=font,fontSize=17};
            float x=(Screen.width-560)/2f,y=(Screen.height-280)/2f;
            GUI.Label(new Rect(x,y,560,60),completedTour?"관람을 마쳤습니다":"박물관 입구",title);
            GUI.Label(new Rect(x,y+65,560,65),completedTour?"함께 둘러봐 주셔서 감사합니다.\n처음부터 다시 관람하거나 전시실로 돌아갈 수 있습니다.":"입장하면 첫 전시실부터 차례대로 둘러볼 수 있습니다.",body);
            if(GUI.Button(new Rect(x+40,y+160,230,52),completedTour?"처음부터 다시 관람":"박물관 입장",button))_=EnterMuseumAsync();
            if(GUI.Button(new Rect(x+290,y+160,230,52),"전시실로 돌아가기",button))ReturnInside();
        }
        public IEnumerator RunBoundarySmoke()
        {
            boundarySmoke=true;BoundarySmokePassed=true;
            GetComponent<MuseumVisitors>().SetCount(0);
            var session=GetComponent<MuseumSession>();var rig=session.desktopRig;var controller=rig.GetComponent<CharacterController>();var navigator=rig.GetComponent<DesktopNavigator>();
            for(int room=0;room<RoomCount;room++)foreach(bool exit in new[]{false,true})
            {
                IsOutside=false;var task=SwitchRoomAsync(room);while(!task.IsCompleted)yield return null;yield return null;
                var nav=TourNavigation.Get(rooms[room]);var door=exit?nav.exit:nav.entrance;var inward=TourNavigation.Inward(door);
                navigator.enabled=false;controller.enabled=false;rig.transform.position=TourNavigation.Door(door)+inward*.4f+Vector3.up*.05f;controller.enabled=true;Physics.SyncTransforms();
                portalReadyAt=Time.unscaledTime+100;
                bool supported=true;
                for(int i=0;i<60;i++){controller.Move(-inward*.1f+Vector3.down*.035f);supported &= rig.transform.position.y>=-.2f;if(i%10==0)yield return null;}
                CheckDoorTransition(rig.transform.position);BoundarySmokePassed &= supported&&!IsOutside&&!IsLoading;
                portalReadyAt=0;CheckDoorTransition(rig.transform.position);
                bool exterior=exit?room==RoomCount-1:room==0;
                float deadline=Time.realtimeSinceStartup+30;while(IsLoading&&Time.realtimeSinceStartup<deadline)yield return null;
                bool correct=exterior?IsOutside:!IsOutside&&CurrentRoom==(exit?room+1:room-1);
                BoundarySmokePassed &= correct;
                Debug.Log($"EXIT_BOUNDARY_CHECK: room={room} side={door.side} exit={exit} supported={supported} destination={correct}");
                ReturnInside();navigator.enabled=false;controller.enabled=false;rig.transform.position=new Vector3(100,-50,100);controller.enabled=true;
                BoundarySmokePassed &= RecoverUnsafePlayer()&&rig.transform.position.y>=0&&SafeFootprint(rig.transform.position);
                yield return null;
            }
            var first=EnterMuseumAsync();while(!first.IsCompleted)yield return null;
            BoundarySmokePassed &= CurrentRoom==0&&!IsOutside;
            Debug.Log(BoundarySmokePassed?"EXIT_BOUNDARY_SMOKE_PASS":"EXIT_BOUNDARY_SMOKE_FAIL");
            var last=SwitchRoomAsync(RoomCount-1);while(!last.IsCompleted)yield return null;
            ShowOutside(true); // Leave a stable completion panel for screenshot verification.
            if(RoomCount==1){var door=TourNavigation.Get(rooms[0]).exit;var target=TourNavigation.Door(door);controller.enabled=false;rig.transform.position=target+TourNavigation.Inward(door)*.05f+Vector3.up*.05f;controller.enabled=true;Physics.SyncTransforms();var probe=new GameObject("Exit traffic probe").AddComponent<MuseumGuest>();probe.transform.position=target+TourNavigation.Inward(door);bool clear=GetComponent<MuseumVisitors>().CanMove(probe,target);Destroy(probe.gameObject);BoundarySmokePassed &= clear;Debug.Log("EXIT_TRAFFIC_CHECK: passed="+clear);Debug.Log(BoundarySmokePassed?"EXIT_BOUNDARY_FINAL_PASS":"EXIT_BOUNDARY_FINAL_FAIL");}
        }
    }
}
