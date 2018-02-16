// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.GUIUtils;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace KAS {

/// <summary>Module for the kerbal vessel that allows carrying the cable heads.</summary>
// Next localization ID: #kasLOC_10003.
public sealed class KASModuleKerbalLinkTarget : KASModuleLinkTargetBase,
    // KAS interfaces.
    IHasContextMenu,
    // KSPDev sugar interafces.
    IHasGUI {
  #region Localizable GUI strings.
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> DropConnectorHintMsg= new Message<KeyboardEventType>(
      "#kasLOC_100001",
      defaultTemplate: "To drop the connector press: [<<1>>]",
      description: "A hint string, instructing what to press in order to drop the currently carried"
      + "cable connector.\nArgument <<1>> is the current key binding of type KeyboardEventType.",
      example: "To drop the connector press: [Ctrl+Y]");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<KeyboardEventType> PickupConnectorHintMsg= new Message<KeyboardEventType>(
      "#kasLOC_100002",
      defaultTemplate: "[<<1>>]: Pickup connector",
      description: "A hint string, instructing what to press in order to pickup a cable connector"
      + "which is currently in range.\nArgument <<1>> is the current key binding of type"
      + "KeyboardEventType.",
      example: "[Y]: Pickup connector");
  #endregion

  #region Part's config fields
  /// <summary>Keyboard key to trigger the drop connector event.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string dropConnectorKey = "Y";

  /// <summary>Keyboard key to trigger the pickup connector event.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string pickupConnectorKey = "Y";

  /// <summary>Color to use to highlight the closest connector that can be picked up.</summary>
  /// <remarks>
  /// If set to <i>black</i> <c>(0, 0, 0)</c>, then the closests connector will not be highlighted.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public Color closestConnectorHighlightColor = Color.cyan;

  /// <summary>
  /// Name of the skinned mesh in the kerbal modle to bind the attach the node to.
  /// </summary>
  /// <remarks>If empty string, then the attach node will not follow the bones.</remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string equipMeshName = "";

  /// <summary>Name of the bone within the skinned mesh to bind the attach the node to.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string equipBoneName = "";
  #endregion

  #region Local fields and properties
  /// <summary>The closest connector that is compatible with the kerbal's target.</summary>
  /// <value>COnnector module or <c>null</c>.</value>
  InternalKASModulePhysicalConnector closestConnector {
    get {
      return connectorsInRange
          .Where(x => x != null && x.ownerModule as ILinkSource != null && x.connectorRb != null)
          .Select(x => new {
              connector = x,
              source = x.ownerModule as ILinkSource,
              distance = Vector3.Distance(gameObject.transform.position,
                                          x.connectorRb.transform.position)
          })
          .Where(x => x.source.linkState == LinkState.Available && x.source.cfgLinkType == linkType)
          .OrderBy(x => x.distance)
          .Select(x => x.connector)
          .FirstOrDefault();
    }
  }

  /// <summary>List of all the connectors that triggered the interaction collision event.</summary>
  /// <remarks>
  /// This collection must be a list since the items in it can become <c>null</c> in case of Unity
  /// has destroyed the owner object. So no sets!
  /// </remarks>
  readonly List<InternalKASModulePhysicalConnector> connectorsInRange =
      new List<InternalKASModulePhysicalConnector>();

  /// <summary>Keyboard event to react to drop the carried connector.</summary>
  /// <remarks>It's set from the part's config.</remarks>
  /// <seealso cref="dropConnectorKey"/>
  static Event dropConnectorKeyEvent;

  /// <summary>Keyboard event to react to pucik up a dropped connector.</summary>
  /// <remarks>It's set from the part's config.</remarks>
  /// <seealso cref="pickupConnectorKey"/>
  static Event pickupConnectorKeyEvent;

  /// <summary>Message to show when there is a dropped connector in the pickup range.</summary>
  /// <remarks>
  /// The message appears in the middle of the screen, and stays as long as there are connectors in
  /// range.
  /// </remarks>
  ScreenMessage dropConnectorMessage;

  /// <summary>Message to show when there is a dropped connector in the pickup range.</summary>
  /// <remarks>
  /// The message appears in the middle of the screen, and stays as long as there are connectors in
  /// range.
  /// </remarks>
  ScreenMessage pickupConnectorMessage;

  /// <summary>Connector that is currently highlighted as the pickup candidate.</summary>
  InternalKASModulePhysicalConnector focusedPickupConnector;

  /// <summary>Transform object of the bone which the atatch node needs to follow.</summary>
  /// <remarks>
  /// Kerbal's model is tricky, and many objects live at the unusual layers. To not get affected by
  /// this logic, the attach node is not connected to the bone as a child. Instead, a runtime code
  /// adjusts the position on every frame to follow the bone.
  /// </remarks>
  Transform attachBoneTransform;

  /// <summary>Local position of the attach node relative to the kerbal's model bone.</summary>
  Vector3 boneAttachNodePosition;

  /// <summary>Local orientation of the attach node relative to the kerbal's model bone.</summary>
  Quaternion boneAttachNodeRotation;
  #endregion

  #region Context menu events/actions
  /// <summary>A context menu item that picks up the cable connector in range.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true)]
  [LocalizableItem(
      tag = "#kasLOC_10000",
      defaultTemplate = "Pickup connector",
      description = "A context menu item that picks up the cable connector in range.")]
  public void PickupConnectorEvent() {
    var connector = closestConnector;
    if (connector != null) {
      var closestSource = connector.ownerModule as ILinkSource;
      HostedDebugLog.Info(this, "Try picking up a physical connector of: {0}...", closestSource);
      if (closestSource.CheckCanLinkTo(this, reportToGUI: true)) {
        if (closestSource.LinkToTarget(LinkActorType.Player, this)) {
          var winch = closestSource as IWinchControl;
          if (winch != null) {
            winch.ReleaseCable();
          }
        }
      } else {
        UISoundPlayer.instance.Play(CommonConfig.sndPathBipWrong);
      }
    }
  }
  #endregion

  #region KASModuleLinkTargetBase overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();

    linkStateMachine.onAfterTransition += (start, end) => UpdateContextMenu();
    dropConnectorKeyEvent = Event.KeyboardEvent(dropConnectorKey);
    pickupConnectorKeyEvent = Event.KeyboardEvent(pickupConnectorKey);
    useGUILayout = false;
    dropConnectorMessage = new ScreenMessage(
        "", ScreenMessaging.DefaultMessageTimeout, ScreenMessageStyle.UPPER_CENTER);
    pickupConnectorMessage = new ScreenMessage(
        "", ScreenMessaging.DefaultMessageTimeout, ScreenMessageStyle.LOWER_CENTER);
    UpdateContextMenu();
  }

  /// <inheritdoc/>
  public override void OnStart(PartModule.StartState state) {
    // The EVA parts don't get the load method called. So, to complete the initalization, pretend
    // the method was called with no config provided. This is needed to make the parent working.
    Load(new ConfigNode());

    base.OnStart(state);

    if (equipMeshName != "") {
      attachBoneTransform = part.GetComponentsInChildren<SkinnedMeshRenderer>()
          .Where(m => m.name == equipMeshName)
          .Select(m => m.bones.FirstOrDefault(b => b.name == equipBoneName))
          .Select(b => b.transform)
          .FirstOrDefault();
      if (attachBoneTransform != null) {
        boneAttachNodePosition = attachBoneTransform.InverseTransformPoint(nodeTransform.position);
        boneAttachNodeRotation =
            Quaternion.Inverse(attachBoneTransform.rotation) * nodeTransform.rotation;
      } else {
        HostedDebugLog.Error(this, "Cannot find bone for: mesh name={0}, bone name={1}",
                             equipMeshName, equipBoneName);
      }
    }
  }

  /// <inheritdoc/>
  public override void OnUpdate() {
    base.OnUpdate();
    if (attachBoneTransform != null && isLinked) {
      nodeTransform.rotation = attachBoneTransform.rotation * boneAttachNodeRotation;
      nodeTransform.position = attachBoneTransform.TransformPoint(boneAttachNodePosition);
    }
  }
  #endregion

  #region IHasGUI implementation
  /// <inheritdoc/>
  public void OnGUI() {
    var thisVesselIsActive = FlightGlobals.ActiveVessel == vessel;
    var pickupConnector = thisVesselIsActive && !isLinked ? closestConnector : null;

    if (pickupConnector != focusedPickupConnector) {
      if (focusedPickupConnector != null) {
        focusedPickupConnector.SetHighlighting(null);
      }
      focusedPickupConnector = pickupConnector;
      if (focusedPickupConnector != null && closestConnectorHighlightColor != Color.black) {
        focusedPickupConnector.SetHighlighting(closestConnectorHighlightColor);
      }
    }

    // Remove hints if any.
    if (!isLinked || !thisVesselIsActive) {
      ScreenMessages.RemoveMessage(dropConnectorMessage);
      UpdateContextMenu();
    }
    if (pickupConnector == null) {
      ScreenMessages.RemoveMessage(pickupConnectorMessage);
      UpdateContextMenu();
    }
    if (!thisVesselIsActive) {
      return;  // No GUI for the inactive vessel.
    }

    // Handle the cable head drop/pickup events. 
    if (Event.current.Equals(dropConnectorKeyEvent) && isLinked) {
      Event.current.Use();
      linkSource.BreakCurrentLink(
          LinkActorType.Player,
          moveFocusOnTarget: linkSource.linkTarget.part.vessel == FlightGlobals.ActiveVessel);
    }
    if (Event.current.Equals(pickupConnectorKeyEvent) && pickupConnector != null) {
      Event.current.Use();
      PickupConnectorEvent();
    }

    // Show the head drop hint message.
    if (isLinked) {
      dropConnectorMessage.message = DropConnectorHintMsg.Format(dropConnectorKeyEvent);
      ScreenMessages.PostScreenMessage(dropConnectorMessage);
    }
    if (pickupConnector != null) {
      pickupConnectorMessage.message = PickupConnectorHintMsg.Format(pickupConnectorKeyEvent);
      ScreenMessages.PostScreenMessage(pickupConnectorMessage);
    }

    UpdateContextMenu();
  }
  #endregion

  #region IHasContextMenu implementation
  /// <inheritdoc/>
  public void UpdateContextMenu() {
    PartModuleUtils.SetupEvent(
        this, PickupConnectorEvent,
        x => x.guiActive = FlightGlobals.fetch != null && FlightGlobals.ActiveVessel == vessel
            && !isLinked && closestConnector != null);
  }
  #endregion

  #region Local utility methods
  /// <summary>Collects a connector in the pickup range.</summary>
  /// <remarks>It's a <c>MonoBehavior</c> callback.</remarks>
  /// <param name="other">The collider that triggered the pickup collider check.</param>
  void OnTriggerEnter(Collider other) {
    if (other.name == InternalKASModulePhysicalConnector.InteractionAreaCollider) {
      var connector = other.gameObject.GetComponentInParent<InternalKASModulePhysicalConnector>();
      if (connector != null) {
        connectorsInRange.Add(connector);
      }
    }
  }

  /// <summary>Removes the connector that leaves the pickup range.</summary>
  /// <remarks>It's a <c>MonoBehavior</c> callback.</remarks>
  /// <param name="other">The collider that triggered the pickup collider check.</param>
  void OnTriggerExit(Collider other) {
    if (other.name == InternalKASModulePhysicalConnector.InteractionAreaCollider) {
      var connector = other.gameObject.GetComponentInParent<InternalKASModulePhysicalConnector>();
      if (connector != null) {
        connectorsInRange.Remove(connector);
      }
    }
  }
  #endregion
}

}  // namespace
