// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.GUIUtils;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
using System;
using UnityEngine;

namespace KAS {

/// <summary>Module for the kerbal vessel that allows carrying the cable heads.</summary>
// Next localization ID: #kasLOC_10003.
// FIXME: adjust nodeTransform to follow the bones.
public sealed class KASModuleKerbalLinkTarget : KASModuleLinkTargetBase,
    // KAS interfaces.
    IHasContextMenu,
    // KSPDev sugar interafces.
    IHasGUI {
  #region Localizable GUI strings.
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> DropHeadHintMsg= new Message<KeyboardEventType>(
      "#kasLOC_100002",
      defaultTemplate: "To drop the head press: [<<1>>]",
      description: "A hint string, instructing what to press in order to drop the currently carried"
      + "cable head.\nArgument <<1>> is the current key binding of type KeyboardEventType.",
      example: "To drop the head press: [Ctrl+Y]");
  #endregion

  //FIXME: move into the global config.
  static Event dropHeadKeyEvent = Event.KeyboardEvent("y");
  const bool hasCableHeadInRange = false;

  /// <summary>Status screen message to be displayed during carrying the head.</summary>
  ScreenMessage carryingStatusScreenMessage;

  #region Context menu events/actions
  /// <summary>A context menu item that drops the cable head attached to the kerbal.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true)]
  [LocalizableItem(
      tag = "#kasLOC_10000",
      defaultTemplate = "Drop head",
      description = "A context menu item that drops the cable head attached to the kerbal.")]
  public void DropHeadEvent() {
    if (isLinked) {
      linkSource.BreakCurrentLink(
          LinkActorType.Player,
          moveFocusOnTarget: linkSource.linkTarget.part.vessel == FlightGlobals.ActiveVessel);
      HostedDebugLog.Warning(this, "Link head dropped from the EVA kerbal");
    }
  }

  /// <summary>A context menu item that picks up the cable head in range.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true)]
  [LocalizableItem(
      tag = "#kasLOC_10001",
      defaultTemplate = "Pickup the head",
      description = "A context menu item that picks up the cable head in range.")]
  public void PickupHeadEvent() {
    //FIXME: implement
    HostedDebugLog.Error(this, "Not implemented");
  }
  #endregion

  #region IHasGUI implementation
  /// <inheritdoc/>
  public void OnGUI() {
    var thisVesselIsActive = FlightGlobals.ActiveVessel == vessel;
    // Remove hints if any.
    if (carryingStatusScreenMessage != null && (!isLinked || !thisVesselIsActive)) {
      ScreenMessages.RemoveMessage(carryingStatusScreenMessage);
      carryingStatusScreenMessage = null;
    }
    if (!thisVesselIsActive) {
      return;  // No GUI for the inactive vessel.
    }

    // Handle the cable head drop event. 
    if (Event.current.Equals(dropHeadKeyEvent)) {
      Event.current.Use();
      if (isLinked) {
        DropHeadEvent();
      } else if (hasCableHeadInRange) {
        PickupHeadEvent();
      }
    }

    // Show the head drop hint message.
    if (isLinked) {
      if (carryingStatusScreenMessage == null) {
        carryingStatusScreenMessage = new ScreenMessage(
            DropHeadHintMsg.Format(dropHeadKeyEvent),
            ScreenMessaging.DefaultMessageTimeout, ScreenMessageStyle.UPPER_CENTER);
      }
      ScreenMessages.PostScreenMessage(carryingStatusScreenMessage);
    }
  }
  #endregion
  
  #region IHasContextMenu implementation
  /// <inheritdoc/>
  public void UpdateContextMenu() {
    PartModuleUtils.SetupEvent(this, PickupHeadEvent, x => x.guiActive = hasCableHeadInRange);
    PartModuleUtils.SetupEvent(this, DropHeadEvent, x => x.guiActive = isLinked);
  }
  #endregion

  #region ParModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    useGUILayout = false;
    carryingStatusScreenMessage = new ScreenMessage(
        "", ScreenMessaging.DefaultMessageTimeout, ScreenMessageStyle.UPPER_CENTER);
    UpdateContextMenu();
  }
  #endregion

  #region KASModuleLinkTargetBase overrides
  /// <inheritdoc/>
  protected override void OnStateChange(LinkState? oldState) {
    base.OnStateChange(oldState);
    UpdateContextMenu();
  }
  #endregion
}

}  // namespace
