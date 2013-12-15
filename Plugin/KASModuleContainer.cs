using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using UnityEngine;

namespace KAS
{
    public class KASModuleContainer : PartModule
    {
        [KSPField] public float maxSize = 10f;
        [KSPField] public float maxOpenDistance = 2f;
        [KSPField] public string sndStorePath = "KAS/Sounds/hookBayStore";
        [KSPField] public string sndOpenPath = "KAS/Sounds/containerOpen";
        [KSPField] public string sndClosePath = "KAS/Sounds/containerClose";
        [KSPField] public string bipWrongSndPath = "KAS/Sounds/bipwrong";

        public FXGroup fxSndStore, fxSndOpen, fxSndClose, fxSndBipWrong;

        public Dictionary<AvailablePart, int> availableContents = new Dictionary<AvailablePart, int>();
        public Dictionary<AvailablePart, int> contents = new Dictionary<AvailablePart, int>();

        private KASModuleContainer exchangeContainer = null;
        public float totalSize = 0;
        private bool waitAndGrabRunning = false;
        private float orgMass;

        private GUIStyle guiButtonStyle, guiDataboxStyle, guiCenterStyle, guiBoldCyanCenterStyle, guiTooltipStyle;
        public Rect guiMainWindowPos;
        private Vector2 scrollPos1 = Vector2.zero;
        private Vector2 scrollPos2 = Vector2.zero;
        private EditTab activeEditTab = EditTab.All;
        public enum EditTab
        {
            None = -1,
            All = 0,
            Propulsion = 1,
            Control = 2,
            Structural = 3,
            Aero = 4,
            Utility = 5,
            Science = 6,
            Pods = 7,
        }

        private guiMode showGUI = guiMode.None;
        public enum guiMode
        {
            None = 0,
            Edit = 1,
            Take = 2,
            Exchange = 3,
            View = 4,
        }

        public enum ShowButton
        {
            None = 0,
            Add = 1,
            Remove = 2,
            Take = 3,
            Move = 4,
        }

        public override string GetInfo()
        {
            return String.Format("<b>Capacity</b>: {0:F0}", maxSize);
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            foreach (KeyValuePair<AvailablePart, int> ct in contents)
            {
                ConfigNode nodeD = node.AddNode("CONTENT");
                nodeD.AddValue("name", ct.Key.name);
                nodeD.AddValue("qty", ct.Value);
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (node.HasNode("CONTENT"))
            {
                contents.Clear();
            }

            foreach (ConfigNode cn in node.GetNodes("CONTENT"))
            {
                if (cn.HasValue("name") && cn.HasValue("qty"))
                {
                    string AvPartName = cn.GetValue("name").ToString();
                    AvailablePart avPart = null;
                    avPart = PartLoader.getPartInfoByName(AvPartName);
                    int qty = int.Parse(cn.GetValue("qty"));
                    if (avPart != null)
                    {
                        contents.Add(avPart, qty);             
                    }
                    else
                    {
                        KAS_Shared.DebugError("Load(Container) - Cannot retrieve " + AvPartName + " from PartLoader !");
                    }
                }
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (state == StartState.None) return;
            KAS_Shared.createFXSound(this.part, fxSndStore, sndStorePath, false);
            KAS_Shared.createFXSound(this.part, fxSndOpen, sndOpenPath, false);
            KAS_Shared.createFXSound(this.part, fxSndClose, sndClosePath, false);
            KAS_Shared.createFXSound(this.part, fxSndBipWrong, bipWrongSndPath, false);
            GameEvents.onVesselChange.Add(new EventData<Vessel>.OnEvent(this.OnVesselChange));
            orgMass = this.part.partInfo.partPrefab.mass;
            RefreshTotalSize();
        }

        void OnVesselChange(Vessel vess)
        {
            if (showGUI != guiMode.None)
            {
                CloseAllGUI();
            }
        }

        void OnDestroy()
        {
            GameEvents.onVesselChange.Remove(new EventData<Vessel>.OnEvent(this.OnVesselChange));
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (!HighLogic.LoadedSceneIsFlight) return;
            if (showGUI == guiMode.Take)
            {
                float distEvaToContainer = Vector3.Distance(FlightGlobals.ActiveVessel.transform.position, this.part.transform.position);
                if (distEvaToContainer > maxOpenDistance)
                {
                    CloseAllGUI();
                }
            }
        }

        public Dictionary<AvailablePart, int> GetContent()
        {
            Dictionary<AvailablePart, int> returnContents = new Dictionary<AvailablePart, int>();

            foreach (KeyValuePair<AvailablePart, int> ct in contents)
            {
                returnContents.Add(ct.Key,ct.Value);
            }
            return returnContents;
        }

        private Dictionary<AvailablePart, int> GetAvailableContents(PartCategories category)
        {
            Dictionary<AvailablePart, int> edct = new Dictionary<AvailablePart, int>();
            foreach (AvailablePart avPart in PartLoader.LoadedPartsList)
            {
                KASModuleGrab grabModule = avPart.partPrefab.GetComponent<KASModuleGrab>();
                if (grabModule)
                {
                    if (grabModule.storable)
                    {
                        if (avPart.category == category)
                        {
                            edct.Add(avPart, 0);
                        }
                    }
                }
            }
            return edct;
        }

        private Dictionary<AvailablePart, int> GetAvailableContents()
        {
            Dictionary<AvailablePart, int> edct = new Dictionary<AvailablePart, int>();
            foreach (AvailablePart avPart in PartLoader.LoadedPartsList)
            {
                if (!ResearchAndDevelopment.PartModelPurchased(avPart)) { continue; }
                KASModuleGrab grabModule = avPart.partPrefab.GetComponent<KASModuleGrab>();
                if (grabModule)
                {
                    if (grabModule.storable)
                    {
                        edct.Add(avPart, 0);                    
                    }
                }
            }
            return edct;
        }

        private void Add(AvailablePart avPart, int qty)
        {
            if (contents.ContainsKey(avPart))
            {
                contents[avPart] += qty;
            }
            else
            {
                contents.Add(avPart,qty);       
            }
            RefreshTotalSize();
        }

        private void Remove(AvailablePart avPart, int qty)
        {
            if (contents.ContainsKey(avPart))
            {
                if (contents[avPart] - 1 > 0)
                {
                    contents[avPart] -= qty;
                }
                else
                {
                    contents.Remove(avPart);
                }
                RefreshTotalSize();
            }
            else
            {
                KAS_Shared.DebugLog("Remove(Container) - Nothing to remove");
            }
        }

        private void RefreshTotalSize()
        {
            totalSize = 0;
            this.part.mass = orgMass;
            foreach (KeyValuePair<AvailablePart, int> contentPart in contents)
            {
                KASModuleGrab grabModule = contentPart.Key.partPrefab.GetComponent<KASModuleGrab>();
                if (grabModule)
                {
                    totalSize += contentPart.Value * grabModule.storedSize;
                    this.part.mass += contentPart.Value * contentPart.Key.partPrefab.mass;
                }
            } 
        }

        private bool MaxSizeReached(AvailablePart avPart, int qty)
        {
            KASModuleGrab moduleGrab = avPart.partPrefab.GetComponent<KASModuleGrab>();
            if (moduleGrab)
            {
                if (totalSize + (moduleGrab.storedSize * qty) > maxSize)
                {
                    return true;
                }
            }
            return false;
        }

        private float GetPartSize(AvailablePart avPart)
        {
            KASModuleGrab grabModule = avPart.partPrefab.GetComponent<KASModuleGrab>();
            if (grabModule)
            {
                return grabModule.storedSize;
            }
            else
            {
                KAS_Shared.DebugError("Cannot retrieve part size, grab module not found !");
                return 0;
            }  
        }

        private void Take(AvailablePart avPart)
        {
            if (waitAndGrabRunning)
            {
                KAS_Shared.DebugError("Take(Container) Take action is already running, please wait !");
                return;
            }
            KASModuleGrab prefabGrabModule = avPart.partPrefab.GetComponent<KASModuleGrab>();
            if (!prefabGrabModule)
            {
                KAS_Shared.DebugError("Take(Container) Can't find the prefab grab module !");
                return;
            }
            // get grabbed position and rotation
            Vector3 pos = FlightGlobals.ActiveVessel.rootPart.transform.TransformPoint(prefabGrabModule.evaPartPos);
            Quaternion rot = FlightGlobals.ActiveVessel.rootPart.transform.rotation * Quaternion.Euler(prefabGrabModule.evaPartDir);

            //Move away the part at creation
            pos += new Vector3(0f, 0f, 100);

            //Part newPart = KAS_Shared.CreatePart(avPart, pos, rot, this.part);
            Part newPart = KAS_Shared.CreatePart(avPart.name, pos, rot, this.part);
            if (!newPart)
            {
                KAS_Shared.DebugError("Take(Container) failed to create the part !");
                return;
            }

            KASModuleGrab moduleGrab = newPart.GetComponent<KASModuleGrab>();
            if (!moduleGrab)
            {
                KAS_Shared.DebugError("Take(Container) Cannot grab the part taken, no grab module found !");
                return;
            }
            Remove(avPart, 1);
            StartCoroutine(WaitAndGrab(moduleGrab, FlightGlobals.ActiveVessel));
        }

        private IEnumerator WaitAndGrab(KASModuleGrab moduleGrab, Vessel vess)
        {
            waitAndGrabRunning = true;
            while (!moduleGrab.part.rigidbody)
            {
                KAS_Shared.DebugLog("WaitAndGrab(Container) - Waiting rigidbody to initialize...");
                yield return new WaitForFixedUpdate();
            }
            KAS_Shared.DebugLog("WaitAndGrab(Container) - Rigidbody initialized, setting velocity...");
            moduleGrab.part.rigidbody.velocity = vess.rootPart.rigidbody.velocity;
            moduleGrab.part.rigidbody.angularVelocity = vess.rootPart.rigidbody.angularVelocity;
            KAS_Shared.DebugLog("WaitAndGrab(Container) - Waiting velocity to apply by waiting 0.1 seconds...");
            yield return new WaitForSeconds(0.1f);
            KAS_Shared.DebugLog("WaitAndGrab(Container) - Grab part...");
            if (!moduleGrab.evaHolderPart) moduleGrab.Grab(vess);
            KAS_Shared.DebugLog("WaitAndGrab(Container) - End of coroutine...");
            waitAndGrabRunning = false;
        }

        private void StoreGrabbedPart()
        {
            KASModuleGrab moduleGrab = KAS_Shared.GetGrabbedPartModule(FlightGlobals.ActiveVessel);
            if (!moduleGrab)
            {
                fxSndBipWrong.audio.Play();
                ScreenMessages.PostScreenMessage("You didn't grab anything to store !", 5, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            if (!moduleGrab.storable)
            {
                fxSndBipWrong.audio.Play();
                ScreenMessages.PostScreenMessage("This part cannot be stored !", 5, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            if (MaxSizeReached(moduleGrab.part.partInfo,1))
            {
                fxSndBipWrong.audio.Play();
                ScreenMessages.PostScreenMessage("Max size of the container reached !", 5, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            Add(moduleGrab.part.partInfo, 1);
            moduleGrab.Drop();
            moduleGrab.part.Die();
            fxSndStore.audio.Play();
        }

        private void Move(AvailablePart aPart, int qty, KASModuleContainer destContainer)
        {
            KASModuleGrab moduleGrab = aPart.partPrefab.GetComponent<KASModuleGrab>();
            if (!moduleGrab)
            {
                fxSndBipWrong.audio.Play();
                KAS_Shared.DebugError("Cannot grab the part taken, no grab module found !");
                return;
            }
            if (destContainer.MaxSizeReached(aPart, 1))
            {
                fxSndBipWrong.audio.Play();
                ScreenMessages.PostScreenMessage("Max size of the destination container reached !", 5, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            Remove(aPart, qty);
            destContainer.Add(aPart, qty);
        }

        public void TakeContents(Vessel evaVessel)
        {
            if (showGUI != guiMode.None)
            {
                CloseGUI();
                fxSndClose.audio.Play();
            }
            else
            {
                CloseAllGUI();
                guiMainWindowPos = new Rect(Screen.width / 2, Screen.height / 2, 10, 10);
                showGUI = guiMode.Take;
                fxSndOpen.audio.Play();
            }
        }

        public void ExchangeContents(KASModuleContainer moduleContainer)
        {
            if (showGUI != guiMode.None)
            {
                CloseGUI();
                fxSndClose.audio.Play();
            }
            else
            {
                CloseAllGUI();
                exchangeContainer = moduleContainer;
                guiMainWindowPos = new Rect(Screen.width / 2, Screen.height / 2, 10, 10);
                showGUI = guiMode.Exchange;
                fxSndOpen.audio.Play();
            }
        }

        public void EditContents()
        {
            if (showGUI != guiMode.None)
            {
                CloseGUI();
                fxSndClose.audio.Play();
            }
            else
            {
                CloseAllGUI();
                availableContents = GetAvailableContents();
                guiMainWindowPos = new Rect(Screen.width / 3, 35, 10, 10);
                showGUI = guiMode.Edit;
                fxSndOpen.audio.Play();
            }
        }

        public void ViewContents()
        {
            if (showGUI != guiMode.None)
            {
                CloseGUI();
            }
            else
            {
                CloseAllGUI();
                guiMainWindowPos = new Rect(Screen.width / 2, Screen.height / 2, 10, 10);
                showGUI = guiMode.View;
                fxSndOpen.audio.Play();
            }
        }

        private void CloseAllGUI()
        {
            List<KASModuleContainer> allInvModule = new List<KASModuleContainer>(GameObject.FindObjectsOfType(typeof(KASModuleContainer)) as KASModuleContainer[]);
            foreach (KASModuleContainer invModule in allInvModule)
            {
                if (invModule.showGUI != guiMode.None)
                {
                    invModule.CloseGUI();
                }
            }
        }

        private void CloseGUI()
        {
            showGUI = guiMode.None;
            exchangeContainer = null;
            availableContents = null;
            if (HighLogic.LoadedSceneIsEditor) EditorLogic.fetch.Unlock("KAS DisableEditorClickthrough");
        }

        void OnGUI()
        {
            GUI.skin = HighLogic.Skin;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.button.alignment = TextAnchor.MiddleCenter;

            if (showGUI != guiMode.None)
            {
                guiMainWindowPos = GUILayout.Window(5501, guiMainWindowPos, GuiMainWindow, this.part.partInfo.title, GUILayout.MinWidth(1), GUILayout.MinHeight(1));
                if (HighLogic.LoadedSceneIsEditor) KAS_Shared.DisableEditorClickthrough(guiMainWindowPos);
            }  
        }

        private void GuiStyles()
        {
            guiButtonStyle = new GUIStyle(GUI.skin.button);
            guiButtonStyle.normal.textColor = guiButtonStyle.focused.textColor = Color.white;
            guiButtonStyle.hover.textColor = guiButtonStyle.active.textColor = Color.yellow;
            guiButtonStyle.onNormal.textColor = guiButtonStyle.onFocused.textColor = guiButtonStyle.onHover.textColor = guiButtonStyle.onActive.textColor = Color.green;
            guiButtonStyle.padding = new RectOffset(4, 4, 4, 4);
            guiButtonStyle.alignment = TextAnchor.MiddleCenter;

            guiDataboxStyle = new GUIStyle(GUI.skin.box);
            guiDataboxStyle.margin.top = guiDataboxStyle.margin.bottom = -5;
            guiDataboxStyle.border.top = guiDataboxStyle.border.bottom = 0;
            guiDataboxStyle.wordWrap = false;
            guiDataboxStyle.alignment = TextAnchor.MiddleCenter;

            guiCenterStyle = new GUIStyle(GUI.skin.label);
            guiCenterStyle.wordWrap = false;
            guiCenterStyle.alignment = TextAnchor.MiddleCenter;

            guiBoldCyanCenterStyle = new GUIStyle(GUI.skin.label);
            guiBoldCyanCenterStyle.wordWrap = false;
            guiBoldCyanCenterStyle.fontStyle = FontStyle.Bold;
            guiBoldCyanCenterStyle.normal.textColor = Color.cyan;
            guiBoldCyanCenterStyle.alignment = TextAnchor.MiddleCenter;

            guiTooltipStyle = new GUIStyle(GUI.skin.label);
            guiTooltipStyle.normal.textColor = Color.white;
            guiTooltipStyle.alignment = TextAnchor.MiddleCenter;
            guiTooltipStyle.fontSize = 14;
        }

        private void GuiMainWindow(int windowID)
        {
            GuiStyles();
            GUILayout.Space(15);
            if (showGUI == guiMode.Edit)
            {
                GuiEditTabs();
                GUILayout.Label("- Available contents -", guiBoldCyanCenterStyle);
                scrollPos1 = GuiContentList(availableContents, ShowButton.Add, this, scrollPos1, false, scrollHeight:400f);
                GUILayout.Label("- Container contents -", guiBoldCyanCenterStyle);
                scrollPos2 = GuiContentList(GetContent(), ShowButton.Remove, this, scrollPos2, scrollHeight: 200f);
            }

            if (showGUI == guiMode.Take)
            {
                GUILayout.Label("- Container contents -", guiBoldCyanCenterStyle);
                scrollPos1 = GuiContentList(GetContent(), ShowButton.Take, this, scrollPos1, scrollHeight: 200f);
            }

            if (showGUI == guiMode.View)
            {
                GUILayout.Label("- Container contents -", guiBoldCyanCenterStyle);
                scrollPos1 = GuiContentList(GetContent(), ShowButton.None, this, scrollPos1, scrollHeight: 200f);
            }

            if (showGUI == guiMode.Exchange)
            {
                GUILayout.Label("- Source container contents -", guiBoldCyanCenterStyle);
                scrollPos1 = GuiContentList(GetContent(), ShowButton.Move, this, scrollPos1, true, exchangeContainer, scrollHeight: 300f);
                GUILayout.Label("- Destination container contents -", guiBoldCyanCenterStyle);
                scrollPos2 = GuiContentList(GetContent(), ShowButton.Move, exchangeContainer, scrollPos2, true, this, scrollHeight: 300f);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Close", "Close container"), guiButtonStyle, GUILayout.Width(60f)))
            {
                CloseAllGUI();
                fxSndClose.audio.Play();
            }
            if (showGUI == guiMode.Take)
            {
                if (GUILayout.Button(new GUIContent("Store", "Store current grabbed part"), guiButtonStyle, GUILayout.Width(60f)))
                {
                    StoreGrabbedPart();
                }
            }
            GUILayout.EndHorizontal();

            GUI.Label(new Rect(0, 18, guiMainWindowPos.width, 30), GUI.tooltip, guiTooltipStyle);         
            GUI.DragWindow();
        }

        private void GuiEditTabs()
        {
            GUILayout.BeginHorizontal(guiCenterStyle);
            guiButtonStyle.fontSize = 12;
            float tabButtonWidth = 60f;

            guiButtonStyle.normal.textColor = (activeEditTab == EditTab.All) ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent("All", "Show all part(s)"), guiButtonStyle, GUILayout.Width(tabButtonWidth)))
            {
                availableContents = GetAvailableContents();
                activeEditTab = EditTab.All;
            }

            guiButtonStyle.normal.textColor = (activeEditTab == EditTab.Pods) ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent("Pods", "Show part(s) from pods category"), guiButtonStyle, GUILayout.Width(tabButtonWidth)))
            {
                availableContents = GetAvailableContents(PartCategories.Pods);
                activeEditTab = EditTab.Pods;
            }

            guiButtonStyle.normal.textColor = (activeEditTab == EditTab.Propulsion) ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent("Propulsion", "Show part(s) from propulsion category"), guiButtonStyle, GUILayout.Width(tabButtonWidth)))
            {
                availableContents = GetAvailableContents(PartCategories.Propulsion);
                activeEditTab = EditTab.Propulsion;
            }

            guiButtonStyle.normal.textColor = (activeEditTab == EditTab.Control) ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent("Control", "Show part(s) from control category"), guiButtonStyle, GUILayout.Width(tabButtonWidth)))
            {
                availableContents = GetAvailableContents(PartCategories.Control);
                activeEditTab = EditTab.Control;
            }

            guiButtonStyle.normal.textColor = (activeEditTab == EditTab.Structural) ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent("Structural", "Show part(s) from structural category"), guiButtonStyle, GUILayout.Width(tabButtonWidth)))
            {
                availableContents = GetAvailableContents(PartCategories.Structural);
                activeEditTab = EditTab.Structural;
            }

            guiButtonStyle.normal.textColor = (activeEditTab == EditTab.Aero) ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent("Aero", "Show part(s) from aero category"), guiButtonStyle, GUILayout.Width(tabButtonWidth)))
            {
                availableContents = GetAvailableContents(PartCategories.Aero);
                activeEditTab = EditTab.Aero;
            }

            guiButtonStyle.normal.textColor = (activeEditTab == EditTab.Utility) ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent("Utility", "Show part(s) from utility category"), guiButtonStyle, GUILayout.Width(tabButtonWidth)))
            {
                availableContents = GetAvailableContents(PartCategories.Utility);
                activeEditTab = EditTab.Utility;
            }

            guiButtonStyle.normal.textColor = (activeEditTab == EditTab.Science) ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent("Science", "Show part(s) from science category"), guiButtonStyle, GUILayout.Width(tabButtonWidth)))
            {
                availableContents = GetAvailableContents(PartCategories.Science);
                activeEditTab = EditTab.Science;
            }

            guiButtonStyle.normal.textColor = (activeEditTab == EditTab.None) ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent("None", "Show part(s) without any category"), guiButtonStyle, GUILayout.Width(tabButtonWidth)))
            {
                availableContents = GetAvailableContents(PartCategories.none);
                activeEditTab = EditTab.None;
            }
            guiButtonStyle.fontSize = 14;
            guiButtonStyle.normal.textColor = Color.white;
            GUILayout.EndHorizontal();
        }

        private Vector2 GuiContentList(Dictionary<AvailablePart, int> contentsList, ShowButton showButton, KASModuleContainer actionContainer, Vector2 scrollPos, bool showTotal = true, KASModuleContainer destContainer = null, float scrollHeight = 300f)
        {
            scrollPos = GUILayout.BeginScrollView(scrollPos, guiDataboxStyle, GUILayout.Width(600f), GUILayout.Height(scrollHeight));

            foreach (KeyValuePair<AvailablePart, int> ct in contentsList)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent("  " + ct.Key.title, "Name"), guiCenterStyle, GUILayout.Width(300f));
                GUILayout.Label(new GUIContent("  " + GetPartSize(ct.Key).ToString("0.0"), "Size"), guiCenterStyle, GUILayout.Width(50f));
                GUILayout.Label(new GUIContent("  " + ct.Key.partPrefab.mass.ToString("0.000"), "Mass"), guiCenterStyle, GUILayout.Width(50f));
                GUILayout.Label(new GUIContent("  " + ct.Value, "Quantity"), guiCenterStyle, GUILayout.Width(50f));
                if (showButton == ShowButton.Add)
                {
                    if (GUILayout.Button(new GUIContent("Add", "Add part to container"), guiButtonStyle, GUILayout.Width(50f)))
                    {
                        if (actionContainer.MaxSizeReached(ct.Key, 1))
                        {
                            fxSndBipWrong.audio.Play();
                            ScreenMessages.PostScreenMessage("Max size of the container reached !", 5, ScreenMessageStyle.UPPER_CENTER);
                            return scrollPos;
                        }
                        else
                        {
                            actionContainer.Add(ct.Key, 1);
                        }
                    }
                }
                if (showButton == ShowButton.Remove)
                {
                    if (GUILayout.Button(new GUIContent("Remove", "Remove part from container"), guiButtonStyle, GUILayout.Width(60f)))
                    {
                        actionContainer.Remove(ct.Key, 1);
                    }
                }
                if (showButton == ShowButton.Take)
                {
                    if (GUILayout.Button(new GUIContent("Take", "Take part from container"), guiButtonStyle, GUILayout.Width(60f)))
                    {
                        actionContainer.Take(ct.Key);
                    }
                    /*
                    if (GUILayout.Button(new GUIContent("Attach", "Attach part"), guiButtonStyle, GUILayout.Width(60f)))
                    {
                        KASModuleGrab moduleGrab = ct.Key.partPrefab.GetComponent<KASModuleGrab>();
                        KASAddonPointer.StartPointer(ct.Key.partPrefab, KASAddonPointer.PointerMode.CopyAndAttach, true, true, true, 2, this.part.transform, false);
                    }*/
                }
                if (showButton == ShowButton.Move)
                {
                    if (GUILayout.Button(new GUIContent("Move", "Move part to the destination container"), guiButtonStyle, GUILayout.Width(60f)))
                    {
                        actionContainer.Move(ct.Key, 1, destContainer);
                    }
                }
                GUILayout.EndHorizontal();
           
            }
            if (showTotal)
            {
                GUILayout.Space(5);
                GUILayout.BeginVertical(guiCenterStyle);
                GUILayout.BeginHorizontal();

                GUIStyle guiLeftWhiteStyle = new GUIStyle(GUI.skin.label);
                guiLeftWhiteStyle.wordWrap = false;
                guiLeftWhiteStyle.normal.textColor = Color.white;
                guiLeftWhiteStyle.alignment = TextAnchor.MiddleLeft;

                GUIStyle guiRightWhiteStyle = new GUIStyle(GUI.skin.label);
                guiRightWhiteStyle.wordWrap = false;
                guiRightWhiteStyle.normal.textColor = Color.white;
                guiRightWhiteStyle.alignment = TextAnchor.MiddleRight;

                GUILayout.Label(new GUIContent("Weight : " + actionContainer.part.mass.ToString("0.000"), "Total weight of the container with contents"), guiRightWhiteStyle);
                GUILayout.Label(new GUIContent(" | Space : " + actionContainer.totalSize.ToString("0.0") + " / " + actionContainer.maxSize.ToString("0.0"), "Space used / Max space"), guiRightWhiteStyle);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
            return scrollPos;
        }

        [KSPEvent(name = "ContextMenuOpenContainer", active = true, guiActive = true, guiActiveUnfocused = true, guiName = "Open container")]
        public void ContextMenuOpenContainer()
        {
            if (FlightGlobals.ActiveVessel.isEVA)
            {
                TakeContents(FlightGlobals.ActiveVessel);
            }
            else
            {
                ViewContents();
            }           
        }
    }
}
