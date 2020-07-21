// Kerbal Attachment System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv2;
using KSPDev.ConfigUtils;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.LogUtils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KAS {

/// <summary>Unity module that simulates remote control for the winches in the scene.</summary>
/// <remarks>
/// The winches are found by the interface. Any custom implementations will be recognized as winches
/// if they implement the <see cref="IWinchControl"/> interface. The remote control will only work
/// for the winches that belong to a controllable vessel. 
/// </remarks>
// Next localization ID: #kasLOC_11028.
[KSPAddon(KSPAddon.Startup.Flight, false /*once*/)]
[PersistentFieldsDatabase("KAS/settings/KASConfig")]
internal sealed class ControllerWinchRemote : MonoBehaviour, IHasGUI {
  #region Localizable GUI strings.
  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message<KeyboardEventType> WindowTitleTxt = new Message<KeyboardEventType>(
      "#kasLOC_11000",
      defaultTemplate: "Winch Remote Control (<<1>>)",
      description: "The title of the remote control dialog. It also gives a hint on the keyboard"
      + " sequence that brings the GUI up."
      + "\nArgument <<1>> is the keyboard even of type KeyboardEventType.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message ReleaseBtn = new Message(
      "#kasLOC_11001",
      defaultTemplate: "Release",
      description: "The caption of the button that triggers cable release.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message ReleaseBtnHint = new Message(
      "#kasLOC_11002",
      defaultTemplate: "Release the connector and set cable length to the maximum",
      description: "The GUI hint to explain the effect of the release button.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message StretchBtn = new Message(
      "#kasLOC_11003",
      defaultTemplate: "Stretch",
      description: "The caption of the button that stretches teeh cable.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message StretchBtnHint = new Message(
      "#kasLOC_11004",
      defaultTemplate: "Set the cable length to the actual distance",
      description: "The GUI hint to explain the effect of the stretch button.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message DetachBtn = new Message(
      "#kasLOC_11005",
      defaultTemplate: "Detach",
      description: "The caption of the button that deatches the cable from the target.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message DetachBtnHint = new Message(
      "#kasLOC_11006",
      defaultTemplate: "Detach the cable from the target part",
      description: "The GUI hint to explain the effect of the detach button.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message CloseBtn = new Message(
      "#kasLOC_11007",
      defaultTemplate: "Close",
      description: "The caption of the button that closes the GUI dialog.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message CloseBtnHint = new Message(
      "#kasLOC_11008",
      defaultTemplate: "Close GUI",
      description: "The GUI hint to explain the effect of the close button.");

  static readonly Message HightlightWinchBtn = new Message(
  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
      "#kasLOC_11009",
      defaultTemplate: "H",
      description: "The caption for the toggle control (button style) which tells if the winch"
      + " should be highlighted in the scene. It's better keep the text as short as possible.");

  static readonly Message HightlightWinchBtnHint = new Message(
  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
      "#kasLOC_11010",
      defaultTemplate: "Highlight the winch in the scene",
      description: "The GUI hint to explain the effect of toggling the control.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message WinchModeOfflineTxt = new Message(
      "#kasLOC_11011",
      defaultTemplate: "<gui:min:150,0><color=red>Offline</color>",
      description: "The text for the winch status in which it cannot be remotely operated for any"
      + " reason.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message WinchModeOfflineTxtHint = new Message(
      "#kasLOC_11012",
      defaultTemplate: "Cannot contact the winch. Is the vessel controllable?",
      description: "The GUI hint to explain the OFFLINE state.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message WinchModeBlockedTxt = new Message(
      "#kasLOC_11013",
      defaultTemplate: "<gui:min:150,0><color=red>Blocked</color>",
      description: "The text for the winch status that tells that the main winch attach node is"
      + " occupied by an incompatible (non-KAS) part.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message WinchModeBlockedTxtHint = new Message(
      "#kasLOC_11014",
      defaultTemplate: "Winch attach node is blocked by another part",
      description: "The GUI hint to explain the BLOCKED state.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message WinchModeRetractedTxt = new Message(
      "#kasLOC_11015",
      defaultTemplate: "<gui:min:150,0><color=#00ff00>Retracted</color>",
      description: "The text for the winch status that tells that the cable connector is locked to"
      + " the winch, and the cable length is zero.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message WinchModeRetractedTxtHint = new Message(
      "#kasLOC_11016",
      defaultTemplate: "The connector is locked into the winch",
      description: "The GUI hint to explain the RETRACTED state.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message StartRetractingCableBtnHint = new Message(
      "#kasLOC_11017",
      defaultTemplate: "Start retracting the cable",
      description: "The GUI hint of the button that triggers retracting of the cable. The cable"
      + " will be retracting until the motor status is changed or the connector get locked.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message StartExtendingCableBtnHint = new Message(
      "#kasLOC_11018",
      defaultTemplate: "Start extending the cable",
      description: "The GUI hint of the button that triggers deploying of the cable. The cable will"
      + " be deploying until the motor status is changed.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message RetractCableBtnHint = new Message(
      "#kasLOC_11019",
      defaultTemplate: "Retract the cable",
      description: "The GUI hint of the button that retracts the cable. The cable will be"
      + " retracting as long as the button is pressed.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message ExtendCableBtnHint = new Message(
      "#kasLOC_11020",
      defaultTemplate: "Extend the cable",
      description: "The GUI hint of the button that extends the cable. The cable will be"
      + " extending as long as the button is pressed.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message MotorSpeedSettingsTxtHint = new Message(
      "#kasLOC_11021",
      defaultTemplate: "Motor speed setting",
      description: "The GUI hint to show for the control that changes the motor speed. It's the"
      + " maximum speed which the motor can reach when retracting or extending the cable.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message CableLengthTxtHint = new Message(
      "#kasLOC_11022",
      defaultTemplate: "The deployed/real length of the cable",
      description: "The GUI hint to show for the area which displays two values: the deployed cable"
      + " length and the real distance between the winch and the target (connector or part). The"
      + " values are presented as a pair, separated by symbol '/'.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message MotorSpeedStatusTxtHint = new Message(
      "#kasLOC_11023",
      defaultTemplate: "Current motor speed / Motor speed setting",
      description: "The GUI hint to show for the area which displays two values: the current motro"
      + " speed and the maximum possible motor speed. The values are presented as a pair, separated"
      + " by symbol '/'.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message NoContentTxt = new Message(
      "#kasLOC_11024",
      defaultTemplate: "No winches found in the scene!",
      description: "The string to present when the dialog is opened, but no KAS winches found in"
      + " the scene.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message2/*"/>
  static readonly Message<DistanceType, DistanceType> RelaxedCableLengthTxt =
      new Message<DistanceType, DistanceType>(
          "#kasLOC_11025",
          defaultTemplate: "<gui:min:150,0><<1>> / <<2>>",
          description: "The formatter string for the cable lengths when the cable is *not* under"
          + " strain. I.e. its actual length is not greater than the winch allows."
          + "\nArgument <<1>> is the length, allowed by the winch of type DistanceType."
          + "\nArgument <<1>> is the real cable length of type DistanceType.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message2/*"/>
  static readonly Message<DistanceType, DistanceType> StrainedCableLengthTxt =
      new Message<DistanceType, DistanceType>(
          "#kasLOC_11026",
          defaultTemplate: "<gui:min:150,0><<1>> / <color=magenta><<2>></color>",
          description: "The formatter string for the cable lengths when the cable *is* under strain."
          + " I.e. its actual length is greater than the winch allows."
          + "\nArgument <<1>> is the length, allowed by the winch of type DistanceType."
          + "\nArgument <<2>> is the real cable length of type DistanceType.");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message2/*"/>
  static readonly Message<VelocityType, VelocityType> MotorSpeedTxt =
      new Message<VelocityType, VelocityType>(
          "#kasLOC_11027",
          defaultTemplate: "<gui:min:150,0><<1>> / <<2>>",
          description: "The formatter string for the winch motor speed."
          + "\nArgument <<1>> is the current motor speed type VelocityType."
          + "\nArgument <<2>> is the settings for the desired motor speed of type VelocityType.");
  #endregion

  #region Configuration settings
  /// <summary>Keyboard key to trigger the GUI.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [PersistentField("Winch/remoteControlKey")]
  public string openGUIKey = "&P";  // Alt+P
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
  GUIStyle guiNoWrapStyle;
  GUIStyle guiNoWrapCenteredStyle;
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
  float winchCableStatusMinWidth = 0;
  #endregion

  #region Local fields
  /// <summary>Cached module states.</summary>
  /// <remarks>The key is the part's flight ID.</remarks>
  Dictionary<uint, WinchState> sceneModules = new Dictionary<uint, WinchState>();

  /// <summary>Ordered collection to use to draw the list in GUI.</summary>
  WinchState[] sortedSceneModules;

  /// <summary>Actual screen position of the console window.</summary>
  /// TODO(ihsoft): Persist and restore.
  static Rect windowRect = new Rect(100, 100, 1, 1);
  
  /// <summary>A title bar location.</summary>
  static Rect titleBarRect = new Rect(0, 0, 10000, 20);

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

  /// <summary>GUI table to align winch status fields.</summary>
  /// <remarks>Cable status + Motor status</remarks>
  readonly GUILayoutStringTable guiWinchTable = new GUILayoutStringTable(2);
  #endregion

  #region IHasGUI implementation
  /// <inheritdoc/>
  public void OnGUI() {
    if (Time.timeScale <= float.Epsilon) {
      return;  // No events and menu in the paused mode.
    }
    if (Event.current.Equals(openGUIEvent)) {
      Event.current.Use();
      ToggleGUI(!isGUIOpen);
    }
    if (isGUIOpen) {
      windowRect = GUILayout.Window(
          GetInstanceID(), windowRect, ConsoleWindowFunc, WindowTitleTxt.Format(openGUIEvent),
          GUILayout.MaxHeight(1), GUILayout.MaxWidth(1));
    }
  }
  #endregion

  #region Methods for the outer modules
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
    ConfigAccessor.ReadFieldsInType(GetType(), this);
    openGUIEvent = Event.KeyboardEvent(openGUIKey);
    instance = this;
    LoadLocalizedContent();
    GameEvents.onLanguageSwitched.Add(LoadLocalizedContent);
    GameEvents.onVesselWasModified.Add(OnVesselUpdated);
    GameEvents.onVesselDestroy.Add(OnVesselUpdated);
    GameEvents.onVesselCreate.Add(OnVesselUpdated);
  }

  void OnDestroy() {
    DebugEx.Info("Winch remote controller destroyed");
    instance = null;
    GameEvents.onLanguageSwitched.Remove(LoadLocalizedContent);
    GameEvents.onVesselWasModified.Remove(OnVesselUpdated);
    GameEvents.onVesselDestroy.Remove(OnVesselUpdated);
    GameEvents.onVesselCreate.Remove(OnVesselUpdated);
  }
  #endregion

  /// <summary>Shows a window that displays the winch controls.</summary>
  /// <param name="windowId">Window ID.</param>
  void ConsoleWindowFunc(int windowId) {
    MakeGuiStyles();

    if (guiActions.ExecutePendingGuiActions()) {
      MaybeUpdateModules();
      guiWinchTable.UpdateFrame();
    }

    if (sortedSceneModules.Length == 0) {
      GUILayout.Label(NoContentTxt, guiNoWrapStyle);
    }

    // TODO(ihsoft): Add paging and the setting for the number of items per page.
    // Render the winch items if any.
    for (var i = 0; i < sortedSceneModules.Length; i++) {
      var winchState = sortedSceneModules[i];
      var winch = winchState.winchModule;
      var winchCable = winchState.winchModule.linkJoint as ILinkCableJoint;
      var disableWinchGUI =
          !winch.part.vessel.IsControllable || winch.isNodeBlocked || winch.isLocked;
      var motorSpeed = winchState.motorSpeedSetting * winch.cfgMotorMaxSpeed;
      guiWinchTable.StartNewRow();

      using (new GUILayout.HorizontalScope(GUI.skin.box)) {
        // Winch highlighting column.
        winchState.highlighted = GUILayoutButtons.Toggle(
            winchState.highlighted, highlightWinchCnt, GUI.skin.button, null,
            fnOn: () => {
              winch.part.SetHighlight(true, false);
              winch.part.SetHighlightType(Part.HighlightType.AlwaysOn);
            },
            fnOff: () => {
              winch.part.SetHighlightDefault();
            },
            actionsList: guiActions);

        // Cable retracting controls.
        using (new GuiEnabledStateScope(!disableWinchGUI && winchCable.realCableLength > 0)) {
          // Start retracting the cable column.
          winchState.retractBtnPressed &= winch.motorTargetSpeed < 0;
          winchState.retractBtnPressed = GUILayoutButtons.Toggle(
              winchState.retractBtnPressed,
              startRetractingCnt,
              GUI.skin.button,
              new[] {MinSizeLayout},
              fnOn: () => winch.SetMotor(-motorSpeed),
              fnOff: () => winch.SetMotor(0),
              actionsList: guiActions);
          // Retract the cable column.
          winchState.retracting &= winch.motorTargetSpeed < 0;
          winchState.retracting = GUILayoutButtons.Push(
              winchState.retracting,
              retractCnt,
              GUI.skin.button,
              new[] {MinSizeLayout},
              fnPush: () => winch.SetMotor(-motorSpeed),
              fnRelease: () => winch.SetMotor(0),
              actionsList: guiActions);
        }
        
        // Cable lenght/status column.
        if (!winch.part.vessel.IsControllable) {
          guiWinchTable.AddTextColumn(
              winchModeOfflineCnt, guiNoWrapCenteredStyle, minWidth: winchCableStatusMinWidth);
        } else if (winch.isNodeBlocked || winch.isLocked) {
          guiWinchTable.AddTextColumn(
              winchModeBlockedCnt, guiNoWrapCenteredStyle, minWidth: winchCableStatusMinWidth);
        } else if (winch.isConnectorLocked) {
          guiWinchTable.AddTextColumn(
              winchModeRetractedCnt, guiNoWrapCenteredStyle, minWidth: winchCableStatusMinWidth);
        } else {
          cableStatusCnt.text = winchCable.realCableLength <= winch.currentCableLength
              ? RelaxedCableLengthTxt.Format(winch.currentCableLength, winchCable.realCableLength)
              : StrainedCableLengthTxt.Format(winch.currentCableLength, winchCable.realCableLength);
          guiWinchTable.AddTextColumn(
              cableStatusCnt, guiNoWrapCenteredStyle, minWidth: winchCableStatusMinWidth);
        }

        // Cable extending controls.
        using (new GuiEnabledStateScope(
            !disableWinchGUI && winchCable.deployedCableLength < winch.cfgMaxCableLength)) {
          // Start extending the cable column.
          winchState.extendBtnPressed &= winch.motorTargetSpeed > 0;
          winchState.extendBtnPressed = GUILayoutButtons.Toggle(
              winchState.extendBtnPressed,
              startExtendingCnt,
              GUI.skin.button,
              new[] {MinSizeLayout},
              fnOn: () => winch.SetMotor(motorSpeed),
              fnOff: () => winch.SetMotor(0),
              actionsList: guiActions);
          // Extend the cable column.
          winchState.extending &= winch.motorTargetSpeed > 0;
          winchState.extending = GUILayoutButtons.Push(
              winchState.extending,
              extendCnt,
              GUI.skin.button,
              new[] {MinSizeLayout},
              fnPush: () => winch.SetMotor(motorSpeed),
              fnRelease: () => winch.SetMotor(0),
              actionsList: guiActions);
        }

        using (new GuiEnabledStateScope(!disableWinchGUI)) {
          // Motor speed settings column.
          using (new GUILayout.VerticalScope(motorSpeedSettingsCnt, GUI.skin.label)) {
            GUI.changed = false;
            GUILayout.FlexibleSpace();
            var newMotorSpeedSetting = GUILayout.HorizontalSlider(
                winchState.motorSpeedSetting, 0.1f, 1.0f,
                GUILayout.Width(100f));
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
        }

        // Motor speed info column.
        motorSpeedCnt.text = MotorSpeedTxt.Format(Mathf.Abs(winch.motorCurrentSpeed), motorSpeed);
        guiWinchTable.AddTextColumn(
            motorSpeedCnt, guiNoWrapCenteredStyle, minWidth: MotorSpeedTxt.guiTags.minWidth);

        // Release cable column.
        using (new GuiEnabledStateScope(
            !disableWinchGUI && winch.currentCableLength < winch.cfgMaxCableLength)) {
          if (GUILayout.Button(releaseBtnCnt, GUI.skin.button, MinSizeLayout)) {
            guiActions.Add(winch.ReleaseCable);
          }
        }

        // Stretch cable column.
        using (new GuiEnabledStateScope(!disableWinchGUI && !winch.isConnectorLocked)) {
          if (GUILayout.Button(stretchBtnCnt, GUI.skin.button, MinSizeLayout)) {
            guiActions.Add(winch.StretchCable);
          }
        }

        // Disconnect connector column.
        using (new GuiEnabledStateScope(!disableWinchGUI && winch.isLinked)) {
          if (GUILayout.Button(detachBtnCnt, GUI.skin.button, MinSizeLayout)) {
            guiActions.Add(() => winch.BreakCurrentLink(LinkActorType.Player));
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

  /// <summary>Creates the styles. Only does it once.</summary>
  void MakeGuiStyles() {
    if (guiNoWrapStyle == null) {
      guiNoWrapStyle = new GUIStyle(GUI.skin.label);
      guiNoWrapStyle.stretchHeight = true;
      guiNoWrapStyle.wordWrap = false;
      guiNoWrapStyle.alignment = TextAnchor.MiddleLeft;
      guiNoWrapCenteredStyle = new GUIStyle(guiNoWrapStyle);
      guiNoWrapCenteredStyle.alignment = TextAnchor.MiddleCenter;
    }
  }

  /// <summary>Prepares or updates the localizable GUI strings.</summary>
  void LoadLocalizedContent() {
    highlightWinchCnt = new GUIContent(HightlightWinchBtn, HightlightWinchBtnHint);
    startRetractingCnt = new GUIContent("<<", StartRetractingCableBtnHint);
    startExtendingCnt = new GUIContent(">>", StartExtendingCableBtnHint);
    retractCnt = new GUIContent("<", RetractCableBtnHint);
    extendCnt = new GUIContent(">", ExtendCableBtnHint);
    releaseBtnCnt = new GUIContent(ReleaseBtn, ReleaseBtnHint);
    stretchBtnCnt = new GUIContent(StretchBtn, StretchBtnHint);
    detachBtnCnt = new GUIContent(DetachBtn, DetachBtnHint);
    closeGuiCnt = new GUIContent(CloseBtn, CloseBtnHint);

    motorSpeedSettingsCnt = new GUIContent("", MotorSpeedSettingsTxtHint);
    cableStatusCnt = new GUIContent("", CableLengthTxtHint);
    motorSpeedCnt = new GUIContent("", MotorSpeedStatusTxtHint);
    MotorSpeedTxt.LoadLocalization();  // To update guiTags. 

    winchModeOfflineCnt = new GUIContent(WinchModeOfflineTxt, WinchModeOfflineTxtHint);
    winchModeBlockedCnt = new GUIContent(WinchModeBlockedTxt, WinchModeBlockedTxtHint);
    winchModeRetractedCnt = new GUIContent(WinchModeRetractedTxt, WinchModeRetractedTxtHint);
    RelaxedCableLengthTxt.LoadLocalization();  // To update guiTags.
    StrainedCableLengthTxt.LoadLocalization();  // To update guiTags.
    winchCableStatusMinWidth = Mathf.Max(
        RelaxedCableLengthTxt.guiTags.minWidth,
        StrainedCableLengthTxt.guiTags.minWidth,
        WinchModeOfflineTxt.guiTags.minWidth,
        WinchModeBlockedTxt.guiTags.minWidth,
        WinchModeRetractedTxt.guiTags.minWidth);
  }

  /// <summary>Checks if the cached list of the winch controllers needs to be refreshed.</summary>
  void MaybeUpdateModules() {
    if (!modulesNeedUpdate) {
      return;
    }
    modulesNeedUpdate = false;
    DebugEx.Fine("Updating winch modules...");
    sortedSceneModules = FlightGlobals.VesselsLoaded
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
        .ToArray();
    sceneModules = sortedSceneModules.ToDictionary(s => s.flightId);
    DebugEx.Fine("Found {0} winch modules", sortedSceneModules.Length);
  }

  /// <summary>
  /// Forces an update of the list of the cached wiches. It's an expensive operation.
  /// </summary>
  void OnVesselUpdated(Vessel v) {
    modulesNeedUpdate = true;
  }
}

}  // namespace
