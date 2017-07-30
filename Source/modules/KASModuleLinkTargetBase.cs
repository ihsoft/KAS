// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Text;
using KASAPIv1;
using KSPDev.GUIUtils;
using KSPDev.KSPInterfaces;
using KSPDev.LogUtils;
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
    IPartModule, IsDestroyable, IsPartDeathListener, IKSPDevModuleInfo, IKSPActivateOnDecouple {

  #region Localizable GUI strings
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  protected static readonly Message<string> AcceptsLinkTypeInfo = new Message<string>(
      "#kasLOC_03000",
      defaultTemplate: "Accepts link type: <<1>>",
      description: "Info string in the editor for the link type setting."
      + "\nArgument <<1>> is the type string from the part's config.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected static readonly Message ModuleTitleInfo = new Message(
      "#kasLOC_03001",
      defaultTemplate: "KAS Joint Target",
      description: "Title of the module to present in the editor details window.");
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
        if (value != null) {
          persistedLinkSourcePartId = value.part.flightID;
          persistedLinkMode = value.cfgLinkMode;
          physicalAnchor = value.targetPhysicalAnchor;
          linkState = LinkState.Linked;
        } else {
          persistedLinkSourcePartId = 0;
          persistedLinkMode = LinkMode.DockVessels;  // Simply a default value.
          physicalAnchor = Vector3.zero;
          linkState = LinkState.Available;
        }
        persistedPhysicalAnchor = physicalAnchor;
        TriggerSourceChangeEvents(oldSource);
      }
    }
  }
  ILinkSource _linkSource;

  /// <inheritdoc/>
  public uint linkSourcePartId { get { return persistedLinkSourcePartId; } }

  /// <inheritdoc/>
  public virtual LinkState linkState {
    get {
      return linkStateMachine.currentState ?? persistedLinkState;
    }
    private set {
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
  public Vector3 physicalAnchor { get; private set; }

  /// <inheritdoc/>
  public AttachNode attachNode { get; private set; }
  #endregion

  #region Persistent fields
  /// <summary>Target link state in the last save action.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  [KSPField(isPersistant = true)]
  public LinkState persistedLinkState = LinkState.Available;

  /// <summary>Source part flight ID.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  [KSPField(isPersistant = true)]
  public uint persistedLinkSourcePartId;

  /// <summary>
  /// Source link mode. It only makes sense when state is <see cref="LinkState.Linked"/>. Target
  /// doesn't have own link mode until linked to a source.
  /// </summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  [KSPField(isPersistant = true)]
  public LinkMode persistedLinkMode = LinkMode.DockVessels;

  /// <summary>Physical anchor relative to the node trasfrom.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  [KSPField(isPersistant = true)]
  public Vector3 persistedPhysicalAnchor;
  #endregion

  #region Part's config fields
  /// <summary>See <see cref="cfgLinkType"/>.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string linkType = "";

  /// <summary>See <see cref="cfgAttachNodeName"/>.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string attachNodeName = "";

  /// <summary>Name of object in the model that defines attach node.</summary>
  /// <remarks>
  /// The value is a <see cref="Hierarchy.FindPartModelByPath(Part,string,Transform)"/> search
  /// path. The path is looked starting from the part's model root.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='M:KSPDev.Hierarchy.FindTransformByPath']/*"/>
  [KSPField]
  public string attachNodeTransformName = "";

  /// <summary>Defines attach node position in the local units.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public Vector3 attachNodePosition = Vector3.zero;

  /// <summary>Defines attach node orientation in the local units.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public Vector3 attachNodeOrientation = Vector3.up;

  /// <summary>
  /// Tells if compatible targets should highlight themselves when linking mode started.
  /// </summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public bool highlightCompatibleTargets = true;

  /// <summary>Defines highlight color for the compatible targets.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public Color highlightColor = Color.cyan;
  #endregion

  /// <summary>State machine that controls event reaction in different states.</summary>
  /// <remarks>
  /// Primary usage of the machine is managing subscriptions to the different game events. It's
  /// highly discouraged to use it for firing events or taking actions. Initial state can be setup
  /// under different circumstances, and the associated events and actions may get triggered at the
  /// inappropriate moment.
  /// </remarks>
  protected SimpleStateMachine<LinkState> linkStateMachine;

  /// <summary>Tells if this source is currectly linked with a target.</summary>
  /// <value>The current state of the link.</value>
  protected bool isLinked {
    get { return linkState == LinkState.Linked; }
  }

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();

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
    linkStateMachine.AddStateHandlers(
        LinkState.Linked,
        enterHandler: x => GameEvents.onVesselWillDestroy.Add(OnVesselWillDestroyGameEvent),
        leaveHandler: x => GameEvents.onVesselWillDestroy.Remove(OnVesselWillDestroyGameEvent));
  }

  /// <inheritdoc/>
  public override void OnStart(PartModule.StartState state) {
    base.OnStart(state);
    InitNodeTransform();  // Kerbal models may skip OnLoad event.

    // Try to restore link to the target.
    if (persistedLinkState == LinkState.Linked) {
      if (persistedLinkMode == LinkMode.DockVessels) {
        RestoreSource();
      } else {
        // Target vessel may not be loaded yet. Wait for it.
        AsyncCall.CallOnEndOfFrame(this, RestoreSource);
      }
    } else {
      linkStateMachine.currentState = persistedLinkState;
      linkState = linkState;  // Trigger state updates.
    }
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    base.OnLoad(node);
    InitNodeTransform();

    // If target is linked and docked then we need actual attach node. Create it.
    if (persistedLinkState == LinkState.Linked && persistedLinkMode == LinkMode.DockVessels) {
      attachNode = KASAPI.AttachNodesUtils.CreateAttachNode(part, attachNodeName, nodeTransform);
    }
  }
  #endregion

  #region IsDestroyable implementation
  /// <inheritdoc/>
  public virtual void OnDestroy() {
    linkStateMachine.currentState = null;  // Stop.
  }
  #endregion

  #region IsPartDeathListener implemenation
  /// <inheritdoc/>
  public virtual void OnPartDie() {
    if (isLinked) {
      linkSource.BreakCurrentLink(LinkActorType.Physics);
    }
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
    linkState = CheckCanLinkWith(source) ? LinkState.AcceptingLinks : LinkState.RejectingLinks;
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
  /// <summary>Triggers when the state has been assigned with a value.</summary>
  /// <remarks>
  /// This method triggers even when the new state doesn't differ from the old one. When it's
  /// important to catch the transition, check for the <paramref name="oldState"/>.
  /// </remarks>
  /// <param name="oldState">
  /// The state prior to the change. If it's <c>null</c>, then it's an initial state on the module
  /// creation.
  /// </param>
  protected virtual void OnStateChange(LinkState? oldState) {
    if (linkState == LinkState.AcceptingLinks && attachNode == null) {
      // Create an attach node to allow coupling.
      attachNode = KASAPI.AttachNodesUtils.CreateAttachNode(part, attachNodeName, nodeTransform);
    }
    if (oldState == LinkState.AcceptingLinks && !isLinked && attachNode != null) {
      // Drop the node once linking mode is over and the link hasn't been established.
      KASAPI.AttachNodesUtils.DropAttachNode(part, attachNodeName);
      attachNode = null;
    }

    // Install vessel destruction events.
    if (oldState != linkState) {
      if (linkState == LinkState.Linked) {
        GameEvents.onVesselWillDestroy.Add(OnVesselWillDestroyGameEvent);
      } else if (oldState == LinkState.Linked) {
        GameEvents.onVesselWillDestroy.Remove(OnVesselWillDestroyGameEvent);
      }
    }

    // Adjust compatible part highlight.
    // TODO(ihsoft): Handle mutliple targets on part to not override settings.
    if (highlightCompatibleTargets && oldState != linkState) {
      if (linkState == LinkState.AcceptingLinks) {
        part.SetHighlightType(Part.HighlightType.AlwaysOn);
        part.SetHighlightColor(highlightColor);
        part.SetHighlight(true, false);
      } else if (oldState == LinkState.AcceptingLinks) {
        part.SetHighlightDefault();
      }
    }
  }

  /// <summary>Finds linked source for the target, and updates the state.</summary>
  /// <remarks>
  /// Depending on the link mode this method may be called synchronously when the part is started or
  /// asynchronously at the end of frame.
  /// </remarks>
  /// <seealso cref="persistedLinkMode"/>
  protected virtual void RestoreSource() {
    _linkSource = KASAPI.LinkUtils.FindLinkSourceFromTarget(this);
    var startState = persistedLinkState;
    if (_linkSource == null) {
      HostedDebugLog.Error(
          this, "Cannot restore link to the source part id={0} on the attach node {1}",
          persistedLinkSourcePartId, attachNodeName);
      persistedLinkSourcePartId = 0;
      persistedLinkMode = LinkMode.DockVessels;
      persistedPhysicalAnchor = Vector3.zero;
      startState = LinkState.Available;
    }
    physicalAnchor = persistedPhysicalAnchor;
    linkStateMachine.currentState = startState;
    linkState = linkState;  // Trigger state updates.
  }

  /// <summary>Verifies that part can link with the source.</summary>
  /// <param name="source">Source to check against.</param>
  /// <returns>
  /// <c>true</c> if link is <i>technically</i> possible. It's not guaranteed that the link will
  /// succeed.
  /// </returns>
  protected virtual bool CheckCanLinkWith(ILinkSource source) {
    // Cannot attach to itself or incompatible link type.
    if (part == source.part || cfgLinkType != source.cfgLinkType) {
      return false;
    }
    // Check if same vessel part links are enabled. 
    if (source.part.vessel == vessel
        && (source.cfgLinkMode == LinkMode.TiePartsOnSameVessel
            || source.cfgLinkMode == LinkMode.TieAnyParts)) {
      return true;
    }
    // Check if different vessel part links are enabled. 
    if (source.part.vessel != vessel
        && (source.cfgLinkMode == LinkMode.DockVessels
            || source.cfgLinkMode == LinkMode.TiePartsOnDifferentVessels
            || source.cfgLinkMode == LinkMode.TieAnyParts)) {
      return true;
    }
    // Link is not allowed.
    return false;
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

  /// <summary>Finds the attach node transform or creates one.</summary>
  void InitNodeTransform() {
    var nodeName = attachNodeTransformName != ""
        ? attachNodeTransformName
        : attachNodeName + "-node";
    nodeTransform = Hierarchy.FindPartModelByPath(part, nodeName);
    if (nodeTransform == null) {
      nodeTransform = new GameObject(nodeName).transform;
      Hierarchy.MoveToParent(nodeTransform, Hierarchy.GetPartModelTransform(part),
                             newPosition: attachNodePosition,
                             newRotation: Quaternion.LookRotation(attachNodeOrientation));
      HostedDebugLog.Info(this, "Create attach node transform {0}: pos={1}, euler={2}",
                          nodeTransform,
                          DbgFormatter.Vector(nodeTransform.localPosition),
                          DbgFormatter.Vector(nodeTransform.localRotation.eulerAngles));
    } else {
      HostedDebugLog.Info(this, "Use attach node transform {0}: pos={1}, euler={2}",
                          nodeTransform,
                          DbgFormatter.Vector(nodeTransform.localPosition),
                          DbgFormatter.Vector(nodeTransform.localRotation.eulerAngles));
    }
  }

  /// <summary>Reacts on the vessel destcurtion and break the link if needed.</summary>
  /// <param name="targetVessel">The vessel that is being destroyed.</param>
  void OnVesselWillDestroyGameEvent(Vessel targetVessel) {
    if (isLinked && targetVessel == part.vessel && targetVessel != linkSource.part.vessel) {
      HostedDebugLog.Info(this, "Drop the link due to the owner vessel destruction");
      linkSource.BreakCurrentLink(LinkActorType.Physics);
    }
  }
  #endregion
}

}  // namespace
