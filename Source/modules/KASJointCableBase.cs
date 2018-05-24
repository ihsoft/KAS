// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.GUIUtils;
using KSPDev.KSPInterfaces;
using KSPDev.LogUtils;
using System.Collections.Generic;
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
    IModuleInfo,
    // KAS interfaces.
    ILinkCableJoint,
    // KSPDev syntax sugar interfaces.
    IKSPDevModuleInfo {

  #region Localizable GUI strings.
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.ForceType']/*"/>
  readonly static Message<ForceType> CableSpringStrengthInfo = new Message<ForceType>(
      "#kasLOC_09000",
      defaultTemplate: "Spring force: <<1>>",
      description: "Info string in the editor for the cable spring force setting. The argument is"
      + " of type ForceType.",
      example: "Cable break force: 1.2 kN");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  readonly static Message ModuleTitle = new Message(
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
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float cableSpringForce;

  /// <summary>Damper force to apply to stop the oscillations.</summary>
  /// <remarks>The force is measured in kilonewtons.</remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float cableSpringDamper = 1f;

  /// <summary>
  /// Tells if the stock joint must be kept in case of the parts have coupled at the zero distance.
  /// </summary>
  /// <remarks>
  /// Since the the stock joint is rigid, it's always destroyed by this module. However, in some
  /// cases the parts coupled at zero distance (docked) need to stay fixed. Set this setting to
  /// <c>true</c> to allow this behavior.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public bool allowDockingAtZeroDistance;
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

  /// <summary>Head's transform at which the cable is attached.</summary>
  /// <value>The anchor of the physical head, or <c>null</c> if the head is not started.</value>
  protected Transform headPhysicalAnchor { get; private set; }
  #endregion

  #region AbstractJoint overrides
  /// <inheritdoc/>
  protected override void SetupPhysXJoints() {
    if (isHeadStarted) {
      HostedDebugLog.Warning(this, "A physical head is running. Stop it before the link!");
      StopPhysicalHead();
    }
    CreateDistanceJoint(linkSource, linkTarget.part.Rigidbody,
                        GetTargetPhysicalAnchor(linkSource, linkTarget),
                        originalLength);
    if (partJoint != null
        && (!allowDockingAtZeroDistance || Mathf.Approximately(realCableLength, 0))) {
      HostedDebugLog.Fine(this, "Dropping the stock joint to: {0}", partJoint.Child);
      partJoint.DestroyJoint();
      partJoint.Child.attachJoint = null;
    }
  }

  /// <inheritdoc/>
  protected override void CleanupPhysXJoints() {
    base.CleanupPhysXJoints();
    cableJoint = null;
  }

  /// <inheritdoc/>
  public override bool SetCoupleOnLinkMode(bool isCoupleOnLink) {
    var oldLimit = deployedCableLength;
    var res = base.SetCoupleOnLinkMode(isCoupleOnLink);if (res) {
      SetCableLength(oldLimit);  // Keep the same deployed length.
    }
    return res;
  }
  #endregion

  #region ILinkCableJoint implementation
  /// <inheritdoc/>
  public virtual void StartPhysicalHead(ILinkSource source, Transform headObjAnchor) {
    //FIXME: add the physical head module here.
    headRb = headObjAnchor.GetComponentInParent<Rigidbody>();
    if (isHeadStarted || isLinked || headRb == null) {
      HostedDebugLog.Error(this,
          "Bad link state for the physical head start: isLinked={0}, isHeadStarted={1}, hasRb=[2}",
          isLinked, isHeadStarted, headRb != null);
      return;
    }
    headSource = source;
    headPhysicalAnchor = headObjAnchor;

    // Attach the head to the source.
    CreateDistanceJoint(source, headRb,
                        headObjAnchor.position,
                        Vector3.Distance(GetSourcePhysicalAnchor(source), headObjAnchor.position));
    SetCableLength(float.NegativeInfinity);
  }

  /// <inheritdoc/>
  public virtual void StopPhysicalHead() {
    headRb = null;
    headSource = null;
    headPhysicalAnchor = null;
    DestroyImmediate(cableJoint);
    cableJoint = null;
  }

  /// <inheritdoc/>
  public virtual void SetCableLength(float length) {
    if (cableJoint != null) {
      if (float.IsPositiveInfinity(length)) {
        length = cfgMaxCableLength;
      } else if (float.IsNegativeInfinity(length)) {
        length = Mathf.Min(realCableLength, deployedCableLength);
      } else {
        length = Mathf.Max(length, 0);
      }
      cableJoint.linearLimit = new SoftJointLimit() { limit = length };
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
  /// <param name="distanceLimit">The distance limit to set on the joint.</param>
  void CreateDistanceJoint(ILinkSource source, Rigidbody tgtRb, Vector3 tgtAnchor,
                           float distanceLimit) {
    var joint = source.part.gameObject.AddComponent<ConfigurableJoint>();
    KASAPI.JointUtils.ResetJoint(joint);
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
