// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.ConfigUtils;
using KSPDev.GUIUtils;
using KSPDev.KSPInterfaces;
using KSPDev.LogUtils;
using KSPDev.ProcessingUtils;
using System.Linq;
using UnityEngine;

namespace KAS {

/// <summary>Base class that handles the basic functionality of the link's end.</summary>
/// <remarks>
/// <para>
/// This module doesn't define how the link is created, but it does the heavy lifting to keep it,
/// once it's established. The descendants are resposible for determining what peers can link with
/// each other.
/// </para>
/// <para>
/// The descendants of this module can use the custom persistent fields of groups:
/// </para>
/// <list type="bullet">
/// <item><c>StdPersistentGroups.PartConfigLoadGroup</c></item>
/// <item><c>StdPersistentGroups.PartPersistant</c></item>
/// </list>
/// </remarks>
/// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ConfigUtils.PersistentFieldAttribute']/*"/>
/// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ConfigUtils.StdPersistentGroups']/*"/>
public abstract class AbstractLinkPeer : PartModule,
    // KSP interfaces.
    IActivateOnDecouple,
    // KAS interfaces.
    ILinkPeer, ILinkStateEventListener,
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
  /// <seealso cref="coupleNode"/>
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

  /// <summary>
  /// Comma-separated list of names of the attach nodes which this module doesn't own but wants to
  /// aligns the state with.
  /// </summary>
  /// <remarks>
  /// The module will track the nodes and adjust its state as they were owned by the module. This
  /// can be used to lock/block the peer modules that control the different nodes, but need to
  /// cooperate with the other similar modules on the part. By setting the dependent nodes it's
  /// possible to define a group of peer modules which only allows linking of a single module at the
  /// time.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <seealso cref="attachNodeDef"/>
  [KSPField]
  public string dependentNodes = "";

  /// <summary>Specifies if this peer can couple into the vessel's hirerachy.</summary>
  /// <seealso cref="coupleNode"/>
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
  public string cfgAttachNodeName { get { return attachNodeName; } }

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
  public AttachNode coupleNode {
    get { return allowCoupling ? parsedAttachNode : null; }
  }

  /// <inheritdoc/>
  public AttachNode attachNode {
    get { return parsedAttachNode; }
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
      if (value != isLocked) {
        linkState = value ? LinkState.Locked : LinkState.Available;
      }
    }
  }

  /// <inheritdoc/>
  /// <remarks>
  /// The descendants <i>must</i> to change the blocked status via this property. Otherwise, the
  /// notifications won't fire. The event will also not fire if the new value doesn't differ from
  /// the old one.
  /// </remarks>
  public bool isNodeBlocked {
    get { return linkState == LinkState.NodeIsBlocked; }
    protected set {
      // Don't trigger the change event when the value hasn't changed.
      if (value != isNodeBlocked) {
        linkState = value ? LinkState.NodeIsBlocked : LinkState.Available;
        part.Modules.OfType<ILinkStateEventListener>()
            .Where(l => !ReferenceEquals(l, this))
            .ToList()
            .ForEach(m => m.OnKASNodeBlockedState(this, value));
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

  /// <summary>
  /// Accessor to the dependent nodes setting. See <see cref="dependentNodes"/>
  /// </summary>
  protected string[] dependentNodeNames {
    get {
      if (_dependentNodeNames == null) {
        _dependentNodeNames = dependentNodes.Split(new[] {','});
      }
      return _dependentNodeNames;
    }
  }
  string[] _dependentNodeNames;
  #endregion

  #region IActivateOnDecouple implementation
  /// <inheritdoc/>
  public virtual void DecoupleAction(string nodeName, bool weDecouple) {
    if (nodeName == attachNodeName) {
      AsyncCall.CallOnEndOfFrame(this, CheckAttachNode);
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
    ConfigAccessor.ReadPartConfig(this);

    LocalizeModule();
    linkStateMachine = new SimpleStateMachine<LinkState>(true /* strict */);
    SetupStateMachine();
    GameEvents.onPartCouple.Add(OnPartCoupleEvent);
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    base.OnLoad(node);
    ConfigAccessor.ReadPartConfig(this);
    ConfigAccessor.ReadFieldsFromNode(node, GetType(), this, StdPersistentGroups.PartPersistant);

    parsedAttachNode = part.FindAttachNode(attachNodeName);
    isAutoAttachNode = parsedAttachNode == null;
    if (isAutoAttachNode) {
      parsedAttachNode = KASAPI.AttachNodesUtils.ParseNodeFromString(
          part, attachNodeDef, attachNodeName);
      if (parsedAttachNode != null) {
        HostedDebugLog.Fine(
            this, "Created auto node: {0}", KASAPI.AttachNodesUtils.NodeId(parsedAttachNode));
        if (coupleNode != null && (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor)) {
          // Only pre-add the node in the scenes that assume restoring a vessel state.
          // We'll drop it in the OnStartFinished if not used.
          KASAPI.AttachNodesUtils.AddNode(part, coupleNode);
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
  public override void OnSave(ConfigNode node) {
    base.OnSave(node);
    ConfigAccessor.WriteFieldsIntoNode(node, GetType(), this, StdPersistentGroups.PartPersistant);
  }

  /// <inheritdoc/>
  public override void OnStartFinished(PartModule.StartState state) {
    base.OnStartFinished(state);
    // Prevent the third-party logic on the auto node. See OnLoad.
    if ((HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor)
        && isAutoAttachNode && coupleNode != null && coupleNode.attachedPart == null) {
      KASAPI.AttachNodesUtils.DropNode(part, coupleNode);
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
    // The check may want to establish a link, but this will only succeed if the physics has
    // started.
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
  protected virtual void CheckAttachNode() {
    if (isAutoAttachNode && coupleNode != null && coupleNode.attachedPart == null) {
      // Ensure the auto node is removed and is cleared from the attached part if not used.
      KASAPI.AttachNodesUtils.DropNode(part, coupleNode);
    }
  }

  /// <summary>Sets the peer's state machine.</summary>
  protected virtual void SetupStateMachine() {
    linkStateMachine.onAfterTransition += (start, end) => {
      persistedLinkState = linkState;
      if (coupleNode != null && (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor)) {
        if (end == LinkState.Available) {
          if (!isAutoAttachNode) {
            KASAPI.AttachNodesUtils.AddNode(part, coupleNode);
          }
        } else {
          if (coupleNode.attachedPart == null) {
            KASAPI.AttachNodesUtils.DropNode(part, coupleNode);
          }
        }
      }
    };
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

  #region ILinkStateEventListener implementation
  /// <inheritdoc/>
  public virtual void OnKASLinkedState(KASEvents.LinkEvent info, bool isLinked) {
    var peer = info.source.part == part
        ? info.source as ILinkPeer
        : info.target as ILinkPeer;
    if (ReferenceEquals(peer, this)
        || (peer.cfgAttachNodeName != attachNodeName
            && !dependentNodeNames.Contains(peer.cfgAttachNodeName))) {
      return;  // Nothing to do.
    }
    if (isLinked) {
      isLocked = true;
    } else {
      if (isLocked) {  // Ensure we're not trying to reset a custom mode. 
        isLocked = false;
      }
    }
  }

  /// <inheritdoc/>
  public virtual void OnKASNodeBlockedState(ILinkPeer ownerPeer, bool isBlocked) {
    if (dependentNodeNames.Contains(ownerPeer.cfgAttachNodeName)) {
      // This is a notification handler, so don't use the isNodeBlocked property to not trigger
      // more notifications.
      //FIXME: fix the comment when migrated to the setter methods.
      linkState = isBlocked ? LinkState.NodeIsBlocked : LinkState.Available;
    }
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
