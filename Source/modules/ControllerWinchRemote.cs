// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.GUIUtils;
using KSPDev.LogUtils;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;

namespace KAS {

/// <summary>Unity module that simulates remote control for the winches in the scene.</summary>
/// <remarks>
/// The winches are found by the interface. Any custom implementations will be recognized as winches
/// if they implement the <see cref="IWinchControl"/> interface. The remote control will only work
/// for the winches that belong to a controllable vessel. 
/// </remarks>
// Next localization ID: #kasLOC_11001.
[KSPAddon(KSPAddon.Startup.Flight, false /*once*/)]
sealed class ControllerWinchRemote : MonoBehaviour,
    IHasGUI {
  #region Localizable GUI strings.
  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message WindowTitleTxt = new Message(
      "#kasLOC_11000",
      defaultTemplate: "Winch Remote Control",
      description: "Title of the remote control dialog.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message ReleaseGuiBtn = new Message(
      null,
      defaultTemplate: "Release",
      description: "");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message ReleaseBtnHint = new Message(
      null,
      defaultTemplate: "Release the connector and set cable length to the maximum",
      description: "");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message StretchGuiBtn = new Message(
      null,
      defaultTemplate: "Stretch",
      description: "");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message StretchBtnHint = new Message(
      null,
      defaultTemplate: "Set the cable length to the actual distance",
      description: "");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message DetachGuiBtn = new Message(
      null,
      defaultTemplate: "Detach",
      description: "");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message DetachBtnHint = new Message(
      null,
      defaultTemplate: "Detach the cable from the target part",
      description: "");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message CloseGuiBtn = new Message(
      null,
      defaultTemplate: "Close",
      description: "");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message CloseBtnHint = new Message(
      null,
      defaultTemplate: "Close GUI",
      description: "");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message HightlightWinchBtn = new Message(
      null,
      defaultTemplate: "H",
      description: "");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message HightlightWinchBtnHint = new Message(
      null,
      defaultTemplate: "Highlight the winch in the scene",
      description: "");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message WinchModeOfflineTxt = new Message(
      null,
      defaultTemplate: "Offline",
      description: "");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message WinchModeOfflineTxtHint = new Message(
      null,
      defaultTemplate: "Cannot contact the winch. Is the vessel controllable?",
      description: "");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message WinchModeBlockedTxt = new Message(
      null,
      defaultTemplate: "Blocked",
      description: "");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message WinchModeBlockedTxtHint = new Message(
      null,
      defaultTemplate: "Winch is blocked by another part",
      description: "");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message WinchModeRetractedTxt = new Message(
      null,
      defaultTemplate: "Retracted",
      description: "");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message WinchModeRetractedTxtHint = new Message(
      null,
      defaultTemplate: "The connector is locked into the winch",
      description: "");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message StartRetractingCableBtnHint = new Message(
      null,
      defaultTemplate: "Start retracting the cable",
      description: "");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message StartExtendingCableBtnHint = new Message(
      null,
      defaultTemplate: "Start extending the cable",
      description: "");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message RetractCableBtnHint = new Message(
      null,
      defaultTemplate: "Retract the cable",
      description: "");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message ExtendCableBtnHint = new Message(
      null,
      defaultTemplate: "Extend the cable",
      description: "");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message MotorSpeedSettingsTxtHint = new Message(
      null,
      defaultTemplate: "Motor speed setting",
      description: "");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message CableLengthTxtHint = new Message(
      null,
      defaultTemplate: "The deployed/real length of the cable",
      description: "");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message MotorSpeedStatusTxtHint = new Message(
      null,
      defaultTemplate: "Current motor speed / Motor speed setting",
      description: "");
  #endregion

  #region Configuration settings
  /// <summary>Keyboard key to trigger the GUI.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// TODO(ihsoft): Load from config.
  string openGUIKey = "&P";  // Alt+P
  #endregion

  #region Internal helper types
  /// <summary>Storage for the cached winch state.</summary>
  class WinchState {
    public string vesselGUID;
    public string vesselName;
    public IWinchControl winchModule;
    public uint flightId;
    public float motorSpeedSetting = 1.0f;
    public bool extending;
    public bool extendBtnPressed;
    public bool retracting;
    public bool retractBtnPressed;
    public bool highlighted;
  }
  #endregion

  #region GUI styles and contents
  // These fields are set/updated in LoadLocalizedContent.
  GUIContent highlightWinchCnt;
  GUIContent startRetractingCnt;
  GUIContent startExtendingCnt;
  GUIContent retractCnt;
  GUIContent extendCnt;
  GUIContent winchModeOfflineCnt;
  GUIContent winchModeBlockedCnt;
  GUIContent winchModeRetractedCnt;
  GUIContent motorSpeedSettingsCnt;
  GUIContent cableStatusCnt;
  GUIContent motorSpeedCnt;
  GUIContent releaseBtnCnt;
  GUIContent stretchBtnCnt;
  GUIContent detachBtnCnt;
  GUIContent closeGuiCnt;
  #endregion

  #region Local fields
  /// <summary>Cached module states.</summary>
  /// <remarks>The key is the part's flight ID.</remarks>
  Dictionary<uint, WinchState> sceneModules = new Dictionary<uint, WinchState>();

  /// <summary>Ordered collection to use to draw the list in GUI.</summary>
  List<WinchState> sordedSceneModules = new List<WinchState>();

  /// <summary>Actual screen position of the console window.</summary>
  /// TODO(ihsoft): Persist and restore.
  static Rect windowRect = new Rect(100, 100, 1, 1);
  
  /// <summary>A title bar location.</summary>
  static Rect titleBarRect = new Rect(0, 0, 10000, 20);

  /// <summary>For every UI window Unity needs a unique ID. This is the one.</summary>
  const int WindowId = 20140221;

  /// <summary>A list of actions to apply at the end of the GUI frame.</summary>
  static readonly GuiActionsList guiActions = new GuiActionsList();

  /// <summary>Style to draw a control of the minimum size.</summary>
  static readonly GUILayoutOption MinSizeLayout = GUILayout.ExpandWidth(false);

  /// <summary>Keyboard event that opens/closes the remote GUI.</summary>
  static Event openGUIEvent;

  static ControllerWinchRemote instance;

  /// <summary>Tells if GUI is open.</summary>
  bool isGUIOpen;

  /// <summary>Tells if the list of cached winches needs to be refreshed.</summary>
  /// <remarks>This value is ched on every frame update, so don't update it too frequently</remarks>
  bool modulesNeedUpdate;

  /// <summary>
  /// Count of the loaded vessels in teh scene when the winch modules were updated last time.
  /// </summary>
  int lastKnownCount;

  /// <summary>Game time when the winch modules were updated last time.</summary>
  float lastTimeChecked;
  #endregion

  #region IHasGUI implementation
  /// <inheritdoc/>
  public void OnGUI() {
    if (Mathf.Approximately(Time.timeScale, 0)) {
      return;  // No events and menu in the paused mode.
    }
    if (Event.current.Equals(openGUIEvent)) {
      Event.current.Use();
      ToggleGUI(!isGUIOpen);
    }
    if (isGUIOpen) {
      windowRect = GUILayout.Window(WindowId, windowRect, ConsoleWindowFunc, WindowTitleTxt,
                                    GUILayout.MaxHeight(1), GUILayout.MaxWidth(1));
    }
  }
  #endregion

  #region Method for the outer modules
  /// <summary>Open the winches GUI.</summary>
  public static void ToggleGUI(bool isVisible) {
    if (instance != null) {
      if (isVisible) {
        instance.isGUIOpen = true;
        instance.modulesNeedUpdate = true;
      } else {
        instance.isGUIOpen = false;
      }
      DebugEx.Fine("Toggle winch remote control GUI: {0}", instance.isGUIOpen);
    }
  }
  #endregion

  #region MonoBehavour methods
  void Awake() {
    DebugEx.Info("Winch remote controller created");
    openGUIEvent = Event.KeyboardEvent(openGUIKey);
    instance = this;
    LoadLocalizedContent();
    GameEvents.onLanguageSwitched.Add(LoadLocalizedContent);
  }

  void OnDestroy() {
    DebugEx.Info("Winch remote controller destroyed");
    instance = null;
    GameEvents.onLanguageSwitched.Remove(LoadLocalizedContent);
  }
  #endregion

  /// <summary>Shows a window that displays the winch controls.</summary>
  /// <param name="windowId">Window ID.</param>
  void ConsoleWindowFunc(int windowId) {
    // HACK: Workaround a bug in the Unity GUI system. The alignment setting is not honored when set
    // in a customized style.
    GUI.skin.label.alignment = TextAnchor.MiddleCenter;
    var guiNoWrapStyle = new GUIStyle(GUI.skin.label);
    guiNoWrapStyle.wordWrap = false;

    if (guiActions.ExecutePendingGuiActions()) {
      MaybeUpdateModules();
    }

    // FIXME(ihsoft): There can be no elements.
    // TODO(ihsoft): Add paging and teh setting for the number of items per page.
    foreach (var winchState in sordedSceneModules) {
      var winch = winchState.winchModule;
      var winchCable = winchState.winchModule.linkJoint as ILinkCableJoint;
      var disableWinchGUI = false;
      var motorSpeed = winchState.motorSpeedSetting * winch.cfgMotorMaxSpeed;

      // Resolve the winche's state.
      GUIContent winchStatusCnt = null;
      if (!winch.part.vessel.IsControllable) {
        winchStatusCnt = winchModeOfflineCnt;
        disableWinchGUI = true;
      } else if (winch.isNodeBlocked || winch.isLocked) {
        winchStatusCnt = winchModeBlockedCnt;
        disableWinchGUI = true;
      } else if (winch.isConnectorLocked) {
        winchStatusCnt = winchModeRetractedCnt;
      }

      //using (new GUILayout.HorizontalScope(GUI.skin.box)) {
      using (new GUILayout.HorizontalScope(GUI.skin.box)) {
        // Winch highlighting column.
        var highlighted = GUILayout.Toggle(
            winchState.highlighted, highlightWinchCnt, GUI.skin.button, MinSizeLayout);
        if (highlighted && !winchState.highlighted) {
          winch.part.SetHighlight(true, false);
          winch.part.SetHighlightType(Part.HighlightType.AlwaysOn);
          winchState.highlighted = true;
        } else if (!highlighted && winchState.highlighted) {
          winch.part.SetHighlightDefault();
          winchState.highlighted = false;
        }

        // Cable retracting controls.
        using (new GuiEnabledStateScope(!disableWinchGUI && winchCable.realCableLength > 0)) {
          // Start retracting the cable column.
          winchState.retractBtnPressed &= winch.motorTargetSpeed < 0;
          KSPUtilsGUILayout.ToggleButton(
              ref winchState.retractBtnPressed,
              startRetractingCnt,
              GUI.skin.button,
              new[] {MinSizeLayout},
              fnOn: () => winch.SetMotor(-motorSpeed),
              fnOff: () => winch.SetMotor(0));
          // Retract the cable column.
          winchState.retracting &= winch.motorTargetSpeed < 0;
          KSPUtilsGUILayout.PushButton(
              ref winchState.retracting,
              retractCnt,
              GUI.skin.button,
              new[] {MinSizeLayout},
              fnPush: () => winch.SetMotor(-motorSpeed),
              fnRelease: () => winch.SetMotor(0));
        }
        
        // Cable lenght/status column.
        if (winchStatusCnt != null) {
          GUILayout.Label(winchStatusCnt, guiNoWrapStyle, GUILayout.Width(150));
        } else {
          var text = DistanceType.Format(winch.currentCableLength) + " / ";
          var realLength = winchCable.realCableLength;
          if (realLength > winch.currentCableLength) {
            text += ScreenMessaging.SetColorToRichText(
                DistanceType.Format(realLength), Color.magenta);
          } else {
            text += DistanceType.Format(realLength);
          }
          cableStatusCnt.text = text;
          GUILayout.Label(cableStatusCnt, guiNoWrapStyle, GUILayout.Width(150));
        }

        // Cable extending controls.
        using (new GuiEnabledStateScope(
            !disableWinchGUI && winchCable.deployedCableLength < winch.cfgMaxCableLength)) {
          // Start extending the cable column.
          winchState.extendBtnPressed &= winch.motorTargetSpeed > 0;
          KSPUtilsGUILayout.ToggleButton(
              ref winchState.extendBtnPressed,
              startExtendingCnt,
              GUI.skin.button,
              new[] {MinSizeLayout},
              fnOn: () => winch.SetMotor(motorSpeed),
              fnOff: () => winch.SetMotor(0));
          // Extend the cable column.
          winchState.extending &= winch.motorTargetSpeed > 0;
          KSPUtilsGUILayout.PushButton(
              ref winchState.extending,
              extendCnt,
              GUI.skin.button,
              new[] {MinSizeLayout},
              fnPush: () => winch.SetMotor(motorSpeed),
              fnRelease: () => winch.SetMotor(0));
        }

        using (new GuiEnabledStateScope(!disableWinchGUI)) {
          // Motor speed settings column.
          GUI.changed = false;
          var newMotorSpeedSetting = GUILayout.HorizontalSlider(
              winchState.motorSpeedSetting, 0.1f, 1.0f,
              GUILayout.Width(100f));
          GUI.Box(GUILayoutUtility.GetLastRect(), motorSpeedSettingsCnt);
          if (GUI.changed) {
            guiActions.Add(() => {
              winchState.motorSpeedSetting = newMotorSpeedSetting;
              var newSpeed = newMotorSpeedSetting * winch.cfgMotorMaxSpeed;
              if (winchState.extending || winchState.extendBtnPressed) {
                winch.SetMotor(newSpeed);
              }
              if (winchState.retracting || winchState.retractBtnPressed) {
                winch.SetMotor(-newSpeed);
              }
            });
          }
        }

        // Motor speed info column.
        motorSpeedCnt.text =
            VelocityType.Format(Mathf.Abs(winch.motorCurrentSpeed))
            + " / "
            + VelocityType.Format(motorSpeed);
        GUILayout.Label(motorSpeedCnt, guiNoWrapStyle, GUILayout.Width(150f));

        // Release cable column.
        using (new GuiEnabledStateScope(
            !disableWinchGUI && winch.currentCableLength < winch.cfgMaxCableLength)) {
          if (GUILayout.Button(releaseBtnCnt, GUI.skin.button, MinSizeLayout)) {
            winch.ReleaseCable();
          }
        }

        // Stretch cable column.
        using (new GuiEnabledStateScope(!disableWinchGUI && !winch.isConnectorLocked)) {
          if (GUILayout.Button(stretchBtnCnt, GUI.skin.button, MinSizeLayout)) {
            winch.StretchCable();
          }
        }

        // Disconnect connector column.
        using (new GuiEnabledStateScope(!disableWinchGUI && winch.isLinked)) {
          if (GUILayout.Button(detachBtnCnt, GUI.skin.button, MinSizeLayout)) {
            winch.BreakCurrentLink(LinkActorType.Player);
          }
        }
      }
    }
    
    using (new GUILayout.HorizontalScope()) {
      if (GUILayout.Button(closeGuiCnt, MinSizeLayout)) {
        guiActions.Add(() => isGUIOpen = false);
      }
      GUILayout.Label("");
      GUI.Label(GUILayoutUtility.GetLastRect(), GUI.tooltip);
    }

    // Allow the window to be dragged by its title bar.
    GuiWindow.DragWindow(ref windowRect, titleBarRect);
  }

  /// <summary>Prepares or updates the localizable GUI strings.</summary>
  void LoadLocalizedContent() {
    highlightWinchCnt = new GUIContent(HightlightWinchBtn, HightlightWinchBtnHint);
    startRetractingCnt = new GUIContent("<<", StartRetractingCableBtnHint);
    startExtendingCnt = new GUIContent(">>", StartExtendingCableBtnHint);
    retractCnt = new GUIContent("<", RetractCableBtnHint);
    extendCnt = new GUIContent(">", ExtendCableBtnHint);
    releaseBtnCnt = new GUIContent(ReleaseGuiBtn, ReleaseBtnHint);
    stretchBtnCnt = new GUIContent(StretchGuiBtn, StretchBtnHint);
    detachBtnCnt = new GUIContent(DetachGuiBtn, DetachBtnHint);
    closeGuiCnt = new GUIContent(CloseGuiBtn, CloseBtnHint);

    motorSpeedSettingsCnt = new GUIContent("", MotorSpeedSettingsTxtHint);
    cableStatusCnt = new GUIContent("", CableLengthTxtHint);
    motorSpeedCnt = new GUIContent("", MotorSpeedStatusTxtHint);

    winchModeOfflineCnt = new GUIContent(
        ScreenMessaging.SetColorToRichText(WinchModeOfflineTxt, Color.red),
        WinchModeOfflineTxtHint);
    winchModeBlockedCnt = new GUIContent(
        ScreenMessaging.SetColorToRichText(WinchModeBlockedTxt, Color.red),
        WinchModeBlockedTxtHint);
    winchModeRetractedCnt = new GUIContent(
        ScreenMessaging.SetColorToRichText(WinchModeRetractedTxt, Color.green),
        WinchModeRetractedTxtHint);
  }

  /// <summary>Checks if the cached list of the winch controllers needs to be refreshed.</summary>
  void MaybeUpdateModules() {
    // Try to avoid too frequent refreshes.
    // TODO(ihsoft): Updates once per a second slows down significantly. Find a better way.
    if (FlightGlobals.Vessels.Count == lastKnownCount
        && Time.time < lastTimeChecked + 1.0f
        && !modulesNeedUpdate) {
      return;  // Nothing changed.
    }
    lastKnownCount = FlightGlobals.Vessels.Count;
    lastTimeChecked = Time.time;
    modulesNeedUpdate = false;
    
    DebugEx.Fine("Updating winch modules...");
    sordedSceneModules = FlightGlobals.VesselsLoaded
        .Where(v => !v.packed)
        .SelectMany(v => v.parts)
        .SelectMany(p => p.Modules.OfType<IWinchControl>())
        .Select(w => sceneModules.ContainsKey(w.part.flightID)
            ? sceneModules[w.part.flightID]
            : new WinchState() {
              vesselGUID = w.part.vessel.id.ToString(),
              vesselName = w.part.vessel.vesselName,
              flightId = w.part.flightID,
              winchModule = w,
            })
        .OrderBy(s => s.vesselGUID)
        .ThenBy(s => s.flightId)
        .ToList();
    sceneModules = sordedSceneModules.ToDictionary(s => s.flightId);
    modulesNeedUpdate = false;
    DebugEx.Fine("Found {0} winch modules", sordedSceneModules.Count);
  }
}

}  // namespace
