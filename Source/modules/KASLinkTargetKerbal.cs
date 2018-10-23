// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.GUIUtils;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace KAS {

/// <summary>Module for the kerbal vessel that allows carrying the cable heads.</summary>
// Next localization ID: #kasLOC_10004.
public sealed class KASLinkTargetKerbal : KASLinkTargetBase,
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

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message attachConnectorMenu = new Message(
      "#kasLOC_10003",
      defaultTemplate: "Attach connector",
      description: "Context menu item that appear on the target part and transfers the EVA carried"
      + " connector to it.");
  #endregion

  #region Part's config fields
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
  /// <summary>The closest connector.</summary>
  /// <remarks>
  /// If the closes connector is incompatible with the target, then <c>null</c> is returned.
  /// </remarks>
  /// <value>Connector module or <c>null</c>.</value>
  KASInternalPhysicalConnector closestConnector {
    get {
      return connectorsInRange
          .Where(x => x != null && x.ownerModule as ILinkSource != null && x.connectorRb != null)
          .Where(x => (x.ownerModule as ILinkSource).linkState == LinkState.Available)
          .OrderBy(x => Vector3.Distance(gameObject.transform.position,
                                         x.connectorRb.transform.position))
          .Take(1)
          .FirstOrDefault(x => (x.ownerModule as ILinkSource).cfgLinkType == linkType);
    }
  }

  /// <summary>List of all the connectors that triggered the interaction collision event.</summary>
  /// <remarks>
  /// This collection must be a list since the items in it can become <c>null</c> in case of Unity
  /// has destroyed the owner object. So no sets!
  /// </remarks>
  readonly List<KASInternalPhysicalConnector> connectorsInRange =
      new List<KASInternalPhysicalConnector>();

  /// <summary>Keyboard event to react to drop the carried connector.</summary>
  /// <remarks>It's set from the part's config.</remarks>
  static Event dropConnectorKeyEvent;

  /// <summary>Keyboard event to react to pucik up a dropped connector.</summary>
  /// <remarks>It's set from the part's config.</remarks>
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
  KASInternalPhysicalConnector focusedPickupConnector;

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

  /// <summary>Helper container for the injected items.</summary>
  class InjectedEvent {
    /// <summary>Module that own the menu item.</summary>
    public ILinkPeer module;

    /// <summary>The injected event.</summary>
    /// <remarks>It's <c>null</c> if the event has not been injected.</remarks>
    public BaseEvent baseEvent;
  }

  /// <summary>Cache of the injected menu items.</summary>
  /// <remarks>
  /// It's updated in the GUI menu callbacks and gets wiped out when the vessel looses focus.
  /// </remarks>
  Dictionary<uint, List<InjectedEvent>> targetCandidates =
      new Dictionary<uint, List<InjectedEvent>>();
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
      if (closestSource.LinkToTarget(LinkActorType.Player, this)) {
        var cableJoint = closestSource.linkJoint as ILinkCableJoint;
        if (cableJoint != null) {
          // By default, the cable joints set the length limit to the actual distance. 
          cableJoint.SetCableLength(float.PositiveInfinity);
        }
        var updatableMenu = closestSource as IHasContextMenu;
        if (updatableMenu != null) {
          // Let the module know that we've changed the values.
          updatableMenu.UpdateContextMenu();
        }
      } else {
        UISoundPlayer.instance.Play(KASAPI.CommonConfig.sndPathBipWrong);
      }
    }
  }
  #endregion

  #region KASModuleLinkTargetBase overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();

    linkStateMachine.onAfterTransition += (start, end) => UpdateContextMenu();
    dropConnectorKeyEvent = Event.KeyboardEvent(KASAPI.CommonConfig.keyDropConnector);
    pickupConnectorKeyEvent = Event.KeyboardEvent(KASAPI.CommonConfig.keyPickupConnector);
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

  /// <inheritdoc/>
  protected override void SetupStateMachine() {
    base.SetupStateMachine();
    if (HighLogic.LoadedSceneIsFlight) {
      linkStateMachine.onBeforeTransition += (start, end) => {
        if (!start.HasValue) {
          GameEvents.onPartActionUICreate.Add(OnPartGUIStart);
          GameEvents.onPartActionUIDismiss.Add(OnPartGUIStop);
        }
        if (!end.HasValue) {
          GameEvents.onPartActionUICreate.Remove(OnPartGUIStart);
          GameEvents.onPartActionUIDismiss.Remove(OnPartGUIStop);
        }
      };
    }
  }
  #endregion

  #region IHasGUI implementation
  /// <inheritdoc/>
  public void OnGUI() {
    var thisVesselIsActive = FlightGlobals.ActiveVessel == vessel;
    var pickupConnector = thisVesselIsActive && linkState == LinkState.Available
        ? closestConnector : null;

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
      linkSource.BreakCurrentLink(LinkActorType.Player);
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
    if (other.name == KASInternalPhysicalConnector.InteractionAreaCollider) {
      var connector = other.gameObject.GetComponentInParent<KASInternalPhysicalConnector>();
      if (connector != null) {
        connectorsInRange.Add(connector);
      }
    }
  }

  /// <summary>Removes the connector that leaves the pickup range.</summary>
  /// <remarks>It's a <c>MonoBehavior</c> callback.</remarks>
  /// <param name="other">The collider that triggered the pickup collider check.</param>
  void OnTriggerExit(Collider other) {
    if (other.name == KASInternalPhysicalConnector.InteractionAreaCollider) {
      var connector = other.gameObject.GetComponentInParent<KASInternalPhysicalConnector>();
      if (connector != null) {
        connectorsInRange.Remove(connector);
      }
    }
  }

  /// <summary>Updates the GUI items when a part's context menu is opened.</summary>
  /// <remarks>
  /// <para>
  /// The goal of this method is to intercept the action of opening a context menu on the other
  /// part. The method checks if the target part can be a target for the link of the connector that
  /// is being carried by the kerbal. If this is the case, then a special menu item is injected to
  /// allow player to complete the link.
  /// </para>
  /// <para>
  /// This event is called once for every part with an opened menu in every frame. For this reason
  /// it must be very efficient, or else the performance will suffer. To not impact the performance,
  /// this method caches all the opened menus.
  /// </para>
  /// </remarks>
  /// <param name="menuOwnerPart">The part for which the UI is created.</param>
  /// <seealso cref="OnPartGUIStop"/>
  void OnPartGUIStart(Part menuOwnerPart) {
    if (FlightGlobals.ActiveVessel != vessel) {
      // If the EVA part has lost the focus, then cleanup all the caches.
      if (targetCandidates.Count > 0) {
        targetCandidates
            .SelectMany(t => t.Value)
            .Where(ie => ie.baseEvent != null)
            .ToList()
            .ForEach(ie => PartModuleUtils.DropEvent(ie.module as PartModule, ie.baseEvent));
        targetCandidates.Clear();
      }
      return;
    }

    // Check if the menu injects need to be added/removed on the monitored parts. 
    List<InjectedEvent> injects;
    if (!targetCandidates.TryGetValue(menuOwnerPart.flightID, out injects)) {
      injects = menuOwnerPart.Modules.OfType<ILinkTarget>()
          .Where(t => t.cfgLinkType == cfgLinkType)
          .Select(t => new InjectedEvent() { module = t, baseEvent = null })
          .ToList();
      targetCandidates.Add(menuOwnerPart.flightID, injects);
    }
    foreach (var inject in injects) {
      var target = inject.module;
      var canLink = inject.module.linkState == LinkState.Available && isLinked;
      if (!canLink && inject.baseEvent != null) {
        PartModuleUtils.DropEvent(target as PartModule, inject.baseEvent);
        inject.baseEvent = null;
      } else if (canLink && inject.baseEvent == null) {
        inject.baseEvent = MakeEvent(target, attachConnectorMenu, LinkCarriedConnector);
        PartModuleUtils.AddEvent(target as PartModule, inject.baseEvent);
      }
    }
  }

  /// <summary>Cleans up the internal cache of the injected menu items.</summary>
  /// <param name="menuOwnerPart">The part for which the UI is being destroyed.</param>
  /// <seealso cref="OnPartGUIStart"/>
  void OnPartGUIStop(Part menuOwnerPart) {
    List<InjectedEvent> injects;
    if (targetCandidates.TryGetValue(menuOwnerPart.flightID, out injects)) {
      injects
          .Where(ie => ie.baseEvent != null).ToList()
          .ForEach(ie => PartModuleUtils.DropEvent(ie.module as PartModule, ie.baseEvent));
      targetCandidates.Remove(menuOwnerPart.flightID);
    }
  }

  /// <summary>
  /// Removes the linked connector from the kerbal and links it to the target part.
  /// </summary>
  /// <param name="peer">The target part link module.</param>
  void LinkCarriedConnector(ILinkPeer peer) {
    var target = peer as ILinkTarget;
    if (target != null && isLinked
        && linkSource.CheckCanLinkTo(target, checkStates: false, reportToGUI: true)) {
      var source = linkSource;
      source.BreakCurrentLink(LinkActorType.API);
      if (!source.LinkToTarget(LinkActorType.Player, target)) {
        UISoundPlayer.instance.Play(KASAPI.CommonConfig.sndPathBipWrong);
      }
    } else {
      UISoundPlayer.instance.Play(KASAPI.CommonConfig.sndPathBipWrong);
    }
  }

  /// <summary>Helper method to create a part's context menu.</summary>
  /// <param name="peer">The peer module to create an action for.</param>
  /// <param name="guiName">The GUI name of the menu item.</param>
  /// <param name="action">The action to exectue when the menu item is triggered.</param>
  /// <returns>The new event. It's not automatically injected into the part.</returns>
  BaseEvent MakeEvent(ILinkPeer peer, Message guiName, Action<ILinkPeer> action) {
    var ev = new BaseEvent(
        peer.part.Events,
        "autoEventAttach" + peer.part.Modules.IndexOf(peer as PartModule),
        () => action.Invoke(peer),
        new KSPEvent());
    ev.guiActive = true;
    ev.guiActiveUncommand = true;
    ev.guiActiveUnfocused = true;
    ev.guiName = guiName;
    return ev;
  }
  #endregion
}

}  // namespace
