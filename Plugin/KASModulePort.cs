using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using UnityEngine;

namespace KAS
{
    public class KASModulePort : KASModuleAttachCore
    {
        //Part.cfg
        [KSPField]
        public string nodeType = "kasplug";
        [KSPField]
        public string attachNode = "bottom";
        [KSPField]
        public string nodeTransformName = "portNode";
        [KSPField]
        public float breakForce = 30;
        [KSPField]
        public float rotateForce = 1f;

        //Sounds
        [KSPField]
        public string plugSndPath = "KAS/Sounds/plug";
        [KSPField]
        public string unplugSndPath = "KAS/Sounds/unplug";
        [KSPField]
        public string plugDockedSndPath = "KAS/Sounds/plugdocked";
        [KSPField]
        public string unplugDockedSndPath = "KAS/Sounds/unplugdocked";

        [KSPField(isPersistant = true)]
        public bool plugged = false;

        public KASModuleWinch winchConnected;

        public Transform portNode
        {
            get { return this.part.FindModelTransform(nodeTransformName); }
        }

        public Part nodeConnectedPart
        {
            get
            {
                AttachNode an = this.part.findAttachNode(attachNode);
                if (an != null)
                {
                    return an.attachedPart;
                }
                return null;
            }
            set
            {
                AttachNode an = this.part.findAttachNode(attachNode);
                if (an != null)
                {
                    an.attachedPart = value;
                }
                else
                {
                    KAS_Shared.DebugError("connectedPart(Port) Cannot set connectedPart !");
                }
            }
        }

        public override string GetInfo()
        {
            var sb = new StringBuilder();
            sb.AppendFormat("<b>Strength</b>: {0:F0}", breakForce); sb.AppendLine();
            sb.AppendFormat("<b>Rotor torque</b>: {0:F1}", rotateForce); sb.AppendLine();
            return sb.ToString();
        }

        protected override void OnPartDie()
        {
            base.OnPartDie();

            if (winchConnected)
            {
                winchConnected.UnplugHead(false);
            }
        }

        public void OnKISAction(BaseEventData baseEventData)
        {
            string action = baseEventData.GetString("action");
            Part tgtPart = (Part)baseEventData.Get("targetPart");
            AttachNode tgtNode = (AttachNode)baseEventData.Get("targetNode");

            if (action == KIS.KIS_Shared.MessageAction.Store.ToString())
            {
                if (winchConnected)
                {
                    winchConnected.UnplugHead(false);
                }
            }
            if (action == KIS.KIS_Shared.MessageAction.DropEnd.ToString())
            {
                if (winchConnected)
                {
                    winchConnected.cableJointLength = winchConnected.cableRealLenght;
                    winchConnected.PlugHead(this, KASModuleWinch.PlugState.PlugDocked, false, false, true);
                }
            }
            if (action == KIS.KIS_Shared.MessageAction.AttachStart.ToString())
            {
                if (tgtNode != null)
                {
                    KASModuleWinch moduleWinch = tgtNode.owner.GetComponent<KASModuleWinch>();
                    if (moduleWinch)
                    {
                        if (moduleWinch.headState == KASModuleWinch.PlugState.Deployed && tgtNode.id == moduleWinch.connectedPortNodeName)
                        {
                            if (winchConnected)
                            {
                                winchConnected.UnplugHead(false);
                                return;
                            }
                        }
                    }
                }
            }
            if (action == KIS.KIS_Shared.MessageAction.AttachEnd.ToString())
            {
                if (tgtNode != null)
                {
                    KASModuleWinch moduleWinch = tgtNode.owner.GetComponent<KASModuleWinch>();
                    if (moduleWinch)
                    {
                        if (moduleWinch.headState == KASModuleWinch.PlugState.Deployed && tgtNode.id == moduleWinch.connectedPortNodeName)
                        {
                            moduleWinch.PlugHead(this, KASModuleWinch.PlugState.PlugDocked, alreadyDocked: true);
                            StartCoroutine(WaitAndRemoveJoint());
                        }
                    }
                }
            }
        }

        private IEnumerator WaitAndRemoveJoint()
        {
            while (!this.part.started && this.part.State != PartStates.DEAD)
            {
                KAS_Shared.DebugLog("WaitAndRemoveJoint - Waiting initialization of the part...");
                yield return null;
            }
            if (this.part.attachJoint)
            {
                this.part.attachJoint.DestroyJoint();
            }
        }

        public void TurnLeft()
        {
            Vector3 force = portNode.TransformDirection(Vector3.forward) * rotateForce;
            this.part.Rigidbody.AddTorque(force, ForceMode.Force);
        }

        public void TurnRight()
        {
            Vector3 force = portNode.TransformDirection(Vector3.forward) * rotateForce;
            this.part.Rigidbody.AddTorque(-force, ForceMode.Force);
        }

        public bool strutConnected()
        {
            KASModuleStrut moduleStrut = this.part.GetComponent<KASModuleStrut>();
            if (moduleStrut)
            {
                if (moduleStrut.linkedStrutModule)
                {
                    return true;
                }
            }
            return false;
        }

        [KSPEvent(name = "ContextMenuPlugUndocked", active = true, guiActive = false, guiActiveUnfocused = true, guiName = "Plug(Undocked)")]
        public void ContextMenuPlugUndocked()
        {
            KASModuleWinch winchModule = KAS_Shared.GetWinchModuleGrabbed(FlightGlobals.ActiveVessel);
            if (winchModule)
            {
                winchModule.PlugHead(this, KASModuleWinch.PlugState.PlugUndocked);
            }
            else
            {
                ScreenMessages.PostScreenMessage("You didn't have anything to plug !", 5, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        [KSPEvent(name = "ContextMenuPlugDocked", active = true, guiActive = false, guiActiveUnfocused = true, guiName = "Plug(Docked)")]
        public void ContextMenuPlugDocked()
        {
            KASModuleWinch winchModule = KAS_Shared.GetWinchModuleGrabbed(FlightGlobals.ActiveVessel);
            if (winchModule)
            {
                winchModule.PlugHead(this, KASModuleWinch.PlugState.PlugDocked);
            }
            else
            {
                ScreenMessages.PostScreenMessage("You didn't have anything to plug !", 5, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        [KSPEvent(name = "ContextMenuUnplug", active = true, guiActive = true, guiActiveUnfocused = true, guiName = "Unplug")]
        public void ContextMenuUnplug()
        {
            if (winchConnected)
            {
                winchConnected.UnplugHead();
            }
            else
            {
                ScreenMessages.PostScreenMessage("There is nothing to unplug !", 5, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        [KSPAction("Unplug", actionGroup = KSPActionGroup.None)]
        public void ActionGroupUnplug(KSPActionParam param)
        {
            if (!this.part.packed)
            {
                if (winchConnected)
                {
                    winchConnected.UnplugHead();
                }
                else
                {
                    ScreenMessages.PostScreenMessage("There is nothing to unplug !", 5, ScreenMessageStyle.UPPER_CENTER);
                }
            }
        }

    }
}