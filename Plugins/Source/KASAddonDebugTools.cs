using System;
using System.Collections.Generic;
using UnityEngine;

namespace KAS {

[KSPAddon(KSPAddon.Startup.Flight, false)]
class KASAddonDebugTools : MonoBehaviour {
  protected Rect guiConfigWindowPos;
  private GUIStyle guiButtonStyle;
  private GUIStyle guiDataboxStyle;
  private GUIStyle guigreenStyle;
  private GUIStyle guiYellowStyle;
  private GUIStyle guiCyanStyle;
  private GUIStyle guiMagentaStyle;
  private GUIStyle guiCenterStyle;
  private GUIStyle guiBoldCenterStyle;
  private float configIncrement = 1;
  public Dictionary<string, string> editFields = new Dictionary<string, string>();
  private Vector2 scrollPos = Vector2.zero;

  //External
  private KASModuleWinch moduleWinch = null;
  private KASModuleMagnet moduleMagnet = null;
  private KASModuleHarpoon moduleGrapple = null;
  private KASModulePort modulePort = null;
  private KASModuleAnchor moduleAnchor = null;
  private KASModuleStrut moduleStrut = null;
  private KASModuleRotor moduleRotor = null;

  //Config menu tab
  Part clickedPart;
  private bool GuiConfigToogle = false;
  public KASGuiConfigMenu menu = 0;
  public enum KASGuiConfigMenu {
    NONE = 0,
    WINCH = 1,
    GRAB = 2,
    SUCTION = 3,
    GRAPPLE = 4,
    MAGNET = 5,
    TIMEDBOMB = 6,
    PORT = 7,
    ANCHOR = 8,
    STRUT = 9,
    ROTOR = 10,
  }

  public void Update() {
    if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.K)) {
      if (GuiConfigToogle) {
        Debug.Log("KASAddonDebugMenu - Closing KAS debug tools");
        GuiConfigToogle = false;
      } else {
        Debug.Log("KASAddonDebugMenu - Opening KAS debug tools");
        GuiConfigToogle = true;
      }
    }

    if (GuiConfigToogle) {
      if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Mouse0)) {
        Debug.Log("KASAddonDebugMenu - On click");
        clickedPart = KAS_Shared.GetPartUnderCursor();
        if (clickedPart) {
          moduleWinch = null;
          moduleMagnet = null;
          moduleGrapple = null;
          modulePort = null;
          moduleAnchor = null;
          moduleStrut = null;
          moduleRotor = null;

          moduleWinch = clickedPart.GetComponent<KASModuleWinch>();
          moduleMagnet = clickedPart.GetComponent<KASModuleMagnet>();
          moduleGrapple = clickedPart.GetComponent<KASModuleHarpoon>();
          modulePort = clickedPart.GetComponent<KASModulePort>();
          moduleAnchor = clickedPart.GetComponent<KASModuleAnchor>();
          moduleStrut = clickedPart.GetComponent<KASModuleStrut>();
          moduleRotor = clickedPart.GetComponent<KASModuleRotor>();
        }     
      }
    }

    if (FlightGlobals.ActiveVessel.heightFromTerrain == -1f) {
      if (FlightGlobals.ActiveVessel.Landed) {
        KAS_Shared.DebugWarning(
            "Warning ! ActiveVessel is landed and heightFromTerrain are negative ("
            + FlightGlobals.ActiveVessel.heightFromTerrain + ")");
      }
    }
  }

  void OnGUI() {
    if (!GuiConfigToogle) {
      return;
    }

    GUI.skin = HighLogic.Skin;
    GUI.skin.label.alignment = TextAnchor.MiddleCenter;
    GUI.skin.button.alignment = TextAnchor.MiddleCenter;

    //Config window
    if (guiConfigWindowPos.x == 0 && guiConfigWindowPos.y == 0) {
      guiConfigWindowPos = new Rect(Screen.width - (Screen.width / 3), 25, 10, 10);
    }
    guiConfigWindowPos = GUILayout.Window(
        5600, guiConfigWindowPos, GuiConfigWindow, "KAS - Debug Config",
        GUILayout.MinWidth(150), GUILayout.MinHeight(150));
  }

  private void GuiConfigWindow(int windowID) {
    #region GUI - Styles
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

    guiDataboxStyle = new GUIStyle(GUI.skin.box);
    guiDataboxStyle.margin.top = guiDataboxStyle.margin.bottom = -5;
    guiDataboxStyle.border.top = guiDataboxStyle.border.bottom = 0;
    guiDataboxStyle.wordWrap = false;
    guiDataboxStyle.alignment = TextAnchor.MiddleCenter;

    guigreenStyle = new GUIStyle(GUI.skin.label);
    guigreenStyle.normal.textColor = Color.green;

    guiYellowStyle = new GUIStyle(GUI.skin.label);
    guiYellowStyle.normal.textColor = Color.yellow;

    guiCyanStyle = new GUIStyle(GUI.skin.label);
    guiCyanStyle.normal.textColor = Color.cyan;

    guiMagentaStyle = new GUIStyle(GUI.skin.label);
    guiMagentaStyle.normal.textColor = Color.magenta;

    guiCenterStyle = new GUIStyle(GUI.skin.label);
    guiCenterStyle.alignment = TextAnchor.MiddleCenter;

    guiBoldCenterStyle = new GUIStyle(GUI.skin.label);
    guiBoldCenterStyle.alignment = TextAnchor.MiddleCenter;
    guiBoldCenterStyle.fontStyle = FontStyle.Bold;

    #endregion

    if (clickedPart) {
      GUILayout.Label("--- " + clickedPart.partInfo.title
                      + "(" + clickedPart.partInfo.name + ") ---", guiYellowStyle);
    } else {
      GUILayout.Label("--- No part selected ---", guiYellowStyle);               
    }
    GUILayout.Label("( Use Ctrl + mouse click to select a part )", guiCenterStyle);

    #region Increment setting
    GUILayout.BeginHorizontal();
    GUILayout.Label("Increment", GUILayout.Width(100f));
    if (GUILayout.Button("-", guiButtonStyle, GUILayout.Width(30f))) {
      if (((float)Math.Round(configIncrement / 10f, 5)) > 0.000001) {
        configIncrement = (float)Math.Round(configIncrement / 10f, 5);
      }
    }
    GUILayout.Label(configIncrement.ToString(), GUILayout.Width(80f));
    if (GUILayout.Button("+", guiButtonStyle, GUILayout.Width(30f))) {
      configIncrement = (float)Math.Round(configIncrement * 10f, 5);
    }
    if (GUILayout.Button("Def.", guiButtonStyle, GUILayout.Width(50f))) {
      configIncrement = 1;
    }
    GUILayout.EndHorizontal();
    #endregion

    GUILayout.BeginHorizontal();
    if (moduleMagnet) {
      if (GUILayout.Button("Magnet", guiButtonStyle)) {
        menu = KASGuiConfigMenu.MAGNET;
      }
    }
    if (moduleGrapple) {
      if (GUILayout.Button("Grapple", guiButtonStyle)) {
        menu = KASGuiConfigMenu.GRAPPLE;
      }
    }
    if (moduleWinch) {
      if (GUILayout.Button("Winch", guiButtonStyle)) {
        menu = KASGuiConfigMenu.WINCH;
      }
    }
    if (modulePort) {
      if (GUILayout.Button("Port", guiButtonStyle)) {
        menu = KASGuiConfigMenu.PORT;
      }
    }
    if (moduleAnchor) {
      if (GUILayout.Button("Anchor", guiButtonStyle)) {
        menu = KASGuiConfigMenu.ANCHOR;
      }
    }
    if (moduleStrut) {
      if (GUILayout.Button("Strut", guiButtonStyle)) {
        menu = KASGuiConfigMenu.STRUT;
      }
    }
    if (moduleRotor) {
      if (GUILayout.Button("Rotor", guiButtonStyle)) {
        menu = KASGuiConfigMenu.ROTOR;
      }
    }

    if (!moduleMagnet && !moduleWinch && !modulePort
        && !moduleGrapple && !moduleAnchor && !moduleRotor) {
      GUILayout.Label("No supported module found !", guiMagentaStyle);
    }

    GUILayout.EndHorizontal();

    if (menu == KASGuiConfigMenu.WINCH) {
      GuiConfigWinchTab();
    }
    if (menu == KASGuiConfigMenu.GRAPPLE) {
      GuiConfigGrappleTab();
    }
    if (menu == KASGuiConfigMenu.MAGNET) {
      GuiConfigMagnetTab();
    }
    if (menu == KASGuiConfigMenu.PORT) {
      GuiConfigPortTab();
    }
    if (menu == KASGuiConfigMenu.ANCHOR) {
      GuiConfigAnchorTab();
    }
    if (menu == KASGuiConfigMenu.STRUT) {
      GuiConfigStrutTab();
    }
    if (menu == KASGuiConfigMenu.ROTOR) {
      GuiConfigRotorTab();
    }
          
    if (GUILayout.Button("Close", guiButtonStyle)) {
      GuiConfigToogle = false;
    }

    GUI.DragWindow();
  }

  private bool ConfigValues(Dictionary<string, float> confValues) {
    //Loop for float configuration values
    bool buttonPressed = false;
    List<string> confKeys = new List<string>(confValues.Keys);
    foreach (string param in confKeys) {
      GUILayout.BeginHorizontal();
      GUILayout.Label(param.ToString(), GUILayout.Width(250f));
      if (GUILayout.Button("-", guiButtonStyle, GUILayout.Width(30f))) {
        confValues[param] += -configIncrement;
        buttonPressed = true;
      }
      GUILayout.Label(confValues[param].ToString("0.0000"), GUILayout.Width(70f));
      if (GUILayout.Button("+", guiButtonStyle, GUILayout.Width(30f))) {
        confValues[param] += configIncrement;
        buttonPressed = true;
      }
      GUILayout.EndHorizontal();
    }
    return buttonPressed;
  }

  private void GuiConfigWinchTab() {
    GUILayout.BeginVertical(guiDataboxStyle);

    //List all configuration values
    Dictionary<string, float> confValues = new Dictionary<string, float>();
    if (moduleWinch.cableJoint) {
      confValues.Add("spring", moduleWinch.cableJoint.spring);
      confValues.Add("damper", moduleWinch.cableJoint.damper);
    }
    confValues.Add("maxLenght", moduleWinch.maxLenght);
    confValues.Add("motorSpeed", moduleWinch.motorMaxSpeed);
    confValues.Add("winchPowerDrain", moduleWinch.powerDrain);
    confValues.Add("cableWidth", moduleWinch.cableWidth);
    confValues.Add("ejectForce", moduleWinch.ejectForce);
    confValues.Add("headMass", moduleWinch.headMass);       
    confValues.Add("lockMinDist", moduleWinch.lockMinDist);
    confValues.Add("lockMinFwdDot", moduleWinch.lockMinFwdDot);
    confValues.Add("evaHeadPosx", moduleWinch.evaGrabHeadPos.x);
    confValues.Add("evaHeadPosy", moduleWinch.evaGrabHeadPos.y);
    confValues.Add("evaHeadPosz", moduleWinch.evaGrabHeadPos.z);
    confValues.Add("evaHeadRotx", moduleWinch.evaGrabHeadDir.x);
    confValues.Add("evaHeadRoty", moduleWinch.evaGrabHeadDir.y);
    confValues.Add("evaHeadRotz", moduleWinch.evaGrabHeadDir.z);
    confValues.Add("evaDropHeadPosx", moduleWinch.evaDropHeadPos.x);
    confValues.Add("evaDropHeadPosy", moduleWinch.evaDropHeadPos.y);
    confValues.Add("evaDropHeadPosz", moduleWinch.evaDropHeadPos.z);
    confValues.Add("evaDropHeadRotx", moduleWinch.evaDropHeadRot.x);
    confValues.Add("evaDropHeadRoty", moduleWinch.evaDropHeadRot.y);
    confValues.Add("evaDropHeadRotz", moduleWinch.evaDropHeadRot.z);

    //Show config controls
    bool buttonPressed = ConfigValues(confValues);

    //Toogle buttons for bool values
    moduleWinch.ejectEnabled =
        GUILayout.Toggle(moduleWinch.ejectEnabled, "ejectEnabled", guiButtonStyle);

    //Set configuration if value changed
    if (moduleWinch.cableJoint) {
      if (confValues["spring"] != moduleWinch.cableJoint.spring) {
        moduleWinch.cableJoint.spring = moduleWinch.cableSpring = confValues["spring"];
      }
      if (confValues["damper"] != moduleWinch.cableJoint.damper) {
        moduleWinch.cableJoint.damper = moduleWinch.cableDamper = confValues["damper"];
      }
    }

    if (confValues["maxLenght"] != moduleWinch.maxLenght) {
      moduleWinch.maxLenght = confValues["maxLenght"];
    }
    if (confValues["motorSpeed"] != moduleWinch.motorMaxSpeed) {
      moduleWinch.motorMaxSpeed = confValues["motorSpeed"];
    }
    if (confValues["winchPowerDrain"] != moduleWinch.powerDrain) {
      moduleWinch.powerDrain = confValues["winchPowerDrain"];
    }
    if (confValues["cableWidth"] != moduleWinch.cableWidth) {
      moduleWinch.cableWidth = confValues["cableWidth"];
    }
    if (confValues["ejectForce"] != moduleWinch.ejectForce) {
      moduleWinch.ejectForce = confValues["ejectForce"];
    }
    if (confValues["headMass"] != moduleWinch.headMass) {
      moduleWinch.headMass = confValues["headMass"];
    }
    if (confValues["lockMinDist"] != moduleWinch.lockMinDist) {
      moduleWinch.lockMinDist = confValues["lockMinDist"];
    }
    if (confValues["lockMinFwdDot"] != moduleWinch.lockMinFwdDot) {
      moduleWinch.lockMinFwdDot = confValues["lockMinFwdDot"];
    }
    if (confValues["evaHeadPosx"] != moduleWinch.evaGrabHeadPos.x) {
      moduleWinch.evaGrabHeadPos = new Vector3(
          confValues["evaHeadPosx"], confValues["evaHeadPosy"], confValues["evaHeadPosz"]);
    }
    if (confValues["evaHeadPosy"] != moduleWinch.evaGrabHeadPos.y) {
      moduleWinch.evaGrabHeadPos = new Vector3(
          confValues["evaHeadPosx"], confValues["evaHeadPosy"], confValues["evaHeadPosz"]);
    }
    if (confValues["evaHeadPosz"] != moduleWinch.evaGrabHeadPos.z) {
      moduleWinch.evaGrabHeadPos = new Vector3(
          confValues["evaHeadPosx"], confValues["evaHeadPosy"], confValues["evaHeadPosz"]);
    }
    if (confValues["evaHeadRotx"] != moduleWinch.evaGrabHeadDir.x) {
      moduleWinch.evaGrabHeadDir = new Vector3(
          confValues["evaHeadRotx"], confValues["evaHeadRoty"], confValues["evaHeadRotz"]);
    }
    if (confValues["evaHeadRoty"] != moduleWinch.evaGrabHeadDir.y) {
      moduleWinch.evaGrabHeadDir = new Vector3(
          confValues["evaHeadRotx"], confValues["evaHeadRoty"], confValues["evaHeadRotz"]);
    }
    if (confValues["evaHeadRotz"] != moduleWinch.evaGrabHeadDir.z) {
      moduleWinch.evaGrabHeadDir = new Vector3(
          confValues["evaHeadRotx"], confValues["evaHeadRoty"], confValues["evaHeadRotz"]);
    }

    if (confValues["evaDropHeadPosx"] != moduleWinch.evaDropHeadPos.x) {
      moduleWinch.evaDropHeadPos = new Vector3(confValues["evaDropHeadPosx"],
                                               confValues["evaDropHeadPosy"],
                                               confValues["evaDropHeadPosz"]);
    }
    if (confValues["evaDropHeadPosy"] != moduleWinch.evaDropHeadPos.y) {
      moduleWinch.evaDropHeadPos = new Vector3(confValues["evaDropHeadPosx"],
                                               confValues["evaDropHeadPosy"],
                                               confValues["evaDropHeadPosz"]);
    }
    if (confValues["evaDropHeadPosz"] != moduleWinch.evaDropHeadPos.z) {
      moduleWinch.evaDropHeadPos = new Vector3(confValues["evaDropHeadPosx"],
                                               confValues["evaDropHeadPosy"],
                                               confValues["evaDropHeadPosz"]);
    }

    if (confValues["evaDropHeadRotx"] != moduleWinch.evaDropHeadRot.x) {
      moduleWinch.evaDropHeadRot = new Vector3(confValues["evaDropHeadRotx"],
                                               confValues["evaDropHeadRoty"],
                                               confValues["evaDropHeadRotz"]);
    }
    if (confValues["evaDropHeadRoty"] != moduleWinch.evaDropHeadRot.y) {
      moduleWinch.evaDropHeadRot = new Vector3(confValues["evaDropHeadRotx"],
                                               confValues["evaDropHeadRoty"],
                                               confValues["evaDropHeadRotz"]);
    }
    if (confValues["evaDropHeadRotz"] != moduleWinch.evaDropHeadRot.z) {
      moduleWinch.evaDropHeadRot = new Vector3(confValues["evaDropHeadRotx"],
                                               confValues["evaDropHeadRoty"],
                                               confValues["evaDropHeadRotz"]);
    }
    GUILayout.Space(25);
    GUILayout.EndVertical();
  }

  private void GuiConfigStrutTab() {
    GUILayout.BeginVertical(guiDataboxStyle);

    //List all configuration values
    Dictionary<string, float> confValues = new Dictionary<string, float>();
    confValues.Add("maxLenght", moduleStrut.maxLenght);
    confValues.Add("maxAngle", moduleStrut.maxAngle);
    confValues.Add("breakForce", moduleStrut.breakForce);
    confValues.Add("tubeScale", moduleStrut.tubeScale);
    confValues.Add("jointScale", moduleStrut.jointScale);
    confValues.Add("textureTiling", moduleStrut.textureTiling);

    //Show config controls
    ConfigValues(confValues);

    //Toogle buttons for bool values
    moduleStrut.allowDock = GUILayout.Toggle(moduleStrut.allowDock, "allowDock", guiButtonStyle);
    moduleStrut.hasCollider =
        GUILayout.Toggle(moduleStrut.hasCollider, "hasCollider", guiButtonStyle);

    //Set configuration if value changed
    if (confValues["maxLenght"] != moduleStrut.maxLenght) {
      moduleStrut.maxLenght = confValues["maxLenght"];
    }
    if (confValues["maxAngle"] != moduleStrut.maxAngle) {
      moduleStrut.maxAngle = confValues["maxAngle"];
    }
    if (confValues["breakForce"] != moduleStrut.breakForce) {
      moduleStrut.breakForce = confValues["breakForce"];
    }
    if (confValues["tubeScale"] != moduleStrut.tubeScale) {
      moduleStrut.tubeScale = confValues["tubeScale"];
    }
    if (confValues["jointScale"] != moduleStrut.jointScale) {
      moduleStrut.jointScale = confValues["jointScale"];
    }
    if (confValues["textureTiling"] != moduleStrut.textureTiling) {
      moduleStrut.textureTiling = confValues["textureTiling"];
    }

    GUILayout.Space(25);
    GUILayout.EndVertical();
  }

  private void GuiConfigAnchorTab() {
    GUILayout.BeginVertical(guiDataboxStyle);

    //List all configuration values
    Dictionary<string, float> confValues = new Dictionary<string, float>();
    confValues.Add("anchorGroundDrag", moduleAnchor.groundDrag);
    confValues.Add("anchorBounciness", moduleAnchor.bounciness);
    confValues.Add("anchorDynamicFriction", moduleAnchor.dynamicFriction);
    confValues.Add("anchorStaticFriction", moduleAnchor.staticFriction);

    //Show config controls
    ConfigValues(confValues);

    //Set configuration if value changed
    if (confValues["anchorGroundDrag"] != moduleAnchor.groundDrag) {
      moduleAnchor.groundDrag = confValues["anchorGroundDrag"];
    }
    if (confValues["anchorBounciness"] != moduleAnchor.bounciness) {
      moduleAnchor.bounciness = confValues["anchorBounciness"];
    }
    if (confValues["anchorDynamicFriction"] != moduleAnchor.dynamicFriction) {
      moduleAnchor.dynamicFriction = confValues["anchorDynamicFriction"];
    }
    if (confValues["anchorStaticFriction"] != moduleAnchor.staticFriction) {
      moduleAnchor.staticFriction = confValues["anchorStaticFriction"];
    }
    GUILayout.Space(25);
    GUILayout.EndVertical();
  }

  private void GuiConfigGrappleTab() {
    GUILayout.BeginVertical(guiDataboxStyle);

    //List all configuration values
    Dictionary<string, float> confValues = new Dictionary<string, float>();
    confValues.Add("partBreakForce", moduleGrapple.partBreakForce);
    confValues.Add("staticBreakForce", moduleGrapple.staticBreakForce);
    confValues.Add("aboveDist", moduleGrapple.aboveDist);
    confValues.Add("forceNeeded", moduleGrapple.forceNeeded);
    confValues.Add("rayLenght", moduleGrapple.rayLenght);
    confValues.Add("RayDirx", moduleGrapple.rayDir.x);
    confValues.Add("RayDiry", moduleGrapple.rayDir.y);
    confValues.Add("RayDirz", moduleGrapple.rayDir.z);
          
    //Show config controls
    ConfigValues(confValues);

    //Set configuration if value changed
    if (confValues["partBreakForce"] != moduleGrapple.partBreakForce) {
      moduleGrapple.partBreakForce = confValues["partBreakForce"];
    }
    if (confValues["staticBreakForce"] != moduleGrapple.staticBreakForce) {
      moduleGrapple.staticBreakForce = confValues["staticBreakForce"];
    }
    if (confValues["aboveDist"] != moduleGrapple.aboveDist) {
      moduleGrapple.aboveDist = confValues["aboveDist"];
    }
    if (confValues["forceNeeded"] != moduleGrapple.forceNeeded) {
      moduleGrapple.forceNeeded = confValues["forceNeeded"];
    }
    if (confValues["rayLenght"] != moduleGrapple.rayLenght) {
      moduleGrapple.rayLenght = confValues["rayLenght"];
    }
    if (confValues["RayDirx"] != moduleGrapple.rayDir.x) {
      moduleGrapple.rayDir =
          new Vector3(confValues["RayDirx"], confValues["RayDiry"], confValues["RayDirz"]);
    }
    if (confValues["RayDiry"] != moduleGrapple.rayDir.y) {
      moduleGrapple.rayDir =
          new Vector3(confValues["RayDirx"], confValues["RayDiry"], confValues["RayDirz"]);
    }
    if (confValues["RayDirz"] != moduleGrapple.rayDir.z) {
      moduleGrapple.rayDir =
          new Vector3(confValues["RayDirx"], confValues["RayDiry"], confValues["RayDirz"]);
    }

    GUILayout.Space(25);
    GUILayout.EndVertical();
  }

  private void GuiConfigMagnetTab() {
    GUILayout.BeginVertical(guiDataboxStyle);

    //List all configuration values
    Dictionary<string, float> confValues = new Dictionary<string, float>();
    confValues.Add("breakForce", moduleMagnet.breakForce);
    confValues.Add("magnetPowerDrain", moduleMagnet.powerDrain);

    //Show config controls
    ConfigValues(confValues);

    //Toogle buttons for bool values
    moduleMagnet.attachToEva =
        GUILayout.Toggle(moduleMagnet.attachToEva, "attachToEva", guiButtonStyle);

    //Set configuration if value changed
    if (confValues["breakForce"] != moduleMagnet.breakForce) {
      moduleMagnet.breakForce = confValues["breakForce"];
    }
    if (confValues["magnetPowerDrain"] != moduleMagnet.powerDrain) {
      moduleMagnet.powerDrain = confValues["magnetPowerDrain"];
    }
          
    GUILayout.Space(25);
    GUILayout.EndVertical();
  }

  private void GuiConfigPortTab() {
    GUILayout.BeginVertical(guiDataboxStyle);

    //List all configuration values
    Dictionary<string, float> confValues = new Dictionary<string, float>();
    confValues.Add("breakForce", modulePort.breakForce);
    confValues.Add("rotateForce", modulePort.rotateForce);
          
    //Show config controls
    ConfigValues(confValues);

    //Set configuration if value changed
    if (confValues["breakForce"] != modulePort.breakForce) {
      modulePort.breakForce = confValues["breakForce"];
    }
    if (confValues["rotateForce"] != modulePort.rotateForce) {
      modulePort.rotateForce = confValues["rotateForce"];
    }

    GUILayout.Space(25);
    GUILayout.EndVertical();
  }

  private void GuiConfigRotorTab() {
    scrollPos = GUILayout.BeginScrollView(
        scrollPos, guiDataboxStyle, GUILayout.Width(500f), GUILayout.Height(400f));

    EditField("rotor powerDrain", ref moduleRotor.powerDrain);
    EditField("rotor breakForce", ref moduleRotor.breakForce);
    EditField("rotor rotorTransformName", ref moduleRotor.rotorTransformName);
    //EditField("rotor axis", ref moduleRotor.axis);
    EditField("rotor speed", ref moduleRotor.speed);
    EditField("rotor spring", ref moduleRotor.spring);
    EditField("rotor damper", ref moduleRotor.damper);
    EditField("rotor force", ref moduleRotor.force);
    EditField("rotor rotorMass", ref moduleRotor.rotorMass);
    EditField("rotor hasLimit", ref moduleRotor.hasLimit);
    EditField("rotor limitMin", ref moduleRotor.limitMin);
    EditField("rotor limitMinBounce", ref moduleRotor.limitMinBounce);
    EditField("rotor limitMax", ref moduleRotor.limitMax);
    EditField("rotor limitMaxBounce", ref moduleRotor.limitMaxBounce);
    EditField("rotor stopOffset", ref moduleRotor.stopOffset);
    EditField("rotor goToMinAngle", ref moduleRotor.goToMinAngle);
    EditField("rotor goToMinSpeed", ref moduleRotor.goToMinSpeed);
    EditField("rotor freeSpin", ref moduleRotor.freeSpin);
    EditField("rotor negativeWayText", ref moduleRotor.negativeWayText);
    EditField("rotor positiveWayText", ref moduleRotor.positiveWayText);
    EditField("rotor limitMin", ref moduleRotor.limitMin);
    EditField("rotor limitMin", ref moduleRotor.limitMin);

    GUILayout.EndScrollView();
  }

  private void EditField(string label, ref bool value, int maxLenght = 10) {
    value = GUILayout.Toggle(value, label, guiButtonStyle);
  }

  private void EditField(string label, ref Vector3 value, int maxLenght = 10) {
    if (!editFields.ContainsKey(label)) {
      editFields.Add(label + "x", value.x.ToString());
      editFields.Add(label + "y", value.y.ToString());
      editFields.Add(label + "z", value.z.ToString());
    }
    GUILayout.BeginHorizontal();
    GUILayout.Label(label + " : " + value + "   ", guiCenterStyle);
    editFields[label + "x"] = GUILayout.TextField(editFields[label + "x"], maxLenght);
    editFields[label + "y"] = GUILayout.TextField(editFields[label + "y"], maxLenght);
    editFields[label + "z"] = GUILayout.TextField(editFields[label + "z"], maxLenght);
    if (GUILayout.Button(new GUIContent("Set", "Set vector"),
                         guiButtonStyle, GUILayout.Width(60f))) {
      Vector3 tmpVector3 = new Vector3(float.Parse(editFields[label + "x"]),
                                       float.Parse(editFields[label + "y"]),
                                       float.Parse(editFields[label + "z"]));
      value = tmpVector3;
    }
    GUILayout.EndHorizontal();
  }

  private void EditField(string label, ref string value, int maxLenght = 10) {
    if (!editFields.ContainsKey(label))
      editFields.Add(label, value.ToString());

    GUILayout.BeginHorizontal();
    GUILayout.Label(label + " : " + value + "   ", guiCenterStyle);
    editFields[label] = GUILayout.TextField(editFields[label], maxLenght);
    if (GUILayout.Button(new GUIContent("Set", "Set string"),
                         guiButtonStyle, GUILayout.Width(60f))) {
      value = editFields[label];
    }
    GUILayout.EndHorizontal();
  }

  private void EditField(string label, ref float value, int maxLenght = 10) {
    if (!editFields.ContainsKey(label))
      editFields.Add(label, value.ToString());

    GUILayout.BeginHorizontal();
    GUILayout.Label(label + " : " + value + "   ", guiCenterStyle);
    editFields[label] = GUILayout.TextField(editFields[label], maxLenght);
    if (GUILayout.Button(new GUIContent("Set", "Set float"),
                         guiButtonStyle, GUILayout.Width(60f))) {
      value = float.Parse(editFields[label]);
    }
    GUILayout.EndHorizontal();
  }
}

}  // namespace
