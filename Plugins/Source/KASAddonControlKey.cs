using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KAS {

[KSPAddon(KSPAddon.Startup.Flight, false)]
public class KASAddonControlKey : MonoBehaviour {
  public static float radius = 2f;
  public static string grabHeadKey = "w";
  public static string winchExtendKey = "[2]";
  public static string winchRetractKey = "[5]";
  public static string winchHeadLeftKey = "[1]";
  public static string winchHeadRightKey = "[3]";
  public static string winchHookKey = "[0]";
  public static string winchEjectKey = "[8]";
  public static string winchEvaExtendKey = "K";
  public static string winchEvaRetractKey = "I";
  public static string rotorNegativeKey = "[4]";
  public static string rotorPositiveKey = "[6]";
  public static string telescopicExtendKey = "[9]";
  public static string telescopicRetractKey = "[7]";
  private static float guiScrollHeight = 300f;
  private static string guiToogleKey;

  protected Rect guiHeadGrabWindowPos;
  private GUIStyle guiButtonStyle;
  private KASModuleWinch clickedWinch = null;

  public void Awake() {
    LoadKeyConfig();
  }

  public void Update() {
    // Ignore if an edit field is active, or in map view
    if (GUIUtility.keyboardControl != 0 || MapView.MapIsEnabled) {
      return;
    }

    UpdateWinchMouseGrab();
    UpdateWinchKeyGrab();
    UpdateWinchCableControl();
    UpdateGUIControl();
  }

  public static void LoadKeyConfig() {
    var node = ConfigNode.Load(KSPUtil.ApplicationRootPath + "GameData/KAS/settings.cfg")
        ?? new ConfigNode();
    foreach (ConfigNode winchNode in node.GetNodes("WinchModule")) {
      if (winchNode.HasValue("grabConnectorKey")) {
        grabHeadKey = winchNode.GetValue("grabConnectorKey");
      }
      if (winchNode.HasValue("extendKey")) {
        winchExtendKey = winchNode.GetValue("extendKey");
      }
      if (winchNode.HasValue("retractKey")) {
        winchRetractKey = winchNode.GetValue("retractKey");
      }
      if (winchNode.HasValue("connectorLeftKey")) {
        winchHeadLeftKey = winchNode.GetValue("connectorLeftKey");
      }
      if (winchNode.HasValue("connectorRightKey")) {
        winchHeadRightKey = winchNode.GetValue("connectorRightKey");
      }
      if (winchNode.HasValue("hookKey")) {
        winchHookKey = winchNode.GetValue("hookKey");
      }
      if (winchNode.HasValue("ejectKey")) {
        winchEjectKey = winchNode.GetValue("ejectKey");
      }
      if (winchNode.HasValue("evaExtendKey")) {
        winchEvaExtendKey = winchNode.GetValue("evaExtendKey");
      }
      if (winchNode.HasValue("evaRetractKey")) {
        winchEvaRetractKey = winchNode.GetValue("evaRetractKey");
      }
    }
    foreach (ConfigNode rotorNode in node.GetNodes("Rotor")) {
      if (rotorNode.HasValue("rotorNegativeKey")) {
        rotorNegativeKey = rotorNode.GetValue("rotorNegativeKey");
      }
      if (rotorNode.HasValue("rotorPositiveKey ")) {
        rotorPositiveKey = rotorNode.GetValue("rotorPositiveKey ");
      }
    }
    foreach (ConfigNode telescopicArmNode in node.GetNodes("TelescopicArm")) {
      if (telescopicArmNode.HasValue("extendKey")) {
        telescopicExtendKey = telescopicArmNode.GetValue("extendKey");
      }
      if (telescopicArmNode.HasValue("retractKey")) {
        telescopicRetractKey = telescopicArmNode.GetValue("retractKey");
      }
    }
    foreach (ConfigNode winchGuiNode in node.GetNodes("WinchGUI")) {
      if (winchGuiNode.HasValue("toogleKey")) {
        guiToogleKey = winchGuiNode.GetValue("toogleKey");
      }
      if (winchGuiNode.HasValue("height")) {
        guiScrollHeight = float.Parse(winchGuiNode.GetValue("height"));
      }
    }
  }

  void OnGUI() {
    if (!clickedWinch) {
      return;
    }

    GUI.skin = HighLogic.Skin;
    GUI.skin.label.alignment = TextAnchor.MiddleCenter;
    GUI.skin.button.alignment = TextAnchor.MiddleCenter;

    guiButtonStyle = new GUIStyle(GUI.skin.button);
    guiButtonStyle.normal.textColor = Color.white;
    guiButtonStyle.focused.textColor = Color.white;
    guiButtonStyle.hover.textColor = Color.yellow;
    guiButtonStyle.active.textColor = Color.yellow;
    guiButtonStyle.onNormal.textColor = Color.green;
    guiButtonStyle.onFocused.textColor = Color.green;
    guiButtonStyle.onHover.textColor = Color.green;
    guiButtonStyle.onActive.textColor = Color.green;

    guiButtonStyle.padding = new RectOffset(4, 4, 4, 4);
    guiButtonStyle.alignment = TextAnchor.MiddleCenter;

    Vector3 headScreenPoint = Camera.main.WorldToScreenPoint(clickedWinch.headTransform.position);

    GUILayout.BeginArea(new Rect(headScreenPoint.x, Screen.height - headScreenPoint.y, 200, 200));
    GUILayout.BeginVertical();

    if (clickedWinch.evaHolderPart) {
      if (GUILayout.Button("Drop (Key " + grabHeadKey + ")",
                           guiButtonStyle, GUILayout.Width(100f))) {
        clickedWinch.DropHead();
        clickedWinch = null;
      }
    } else {
      if (GUILayout.Button("Grab (Key " + grabHeadKey + ")",
                           guiButtonStyle, GUILayout.Width(100f))) {
        clickedWinch.GrabHead(FlightGlobals.ActiveVessel);
        clickedWinch = null;
      }
    }
    GUILayout.EndHorizontal();
    GUILayout.EndArea();
  }

  private void UpdateWinchMouseGrab() {
    if (Input.GetKeyDown(KeyCode.Mouse1)) {
      if (clickedWinch) {
        clickedWinch = null;
        return;
      }
      if (FlightGlobals.ActiveVessel.isEVA) {
        KerbalEVA kerbalEva = KAS_Shared.GetKerbalEvaUnderCursor();
        if (kerbalEva) {
          KASModuleWinch winchModule = KAS_Shared.GetWinchModuleGrabbed(kerbalEva.vessel);
          if (winchModule) {
            clickedWinch = winchModule;
            return;
          }
        }

        Transform headTransform = KAS_Shared.GetTransformUnderCursor();
        if (headTransform) {
          KAS_LinkedPart linkedPart = headTransform.gameObject.GetComponent<KAS_LinkedPart>();
          if (linkedPart) {
            float dist = Vector3.Distance(FlightGlobals.ActiveVessel.transform.position,
                                          headTransform.position);
            if (dist <= radius) {
              clickedWinch = linkedPart.part.GetComponent<KASModuleWinch>();
              return;
            }
          }
        }
      }
    }
  }

  private void UpdateWinchKeyGrab() {
    if (Input.GetKeyDown(grabHeadKey.ToLower())) {
      if (FlightGlobals.ActiveVessel.isEVA) {
        KASModuleWinch tmpGrabbbedHead = KAS_Shared.GetWinchModuleGrabbed(FlightGlobals.ActiveVessel);
        if (tmpGrabbbedHead) {
          tmpGrabbbedHead.DropHead();
          return;
        }
        var nearestColliders = new List<Collider>(
            Physics.OverlapSphere(FlightGlobals.ActiveVessel.transform.position, radius, 557059));
        float shorterDist = Mathf.Infinity;
        KASModuleWinch nearestModuleWinch = null;
        foreach (Collider col in nearestColliders) {
          KAS_LinkedPart linkedPart = col.transform.gameObject.GetComponent<KAS_LinkedPart>();
          if (!linkedPart) {
            continue;
          }
          KASModuleWinch winchModule = linkedPart.part.GetComponent<KASModuleWinch>();
          if (!winchModule) {
            continue;
          }

          // Check if the head is plugged
          if (winchModule.headState != KASModuleWinch.PlugState.Deployed) {
            continue;
          }
          // Check if it's a head grabbed by another kerbal eva
          if (winchModule.evaHolderPart) {
            continue;
          }
          // Select the nearest grabbable part
          float distToGrab = Vector3.Distance(FlightGlobals.ActiveVessel.transform.position,
                                              winchModule.part.transform.position);
          if (distToGrab <= shorterDist) {
            shorterDist = distToGrab;
            nearestModuleWinch = winchModule;
          }
        }
        //Grab nearest head if exist
        if (nearestModuleWinch) {
          nearestModuleWinch.GrabHead(FlightGlobals.ActiveVessel);
          return;
        }
      }
    }
  }

  private void UpdateGUIControl() {
    if (Input.GetKeyDown(guiToogleKey.ToLower())) {
      if (KAS_Shared.GetAllWinch(FlightGlobals.ActiveVessel).Count > 0) {
        KAS_Shared.DebugLog(KAS_Shared.GetAllWinch(FlightGlobals.ActiveVessel).Count
                            + " winch has been found on the vessel, showing GUI...");
        KASAddonWinchGUI.ToggleGUI();
      } else {
        KASAddonWinchGUI.ShowGUI(false);
      }
    }
  }

  private void UpdateWinchCableControl() {
    //Extend key pressed
    if (winchExtendKey != "") {
      if (Input.GetKeyDown(winchExtendKey.ToLower())) {
        KAS_Shared.SendMsgToWinch("EventWinchExtend", true, vess: FlightGlobals.ActiveVessel);
      }
      if (Input.GetKeyUp(winchExtendKey.ToLower())) {
        KAS_Shared.SendMsgToWinch("EventWinchExtend", false, vess: FlightGlobals.ActiveVessel);
      }
    }
    //Retract key pressed
    if (winchRetractKey != "") {
      if (Input.GetKeyDown(winchRetractKey.ToLower())) {
        KAS_Shared.SendMsgToWinch("EventWinchRetract", true, vess: FlightGlobals.ActiveVessel);
      }
      if (Input.GetKeyUp(winchRetractKey.ToLower())) {
        KAS_Shared.SendMsgToWinch("EventWinchRetract", false, vess: FlightGlobals.ActiveVessel);
      }
    }
    //Head left key pressed
    if (winchHeadLeftKey != "") {
      if (Input.GetKey(winchHeadLeftKey.ToLower())) {
        KAS_Shared.SendMsgToWinch("EventWinchHeadLeft", vess: FlightGlobals.ActiveVessel);
      }
    }
    //Head right key pressed
    if (winchHeadRightKey != "") {
      if (Input.GetKey(winchHeadRightKey.ToLower())) {
        KAS_Shared.SendMsgToWinch("EventWinchHeadRight", vess: FlightGlobals.ActiveVessel);
      }
    }
    //Eject key pressed
    if (winchEjectKey != "") {
      if (Input.GetKeyDown(winchEjectKey.ToLower())) {
        KAS_Shared.SendMsgToWinch("EventWinchEject", vess: FlightGlobals.ActiveVessel);
      }
    }
    //Hook key pressed
    if (winchHookKey != "") {
      if (Input.GetKeyDown(winchHookKey.ToLower())) {
        KAS_Shared.SendMsgToWinch("EventWinchHook", vess: FlightGlobals.ActiveVessel);
      }
    }
    //Eva Extend key pressed
    if (winchEvaExtendKey != "") {
      if (Input.GetKeyDown(winchEvaExtendKey.ToLower())) {
        var grabbedWinchModule = KAS_Shared.GetWinchModuleGrabbed(FlightGlobals.ActiveVessel);
        if (grabbedWinchModule) {
          grabbedWinchModule.EventWinchExtend(true);
        }
      }
      if (Input.GetKeyUp(winchEvaExtendKey.ToLower())) {
        var grabbedWinchModule = KAS_Shared.GetWinchModuleGrabbed(FlightGlobals.ActiveVessel);
        if (grabbedWinchModule) {
          grabbedWinchModule.EventWinchExtend(false);
        }
      }
    }
    //Eva Retract key pressed
    if (winchEvaRetractKey != "") {
      if (Input.GetKeyDown(winchEvaRetractKey.ToLower())) {
        var grabbedWinchModule = KAS_Shared.GetWinchModuleGrabbed(FlightGlobals.ActiveVessel);
        if (grabbedWinchModule) {
          grabbedWinchModule.EventWinchRetract(true);
        }
      }
      if (Input.GetKeyUp(winchEvaRetractKey.ToLower())) {
        var grabbedWinchModule = KAS_Shared.GetWinchModuleGrabbed(FlightGlobals.ActiveVessel);
        if (grabbedWinchModule) {
          grabbedWinchModule.EventWinchRetract(false);
        }
      }
    }
  }
}

}  // namespace
