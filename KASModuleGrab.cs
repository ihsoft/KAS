using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using UnityEngine;

namespace KAS
{
    public class KASModuleGrab : KASModuleAttachCore
    {
        //Part.cfg
        [KSPField] public Vector3 evaPartPos = new Vector3(0f, 0f, 0f);
        [KSPField] public Vector3 evaPartDir = new Vector3(0f, 0f, -1f);
        [KSPField] public string attachNodeName = null;
        [KSPField] public bool customGroundPos = false;
        [KSPField] public Vector3 dropPartPos = new Vector3(0f, 0f, 0f);
        [KSPField] public Vector3 dropPartRot = new Vector3(0f, 0f, 0f);
        [KSPField] public bool addPartMass = true;
        [KSPField] public bool physicJoint = false;
        [KSPField] public string evaTransformName = "jetpackCollider";
        [KSPField] public bool storable = false;
        [KSPField] public int storedSize = 1;
        [KSPField] public string bayType = null;
        [KSPField] public string bayNode  = "top";
        [KSPField] public Vector3 bayRot  = new Vector3(0f, 0f, 0f);

        [KSPField] public float attachMaxDist = 2f;
        [KSPField] public bool attachOnPart = false;
        [KSPField] public bool attachOnEva = false;
        [KSPField] public bool attachOnStatic = false;
        [KSPField] public bool attachSendMsgOnly = false;

        //Sounds
        [KSPField] public string attachPartSndPath = "KAS/Sounds/attach";
        [KSPField] public string attachStaticSndPath = "KAS/Sounds/grappleAttachStatic";
        [KSPField] public string detachSndPath = "KAS/Sounds/detach";

        public FXGroup fxSndAttachPart, fxSndAttachStatic, fxSndDetach;


        //Sounds
        [KSPField] public string grabSndPath = "KAS/Sounds/grab";
        public FXGroup fxSndGrab;

        //Grab
        [KSPField(isPersistant = true)] public bool grabbed = false;
        [KSPField(isPersistant = true)] public string evaHolderVesselName = null;
        public Part evaHolderPart = null;
        private float orgKerbalMass;
        private Collider evaCollider;
        private Transform evaNodeTransform;
        private AttachNode partNode;
        private FixedJoint evaJoint;


        public override string GetInfo()
        {
            string info = base.GetInfo();
            info += "---- Grab ----";
            info += "\n";
            info += "Grab key : " + KASAddonControlKey.grabPartKey;
            info += "\n";
            info += "Storable : " + storable;
            if (storable)
            {
                info += "\n";
                info += "Stored size : " + storedSize;
            }
            if (attachOnPart || attachOnEva || attachOnStatic)
            {
                info += "\n";
                info += "Attach key : " + KASAddonControlKey.attachKey;
                info += "\n";
                info += "Can be attached on : ";
                if (attachOnPart)
                {
                    info += "\n";
                    info += "- Parts";
                }
                if (attachOnEva)
                {
                    info += "\n";
                    info += "- Eva";
                }
                if (attachOnStatic)
                {
                    info += "\n";
                    info += "- Static";
                }
            }
            return info;
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (state == StartState.Editor || state == StartState.None) return;
            KAS_Shared.createFXSound(this.part, fxSndGrab, grabSndPath, false);
            KAS_Shared.createFXSound(this.part, fxSndAttachPart, attachPartSndPath, false);
            KAS_Shared.createFXSound(this.part, fxSndDetach, detachSndPath, false);
            KAS_Shared.createFXSound(this.part, fxSndAttachStatic, attachStaticSndPath, false);
            GameEvents.onCrewBoardVessel.Add(new EventData<GameEvents.FromToAction<Part, Part>>.OnEvent(this.OnCrewBoardVessel));
            GameEvents.onVesselChange.Add(new EventData<Vessel>.OnEvent(this.OnVesselChange));
            RefreshContextMenu();
        }


        void OnVesselChange(Vessel vesselChange)
        {
            if (KASAddonPointer.isRunning) KASAddonPointer.StopPointer();
        }

        void OnCrewBoardVessel(GameEvents.FromToAction<Part, Part> fromToAction)
        {
            if (grabbed && fromToAction.from.vessel == evaHolderPart.vessel)
            {
                KAS_Shared.DebugLog(fromToAction.from.vessel.vesselName + " boarding " + fromToAction.to.vessel.vesselName + " with a part grabbed, dropping it to avoid destruction");
                Drop();
            }
        }

        void OnDestroy()
        {
            GameEvents.onCrewBoardVessel.Remove(new EventData<GameEvents.FromToAction<Part, Part>>.OnEvent(this.OnCrewBoardVessel));
            GameEvents.onVesselChange.Remove(new EventData<Vessel>.OnEvent(this.OnVesselChange));
        }

        public void OnPartUnpack()
        {
            if (grabbed)
            {
                if (evaHolderVesselName != null && evaHolderVesselName != "")
                {              
                    Vessel vess = KAS_Shared.GetVesselByName(evaHolderVesselName);
                    if (vess)
                    {
                        KAS_Shared.DebugLog("OnPartUnpack(EvaGrab) - Reset grab on : " + evaHolderVesselName);
                        Grab(vess);
                    }
                    else
                    {
                        evaHolderVesselName = null;
                        evaHolderPart = null;
                        grabbed = false;
                    }
                }
            }
        }

        public void Grab(Vessel kerbalEvaVessel)
        {
            KAS_Shared.DebugLog("Grab - Grabbing part :" + this.part.partInfo.name);

            //Get eva transform
            evaCollider = KAS_Shared.GetEvaCollider(kerbalEvaVessel, evaTransformName);
            if (!evaCollider)
            {
                KAS_Shared.DebugLog("Grab - " + evaTransformName + "transform not found on eva !");
                return;
            }

            //Get attach node
            if (attachNodeName == null || attachNodeName == "")
            {
                if (this.part.srfAttachNode == null)
                {
                    KAS_Shared.DebugLog("Grab - surface attach node cannot be found on the part !");
                    return;
                }
                KAS_Shared.AddNodeTransform(this.part, this.part.srfAttachNode);
                partNode = this.part.srfAttachNode;
            }
            else
            {
                AttachNode an = this.part.findAttachNode(attachNodeName);
                if (an == null)
                {
                    KAS_Shared.DebugLog("Grab - " + attachNodeName + " node cannot be found on the part !");
                    return;
                }
                KAS_Shared.AddNodeTransform(this.part, an);
                partNode = an;
            }

            //Send message to other modules
            base.SendMessage("OnPartGrab", kerbalEvaVessel, SendMessageOptions.DontRequireReceiver);

            //Drop grabbed part on eva if needed
            KASModuleGrab tmpGrabbbedPartModule = KAS_Shared.GetGrabbedPartModule(kerbalEvaVessel);
            if (tmpGrabbbedPartModule)
            {
                KAS_Shared.DebugWarning("Grab - Drop current grabbed part");
                tmpGrabbbedPartModule.Drop();
            }
 
            evaNodeTransform = new GameObject("KASEvaNode").transform;
            evaNodeTransform.parent = evaCollider.transform;
            evaNodeTransform.localPosition = evaPartPos;
            evaNodeTransform.rotation = KAS_Shared.DirectionToQuaternion(evaCollider.transform, evaPartDir);

            KAS_Shared.MoveAlign(this.part.transform, partNode.nodeTransform, evaNodeTransform);

            //Grab winch connected head if any
            KASModuleWinch moduleWinch = KAS_Shared.GetConnectedWinch(this.part);
            if (moduleWinch)
            {
                KASModulePort modulePort = this.part.GetComponent<KASModulePort>();
                moduleWinch.UnplugHead(false);
                moduleWinch.GrabHead(kerbalEvaVessel, modulePort);
            }

            List<Collider> allColliders = new List<Collider>(this.part.GetComponentsInChildren<Collider>() as Collider[]);
            foreach (Collider col in allColliders)
            {
                col.isTrigger = true;
            }

            Detach();
            KAS_Shared.DecoupleFromAll(this.part);
            this.part.Couple(kerbalEvaVessel.rootPart);
            //Destroy joint to avoid buggy eva move
            Destroy(this.part.attachJoint);
            
            this.part.rigidbody.velocity = kerbalEvaVessel.rootPart.rigidbody.velocity;

            if (physicJoint)
            {
                if (evaJoint) Destroy(evaJoint);
                evaJoint = this.part.gameObject.AddComponent<FixedJoint>();
                evaJoint.connectedBody = evaCollider.attachedRigidbody;
                evaJoint.breakForce = 5;
                evaJoint.breakTorque = 5;
            }
            else
            {
                this.part.physicalSignificance = Part.PhysicalSignificance.NONE;
                this.part.transform.parent = evaNodeTransform;
                this.part.rigidbody.isKinematic = true;
            }

            //Add grabbed part mass to eva
            if (addPartMass && !physicJoint)
            {
                orgKerbalMass = kerbalEvaVessel.rootPart.mass;
                kerbalEvaVessel.rootPart.mass += this.part.mass;
            }

            evaHolderVesselName = kerbalEvaVessel.vesselName;
            evaHolderPart = kerbalEvaVessel.rootPart;
            grabbed = true;

            RefreshContextMenu();

            //Play grab sound
            fxSndGrab.audio.Play();
            base.SendMessage("OnPartGrabbed", kerbalEvaVessel, SendMessageOptions.DontRequireReceiver);
        }

        public void Drop()
        {
            if (grabbed)
            {
                KAS_Shared.DebugLog("Drop - Dropping part :" + this.part.partInfo.name);
                base.SendMessage("OnPartDrop", SendMessageOptions.DontRequireReceiver);

                if (this.part.vessel.isEVA)
                {
                    this.part.decouple();
                }

                //Remove created joints between eva and part if exist
                KAS_Shared.RemoveFixedJointBetween(this.part, evaHolderPart);
                KAS_Shared.RemoveHingeJointBetween(this.part, evaHolderPart);

                List<Collider> allColliders = new List<Collider>(this.part.GetComponentsInChildren<Collider>() as Collider[]);
                foreach (Collider col in allColliders)
                {
                    col.isTrigger = false;
                }

                if (customGroundPos && evaHolderPart.checkLanded())
                {
                    KAS_Shared.MoveRelatedTo(this.part.transform, evaCollider.transform, dropPartPos, dropPartRot);
                }
                else
                {
                    KAS_Shared.MoveAlign(this.part.transform, partNode.nodeTransform, evaNodeTransform);
                }

                if (evaNodeTransform) Destroy(evaNodeTransform.gameObject);
                if (evaJoint) Destroy(evaJoint);

                this.part.transform.parent = null;
                this.part.rigidbody.isKinematic = false;
                this.part.physicalSignificance = Part.PhysicalSignificance.FULL;
                this.part.rigidbody.velocity = evaHolderPart.rigidbody.velocity;

                if (addPartMass & !physicJoint) evaHolderPart.mass = orgKerbalMass;

                KASModuleWinch grabbedWinchHead = KAS_Shared.GetWinchModuleGrabbed(evaHolderPart.vessel);
                if (grabbedWinchHead)
                {
                    if (grabbedWinchHead.grabbedPortModule)
                    {
                        KAS_Shared.DebugLog("Drop - Grabbed part have a port connected");
                        grabbedWinchHead.PlugHead(grabbedWinchHead.grabbedPortModule, KASModuleWinch.PlugState.PlugDocked,fireSound:false);
                    }
                }

                evaJoint = null;
                evaNodeTransform = null;
                evaHolderVesselName = null;
                evaHolderPart = null;
                grabbed = false;

                RefreshContextMenu();

                //Send drop message to all child objects
                base.SendMessage("OnPartDropped", SendMessageOptions.DontRequireReceiver);
            }
            else
            {
                KAS_Shared.DebugWarning("Drop - Part not grabbed, ignoring drop...");
            }
        }

        public void RefreshContextMenu()
        {
            if (grabbed)
            {
                Events["ContextMenuGrab"].guiActiveUnfocused = false;
                Events["ContextMenuDrop"].guiActiveUnfocused = true;
                Events["ContextMenuDrop"].guiActive = true;
            }
            else
            {
                Events["ContextMenuGrab"].guiActiveUnfocused = true;
                Events["ContextMenuDrop"].guiActiveUnfocused = false;
                Events["ContextMenuDrop"].guiActive = false;
            }

            Events["ContextMenuGrab"].unfocusedRange = KASAddonControlKey.radius;
            Events["ContextMenuGrab"].guiName = "Grab" + " (Key " + KASAddonControlKey.grabPartKey.ToUpper() + ")";

            if (attachOnPart || attachOnEva || attachOnStatic)
            {
                Events["ContextMenuEvaAttach"].guiActiveUnfocused = true;
                if (grabbed) Events["ContextMenuEvaAttach"].guiActive = true;
                else Events["ContextMenuEvaAttach"].guiActive = false;
            }
            else
            {
                Events["ContextMenuEvaAttach"].guiActiveUnfocused = false;
                Events["ContextMenuEvaAttach"].guiActive = false;
            }
        }

        [KSPEvent(name = "ContextMenuGrab", active = true, guiActiveUnfocused = false, guiActive = false, unfocusedRange = 2f, guiName = "Grab")]
        public void ContextMenuGrab()
        {
            Grab(FlightGlobals.ActiveVessel);          
        }

        [KSPEvent(name = "ContextMenuDrop", active = true, guiActiveUnfocused = false, guiActive = false, unfocusedRange = 2f, guiName = "Drop")]
        public void ContextMenuDrop()
        {
            Drop();
        }

        [KSPEvent(name = "ContextMenuEvaAttach", active = true, guiActiveUnfocused = false, guiActive = false, guiName = "Attach")]
        public void ContextMenuEvaAttach()
        {
            if (attachOnPart || attachOnEva || attachOnStatic)
            {
                if (KASAddonPointer.isRunning)
                {
                    KASAddonPointer.StopPointer();
                }
                else
                {
                    KASAddonPointer.StartPointer(this.part, KASAddonPointer.PointerMode.MoveAndAttach, attachOnPart, attachOnEva, attachOnStatic, attachMaxDist, this.part.transform, attachSendMsgOnly);
                }
            }
            else
            {
                ScreenMessages.PostScreenMessage("This part cannot be attached", 5, ScreenMessageStyle.UPPER_CENTER);
            }
        }
    }
}
