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
        [KSPField] public string nodeType = "kasplug";
        [KSPField] public string attachNode = "bottom";
        [KSPField] public string nodeTransformName = "portNode";
        [KSPField] public float breakForce = 30;
        [KSPField] public float rotateForce = 1f;

        //Sounds
        [KSPField] public string plugSndPath = "KAS/Sounds/plug";
        [KSPField] public string unplugSndPath = "KAS/Sounds/unplug";
        [KSPField] public string plugDockedSndPath = "KAS/Sounds/plugdocked";
        [KSPField] public string unplugDockedSndPath = "KAS/Sounds/unplugdocked";
        public FXGroup fxSndPlug, fxSndUnplug, fxSndPlugDocked, fxSndUnplugDocked; 

        public KASPlugState plugState = 0;
        public enum KASPlugState
        {
            Ready = 0,
            PreAttached = 1,
            PlugUndock = 2,
            PlugDock = 3,
        }

        public KASModuleWinch winchConnected;

        public Transform portNode
        {
            get { return this.part.FindModelTransform(nodeTransformName);}
        }

        public override string GetInfo()
        {
            string info = base.GetInfo();
            info += "---- Port ----";
            info += "\n";
            info += "Strength : " + breakForce;
            info += "\n";
            info += "Rotating force : " + rotateForce;
            return info;
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (state == StartState.Editor || state == StartState.None) return;
            KAS_Shared.createFXSound(this.part, fxSndPlug, plugSndPath, false);
            KAS_Shared.createFXSound(this.part, fxSndUnplug, unplugSndPath, false);
            KAS_Shared.createFXSound(this.part, fxSndPlugDocked, plugDockedSndPath, false);
            KAS_Shared.createFXSound(this.part, fxSndUnplugDocked, unplugDockedSndPath, false);   
        }

        void OnDestroy()
        {
            if (winchConnected)
            {
                if (!winchConnected.part.packed) winchConnected.UnplugHead();
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