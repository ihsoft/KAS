// Kerbal Attachment System
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv2;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.LogUtils;
using KSPDev.ProcessingUtils;
using System.Text;
using UnityEngine;

namespace KAS {

/// <summary>Module that controls a physical cable joint on a KAS part.</summary>
/// <remarks>
/// When creating a link, the cable's <see cref="deployedCableLength"/> is set to the actual
/// distance between the objects at the moment of creation. The colliders on the objects are enabled
/// by default, i.e. the source and the target can collide.
/// </remarks>
//  Next localization ID: #kasLOC_09002.
public class KASJointCableBase : AbstractJoint,
    // KSP interfaces.
    IJointLockState,
    // KAS interfaces.
    ILinkCableJoint {

  #region Localizable GUI strings.
  /// <include file="../SpecialDocTags.xml" path="Tags/Message1/*"/>
  /// <include file="../KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.ForceType']/*"/>
  static readonly Message<ForceType> CableSpringStrengthInfo = new Message<ForceType>(
      "#kasLOC_09000",
      defaultTemplate: "Spring force: <<1>>",
      description: "Info string in the editor for the cable spring force setting."
      + "\nArgument <<1>> is the force of type ForceType.",
      example: "Spring force: 1.2 kN");

  /// <include file="../SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message ModuleTitle = new Message(
      "#kasLOC_09001",
      defaultTemplate: "KAS Cable",
      description: "Title of the module to present in the editor details window.");
  #endregion

  #region Part's config fields
  /// <summary>Spring force of the cable which connects the two parts.</summary>
  /// <remarks>
  /// It's a force per meter of the strected distance to keep the objects distance below the maximum
  /// distance. The force is measured in kilonewtons.
  /// </remarks>
  /// <include file="..//SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Cable spring force")]
  public float cableSpringForce;

  /// <summary>Damper force to apply to stop the oscillations.</summary>
  /// <remarks>The force is measured in kilonewtons.</remarks>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Cable spring damper")]
  public float cableSpringDamper = 1f;
  #endregion

  #region IJointLockState implemenation
  /// <inheritdoc/>
  public bool IsJointUnlocked() {
    return true;  // Cables are always unlocked!
  }
  #endregion

  #region ILinkCableJoint CFG properties
  /// <inheritdoc/>
  public float cfgMaxCableLength { get { return maxLinkLength; } }
  #endregion

  #region ILinkCableJoint properties
  /// <inheritdoc/>
  public Rigidbody headRb { get; private set; }

  /// <inheritdoc/>
  public float deployedCableLength {
    get { return cableJoint != null ? cableJoint.linearLimit.limit : 0; }
  }

  /// <inheritdoc/>
  public float realCableLength {
    get {
      if (cableJoint != null) {
        var ownerRb = cableJoint.gameObject.GetComponent<Rigidbody>();
        return Vector3.Distance(
            ownerRb.transform.TransformPoint(cableJoint.anchor),
            cableJoint.connectedBody.transform.TransformPoint(cableJoint.connectedAnchor));
      }
      return 0;
    }
  }

  /// <inheritdoc/>
  public bool isLockedWhenCoupled { get; private set; }
  #endregion

  #region Inheritable properties
  /// <summary>Tells if the physical head is started and active.</summary>
  /// <value>The status of the physical head.</value>
  protected bool isHeadStarted { get { return headSource != null; } }

  /// <summary>Physical joint object that connects source to the target.</summary>
  /// <value>The PhysX joint that connects the parts.</value>
  protected ConfigurableJoint cableJoint { get; private set; }

  /// <summary>Source that owns the physical head.</summary>
  /// <value>The source, or <c>null</c> if the head is not started.</value>
  /// <seealso cref="ILinkSource"/>
  protected ILinkSource headSource { get; private set; }
  #endregion

  #region AbstractJoint overrides
  /// <inheritdoc/>
  protected override void SetupPhysXJoints() {
    if (isHeadStarted) {
      HostedDebugLog.Warning(this, "A physical head is running. Stop it before the link!");
      StopPhysicalHead();
    }
    var needStockJoint = isCoupled && isLockedWhenCoupled;
    if (needStockJoint && partJoint == null) {
      if (linkTarget.part.parent == linkSource.part) {
        HostedDebugLog.Fine(this, "Create a stock joint: from={0}, to={1}", linkTarget, linkSource);
        linkTarget.part.CreateAttachJoint(AttachModes.STACK);
      } else if (linkSource.part.parent == linkTarget.part) {
        HostedDebugLog.Fine(this, "Create a stock joint: from={0}, to={1}", linkSource, linkTarget);
        linkSource.part.CreateAttachJoint(AttachModes.STACK);
      } else {
        HostedDebugLog.Error(
            this, "Cannot create stock joint: {0} <=> {1}", linkSource, linkTarget);
        needStockJoint = false;
      }
    } else if (!needStockJoint && partJoint != null) {
      HostedDebugLog.Fine(
          this, "Drop stock joint: to={0}, isLockedWhenDocked={1}, isCoupled={2}",
          partJoint.Child, isLockedWhenCoupled, isCoupled);
      partJoint.DestroyJoint();
      partJoint.Child.attachJoint = null;
    }
    if (!needStockJoint) {
      CreateDistanceJoint(
          linkSource, linkTarget.part.Rigidbody, GetTargetPhysicalAnchor(linkSource, linkTarget));
    }
  }

  /// <inheritdoc/>
  protected override void CleanupPhysXJoints() {
    base.CleanupPhysXJoints();
    cableJoint = null;
  }
  #endregion

  #region ILinkCableJoint implementation
  /// <inheritdoc/>
  public virtual void StartPhysicalHead(ILinkSource source, Transform headObjAnchor) {
    headRb = headObjAnchor.GetComponentInParent<Rigidbody>();
    if (isHeadStarted || isLinked || headRb == null) {
      HostedDebugLog.Error(this,
          "Bad link state for the physical head start: isLinked={0}, isHeadStarted={1}, hasRb={2}",
          isLinked, isHeadStarted, headRb != null);
      return;
    }
    headSource = source;

    // Attach the head to the source.
    CreateDistanceJoint(source, headRb, headObjAnchor.position);
    SetOrigianlLength(deployedCableLength);
  }

  /// <inheritdoc/>
  public virtual void StopPhysicalHead() {
    headRb = null;
    headSource = null;
    Destroy(cableJoint);
    cableJoint = null;
    SetOrigianlLength(null);
  }

  /// <inheritdoc/>
  public virtual void SetCableLength(float length) {
    if (cableJoint == null) {
      SetOrigianlLength(null);  // Just in case.
      return;
    }
    if (float.IsPositiveInfinity(length)) {
      length = cfgMaxCableLength;
    } else if (float.IsNegativeInfinity(length)) {
      length = Mathf.Min(realCableLength, deployedCableLength);
    }
    ArgumentGuard.InRange(length, "length", 0, cfgMaxCableLength, context: this);
    SetOrigianlLength(length);
    cableJoint.linearLimit = new SoftJointLimit() { limit = length };
  }

  /// <inheritdoc/>
  public void SetLockedOnCouple(bool mode) {
    if (isLinked) {
      if (isLockedWhenCoupled != mode) {
        isLockedWhenCoupled = mode;
        HostedDebugLog.Fine(this, "Change locked on coupled part: {0}", isLockedWhenCoupled);
        CleanupPhysXJoints();
        SetupPhysXJoints();
      }
    } else {
      isLockedWhenCoupled = mode;
      HostedDebugLog.Fine(this, "Set locked on couple mode in a non-linked module: {0}", mode);
    }
  }
  #endregion

  #region IModuleInfo implementation
  /// <inheritdoc/>
  public override string GetInfo() {
    var sb = new StringBuilder(base.GetInfo());
    sb.AppendLine(CableSpringStrengthInfo.Format(cableSpringForce));
    return sb.ToString();
  }

  /// <inheritdoc/>
  public override string GetModuleTitle() {
    return ModuleTitle;
  }
  #endregion

  #region Utility methods
  /// <summary>
  /// Creates a distance joint between the source and an arbitrary physical object.   
  /// </summary>
  /// <remarks>It sets the maximum cable length to the persisted value. Even if it's zero!</remarks>
  /// <param name="source">The source of the link.</param>
  /// <param name="tgtRb">The rigidbody of the physical object.</param>
  /// <param name="tgtAnchor">The anchor at the physical object in world coordinates.</param>
  void CreateDistanceJoint(ILinkSource source, Rigidbody tgtRb, Vector3 tgtAnchor) {
    var distanceLimit = originalLength
        ?? Vector3.Distance(GetSourcePhysicalAnchor(source), tgtAnchor);
    var joint = source.part.gameObject.AddComponent<ConfigurableJoint>();
    KASAPI.JointUtils.ResetJoint(joint);
    if (distanceLimit < 0.001f) {
      // Reset the distance if it's below the KSP distance resolution.
      HostedDebugLog.Fine(this, "Reset joint to zero: distance={0}", distanceLimit);
      distanceLimit = 0;
    }

    KASAPI.JointUtils.SetupDistanceJoint(
        joint,
        springForce: cableSpringForce, springDamper: cableSpringDamper,
        maxDistance: distanceLimit);
    joint.autoConfigureConnectedAnchor = false;
    joint.anchor = source.part.Rigidbody.transform.InverseTransformPoint(
        GetSourcePhysicalAnchor(source));
    joint.connectedBody = tgtRb;
    joint.connectedAnchor = tgtRb.transform.InverseTransformPoint(tgtAnchor);
    SetBreakForces(joint);
    SetCustomJoints(new[] {joint});
    cableJoint = joint;
  }
  #endregion
}

}  // namespace
