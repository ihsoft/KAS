// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.GUIUtils;
using KSPDev.PartUtils;
using KSPDev.ProcessingUtils;
using KASAPIv1;
using System;
using System.Linq;
using UnityEngine;

namespace KAS {

/// <summary>Module that allows connecting the parts by a mouse via GUI.</summary>
/// <remarks>
/// When the player starts the linking mode, he must either complete it by clicking on a compatible
/// target part or abort the mode altogether.
/// <para>
/// EVA kerbal movement is locked when linking mode is active, so both source and target parts
/// must be in the range from the kerbal.
/// </para>
/// </remarks>
// Next localization ID: #kasLOC_01004.
public sealed class KASModuleInteractiveLinkSource : KASModuleLinkSourceBase,
    // KSPDev interfaces.
    IHasContextMenu, IsLocalizableModule {

  #region Localizable GUI strings
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.DistanceType']/*"/>
  static readonly Message<DistanceType> CanBeConnectedMsg = new Message<DistanceType>(
      "#kasLOC_01000",
      defaultTemplate: "Click to establish a link (length <<1>>)",
      description: "Message to display when a compatible target part is hovered over and the source"
      + " is in the linking mode."
      + "\nArgument <<1>> is the possible link length of type DistanceType.",
      example: "Click to establish a link (length 1.22 m)");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message LinkingInProgressMsg = new Message(
      "#kasLOC_01001",
      defaultTemplate: "Select a compatible socket or press ESC",
      description: "Message to display as a help string when an interactive linking mode has"
      + " started.");
  #endregion

  #region Part's config fields
  /// <summary>Audio sample to play when the parts are attached by the player.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string sndPathPlug = "";

  /// <summary>Audio sample to play when the parts are detached by the player.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string sndPathUnplug = "";

  /// <summary>Audio sample to play when the parts are docked by the player.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string sndPathDock = "";

  /// <summary>Audio sample to play when the parts are undocked by the player.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string sndPathUndock = "";

  /// <summary>Audio sample to play when the link is broken by the physics events.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string sndPathBroken = "";

  /// <summary>Name of the menu item to start linking mode.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string startLinkMenu = "";

  /// <summary>Name of the menu item to break currently established link.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string breakLinkMenu = "";

  /// <summary>
  /// Tells if there should be a UI menu item that allows switching the docked (coupled) mode of the
  /// joint.
  /// </summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public bool allowChanginDockingMode;
  #endregion

  // TODO(ihsoft): Disallow non-eva control.
  #region Context menu events/actions
  /// <summary>Event handler. Initiates a link that must be completed by a mouse click.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true, guiActiveUnfocused = true)]
  [LocalizableItem(tag = null)]
  public void StartLinkContextMenuAction() {
    StartLinking(GUILinkMode.Interactive, LinkActorType.Player);
  }

  /// <summary>Event handler. Breaks current link between source and target.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true, guiActiveUnfocused = true)]
  [LocalizableItem(tag = null)]
  public void BreakLinkContextMenuAction() {
    BreakCurrentLink(LinkActorType.Player);
  }

  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true, guiActiveUnfocused = true)]
  [LocalizableItem(
      tag = "#kasLOC_01002",
      defaultTemplate = "Link mode: DOCKED",
      description = "The name of the part's context menu event that triggers a separtation of the"
      + " linked parts into two different vessels if they are coupled thru this joint. At the same"
      + " time, the name of the event gives a currently selected state.")]
  public void UndockVesselsContextMenuAction() {
    linkJoint.SetCoupleOnLinkMode(false, LinkActorType.Player);
    UpdateContextMenu();
  }

  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  /// FIXME: review caption string
  [KSPEvent(guiActive = true, guiActiveUnfocused = true)]
  [LocalizableItem(
      tag = "#kasLOC_01003",
      defaultTemplate = "Link mode: UNDOCKED",
      description = "The name of the part's context menu event that triggers a merging of the"
      + " linked parts if they were not coupled before. At  the same time, the name of the event"
      + " gives a currently selected state.")]
  public void DockVesselsContextMenuAction() {
    linkJoint.SetCoupleOnLinkMode(true, LinkActorType.Player);
    UpdateContextMenu();
  }
  #endregion

  #region Local properties and fields
  /// <summary>Color of the pipe in the linking mode when the link can be established.</summary>
  static readonly Color GoodLinkColor = new Color(0, 1, 0, 0.5f);

  /// <summary>Color of the pipe in the linking mode when the link cannot be established.</summary>
  static readonly Color BadLinkColor = new Color(1, 0, 0, 0.5f);

  /// <summary>The lock name that restricts anything but the camera positioning.</summary>
  const string TotalControlLock = "KASInteractiveJointUberLock";

  /// <summary>Shader that reders the pipe during linking.</summary>
  const string InteractiveShaderName = "Transparent/Diffuse";  

  /// <summary>The compatible target under the mouse cursor.</summary>
  ILinkTarget targetCandidate;

  /// <summary>Tells if the connection with the candidate will be successful.</summary>
  bool targetCandidateIsGood;

  /// <summary>
  /// The last known hovered part. Used to trigger the detection of the target candidate.
  /// </summary>
  Part lastHoveredPart;

  /// <summary>The message, displayed during the interactive linking.</summary>
  ScreenMessage statusScreenMessage;

  /// <summary>
  /// A variable to store the auto save state before starting the interactive mode.
  /// </summary>
  bool canAutoSaveState;
  #endregion

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    LocalizeModule();
  }

  /// <inheritdoc/>
  public override void OnUpdate() {
    base.OnUpdate();
    if (linkState == LinkState.Linking && guiLinkMode == GUILinkMode.Interactive) {
      UpdateLinkingState();

      // Handle link mode cancel.
      if (Input.GetKeyUp(KeyCode.Escape)) {
        AsyncCall.CallOnEndOfFrame(this, () => CancelLinking());
      }
      // Handle link action (mouse click).
      if (targetCandidateIsGood && Input.GetKeyDown(KeyCode.Mouse0)) {
        AsyncCall.CallOnEndOfFrame(this, () => LinkToTarget(targetCandidate));
      }
    }
  }

  /// <inheritdoc/>
  public override void OnStart(PartModule.StartState state) {
    base.OnStart(state);
    // Infinity duration doesn't mean the message will be shown forever. It must be refreshed in the
    // Update method.
    statusScreenMessage = new ScreenMessage("", Mathf.Infinity, ScreenMessageStyle.UPPER_CENTER);
    UpdateContextMenu();
  }
  #endregion

  #region IsLocalizableModule implementation
  /// <inheritdoc/>
  public void LocalizeModule() {
    LocalizationLoader.LoadItemsInModule(this);
  }
  #endregion

  #region IHasContextMenu implementation
  /// <inheritdoc/>
  public void UpdateContextMenu() {
    PartModuleUtils.SetupEvent(this, StartLinkContextMenuAction, e => {
                                 e.guiName = startLinkMenu;
                                 e.active = linkState == LinkState.Available;
                               });
    PartModuleUtils.SetupEvent(this, BreakLinkContextMenuAction, e => {
                                 e.guiName = breakLinkMenu;
                                 e.active = linkState == LinkState.Linked;
                               });
    PartModuleUtils.SetupEvent(
        this, DockVesselsContextMenuAction,
        e => e.active = allowChanginDockingMode && !linkJoint.coupleOnLinkMode);
    PartModuleUtils.SetupEvent(
        this, UndockVesselsContextMenuAction,
        e => e.active = allowChanginDockingMode && linkJoint.coupleOnLinkMode);
  }
  #endregion

  #region KASModuleLinkSourceBase overrides
  /// <inheritdoc/>
  public override bool StartLinking(GUILinkMode mode, LinkActorType actor) {
    // Don't allow EVA linking mode.
    if (mode != GUILinkMode.Interactive && mode != GUILinkMode.API) {
      return false;
    }
    return base.StartLinking(mode, actor);
  }

  /// <inheritdoc/>
  protected override void StartLinkGUIMode(GUILinkMode mode, LinkActorType actor) {
    base.StartLinkGUIMode(mode, actor);
    InputLockManager.SetControlLock(
        ControlTypes.All & ~ControlTypes.CAMERACONTROLS, TotalControlLock);
    canAutoSaveState = HighLogic.CurrentGame.Parameters.Flight.CanAutoSave;
    HighLogic.CurrentGame.Parameters.Flight.CanAutoSave = false;
    linkRenderer.shaderNameOverride = InteractiveShaderName;
    linkRenderer.colorOverride = BadLinkColor;
    linkRenderer.isPhysicalCollider = false;
  }

  /// <inheritdoc/>
  protected override void StopLinkGUIMode() {
    linkRenderer.StopRenderer();
    linkRenderer.shaderNameOverride = null;
    linkRenderer.colorOverride = null;
    linkRenderer.isPhysicalCollider = true;
    ScreenMessages.RemoveMessage(statusScreenMessage);
    InputLockManager.RemoveControlLock(TotalControlLock);
    HighLogic.CurrentGame.Parameters.Flight.CanAutoSave = canAutoSaveState;
    lastHoveredPart = null;
    base.StopLinkGUIMode();

    // Start renderer if link has been established.
    if (linkState == LinkState.Linked) {
      linkRenderer.StartRenderer(nodeTransform, linkTarget.nodeTransform);
    }
  }

  /// <inheritdoc/>
  protected override void OnStateChange(LinkState? oldState) {
    base.OnStateChange(oldState);
    UpdateContextMenu();
  }

  /// <inheritdoc/>
  public override void OnKASLinkCreatedEvent(KASEvents.LinkEvent info) {
    base.OnKASLinkCreatedEvent(info);
    if (info.actor == LinkActorType.Player || info.actor == LinkActorType.Physics) {
      UISoundPlayer.instance.Play(linkJoint.coupleOnLinkMode ? sndPathDock : sndPathPlug);
    }
  }

  /// <inheritdoc/>
  public override void OnKASLinkBrokenEvent(KASEvents.LinkEvent info) {
    base.OnKASLinkBrokenEvent(info);
    if (info.actor == LinkActorType.Player) {
      UISoundPlayer.instance.Play(linkJoint.coupleOnLinkMode ? sndPathUndock : sndPathUnplug);
    } else if (info.actor == LinkActorType.Physics) {
      UISoundPlayer.instance.Play(sndPathBroken);
    }
  }
  #endregion

  #region Local utility methods
  /// <summary>Displays linking status in real time.</summary>
  void UpdateLinkingState() {
    // Catch the hovered part, a possible target on it, and the link feasibility.
    if (Mouse.HoveredPart != lastHoveredPart) {
      lastHoveredPart = Mouse.HoveredPart;
      targetCandidateIsGood = false;
      if (lastHoveredPart == null ) {
        targetCandidate = null;
      } else {
        targetCandidate = lastHoveredPart.FindModulesImplementing<ILinkTarget>()
            .FirstOrDefault(x => x.cfgLinkType == cfgLinkType
                            && x.linkState == LinkState.AcceptingLinks);
        if (targetCandidate != null) {
          var linkStatusErrors = new[] {
              CheckBasicLinkConditions(targetCandidate),
              linkRenderer.CheckColliderHits(nodeTransform, targetCandidate.nodeTransform)
          };
          linkStatusErrors = linkStatusErrors
              .Concat(linkJoint.CheckConstraints(this, targetCandidate.nodeTransform))
              .Where(x => x != null).ToArray();
          if (linkStatusErrors.Length == 0) {
            targetCandidateIsGood = true;
            statusScreenMessage.message = CanBeConnectedMsg.Format(
                Vector3.Distance(nodeTransform.position, targetCandidate.nodeTransform.position));
          } else {
            statusScreenMessage.message = ScreenMessaging.SetColorToRichText(
                String.Join("\n", linkStatusErrors), ScreenMessaging.ErrorColor);
          }
        }
      }
      // Show the possible link or indicate the error.
      if (targetCandidate != null) {
        linkRenderer.colorOverride = targetCandidateIsGood ? GoodLinkColor : BadLinkColor;
        linkRenderer.StartRenderer(nodeTransform, targetCandidate.nodeTransform);
      } else {
        linkRenderer.colorOverride = BadLinkColor;
        linkRenderer.StopRenderer();
      }
    }

    // Update linking messages (it needs to be refreshed to not go out by timeout).
    if (targetCandidate == null) {
      statusScreenMessage.message = LinkingInProgressMsg;
    }
    ScreenMessages.PostScreenMessage(statusScreenMessage);
  }
  #endregion
}

}  // namespace
