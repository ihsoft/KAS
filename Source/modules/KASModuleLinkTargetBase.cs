// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using System.Text;
using KASAPIv1;
using KSPDev.KSPInterfaces;
using KSPDev.GUIUtils;
using KSPDev.ModelUtils;
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
    PartModule, IModuleInfo, IActivateOnDecouple,
    // KAS parents.
    ILinkTarget, ILinkStateEventListener,
    // Syntax sugar parents.
    IPartModule, IsDestroyable, IKSPDevModuleInfo, IKSPActivateOnDecouple {

  #region Localizable GUI strings
  /// <summary>Info string in the editor for link type setting.</summary>
  protected static Message<string> AcceptsLinkTypeInfo = "Accepts link type: {0}";
  /// <summary>Title of the module to present in the editor details window.</summary>
  protected static Message ModuleTitleInfo = "KAS Joint Target";
  #endregion

  #region ILinkTarget config properties implementation
  /// <inheritdoc/>
  public string cfgLinkType { get { return linkType; } }
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
        persistedLinkSourcePartId = value != null ? value.part.flightID : 0;
        TriggerSourceChangeEvents(oldSource);
      }
    }
  }
  ILinkSource _linkSource;

  /// <inheritdoc/>
  public uint linkSourcePartId { get { return persistedLinkSourcePartId; } }

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

  #region Persistent fields
  /// <summary>Persistent config field. Target link state in the last save action.</summary>
  /// <remarks>
  /// This is a <see cref="KSPField"/> annotated field that is saved/restored with the vessel. It's
  /// handled by the KSP core and must <i>not</i> be altered directly. Moreover, in spite of it's
  /// declared <c>public</c> it must not be accessed outside of the module.
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField(isPersistant = true)]
  public LinkState persistedLinkState = LinkState.Available;

  /// <summary>Persistent config field. Source part flight ID.</summary>
  /// <remarks>
  /// <para>
  /// This is a <see cref="KSPField"/> annotated field that is saved/restored with the vessel. It's
  /// handled by the KSP core and must <i>not</i> be altered directly. Moreover, in spite of it's
  /// declared <c>public</c> it must not be accessed outside of the module.
  /// </para>
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField(isPersistant = true)]
  public uint persistedLinkSourcePartId;
  #endregion

  #region Part's config fields
  /// <summary>Config setting. See <see cref="cfgLinkType"/>.</summary>
  /// <remarks>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public string linkType = "";
  /// <summary>Config setting. See <see cref="cfgAttachNodeName"/>.</summary>
  /// <remarks>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public string attachNodeName = "";
  /// <summary>Config setting. Defines attach node position in the local units.</summary>
  /// <remarks>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public Vector3 attachNodePosition = Vector3.zero;
  /// <summary>Config setting. Defines attach node orientation in the local units.</summary>
  /// <remarks>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public Vector3 attachNodeOrientation = Vector3.up;
  /// <summary>
  /// Config setting. Tells if compatible targets should highlight themselves when linking mode
  /// started.
  /// </summary>
  /// <remarks>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public bool highlightCompatibleTargets = true;
  /// <summary>Config setting. Defines highlight color for the compatible targets.</summary>
  /// <remarks>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
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
      _linkSource = KASAPI.LinkUtils.FindLinkSourceFromTarget(this);
      if (_linkSource == null) {
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

    // Create attach node transform. It will become a part of the model.
    if (HighLogic.LoadedScene == GameScenes.LOADING) {
      nodeTransform = new GameObject(attachNodeName + "-node").transform;
      nodeTransform.parent = Hierarchy.GetPartModelTransform(part);
      nodeTransform.localPosition = attachNodePosition;
      nodeTransform.localScale = Vector3.one;
      nodeTransform.localRotation = Quaternion.LookRotation(attachNodeOrientation);
    } else {
      nodeTransform = part.FindModelTransform(attachNodeName + "-node");
    }

    // If source is linked then we need actual attach node. Create it.
    if (persistedLinkState == LinkState.Linked && HighLogic.LoadedSceneIsFlight) {
      attachNode = KASAPI.AttachNodesUtils.CreateAttachNode(part, attachNodeName, nodeTransform);
    }
  }
  #endregion

  #region IsDestroyable implementation
  /// <inheritdoc/>
  public virtual void OnDestroy() {
    linkStateMachine.Stop();
  }
  #endregion

  #region IModuleInfo implementation
  /// <inheritdoc/>
  public override string GetInfo() {
    var sb = new StringBuilder(base.GetInfo());
    sb.Append(AcceptsLinkTypeInfo.Format(linkType));
    return sb.ToString();
  }

  /// <inheritdoc/>
  public string GetModuleTitle() {
    return ModuleTitleInfo;
  }

  /// <inheritdoc/>
  public Callback<Rect> GetDrawModulePanelCallback() {
    return null;
  }

  /// <inheritdoc/>
  public string GetPrimaryField() {
    return null;
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
    if (part != source.part && cfgLinkType == source.cfgLinkType
        && (source.cfgLinkMode == LinkMode.Strut && vessel == source.part.vessel
            || source.cfgLinkMode != LinkMode.Strut && vessel != source.part.vessel)) {
      linkState = LinkState.AcceptingLinks;
    } else {
      linkState = LinkState.RejectingLinks;
    }
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
    if (nodeName == attachNodeName) {
      // Cleanup the node since once decoupled it's not more needed.
      KASAPI.AttachNodesUtils.DropAttachNode(part, attachNodeName);
      attachNode = null;
    }
  }
  #endregion

  #region New inheritable methods
  /// <summary>Triggers when state has being assigned with a value.</summary>
  /// <remarks>This method triggers even when new state doesn't differ from the old one. When it's
  /// important to catch the transition check for <paramref name="oldState"/>.</remarks>
  /// <param name="oldState">State prior to the change.</param>
  protected virtual void OnStateChange(LinkState oldState) {
    // Create attach node for linking state t oallow coupling. Drop the node once linking mode is
    // over and link hasn't been established.
    if (linkState == LinkState.AcceptingLinks && attachNode == null) {
      attachNode = KASAPI.AttachNodesUtils.CreateAttachNode(part, attachNodeName, nodeTransform);
    }
    if (oldState == LinkState.AcceptingLinks && linkState != LinkState.Linked
        && attachNode != null) {
      KASAPI.AttachNodesUtils.DropAttachNode(part, attachNodeName);
      attachNode = null;
    }

    // Adjust compatible part highlight. 
    if (highlightCompatibleTargets && oldState != linkState) {
      if (linkState == LinkState.AcceptingLinks) {
        part.highlighter.ConstantOn(highlightColor);
      } else if (oldState == LinkState.AcceptingLinks) {
        part.highlighter.ConstantOff();
      }
    }
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
  #endregion
}

}  // namespace
