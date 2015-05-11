using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KIS;

namespace KAS
{
    public class KASModuleStrut : KASModuleAttachCore
    {
        [KSPField] public string type = "default";
        [KSPField] public string nodeTransform = null;
        [KSPField] public float maxLenght = 4f;
        [KSPField] public float maxAngle = 0.10f;
        [KSPField] public bool allowDock = false;
        [KSPField] public bool allowPumpFuel = false;
        [KSPField] public float breakForce = 15f;
        [KSPField] public float tubeScale = 0.15f;
        [KSPField] public float jointScale = 0.15f;
        [KSPField] public string tubeSrcType = "Joined";
        [KSPField] public string tubeTgtType = "Joined";
        [KSPField] public float textureTiling = 4;
        [KSPField] public bool hasCollider = false;
        [KSPField] public string tubeTexPath = "KAS/Textures/strut";
        [KSPField] public string sndLinkPath = "KAS/Sounds/strutBuild";
        [KSPField] public string sndUnlinkPath = "KAS/Sounds/strutRemove";
        [KSPField] public string sndBrokePath = "KAS/Sounds/broke";
        [KSPField] public Vector3 evaStrutPos = new Vector3(0f, 0.03f, -0.24f);
        [KSPField] public Vector3 evaStrutRot = new Vector3(270f, 0f, 0f);

        [KSPField(isPersistant=true)]
        public bool pumpFuel = false;

        public KAS_Tube strutRenderer;

        private bool linkValid = true;
        public  bool linked = false;
        public KASModuleStrut linkedStrutModule = null;
        public Vessel linkedEvaVessel = null;

        private Part pumpFrom, pumpTo;

        public FXGroup fxSndLink, fxSndUnlink, fxSndBroke;
   
        private Texture2D texStrut;
        private Transform evaStrutTransform;

        private Transform strutTransform
        {
            get { return this.part.FindModelTransform(nodeTransform); }
        }

        public override string GetInfo()
        {
            var sb = new StringBuilder();
            sb.Append("<b>Compatibility</b>: "); sb.AppendLine(type);
            sb.AppendFormat("<b>Maximum length</b>: {0:F0}m", maxLenght); sb.AppendLine();
            sb.AppendFormat("<b>Maximum angle</b>: {0:F0}", maxAngle); sb.AppendLine();
            sb.AppendFormat("<b>Strength</b>: {0:F0}", breakForce); sb.AppendLine();
            if (allowDock)
            {
                sb.AppendLine();
                sb.AppendLine("Can be linked to another vessel.");
            }
            return sb.ToString();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (state == StartState.Editor || state == StartState.None) return;
            //Loading texture
            texStrut = GameDatabase.Instance.GetTexture(tubeTexPath, false);
            if (!texStrut)
            {
                KAS_Shared.DebugError("tube texture loading error !");
                ScreenMessages.PostScreenMessage("Texture file : " + tubeTexPath + " as not been found, please check your KAS installation !", 10, ScreenMessageStyle.UPPER_CENTER);
            }
            // loading sounds
            KAS_Shared.createFXSound(this.part, fxSndLink, sndLinkPath, false);
            KAS_Shared.createFXSound(this.part, fxSndUnlink, sndUnlinkPath, false);
            KAS_Shared.createFXSound(this.part, fxSndBroke, sndBrokePath, false);

            // loading strut renderer
            strutRenderer = this.part.gameObject.AddComponent<KAS_Tube>();
            strutRenderer.tubeTexTilingOffset = textureTiling;
            strutRenderer.tubeScale = tubeScale;
            strutRenderer.sphereScale = jointScale;
            strutRenderer.tubeTexture = texStrut;
            strutRenderer.sphereTexture = texStrut;
            strutRenderer.tubeJoinedTexture = texStrut;
            strutRenderer.srcNode = strutTransform;

            // loading tube type
            switch (tubeSrcType)
            {
                case "None":
                    strutRenderer.srcJointType = KAS_Tube.tubeJointType.None;
                    break;
                case "Rounded":
                    strutRenderer.srcJointType = KAS_Tube.tubeJointType.Rounded;
                    break;
                case "ShiftedAndRounded":
                    strutRenderer.srcJointType = KAS_Tube.tubeJointType.ShiftedAndRounded;
                    break;
                case "Joined":
                    strutRenderer.srcJointType = KAS_Tube.tubeJointType.Joined;
                    break;
                default:
                    strutRenderer.srcJointType = KAS_Tube.tubeJointType.Joined;
                    break;
            }
            switch (tubeTgtType)
            {
                case "None":
                    strutRenderer.tgtJointType = KAS_Tube.tubeJointType.None;
                    break;
                case "Rounded":
                    strutRenderer.tgtJointType = KAS_Tube.tubeJointType.Rounded;
                    break;
                case "ShiftedAndRounded":
                    strutRenderer.tgtJointType = KAS_Tube.tubeJointType.ShiftedAndRounded;
                    break;
                case "Joined":
                    strutRenderer.tgtJointType = KAS_Tube.tubeJointType.Joined;
                    break;
                default:
                    strutRenderer.tgtJointType = KAS_Tube.tubeJointType.Joined;
                    break;
            }

            // Reset link if docked
            if (attachMode.Docked && !linked)
            {
                KAS_Shared.DebugLog("OnStart(strut) Docked strut detected from save, relinking...");
                KASModuleStrut linkedStrutModuleSavedD = dockedAttachModule.GetComponent<KASModuleStrut>();
                LinkTo(linkedStrutModuleSavedD, false, true);
            }

            // Loading onVesselWasModified KSP event
            GameEvents.onVesselWasModified.Add(new EventData<Vessel>.OnEvent(this.OnVesselWasModified));
        }

        protected override void InitFixedAttach()
        {
            base.InitFixedAttach();

            // Reset link if a fixed joint exist
            if (attachMode.FixedJoint)
            {
                KAS_Shared.DebugLog("OnStart(strut) Docked / fixed joint detected from save, relinking...");
                KASModuleStrut linkedStrutModuleSavedF = FixedAttach.connectedPart.GetComponent<KASModuleStrut>();
                LinkTo(linkedStrutModuleSavedF, false, true);
            }
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (!HighLogic.LoadedSceneIsFlight) return;
            if (linkedEvaVessel)
            {
                // Check if cancel key pressed
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return))
                {
                    StopEvaLink();
                    return;
                }
                // Check link state
                bool tmpLinkValid = CheckLink(strutRenderer.srcNode, strutRenderer.tgtNode, false);
                if (tmpLinkValid)
                {
                    if (!linkValid)
                    {
                        strutRenderer.SetColor(Color.green);
                        linkValid = true;
                    }
                }
                else
                {
                    if (linkValid)
                    {
                        strutRenderer.SetColor(Color.red);
                        linkValid = false;
                    }
                }
            }
        }

        void OnVesselWasModified(Vessel vesselModified)
        {
            if (vesselModified != this.vessel) return;

            if (linked)
            {
                if (linkedStrutModule.vessel != this.vessel)
                {
                    if (allowDock)
                    {
                        KAS_Shared.DebugWarning("OnVesselWasModified(strut) Source and target vessel are different, postponing docking strut... (allowDock = true)");
                        // This callback is invoked while the vessel is being
                        // modified, so any further changes must be postponed.
                        StartCoroutine(WaitAndRedock());
                    }
                    else
                    {
                        KAS_Shared.DebugWarning("OnVesselWasModified(strut) Source and target vessel are different, unlinking strut... (allowDock = false)");
                        Unlink();
                        fxSndBroke.audio.Play();
                    }
                }
                else if (pumpTo && (!pumpFrom || pumpFrom.State == PartStates.DEAD || pumpTo.vessel != pumpFrom.vessel))
                {
                    StopPump();
                }
            }        
        }

        private IEnumerator<YieldInstruction> WaitAndRedock()
        {
            yield return null;

            // If still ok, we can redock now
            if (part && vessel && linked && linkedStrutModule.vessel != this.vessel && allowDock)
            {
                KAS_Shared.DebugWarning("WaitAndRedock(strut) Source and target vessel are different, docking strut... (allowDock = true)");
                KASModuleStrut tmpLinkedStrutMod = linkedStrutModule;
                Unlink();
                LinkTo(tmpLinkedStrutMod, false);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            UnlinkPump();

            GameEvents.onVesselWasModified.Remove(new EventData<Vessel>.OnEvent(this.OnVesselWasModified));
        }

        protected override void OnPartDie()
        {
            base.OnPartDie();

            if (linked)
            {
                Unlink();
            }
        }

        public override void OnJointBreakFixed()
        {
            KAS_Shared.DebugWarning("OnJointBreak(Strut) A joint broken on " + part.partInfo.title + " !, force: " + breakForce);
            Unlink();
            fxSndBroke.audio.Play();
        }

        public void OnKISAction(KIS_Shared.MessageInfo messageInfo)
        {
            if (messageInfo.action == KIS_Shared.MessageAction.Store || messageInfo.action == KIS_Shared.MessageAction.AttachStart || messageInfo.action == KIS_Shared.MessageAction.DropEnd)
            {
                if (linked) fxSndBroke.audio.Play();
                StopEvaLink();
                Unlink();
                StopPump();
            }
        }

        private void SetEvaLink()
        {
            //Check condition
            if (!FlightGlobals.ActiveVessel.isEVA)
            {
                ScreenMessages.PostScreenMessage("Active vessel is not eva !", 5, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            if (linked)
            {
                ScreenMessages.PostScreenMessage(this.part.partInfo.title + " is already linked !", 5, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            StopEvaLink();

            evaStrutTransform = new GameObject("KASEvaStrutNode").transform;
            evaStrutTransform.position = FlightGlobals.ActiveVessel.transform.TransformPoint(evaStrutPos);
            evaStrutTransform.rotation = FlightGlobals.ActiveVessel.transform.rotation * Quaternion.Euler(evaStrutRot);
            evaStrutTransform.parent = FlightGlobals.ActiveVessel.transform;

            strutRenderer.shaderName = "Transparent/Diffuse";
            strutRenderer.tgtNode = evaStrutTransform;
            strutRenderer.color = Color.green;
            strutRenderer.color.a = 0.5f;
            strutRenderer.Load();
            linkedEvaVessel = FlightGlobals.ActiveVessel;
            InputLockManager.SetControlLock(ControlTypes.PAUSE, "KASStrutLink");
        }

        private void StopEvaLink()
        {
            strutRenderer.UnLoad();
            linkedEvaVessel = null;
            if (evaStrutTransform) Destroy(evaStrutTransform.gameObject);
            InputLockManager.RemoveControlLock("KASStrutLink");
        }

        private bool LinkTo(KASModuleStrut tgtModule, bool checkCondition = true, bool setJointOrDock = true)
        {
            //Check condition if needed
            if (checkCondition)
            {
                if (!CheckLink(this.strutTransform, tgtModule.strutTransform, true))
                {
                    ScreenMessages.PostScreenMessage("Max angle or length reached, cannot link !", 5, ScreenMessageStyle.UPPER_CENTER);
                    return false;
                }

                if (tgtModule == this)
                {
                    ScreenMessages.PostScreenMessage(this.part.partInfo.title + " cannot be linked to itself !", 5, ScreenMessageStyle.UPPER_CENTER);
                    return false;
                }

                if (tgtModule.type != this.type)
                {
                    ScreenMessages.PostScreenMessage(this.part.partInfo.title + " cannot be linked to " + tgtModule.part.partInfo.title + " because they are not compatible !", 5, ScreenMessageStyle.UPPER_CENTER);
                    return false;
                }

                if (tgtModule.vessel != this.vessel && !allowDock)
                {
                    ScreenMessages.PostScreenMessage(this.part.partInfo.title + " cannot be linked to another vessel !", 5, ScreenMessageStyle.UPPER_CENTER);
                    return false;
                }
            }

            // Load tube renderer in this module
            this.StopEvaLink();
            this.strutRenderer.tgtNode = tgtModule.strutTransform;
            this.strutRenderer.shaderName = "Diffuse";
            this.strutRenderer.color = Color.white;
            this.strutRenderer.color.a = 1f;
            this.strutRenderer.Load();

            // Set references for the current module
            this.Events["ContextMenuUnlink"].guiActiveUnfocused = true;
            this.Events["ContextMenuLink"].guiActiveUnfocused = false;
            this.linkedStrutModule = tgtModule;
            this.linked = true;

            // Set references for the target module
            tgtModule.linkedStrutModule = this;
            tgtModule.Events["ContextMenuUnlink"].guiActiveUnfocused = true;
            tgtModule.Events["ContextMenuLink"].guiActiveUnfocused = false;
            tgtModule.linked = true;

            KAS_Shared.InvalidateContextMenu(this.part);
            KAS_Shared.InvalidateContextMenu(tgtModule.part);

            if (setJointOrDock)
            {
                // Create joint or dock part
                if (tgtModule.vessel == this.vessel)
                {
                    if (tgtModule.part.parent != this.part && this.part.parent != tgtModule.part)
                    {
                        KAS_Shared.DebugLog("LinkTo(Strut) Parts are from the same vessel but are not connected, setting joint...");
                        AttachFixed(tgtModule.part, breakForce);
                    }
                }
                else
                {
                    KAS_Shared.DebugLog("LinkTo(Strut) Parts are from a different vessel, docking...");
                    AttachDocked(tgtModule);
                }
            }
            else
            {
                KAS_Shared.DebugLog("LinkTo(Strut) setJointOrDock = false, ignoring dock and creating joint");
            }

            // Connect fuel flow when appropriate
            bool both_attached = this.part.srfAttachNode.attachedPart && tgtModule.part.srfAttachNode.attachedPart;

            this.Events["ContextMenuTogglePump"].active = this.allowPumpFuel && both_attached;
            if (this.pumpFuel)
            {
                this.StartPump(checkCondition);
            }

            tgtModule.Events["ContextMenuTogglePump"].active = tgtModule.allowPumpFuel && both_attached;
            if (tgtModule.pumpFuel)
            {
                tgtModule.StartPump(checkCondition);
            }

            return true;
        }

        private void StartPump(bool from_ui)
        {
            StopPump();

            if (!linkedStrutModule || linkedStrutModule.part.vessel != part.vessel)
            {
                if (from_ui)
                {
                    ScreenMessages.PostScreenMessage("Can't pump when not connected to the same vessel!", 3, ScreenMessageStyle.UPPER_CENTER);
                }

                return;
            }

            Part target = part.srfAttachNode.attachedPart;
            Part source = linkedStrutModule.part.srfAttachNode.attachedPart;

            if (!target || !source)
            {
                if (from_ui)
                {
                    ScreenMessages.PostScreenMessage("Can't pump when an end is dangling!", 3, ScreenMessageStyle.UPPER_CENTER);
                }

                return;
            }

            pumpTo = target;
            pumpFrom = source;
            pumpTo.fuelLookupTargets.Add(pumpFrom);
            pumpFuel = true;
            Events["ContextMenuTogglePump"].guiName = "Stop Pumping";
            KAS_Shared.InvalidateContextMenu(this.part);
        }

        private void StopPump()
        {
            UnlinkPump();
            pumpFuel = false;
            Events["ContextMenuTogglePump"].guiName = "Pump Here";
            KAS_Shared.InvalidateContextMenu(this.part);
        }

        private void UnlinkPump()
        {
            if (pumpTo)
            {
                pumpTo.fuelLookupTargets.Remove(pumpFrom);
            }
            pumpTo = pumpFrom = null;
        }

        private void Unlink()
        {
            // Unload tube renderer
            if (linkedStrutModule)
            {
                linkedStrutModule.UnlinkPump();
                linkedStrutModule.strutRenderer.UnLoad();
                linkedStrutModule.linked = false;
                linkedStrutModule.Events["ContextMenuUnlink"].guiActiveUnfocused = false;
                linkedStrutModule.Events["ContextMenuLink"].guiActiveUnfocused = true;
                linkedStrutModule.Events["ContextMenuTogglePump"].active = false;
                KAS_Shared.InvalidateContextMenu(linkedStrutModule.part);
            }
            this.UnlinkPump();
            this.strutRenderer.UnLoad();
            this.linked = false;
            this.Events["ContextMenuUnlink"].guiActiveUnfocused = false;
            this.Events["ContextMenuLink"].guiActiveUnfocused = true;
            this.Events["ContextMenuTogglePump"].active = false;
            KAS_Shared.InvalidateContextMenu(this.part);
            // Detach parts
            if (linkedStrutModule) linkedStrutModule.Detach();
            this.Detach();
            // Clean references
            if (linkedStrutModule) linkedStrutModule.linkedStrutModule = null;
            this.linkedStrutModule = null;
        }
         
        private bool CheckLink(Transform srcTransform, Transform tgtTransform, bool checkTgtAngle)
        {
            bool maxLenghtReached = false;
            bool maxAngleReached = false;
            // Check max lenght
            if (Vector3.Distance(srcTransform.position, tgtTransform.position) > maxLenght)
            {
                maxLenghtReached = true;
            }
            else
            {
                maxLenghtReached = false;
            }
            // Check max angle
            if (KAS_Shared.GetAngleFromDirAndPoints(srcTransform.forward, srcTransform.position, tgtTransform.position) > maxAngle)
            {
                maxAngleReached = true;
            }
            if (checkTgtAngle)
            {
                if (KAS_Shared.GetAngleFromDirAndPoints(tgtTransform.forward, tgtTransform.position, srcTransform.position) > maxAngle)
                {
                    maxAngleReached = true;
                }
            }
            // Set color related to checks
            if (maxLenghtReached || maxAngleReached)
            //if (maxLenghtReached)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private KASModuleStrut GetEvaLinkedStrutModule(Vessel evaVessel)
        {
            List<KASModuleStrut> allStrut = new List<KASModuleStrut>(GameObject.FindObjectsOfType(typeof(KASModuleStrut)) as KASModuleStrut[]);
            foreach (KASModuleStrut strut in allStrut)
            {
                if (strut.linkedEvaVessel == evaVessel)
                {
                    return strut;
                }
            }
            return null;
        }

        [KSPEvent(name = "ContextMenuTogglePump", active = false, guiActiveUnfocused = true, guiActive = true, unfocusedRange = 2f, guiName = "Pump Here")]
        public void ContextMenuTogglePump()
        {
            if (pumpFuel)
            {
                StopPump();
            }
            else
            {
                StartPump(true);
            }
        }

        [KSPEvent(name = "ContextMenuLink", active = true, guiActiveUnfocused = true, guiActive = false, unfocusedRange = 2f, guiName = "Link")]
        public void ContextMenuLink()
        {
            KASModulePort portModule = this.part.GetComponent<KASModulePort>();
            if (portModule)
            {
                if (portModule.plugged)
                {
                    ScreenMessages.PostScreenMessage("Cannot link, port is already used", 5, ScreenMessageStyle.UPPER_CENTER);
                    return;
                }
            }
            KASModuleStrut EvaLinkedStrutModule = GetEvaLinkedStrutModule(FlightGlobals.ActiveVessel);
            if (EvaLinkedStrutModule)
            {
                if (EvaLinkedStrutModule.LinkTo(this)) fxSndLink.audio.Play();           
            }
            else
            {
                SetEvaLink();
                ScreenMessages.PostScreenMessage("Link mode enabled, press Escape or Enter to cancel", 10, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        [KSPEvent(name = "ContextMenuUnlink", active = true, guiActiveUnfocused = false, guiActive = false, unfocusedRange = 2f, guiName = "Unlink")]
        public void ContextMenuUnlink()
        {
            Unlink();
            this.fxSndUnlink.audio.Play();
        }

    }
}
