// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.ConfigUtils;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.KSPInterfaces;
using KSPDev.LogUtils;
using KSPDev.ProcessingUtils;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KAS {

/// <summary>Module that implements basic logic for a physical joint on a KAS part.</summary>
/// <remarks>
/// <para>
/// This module implements the logical part of managing the joints but doesn't actually create any.
/// By the contract, the joint state is never persisted. Instead, the link source should take care
/// of it and call <seealso cref="CreateJoint"/> when the part is loaded.
/// </para>
/// <para>
/// At the very least, the descendants must implement the <see cref="SetupPhysXJoints"/> method,
/// which establishes the PhysX joints. In the unusual cases an overriding of
/// <seealso cref="CleanupPhysXJoints"/> may be needed.
/// </para>
/// <para>
/// The descendants of this module can use the custom persistent fields of groups:
/// </para>
/// <list type="bullet">
/// <item><c>StdPersistentGroups.PartConfigLoadGroup</c></item>
/// <item><c>StdPersistentGroups.PartPersistant</c></item>
/// </list>
/// </remarks>
/// <seealso cref="SetupPhysXJoints"/>
/// <seealso cref="CleanupPhysXJoints"/>
/// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ConfigUtils.PersistentFieldAttribute']/*"/>
/// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ConfigUtils.StdPersistentGroups']/*"/>
// Next localization ID: #kasLOC_00011.
public abstract class AbstractJoint : PartModule,
    // KSP interfaces.
    IModuleInfo, IActivateOnDecouple,
    // KAS interfaces.
    ILinkJoint,
    // KSPDev parents.
    IsLocalizableModule,
    // KSPDev syntax sugar interfaces.
    IPartModule, IsPackable, IsDestroyable, IKSPDevModuleInfo, IKSPActivateOnDecouple {

  #region Localizable GUI strings
  /// <include file="SpecialDocTags.xml" path="Tags/Message2/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.DistanceType']/*"/>
  readonly static Message<DistanceType, DistanceType> MinLengthLimitReachedMsg =
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
  readonly static Message<DistanceType, DistanceType> MaxLengthLimitReachedMsg =
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
  readonly static Message<AngleType, AngleType> SourceNodeAngleLimitReachedMsg =
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
  readonly static Message<AngleType, AngleType> TargetNodeAngleLimitReachedMsg =
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
  readonly static Message<ForceType> LinkLinearStrengthInfo = new Message<ForceType>(
      "#kasLOC_00004",
      defaultTemplate: "Link break force: <<1>>",
      description: "Info string in the editor for the link break force setting. The argument is of"
      + " type ForceType.",
      example: "Link break force: 1.2 kN");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.ForceType']/*"/>
  readonly static Message<ForceType> LinkBreakStrengthInfo = new Message<ForceType>(
      "#kasLOC_00005",
      defaultTemplate: "Link torque force: <<1>>",
      description: "Info string in the editor for the link break torque setting. The argument is of"
      + " type ForceType.",
      example: "Link torque force: 1.2 kN");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.DistanceType']/*"/>
  readonly static Message<DistanceType> MinimumLinkLengthInfo =
      new Message<DistanceType>(
          "#kasLOC_00006",
          defaultTemplate: "Minimum link length: <<1>>",
          description: "Info string in the editor for the minimum link length setting."
          + "\nArgument <<1>> is the part's config setting of type DistanceType.",
          example: "Minimum link length: 1.22 m");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.DistanceType']/*"/>
  readonly static Message<DistanceType> MaximumLinkLengthInfo =
      new Message<DistanceType>(
          "#kasLOC_00007",
          defaultTemplate: "Maximum link length: <<1>>",
          description: "Info string in the editor for the maximum link length setting."
          + "\nArgument <<1>> is the part's config setting of type DistanceType.",
          example: "Maximum link length: 1.22 m");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.AngleType']/*"/>
  readonly static Message<AngleType> SourceJointFreedomInfo = new Message<AngleType>(
      "#kasLOC_00008",
      defaultTemplate: "Source angle limit: <<1>>",
      description: "Info string in the editor for the maximum allowed angle at the source."
      + "\nArgument <<1>> is the part's config setting of type AngleType.",
      example: "Source angle limit: 1.2°");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.AngleType']/*"/>
  readonly static Message<AngleType> TargetJointFreedomInfo = new Message<AngleType>(
      "#kasLOC_00009",
      defaultTemplate: "Target angle limit: <<1>>",
      description: "Info string in the editor for the maximum allowed angle at the target."
      + "\nArgument <<1>> is the part's config setting of type AngleType.",
      example: "Target angle limit: 1.2°");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  readonly static Message ModuleTitle = new Message(
      "#kasLOC_00010",
      defaultTemplate: "KAS Joint",
      description: "Title of the module to present in the editor details window.");
  #endregion

  #region ILinkJoint CFG properties
  /// <inheritdoc/>
  public string cfgJointName { get { return jointName; } }
  #endregion

  #region Part's config fields
  /// <summary>See <see cref="cfgJointName"/>.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string jointName = "";

  /// <summary>Breaking force for the strut connecting the two parts.</summary>
  /// <remarks>
  /// Force is in kilonewtons. If <c>0</c>, then the joint strength infinite.
  /// </remarks>
  /// <seealso cref="SetBreakForces"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float linkBreakForce;

  /// <summary>Breaking torque for the sttrut connecting the two parts.</summary>
  /// <value>
  /// Force is in kilonewtons. If <c>0</c>, then the joint strength is infinite.
  /// </value>
  /// <seealso cref="SetBreakForces"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float linkBreakTorque;

  /// <summary>
  /// Maximum allowed angle between the attach node normal and the link at the source part.
  /// </summary>
  /// <remarks>Angle is in degrees. If <c>0</c>, then the angle is not checked.</remarks>
  /// <seealso cref="CheckConstraints"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public int sourceLinkAngleLimit;

  /// <summary>
  /// Maximum allowed angle between the attach node normal and the link at the target part.
  /// </summary>
  /// <remarks>Angle is in degrees. If <c>0</c>, then the angle is not checked.</remarks>
  /// <seealso cref="CheckConstraints"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public int targetLinkAngleLimit;

  /// <summary>Minimum allowed distance between parts to establish a link.</summary>
  /// <remarks>
  /// Distance is in meters. If <c>0</c>, then no limit for the minimum value is applied.
  /// </remarks>
  /// <seealso cref="CheckConstraints"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float minLinkLength;

  /// <summary>Maximum allowed distance between parts to establish a link.</summary>
  /// <remarks>
  /// Distance is in meters. If <c>0</c>, then no limit for the minimum value is applied.
  /// </remarks>
  /// <seealso cref="CheckConstraints"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float maxLinkLength;

  /// <summary>
  /// Offset of the physical anchor at the source part relative to its link peer's node.
  /// </summary>
  /// <seealso cref="ILinkPeer.nodeTransform"/>
  /// <seealso cref="CheckConstraints"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public Vector3 anchorAtSource = Vector3.zero;

  /// <summary>
  /// Offset of the physical anchor at the target part relative to its link peer's node.
  /// </summary>
  /// <seealso cref="ILinkPeer.nodeTransform"/>
  /// <seealso cref="CheckConstraints"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public Vector3 anchorAtTarget = Vector3.zero;
  #endregion

  #region CFG/persistent fields
  /// <summary>
  /// Tells if the source and the target parts should couple when making a link between the
  /// different vessels.
  /// </summary>
  /// <seealso cref="coupleOnLinkMode"/>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField(isPersistant = true)]
  public bool coupleWhenLinked;

  /// <summary>Vessel info of the source part.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  [PersistentField("persistedSrcVesselInfo", group = StdPersistentGroups.PartPersistant)]
  protected DockedVesselInfo persistedSrcVesselInfo;

  /// <summary>Vessel info of the target part.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  [PersistentField("persistedTgtVesselInfo", group = StdPersistentGroups.PartPersistant)]
  protected DockedVesselInfo persistedTgtVesselInfo;

  /// <summary>Length at the moment of creating the joint.</summary>
  /// <remarks>
  /// This value is used to restore the link state, but only if it's greater than zero. If it's
  /// less, then the implementation should decide which length to set when the joint is created.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  [KSPField(isPersistant = true)]
  public float persistedLinkLength = -1.0f;
  #endregion

  #region ILinkJoint properties
  /// <inheritdoc/>
  public ILinkSource linkSource { get; private set; }

  /// <inheritdoc/>
  public ILinkTarget linkTarget { get; private set; }

  /// <inheritdoc/>
  public bool coupleOnLinkMode {
    get { return coupleWhenLinked; }
    private set { coupleWhenLinked = value; }
  }

  /// <inheritdoc/>
  public bool isLinked { get; private set; }
  #endregion

  #region Inheritable properties
  /// <summary>
  /// Length at the moment of creating the joint, or a whatever length which deserves restoring
  /// "as-is" on the scene load.
  /// </summary>
  /// <value>
  /// Distance in meters or <c>null</c>. The <c>null</c> value means that this joint doesn't care
  /// about the particular length in the current state, and it's up to the implementation.
  /// </value>
  protected float? originalLength {
    get { return persistedLinkLength < 0 ? (float?) null : persistedLinkLength; }
  }

  /// <summary>Tells if the parts of the link are coupled in the vessels hierarchy.</summary>
  /// <value>
  /// <c>true</c> if either the source part is coupled to the target, or the vise versa.
  /// </value>
  protected bool isCoupled {
    get {
      return isLinked && CheckCoupled(linkSource, linkTarget);
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

  /// <summary>The physical joints that were created by the custom code.</summary>
  /// <remarks>
  /// When the parts are coupled, there can be a stock joint created. It will not be listed here.
  /// </remarks>
  /// <value>The list of the joints or <c>null</c> if there are none.</value>
  /// <seealso cref="joints"/>
  /// <seealso cref="SetCustomJoints"/>
  /// <seealso cref="partJoint"/>
  /// <seealso cref="CleanupPhysXJoints"/>
  protected List<ConfigurableJoint> customJoints { get { return _customJoints; } }
  readonly List<ConfigurableJoint> _customJoints = new List<ConfigurableJoint>();

  /// <summary>The objects that were used by the custom joints.</summary>
  /// <remarks>These objects will be destoyed on the joints clean up.</remarks>
  /// <seealso cref="SetCustomJoints"/>
  /// <seealso cref="CleanupPhysXJoints"/>
  protected List<Object> customExtraObjects { get { return _customObjects; } }
  readonly List<Object> _customObjects = new List<Object>();
  #endregion

  #region Local members
  /// <summary>Set when the coupled parts are decoupled by a self-triggered event.</summary>
  protected bool selfDecoupledAction { get; private set; }
  #endregion

  #region IActivateOnDecouple implementation
  /// <inheritdoc/>
  public virtual void DecoupleAction(string nodeName, bool weDecouple) {
    if (isLinked && !selfDecoupledAction
        && linkSource.coupleNode != null && linkSource.coupleNode.id == nodeName) {
      // Do the link cleanup.
      RestorePartialVesselInfo(linkSource, linkTarget, weDecouple);
      MaybeBreakLink(linkSource, linkTarget);
    }
  }
  #endregion

  #region IJointEventsListener implementation
  /// <inheritdoc/>
  public virtual void OnJointBreak(float breakForce) {
    HostedDebugLog.Fine(this, "Joint is broken with force: {0}", breakForce);
    Part parentPart = null;
    Vector3 relPos = Vector3.zero;
    Quaternion relRot = Quaternion.identity;
    if (part.parent != linkTarget.part) {
      // Calculate relative position and rotation of the part to properly restore the coupling.
      parentPart = part.parent;
      var root = vessel.rootPart.transform;
      var thisPartPos = root.TransformPoint(part.orgPos);
      var thisPartRot = root.rotation * part.orgRot;
      var parentPartPos = root.TransformPoint(parentPart.orgPos);
      var parentPartRot = root.rotation * parentPart.orgRot;
      relPos = parentPartRot.Inverse() * (thisPartPos - parentPartPos);
      relRot = parentPartRot.Inverse() * thisPartRot;
    }
    
    // The break event is sent for *any* joint on the game object that got broken. However, it may
    // not be our link's joint. To figure it out, wait till the engine has cleared the object. 
    AsyncCall.CallOnFixedUpdate(this, () => {
      if (isLinked && customJoints.Any(x => x == null)) {
        if (parentPart != null) {
          HostedDebugLog.Fine(this, "Restore coupling with: {0}", parentPart);
          part.transform.position =
              parentPart.transform.position + parentPart.transform.rotation * relPos;
          part.transform.rotation = parentPart.transform.rotation * relRot;
          part.Couple(parentPart);
        }
        HostedDebugLog.Info(this, "KAS joint is broken, unlink the parts");
        linkSource.BreakCurrentLink(LinkActorType.Physics);
      }
    });
  }
  #endregion

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    ConfigAccessor.CopyPartConfigFromPrefab(this);
    base.OnAwake();
    GameEvents.onVesselRename.Add(OnVesselRename);
    LocalizeModule();
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    ConfigAccessor.ReadPartConfig(this, cfgNode: node);
    ConfigAccessor.ReadFieldsFromNode(node, GetType(), this, StdPersistentGroups.PartPersistant);
    base.OnLoad(node);
  }

  /// <inheritdoc/>
  public override void OnSave(ConfigNode node) {
    base.OnSave(node);
    ConfigAccessor.WriteFieldsIntoNode(node, GetType(), this, StdPersistentGroups.PartPersistant);
  }
  #endregion

  #region IsDestroyable implementation
  /// <inheritdoc/>
  public virtual void OnDestroy() {
    GameEvents.onVesselRename.Remove(OnVesselRename);
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
    if (!CheckCoupled(source, target)) {
      var errors = CheckConstraints(source, target);
      if (errors.Length > 0) {
        HostedDebugLog.Error(this, "Cannot create joint:\n{0}", DbgFormatter.C2S(errors));
        return false;
      }
    } else {
      HostedDebugLog.Fine(this, "The parts are coupled. Skip the constraints check");
    }
    linkSource = source;
    linkTarget = target;
    if (!originalLength.HasValue) {
      SetOrigianlLength(Vector3.Distance(
          GetSourcePhysicalAnchor(source), GetTargetPhysicalAnchor(source, target)));
    }
    isLinked = true;
    // If the parts are already coupled at this moment, then the mode must be set as such.      
    coupleOnLinkMode |= isCoupled;
    // Ensure the coupling can be done. 
    coupleOnLinkMode &= linkSource.coupleNode != null && linkTarget.coupleNode != null;
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
        coupleOnLinkMode = false;
      } else {
        DetachParts();
      }
    }
    SetCustomJoints(null);
    SetOrigianlLength(null);
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
        joints.ForEach(j => SetBreakForces(j, linkBreakForce, linkBreakTorque));
      }
    }
  }

  /// <inheritdoc/>
  public virtual string[] CheckConstraints(ILinkSource source, ILinkTarget target) {
    var errors = new[] {
        CheckLengthLimit(source, target),
        CheckAngleLimitAtSource(source, target),
        CheckAngleLimitAtTarget(source, target),
    };
    return errors.Where(x => x != null).ToArray();
  }

  /// <inheritdoc/>
  public virtual bool SetCoupleOnLinkMode(bool isCoupleOnLink) {
    if (!isLinked) {
      coupleOnLinkMode = isCoupleOnLink;
      HostedDebugLog.Fine(
          this, "Coupling mode updated in a non-linked module: {0}", isCoupleOnLink);
      return true;
    }
    if (isCoupleOnLink && (linkSource.coupleNode == null || linkTarget.coupleNode == null)) {
      HostedDebugLog.Error(this, "Cannot couple due to source or target doesn't support it");
      coupleOnLinkMode = false;
      return false;
    }
    if (isCoupleOnLink && linkSource.part.vessel != linkTarget.part.vessel) {
      // Couple the parts, and drop the other link(s).
      DetachParts();
      coupleOnLinkMode = isCoupleOnLink;
      CoupleParts();
    } else if (!isCoupleOnLink && isCoupled) {
      // Decouple the parts, and make the non-coupling link(s).
      DecoupleParts();
      coupleOnLinkMode = isCoupleOnLink;
      AttachParts();
    } else {
      coupleOnLinkMode = isCoupleOnLink;  // Simply change the mode.
    }
    return true;
  }
  #endregion

  #region IsLocalizableModule implementation
  /// <inheritdoc/>
  public virtual void LocalizeModule() {
    LocalizationLoader.LoadItemsInModule(this);
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
  /// <summary>Sets the original length of the joint.</summary>
  /// <remarks>
  /// This length is a base for many manipulations with the joint. It should only be changed when
  /// the new value is a length which this joint is going to maintain the long-term (e.g. between
  /// the scenes).
  /// </remarks>
  /// <param name="newLength">
  /// The new length in meters. Can be <c>null</c> if the implementation is allowed to decide what
  /// length to set on the joint creation.
  /// </param>
  /// <seealso cref="originalLength"/>
  protected void SetOrigianlLength(float? newLength) {
    persistedLinkLength = newLength.HasValue ? newLength.Value : -1;
  }

  /// <summary>Creates the actual PhysX joints between the rigid objects.</summary>
  /// <remarks>
  /// <para>
  /// The recommended approach is to add all the created PhysX joints into the
  /// <see cref="customJoints"/> collection. In this case the other descendants will be able to
  /// access the joints if they need to. Moreover, the default cleanup logic will work just fine
  /// with such joints.
  /// </para>
  /// <para>
  /// When defining a custom setup for the joints, the module must check if the parts are coupled.
  /// If they are, then there is a stock <see cref="partJoint"/> which controls the physical joint.
  /// In most cases, this joint needs to be dropped (via <c>PartJoint.DestroyJoint</c>) to not
  /// interfere with the stock logic.
  /// </para>
  /// </remarks>
  /// <seealso cref="SetCustomJoints"/>
  /// <seealso cref="CleanupPhysXJoints"/>
  protected abstract void SetupPhysXJoints();

  /// <summary>Drops and cleans up all the PhysX joints between the rigid objects.</summary>
  /// <remarks>
  /// <para>
  /// The default implementation simply destroys the joints from the <see cref="customJoints"/>
  /// collection. In most cases it's enough to update the physics in the game. However, if module
  /// manages some other objects or components, then this method is the right place to do the
  /// cleanup.
  /// </para>
  /// <para>
  /// IMPORTANT! The <see cref="SetCustomJoints"/> method cleans up all the joints by invoking this
  /// method. If there are extra objects that the child class needs to cleanup, then they must
  /// <i>not</i> get initialized before the new joints are set. Otherwise, the newly created objects
  /// may get destroyed. The suggested way of cleaning up the Unity objects is adding them into the
  /// <see cref="customExtraObjects"/> collection.  
  /// </para>
  /// </remarks>
  /// <seealso cref="customJoints"/>
  /// <seealso cref="customExtraObjects"/>
  protected virtual void CleanupPhysXJoints() {
    if (customJoints.Count > 0) {
      HostedDebugLog.Fine(this, "Drop {0} joint(s) to: {1}", customJoints.Count, linkTarget);
      customJoints.ForEach(Object.Destroy);
      customJoints.Clear();
      customExtraObjects.ForEach(Object.Destroy);
      customExtraObjects.Clear();
    }
  }

  /// <summary>Returns an anchor for the physical joint at the target part.</summary>
  /// <remarks>
  /// The anchor will be calculated in the source's part scale, and the target's model scale will
  /// be ignored.
  /// </remarks>
  /// <param name="source">The source of the link.</param>
  /// <param name="target">The target of the link.</param>
  /// <returns>The position in the world coordinates.</returns>
  protected Vector3 GetTargetPhysicalAnchor(ILinkSource source, ILinkTarget target) {
    var scale = source.nodeTransform.lossyScale;
    if (Mathf.Abs(scale.x - scale.y) > 1e-05 || Mathf.Abs(scale.x - scale.z) > 1e-05) {
      HostedDebugLog.Error(this, "Uneven scale on the source part is not supported: {0}",
                           DbgFormatter.Vector(scale));
    }
    return target.nodeTransform.position
        + target.nodeTransform.rotation * (anchorAtTarget * scale.x);
  }

  /// <summary>Returns an anchor for the physical joint at the source part.</summary>
  /// <remarks>The anchor will be affected by the part's model scale.</remarks>
  /// <param name="source">The source of the link.</param>
  /// <returns>The position in the world coordinates.</returns>
  protected Vector3 GetSourcePhysicalAnchor(ILinkSource source) {
    return source.nodeTransform.TransformPoint(anchorAtSource);
  }

  /// <summary>Sets a new custom joints set.</summary>
  /// <remarks>
  /// If there are other custom joints existing, they will be cleaned up. This method triggers
  /// <see cref="CleanupPhysXJoints"/>, so keep it in mind when setting up the custom joints.
  /// </remarks>
  /// <param name="joints">
  /// The new joints. If <c>null</c>, then the old joints will be cleaned up and no new joints will
  /// be added.
  /// </param>
  /// <param name="extraObjects">
  /// The Unity objects that need to be destoyed <i>after</i> the joints are cleaned up. They can be
  /// anything.
  /// </param>
  /// <seealso cref="customExtraObjects"/>
  /// <seealso cref="customJoints"/>
  protected void SetCustomJoints(IEnumerable<ConfigurableJoint> joints,
                                 IEnumerable<Object> extraObjects = null) {
    CleanupPhysXJoints();
    if (joints != null) {
      customJoints.AddRange(joints);
    }
    if (extraObjects != null) {
      customExtraObjects.AddRange(extraObjects);
    }
  }
  #endregion

  #region Utility methods
  /// <summary>
  /// Setups up the joint break force and torque. It takes into account the values from the config.
  /// </summary>
  /// <param name="joint">The joint to set forces for.</param>
  /// <param name="force">The breaking force. If not set, then the config value is used.</param>
  /// <param name="torque">The breaking torque. If not set, then the config value is used.</param>
  /// <seealso cref="linkBreakForce"/>
  /// <seealso cref="linkBreakTorque"/>
  protected void SetBreakForces(Joint joint, float? force = null, float? torque = null) {
    var realForce = force ?? linkBreakForce;
    joint.breakForce = Mathf.Approximately(realForce, 0) ? float.PositiveInfinity : realForce;
    var realTorque = torque ?? linkBreakTorque;
    joint.breakTorque = Mathf.Approximately(realTorque, 0) ? float.PositiveInfinity : realTorque;
  }

  /// <summary>Checks if the link's length is within the limits.</summary>
  /// <remarks>This method takes into consideration the anchor settings.</remarks>
  /// <param name="source">The possible source of the link.</param>
  /// <param name="target">The possible target of the link.</param>
  /// <returns>An error message if link length is over limit or <c>null</c> otherwise.</returns>
  /// <seealso cref="anchorAtSource"/>
  /// <seealso cref="anchorAtTarget"/>
  protected string CheckLengthLimit(ILinkSource source, ILinkTarget target) {
    var length = Vector3.Distance(
        GetSourcePhysicalAnchor(source), GetTargetPhysicalAnchor(source, target));
    if (maxLinkLength > 0 && length > maxLinkLength) {
      return MaxLengthLimitReachedMsg.Format(length, maxLinkLength);
    }
    if (minLinkLength > 0 && length < minLinkLength) {
      return MinLengthLimitReachedMsg.Format(length, minLinkLength);
    }
    return null;
  }

  /// <summary>Checks if the link's angle at the source joint is within the limits.</summary>
  /// <remarks>This method takes into consideration the anchor settings.</remarks>
  /// <param name="source">The possible source of the link.</param>
  /// <param name="target">The possible target of the link.</param>
  /// <returns>An error message if angle is over limit or <c>null</c> otherwise.</returns>
  /// <seealso cref="anchorAtSource"/>
  /// <seealso cref="anchorAtTarget"/>
  protected string CheckAngleLimitAtSource(ILinkSource source, ILinkTarget target) {
    var linkVector = GetTargetPhysicalAnchor(source, target) - GetSourcePhysicalAnchor(source);
    var angle = Vector3.Angle(source.nodeTransform.rotation * Vector3.forward, linkVector);
    return sourceLinkAngleLimit > 0 && angle > sourceLinkAngleLimit
        ? SourceNodeAngleLimitReachedMsg.Format(angle, sourceLinkAngleLimit)
        : null;
  }

  /// <summary>Checks if the link's angle at the target joint is within the limits.</summary>
  /// <remarks>This method takes into consideration the anchor settings.</remarks>
  /// <param name="source">The possible source of the link.</param>
  /// <param name="target">The possible target of the link.</param>
  /// <returns>An error message if the angle is over limit or <c>null</c> otherwise.</returns>
  /// <seealso cref="anchorAtSource"/>
  /// <seealso cref="anchorAtTarget"/>
  protected string CheckAngleLimitAtTarget(ILinkSource source, ILinkTarget target) {
    var linkVector = GetSourcePhysicalAnchor(source) - GetTargetPhysicalAnchor(source, target);
    var angle = Vector3.Angle(target.nodeTransform.rotation * Vector3.forward, linkVector);
    return targetLinkAngleLimit > 0 && angle > targetLinkAngleLimit
        ? TargetNodeAngleLimitReachedMsg.Format(angle, targetLinkAngleLimit)
        : null;
  }
  #endregion

  #region Local utility methods
  /// <summary>Couples the source and the target parts merging them into a single vessel.</summary>
  /// <remarks>
  /// It's OK to call this method if the parts are already coupled. It's a normal way to have the
  /// attach nodes created on the vessel load.
  /// </remarks>
  /// <seealso cref="DecoupleParts"/>
  void CoupleParts() {
    if (isCoupled) {
      // If the parts are already coupled, then refresh the state and update the joints.
      if (persistedSrcVesselInfo == null) {
        HostedDebugLog.Fine(this, "Update link source vessel info to: {0}", vessel);
        persistedSrcVesselInfo = GetVesselInfo(vessel);
      }
      if (persistedTgtVesselInfo == null) {
        HostedDebugLog.Fine(this, "Update link target vessel info to: {0}", vessel);
        persistedTgtVesselInfo = GetVesselInfo(vessel);
      }
      SetupPhysXJoints();
      return;
    }
    if (linkSource.part.vessel == linkTarget.part.vessel) {
      // If the parts belong to the same vessel, but are not coupled, then update the joints.
      HostedDebugLog.Fine(this, "Already coupled, skipping: {0} <=> {1}", linkSource, linkTarget);
      SetupPhysXJoints();
      return;
    }
    // Remember the vessel info to restore it on the decoupling. And do the couple!
    persistedSrcVesselInfo = GetVesselInfo(linkSource.part.vessel);
    persistedTgtVesselInfo = GetVesselInfo(linkTarget.part.vessel);
    KASAPI.LinkUtils.CoupleParts(
        linkSource.coupleNode, linkTarget.coupleNode, toDominantVessel: true);
    SetupPhysXJoints();
  }

  /// <summary>Creates a physical link between the source and the target parts.</summary>
  /// <seealso cref="DetachParts"/>
  void AttachParts() {
    SetupPhysXJoints();
  }

  /// <summary>
  /// Decouples the source and the target parts turning them into the separate vessels.
  /// </summary>
  /// <seealso cref="CoupleParts"/>
  void DecoupleParts() {
    if (!isCoupled) {
      HostedDebugLog.Error(this, "Cannot decouple - bad link/part state");
      return;
    }
    selfDecoupledAction = true;  // Protect the action to not let the link auto-broken.
    KASAPI.LinkUtils.DecoupleParts(
        linkSource.part, linkTarget.part,
        vesselInfo1: persistedSrcVesselInfo, vesselInfo2: persistedTgtVesselInfo);
    selfDecoupledAction = false;
    persistedSrcVesselInfo = null;
    persistedTgtVesselInfo = null;
    DelegateCouplingRole(linkTarget.part);
    SetCustomJoints(null);
  }

  /// <summary>Destroys the physical link between the source and the target parts.</summary>
  /// <seealso cref="AttachParts"/>
  void DetachParts() {
    SetCustomJoints(null);
  }

  /// <summary>Checks if the peer parts are coupled in the vessel hierarchy.</summary>
  /// <param name="source">The first peer of the link.</param>
  /// <param name="target">The other peer of the link.</param>
  /// <returns><c>true</c> if the peers are coupled.</returns>
  static bool CheckCoupled(ILinkPeer source, ILinkPeer target) {
    return source.part.parent == target.part || target.part.parent == source.part;
  }

  /// <summary>Reacts on the vessel name change and updates the vessel infos.</summary>
  void OnVesselRename(GameEvents.HostedFromToAction<Vessel, string> action) {
    if (!isLinked || action.host != vessel) {
      return;  // Nothing to do.
    }
    if (persistedSrcVesselInfo.rootPartUId == action.host.rootPart.flightID) {
      persistedSrcVesselInfo.name = action.host.vesselName;
      persistedSrcVesselInfo.vesselType = action.host.vesselType;
      HostedDebugLog.Fine(this, "Update source vessel info to: name={0}, type={1}",
                          persistedSrcVesselInfo.name, persistedSrcVesselInfo.vesselType);
    }
    if (persistedTgtVesselInfo.rootPartUId == action.host.rootPart.flightID) {
      persistedTgtVesselInfo.name = action.host.vesselName;
      persistedTgtVesselInfo.vesselType = action.host.vesselType;
      HostedDebugLog.Fine(this, "Update target vessel info to: name={0}, type={1}",
                          persistedTgtVesselInfo.name, persistedTgtVesselInfo.vesselType);
    }
  }

  /// <summary>Cleans up the attach nodes and, optionally, breaks the link.</summary>
  /// <remarks>
  /// The actual changes are delyed till the end of frame. So it's safe to call this method from an
  /// event handler.
  /// </remarks>
  /// <param name="source">The link source at the moemnt of cleanup.</param>
  /// <param name="target">The link target at the moment of cleanup.</param>
  void MaybeBreakLink(ILinkSource source, ILinkTarget target) {
    // Delay the nodes cleanup to let the other logic work smoothly. Copy the properties since
    // they will be null'ed on the link destruction.
    AsyncCall.CallOnEndOfFrame(this, () => {
      if (isLinked) {
        source.BreakCurrentLink(LinkActorType.Physics);
      }
    });
  }

  /// <summary>Updates the vessel info on the part if it has the relevant module.</summary>
  /// <param name="v">The vessel to capture the info for.</param>
  static DockedVesselInfo GetVesselInfo(Vessel v) {
    var vesselInfo = new DockedVesselInfo();
    vesselInfo.name = v.vesselName;
    vesselInfo.vesselType = v.vesselType;
    vesselInfo.rootPartUId = v.rootPart.flightID;
    return vesselInfo;
  }

  /// <summary>Restores the name and type of the vessels of the former coupled parts.</summary>
  /// <remarks>
  /// The source and target parts need to be separated, but the logical link still need to exist.
  /// On restore the vessel info will be cleared on the module. Alas, when the link is broken
  /// extrenally, the root vessel part cannot be properly restored.
  /// </remarks>
  void RestorePartialVesselInfo(ILinkSource source, ILinkTarget target, bool weDecouple) {
    AsyncCall.CallOnEndOfFrame(this, () => {
      var vesselInfo = weDecouple ? persistedSrcVesselInfo : persistedTgtVesselInfo;
      var childPart = weDecouple ? source.part : target.part;
      if (childPart.vessel.vesselType != vesselInfo.vesselType
          || childPart.vessel.vesselName != vesselInfo.name) {
        HostedDebugLog.Warning(this, "Partially restoring vessel info on {0}: type={1}, name={2}",
                               childPart, vesselInfo.vesselType, vesselInfo.name);
        childPart.vessel.vesselType = vesselInfo.vesselType;
        childPart.vessel.vesselName = vesselInfo.name;
      }
      persistedSrcVesselInfo = null;
      persistedTgtVesselInfo = null;
    });
  }

  /// <summary>
  /// Goes thru the parts on the source and target vessels, and tries to restore the coupling
  /// between the parts.
  /// </summary>
  /// <remarks>
  /// Any linking module on the source or the target vessel, which is linked and in the docking
  /// mode, will be attempted to use to restore the vessels coupling. This work will be done at the
  /// end of frame to let the other logic to cleanup.
  /// </remarks>
  /// <param name="tgtPart">
  /// The former target part that was holding the coupling with this part.
  /// </param>
  void DelegateCouplingRole(Part tgtPart) {
    AsyncCall.CallOnEndOfFrame(this, () => {
      var candidates = new List<ILinkJoint>()
          .Concat(vessel.parts
              .SelectMany(p => p.Modules.OfType<ILinkJoint>())
              .Where(j => !ReferenceEquals(j, this) && j.coupleOnLinkMode && j.isLinked
                          && j.linkTarget.part.vessel == tgtPart.vessel))
          .Concat(tgtPart.vessel.parts
              .SelectMany(p => p.Modules.OfType<ILinkJoint>())
              .Where(j => j.coupleOnLinkMode && j.isLinked && j.linkTarget.part.vessel == vessel));
      foreach (var joint in candidates) {
        HostedDebugLog.Fine(this, "Trying to couple via: {0}", joint);
        if (joint.SetCoupleOnLinkMode(true)) {
          HostedDebugLog.Info(this, "The coupling role is delegated to: {0}", joint);
          return;
        }
      }
      if (candidates.Any()) {
        HostedDebugLog.Warning(this, "None of the found candidates took the coupling role");
      }
    });
  }
  #endregion
}

}  // namespace
