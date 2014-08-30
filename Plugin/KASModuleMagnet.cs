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

        public FXGroup fxSndAttach, fxSndDetach, fxSndMagnet, fxSndMagnetStart, fxSndMagnetStop;

        [KSPField(guiActive = true, guiName = "State", guiFormat="S")] public string state = "Off";
        private bool _magnetActive = false;

        public bool MagnetActive
        {
            get
            {
                return _magnetActive;
            }
            set
            {
                if (value == true && _magnetActive == false)
                {
                    KAS_Shared.DebugLog("MagnetActive(Magnet) Start magnet...");
                    state = "On";
                    if (!fxSndMagnetStart.audio.isPlaying)
                    {
                        fxSndMagnetStart.audio.Play();
                    }
                    if (!fxSndMagnet.audio.isPlaying)
                    {
                        fxSndMagnet.audio.Play();
                    }
                    _magnetActive = true;
                }
                else if (value == false && _magnetActive == true)
                {
                    KAS_Shared.DebugLog("MagnetActive(Magnet) Stop magnet...");
                    state = "Off";
                    DetachMagnet();
                    _magnetActive = false;
                }
            }
        }

        public override string GetInfo()
        {
            var sb = new StringBuilder();
            sb.AppendFormat("<b>Magnet strength</b>: {0:F0}", breakForce); sb.AppendLine();
            sb.AppendFormat("<b>Power consumption</b>: {0:F1}/s", powerDrain); sb.AppendLine();
            return sb.ToString();
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
            if (attachMode.FixedJoint)
            {
                MagnetActive = true;
            }
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (!HighLogic.LoadedSceneIsFlight) return;
            UpdateMagnet();
        }

        protected override void OnJointBreak(float breakForce)
        {
            base.OnJointBreak(breakForce);
            KAS_Shared.DebugWarning("A joint broken on " + part.partInfo.title + " !, force: " + breakForce);
            KAS_Shared.DebugWarning("Disable magnet...");
            MagnetActive = false;
        }

        public void OnPartGrab(Vessel kerbalEvaVessel)
        {
            MagnetActive = false;
            Events["ContextMenuMagnet"].guiActive = false;
        }

        public void OnPartDrop()
        {
            MagnetActive = false;
            Events["ContextMenuMagnet"].guiActive = true;
        }

        public void OnAttachPart(Part targetPart)
        {
            KAS_Shared.DebugLog("OnAttachPart(magnet) - Attach magnet to : " + targetPart.partInfo.title);
            if (FixedAttach.connectedPart)
            {
                MagnetActive = false;
            }        
            AttachMagnet(targetPart); 
        }

        void OnCollisionEnter(Collision collision)
        {
            if (MagnetActive)
            {
                AttachOnCollision(collision, "enter");
            }
        }

        void OnCollisionStay(Collision collisionInfo)
        {
            if (MagnetActive)
            {
                AttachOnCollision(collisionInfo, "stay");
            }
        }

        void UpdateMagnet()
        {         
            if (!MagnetActive) return;
            //Drain power and stop if no energy
            if (!KAS_Shared.RequestPower(this.part, powerDrain))
            {
                state = "Insufficient Power";
                if (this.part.vessel == FlightGlobals.ActiveVessel)
                {
                    ScreenMessages.PostScreenMessage("Magnet stopped ! Insufficient Power", 5, ScreenMessageStyle.UPPER_CENTER);
                }
                MagnetActive = false;
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
                if (FixedAttach.connectedPart)
                {
                    fxSndAttach.audio.Play();
                }
            }
        }

        private void AttachMagnet(Part partToAttach)
        {
            if (KAS_Shared.RequestPower(this.part, powerDrain))
            {
                //Disable all collisions
                this.part.collider.isTrigger = true;
                // Create joint
                AttachFixed(partToAttach, breakForce);
                // Set reference
                MagnetActive = true;
            }
            else
            {
                ScreenMessages.PostScreenMessage("Magnet not powered !", 5, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        private void DetachMagnet()
        {
            this.part.collider.isTrigger = false;
            if (FixedAttach.connectedPart)
            {
                fxSndDetach.audio.Play();
                fxSndMagnetStop.audio.Play();
            }
            Detach();
            fxSndMagnet.audio.Stop();
        }

        [KSPEvent(name = "ContextMenuMagnet", active = true, guiActive = true, guiActiveUnfocused = false, guiName = "Magnet On/Off")]
        public void ContextMenuMagnet()
        {
            MagnetActive = !MagnetActive;
        }

        [KSPAction("Toogle Magnet")]
        public void ActionGroupMagnetToggle(KSPActionParam param)
        {
            if (!this.part.packed)
            {
                if (param.type == KSPActionType.Activate)
                {
                    MagnetActive = true;
                }
                if (param.type == KSPActionType.Deactivate)
                {
                    MagnetActive = false;
                }
            }
        }

        [KSPAction("Magnet On")]
        public void ActionGroupMagnetOn(KSPActionParam param)
        {
            if (!this.part.packed)
            {
                MagnetActive = true;
            }
        }

        [KSPAction("Magnet Off")]
        public void ActionGroupMagnetOff(KSPActionParam param)
        {
            if (!this.part.packed)
            {
                MagnetActive = false;
            }
        }

    }
}
