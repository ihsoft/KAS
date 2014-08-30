using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KAS
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class KASAddonAddModule : MonoBehaviour
    {
        public void Awake()
        {
            ConfigNode node = ConfigNode.Load(KSPUtil.ApplicationRootPath + "GameData/KAS/addModule.cfg") ?? new ConfigNode();
            AddGrabModule(node);
        }

        private void AddGrabModule(ConfigNode node)
        {
            foreach (ConfigNode grabNode in node.GetNodes("GRAB"))
            {
                // Check if the node has value
                if (!grabNode.HasValue("stockPartName"))
                {
                    KAS_Shared.DebugWarning("AddGrabModule(AddModule) Missing stockPartName node !");
                    continue;
                }
                // Add and Retrieve the module
                string partName = grabNode.GetValue("stockPartName").Replace('_', '.');
                AvailablePart aPart = PartLoader.getPartInfoByName(partName);
                if (aPart == null)
                {
                    KAS_Shared.DebugError("AddModule(AddModule) - " + partName + " not found in partloader");
                    continue;
                }

                // get or add grab module
                KASModuleGrab grabModule = aPart.partPrefab.GetComponent<KASModuleGrab>();
                if (grabModule)
                {
                    KAS_Shared.DebugWarning("AddModule(AddModule) - KASModuleGrab already added to " + partName);
                }
                else
                {
                    grabModule = aPart.partPrefab.AddModule("KASModuleGrab") as KASModuleGrab;
                    if (!grabModule)
                    {
                        KAS_Shared.DebugError("AddGrabModule(AddModule) Error when adding module !");
                        continue;
                    }
                }

                // Configure the module
                if (grabNode.HasValue("evaPartPos")) grabModule.evaPartPos = KAS_Shared.ParseCfgVector3(grabNode.GetValue("evaPartPos"));
                if (grabNode.HasValue("evaPartDir")) grabModule.evaPartDir = KAS_Shared.ParseCfgVector3(grabNode.GetValue("evaPartDir"));
                if (grabNode.HasValue("customGroundPos")) grabModule.customGroundPos = bool.Parse(grabNode.GetValue("customGroundPos"));
                if (grabNode.HasValue("dropPartPos")) grabModule.dropPartPos = KAS_Shared.ParseCfgVector3(grabNode.GetValue("dropPartPos"));
                if (grabNode.HasValue("dropPartRot")) grabModule.dropPartRot = KAS_Shared.ParseCfgVector3(grabNode.GetValue("dropPartRot"));
                if (grabNode.HasValue("physicJoint")) grabModule.physicJoint = bool.Parse(grabNode.GetValue("physicJoint"));
                if (grabNode.HasValue("addPartMass")) grabModule.addPartMass = bool.Parse(grabNode.GetValue("addPartMass"));
                if (grabNode.HasValue("storable")) grabModule.storable = bool.Parse(grabNode.GetValue("storable"));
                if (grabNode.HasValue("stateless")) grabModule.stateless = bool.Parse(grabNode.GetValue("stateless"));
                if (grabNode.HasValue("storedSize")) grabModule.storedSize = int.Parse(grabNode.GetValue("storedSize"));
                if (grabNode.HasValue("bayType")) grabModule.bayType = grabNode.GetValue("bayType").ToString();
                if (grabNode.HasValue("bayNode")) grabModule.bayNode = grabNode.GetValue("bayNode").ToString();
                if (grabNode.HasValue("bayRot")) grabModule.bayRot = KAS_Shared.ParseCfgVector3(grabNode.GetValue("bayRot"));
                if (grabNode.HasValue("grabSndPath")) grabModule.grabSndPath = grabNode.GetValue("grabSndPath").ToString();
                if (grabNode.HasValue("attachMaxDist")) grabModule.attachMaxDist = float.Parse(grabNode.GetValue("attachMaxDist"));
                if (grabNode.HasValue("attachOnPart")) grabModule.attachOnPart = bool.Parse(grabNode.GetValue("attachOnPart"));
                if (grabNode.HasValue("attachOnEva")) grabModule.attachOnEva = bool.Parse(grabNode.GetValue("attachOnEva"));
                if (grabNode.HasValue("attachOnStatic")) grabModule.attachOnStatic = bool.Parse(grabNode.GetValue("attachOnStatic"));
                if (grabNode.HasValue("attachSendMsgOnly")) grabModule.attachSendMsgOnly = bool.Parse(grabNode.GetValue("attachSendMsgOnly"));
                if (grabNode.HasValue("attachPartSndPath")) grabModule.attachPartSndPath = grabNode.GetValue("attachPartSndPath").ToString();
                if (grabNode.HasValue("attachStaticSndPath")) grabModule.attachStaticSndPath = grabNode.GetValue("attachStaticSndPath").ToString();
                if (grabNode.HasValue("detachSndPath")) grabModule.detachSndPath = grabNode.GetValue("detachSndPath").ToString();
                KAS_Shared.DebugLog("AddGrabModule(AddModule) Module successfully configured on " + partName);
            }
        }
    }
}
