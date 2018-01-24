// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.GUIUtils;
using KSPDev.KSPInterfaces;
using KSPDev.LogUtils;
using KSPDev.ProcessingUtils;
using UnityEngine;

namespace KAS {

/// <summary>
/// Module that manages the part attach node for the coupling actions on the target.
/// </summary>
/// <remarks>
/// This module must exist at the target part in order to allow the linked source to switch into
/// "coupling" mode. In this mode the source and the target belong to the same vessel. 
/// </remarks>
/// <seealso cref="ILinkJoint.SetCoupleOnLinkMode"/>
public abstract class AbstractLinkPeer : PartModule,
    // KSP interfaces.
    IActivateOnDecouple,
    // KAS interfaces.
    ILinkPeer,
    // KSPDev parents.
    IsLocalizableModule,
    // KSPDev syntax sugar interfaces.
    IPartModule, IKSPActivateOnDecouple, IsDestroyable {

  #region Part's config fields
  /// <summary>See <see cref="cfgLinkType"/>.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string linkType = "";

  /// <summary>
  /// Definition of the attach node to automatically create when the coupling is needed.
  /// </summary>
  /// <remarks>
  /// The format of the string is exactly the same as for the part's attach nodes in the config.
  /// This node will not be available in the editor or in the flight for the third-party mods
  /// (like KIS).
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <seealso cref="attachNode"/>
  [KSPField]
  public string attachNodeDef = "";

  /// <summary>Name of the attach node for the link and coupling operations.</summary>
  /// <remarks>
  /// If an attach node with this name is existing on the part, then it will be used. Otherwise, it
  /// will be assumed that the node needs to be created/removed automatically as needed.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <seealso cref="attachNodeDef"/>
  [KSPField]
  public string attachNodeName = "";

  /// <summary>Specifies if this peer can couple into the vessel's hirerachy.</summary>
  /// <seealso cref="attachNode"/>
  [KSPField]
  public bool allowCoupling;
  #endregion

  #region Persistent fields
  /// <summary>The link state in the last save action.</summary>
  /// <remarks>Normally, the base class handles it.</remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  /// <seealso cref="linkState"/>
  [KSPField(isPersistant = true)]
  public LinkState persistedLinkState = LinkState.Available;

  /// <summary>The other peer's part flight ID in the last save action.</summary>
  /// <remarks>Normally, the base class handles it.</remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  /// <seealso cref="otherPeer"/>
  [KSPField(isPersistant = true)]
  public uint persistedLinkPartId;
  #endregion

  #region ILinkPeer properties implementation
  /// <inheritdoc/>
  public string cfgLinkType { get { return linkType; } }

  /// <inheritdoc/>
  /// <seealso cref="persistedLinkState"/>
  public LinkState linkState {
    get {
      return linkStateMachine.currentState ?? persistedLinkState;
    }
    protected set {
      linkStateMachine.currentState = value;
    }
  }

  /// <inheritdoc/>
  /// <remarks>
  /// The descendants must eitehr use this property when linking with the other peer, or fully mimic
  /// the behavior.
  /// </remarks>
  /// <seealso cref="persistedLinkPartId"/>
  /// <seealso cref="OnPeerChange"/>
  public virtual ILinkPeer otherPeer {
    get { return _otherPeer; }
    protected set {
      var oldPeer = _otherPeer;
      _otherPeer = value;
      persistedLinkPartId = _otherPeer != null ? _otherPeer.part.flightID : 0;
      OnPeerChange(oldPeer);
    }
  }
  ILinkPeer _otherPeer;

  /// <inheritdoc/>
  public uint linkPartId {
    get { return persistedLinkPartId; }
  }

  /// <inheritdoc/>
  public Transform nodeTransform { get; private set; }

  /// <inheritdoc/>
  public AttachNode attachNode {
    get {
      return allowCoupling ? parsedAttachNode : null;
    }
  }

  /// <inheritdoc/>
  public bool isLinked {
    get { return linkState == LinkState.Linked; }
  }

  /// <inheritdoc/>
  public bool isLocked {
    get { return linkState == LinkState.Locked; }
    protected set {
      // Don't trigger state change events when the value hasn't changed.
      if (value != (linkState == LinkState.Locked)) {
        linkState = value ? LinkState.Locked : LinkState.Available;
      }
    }
  }
  #endregion

  #region Inheritable fields & properties
  /// <summary>
  /// State machine that controls the source state tranistions and defines the reaction on these
  /// changes.
  /// </summary>
  /// <remarks>
  /// When the state is restored form a config file, it can be set to any arbitrary value. To
  /// properly handle it, the state transition handlers behavior must be consistent:  
  /// <list type="bullet">
  /// <item>
  /// Define the "default" state which is set on the module in the <c>OnAwake</c> method.
  /// </item>
  /// <item>
  /// In the <c>enterState</c> handlers assume the current state is the "default", and do <i>all</i>
  /// the adjustment to set the new state. Do <i>not</i> assume the transition has happen from
  /// another known state.
  /// </item>
  /// <item>
  /// In the <c>leaveState</c> handlers reset all the settings to bring the module back to the
  /// "default" state. Don't leave anything behind!
  /// </item>
  /// </list>
  /// </remarks>
  /// <value>The state machine instance.</value>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@anme='T:KSPDev.ProcessingUtils.SimpleStateMachine_1']/*"/>
  protected SimpleStateMachine<LinkState> linkStateMachine { get; private set; }

  /// <summary>Attach node loaded from the part's config.</summary>
  /// <value>The attach node. It's never <c>null</c>.</value>
  /// <seealso cref="attachNodeName"/>
  /// <seealso cref="attachNodeDef"/>
  protected AttachNode parsedAttachNode { get; private set; }

  /// <summary>Tells if the attach node in this module is dynamically created when needed.</summary>
  /// <value><c>true</c> if the node only exists for the coupling.</value>
  protected bool isAutoAttachNode { get; private set; }
  #endregion

  #region IActivateOnDecouple implementation
  /// <inheritdoc/>
  public virtual void DecoupleAction(string nodeName, bool weDecouple) {
    if (nodeName == attachNodeName) {
      AsyncCall.CallOnEndOfFrame(this, CheckAttachNode);
      if (isAutoAttachNode && attachNode != null) {
        HostedDebugLog.Fine(
            this, "Removing auto node: {0}", KASAPI.AttachNodesUtils.NodeId(attachNode));
        part.attachNodes.Remove(attachNode);
        attachNode.attachedPart = null;
        attachNode.attachedPartId = 0;
      }
    }
  }
  #endregion

  #region IsLocalizableModule implementation
  /// <inheritdoc/>
  public virtual void LocalizeModule() {
    LocalizationLoader.LoadItemsInModule(this);
  }
  #endregion

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    LocalizeModule();
    linkStateMachine = new SimpleStateMachine<LinkState>(true /* strict */);
    SetupStateMachine();
    GameEvents.onPartCouple.Add(OnPartCoupleEvent);
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    base.OnLoad(node);

    parsedAttachNode = part.FindAttachNode(attachNodeName);
    isAutoAttachNode = parsedAttachNode == null;
    if (isAutoAttachNode) {
      parsedAttachNode = KASAPI.AttachNodesUtils.ParseNodeFromString(
          part, attachNodeDef, attachNodeName);
      if (parsedAttachNode != null) {
        HostedDebugLog.Fine(
            this, "Created auto node: {0}", KASAPI.AttachNodesUtils.NodeId(parsedAttachNode));
        if (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor) {
          // Only pre-add the node in the scenes that assume restoring a vessel state.
          part.attachNodes.Add(parsedAttachNode);
        }
      } else {
        HostedDebugLog.Error(this, "Cannot create auto node from: {0}", attachNodeDef);
      }
    }
    if (parsedAttachNode != null) {
      // HACK: Handle a KIS issue which causes the nodes to be owned by the prefab part.
      parsedAttachNode.owner = part;
      nodeTransform = KASAPI.AttachNodesUtils.GetTransformForNode(part, parsedAttachNode);
    }
  }

  /// <inheritdoc/>
  public override void OnStartFinished(PartModule.StartState state) {
    base.OnStartFinished(state);
    // Prevent the third-party logic on the auto node.
    if ((HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor)
        && isAutoAttachNode && attachNode != null && attachNode.attachedPart == null) {
      part.attachNodes.Remove(attachNode);
      HostedDebugLog.Fine(this, "Cleaning up the unused auto node: {0}",
                          KASAPI.AttachNodesUtils.NodeId(attachNode));
    }
    if (persistedLinkState == LinkState.Linked) {
      RestoreOtherPeer();
      if (otherPeer == null) {
        HostedDebugLog.Error(
            this, "Cannot restore the link's peer: tgtPartID={0}", persistedLinkPartId);
        persistedLinkState = LinkState.Available;
      } else {
        HostedDebugLog.Fine(this, "Restored link to: {0}", otherPeer);
      }
    }
    linkState = persistedLinkState;
  }
  #endregion

  #region IsDestroyable implementation
  /// <inheritdoc/>
  public virtual void OnDestroy() {
    ShutdownStateMachine();
    GameEvents.onPartCouple.Remove(OnPartCoupleEvent);
  }
  #endregion

  #region IsPackable implementation
  /// <inheritdoc/>
  public virtual void OnPartUnpack() {
    AsyncCall.CallOnEndOfFrame(this, CheckAttachNode);
  }

  /// <inheritdoc/>
  public virtual void OnPartPack() {
  }
  #endregion

  #region Inheritable methods
  /// <summary>Fires when the attach node needs to be checked for a possible state change.</summary>
  /// <remarks>
  /// This method is called asynchronously at the end of frame. The triggering of this call doesn't
  /// mean the attach node state has changed. It only means that it could have changed. The code is
  /// responsible to verify it and act accordignly. Examples of the changed state are: a part has
  /// been attached to the node by the external code, or the part has been detached from the node.
  /// </remarks>
  protected abstract void CheckAttachNode();

  /// <summary>Sets the peer's state machine.</summary>
  protected virtual void SetupStateMachine() {
    linkStateMachine.onAfterTransition += (start, end) => persistedLinkState = linkState;
  }

  /// <summary>Cleanups the peer's state machine.</summary>
  /// <remarks>Can be also used to cleanup the module state.</remarks>
  protected virtual void ShutdownStateMachine() {
    linkStateMachine.currentState = null;  // Stop the machine to let the cleanup handlers working.
  }

  /// <summary>Triggers when a linked peers has been assigned with a value.</summary>
  /// <remarks>
  /// This method triggers even when the new peer doesn't differ from the old one. When it's
  /// important to catch the transition, check for the <paramref name="oldPeer"/>.
  /// </remarks>
  /// <param name="oldPeer">The peer prior to the change.</param>
  protected virtual void OnPeerChange(ILinkPeer oldPeer) {
  }

  /// <summary>Finds the other peer of the link.</summary>
  /// <remarks>
  /// The decendants may override this method to react on the link loading. If the link must not be
  /// restorted, just reset <see cref="otherPeer"/> to <c>null</c>.
  /// </remarks>
  /// <seealso cref="otherPeer"/>
  protected virtual void RestoreOtherPeer() {
    otherPeer = KASAPI.LinkUtils.FindLinkPeer(this);
  }

  /// <summary>Shows a UI messages with regard to the currently active vessel.</summary>
  /// <remarks>
  /// The UI messages from the active vessel are show n at the highest priority to bring attention
  /// of the player. The messages from the inactive vessels are show only as a status, that is not
  /// intended to distract the player from the current vessel operations.
  /// </remarks>
  /// <param name="msg">The message to show.</param>
  /// <param name="isError">
  /// Tells if the messages is an error condition report. Such messages will be highlighed with the
  /// color.
  /// </param>
  protected void ShowStatusMessage(string msg, bool isError = false) {
    if (FlightGlobals.ActiveVessel != vessel) {
      msg = string.Format("[{0}]: {1}", vessel.vesselName, msg);
    }
    if (isError) {
      msg = ScreenMessaging.SetColorToRichText(msg, ScreenMessaging.ErrorColor);
    }
    var duration = isError
        ? ScreenMessaging.DefaultErrorTimeout
        : ScreenMessaging.DefaultMessageTimeout;
    var location = FlightGlobals.ActiveVessel == vessel
        ? ScreenMessageStyle.UPPER_CENTER
        : (isError ? ScreenMessageStyle.UPPER_RIGHT : ScreenMessageStyle.UPPER_LEFT);
    ScreenMessages.PostScreenMessage(msg, duration, location);
  }
  #endregion

  #region Local utility methods
  void OnPartCoupleEvent(GameEvents.FromToAction<Part, Part> action) {
    AttachNode node = null;
    if (action.from == part) {
      node = action.from.FindPartThroughNodes(action.to);
    } else if (action.to == part) {
      node = action.to.FindPartThroughNodes(action.from);
    }
    if (node != null && node.id == attachNodeName) {
      AsyncCall.CallOnEndOfFrame(this, CheckAttachNode);
    }
  }
  #endregion
}

}  // namespace
