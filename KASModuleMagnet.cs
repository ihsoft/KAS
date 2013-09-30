using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KAS
{
    public class KASModuleMagnet : KASModuleAttachCore
    {
        [KSPField] public float powerDrain = 1f;
        [KSPField] public float breakForce = 10;
        [KSPField] public float minFwdDot = 0.998f;
        [KSPField] public float minRollDot = float.MinValue;
        [KSPField] public bool attachToEva = false;

        //Sounds
        [KSPField] public string attachSndPath = "KAS/Sounds/magnetAttach";
        [KSPField] public string detachSndPath = "KAS/Sounds/magnetDetach";
        [KSPField] public string magnetSndPath = "KAS/Sounds/magnet";
        [KSPField] public string magnetStartSndPath = "KAS/Sounds/magnetstart";
        [KSPField] public string magnetStopSndPath = "KAS/Sounds/magnetstop";

        //Info
        [KSPField(guiActive = true, guiName = "State", guiFormat="S")] public string state = "Off";

        public FXGroup fxSndAttach, fxSndDetach, fxSndMagnet, fxSndMagnetStart, fxSndMagnetStop;
        public bool magnetActive = false;
        private bool magnetStarted = false;

        public override string GetInfo()
        {
            string info = base.GetInfo();
            info += "---- Magnet ----";
            info += "\n";
            info += "Break Force : " + breakForce;
            info += "\n";
            info += "Power consumption : " + powerDrain + "/s";
            return info;
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (state == StartState.Editor || state == StartState.None) return;
            KAS_Shared.createFXSound(this.part, fxSndAttach, attachSndPath, false);
            KAS_Shared.createFXSound(this.part, fxSndDetach, detachSndPath, false);
            KAS_Shared.createFXSound(this.part, fxSndMagnet, magnetSndPath, true);
            KAS_Shared.createFXSound(this.part, fxSndMagnetStart, magnetStartSndPath, false);
            KAS_Shared.createFXSound(this.part, fxSndMagnetStop, magnetStopSndPath, false);
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (!HighLogic.LoadedSceneIsFlight) return;
            UpdateMagnet();
        }

        void OnJointBreak(float breakForce)
        {
            KAS_Shared.DebugWarning("A joint broken on " + part.partInfo.title + " !, force: " + breakForce);
            KAS_Shared.DebugWarning("Disable magnet...");
            magnetActive = false;
        }

        public void OnAttachPart(Part targetPart)
        {
            AttachMagnet(targetPart); 
        }

        void OnCollisionEnter(Collision collision)
        {
            if (magnetActive)
            {
                AttachOnCollision(collision, "enter");
            }
        }

        void OnCollisionStay(Collision collisionInfo)
        {
            if (magnetActive)
            {
                AttachOnCollision(collisionInfo, "stay");
            }
        }

        void UpdateMagnet()
        {
            if (magnetActive)
            {
                //Drain power and stop if no energy
                if (!KAS_Shared.RequestPower(this.part, powerDrain))
                {
                    state = "Insufficient Power";
                    if (this.part.vessel == FlightGlobals.ActiveVessel)
                    {
                        ScreenMessages.PostScreenMessage("Magnet stopped ! Insufficient Power", 5, ScreenMessageStyle.UPPER_CENTER);
                    }
                    magnetActive = false;
                    return;
                }

                if (!magnetStarted)
                {
                    KAS_Shared.DebugLog("UpdateMagnet - Start magnet...");
                    fxSndMagnetStart.audio.Play();
                    magnetStarted = true;
                    state = "On";
                }

                if (!fxSndMagnet.audio.isPlaying)
                {
                    fxSndMagnet.audio.Play();
                }   
            }
            else
            {
                if (magnetStarted)
                {
                    KAS_Shared.DebugLog("UpdateMagnet - Stop magnet...");
                    DetachMagnet();
                }
            }
        }

        private void AttachOnCollision(Collision collision, string type)
        {
            foreach (ContactPoint contact in collision.contacts)
            {
                // Check if if the collider have a rigidbody
                if (!contact.otherCollider.attachedRigidbody)
                {
                    continue;
                }

                // Check if it's a part
                Part p = contact.otherCollider.attachedRigidbody.GetComponent<Part>();
                if (!p)
                {
                    continue;
                }

                // Check if not already attached
                if (FixedAttach.connectedPart)
                {
                    continue;
                }

                // Check if it's not itself
                if (this.part == p)
                {
                    continue;
                }

                //Attach eva if set in part.cfg
                if (!attachToEva)
                {
                    if (p.vessel.isEVA)
                    {
                        continue;
                    }
                }

                // Check forward dot
                float fwdDot = Mathf.Abs(Vector3.Dot(contact.normal, this.transform.up));
                if (fwdDot <= minFwdDot)
                {
                    continue;
                }

                // Check roll dot
                float rollDot = Vector3.Dot(contact.normal, this.transform.up);
                if (rollDot <= minRollDot)
                {
                    continue;
                }

                // Info
                KAS_Shared.DebugLog("forward dot is : " + fwdDot + " and lockMinFwdDot is set to : " + minFwdDot);
                KAS_Shared.DebugLog("roll dot is : " + rollDot + " and lockMinRollDot is set to : " + minRollDot);
                KAS_Shared.DebugLog("AttachOnCollision - Collision with unattached part detected : " + p.name + " | at : " + contact.point);
                KAS_Shared.DebugLog("AttachOnCollision - Attach to surface the object");

                // attach
                AttachMagnet(p); 
   
                // sound
                if (FixedAttach.connectedPart) fxSndAttach.audio.Play();
            }
        }

        public void AttachMagnet(Part partToAttach)
        {
            if (KAS_Shared.RequestPower(this.part, powerDrain))
            {
                //Disable all collisions
                this.part.collider.isTrigger = true;
                // Create joint
                AttachFixed(partToAttach, breakForce);
                // Set reference
                magnetActive = true;
                state = "Attached to : " + FixedAttach.connectedPart.partInfo.title;
            }
            else
            {
                ScreenMessages.PostScreenMessage("Magnet not powered !", 5, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        public void DetachMagnet()
        {
            this.part.collider.isTrigger = false;

            if (FixedAttach.connectedPart)
            {
                fxSndDetach.audio.Play();
                fxSndMagnetStop.audio.Play();
            }

            Detach();

            magnetStarted = false;
            magnetActive = false;
            state = "Off";
            fxSndMagnet.audio.Stop();
        }

        [KSPEvent(name = "ContextMenuMagnet", active = true, guiActive = true, guiActiveUnfocused = false, guiName = "Magnet On/Off")]
        public void ContextMenuMagnet()
        {
            magnetActive = !magnetActive;
        }

        [KSPAction("Toogle Magnet")]
        public void ActionGroupMagnetToggle(KSPActionParam param)
        {
            if (!this.part.packed)
            {
                if (param.type == KSPActionType.Activate)
                {
                    magnetActive = true;
                }
                if (param.type == KSPActionType.Deactivate)
                {
                    magnetActive = false;
                }
            }
        }

        [KSPAction("Magnet On")]
        public void ActionGroupMagnetOn(KSPActionParam param)
        {
            if (!this.part.packed)
            {
                    magnetActive = true;
            }
        }

        [KSPAction("Magnet Off")]
        public void ActionGroupMagnetOff(KSPActionParam param)
        {
            if (!this.part.packed)
            {
                magnetActive = false;
            }
        }

    }
}
