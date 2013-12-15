using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KAS
{
    public class KASModulePartBay : PartModule
    {
        [KSPField] public string sndStorePath = "KAS/Sounds/hookBayStore";
        [KSPField] public bool allowRelease = true;

        public Dictionary<AttachNode, List<string>> bays = new Dictionary<AttachNode, List<string>>();
        public FXGroup fxSndStore;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (state != StartState.None)
            {
                if (allowRelease)
                {
                    Actions["ActionGroupRelease"].active = true;
                    Events["ContextMenuRelease"].guiActive = true;
                    Events["ContextMenuRelease"].guiActiveUnfocused = true;
                }
                else
                {
                    Actions["ActionGroupRelease"].active = false;
                    Events["ContextMenuRelease"].guiActive = false;
                    Events["ContextMenuRelease"].guiActiveUnfocused = false;
                }
            }

            if (state == StartState.Editor || state == StartState.None) return;
            KAS_Shared.createFXSound(this.part, fxSndStore, sndStorePath, false);
            LoadBays();
        }

        private void LoadBays()
        {
            bays.Clear();
            ConfigNode node = KAS_Shared.GetBaseConfigNode(this);
            foreach (ConfigNode bayNode in node.GetNodes("BAY"))
            {
                if (bayNode.HasValue("attachNode") && bayNode.HasValue("type"))
                {
                    string attachNodeName = bayNode.GetValue("attachNode");
                    AttachNode an = this.part.findAttachNode(attachNodeName);
                    if (an == null)
                    {
                        KAS_Shared.DebugError("LoadBays(PartBay) - Node : " + attachNodeName + " not found !");
                        continue;
                    }
                    List<string> allTypes = new List<string>(bayNode.GetValues("type"));
                    KAS_Shared.AddNodeTransform(this.part, an);
                    bays.Add(an, allTypes);
                }
            }
        }

        [KSPEvent(name = "ContextMenuStore", active = true, guiActive = false, guiActiveUnfocused = true, guiName = "Store")]
        public void ContextMenuStore()
        {
            KASModuleGrab moduleEvaGrab = KAS_Shared.GetGrabbedPartModule(FlightGlobals.ActiveVessel);
            if (!moduleEvaGrab)
            {
                ScreenMessages.PostScreenMessage("You need to grab a part before", 2, ScreenMessageStyle.UPPER_CENTER);
                KAS_Shared.DebugWarning("ContextMenuStore - GetGrabbedPartModule return null !");
                return;
            }
            // Select the nearest compatible bay
            float shorterDist = Mathf.Infinity;
            AttachNode nearestBayNode = null;
            foreach (KeyValuePair<AttachNode, List<string>> bay in bays)
            {
                if (bay.Value != null)
                {
                    if (!bay.Value.Contains(moduleEvaGrab.bayType))
                    {
                        KAS_Shared.DebugWarning("ContextMenuStore - Part type : " + moduleEvaGrab.bayType + " is not allowed | Attach node : " + bay.Key.id);
                        foreach (string type in bay.Value)
                        {
                            KAS_Shared.DebugWarning("ContextMenuStore - Allowed type : " + type);
                        }
                        continue;
                    }
                }
                if (bay.Key.attachedPart)
                {
                    KAS_Shared.DebugWarning("ContextMenuStore - This node are used");
                    continue;
                }
                
                float distToBay = Vector3.Distance(FlightGlobals.ActiveVessel.transform.position, bay.Key.nodeTransform.position);
                if (distToBay <= shorterDist)
                {
                    shorterDist = distToBay;
                    nearestBayNode = bay.Key;
                }
            }

            if (nearestBayNode == null)
            {
                ScreenMessages.PostScreenMessage("Part is not compatible or there is no free space", 2, ScreenMessageStyle.UPPER_CENTER);
                KAS_Shared.DebugWarning("ContextMenuStore - Part is not compatible or there is no free space");
                return;
            }

            AttachNode grabbedPartAn = moduleEvaGrab.part.findAttachNode(moduleEvaGrab.bayNode);
            if (grabbedPartAn == null)
            {
                KAS_Shared.DebugError("ContextMenuStore - Grabbed part bay node not found !");
                return;
            }

            KAS_Shared.DebugLog("ContextMenuStore - Drop part...");
            moduleEvaGrab.Drop();

            KAS_Shared.DebugLog("ContextMenuStore - Add node transform if not exist...");
            KAS_Shared.AddNodeTransform(moduleEvaGrab.part, grabbedPartAn);

            KAS_Shared.DebugLog("ContextMenuStore - Move part...");
            KAS_Shared.MoveAlign(moduleEvaGrab.part.transform, grabbedPartAn.nodeTransform, nearestBayNode.nodeTransform);
            moduleEvaGrab.part.transform.rotation *= Quaternion.Euler(moduleEvaGrab.bayRot);

            //Couple part with bay
            KAS_Shared.DebugLog("ContextMenuStore - Couple part with bay...");
            moduleEvaGrab.part.Couple(this.part);
            nearestBayNode.attachedPart = moduleEvaGrab.part;

            fxSndStore.audio.Play();
            moduleEvaGrab.part.SendMessage("OnBayStore", SendMessageOptions.DontRequireReceiver);
        }

        [KSPEvent(name = "ContextMenuRelease", active = true, guiActive = true, guiActiveUnfocused = true, guiName = "Release")]
        public void ContextMenuRelease()
        {
            foreach (KeyValuePair<AttachNode, List<string>> bay in bays)
            {
                if (bay.Key.attachedPart)
                {
                    Part tmpPart = bay.Key.attachedPart;
                    KAS_Shared.DecoupleFromAll(bay.Key.attachedPart);
                    Physics.IgnoreCollision(tmpPart.collider, this.part.collider);                  
                }
            }
        }

        [KSPAction("Release")]
        public void ActionGroupRelease(KSPActionParam param)
        {
            if (!this.part.packed)
            {
                ContextMenuRelease();
            }
        }
    }
}
