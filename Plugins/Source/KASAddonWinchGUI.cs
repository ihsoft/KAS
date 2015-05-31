using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KAS
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class KASAddonWinchGUI : MonoBehaviour
    {
        public static bool GuiActive = false;
        protected Rect guiWindowPos;
        private GUIStyle guiButtonStyle, guiDataboxStyle, guigreenStyle, guiYellowStyle, guiCyanStyle, guiMagentaStyle, guiCenterStyle, guiBoldCenterStyle, guiTooltipStyle;
        private Vector2 scrollPos = Vector2.zero;

        private KASModuleWinch selectedWinchModule = null;
        private string tempWinchName = "";
        private bool remameActived = false;
        private KASModuleWinch[] allWinchModule = null;

        private static List<Vessel> vesselOpenGui = new List<Vessel>();

        public void Awake()
        {
            GameEvents.onVesselChange.Add(new EventData<Vessel>.OnEvent(this.OnVesselChange));
        }

        void OnVesselChange(Vessel vess)
        {
            if (vesselOpenGui.Contains(FlightGlobals.ActiveVessel))
            {
                if (KAS_Shared.GetAllWinch(FlightGlobals.ActiveVessel).Count > 0)
                {
                    GuiActive = true;
                }
                else
                {
                    ShowGUI(false);
                }
            }
            else
            {
                GuiActive = false;
            }
        }

        void OnDestroy()
        {
            GameEvents.onVesselChange.Remove(new EventData<Vessel>.OnEvent(this.OnVesselChange));
        }

        public static void ToggleGUI()
        {
            ShowGUI(!GuiActive);
        }

        public static void ShowGUI(bool active)
        {
            if (active && !GuiActive)
            {
                KAS_Shared.DebugLog("WinchGUI - Showing GUI...");
                if (!vesselOpenGui.Contains(FlightGlobals.ActiveVessel))
                {
                    vesselOpenGui.Add(FlightGlobals.ActiveVessel);
                }
                GuiActive = true;
            }
            else if (!active && GuiActive)
            {
                KAS_Shared.DebugLog("WinchGUI - Closing GUI...");
                if (vesselOpenGui.Contains(FlightGlobals.ActiveVessel))
                {
                    vesselOpenGui.Remove(FlightGlobals.ActiveVessel);
                }
                GuiActive = false;
            }   
        }

        void OnGUI()
        {
            if (!GuiActive) return;

            GUI.skin = HighLogic.Skin;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.button.alignment = TextAnchor.MiddleCenter;

            if (guiWindowPos.x == 0 && guiWindowPos.y == 0)
            {
                guiWindowPos = new Rect(Screen.width / 10, 0, 10, 10);
            }
            guiWindowPos = GUILayout.Window(5501, guiWindowPos, GuiMainWindow, "Winches control", GUILayout.MinWidth(1), GUILayout.MinHeight(1));    
        }

        private void GuiStyles()
        {
            guiButtonStyle = new GUIStyle(GUI.skin.button);
            guiButtonStyle.normal.textColor = guiButtonStyle.focused.textColor = Color.white;
            guiButtonStyle.hover.textColor = guiButtonStyle.active.textColor = Color.yellow;
            guiButtonStyle.onNormal.textColor = guiButtonStyle.onFocused.textColor = guiButtonStyle.onHover.textColor = guiButtonStyle.onActive.textColor = Color.green;
            guiButtonStyle.padding = new RectOffset(4, 4, 4, 4);
            guiButtonStyle.alignment = TextAnchor.MiddleCenter;
            //guiButtonStyle.fontSize = 12;

            guiDataboxStyle = new GUIStyle(GUI.skin.box);
            guiDataboxStyle.margin.top = guiDataboxStyle.margin.bottom = -5;
            guiDataboxStyle.border.top = guiDataboxStyle.border.bottom = 0;
            guiDataboxStyle.wordWrap = false;
            guiDataboxStyle.alignment = TextAnchor.MiddleCenter;

            guigreenStyle = new GUIStyle(GUI.skin.label);
            guigreenStyle.normal.textColor = Color.green;
            guigreenStyle.alignment = TextAnchor.MiddleCenter;

            guiYellowStyle = new GUIStyle(GUI.skin.label);
            guiYellowStyle.normal.textColor = Color.yellow;
            guiYellowStyle.alignment = TextAnchor.MiddleCenter;

            guiCyanStyle = new GUIStyle(GUI.skin.label);
            guiCyanStyle.normal.textColor = Color.cyan;
            guiCyanStyle.alignment = TextAnchor.MiddleCenter;

            guiMagentaStyle = new GUIStyle(GUI.skin.label);
            guiMagentaStyle.normal.textColor = Color.magenta;
            guiMagentaStyle.alignment = TextAnchor.MiddleCenter;

            guiCenterStyle = new GUIStyle(GUI.skin.label);
            guiCenterStyle.alignment = TextAnchor.MiddleCenter;

            guiBoldCenterStyle = new GUIStyle(GUI.skin.label);
            guiBoldCenterStyle.alignment = TextAnchor.MiddleCenter;
            guiBoldCenterStyle.fontStyle = FontStyle.Bold;

            guiTooltipStyle = new GUIStyle(GUI.skin.label);
            guiTooltipStyle.normal.textColor = Color.white;
            guiTooltipStyle.alignment = TextAnchor.MiddleCenter;
            guiTooltipStyle.fontSize = 14;
        }

        private void GuiMainWindow(int windowID)
        {
            GuiStyles();
            GUILayout.Space(15);

            //Get all winches
            if (allWinchModule == null) allWinchModule = GameObject.FindObjectsOfType(typeof(KASModuleWinch)) as KASModuleWinch[];

            //Default selection / Force selection of the first system if no system are selected
            if (selectedWinchModule == null || selectedWinchModule.vessel != FlightGlobals.ActiveVessel)
            {
                foreach (KASModuleWinch winchModule in allWinchModule)
                {
                    if (winchModule.vessel == FlightGlobals.ActiveVessel)
                    {
                        selectedWinchModule = winchModule;
                        break;
                    }
                }
            }

            #region System loop
            //scrollPos = GUILayout.BeginScrollView(scrollPos, guiDataboxStyle, GUILayout.Width(800f), GUILayout.Height(scrollHeight));
            GUILayout.BeginVertical(guiDataboxStyle);
            int i = 0;
            foreach (KASModuleWinch winchModule in allWinchModule)
            {
                if (winchModule.vessel != FlightGlobals.ActiveVessel || !winchModule.isActive || winchModule.CheckBlocked()) continue;

                GUILayout.BeginHorizontal();

                string sysname;
                if (winchModule.winchName != "" && winchModule.winchName != null)
                {
                    sysname = winchModule.winchName;
                }
                else
                {
                    sysname = "Winch(" + i + ")";
                    i++;
                }

                #region System title & selection
                GUILayout.BeginVertical();
                if (winchModule == selectedWinchModule)
                {
                    GUILayout.Label(new GUIContent("> " + sysname, "Selected KAS System"), guiBoldCenterStyle, GUILayout.Width(150f));
                }
                else
                {
                    GUILayout.Label(new GUIContent("  " + sysname, "KAS System"), guiCenterStyle, GUILayout.Width(150f));
                }

                if ((Event.current.type == EventType.repaint) && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                {
                    if (Input.GetMouseButtonDown(0))
                    {
                        selectedWinchModule = winchModule;
                    }
                    if (!winchModule.highLightStarted)
                    {
                        winchModule.part.SetHighlight(true,false);
                        winchModule.highLightStarted = true;
                    }
                }
                else
                {
                    if (winchModule.highLightStarted)
                    {
                        winchModule.part.SetHighlight(false, false);
                        winchModule.highLightStarted = false;
                    }
                }

                GUILayout.EndVertical();
                #endregion

                #region Cable control


                //release
                winchModule.release.active = GUILayout.Toggle(winchModule.release.active, new GUIContent("Release", "Release connector/hook"), guiButtonStyle, GUILayout.Width(60f));

                //Eject
                if (!winchModule.ejectEnabled || winchModule.headState != KASModuleWinch.PlugState.Locked) GUI.enabled = false;
                if (GUILayout.Button(new GUIContent("Eject", "Eject connector/hook"), guiButtonStyle, GUILayout.Width(40f)))
                {
                    winchModule.Eject();
                }
                GUI.enabled = true;

                //Retract
                if (winchModule.extend.active)
                {
                    GUI.enabled = false;
                }

                winchModule.retract.active = GUILayout.Toggle(winchModule.retract.active, new GUIContent("<<", "Toggle retract"), guiButtonStyle);

                if (GUILayout.RepeatButton(new GUIContent("<", "Retract"), guiButtonStyle))
                {
                    winchModule.guiRepeatRetract = true;
                    winchModule.retract.active = true;
                }
                else if (winchModule.guiRepeatRetract)
                {
                    winchModule.guiRepeatRetract = false;
                    winchModule.retract.active = false;
                }

                GUI.enabled = true;

                //Cable length
                GUI.skin.label.alignment = TextAnchor.MiddleCenter;
                if (winchModule.cableJoint)
                {
                    GUILayout.Label(new GUIContent(winchModule.cableJoint.maxDistance.ToString("0.00"), "Current cable length"), guiYellowStyle, GUILayout.Width(40f));
                    float strainDistance = winchModule.cableJoint.maxDistance - winchModule.cableRealLenght;
                    float warningPercentage = 10;
                    float warningDistance = (winchModule.cableJoint.maxDistance / 100) * warningPercentage;

                    GUILayout.Label("|", guiCenterStyle);

                    if (strainDistance < 0)
                    {
                        GUILayout.Label(new GUIContent(strainDistance.ToString("0.00"), "Cable is under strain"), guiMagentaStyle, GUILayout.Width(40f));
                    }
                    else if (strainDistance < warningDistance)
                    {
                        GUILayout.Label(new GUIContent(strainDistance.ToString("0.00"), "Distance before strain"), guiYellowStyle, GUILayout.Width(40f));
                    }
                    else if (strainDistance > warningDistance)
                    {
                        GUILayout.Label(new GUIContent(strainDistance.ToString("0.00"), "Distance before strain"), guigreenStyle, GUILayout.Width(40f));
                    }
                }
                else
                {
                    GUILayout.Label(new GUIContent("Retracted", "Cable is retracted and locked"), guigreenStyle, GUILayout.Width(93f));
                }

                //Extend
                if (winchModule.retract.active)
                {
                    GUI.enabled = false;
                }

                if (GUILayout.RepeatButton(new GUIContent(">", "Extend"), guiButtonStyle))
                {
                    winchModule.extend.active = true;
                    winchModule.guiRepeatExtend = true;
                }
                else if (winchModule.guiRepeatExtend)
                {
                    winchModule.guiRepeatExtend = false;
                    winchModule.extend.active = false;
                }

                winchModule.extend.active = GUILayout.Toggle(winchModule.extend.active, new GUIContent(">>", "Toggle extend"), guiButtonStyle);
                GUI.enabled = true;



                //
                winchModule.motorSpeedSetting = GUILayout.HorizontalSlider(winchModule.motorSpeedSetting, 0, winchModule.motorMaxSpeed, GUILayout.Width(100f));
                GUI.Box(GUILayoutUtility.GetLastRect(), new GUIContent("", "Motor speed setting"));
                GUILayout.Label(new GUIContent(winchModule.motorSpeed.ToString("0.00") + " / " + winchModule.motorSpeedSetting.ToString("0.00"), "Current motor speed / Motor speed setting"), guiCenterStyle, GUILayout.Width(90f));

                if (GUILayout.RepeatButton(new GUIContent("<", "Turn connected port to left"), guiButtonStyle))
                {
                    winchModule.EventWinchHeadLeft();
                    winchModule.guiRepeatTurnLeft = true;
                }
                else if (winchModule.guiRepeatTurnLeft)
                {
                    winchModule.guiRepeatTurnLeft = false;
                }

                if (GUILayout.RepeatButton(new GUIContent(">", "Turn connected port to right"), guiButtonStyle))
                {
                    winchModule.EventWinchHeadRight();
                    winchModule.guiRepeatTurnRight = true;
                }
                else if (winchModule.guiRepeatTurnRight)
                {

                    winchModule.guiRepeatTurnRight = false;
                }

                #endregion

                #region Winch & Connector & Hook controls

                if (winchModule.headState == KASModuleWinch.PlugState.Deployed || winchModule.headState == KASModuleWinch.PlugState.Locked) GUI.enabled = false;
                winchModule.PlugDocked = GUILayout.Toggle(winchModule.PlugDocked, new GUIContent("Docked", "Plug mode"), guiButtonStyle, GUILayout.Width(60f));
                if (GUILayout.Button(new GUIContent("Unplug", "Unplug"), guiButtonStyle, GUILayout.Width(60f)))
                {
                    winchModule.UnplugHead();
                }
                GUI.enabled = true;

                KASModuleMagnet moduleHookMagnet = winchModule.GetHookMagnet();
                KASModuleHarpoon moduleHookGrapple = winchModule.GetHookGrapple();

                if (moduleHookMagnet)
                {
                    moduleHookMagnet.MagnetActive = GUILayout.Toggle(moduleHookMagnet.MagnetActive, new GUIContent("Magnet", "Magnet On/Off"), guiButtonStyle, GUILayout.Width(60f));
                }

                if (moduleHookGrapple)
                {
                    if (!moduleHookGrapple.attachMode.StaticJoint && !moduleHookGrapple.attachMode.FixedJoint) GUI.enabled = false;
                    if (GUILayout.Button(new GUIContent("Detach", "Detach from ground or part"), guiButtonStyle, GUILayout.Width(60f)))
                    {
                        moduleHookGrapple.Detach();
                    }
                    GUI.enabled = true;
                }

                if (!moduleHookMagnet && !moduleHookGrapple)
                {
                    GUI.enabled = false;
                    GUILayout.Button(new GUIContent("-", "Nothing connected or hook not supported"), guiButtonStyle, GUILayout.Width(60f));
                    GUI.enabled = true;
                }


                #endregion

                GUILayout.EndHorizontal();  
                
            }
            GUILayout.EndVertical();
            //GUILayout.EndScrollView();
            #endregion

            #region GUI - Close
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Close", guiButtonStyle, GUILayout.Width(60f)))
            {
                GuiActive = false;
            }

            remameActived = GUILayout.Toggle(remameActived, new GUIContent("Rename", "Rename a winch"), guiButtonStyle, GUILayout.Width(60f));
            if (remameActived)
            {
                tempWinchName = GUILayout.TextField(tempWinchName, GUILayout.Width(120f));
                if (GUILayout.Button(new GUIContent("Set", "Set selected winch name to current text"), guiButtonStyle, GUILayout.Width(60f)))
                {
                    selectedWinchModule.winchName = tempWinchName;
                    remameActived = false;
                }
            }

            GUILayout.EndHorizontal();
            #endregion

            #region GUI - Tooltip & Drag windows
            GUI.Label(new Rect(0, 18, guiWindowPos.width, 30), GUI.tooltip, guiTooltipStyle);
            GUI.DragWindow();
            #endregion

        }
    }
}
