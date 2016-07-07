// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using System.Linq;
using UnityEngine;
using KAS_API;
using KSPDev.Processing;

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
public class KASModuleLinkTargetBase : PartModule, ILinkTarget, ILinkEventListener {
  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkTarget"/>.</para>
  public string linkType { get { return type; } }
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

  // Here go settings from the part config. They must be of at least "protected" visiblity level for
  // the game to handle them properly.
  [KSPField]
  protected string type = string.Empty;

  /// <summary>State machine that controls event reaction in different states.</summary>
  /// <remarks>Primary usage of the machine is managing subscriptions to the different game events. 
  /// It's highly discouraged to use it for firing events or taking actions. Initial state can be
  /// setup under different circumstances, and the associated events and actions may get triggered
  /// at the inappropriate moment.</remarks>
  protected SimpleStateMachine<LinkState> linkStateMachine;

  /// <summary>Initalizes moulde state on part start.</summary>
  /// <remarks>Overridden from <see cref="PartModule"/>.</remarks>
  public override void OnStart(PartModule.StartState state) {
    Debug.LogWarning("*** TARGET: ON START");
    linkStateMachine.Start(LinkState.Available);
    linkState = linkState;  // Trigger updates.
  }

  /// <summary>Initalizes moulde state on part start.</summary>
  /// <remarks>Overridden from <see cref="PartModule"/>.</remarks>
  public override void OnLoad(ConfigNode node) {
    Debug.LogWarning("*** TARGET: ON LOAD");
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
    linkStateMachine.OnDebugStateChange = (from, to) =>
        Debug.LogWarningFormat("TARGET: Part {0} (id={1}), module {2}: {3}=>{4}",
                               part.name, part.craftID, moduleName, from, to);
  }

  /// <summary>Destroys the object. An alternative to destructor.</summary>
  /// <remarks>Overridden from <see cref="MonoBehaviour"/>.</remarks>
  public virtual void OnDestroy() {
    linkStateMachine.Stop();
  }

  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkEventListener"/></para>
  public virtual void OnKASLinkCreatedEvent(KASEvents.LinkInfo info) {
    // Lock this target if another target on the part has accepted the link.
    if (!isLocked && !ReferenceEquals(info.target, this)) {
      isLocked = true;
    }
  }

  /// <inheritdoc/>
  /// <para>Implements <see cref="ILinkEventListener"/></para>
  public virtual void OnKASLinkBrokenEvent(KASEvents.LinkInfo info) {
    // Unlock this target if link with another target on the part has broke.
    if (isLocked && !ReferenceEquals(info.target, this)) {
      isLocked = false;
    }
  }

  /// <summary>Triggers when connection is broken due to too strong force applied.</summary>
  /// <remarks>Overridden from <see cref="MonoBehaviour"/>.</remarks>
  /// <param name="breakForce">Actual force that has been applied.</param>
  protected virtual void OnJointBreak(float breakForce) {
    // Do nothing. The source will handle all the work.
  }

  /// <summary>Triggers when state has being assigned with a value.</summary>
  /// <remarks>This method triggers even when new state doesn't differ from the old one. When it's
  /// important to catch the transition check for <paramref name="oldState"/>.</remarks>
  /// <param name="oldState">State prior to the change.</param>
  protected virtual void OnStateChange(LinkState oldState) {
  }

  /// <summary>Reacts on source link mode change.</summary>
  /// <remarks>KAS events listener.</remarks>
  /// <param name="source"></param>
  void OnStartConnecting(ILinkSource source) {
    linkState = (part != source.part && linkType == source.linkType)
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
      if (_linkSource != null) {
        var linkInfo = new KASEvents.LinkInfo(_linkSource, this);
        SendMessage("OnLinkCreatedEvent", linkInfo, SendMessageOptions.DontRequireReceiver);
      } else {
        var linkInfo = new KASEvents.LinkInfo(oldSource, this);
        SendMessage("OnLinkBrokenEvent", linkInfo, SendMessageOptions.DontRequireReceiver);
      }
    }
  }
}

}  // namespace
