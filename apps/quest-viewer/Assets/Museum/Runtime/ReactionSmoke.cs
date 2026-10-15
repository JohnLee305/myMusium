using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FamilyMuseum
{
    public sealed partial class MuseumVisitors
    {
        public IEnumerator RunReactionSmoke()
        {
            simulation=true;gallery=GetComponent<GalleryDisplay>();PrepareRoom();SetCount(0);
            string root=LocalArchive.FindRoot();
            SmokePassed=true;
            foreach(string file in Directory.GetFiles(Path.Combine(root,"contract-valid"),"*.json"))
            {
                bool valid=true;try{ReactionCatalog.Parse(File.ReadAllText(file),"reaction-a");}catch{valid=false;}
                SmokePassed &= valid;Debug.Log("REACTION_CONTRACT_VALID: "+Path.GetFileName(file)+" "+valid);
            }
            foreach(string file in Directory.GetFiles(Path.Combine(root,"contract-invalid"),"*.json"))
            {
                bool rejected=false;try{ReactionCatalog.Parse(File.ReadAllText(file),"reaction-a");}catch{rejected=true;}
                SmokePassed &= rejected;Debug.Log("REACTION_CONTRACT_INVALID: "+Path.GetFileName(file)+" "+rejected);
            }
            var slots=exhibits.Where(e=>e.Loaded).ToArray();SmokePassed &= slots.Length==3;
            foreach(var slot in slots)
            {
                foreach(string persona in ReactionCatalog.Personas)
                {
                    Reactions.Clear();string selected=Reactions.Select(slot.Data,persona);
                    SmokePassed &= selected.StartsWith(slot.Data.title+" · "+persona);
                }
            }
            if(slots.Length>0)
            {
                var data=slots[0].Data;string file=Path.Combine(root,"artifacts",data.id,"reactions.json"),saved=File.ReadAllText(file);
                try
                {
                    Reactions.Clear();var history=new Queue<string>();var one=Reactions.Select(data,"general",history);var two=Reactions.Select(data,"general",history);var three=Reactions.Select(data,"general",history);
                    SmokePassed &= one!=two && three=="";
                    File.WriteAllText(file,saved.Replace("\"approved\"","\"draft\""));Reactions.Clear();SmokePassed &= Reactions.Select(data,"general")==ReactionCatalog.Fallback;
                    File.WriteAllText(file,saved.Replace("\"enabled\": true","\"enabled\": false"));SmokePassed &= Reactions.Select(data,"general")=="";
                    File.WriteAllText(file,"{");SmokePassed &= Reactions.Select(data,"general")==ReactionCatalog.Fallback;
                    File.WriteAllText(file,new string(' ',65537));SmokePassed &= Reactions.Select(data,"general")==ReactionCatalog.Fallback;
                    File.Delete(file);SmokePassed &= Reactions.Select(data,"general")==ReactionCatalog.Fallback;
                    File.WriteAllText(file,saved);string description=data.description;data.description+="변경";SmokePassed &= Reactions.Select(data,"general")==ReactionCatalog.Fallback;data.description=description;
                }
                finally{File.WriteAllText(file,saved);Reactions.Clear();}
                // Walk through the production state transition, rather than assigning a label directly.
                var guest=Spawn(new Vector3(-1.8f,0,-4.1f));guest.Persona="general";
                bool viewed=false;
                for(int n=0;n<1500&&!viewed&&guest.State!=GuestState.Finished;n++)
                {
                    guest.Tick(.1f);viewed=guest.State==GuestState.Viewing;
                    if(n%20==0)yield return null;
                }
                SmokePassed &= viewed && guest.ReactionText.StartsWith(guest.CurrentArtifactTitle+" · general");
                guest.transform.position=new Vector3(.9f,0,.1f);guest.transform.rotation=Quaternion.Euler(0,200,0);
                Camera.main.transform.position=new Vector3(.9f,1.6f,-3.5f);Camera.main.transform.rotation=Quaternion.Euler(0,0,0);
                guest.UpdateThoughtVisibility();yield return null;
                SmokePassed &= guest.ThoughtVisible;
                var text=guest.GetComponentsInChildren<TextMesh>().First(t=>t.gameObject.name=="Visitor thought");
                var bounds=text.GetComponent<Renderer>().bounds;
                bool readable=bounds.size.x>.5f&&bounds.size.x<2.5f&&bounds.size.y>.08f;
                SmokePassed &= readable;Debug.Log("REACTION_TEXT_SIZE_CHECK: "+bounds.size+" passed="+readable);
                string priorText=guest.ReactionText;guest.SetReactionText(new string('가',72));guest.UpdateThoughtVisibility();yield return null;guest.UpdateThoughtVisibility();
                var local=text.GetComponent<Renderer>().localBounds;float textScale=text.transform.localScale.x;
                var background=guest.transform.Find("Visitor reaction bubble/Reaction background");
                bool fitted=local.size.x*textScale<=1.63f&&local.size.y*textScale+.1f<=background.localScale.y;
                SmokePassed &= fitted;Debug.Log("REACTION_LONG_TEXT_CHECK: width="+(local.size.x*textScale)+" height="+(local.size.y*textScale)+" background="+background.localScale+" passed="+fitted);
                guest.SetReactionText(priorText);guest.UpdateThoughtVisibility();
                ShowReactions=false;guest.UpdateThoughtVisibility();SmokePassed &= !guest.ThoughtVisible;ShowReactions=true;
                Debug.Log("REACTION_VIEWING_CHECK: "+guest.CurrentArtifactTitle+" / "+guest.ReactionText+" / passed="+SmokePassed);
            }
            // Verify room transition destroys guests and session selection history is reset.
            if(gallery.RoomCount>1){var transition=gallery.SwitchRoomAsync(1);while(!transition.IsCompleted)yield return null;yield return null;SmokePassed &= !transition.IsFaulted&&ActiveCount==0;transition=gallery.SwitchRoomAsync(0);while(!transition.IsCompleted)yield return null;PrepareRoom();}
            SetCount(6);for(int n=0;n<180;n++){Tick(.1f);if(n%30==0)yield return null;}
            SmokePassed &= ActiveCount>0&&ActiveCount<=6&&guests.All(g=>ReactionCatalog.Personas.Contains(g.Persona));
            ClearRoom();PrepareRoom();SetCount(0);Reactions.Clear();
            // Screenshot preview still reaches Viewing through the real movement state machine.
            var preview=Spawn(new Vector3(-1.8f,0,-4.1f));preview.Persona="story";
            for(int n=0;n<1500&&preview.State!=GuestState.Viewing&&preview.State!=GuestState.Finished;n++){preview.Tick(.1f);if(n%30==0)yield return null;}
            SmokePassed &= preview.State==GuestState.Viewing;
            preview.transform.position=new Vector3(.9f,0,.1f);preview.transform.rotation=Quaternion.Euler(0,200,0);preview.UpdateThoughtVisibility();
            Debug.Log(SmokePassed?"REACTION_SMOKE_PASS":"REACTION_SMOKE_FAIL");
        }
    }
}
