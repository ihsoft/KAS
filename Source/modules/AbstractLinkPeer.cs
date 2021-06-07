// Kerbal Attachment System
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections;
using KASAPIv2;
using KSPDev.KSPInterfaces;
using KSPDev.LogUtils;
using KSPDev.PartUtils;
using KSPDev.ProcessingUtils;
using System.Linq;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KAS {

/// <summary>Base class that handles the basic functionality of the link's end.</summary>
/// <remarks>
/// This module doesn't define how the link is created, but it does the heavy lifting to keep it,
/// once it's established. The descendants are responsible for determining what peers can link with
/// each other.
/// </remarks>
public abstract class AbstractLinkPeer : AbstractPartModule,
    // KSP interfaces.
    IActivateOnDecouple,
    // KAS interfaces.
    ILinkPeer, ILinkStateEventListener,
    // KSPDev syntax sugar interfaces.
    IsPartDeathListener, IKSPActivateOnDecouple {

  #region Part's config fields
  /// <summary>See <see cref="cfgLinkType"/>.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Link type")]
  public string linkType = "";

  /// <summary>The localized string to display for the link type.</summary>
  /// <remarks>If missing or empty, then the types is show "as-is".</remarks>
  /// <seealso cref="cfgLinkType"/>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string linkTypeDisplayName = "";

  /// <summary>
  /// Definition of the attach node to automatically create when the coupling is needed.
  /// </summary>
  /// <remarks>
  /// The format of the string is exactly the same as for the part's attach nodes in the config.
  /// This node will not be available in the editor or in the flight for the third-party mods
  /// (like KIS).
  /// </remarks>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <seealso cref="coupleNode"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Attach node definition")]
  public string attachNodeDef = "";

  /// <summary>Name of the attach node for the link and coupling operations.</summary>
  /// <remarks>
  /// If an attach node with this name is existing on the part, then it will be used. Otherwise, it
  /// will be assumed that the node needs to be created/removed automatically as needed.
  /// </remarks>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <seealso cref="attachNodeDef"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Attach node name")]
  public string attachNodeName = "";

  /// <summary>
  /// Comma-separated list of names of the attach nodes which this module doesn't own but wants to
  /// aligns the state with.
  /// </summary>
  /// <remarks>
  /// The module will track the nodes and adjust its state as they were owned by the module. This
  /// can be used to lock/block the peer modules that control the different nodes, but need to
  /// cooperate with the other similar modules on the part. By setting the dependent nodes it's
  /// possible to define a group of peer modules which only allow linking of a single module at the
  /// time.
  /// </remarks>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <seealso cref="attachNodeDef"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Dependent nodes")]
  public string dependentNodes = "";

  /// <summary>Specifies if this peer can couple (dock) into the vessel's hierarchy.</summary>
  /// <seealso cref="coupleNode"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Allow coupling")]
  public bool allowCoupling;
  #endregion

  #region Persistent fields
  /// <summary>The link state in the last save action.</summary>
  /// <remarks>Normally, the base class handles it.</remarks>
  /// <include file="../SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  /// <seealso cref="linkState"/>
  [KSPField(isPersistant = true)]
  public LinkState persistedLinkState = LinkState.Available;

  /// <summary>The other peer's part flight ID in the last save action.</summary>
  /// <remarks>It's <c>0</c> if the peer is not linked.</remarks>
  /// <include file="../SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  /// <seealso cref="otherPeer"/>
  [KSPField(isPersistant = true)]
  public uint persistedLinkPartId;

  /// <summary>The other peer's module attach node name.</summary>
  /// <remarks>It's <c>null</c> if the peer is not linked.</remarks>
  /// <include file="../SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  /// <seealso cref="otherPeer"/>
  /// <seealso cref="linkNodeName"/>
  [KSPField(isPersistant = true)]
  public string persistedLinkNodeName = "";
  #endregion

  #region ILinkPeer properties implementation
  /// <inheritdoc/>
  public string cfgLinkType => linkType;

  /// <inheritdoc/>
  public string cfgAttachNodeName => attachNodeName;

  /// <inheritdoc/>
  public string[] cfgDependentNodeNames =>
      _dependentNodeNames ?? (_dependentNodeNames = dependentNodes.Split(','));
  string[] _dependentNodeNames;

  /// <inheritdoc/>
  /// <seealso cref="persistedLinkState"/>
  public LinkState linkState => linkStateMachine.currentState ?? persistedLinkState;

  /// <inheritdoc/>
  /// <seealso cref="persistedLinkPartId"/>
  /// <seealso cref="OnPeerChange"/>
  /// <seealso cref="SetOtherPeer"/>
  public virtual ILinkPeer otherPeer => _otherPeer;
  ILinkPeer _otherPeer;

  /// <inheritdoc/>
  public uint linkPartId => persistedLinkPartId;

  /// <inheritdoc/>
  public string linkNodeName => persistedLinkNodeName;

  /// <inheritdoc/>
  public Transform nodeTransform { get; private set; }

  /// <inheritdoc/>
  public AttachNode coupleNode => allowCoupling ? parsedAttachNode : null;

  /// <inheritdoc/>
  public AttachNode attachNode => parsedAttachNode;

  /// <inheritdoc/>
  public bool isLinked => linkState == LinkState.Linked;

  /// <inheritdoc/>
  /// <seealso cref="linkState"/>
  public bool isLocked => linkState == LinkState.Locked;

  /// <inheritdoc/>
  /// <seealso cref="SetLinkState"/>
  public bool isNodeBlocked => linkState == LinkState.NodeIsBlocked;
  #endregion

  #region Inheritable fields & properties
  /// <summary>
  /// State machine that controls the source state transitions and defines the reaction on these
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
  /// <include file="../KSPDevUtilsAPI_HelpIndex.xml" path="//item[@anme='T:KSPDev.ProcessingUtils.SimpleStateMachine_1']/*"/>
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
  /// <inheritdoc cref="IKSPActivateOnDecouple.DecoupleAction" />
  public virtual void DecoupleAction(string nodeName, bool weDecouple) {
    if (nodeName == attachNodeName) {
      HostedDebugLog.Fine(this, "Schedule coupling check from DECOUPLE action...");
      AsyncCall.CallOnEndOfFrame(this, CheckCoupleNode);
    }
  }
  #endregion

  #region IsPartDeathListener declaration
  /// <inheritdoc/>
  public abstract void OnPartDie();
  #endregion

  #region AbstractPartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    linkStateMachine = new SimpleStateMachine<LinkState>();
    SetupStateMachine();
    RegisterGameEventListener(GameEvents.onPartCouple, OnPartCoupleEvent);
    RegisterGameEventListener(GameEvents.onPartDie, OnPartDieEvent);
    RegisterGameEventListener(GameEvents.OnEVAConstructionModePartAttached, OnEVAConstructionModePartAttached);
  }

  /// <inheritdoc/>
  public override void OnDestroy() {
    base.OnDestroy();
    ShutdownStateMachine();
  }

  /// <inheritdoc/>
  public override void OnStart(StartState state) {
    base.OnStart(state);

    // Adjust state of a newly added module.
    if (persistedLinkState == LinkState.Available) {
      var linkedModule = part.Modules.OfType<ILinkPeer>()
          .FirstOrDefault(x =>
              x.isLinked && (x.cfgAttachNodeName == attachNodeName
                             || cfgDependentNodeNames.Contains(x.cfgAttachNodeName)));
      if (linkedModule != null) {
        persistedLinkState = LinkState.Locked;
        HostedDebugLog.Warning(this, "Lock new module due to state of: {0} => {1}",
                               linkedModule, linkedModule.linkState);
      }
    }
  }

  /// <inheritdoc/>
  public override void OnStartFinished(StartState state) {
    base.OnStartFinished(state);
    // Prevent the third-party logic on the auto node. See OnLoad.
    if ((HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor)
        && isAutoAttachNode && coupleNode != null && coupleNode.attachedPart == null) {
      KASAPI.AttachNodesUtils.DropNode(part, coupleNode);
    }
    if (persistedLinkState == LinkState.Linked) {
      RestoreOtherPeer();
      if (otherPeer == null) {
        HostedDebugLog.Error(this, "Cannot restore the link's peer");
        persistedLinkState = LinkState.Available;
        if (coupleNode != null && coupleNode.attachedPart != null) {
          // Decouple the coupled part if the link cannot be restored. It'll allow the player to
          // restore the game status without hacking the save files.
          AsyncCall.CallOnEndOfFrame(
              this, () => {
                if (coupleNode.attachedPart != null) {  // The part may get decoupled already.
                  KASAPI.LinkUtils.DecoupleParts(part, coupleNode.attachedPart);
                }
              });
        }
        // This may break a lot of logic, built on top of the KAS basic modules. However, it will
        // allow the main logic to work. Given it's a fallback, it's OK.
        part.Modules.OfType<AbstractLinkPeer>()
            .Where(p => p.isLinked && (p.cfgAttachNodeName == attachNodeName
                                       || p.cfgDependentNodeNames.Contains(attachNodeName)))
            .ToList()
            .ForEach(m => SetLinkState(LinkState.Available));
      } else {
        HostedDebugLog.Fine(this, "Restored link to: {0}", otherPeer);
      }
    }
    SetLinkState(persistedLinkState);
  }

  /// <inheritdoc/>
  protected override void InitModuleSettings() {
    base.InitModuleSettings();
    if (isAutoAttachNode && parsedAttachNode != null) {
      KASAPI.AttachNodesUtils.DropNode(part, parsedAttachNode);
    }
    parsedAttachNode = part.FindAttachNode(attachNodeName);
    isAutoAttachNode = parsedAttachNode == null;
    if (isAutoAttachNode) {
      parsedAttachNode = KASAPI.AttachNodesUtils.ParseNodeFromString(
          part, attachNodeDef, attachNodeName);
      if (parsedAttachNode != null) {
        HostedDebugLog.Fine(
            this, "Created auto node: {0}", KASAPI.AttachNodesUtils.NodeId(parsedAttachNode));
        if (coupleNode != null && (HighLogic.LoadedSceneIsFlight && vessel != null || HighLogic.LoadedSceneIsEditor)) {
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
  protected override void OnEvaPartLoaded() {
    base.OnEvaPartLoaded();
    persistedLinkState = LinkState.Available;
    persistedLinkPartId = 0;
    persistedLinkNodeName = "";
  }
  #endregion

  #region IsPackable implementation
  /// <inheritdoc cref="IsPackable.OnPartUnpack" />
  public virtual void OnPartUnpack() {
    StartCoroutine(ValidateCoupling(part, "UNPACK"));
  }

  /// <inheritdoc cref="IsPackable.OnPartPack" />
  public virtual void OnPartPack() {
  }
  #endregion

  #region New inheritable methods
  /// <summary>Fires when the couple node needs to be checked for a possible state change.</summary>
  /// <remarks>
  /// This method is called asynchronously at the end of frame. The triggering of this call doesn't
  /// mean the node state has changed. It only means that it could have changed, and something has
  /// either coupled with or decoupled from the node. The code is responsible to verify it and act
  /// accordingly.
  /// <para>
  /// This callback is called regardless to the <see cref="allowCoupling"/> settings. If the peer,
  /// being checked, cannot afford coupling, it must break the link.
  /// </para>
  /// </remarks>
  protected virtual void CheckCoupleNode() {
    if (isAutoAttachNode && coupleNode != null && coupleNode.attachedPart == null) {
      // Ensure the auto node is removed and is cleared from the attached part if not used.
      KASAPI.AttachNodesUtils.DropNode(part, coupleNode);
    }
    // Unblock node if the blocker is removed.
    if (linkState == LinkState.NodeIsBlocked && parsedAttachNode.attachedPart == null) {
      HostedDebugLog.Fine(this, "Resetting the blocked state due to the attachment has cleared");
      SetLinkState(LinkState.Available);
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
  /// The descendants may override this method to react on the link loading. If the link must not be
  /// restored, just reset <see cref="otherPeer"/> to <c>null</c>.
  /// </remarks>
  /// <seealso cref="otherPeer"/>
  protected virtual void RestoreOtherPeer() {
    SetOtherPeer(KASAPI.LinkUtils.FindLinkPeer(this));
  }

  /// <summary>Sets the link state.</summary>
  /// <param name="state">The new state.</param>
  /// <seealso cref="linkStateMachine"/>
  protected virtual void SetLinkState(LinkState state) {
    linkStateMachine.currentState = state;
  }

  /// <summary>Sets the opposite point of the link.</summary>
  /// <param name="peer">
  /// The other endpoint. It's <c>null</c> when the endpoint must be reset.
  /// </param>
  /// <seealso cref="otherPeer"/>
  protected virtual void SetOtherPeer(ILinkPeer peer) {
    var oldPeer = _otherPeer;
    _otherPeer = peer;
    if (_otherPeer != null) {
      persistedLinkPartId = _otherPeer.part.flightID;
      persistedLinkNodeName = _otherPeer.cfgAttachNodeName;
    } else {
      persistedLinkPartId = 0;
      persistedLinkNodeName = "";
    }
    OnPeerChange(oldPeer);
  }
  #endregion

  #region ILinkStateEventListener implementation
  /// <inheritdoc/>
  public virtual void OnKASLinkedState(IKasLinkEvent info, bool isLinkedState) {
    var peer = info.source.part == part
        ? info.source
        : info.target as ILinkPeer;
    if (!ReferenceEquals(peer, this)
        && (peer.cfgAttachNodeName == attachNodeName
            || cfgDependentNodeNames.Contains(peer.cfgAttachNodeName))) {
      // Only act when it's about (un)locking. Don't affect the state in all other cases.
      if (isLinkedState && linkState != LinkState.NodeIsBlocked) {
        SetLinkState(LinkState.Locked);
      } else if (!isLinkedState && linkState == LinkState.Locked) {
        SetLinkState(LinkState.Available);
      }
    }
  }

  /// <inheritdoc/>
  public void OnKASNodeBlockedState(ILinkPeer ownerPeer, bool isBlocked) {
    HostedDebugLog.Error(this, "Method call is unexpected! Doing nothing");  // Obsolete.
  }
  #endregion

  #region Local utility methods
  /// <summary>Triggers coupled node check.</summary>
  void OnPartCoupleEvent(GameEvents.FromToAction<Part, Part> action) {
    AttachNode node = null;
    if (action.from == part) {
      node = action.from.FindPartThroughNodes(action.to);
    } else if (action.to == part) {
      node = action.to.FindPartThroughNodes(action.from);
    }
    if (node != null && node.id == attachNodeName) {
      StartCoroutine(ValidateCoupling(part, "Couple Event"));
    }
  }

  /// <summary>Reacts on this part death and initiates the death callbacks.</summary>
  void OnPartDieEvent(Part p) {
    if (p == part) {
      HostedDebugLog.Fine(this, "Link peer dies...");
      OnPartDie();
    }
  }

  /// <summary>Reset any linked state on the EVA attached part since it's a clone.</summary>
  void OnEVAConstructionModePartAttached(Vessel v, Part p) {
    if (p.parent == part) {
      StartCoroutine(ValidateCoupling(p, "EVA attach"));
    }
  }

  /// <summary>Validates coupling state on the parts that may be in a packed state.</summary>
  /// <summary>
  /// Use this method when the part in question can be packed. Rhe method will wait till it unpacks, and then will call
  /// the <see cref="CheckCoupleNode"/> on it.
  /// </summary>
  /// <param name="p">The part to wait for the state on.</param>
  /// <param name="reason">A brief reason of why the method is being called. Only used for logging.</param>
  /// <returns>Co-routine enumerator.</returns>
  IEnumerator ValidateCoupling(Part p, string reason) {
    Func<bool> stateCheckFn = () =>
        linkState == LinkState.Available && p.State == PartStates.IDLE && (p == part || p.parent == part)
        && coupleNode?.attachedPart != null;
    if (!stateCheckFn.Invoke()) {
      yield break;  // Nothing to do. 
    }
    if (p.packed) {
      HostedDebugLog.Fine(this, "Waiting for the packed part to unpack: part={0}", p);
      yield return new WaitWhile(() => p.packed && stateCheckFn.Invoke());
    }
    if (linkState != LinkState.Available) {
      HostedDebugLog.Fine(this, "Another module took the ownership on the link");
      yield break;
    }
    if (!stateCheckFn.Invoke()) {
      HostedDebugLog.Warning(
          this, "Part has changed while waiting for coupling: state={0}, parent={1}, coupleNode={2}, attachedPart={3}",
          p.State, p.parent, coupleNode, coupleNode?.attachedPart);
      yield break;
    }
    yield return new WaitForEndOfFrame();
    HostedDebugLog.Fine(this, "Trigger coupling check: dependency={0}, reason={1}", p, reason);
    CheckCoupleNode();
  }
  #endregion
}

}  // namespace
