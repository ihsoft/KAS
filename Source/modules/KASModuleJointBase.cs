// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.GUIUtils;
using KSPDev.KSPInterfaces;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using KSPDev.ProcessingUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KAS {

/// <summary>Module that controls a physical joint on a KAS part.</summary>
/// <remarks>
/// This module reacts on the KAS initated events to create/remove a physical joint between the
/// source and target. This module only deals with the joining of two parts together. It does not
/// deal with the collider(s) (see <see cref="ILinkRenderer"/>).
/// </remarks>
// Next localization ID: #kasLOC_00011.
public class KASModuleJointBase : PartModule,
    // KSP interfaces.
    IModuleInfo, IActivateOnDecouple,
    // KAS interfaces.
    ILinkJoint,
    // KSPDev syntax sugar interfaces.
    IPartModule, IsPackable, IKSPDevModuleInfo {

  #region Localizable GUI strings
  /// <include file="SpecialDocTags.xml" path="Tags/Message2/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.DistanceType']/*"/>
  protected readonly static Message<DistanceType, DistanceType> MinLengthLimitReachedMsg =
      new Message<DistanceType, DistanceType>(
          "#kasLOC_00000",
          defaultTemplate: "Link is too short: <<1>> < <<2>>",
          description: "Message to display when the link cannot be established because it's too"
          + " short."
          + "\nArgument <<1>> is the current link length of type DistanceType."
          + "\nArgument <<2>> is the part's config setting of type DistanceType.",
          example: "Link is too short: 1.22 m < 2.33 m");

  /// <include file="SpecialDocTags.xml" path="Tags/Message2/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.DistanceType']/*"/>
  protected readonly static Message<DistanceType, DistanceType> MaxLengthLimitReachedMsg =
      new Message<DistanceType, DistanceType>(
          "#kasLOC_00001",
          defaultTemplate: "Link is too long: <<1>> > <<2>>",
          description: "Message to display when the link cannot be established because it's too"
          + " long."
          + "\nArgument <<1>> is the current link length of type DistanceType."
          + "\nArgument <<2>> is the part's config setting of type DistanceType.",
          example: "Link is too long: 2.33 m > 1.22 m");

  /// <include file="SpecialDocTags.xml" path="Tags/Message2/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.AngleType']/*"/>
  protected readonly static Message<AngleType, AngleType> SourceNodeAngleLimitReachedMsg =
      new Message<AngleType, AngleType>(
          "#kasLOC_00002",
          defaultTemplate: "Source angle limit reached: <<1>> > <<2>>",
          description: "Message to display when the link cannot be established because the maximum"
          + " angle between the link vector and the joint normal at the SOURCE part is to big."
          + "\nArgument <<1>> is the current link angle of type AngleType."
          + "\nArgument <<2>> is the part's config setting of type AngleType.",
          example: "Source angle limit reached: 3° > 2.5°");

  /// <include file="SpecialDocTags.xml" path="Tags/Message2/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.AngleType']/*"/>
  protected readonly static Message<AngleType, AngleType> TargetNodeAngleLimitReachedMsg =
      new Message<AngleType, AngleType>(
          "#kasLOC_00003",
          defaultTemplate: "Target angle limit reached: <<1>> > <<2>>",
          description: "Message to display when the link cannot be established because the maximum"
          + " angle between the link vector and the joint normal at the TARGET part is to big."
          + "\nArgument <<1>> is the current link angle of type AngleType."
          + "\nArgument <<2>> is the part's config setting of type AngleType.",
          example: "Target angle limit reached: 3° > 2.5°");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.ForceType']/*"/>
  protected readonly static Message<ForceType> LinkLinearStrengthInfo = new Message<ForceType>(
      "#kasLOC_00004",
      defaultTemplate: "Link break force: <<1>>",
      description: "Info string in the editor for the link break force setting. The argument is of"
      + " type ForceType.",
      example: "Link break force: 1.2 kN");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.ForceType']/*"/>
  protected readonly static Message<ForceType> LinkBreakStrengthInfo = new Message<ForceType>(
      "#kasLOC_00005",
      defaultTemplate: "Link torque force: <<1>>",
      description: "Info string in the editor for the link break torque setting. The argument is of"
      + " type ForceType.",
      example: "Link torque force: 1.2 kN");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.DistanceType']/*"/>
  protected readonly static Message<DistanceType> MinimumLinkLengthInfo =
      new Message<DistanceType>(
          "#kasLOC_00006",
          defaultTemplate: "Minimum link length: <<1>>",
          description: "Info string in the editor for the minimum link length setting."
          + "\nArgument <<1>> is the part's config setting of type DistanceType.",
          example: "Minimum link length: 1.22 m");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.DistanceType']/*"/>
  protected readonly static Message<DistanceType> MaximumLinkLengthInfo =
      new Message<DistanceType>(
          "#kasLOC_00007",
          defaultTemplate: "Maximum link length: <<1>>",
          description: "Info string in the editor for the maximum link length setting."
          + "\nArgument <<1>> is the part's config setting of type DistanceType.",
          example: "Maximum link length: 1.22 m");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.AngleType']/*"/>
  protected readonly static Message<AngleType> SourceJointFreedomInfo = new Message<AngleType>(
      "#kasLOC_00008",
      defaultTemplate: "Source angle limit: <<1>>",
      description: "Info string in the editor for the maximum allowed angle at the source."
      + "\nArgument <<1>> is the part's config setting of type AngleType.",
      example: "Source angle limit: 1.2°");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.AngleType']/*"/>
  protected readonly static Message<AngleType> TargetJointFreedomInfo = new Message<AngleType>(
      "#kasLOC_00009",
      defaultTemplate: "Target angle limit: <<1>>",
      description: "Info string in the editor for the maximum allowed angle at the target."
      + "\nArgument <<1>> is the part's config setting of type AngleType.",
      example: "Target angle limit: 1.2°");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected readonly static Message ModuleTitle = new Message(
      "#kasLOC_00010",
      defaultTemplate: "KAS Joint",
      description: "Title of the module to present in the editor details window.");
  #endregion

  #region ILinkJoint CFG properties
  /// <inheritdoc/>
  public string cfgJointName { get { return jointName; } }

  /// <inheritdoc/>
  /// <remarks>
  /// When calculating the strength, the minimum of the source and the target breaking forces is
  /// used as a base. Then, the value is scaled to the node size assuming it's a stack node.
  /// </remarks>
  /// <seealso cref="ScaleForceToNode"/>
  public float cfgLinkBreakForce { get { return linkBreakForce; } }

  /// <inheritdoc/>
  /// <remarks>
  /// When calculating the torque, the minimum of the source and the target breaking torque is
  /// used as a base. Then, the value is scaled to the node size assuming it's a stack node.
  /// </remarks>
  /// <seealso cref="ScaleForceToNode"/>
  public float cfgLinkBreakTorque { get { return linkBreakTorque; } }

  /// <inheritdoc/>
  public int cfgSourceLinkAngleLimit { get { return sourceLinkAngleLimit; } }

  /// <inheritdoc/>
  public int cfgTargetLinkAngleLimit { get { return targetLinkAngleLimit; } }

  /// <inheritdoc/>
  public float cfgMinLinkLength { get { return minLinkLength; } }

  /// <inheritdoc/>
  public float cfgMaxLinkLength { get { return maxLinkLength; } }
  #endregion

  #region Part's config fields
  /// <summary>See <see cref="cfgJointName"/>.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string jointName = "";

  /// <summary>Defines how the physics joint breaking force and torque are scaled.</summary>
  /// <remarks>
  /// The larger is the scale, the higher are the actual values used in physics. Size <c>0</c>
  /// matches the game's "tiny".
  /// </remarks>
  /// <seealso cref="linkBreakForce"/>
  /// <seealso cref="linkBreakTorque"/>
  /// <seealso cref="SetBreakForces"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public int attachNodeSize = 0;

  /// <summary>
  /// The unscaled maximum force that can be applied on the joint before it breaks.
  /// </summary>
  /// <seealso cref="attachNodeSize"/>
  /// <seealso cref="cfgLinkBreakForce"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float linkBreakForce = 0;

  /// <summary>
  /// The unscaled maximum torque that can be applied on the joint before it breaks.
  /// </summary>
  /// <seealso cref="attachNodeSize"/>
  /// <seealso cref="cfgLinkBreakTorque"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float linkBreakTorque = 0;

  /// <summary>
  /// Maximum allowed angle between the attach node normal and the link at the source part.
  /// </summary>
  /// <seealso cref="cfgSourceLinkAngleLimit"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public int sourceLinkAngleLimit = 0;

  /// <summary>
  /// Maximum allowed angle between the attach node normal and the link at the target part.
  /// </summary>
  /// <seealso cref="cfgTargetLinkAngleLimit"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public int targetLinkAngleLimit = 0;

  /// <summary>Minumum allowed distance between the source and target parts.</summary>
  /// <seealso cref="cfgMinLinkLength"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float minLinkLength = 0;

  /// <summary>Maximum allowed distance between the source and target parts.</summary>
  /// <seealso cref="cfgMaxLinkLength"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float maxLinkLength = 0;
  #endregion

  #region CFG/persistent fields
  /// <summary>
  /// Tells if the source and the target parts should couple when making a link between the
  /// different vessels.
  /// </summary>
  /// <seealso cref="coupleOnLinkMode"/>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  [KSPField(isPersistant = true)]
  public bool coupleWhenLinked;
  #endregion

  #region ILinkJoint implementation
  /// <inheritdoc/>
  public ILinkSource linkSource { get; private set; }

  /// <inheritdoc/>
  public ILinkTarget linkTarget { get; private set; }

  /// <inheritdoc/>
  public bool coupleOnLinkMode {
    get { return coupleWhenLinked; }
    private set { coupleWhenLinked = value; }
  }
  #endregion

  #region Inheritable properties
  /// <summary>Length at the moment of creating the joint.</summary>
  /// <value>Distance in meters.</value>
  /// <remarks>
  /// The elastic joints may allow the length deviation. This value can be used as a base.
  /// </remarks>
  protected float originalLength { get; private set; }

  /// <summary>Tells if there is a physical joint created.</summary>
  /// <value><c>true</c> if the source and target parts are physically linked.</value>
  protected bool isLinked { get; private set; }

  /// <summary>Tells if the parts of the link are coupled in the vessels hierarchy.</summary>
  /// <value>
  /// <c>true</c> if either the source part is coupled to the target, or the vise versa.
  /// </value>
  protected bool isCoupled {
    get {
      return linkSource.part.parent == linkTarget.part || linkTarget.part.parent == linkSource.part;
    }
  }

  /// <summary>Returns the PartJoint which manages this connection.</summary>
  /// <value>The joint or <c>null</c> if the link is not established or not coupled.</value>
  protected PartJoint partJoint {
    get {
      if (isCoupled) {
        return linkSource.part.parent == linkTarget.part
            ? linkSource.part.attachJoint
            : linkTarget.part.attachJoint;
      }
      return null;
    }
  }

  /// <summary>All the joints that keep the source and the target together.</summary>
  /// <remarks>The list can be empty if there are no physical joints existing.</remarks>
  /// <value>List of the joints or <c>null</c> if not linked.</value>
  /// <seealso cref="customJoints"/>
  protected List<ConfigurableJoint> joints {
    get {
      if (isLinked) {
        if (partJoint != null) {
          return partJoint.joints;
        }
        return customJoints ?? new List<ConfigurableJoint>();
      }
      return null;
    }
  }

  /// <summary>The physical joints that were created for the not coupling mode.</summary>
  /// <remarks>
  /// This value is simply ignored if there is a <c>PartJoint</c> that connects the parts. The good
  /// approach is to clear eitehr the custom joints or the part joint. Having them both in action is
  /// almost always a bad idea.
  /// </remarks>
  /// <value>The list of the joints or <c>null</c> if there are none.</value>
  /// <seealso cref="joints"/>
  protected List<ConfigurableJoint> customJoints;
  #endregion

  #region Local members
  bool selfDecoupledAction;
  #endregion    

  #region IActivateOnDecouple implementation
  /// <inheritdoc/>
  public virtual void DecoupleAction(string nodeName, bool weDecouple) {
    if (isLinked && linkSource.cfgAttachNodeName == nodeName) {
      var srcPart = linkSource.part;
      var tgtPart = linkTarget.part;
      KASAPI.AttachNodesUtils.DropAttachNode(srcPart, linkSource.cfgAttachNodeName);
      KASAPI.AttachNodesUtils.DropAttachNode(tgtPart, linkTarget.cfgAttachNodeName);
      if (!selfDecoupledAction) {
        linkSource.BreakCurrentLink(LinkActorType.Physics);
      }
      //FIXME: do it in a global vessel modified event? and only refresh the colliders
      // Refresh all the links between the former vessels.
      AsyncCall.CallOnEndOfFrame(this, () => RefreshAllLinks(srcPart.vessel, tgtPart.vessel));
    }
  }
  #endregion

  #region IJointEventsListener implementation
  /// <inheritdoc/>
  public virtual void OnJointBreak(float breakForce) {
    if (!isLinked || isCoupled || customJoints == null) {
      return;  // In the coupled mode we'd get DecoupleAction().
    }
    // The break event is sent for *any* joint on the game object that got broken. However, it may
    // not be our link's joint. To figure it out, wait till the engine has cleared the object. 
    AsyncCall.CallOnFixedUpdate(this, () => {
      if (customJoints != null && customJoints.Any(x => x == null)) {
        linkSource.BreakCurrentLink(
            LinkActorType.Physics,
            moveFocusOnTarget: linkTarget.part.vessel == FlightGlobals.ActiveVessel);
      }
    });
  }
  #endregion

  #region ILinkJoint implementation
  /// <inheritdoc/>
  public virtual bool CreateJoint(ILinkSource source, ILinkTarget target) {
    if (isLinked) {
      HostedDebugLog.Error(
          this, "Cannot link the joint which is already linked to: {0}", linkTarget);
      return false;
    }
    if (CheckConstraints(source, target.physicalAnchorTransform).Length > 0) {
      return false;
    }
    linkSource = source;
    linkTarget = target;
    originalLength = Vector3.Distance(source.physicalAnchorTransform.position,
                                      target.physicalAnchorTransform.position);
    isLinked = true;
    if (coupleOnLinkMode) {
      CoupleParts();
    } else {
      AttachParts();
    }
    return true;
  }

  /// <inheritdoc/>
  public virtual void DropJoint() {
    if (isLinked) {
      if (isCoupled) {
        DecoupleParts();
      } else {
        DetachParts();
      }
    }
    CleanupCustomJoints();
    linkSource = null;
    linkTarget = null;
    isLinked = false;
  }

  /// <inheritdoc/>
  public virtual void AdjustJoint(bool isUnbreakable = false) {
    if (!isCoupled) {
      if (isUnbreakable) {
        joints.ForEach(j => SetBreakForces(j, Mathf.Infinity, Mathf.Infinity));
      } else {
        joints.ForEach(j => SetBreakForces(j, cfgLinkBreakForce, cfgLinkBreakTorque));
      }
    }
  }

  /// <inheritdoc/>
  public string[] CheckConstraints(ILinkSource source, Transform targetTransform) {
    var errors = new[] {
        CheckLengthLimit(source, targetTransform),
        CheckAngleLimitAtSource(source, targetTransform),
        CheckAngleLimitAtTarget(source, targetTransform),
    };
    return errors.Where(x => x != null).ToArray();
  }

  /// <inheritdoc/>
  public virtual void SetCoupleOnLinkMode(bool isCoupleOnLink, LinkActorType actor) {
    coupleOnLinkMode = isCoupleOnLink;
    if (!isLinked) {
      HostedDebugLog.Fine(
          this, "Coupling mode updated in a non-linked module: {0}", isCoupleOnLink);
      return;
    }
    // Refresh the couple state by simply re-linking.
    var oldSource = linkSource;
    var oldTarget = linkTarget;
    linkSource.BreakCurrentLink(actor);
    if (oldSource.StartLinking(GUILinkMode.API, actor) && !oldSource.LinkToTarget(oldTarget)) {
      oldSource.CancelLinking();
    }
  }
  #endregion

  #region IModuleInfo implementation
  /// <inheritdoc/>
  public override string GetInfo() {
    var sb = new StringBuilder(base.GetInfo());
    if (linkBreakForce > 0) {
      sb.AppendLine(LinkLinearStrengthInfo.Format(linkBreakForce));
    }
    if (linkBreakTorque > 0) {
      sb.AppendLine(LinkBreakStrengthInfo.Format(linkBreakTorque));
    }
    if (minLinkLength > 0) {
      sb.AppendLine(MinimumLinkLengthInfo.Format(minLinkLength));
    }
    if (maxLinkLength > 0) {
      sb.AppendLine(MaximumLinkLengthInfo.Format(maxLinkLength));
    }
    if (sourceLinkAngleLimit > 0) {
      sb.AppendLine(SourceJointFreedomInfo.Format(sourceLinkAngleLimit));
    }
    if (targetLinkAngleLimit > 0) {
      sb.AppendLine(TargetJointFreedomInfo.Format(targetLinkAngleLimit));
    }
    return sb.ToString();
  }

  /// <inheritdoc/>
  public virtual string GetModuleTitle() {
    return ModuleTitle;
  }

  /// <inheritdoc/>
  public virtual Callback<Rect> GetDrawModulePanelCallback() {
    return null;
  }

  /// <inheritdoc/>
  public virtual string GetPrimaryField() {
    return null;
  }
  #endregion

  #region IsPackable implementation
  /// <inheritdoc/>
  public virtual void OnPartUnpack() {
    if (isLinked) {
      AdjustJoint();
    }
  }

  /// <inheritdoc/>
  public virtual void OnPartPack() {
    if (isLinked) {
      AdjustJoint(isUnbreakable: true);
    }
  }
  #endregion

  #region Inheritable methods
  /// <summary>Sets the attach node properties.</summary>
  /// <param name="attachNode">The node to set properties for.</param>
  /// <param name="isSource">Tells if the node belings to the source or to the target part.</param>
  protected virtual void SetupAttachNode(AttachNode attachNode, bool isSource) {
    attachNode.attachMethod = AttachNodeMethod.FIXED_JOINT;
    attachNode.size = attachNodeSize;
    attachNode.breakingForce = linkBreakForce;
    attachNode.breakingTorque = linkBreakTorque;
    if (isSource) {
      attachNode.attachedPart = linkTarget.part;
      attachNode.attachedPartId = linkTarget.part.flightID;
    } else {
      attachNode.attachedPart = linkSource.part;
      attachNode.attachedPartId = linkSource.part.flightID;
    }
  }

  /// <summary>Couples the source and the target parts merging them into a single vessel.</summary>
  /// <remarks>
  /// It's OK to call this method if the parts are already coupled. It's a normal way to have the
  /// attach nodes created on the vessel load.
  /// </remarks>
  /// <seealso cref="DecoupleParts"/>
  protected virtual void CoupleParts() {
    if (isLinked && isCoupled) {
      // Ensure the docking nodes are existing. This may not be the case if the vessel has just been
      // restored from the save file.
      HostedDebugLog.Fine(this, "Refreshing nodes. Already coupled: {0} <=> {1}",
                          linkSource, linkTarget);
      //FIXME: create nodes at the physical offset/rotation
      if (linkSource.part.FindAttachNode(linkSource.cfgAttachNodeName) == null) {
        SetupAttachNode(KASAPI.AttachNodesUtils.CreateAttachNode(
            linkSource.part, linkSource.cfgAttachNodeName, linkSource.nodeTransform),
            isSource: true);
      }
      if (linkTarget.part.FindAttachNode(linkTarget.cfgAttachNodeName) == null) {
        SetupAttachNode(KASAPI.AttachNodesUtils.CreateAttachNode(
            linkTarget.part, linkTarget.cfgAttachNodeName, linkTarget.nodeTransform),
            isSource: false);
      }
      return;
    }
    if (!isLinked || linkSource.part.vessel == linkTarget.part.vessel) {
      HostedDebugLog.Fine(this, "Skip coupling: {0} <=> {1}", linkSource, linkTarget);
      return;
    }
    var srcNode = KASAPI.AttachNodesUtils.CreateAttachNode(
        linkSource.part, linkSource.cfgAttachNodeName, linkSource.nodeTransform);
    SetupAttachNode(srcNode, isSource: true);
    var tgtNode = KASAPI.AttachNodesUtils.CreateAttachNode(
        linkTarget.part, linkTarget.cfgAttachNodeName, linkTarget.nodeTransform);
    SetupAttachNode(srcNode, isSource: false);
    // TODO(ihsoft): Store the vessel info into the source.
    KASAPI.LinkUtils.CoupleParts(tgtNode, srcNode, toDominantVessel: true);
  }

  /// <summary>Creates a physical link between the source and the target parts.</summary>
  /// <seealso cref="DetachParts"/>
  protected virtual void AttachParts() {
    customJoints = new List<ConfigurableJoint>();
    var rigidJoint =  linkSource.part.gameObject.AddComponent<ConfigurableJoint>();
    KASAPI.JointUtils.ResetJoint(rigidJoint);
    rigidJoint.connectedBody = linkTarget.part.rb;
    customJoints.Add(rigidJoint);
    SetBreakForces(rigidJoint, linkBreakForce, linkBreakTorque);
    HostedDebugLog.Fine(this, "Create a rigid link to: {0}", linkTarget);
  }

  /// <summary>
  /// Decouples the source and the target parts turning them into the separate vessels.
  /// </summary>
  /// <seealso cref="CoupleParts"/>
  protected virtual void DecoupleParts() {
    if (!isCoupled) {
      HostedDebugLog.Error(this, "Cannot decouple - bad link/part state");
      return;
    }
    selfDecoupledAction = true;
    KASAPI.LinkUtils.DecoupleParts(linkSource.part, linkTarget.part);
    selfDecoupledAction = false;
  }

  /// <summary>Destroys the physical link between the source and the target parts.</summary>
  /// <seealso cref="AttachParts"/>
  protected virtual void DetachParts() {
    CleanupCustomJoints();
  }
  #endregion

  #region Utility methods
  /// <summary>
  /// Setups joint break force and torque while handling special values from config.
  /// </summary>
  /// <remarks>
  /// The forces are set so what they are not contradicting with the attached parts. Normally, joint
  /// must get destroyed by the physics before the attached part did.
  /// </remarks>
  /// <param name="joint">Joint to set forces for.</param>
  /// <param name="forceFromConfig">
  /// Break force from the config. If it's <c>0</c> then maxium acceptable force will be used.
  /// </param>
  /// <param name="torqueFromConfig">
  /// Break torque from the config. If it's <c>0</c> then maxium acceptable torque will be used.
  /// </param>
  /// <seealso cref="GetClampedBreakingForce"/>
  protected void SetBreakForces(Joint joint, float forceFromConfig, float torqueFromConfig) {
    joint.breakForce = GetClampedBreakingForce(forceFromConfig);
    joint.breakTorque = GetClampedBreakingTorque(torqueFromConfig);
  }

  /// <summary>
  /// Rounds down the value so what it doesn't contradict with source and target breaking forces.
  /// </summary>
  /// <remarks>
  /// It's a bad idea to make joint more durable than the parts that are connected with it. It's
  /// always best to have joint broken before the parts destruction. Custom code is encouraged to
  /// use this method to get the right force.
  /// </remarks>
  /// <param name="value">
  /// Breaking force value to round. If it's <c>0</c> then maximum possible value will be returned.
  /// </param>
  /// <param name="isStack">
  /// Type of the connection. Stack connections are much stronger than surface ones.
  /// </param>
  /// <returns>Force value that relates to the source and target parts durability.</returns>
  /// <seealso cref="attachNodeSize"/>
  protected float GetClampedBreakingForce(float value, bool isStack = true) {
    return Mathf.Approximately(value, 0)
        ? ScaleForceToNode(
            Mathf.Min(linkSource.part.breakingForce, linkTarget.part.breakingForce),
            isStack: isStack)
        : ScaleForceToNode(
            Mathf.Min(value, linkSource.part.breakingForce, linkTarget.part.breakingForce),
            isStack: isStack);
  }
  
  /// <summary>
  /// Rounds down the value so what it doesn't contradict with source and target breaking torques.
  /// </summary>
  /// <remarks>
  /// It's a bad idea to make joint more durable than the parts that are connected with it. It's
  /// always best to have joint broken before the parts destruction. Custom code is encouraged to
  /// use this method to get the right torque.
  /// </remarks>
  /// <param name="value">
  /// Breaking force value to round. If it's <c>0</c> then maximum possible value will be returned.
  /// </param>
  /// <param name="isStack">
  /// Type of the connection. Stack connections are much stronger than surface ones.
  /// </param>
  /// <returns>Force value that relates to the source and target parts durability.</returns>
  /// <seealso cref="attachNodeSize"/>
  protected float GetClampedBreakingTorque(float value, bool isStack = true) {
    return Mathf.Approximately(value, 0)
        ? ScaleForceToNode(
            Mathf.Min(linkSource.part.breakingTorque, linkTarget.part.breakingTorque),
            isStack: isStack)
        : ScaleForceToNode(
            Mathf.Min(value, linkSource.part.breakingTorque, linkTarget.part.breakingTorque),
            isStack: isStack);
  }

  /// <summary>Scales the force value to the node size.</summary>
  /// <remarks>Uses same approach as in <see cref="PartJoint"/>.</remarks>
  /// <param name="force">Base force to scale.</param>
  /// <param name="isStack">
  /// Type of the connection. Stack connections are much stronger than surface ones.
  /// </param>
  /// <returns>Force scaled to the node size.</returns>
  /// <seealso cref="attachNodeSize"/>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_part_joint.html">
  /// KSP: PartJoint</seealso>
  protected float ScaleForceToNode(float force, bool isStack = true) {
    return force * (1.0f + attachNodeSize) * (isStack ? 2.0f : 0.8f);
  }

  /// <summary>Checks if the link's length is within the limits.</summary>
  /// <remarks>This method assumes that the <paramref name="targetTransform"/> is a possible
  /// <see cref="ILinkTarget.nodeTransform"/> on the target. For this reason the source's
  /// <see cref="ILinkSource.targetPhysicalAnchor"/> is applied towards it when doing the
  /// calculations.
  /// </remarks>
  /// <param name="source">The source that probes the link.</param>
  /// <param name="targetTransform">The target of the link to check the length against.</param>
  /// <returns>An error message if link is over limit or <c>null</c> otherwise.</returns>
  protected string CheckLengthLimit(ILinkSource source, Transform targetTransform) {
    var length = Vector3.Distance(
        source.physicalAnchorTransform.position,
        targetTransform.TransformPoint(source.targetPhysicalAnchor));
    if (maxLinkLength > 0 && length > maxLinkLength) {
      return MaxLengthLimitReachedMsg.Format(length, maxLinkLength);
    }
    if (minLinkLength > 0 && length < minLinkLength) {
      return MinLengthLimitReachedMsg.Format(length, minLinkLength);
    }
    return null;
  }

  /// <summary>Checks if the link's angle at the source joint is within the limits.</summary>
  /// <remarks>This method assumes that the <paramref name="targetTransform"/> is a possible
  /// <see cref="ILinkTarget.nodeTransform"/> on the target. For this reason the source's
  /// <see cref="ILinkSource.targetPhysicalAnchor"/> is applied towards it when doing the
  /// calculations.
  /// </remarks>
  /// <param name="source">The source that probes the link.</param>
  /// <param name="targetTransform">The target of the link to check the angle against.</param>
  /// <returns>An error message if angle is over limit or <c>null</c> otherwise.</returns>
  protected string CheckAngleLimitAtSource(ILinkSource source, Transform targetTransform) {
    var linkVector = targetTransform.position - source.nodeTransform.position;
    var angle = Vector3.Angle(source.nodeTransform.rotation * Vector3.forward, linkVector);
    return sourceLinkAngleLimit > 0 && angle > sourceLinkAngleLimit
        ? SourceNodeAngleLimitReachedMsg.Format(angle, sourceLinkAngleLimit)
        : null;
  }

  /// <summary>Checks if the link's angle at the target joint is within the limits.</summary>
  /// <remarks>This method assumes that the <paramref name="targetTransform"/> is a possible
  /// <see cref="ILinkTarget.nodeTransform"/> on the target. For this reason the source's
  /// <see cref="ILinkSource.targetPhysicalAnchor"/> is applied towards it when doing the
  /// calculations.
  /// </remarks>
  /// <param name="source">The source that probes the link.</param>
  /// <param name="targetTransform">The target of the link to check the angle against.</param>
  /// <returns>An error message if the angle is over limit or <c>null</c> otherwise.</returns>
  protected string CheckAngleLimitAtTarget(ILinkSource source, Transform targetTransform) {
    var linkVector = source.nodeTransform.position - targetTransform.position;
    var angle = Vector3.Angle(targetTransform.rotation * Vector3.forward, linkVector);
    return targetLinkAngleLimit > 0 && angle > targetLinkAngleLimit
        ? TargetNodeAngleLimitReachedMsg.Format(angle, targetLinkAngleLimit)
        : null;
  }
  #endregion

  #region Local utility methods
  /// <summary>Reconnects all the links between the vesseles to refresh their state.</summary>
  /// <remarks>It's called when the vessels on the end have changed.</remarks>
  /// <param name="srcVessel"></param>
  /// <param name="tgtVessel"></param>
  void RefreshAllLinks(Vessel srcVessel, Vessel tgtVessel) {
    // Make a list copy since it may change as the parts are re-linking. 
    var linkCandidates = srcVessel.parts
        .SelectMany(x => x.FindModulesImplementing<ILinkJointBase>())
        .Where(x => x.linkSource != null && x.linkTarget != null)
        .Where(x =>
             x.linkSource.part.vessel == srcVessel && x.linkTarget.part.vessel == tgtVessel
             || x.linkSource.part.vessel == tgtVessel && x.linkTarget.part.vessel == srcVessel)
        .ToList();
    HostedDebugLog.Fine(this, "Refreshing {0} links between {1} and {2}",
                        linkCandidates.Count, srcVessel, tgtVessel);
    foreach (var linkCandidate in linkCandidates) {
      linkCandidate.SetCoupleOnLinkMode(linkCandidate.coupleOnLinkMode, LinkActorType.API);
    }
  }

  /// <summary>Drops and cleanup any custom joints.</summary>
  void CleanupCustomJoints() {
    if (customJoints != null) {
      HostedDebugLog.Fine(this, "Drop {0} joint(s) to: {1}", customJoints.Count, linkTarget);
      customJoints.ForEach(UnityEngine.Object.Destroy);
      customJoints = null;
    }
  }
  #endregion
}

}  // namespace
