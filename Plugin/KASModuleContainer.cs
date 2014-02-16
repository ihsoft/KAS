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

        public class PartContent
        {
            public string name { get { return part.name; } }

            public readonly AvailablePart part;
            public readonly KASModuleGrab grabModule;

            public float storedSize { get { return grabModule.storedSize; } }

            public int totalCount { get { return pristine_count + instances.Count; } }
            public float totalSize { get { return storedSize * totalCount; } }
            public float totalMass { get { return pristine_mass * pristine_count + instance_mass; } }

            public float averageMass
            {
                get
                {
                    int count = totalCount;
                    return count > 0 ? totalMass / count : pristine_mass;
                }
            }

            public readonly float pristine_mass;
            public int pristine_count;

            public float instance_mass;
            public readonly List<ConfigNode> instances = new List<ConfigNode>();

            private PartContent(AvailablePart avPart, KASModuleGrab grab)
            {
                part = avPart;
                grabModule = grab;
                pristine_mass = part.partPrefab.mass;

                foreach (var res in part.partPrefab.GetComponents<PartResource>())
                {
                    pristine_mass += (float)(res.amount * res.info.density);
                }
            }

            public static PartContent Get(Dictionary<string,PartContent> table, string name, bool create = true)
            {
                PartContent item;

                if (!table.TryGetValue(name, out item) && create)
                {
                    AvailablePart avPart = PartLoader.getPartInfoByName(name);

                    if (avPart != null)
                    {
                        KASModuleGrab grab = avPart.partPrefab.GetComponent<KASModuleGrab>();

                        if (grab != null && grab.storable)
                        {
                            item = new PartContent(avPart, grab);
                            table.Add(name, item);
                        }
                    }
                }

                return item;
            }

            public void Load(ConfigNode node)
            {
                if (node.name == "CONTENT" && node.HasValue("qty"))
                {
                    pristine_count += int.Parse(node.GetValue("qty"));
                }
                else if (node.name == "CONTENT_PART" && node.HasValue("kas_total_mass"))
                {
                    ConfigNode nodeD = new ConfigNode();
                    node.CopyTo(nodeD);
                    instance_mass += float.Parse(node.GetValue("kas_total_mass"));
                    instances.Add(nodeD);
                }
            }

            public void Save(ConfigNode node)
            {
                if (pristine_count > 0)
                {
                    ConfigNode nodeD = node.AddNode("CONTENT");
                    nodeD.AddValue("name", name);
                    nodeD.AddValue("qty", pristine_count);
                }
                foreach (var inst in instances)
                {
                    ConfigNode nodeD = node.AddNode("CONTENT_PART");
                    inst.CopyTo(nodeD);
                }
            }

            public ConfigNode PopInstance()
            {
                ConfigNode node = instances[0];
                float mass = float.Parse(node.GetValue("kas_total_mass"));
                instance_mass -= mass;
                instances.RemoveAt(0);
                return node;
            }
        }

        public Dictionary<string, PartContent> availableContents = new Dictionary<string, PartContent>();
        public Dictionary<string, PartContent> contents = new Dictionary<string, PartContent>();

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

            foreach (var item in contents.Values)
            {
                item.Save(node);
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (node.HasNode("CONTENT") || node.HasNode("CONTENT_PART"))
            {
                contents.Clear();
            }

            foreach (ConfigNode cn in node.nodes)
            {
                if (cn.name != "CONTENT" && cn.name != "CONTENT_PART")
                {
                    continue;
                }

                string AvPartName = cn.GetValue("name") ?? "null";
                PartContent item = PartContent.Get(contents, AvPartName);

                if (item != null)
                {
                    item.Load(cn);
                }
                else
                {
                    KAS_Shared.DebugError("Load(Container) - Cannot retrieve " + AvPartName + " from PartLoader !");
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

        public Dictionary<string, PartContent> GetContent()
        {
            var edct = new Dictionary<string, PartContent>();
            foreach (PartContent avPart in contents.Values)
            {
                if (avPart.totalCount > 0)
                    edct.Add(avPart.name, avPart);
            }
            return edct;
        }

        private Dictionary<string, PartContent> GetAvailableContents(PartCategories category)
        {
            var edct = new Dictionary<string, PartContent>();
            foreach (AvailablePart avPart in PartLoader.LoadedPartsList)
            {
                if (avPart.category == category && ResearchAndDevelopment.PartModelPurchased(avPart))
                    PartContent.Get(edct, avPart.name);
            }
            return edct;
        }

        private Dictionary<string, PartContent> GetAvailableContents()
        {
            var edct = new Dictionary<string, PartContent>();
            foreach (AvailablePart avPart in PartLoader.LoadedPartsList)
            {
                if (ResearchAndDevelopment.PartModelPurchased(avPart))
                    PartContent.Get(edct, avPart.name);
            }
            return edct;
        }

        private void Add(AvailablePart avPart, int qty)
        {
            var item = PartContent.Get(contents, avPart.name);
            if (item != null)
            {
                item.pristine_count += qty;
                RefreshTotalSize();
            }
        }

        private void Remove(AvailablePart avPart, int qty)
        {
            var item = PartContent.Get(contents, avPart.name, false);
            if (item != null)
            {
                item.pristine_count = Math.Max(0, item.pristine_count - qty);
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
            foreach (PartContent item in contents.Values)
            {
                totalSize += item.totalSize;
                this.part.mass += item.totalMass;
            } 
        }

        private bool MaxSizeReached(KASModuleGrab grab, int qty)
        {
            return (totalSize + (grab.storedSize * qty) > maxSize);
        }

        private void Take(PartContent avPart)
        {
            if (waitAndGrabRunning)
            {
                KAS_Shared.DebugError("Take(Container) Take action is already running, please wait !");
                return;
            }
            if (!FlightGlobals.ActiveVessel.isEVA)
            {
                KAS_Shared.DebugError("Take(Container) Can only grab from EVA!");
                return;
            }
            KASModuleGrab grabbed = KAS_Shared.GetGrabbedPartModule(FlightGlobals.ActiveVessel);
            if (grabbed && grabbed.part.packed)
            {
                KAS_Shared.DebugError("Take(Container) EVA holding a packed part!");
                return;
            }
            if (avPart.pristine_count <= 0 && avPart.instances.Count > 0)
            {
                if (TakeStoredInstance(avPart.instances[0], FlightGlobals.ActiveVessel))
                {
                    avPart.PopInstance();
                    RefreshTotalSize();
                }
                return;
            }
            KASModuleGrab prefabGrabModule = avPart.grabModule;
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
            avPart.pristine_count--;
            RefreshTotalSize();
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

        private bool TakeStoredInstance(ConfigNode node, Vessel vessel)
        {
            Part newPart = KAS_Shared.LoadPartSnapshot(vessel, node, Vector3.zero, Quaternion.identity);

            KASModuleGrab moduleGrab = newPart.GetComponent<KASModuleGrab>();
            if (!moduleGrab || !moduleGrab.GrabPending())
            {
                KAS_Shared.DebugError("Take(Container) Cannot grab the part taken, no grab module found !");
                newPart.Die();
                return false;
            }

            return true;
        }

        private void StoreGrabbedPart()
        {
            KASModuleGrab moduleGrab = KAS_Shared.GetGrabbedPartModule(FlightGlobals.ActiveVessel);
            if (!moduleGrab || moduleGrab.part.packed)
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
            if (MaxSizeReached(moduleGrab,1))
            {
                fxSndBipWrong.audio.Play();
                ScreenMessages.PostScreenMessage("Max size of the container reached !", 5, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            PartContent info = PartContent.Get(contents, moduleGrab.part.partInfo.name);
            if (info == null)
            {
                fxSndBipWrong.audio.Play();
                ScreenMessages.PostScreenMessage("Could not store part!", 5, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            if (moduleGrab.stateless)
            {
                info.pristine_count++;
            }
            else
            {
                info.Load(KAS_Shared.SavePartSnapshot(moduleGrab.part));
            }
            RefreshTotalSize();
            moduleGrab.Drop();
            moduleGrab.part.Die();
            fxSndStore.audio.Play();
        }

        private void Move(PartContent aPart, int qty, KASModuleContainer destContainer)
        {
            if (destContainer.MaxSizeReached(aPart.grabModule, qty))
            {
                fxSndBipWrong.audio.Play();
                ScreenMessages.PostScreenMessage("Max size of the destination container reached !", 5, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            PartContent dest = PartContent.Get(destContainer.contents, aPart.name);
            if (aPart.pristine_count > 0)
            {
                int delta = Math.Min(aPart.pristine_count, qty);
                aPart.pristine_count -= delta;
                dest.pristine_count += delta;
                qty -= delta;
            }
            while (qty > 0 && aPart.instances.Count > 0)
            {
                dest.Load(aPart.PopInstance());
                qty--;
            }
            RefreshTotalSize();
            destContainer.RefreshTotalSize();
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

        [KSPEvent(guiActiveEditor = true, active = true, guiName = "Edit Container")]
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
                guiMainWindowPos = GUILayout.Window(GetInstanceID(), guiMainWindowPos, GuiMainWindow, this.part.partInfo.title, GUILayout.MinWidth(1), GUILayout.MinHeight(1));
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

        private Vector2 GuiContentList(Dictionary<string,PartContent> contentsList, ShowButton showButton, KASModuleContainer actionContainer, Vector2 scrollPos, bool showTotal = true, KASModuleContainer destContainer = null, float scrollHeight = 300f)
        {
            scrollPos = GUILayout.BeginScrollView(scrollPos, guiDataboxStyle, GUILayout.Width(600f), GUILayout.Height(scrollHeight));

            foreach (PartContent item in contentsList.Values)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent("  " + item.part.title, "Name"), guiCenterStyle, GUILayout.Width(300f));
                GUILayout.Label(new GUIContent("  " + item.storedSize.ToString("0.0"), "Size"), guiCenterStyle, GUILayout.Width(50f));
                GUILayout.Label(new GUIContent("  " + item.averageMass.ToString("0.000"), "Mass"), guiCenterStyle, GUILayout.Width(50f));
                GUILayout.Label(new GUIContent("  " + item.totalCount, "Quantity"), guiCenterStyle, GUILayout.Width(50f));
                if (showButton == ShowButton.Add)
                {
                    if (GUILayout.Button(new GUIContent("Add", "Add part to container"), guiButtonStyle, GUILayout.Width(50f)))
                    {
                        if (actionContainer.MaxSizeReached(item.grabModule, 1))
                        {
                            fxSndBipWrong.audio.Play();
                            ScreenMessages.PostScreenMessage("Max size of the container reached !", 5, ScreenMessageStyle.UPPER_CENTER);
                            return scrollPos;
                        }
                        else
                        {
                            actionContainer.Add(item.part, 1);
                        }
                    }
                }
                if (showButton == ShowButton.Remove)
                {
                    if (GUILayout.Button(new GUIContent("Remove", "Remove part from container"), guiButtonStyle, GUILayout.Width(60f)))
                    {
                        actionContainer.Remove(item.part, 1);
                    }
                }
                if (showButton == ShowButton.Take)
                {
                    if (GUILayout.Button(new GUIContent("Take", "Take part from container"), guiButtonStyle, GUILayout.Width(60f)))
                    {
                        actionContainer.Take(item);
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
                        actionContainer.Move(item, 1, destContainer);
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
