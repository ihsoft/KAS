using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KAS
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class KASAddonControlKey : MonoBehaviour
    {
        public static float radius = 2f;
        public static string grabPartKey = "g";
        public static string grabHeadKey = "w";
        public static string winchExtendKey = "[2]";
        public static string winchRetractKey = "[5]";
        public static string winchHeadLeftKey = "[1]";
        public static string winchHeadRightKey = "[3]";
        public static string winchHookKey = "[0]";
        public static string winchEjectKey = "[.]";
        public static string winchEvaExtendKey = "K";
        public static string winchEvaRetractKey = "I";
        public static string rotorNegativeKey = "[4]";
        public static string rotorPositiveKey = "[6]";


        protected Rect guiHeadGrabWindowPos;
        private GUIStyle guiButtonStyle;
        private KASModuleWinch clickedWinch = null;

        public void Awake()
        {
            LoadKeyConfig();
        }

        public void Update()
        {
            UpdateGrab();
            UpdateWinchMouseGrab();
            UpdateWinchKeyGrab();
            UpdateWinchCableControl();
            UpdateRotorControl();
        }

        public static void LoadKeyConfig()
        {
            ConfigNode node = ConfigNode.Load(KSPUtil.ApplicationRootPath + "GameData/KAS/settings.cfg") ?? new ConfigNode();
            foreach (ConfigNode grabNode in node.GetNodes("GrabModule"))
            {
                if (grabNode.HasValue("grabPartKey"))
                {
                    grabPartKey = grabNode.GetValue("grabPartKey");
                }
                if (grabNode.HasValue("grabConnectorKey"))
                {
                    grabHeadKey = grabNode.GetValue("grabConnectorKey");
                }
            }
            foreach (ConfigNode winchNode in node.GetNodes("WinchModule"))
            {
                if (winchNode.HasValue("extendKey"))
                {
                    winchExtendKey = winchNode.GetValue("extendKey");
                }
                if (winchNode.HasValue("retractKey"))
                {
                    winchRetractKey = winchNode.GetValue("retractKey");
                }
                if (winchNode.HasValue("connectorLeftKey"))
                {
                    winchHeadLeftKey = winchNode.GetValue("connectorLeftKey");
                }
                if (winchNode.HasValue("connectorRightKey"))
                {
                    winchHeadRightKey = winchNode.GetValue("connectorRightKey");
                }
                if (winchNode.HasValue("hookKey"))
                {
                    winchHookKey = winchNode.GetValue("hookKey");
                }
                if (winchNode.HasValue("ejectKey"))
                {
                    winchEjectKey = winchNode.GetValue("ejectKey");
                }
                if (winchNode.HasValue("evaExtendKey"))
                {
                    winchEvaExtendKey = winchNode.GetValue("evaExtendKey");
                }
                if (winchNode.HasValue("evaRetractKey"))
                {
                    winchEvaRetractKey = winchNode.GetValue("evaRetractKey");
                }
            }
            foreach (ConfigNode rotorNode in node.GetNodes("Rotor"))
            {
                if (rotorNode.HasValue("rotorNegativeKey"))
                {
                    rotorNegativeKey = rotorNode.GetValue("rotorNegativeKey");
                }
                if (rotorNode.HasValue("rotorPositiveKey "))
                {
                    rotorPositiveKey = rotorNode.GetValue("rotorPositiveKey ");
                }
            }
        }

        void OnGUI()
        {
            if (!clickedWinch) return;

            GUI.skin = HighLogic.Skin;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.button.alignment = TextAnchor.MiddleCenter;

            guiButtonStyle = new GUIStyle(GUI.skin.button);
            guiButtonStyle.normal.textColor = guiButtonStyle.focused.textColor = Color.white;
            guiButtonStyle.hover.textColor = guiButtonStyle.active.textColor = Color.yellow;
            guiButtonStyle.onNormal.textColor = guiButtonStyle.onFocused.textColor = guiButtonStyle.onHover.textColor = guiButtonStyle.onActive.textColor = Color.green;
            guiButtonStyle.padding = new RectOffset(4, 4, 4, 4);
            guiButtonStyle.alignment = TextAnchor.MiddleCenter;

            Vector3 headScreenPoint = Camera.main.WorldToScreenPoint(clickedWinch.headTransform.position);

            GUILayout.BeginArea(new Rect(headScreenPoint.x, Screen.height - headScreenPoint.y, 90, 40));
            GUILayout.BeginHorizontal();

            if (clickedWinch.evaHolderPart)
            {
                if (GUILayout.Button("Drop (Key " + grabHeadKey + ")", guiButtonStyle, GUILayout.Width(90f)))
                {
                    clickedWinch.DropHead(); ;
                    clickedWinch = null;
                }
            }
            else
            {
                if (GUILayout.Button("Grab (Key " + grabHeadKey + ")", guiButtonStyle, GUILayout.Width(90f)))
                {
                    clickedWinch.GrabHead(FlightGlobals.ActiveVessel);
                    clickedWinch = null;
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        public void UpdateGrab()
        {
            if (Input.GetKeyDown(grabPartKey.ToLower()))
            {
                if (FlightGlobals.ActiveVessel.isEVA)
                {
                    // Check if a part is already grabbed
                    KASModuleGrab grabbedPart = KAS_Shared.GetGrabbedPartModule(FlightGlobals.ActiveVessel);
                    if (grabbedPart)
                    {
                        grabbedPart.Drop();
                        return;
                    } 
                    List<Collider> nearestColliders = new List<Collider>(Physics.OverlapSphere(FlightGlobals.ActiveVessel.transform.position, radius, 557059));
                    float shorterDist = Mathf.Infinity;
                    KASModuleGrab nearestModuleGrab = null;
                    foreach (Collider col in nearestColliders)
                    {
                        // Check if if the collider have a rigidbody
                        if (!col.attachedRigidbody) continue;
                        // Check if it's a part
                        Part p = col.attachedRigidbody.GetComponent<Part>();
                        if (!p) continue;
                        // Check if it's grabbable part
                        KASModuleGrab moduleGrab = p.GetComponent<KASModuleGrab>();
                        if (!moduleGrab) continue;
                        // Check if it's a part is connected
                        if (moduleGrab.part.isConnected) continue;
                        // Check if it's a part grabbed by another kerbal eva
                        if (moduleGrab.evaHolderPart) continue;

                        // Select the nearest grabbable part
                        float distToGrab = Vector3.Distance(FlightGlobals.ActiveVessel.transform.position, moduleGrab.part.transform.position);
                        if (distToGrab <= shorterDist)
                        {
                            shorterDist = distToGrab;
                            nearestModuleGrab = moduleGrab;
                        }
                    }
                    //Grab nearest part if exist
                    if (nearestModuleGrab)
                    {
                        nearestModuleGrab.Grab(FlightGlobals.ActiveVessel);
                        return;
                    }
                }
            }
        }

        public void UpdateWinchMouseGrab()
        {
            if (Input.GetKeyDown(KeyCode.Mouse1))
            {
                if (clickedWinch)
                {
                    clickedWinch = null;
                    return;
                }
                if (FlightGlobals.ActiveVessel.isEVA)
                {
                    KerbalEVA kerbalEva = KAS_Shared.GetKerbalEvaUnderCursor();
                    if (kerbalEva)
                    {
                        KASModuleWinch winchModule = KAS_Shared.GetWinchModuleGrabbed(kerbalEva.vessel);
                        if (winchModule)
                        {
                            clickedWinch = winchModule;
                            return;
                        }
                    }
                    

                    Transform headTransform = KAS_Shared.GetTransformUnderCursor();
                    if (headTransform)
                    {
                        KASModuleWinchHead winchHeadModule = headTransform.gameObject.GetComponent<KASModuleWinchHead>();
                        if (winchHeadModule)
                        {
                            float dist = Vector3.Distance(FlightGlobals.ActiveVessel.transform.position, headTransform.position);
                            if (dist <= radius)
                            {
                                clickedWinch = winchHeadModule.connectedWinch;
                                return;
                            }
                        }
                    }
                }
            }
        }

        public void UpdateWinchKeyGrab()
        {
            if (Input.GetKeyDown(grabHeadKey.ToLower()))
            {
                if (FlightGlobals.ActiveVessel.isEVA)
                {
                    KASModuleWinch tmpGrabbbedHead = KAS_Shared.GetWinchModuleGrabbed(FlightGlobals.ActiveVessel);
                    if (tmpGrabbbedHead)
                    {
                        tmpGrabbbedHead.DropHead();
                        return;
                    }
                    List<Collider> nearestColliders = new List<Collider>(Physics.OverlapSphere(FlightGlobals.ActiveVessel.transform.position, radius, 557059));
                    float shorterDist = Mathf.Infinity;
                    KASModuleWinch nearestModuleWinch = null;
                    foreach (Collider col in nearestColliders)
                    {
                        KASModuleWinchHead headModule = col.transform.gameObject.GetComponent<KASModuleWinchHead>();
                        if (!headModule) continue;

                        // Check if the head is plugged
                        if (headModule.connectedWinch.headState != KASModuleWinch.PlugState.Deployed) continue;
                        // Check if it's a head grabbed by another kerbal eva
                        if (headModule.connectedWinch.evaHolderPart) continue;
                        // Select the nearest grabbable part
                        float distToGrab = Vector3.Distance(FlightGlobals.ActiveVessel.transform.position, headModule.connectedWinch.part.transform.position);
                        if (distToGrab <= shorterDist)
                        {
                            shorterDist = distToGrab;
                            nearestModuleWinch = headModule.connectedWinch;
                        }
                    }
                    //Grab nearest head if exist
                    if (nearestModuleWinch)
                    {
                        nearestModuleWinch.GrabHead(FlightGlobals.ActiveVessel);
                        return;
                    }
                }
            }
        }

        public void UpdateWinchCableControl()
        {
            //Extend key pressed
            if (winchExtendKey != "")
            {
                if (Input.GetKeyDown(winchExtendKey.ToLower()))
                {
                    KAS_Shared.SendMsgToWinch("EventWinchExtend", true, vess: FlightGlobals.ActiveVessel);
                }
                if (Input.GetKeyUp(winchExtendKey.ToLower()))
                {
                    KAS_Shared.SendMsgToWinch("EventWinchExtend", false, vess: FlightGlobals.ActiveVessel);
                }
            }
            //Retract key pressed
            if (winchRetractKey != "")
            {
                if (Input.GetKeyDown(winchRetractKey.ToLower()))
                {
                    KAS_Shared.SendMsgToWinch("EventWinchRetract", true, vess: FlightGlobals.ActiveVessel);
                }
                if (Input.GetKeyUp(winchRetractKey.ToLower()))
                {
                    KAS_Shared.SendMsgToWinch("EventWinchRetract", false, vess: FlightGlobals.ActiveVessel);
                }
            }
            //Head left key pressed
            if (winchHeadLeftKey != "")
            {
                if (Input.GetKey(winchHeadLeftKey.ToLower()))
                {
                    KAS_Shared.SendMsgToWinch("EventWinchHeadLeft", vess: FlightGlobals.ActiveVessel);
                }
            }
            //Head right key pressed
            if (winchHeadRightKey != "")
            {
                if (Input.GetKey(winchHeadRightKey.ToLower()))
                {
                    KAS_Shared.SendMsgToWinch("EventWinchHeadRight", vess: FlightGlobals.ActiveVessel);
                }
            }
            //Eject key pressed
            if (winchEjectKey != "")
            {
                if (Input.GetKeyDown(winchEjectKey.ToLower()))
                {
                    KAS_Shared.SendMsgToWinch("EventWinchEject", vess: FlightGlobals.ActiveVessel);
                }
            }
            //Hook key pressed
            if (winchHookKey != "")
            {
                if (Input.GetKeyDown(winchHookKey.ToLower()))
                {
                    KAS_Shared.SendMsgToWinch("EventWinchHook", vess: FlightGlobals.ActiveVessel);
                }
            }
            //Eva Extend key pressed
            if (winchEvaExtendKey != "")
            {
                if (Input.GetKeyDown(winchEvaExtendKey.ToLower()))
                {
                    KASModuleWinch grabbedWinchModule = KAS_Shared.GetWinchModuleGrabbed(FlightGlobals.ActiveVessel);
                    if (grabbedWinchModule)
                    {
                        grabbedWinchModule.EventWinchExtend(true);
                    }
                }
                if (Input.GetKeyUp(winchEvaExtendKey.ToLower()))
                {
                    KASModuleWinch grabbedWinchModule = KAS_Shared.GetWinchModuleGrabbed(FlightGlobals.ActiveVessel);
                    if (grabbedWinchModule)
                    {
                        grabbedWinchModule.EventWinchExtend(false);
                    }
                }
            }
            //Eva Retract key pressed
            if (winchEvaRetractKey != "")
            {
                if (Input.GetKeyDown(winchEvaRetractKey.ToLower()))
                {
                    KASModuleWinch grabbedWinchModule = KAS_Shared.GetWinchModuleGrabbed(FlightGlobals.ActiveVessel);
                    if (grabbedWinchModule)
                    {
                        grabbedWinchModule.EventWinchRetract(true);
                    }
                }
                if (Input.GetKeyUp(winchEvaRetractKey.ToLower()))
                {
                    KASModuleWinch grabbedWinchModule = KAS_Shared.GetWinchModuleGrabbed(FlightGlobals.ActiveVessel);
                    if (grabbedWinchModule)
                    {
                        grabbedWinchModule.EventWinchRetract(false);
                    }
                }
            }
        }

        private void UpdateRotorControl()
        {
            //negative key pressed
            if (rotorNegativeKey != "")
            {
                if (Input.GetKeyDown(rotorNegativeKey.ToLower()))
                {
                    KAS_Shared.SendMsgToRotor("EventRotorNegative", true, vess: FlightGlobals.ActiveVessel);
                }
                if (Input.GetKeyUp(rotorNegativeKey.ToLower()))
                {
                    KAS_Shared.SendMsgToRotor("EventRotorNegative", false, vess: FlightGlobals.ActiveVessel);
                }
            }
            //positive key pressed
            if (rotorPositiveKey != "")
            {
                if (Input.GetKeyDown(rotorPositiveKey.ToLower()))
                {
                    KAS_Shared.SendMsgToRotor("EventRotorPositive", true, vess: FlightGlobals.ActiveVessel);
                }
                if (Input.GetKeyUp(rotorPositiveKey.ToLower()))
                {
                    KAS_Shared.SendMsgToRotor("EventRotorPositive", false, vess: FlightGlobals.ActiveVessel);
                }
            }
        }
    }

}
