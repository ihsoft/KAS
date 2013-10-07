using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using UnityEngine;
using KSP.IO;

namespace KAS
{
    public class KASModuleGrapplingHook : KASModuleAttachCore
    {
        [KSPField] public float forceNeeded = 5;
        [KSPField] public bool attachToPart = true;
        [KSPField] public Vector3 rayDir = Vector3.down;
        [KSPField] public float rayLenght = 1;
        [KSPField] public float partBreakForce = 10;
        [KSPField] public float staticBreakForce = 15;
        //[KSPField] public float minFwdDot = 0.990f;
        //[KSPField] public float minRollDot = float.MinValue;
        [KSPField] public float aboveDist = 0f;

        //Sounds
        [KSPField] public string attachStaticSndPath = "KAS/Sounds/grappleAttachStatic";
        [KSPField] public  string attachPartSndPath = "KAS/Sounds/grappleAttachPart";
        [KSPField] public  string attachEvaSndPath = "KAS/Sounds/grappleAttachEva";
        [KSPField] public  string detachSndPath = "KAS/Sounds/grappleDetach";
        public FXGroup fxSndAttachStatic, fxSndAttachPart, fxSndAttachEva, fxSndDetach;

        //Info
        [KSPField(guiActive = true, guiName = "State", guiFormat="S")] public string state = "Idle";


        public override string GetInfo()
        {
            string info = base.GetInfo();
            info += "---- Grappling Hook ----";
            info += "\n";
            info += "Attach strength (part) : " + partBreakForce;
            info += "\n";
            info += "Attach strength (ground) : " + staticBreakForce;
            info += "\n";
            info += "Inpact force : " + forceNeeded;
            return info;
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (attachMode.StaticJoint || attachMode.FixedJoint)
            {
                Events["ContextMenuDetach"].guiActive = true;
                Events["ContextMenuDetach"].guiActiveUnfocused = true;
            }
            else
            {
                Events["ContextMenuDetach"].guiActive = false;
                Events["ContextMenuDetach"].guiActiveUnfocused = false;
            }

            KAS_Shared.createFXSound(this.part, fxSndAttachStatic, attachStaticSndPath, false);
            KAS_Shared.createFXSound(this.part, fxSndAttachPart, attachPartSndPath, false);
            KAS_Shared.createFXSound(this.part, fxSndAttachEva, attachEvaSndPath, false);
            KAS_Shared.createFXSound(this.part, fxSndDetach, detachSndPath, false);
        }

        public void OnPartUnpack()
        {
            this.part.rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        public void OnPartPack()
        {
            this.part.rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }

        public void OnPartGrab(Vessel kerbalEvaVessel)
        {
            DetachGrapple();
        }

        public void OnAttachStatic()
        {
            DetachGrapple();
            AttachStaticGrapple(staticBreakForce);
        }

        public void OnAttachPart(Part targetPart)
        {
            KAS_Shared.DebugLog("OnAttachPart(GrapplingHook)");
            if (FixedAttach.connectedPart)
            {
                DetachGrapple();
            }   

            AttachPartGrapple(targetPart, partBreakForce);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!attachMode.StaticJoint && !attachMode.FixedJoint)
            {
                AttachOnCollision(collision);
            }
        }

        private void AttachOnCollision(Collision collision)
        {       
            //Don't attach if inpact force is too low
            if (collision.relativeVelocity.magnitude < forceNeeded) return;

            float shorterDist = Mathf.Infinity;
            bool nearestHitFound = false;
            Part nearestHitPart = null;
            RaycastHit nearestHit = new RaycastHit();
            Vector3 rayDirection = this.part.transform.TransformDirection(rayDir);
            //Get all raycast hits in front of the grapple
            List<RaycastHit> nearestHits = new List<RaycastHit>(Physics.RaycastAll(this.part.transform.position, rayDirection, rayLenght, 557059));
            foreach (RaycastHit hit in nearestHits)
            {
                //Exclude grapple collider
                if (hit.collider == this.part.collider) continue;
                //Exclude parts if needed
                if (!attachToPart)
                {
                    if (hit.rigidbody)
                    {
                        if (hit.rigidbody.GetComponent<Part>())
                        {
                            continue;
                        }
                    }
                }

                /*
                // Check forward dot
                float fwdDot = Mathf.Abs(Vector3.Dot(hit.normal, this.transform.up));
                if (fwdDot <= minFwdDot)
                {
                    continue;
                }

                // Check roll dot
                float rollDot = Vector3.Dot(hit.normal, this.transform.up);
                if (rollDot <= minRollDot)
                {
                    continue;
                }*/

                // Get closest hit
                float tmpShorterDist = Vector3.Distance(this.part.transform.position, hit.point);
                if (tmpShorterDist <= shorterDist)
                {
                    shorterDist = tmpShorterDist;
                    nearestHit = hit;
                    if (nearestHit.rigidbody) nearestHitPart = nearestHit.rigidbody.GetComponent<Part>();
                    nearestHitFound = true;
                }
            }

            if (!nearestHitFound)
            {
                KAS_Shared.DebugLog("AttachOnCollision - Nothing to attach in front of grapple");
                return;
            }

            KASModuleWinch connectedWinch = KAS_Shared.GetConnectedWinch(this.part);
            if (connectedWinch)
            {
                MoveAbove(nearestHit.point, nearestHit.normal, aboveDist);
                connectedWinch.cableJointLength = connectedWinch.cableRealLenght;
            }
       
            if (nearestHitPart)
            {
                KAS_Shared.DebugLog("AttachOnCollision - grappleAttachOnPart=true");
                KAS_Shared.DebugLog("AttachOnCollision - Attaching to part : " + nearestHitPart.partInfo.title); 
                AttachPartGrapple(nearestHitPart, partBreakForce);
            }
            else
            {
                KAS_Shared.DebugLog("AttachOnCollision - Attaching to static : " + nearestHit.collider.name);
                AttachStaticGrapple(staticBreakForce);
            }
        }

        public void AttachPartGrapple(Part attachToPart, float breakForce)
        {
            AttachFixed(attachToPart, partBreakForce);
            state = "Attached to : " + attachToPart.partInfo.title;
            //Sound
            if (attachToPart.vessel.isEVA)
            {
                fxSndAttachEva.audio.Play();
            }
            else
            {
                fxSndAttachPart.audio.Play();
            }
        }

        public void AttachStaticGrapple(float breakForce)
        {
            AttachStatic(staticBreakForce);
            Events["ContextMenuDetach"].guiActive = true;
            Events["ContextMenuDetach"].guiActiveUnfocused = true;
            state = "Ground attached";
            fxSndAttachStatic.audio.Play();
        }
        
        public void DetachGrapple()
        {
            state = "Idle";
            Events["ContextMenuDetach"].guiActive = false;
            Events["ContextMenuDetach"].guiActiveUnfocused = false;
            if (attachMode.StaticJoint || attachMode.FixedJoint)
            {
                Detach();
                fxSndDetach.audio.Play();
            }
        }

        [KSPEvent(name = "ContextMenuDetach", active = true, guiActive = false, guiActiveUnfocused = false, guiName = "Detach")]
        public void ContextMenuDetach()
        {
            DetachGrapple();
        }

        [KSPAction("Detach")]
        public void ActionGroupDetach(KSPActionParam param)
        {
            if (!this.part.packed)
            {
                DetachGrapple();
            }
        }

    }
}
