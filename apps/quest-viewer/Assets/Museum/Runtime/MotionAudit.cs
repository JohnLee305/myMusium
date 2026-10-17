using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FamilyMuseum
{
    public sealed partial class MuseumGuest
    {
        public int AuditRoutes {get;private set;}
        public bool AuditWantsMove=>State!=GuestState.Viewing && State!=GuestState.Finished && path!=null && waypoint<path.Count;
        public bool AuditYield=>tourYielding;
        public void AuditRoute()=>AuditRoutes++;
    }
    public sealed partial class MuseumVisitors
    {
        bool auditing;
        string auditFolder;
        float auditStart,auditNext;
        float auditDuration=120;
        readonly List<float> auditFrames=new List<float>(),auditCosts=new List<float>();
        readonly Dictionary<int,MotionSample> auditGuests=new Dictionary<int,MotionSample>();
        sealed class MotionSample
        {
            public Vector3 Previous,Direction;
            public bool Moving;
            public int Frames,Stops,Turns,Routes,StillFrames,WantsFrames;
            public float Distance,StillTime,LongestStill;
        }
        void BeginAudit()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-museum-motion-audit");
            if(i<0 || i+1>=args.Length)return;
            auditFolder=Path.GetFullPath(args[i+1]);Directory.CreateDirectory(auditFolder);
            int durationArg=Array.IndexOf(args,"-museum-motion-seconds");
            if(durationArg>=0 && durationArg+1<args.Length && float.TryParse(args[durationArg+1],out float seconds) && seconds>=30 && seconds<=120)auditDuration=seconds;
            auditStart=Time.realtimeSinceStartup;auditing=true;
            Application.runInBackground=true;
            File.WriteAllText(Path.Combine(auditFolder,"motion.csv"),"seconds,active,views,exited,yields,plans,frameMs,tickMs\n");
        }
        void AuditMotion(float tickMs)
        {
            if(!auditing)return;
            auditFrames.Add(Time.unscaledDeltaTime*1000);auditCosts.Add(tickMs);
            foreach(var guest in guests)
            {
                int id=guest.Sequence;
                if(!auditGuests.TryGetValue(id,out var s))
                {s=new MotionSample{Previous=guest.transform.position};auditGuests.Add(id,s);}
                var delta=guest.transform.position-s.Previous;float distance=delta.magnitude;
                bool moving=distance>.0001f;
                if(s.Moving && !moving)s.Stops++;
                if(moving && s.Direction.sqrMagnitude>.0001f && Vector3.Angle(s.Direction,delta)>90)s.Turns++;
                if(moving)s.Direction=delta.normalized;
                if(guest.AuditWantsMove)
                {
                    s.WantsFrames++;
                    if(!moving){s.StillFrames++;s.StillTime+=Time.unscaledDeltaTime;}
                    else s.StillTime=0;
                }
                else s.StillTime=0;
                s.LongestStill=Mathf.Max(s.LongestStill,s.StillTime);
                s.Previous=guest.transform.position;s.Moving=moving;s.Distance+=distance;s.Frames++;s.Routes=guest.AuditRoutes;
            }
            float elapsed=Time.realtimeSinceStartup-auditStart;
            if(elapsed>=auditNext)
            {
                auditNext=elapsed+1;
                File.AppendAllText(Path.Combine(auditFolder,"motion.csv"),FormattableString.Invariant($"{elapsed:F2},{guests.Count},{Views},{Exited},{QueueYields},{QueueWaitingPlans},{Time.unscaledDeltaTime*1000:F2},{tickMs:F2}\n"));
            }
            if(elapsed<auditDuration)return;
            float Percentile(List<float> values,float fraction){values.Sort();return values[Mathf.Min(values.Count-1,(int)(values.Count*fraction))];}
            var output=new List<string>{FormattableString.Invariant($"seconds={elapsed:F1} frames={auditFrames.Count} frameP50={Percentile(auditFrames,.5f):F2} frameP95={Percentile(auditFrames,.95f):F2} frameMax={auditFrames.Last():F2} tickP95={Percentile(auditCosts,.95f):F2} tickMax={auditCosts.Last():F2} entered={Entered} views={Views} exited={Exited} yields={QueueYields} plans={QueueWaitingPlans}")};
            foreach(var pair in auditGuests)
            {var s=pair.Value;output.Add(FormattableString.Invariant($"guest={pair.Key} meters={s.Distance:F2} routes={s.Routes} stops={s.Stops} turns90={s.Turns} wantsFrames={s.WantsFrames} stillFrames={s.StillFrames} longestStill={s.LongestStill:F2}"));}
            foreach(var guest in guests)output.Add(guest.TourStatus);
            File.WriteAllLines(Path.Combine(auditFolder,"summary.txt"),output);
            Debug.Log("MOTION_AUDIT_COMPLETE: "+auditFolder);auditing=false;Application.Quit(0);
        }
    }
}
