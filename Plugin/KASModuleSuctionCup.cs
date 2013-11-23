using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KAS
{
    public class KASModuleSuctionCup : KASModuleAttachCore
    {
        [KSPField]
        public float breakForce = 10;
        [KSPField]
        public string attachSndPath = "KAS/Sounds/attach";
        [KSPField]
        public string detachSndPath = "KAS/Sounds/detach";
        public FXGroup fxSndAttach, fxSndDetach;

        //Info
        [KSPField(guiActive = true, guiName = "State", guiFormat = "S")]
        public string state = "Off";

        public override string GetInfo()
        {
            string info = base.GetInfo();
            info += "---- Suction Cup ----";
            info += "\n";
            info += "Break Force : " + breakForce;
            return info;
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (state == StartState.Editor || state == StartState.None) return;
            KAS_Shared.createFXSound(this.part, fxSndAttach, attachSndPath, false);
            KAS_Shared.createFXSound(this.part, fxSndDetach, detachSndPath, false);
        }

        public void OnAttachPart(Part targetPart)
        {
            AttachSuction(targetPart, breakForce);
        }

        public void AttachSuction(Part partToAttach, float breakforce)
        {
            AttachFixed(partToAttach, breakforce);
            Events["ContextMenuDetach"].guiActiveUnfocused = true;
            Events["ContextMenuDetach"].guiActive = true;
            //Update context menu info
            state = "Attached to : " + partToAttach.partInfo.title;
            //Sound
            fxSndAttach.audio.Play();
        }

        public void DetachSuction()
        {
            Detach();
            Events["ContextMenuDetach"].guiActiveUnfocused = false;
            Events["ContextMenuDetach"].guiActive = false;
            state = "Idle";
            if (attachMode.FixedJoint) fxSndDetach.audio.Play();
        }

        [KSPEvent(name = "ContextMenuDetach", active = true, guiActive = false, guiActiveUnfocused = false, guiName = "Detach")]
        public void ContextMenuDetach()
        {
            DetachSuction();
        }

        [KSPAction("Detach")]
        public void ActionGroupDetach(KSPActionParam param)
        {
            if (!this.part.packed)
            {
                DetachSuction();
            }
        }

    }
}
