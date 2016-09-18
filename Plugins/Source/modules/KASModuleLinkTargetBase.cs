// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using HighlightingSystem;
using System;
using System.Linq;
using KASAPIv1;
using KSPDev.ProcessingUtils;
using UnityEngine;

namespace KAS {

/// <summary>Base link target module. Only controls target link state.</summary>
/// <remarks>This module only deals with logic part of the linking. It remembers the source and
/// notifies other modules on the part about the link state. The actual work to make the link
/// significant in the game engine must be done by the link source, an implementation of
/// <see cref="ILinkSource"/>.
/// <para>External callers must access methods and properties declared in base classes or interfaces
/// only. Members and methods that are not part of these declarations are not intended for the
/// public use <b>regardless</b> to their visibility level.</para>
/// <para>Decendand classes may use any members and methods but good practice is restricting the
/// usage to the interfaces and virtuals only.</para>
/// </remarks>
// TODO(ihsoft): Add code samples.
public class KASModuleLinkTargetBase : PartModule, ILinkTarget, ILinkStateEventListener {
  #region ILinkTarget properties implementation
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTarget"/>.</para>
  public string cfgLinkType { get { return type; } }
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTarget"/>.</para>
  public string cfgAttachNodeName { get { return attachNodeName; } }
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTarget"/>.</para>
  public virtual bool isLocked {
    get { return linkState == LinkState.Locked; }
    set { linkState = value ? LinkState.Locked : LinkState.Available; }
  }
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTarget"/>.</para>
  public LinkState linkState {
    get { return linkStateMachine.currentState; }
    private set {
      var oldState = linkStateMachine.currentState;
      linkStateMachine.currentState = value;
      persistedLinkState = value;
      OnStateChange(oldState);
    }
  }
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTarget"/>.</para>
  public virtual ILinkSource linkSource {
    get { return _linkSource; }
    set {
      var oldSource = _linkSource;
      _linkSource = value;
      linkState = value != null ? LinkState.Linked : LinkState.Available;
      TriggerSourceChangeEvents(oldSource);
    }
  }
  ILinkSource _linkSource;
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTarget"/>.</para>
  public Transform nodeTransform {
    get {
      if (_nodeTransform == null) {
        _nodeTransform = KASAPI.AttachNodesUtils.GetOrCreateNodeTransform(part, attachNodeName);
      }
      return _nodeTransform;
    }
  }
  Transform _nodeTransform;
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTarget"/>.</para>
  public AttachNode attachNode {
    get {
      if (_attachNode == null) {
        var node = KASAPI.AttachNodesUtils.GetAttachNode(part, attachNodeName);
        if (node == null) {
          throw new InvalidOperationException(string.Format(
              "Cannot find attach nodes in the part: {0} (node={1})", part.name, attachNodeName));
        }
        if (node.nodeType != AttachNode.NodeType.Stack) {
          throw new InvalidOperationException(string.Format(
              "Attach node must be of type 'stack': {0} (type={1})", part.name, node.nodeType));
        }
        _attachNode = node;
      }
      return _attachNode;
    }
  }
  AttachNode _attachNode;
  #endregion

  // These fileds must not be accessed outside of the module. They are declared public only
  // because KSP won't work otherwise. Ancenstors and external callers must access values via
  // interface properties. If property is not there then it means it's *intentionally* restricted
  // for the non-internal consumers.
  #region Persistent fields
  [KSPField(isPersistant = true)]
  public LinkState persistedLinkState = LinkState.Available;
  #endregion

  #region Part's config fields
  // Here go settings from the part config. They must be of at least "protected" visiblity level for
  // the game to handle them properly.
  [KSPField]
  public string type = "";
  [KSPField]
  public string attachNodeName = "";
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

  /// <summary>Initalizes moulde state on part start.</summary>
  /// <remarks>Overridden from <see cref="PartModule"/>.</remarks>
  public override void OnStart(PartModule.StartState state) {
    linkStateMachine.Start(persistedLinkState);
    linkState = linkState;  // Trigger updates.
  }

  /// <summary>Initializes the object. An alternative to constructor.</summary>
  /// <remarks>Overridden from <see cref="PartModule"/>.</remarks>
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

    //FIXME
//    linkStateMachine.OnDebugStateChange = (from, to) =>
//        Debug.LogWarningFormat("TARGET: Part {0} (id={1}), module {2}: {3}=>{4}",
//                               part.name, part.craftID, moduleName, from, to);
  }

  /// <summary>Destroys the object. An alternative to destructor.</summary>
  /// <remarks>Overridden from <see cref="MonoBehaviour"/>.</remarks>
  public virtual void OnDestroy() {
    linkStateMachine.Stop();
  }

  #region ILinkEventListener implementation
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkStateEventListener"/></para>
  public virtual void OnKASLinkCreatedEvent(KASEvents.LinkEvent info) {
    // Lock this target if another target on the part has accepted the link.
    if (!isLocked && !ReferenceEquals(info.target, this)) {
      isLocked = true;
    }
  }

  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkStateEventListener"/></para>
  public virtual void OnKASLinkBrokenEvent(KASEvents.LinkEvent info) {
    // Unlock this target if link with another target on the part has broke.
    if (isLocked && !ReferenceEquals(info.target, this)) {
      isLocked = false;
    }
  }
  #endregion

  /// <summary>Triggers when connection is broken due to too strong force applied.</summary>
  /// <remarks>Overridden from <see cref="MonoBehaviour"/>.</remarks>
  /// <param name="breakForce">Actual force that has been applied.</param>
  protected virtual void OnJointBreak(float breakForce) {
    // Do nothing. The source will handle all the work.
  }

  public virtual void OnPartUnpack() {
    //FIXME: maybe use initialize
    //FIXME: may be turn on/off unbreakable
    if (linkState == LinkState.Linked && linkSource == null) {
      //Debug.LogWarningFormat("TRG: OnPartUnpack: {0} (id={1})", part.name, part.flightID);
      var an = part.findAttachNode(attachNodeName);
//      Debug.LogWarningFormat("TRG: node: {0} (id={1})",
//                             an.attachedPart, an.attachedPart.flightID);
      // Restore the link on part load.
      var source = an.attachedPart.FindModulesImplementing<ILinkSource>()
          .FirstOrDefault(x => x.cfgLinkType == cfgLinkType);
      if (source != null) {
        RestoreLink(an, source);
      } else {
        Debug.LogErrorFormat(
            "Target cannot restore link to source: {0} (id={1}) => {2} (id={3})",
            part.name, part.flightID,
            an.attachedPart.name, an.attachedPart.flightID);
        linkState = LinkState.Available;
      }
    }
  }
  
  protected virtual void RestoreLink(AttachNode an, ILinkSource source) {
    _linkSource = source;
  }

  /// <summary>Triggers when state has being assigned with a value.</summary>
  /// <remarks>This method triggers even when new state doesn't differ from the old one. When it's
  /// important to catch the transition check for <paramref name="oldState"/>.</remarks>
  /// <param name="oldState">State prior to the change.</param>
  protected virtual void OnStateChange(LinkState oldState) {
    if (highlightCompatibleTargets
        && (linkState == LinkState.AcceptingLinks || oldState == LinkState.AcceptingLinks)) {
      if (linkState == LinkState.AcceptingLinks) {
        part.highlighter.ConstantOn(highlightColor);
      } else {
        part.highlighter.ConstantOff();
      }
    }
  }

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
}

}  // namespace
