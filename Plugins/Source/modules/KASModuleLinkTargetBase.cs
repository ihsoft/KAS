// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using System.Linq;
using KASAPIv1;
using KSPDev.KSPInterfaces;
using KSPDev.ProcessingUtils;
using UnityEngine;

namespace KAS {

/// <summary>Base link target module. Only controls target link state.</summary>
/// <remarks>
/// This module only deals with logic part of the linking. It remembers the source and notifies
/// other modules on the part about the link state. The actual work to make the link significant in
/// the game engine must be done by the link source, an implementation of <see cref="ILinkSource"/>.
/// <para>
/// External callers must access methods and properties declared in KSP base classes or interfaces
/// only. Members and methods that are not part of these declarations are not intended for the
/// public use <b>regardless</b> to their visibility level.
/// </para>
/// </remarks>
// TODO(ihsoft): Add code samples.
public class KASModuleLinkTargetBase :
    // KSP parents.
    PartModule, IActivateOnDecouple,
    // KAS parents.
    ILinkTarget, ILinkStateEventListener,
    // Syntax sugar parents.
    IPartModule, IsDestroyable, IKSPActivateOnDecouple {

  #region ILinkTarget config properties implementation
  /// <inheritdoc/>
  public string cfgLinkType { get { return type; } }
  /// <inheritdoc/>
  public string cfgAttachNodeName { get { return attachNodeName; } }
  #endregion
  
  #region ILinkTarget properties implementation
  /// <inheritdoc/>
  public virtual ILinkSource linkSource {
    get { return _linkSource; }
    set {
      if (_linkSource != value) {
        var oldSource = _linkSource;
        _linkSource = value;
        linkState = value != null ? LinkState.Linked : LinkState.Available;
        TriggerSourceChangeEvents(oldSource);
      }
    }
  }
  ILinkSource _linkSource;
  /// <inheritdoc/>
  public LinkState linkState {
    get {
      return linkStateMachine.isStarted ? linkStateMachine.currentState : persistedLinkState;
    }
    protected set {
      var oldState = linkStateMachine.currentState;
      linkStateMachine.currentState = value;
      persistedLinkState = value;
      OnStateChange(oldState);
    }
  }
  /// <inheritdoc/>
  public virtual bool isLocked {
    get { return linkState == LinkState.Locked; }
    set {
      if (value != isLocked) {
        linkState = value ? LinkState.Locked : LinkState.Available;
      }
    }
  }
  /// <inheritdoc/>
  public Transform nodeTransform { get; private set; }
  /// <inheritdoc/>
  public AttachNode attachNode { get; private set; }
  #endregion

  // These fileds must not be accessed outside of the module. They are declared public only
  // because KSP won't work otherwise. Ancenstors and external callers must access values via
  // interface properties. If property is not there then it means it's *intentionally* restricted
  // for the non-internal consumers.
  #region Persistent fields
  [KSPField(isPersistant = true)]
  public LinkState persistedLinkState = LinkState.Available;
  #endregion

  // These fileds must not be accessed outside of the module. They are declared public only
  // because KSP won't work otherwise. Ancenstors and external callers must access values via
  // interface properties. If property is not there then it means it's *intentionally* restricted
  // for the non-internal consumers.
  #region Part's config fields
  [KSPField]
  public string type = "";
  [KSPField]
  public string attachNodeName = "";
  [KSPField]
  public Vector3 attachNodePosition = Vector3.zero;
  [KSPField]
  public Vector3 attachNodeOrientation = Vector3.up;
  [KSPField]
  public bool highlightCompatibleTargets = true;
  [KSPField]
  public Color highlightColor = Color.green;
  #endregion

  /// <summary>State machine that controls event reaction in different states.</summary>
  /// <remarks>Primary usage of the machine is managing subscriptions to the different game events. 
  /// It's highly discouraged to use it for firing events or taking actions. Initial state can be
  /// setup under different circumstances, and the associated events and actions may get triggered
  /// at the inappropriate moment.</remarks>
  protected SimpleStateMachine<LinkState> linkStateMachine;

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    linkStateMachine = new SimpleStateMachine<LinkState>(true /* strict */);
    linkStateMachine.SetTransitionConstraint(
        LinkState.Available,
        new[] {LinkState.AcceptingLinks, LinkState.RejectingLinks});
    linkStateMachine.SetTransitionConstraint(
        LinkState.AcceptingLinks,
        new[] {LinkState.Available, LinkState.Linked, LinkState.Locked});
    linkStateMachine.SetTransitionConstraint(
        LinkState.Linked,
        new[] {LinkState.Available});
    linkStateMachine.SetTransitionConstraint(
        LinkState.Locked,
        new[] {LinkState.Available});
    linkStateMachine.SetTransitionConstraint(
        LinkState.RejectingLinks,
        new[] {LinkState.Available, LinkState.Locked});

    linkStateMachine.AddStateHandlers(
        LinkState.Available,
        enterHandler: x => KASEvents.OnStartLinking.Add(OnStartConnecting),
        leaveHandler: x => KASEvents.OnStartLinking.Remove(OnStartConnecting));
    linkStateMachine.AddStateHandlers(
        LinkState.AcceptingLinks,
        enterHandler: x => KASEvents.OnStopLinking.Add(OnStopConnecting),
        leaveHandler: x => KASEvents.OnStopLinking.Remove(OnStopConnecting));
    linkStateMachine.AddStateHandlers(
        LinkState.RejectingLinks,
        enterHandler: x => KASEvents.OnStopLinking.Add(OnStopConnecting),
        leaveHandler: x => KASEvents.OnStopLinking.Remove(OnStopConnecting));
  }

  /// <inheritdoc/>
  public override void OnStart(PartModule.StartState state) {
    var newState = persistedLinkState;
    if (persistedLinkState == LinkState.Linked) {
      //FIXME
      if (attachNode != null) {
        Debug.LogWarningFormat("attach node is not null, part is = {0}", attachNode.attachedPart);
      } else {
        Debug.LogWarningFormat("attach node is NULL");
      }
      
      var source = FindLinkedSource();
      if (source != null) {
        OnLinkRestore(source);
      } else {
        Debug.LogErrorFormat(
            "Target {0} (id={1}) cannot restore link to source on attach node {2}",
            part.name, part.flightID, attachNodeName);
        newState = LinkState.Available;
      }
    }
    linkStateMachine.Start(newState);
    linkState = linkState;  // Trigger updates.
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    base.OnLoad(node);

    //FIXME
    Debug.LogWarningFormat("** ON LOAD");

    // Create attach node transform. It will become a part of the model.
    if (HighLogic.LoadedScene == GameScenes.LOADING) {
      nodeTransform = new GameObject(attachNodeName + "-node").transform;
      nodeTransform.parent = part.FindModelTransform("model");
      nodeTransform.localPosition = attachNodePosition;
      nodeTransform.localScale = Vector3.one;
      nodeTransform.localRotation = Quaternion.LookRotation(attachNodeOrientation);
    } else {
      nodeTransform = part.FindModelTransform(attachNodeName + "-node");
    }

    // If source is linked then we need actual attach node. Create it.
    if (persistedLinkState == LinkState.Linked && HighLogic.LoadedSceneIsFlight) {
      CreateAttachNode();
    }
  }
  #endregion

  #region IsDestroyable implementation
  /// <inheritdoc/>
  public virtual void OnDestroy() {
    linkStateMachine.Stop();
  }
  #endregion

  #region ILinkEventListener implementation
  /// <inheritdoc/>
  public virtual void OnKASLinkCreatedEvent(KASEvents.LinkEvent info) {
    // Lock this target if another target on the part has accepted the link.
    if (!isLocked && !ReferenceEquals(info.target, this)) {
      isLocked = true;
    }
  }

  /// <inheritdoc/>
  public virtual void OnKASLinkBrokenEvent(KASEvents.LinkEvent info) {
    // Unlock this target if link with another target on the part has broke.
    if (isLocked && !ReferenceEquals(info.target, this)) {
      isLocked = false;
    }
  }
  #endregion

  #region KASEvents listeners
  /// <summary>Reacts on source link mode change.</summary>
  /// <remarks>KAS events listener.</remarks>
  /// <param name="source"></param>
  void OnStartConnecting(ILinkSource source) {
    linkState = (part != source.part && cfgLinkType == source.cfgLinkType)
        ? LinkState.AcceptingLinks
        : LinkState.RejectingLinks;
  }

  /// <summary>Reacts on source link mode change.</summary>
  /// <remarks>KAS events listener.</remarks>
  /// <param name="connectionSource"></param>
  void OnStopConnecting(ILinkSource connectionSource) {
    linkState = LinkState.Available;
  }
  #endregion

  #region IActivateOnDecouple implementation
  /// <inheritdoc/>
  public virtual void DecoupleAction(string nodeName, bool weDecouple) {
    //FIXME
    Debug.LogWarningFormat("TARGET: ** DecoupleAction: {0} (id={3}, weDecouple={1}, linkState={2}",
                           nodeName, weDecouple, linkState, part.flightID);
    DropAttachNode();
  }
  #endregion

  #region New inheritable methods
  /// <summary>Triggers when state has being assigned with a value.</summary>
  /// <remarks>This method triggers even when new state doesn't differ from the old one. When it's
  /// important to catch the transition check for <paramref name="oldState"/>.</remarks>
  /// <param name="oldState">State prior to the change.</param>
  protected virtual void OnStateChange(LinkState oldState) {
    if (oldState == linkState) {
      return;
    }
    // Adjust compatible part highlight. 
    if (highlightCompatibleTargets) {
      if (linkState == LinkState.AcceptingLinks) {
        part.highlighter.ConstantOn(highlightColor);
      } else if (oldState == LinkState.AcceptingLinks) {
        part.highlighter.ConstantOff();
      }
    }
    // Create attach node before possible linking, and drop it if link to the target wasn't made.
    if (linkState == LinkState.AcceptingLinks) {
      CreateAttachNode();
    } else if (oldState == LinkState.AcceptingLinks && linkState != LinkState.Linked) {
      DropAttachNode();
    }
  }

  /// <summary>Triggers when link is restored from the save file.</summary>
  /// <param name="source">Linked part source module.</param>
  protected virtual void OnLinkRestore(ILinkSource source) {
    _linkSource = source;
  }
  #endregion

  #region Local untility methods
  /// <summary>Triggesr link/unlink events when needed.</summary>
  /// <param name="oldSource">Link source before the change.</param>
  void TriggerSourceChangeEvents(ILinkSource oldSource) {
    if (oldSource != _linkSource) {
      var linkInfo = new KASEvents.LinkEvent(_linkSource ?? oldSource, this);
      if (_linkSource != null) {
        part.FindModulesImplementing<ILinkStateEventListener>()
            .ForEach(x => x.OnKASLinkCreatedEvent(linkInfo));
      } else {
        part.FindModulesImplementing<ILinkStateEventListener>()
            .ForEach(x => x.OnKASLinkBrokenEvent(linkInfo));
      }
    }
  }

  /// <summary>Finds this source link target.</summary>
  /// <returns>Target or <c>null</c> if nothing found or there is no attached part.</returns>
  ILinkSource FindLinkedSource() {
    if (attachNode != null && attachNode.attachedPart != null) {
      return attachNode.attachedPart.FindModulesImplementing<ILinkSource>()
          .FirstOrDefault(x => x.linkState == LinkState.Linked && x.cfgLinkType == cfgLinkType);
    }
    return null;
  }

  /// <summary>Creates actual attach node on the part.</summary>
  /// <remarks>Size of the node is always "small", and type is "stack".</remarks>
  /// <seealso cref="cfgAttachNodeName"/>
  /// <seealso cref="attachNode"/>
  void CreateAttachNode() {
    //FIXME
    Debug.LogWarningFormat("** TARGET: Create AN {0} for {1}", attachNodeName, part.name);
    attachNode = new AttachNode(attachNodeName, nodeTransform, 0, AttachNodeMethod.FIXED_JOINT);
    attachNode.nodeType = AttachNode.NodeType.Stack;
    attachNode.owner = part;
    attachNode.nodeTransform = nodeTransform;
    part.attachNodes.Add(attachNode);
  }

  /// <summary>Drops actual attach node on the part.</summary>
  /// <remarks>Don't drop the node until parts is decoupled from the vessel. Otherwise, decouple
  /// callback won't be called on the part.</remarks>
  /// <seealso cref="attachNode"/>
  /// <seealso href="https://kerbalspaceprogram.com/api/interface_i_activate_on_decouple.html">
  /// KSP: IActivateOnDecouple</seealso>
  void DropAttachNode() {
    //FIXME
    Debug.LogWarningFormat("** Drop attach node {0} in {1}", attachNode.id, part.name);
    part.attachNodes.Remove(attachNode);
    attachNode = null;
  }
  #endregion
}

}  // namespace
