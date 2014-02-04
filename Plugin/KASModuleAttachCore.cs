using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KAS
{
    public class KASModuleAttachCore : PartModule
    {
        // Fixed attach handling
        public FxAttach FixedAttach;
        public struct FxAttach
        {
            public FixedJoint fixedJoint;
            public Part connectedPart;
            public bool createJointOnUnpack;
            public string savedPartID;
            public string savedVesselID;
            public float savedBreakForce;
        }

        // Static attach handling
        public StAttach StaticAttach;
        public struct StAttach
        {
            public FixedJoint fixedJoint;
            public GameObject connectedGameObject;
        }

        // Docking attach handling
        public KASModuleAttachCore dockedAttachModule = null;
        private DockedVesselInfo vesselInfo = null;
        private string dockedPartID = null;

        // Common
        public struct PhysicObjInfo
        {
            public Vector3 orgPos;
            public Quaternion orgRot;
            public string savedTransformName;
            public float savedMass;
            public Vector3 savedLocalPos;
            public Quaternion savedLocalRot;
        }

        public AttachModeInfo attachMode;
        public struct AttachModeInfo
        {
            public bool Docked;
            public bool Coupled;
            public bool FixedJoint;
            public bool StaticJoint;
        }

        public enum AttachType
        {
            Docked = 1,
            Coupled = 2,
            FixedJoint = 3,
            StaticJoint = 4,
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            if (attachMode.FixedJoint)
            {
                KAS_Shared.DebugLog("OnSave(Core) Fixed joint detected, saving info...");
                ConfigNode FxNode = node.AddNode("FIXEDATTACH");
                FxNode.AddValue("connectedPartID", FixedAttach.connectedPart.flightID.ToString());
                FxNode.AddValue("connectedVesselID", FixedAttach.connectedPart.vessel.id.ToString());
                if (FixedAttach.fixedJoint)
                {
                    KAS_Shared.DebugLog("OnSave(Core) Saving breakforce from joint config : " + FixedAttach.fixedJoint.breakForce);
                    FxNode.AddValue("breakForce", FixedAttach.fixedJoint.breakForce);
                }
                else
                {
                    KAS_Shared.DebugLog("OnSave(Core) Saving breakforce from saved : " + FixedAttach.savedBreakForce);
                    FxNode.AddValue("breakForce", FixedAttach.savedBreakForce);
                }
            }
            if (attachMode.StaticJoint)
            {
                KAS_Shared.DebugLog("OnSave(Core) Static joint detected, saving info...");
                node.AddValue("StaticJoint", "True");
            }
            if (attachMode.Docked)
            {
                KAS_Shared.DebugLog("OnSave(Core) Docked joint detected, saving info...");
                if (dockedAttachModule)
                {
                    node.AddValue("dockedPartID", dockedAttachModule.part.flightID.ToString());
                    ConfigNode nodeD = node.AddNode("DOCKEDVESSEL");
                    this.vesselInfo.Save(nodeD);
                }
                else
                {
                    KAS_Shared.DebugError("OnSave(Core) dockedAttachModule is null !");
                }         
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (node.HasNode("FIXEDATTACH"))
            {
                ConfigNode FxNode = node.GetNode("FIXEDATTACH");
                KAS_Shared.DebugLog("OnLoad(Core) Loading fixed joint info from save...");
                FixedAttach.savedPartID = FxNode.GetValue("connectedPartID").ToString();
                FixedAttach.savedVesselID = FxNode.GetValue("connectedVesselID").ToString();
                FixedAttach.savedBreakForce = float.Parse(FxNode.GetValue("breakForce"));
                attachMode.FixedJoint = true;     
            }
            if (node.HasNode("DOCKEDVESSEL") && node.HasValue("dockedPartID"))
            {
                KAS_Shared.DebugLog("OnLoad(Core) Loading docked info from save...");
                this.vesselInfo = new DockedVesselInfo();
                this.vesselInfo.Load(node.GetNode("DOCKEDVESSEL"));
                dockedPartID = node.GetValue("dockedPartID").ToString();
                attachMode.Docked = true;
            }
            if (node.HasValue("StaticJoint"))
            {
                attachMode.StaticJoint = true;
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (state == StartState.Editor || state == StartState.None) return;

            if (attachMode.Docked)
            {
                Part dockedPart = KAS_Shared.GetPartByID(this.vessel.id.ToString(), dockedPartID);
                if (dockedPart)
                {
                    KASModuleAttachCore dockedAttachModuleTmp = dockedPart.GetComponent<KASModuleAttachCore>();
                    if (dockedAttachModuleTmp)
                    {
                        KAS_Shared.DebugLog("OnLoad(Core) Re-set docking on " + dockedAttachModuleTmp.part.partInfo.title);
                        AttachDocked(dockedAttachModuleTmp);
                    }
                    else
                    {
                        KAS_Shared.DebugError("OnLoad(Core) Unable to get docked module !");
                        attachMode.Docked = false;
                    }
                }
                else
                {
                    KAS_Shared.DebugError("OnLoad(Core) Unable to get saved docked part !");
                    attachMode.Docked = false;
                }
            }

            if (attachMode.Coupled)
            {
                // Todo
            }

            if (attachMode.FixedJoint)
            {

                Part attachedPart = KAS_Shared.GetPartByID(FixedAttach.savedVesselID, FixedAttach.savedPartID);
                if (attachedPart)
                {
                    KAS_Shared.DebugLog("OnLoad(Core) Re-set fixed joint on " + attachedPart.partInfo.title);
                    AttachFixed(attachedPart, FixedAttach.savedBreakForce);
                }
                else
                {
                    KAS_Shared.DebugError("OnLoad(Core) Unable to get saved connected part of the fixed joint !");
                    attachMode.FixedJoint = false;
                }
            }
            if (attachMode.StaticJoint)
            {
                // Nothing to do (see OnVesselLoaded)
            }
            GameEvents.onVesselGoOffRails.Add(new EventData<Vessel>.OnEvent(this.OnVesselGoOffRails));     
        }

        void OnVesselGoOffRails(Vessel vess)
        {
            if (vess != this.vessel) return;
            if (attachMode.StaticJoint && !StaticAttach.fixedJoint)
            {
                KAS_Shared.DebugLog("OnVesselGoOffRails(Core) Re-attach static object");
                AttachStatic();
            }
        }

        void OnJointBreak(float breakForce)
        {
            KAS_Shared.DebugWarning("OnJointBreak(Core) A joint broken on " + part.partInfo.title + " !, force: " + breakForce);
            Detach();
        }

        void OnDestroy()
        {
            GameEvents.onVesselGoOffRails.Remove(new EventData<Vessel>.OnEvent(this.OnVesselGoOffRails));
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (!HighLogic.LoadedSceneIsFlight) return;

            if (FixedAttach.createJointOnUnpack)
            {
                if (!this.part.packed && !FixedAttach.connectedPart.packed)
                {
                    KAS_Shared.DebugWarning("OnUpdate(Core) Fixed attach set and both part unpacked, creating fixed joint...");
                    AttachFixed(FixedAttach.connectedPart, FixedAttach.savedBreakForce);
                    FixedAttach.createJointOnUnpack = false;
                }
            }
        }

        public void MoveAbove(Vector3 position, Vector3 normal, float distance)
        {
            //Find position above the surface   
            Vector3 hitAbove = position + (normal * distance);
            //Find the rotation aligned with the object surface
            Quaternion alignedRotation = Quaternion.FromToRotation(Vector3.up, normal);
            //Set position and rotation
            this.part.transform.position = hitAbove;
            this.part.transform.rotation = alignedRotation;
        }

        public void AttachFixed(Part otherPart, float breakForce)
        {
            attachMode.FixedJoint = true;
            FixedAttach.connectedPart = otherPart;

            if (!this.part.packed && !otherPart.packed)
            {
                KAS_Shared.DebugLog("AttachFixed(Core) Create fixed joint on " + this.part.partInfo.title + " with " + otherPart.partInfo.title);
                if (FixedAttach.fixedJoint) Destroy(FixedAttach.fixedJoint);
                FixedAttach.fixedJoint = this.part.gameObject.AddComponent<FixedJoint>();
                FixedAttach.fixedJoint.connectedBody = otherPart.rigidbody;
                FixedAttach.fixedJoint.breakForce = breakForce;
                FixedAttach.fixedJoint.breakTorque = breakForce;
            }
            else
            {
                FixedAttach.createJointOnUnpack = true;
                KAS_Shared.DebugWarning("AttachFixed(Core) Cannot create fixed joint as part(s) is packed, delaying to unpack...");
            }
        }

        public void AttachStatic(float breakForce = 10)
        {
            KAS_Shared.DebugLog("JointToStatic(Base) Create kinematic rigidbody");
            if (StaticAttach.connectedGameObject) Destroy(StaticAttach.connectedGameObject);
            GameObject obj = new GameObject("KASBody");
            obj.AddComponent<Rigidbody>();
            obj.rigidbody.isKinematic = true;
            obj.transform.position = this.part.transform.position;
            obj.transform.rotation = this.part.transform.rotation;
            StaticAttach.connectedGameObject = obj;

            KAS_Shared.DebugLog("JointToStatic(Base) Create fixed joint on the kinematic rigidbody");
            if (StaticAttach.fixedJoint) Destroy(StaticAttach.fixedJoint);
            FixedJoint CurJoint = obj.AddComponent<FixedJoint>();
            CurJoint.breakForce = breakForce;
            CurJoint.breakTorque = breakForce;
            CurJoint.connectedBody = this.part.rigidbody;
            StaticAttach.fixedJoint = CurJoint;
            attachMode.StaticJoint = true;
        }

        public void AttachDocked(KASModuleAttachCore otherAttachModule)
        {
            // Save vessel Info
            this.vesselInfo = new DockedVesselInfo();
            this.vesselInfo.name = this.vessel.vesselName;
            this.vesselInfo.vesselType = this.vessel.vesselType;
            this.vesselInfo.rootPartUId = this.vessel.rootPart.flightID;
            this.dockedAttachModule = otherAttachModule;

            otherAttachModule.vesselInfo = new DockedVesselInfo();
            otherAttachModule.vesselInfo.name = otherAttachModule.vessel.vesselName;
            otherAttachModule.vesselInfo.vesselType = otherAttachModule.vessel.vesselType;
            otherAttachModule.vesselInfo.rootPartUId = otherAttachModule.vessel.rootPart.flightID;
            otherAttachModule.dockedAttachModule = this;

            // Set reference
            attachMode.Docked = true;

            // Stop if already docked
            if (otherAttachModule.part.parent == this.part || this.part.parent == otherAttachModule.part)
            {
                KAS_Shared.DebugWarning("DockTo(Core) Parts already docked, nothing more to do");
                return;
            }

            // Reset vessels position and rotation for returning all parts to their original position and rotation before coupling
            this.vessel.SetPosition(this.vessel.transform.position, true);
            this.vessel.SetRotation(this.vessel.transform.rotation);
            otherAttachModule.vessel.SetPosition(otherAttachModule.vessel.transform.position, true);
            otherAttachModule.vessel.SetRotation(otherAttachModule.vessel.transform.rotation);
            
            // Couple depending of mass

            Vessel dominantVessel = GetDominantVessel(this.vessel, otherAttachModule.vessel);
            KAS_Shared.DebugLog("DockTo(Core) Master vessel is " + dominantVessel.vesselName);
            
            if (dominantVessel == this.vessel)
            {
                KAS_Shared.DebugLog("DockTo(Core) Docking " + otherAttachModule.part.partInfo.title + " from " + otherAttachModule.vessel.vesselName + " with " + this.part.partInfo.title + " from " + this.vessel.vesselName);
                if (FlightGlobals.ActiveVessel == otherAttachModule.part.vessel) 
                {
                    KAS_Shared.DebugLog("DockTo(Core) Switching focus to " + this.part.vessel.vesselName);
                    FlightGlobals.ForceSetActiveVessel(this.part.vessel);
                    FlightInputHandler.ResumeVesselCtrlState(this.part.vessel);
                }
                otherAttachModule.part.Couple(this.part);
            }
            else
            {
                KAS_Shared.DebugLog("DockTo(Core) Docking " + this.part.partInfo.title + " from " + this.vessel.vesselName + " with " + otherAttachModule.part.partInfo.title + " from " + otherAttachModule.vessel.vesselName);
                if (FlightGlobals.ActiveVessel == this.part.vessel)
                {
                    KAS_Shared.DebugLog("DockTo(Core) Switching focus to " + otherAttachModule.part.vessel.vesselName);
                    FlightGlobals.ForceSetActiveVessel(otherAttachModule.part.vessel);
                    FlightInputHandler.ResumeVesselCtrlState(otherAttachModule.part.vessel);
                }
                this.part.Couple(otherAttachModule.part);
            }

            GameEvents.onVesselWasModified.Fire(this.part.vessel);
        }

        private Vessel GetDominantVessel(Vessel v1, Vessel v2)
        {
            // Check 1 - Dominant vessel will be the higher type
            if (v1.vesselType > v2.vesselType) return v1;
            if (v1.vesselType < v2.vesselType) return v2;

            // Check 2- If type are the same, dominant vessel will be the heaviest
            float diffMass = Mathf.Abs((v1.GetTotalMass() - v2.GetTotalMass()));
            if (diffMass >= 0.01f)
            {
                if (v1.GetTotalMass() <= v2.GetTotalMass())
                {
                    return v2;
                }
                else
                {
                    return v1;
                }
            }
            // Check 3 - If weight is similar, dominant vessel will be the one with the higher ID
            if (v1.id.CompareTo(v2.id) <= 0)
            {
                return v2;
            }
            else
            {
                return v1;
            }     
        }

        public void Detach()
        {
            if (attachMode.Docked) Detach(AttachType.Docked);
            if (attachMode.Coupled) Detach(AttachType.Coupled);
            if (attachMode.FixedJoint) Detach(AttachType.FixedJoint);
            if (attachMode.StaticJoint) Detach(AttachType.StaticJoint);  
        }

        public void Detach(AttachType attachType)
        {
            KAS_Shared.DebugLog("Detach(Base) Attach type is : " + attachMode);

            // Docked
            if (attachType == AttachType.Docked)
            {
                if (dockedAttachModule.part.parent == this.part)
                {
                    KAS_Shared.DebugLog("Detach(Base) Undocking " + dockedAttachModule.part.partInfo.title + " from " + dockedAttachModule.vessel.vesselName);
                    dockedAttachModule.part.Undock(dockedAttachModule.vesselInfo);
                }
                if (this.part.parent == dockedAttachModule.part)
                {
                    KAS_Shared.DebugLog("Detach(Base) Undocking " + this.part.partInfo.title + " from " + this.vessel.vesselName);
                    this.part.Undock(this.vesselInfo);
                }
                dockedAttachModule.dockedAttachModule = null;
                this.dockedAttachModule = null;
                attachMode.Docked = false;
            }
            // Coupled
            if (attachType == AttachType.Coupled)
            {
                // Todo
                attachMode.Coupled = false;
            }
            // FixedJoint
            if (attachType == AttachType.FixedJoint)
            {
                KAS_Shared.DebugLog("Detach(Base) Removing fixed joint on " + this.part.partInfo.title);
                if (FixedAttach.fixedJoint) Destroy(FixedAttach.fixedJoint);
                FixedAttach.fixedJoint = null;
                FixedAttach.connectedPart = null;
                attachMode.FixedJoint = false;
            }
            // StaticJoint
            if (attachType == AttachType.StaticJoint)
            {
                KAS_Shared.DebugLog("Detach(Base) Removing static rigidbody and fixed joint on " + this.part.partInfo.title);
                if (StaticAttach.fixedJoint) Destroy(StaticAttach.fixedJoint);
                if (StaticAttach.connectedGameObject) Destroy(StaticAttach.connectedGameObject);
                StaticAttach.fixedJoint = null;
                StaticAttach.connectedGameObject = null;
                attachMode.StaticJoint = false;
            }
        }
    }
}
