// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md
using System;
using System.Linq;
using System.Collections;
using System.Text;
using UnityEngine;
using KASAPIv1;

namespace KAS {

public class KASModuleTwoEndsSphereJoint : PartModule, IModuleInfo, ILinkJoint {
  /// <summary>Helper class to detect sphere joint ends breakage.</summary>
  /// <remarks>When joint breaks the source part is get decoupled from the parent.</remarks>
  class BrokenJointListener : MonoBehaviour {
    /// <summary>Part to decouple on joint break.</summary>
    public Part host;

    /// <summary>Triggers when joint break force if exceeded.</summary>
    /// <remarks>Overridden from <see cref="MonoBehaviour"/>.</remarks>
    /// <param name="breakForce">Actual force that broke the joint.</param>
    void OnJointBreak(float breakForce) {
      if (host.parent != null) {
        Debug.LogFormat("Joint broken with force: {0}", breakForce);
        host.OnPartJointBreak(breakForce);
      }
    }
  }

  public float cfgMinLinkLength { get { return 0; } }

  // These fileds must not be accessed outside of the module. They are declared public only
  // because KSP won't work otherwise. Ancenstors and external callers must access values via
  // interface properties. If property is not there then it means it's *intentionally* restricted
  // for the non-internal consumers.
  #region Part's config fields
  /// <summary>Defines how heavy is one meter of the link tube.</summary>
  [KSPField]
  public float massPerMeter = 0.01f;
  /// <summary>Breaking force for the strut connecting the two parts.</summary>
  /// <remarks>If <c>0</c> then joint is unbreakable. Note, that this force is measured in any
  /// direction of the joint ends movement. E.g. if limit is <c>10</c> then the joint will break
  /// when either source or target object get affected by a force of 10+. The direction of the force
  /// is not important, i.e. this limit is <i>not</i> a link stretch force limit.</remarks>
  /// FIXME: investigate
  [KSPField]
  public float strutBreakForce = 0f;
  /// <summary>Breaking torque for the strut connecting the two parts.</summary>
  /// <remarks>If <c>0</c> then joint is unbreakable by the torque.</remarks>
  [KSPField]
  public float strutBreakTorque = 0f;
  /// <summary>Force to apply to bring stretched/compressed link back to the original.</summary>
  [KSPField]
  public float strutStretchSpring = 0f;
  /// <summary>Limit rotation for the sphere joints. Applied to both ends of the strut.</summary>
  /// <remarks>If <c>0</c> then joint becomes locked.</remarks>
  [KSPField]
  public float linkAngleLimit = 0f;
  /// <summary>Maximum allowed distance between parts to establish a link.</summary>
  [KSPField]
  public float linkLengthLimit = Mathf.Infinity;
  #endregion

  /// <summary>Source sphere joint.</summary>
  /// <remarks>It doesn't allow linear movements but does allow rotation around any axis. Rotation
  /// can be limited via a configuration parameter <see cref="linkAngleLimit"/>. The joint is
  /// unbreakable by the linear force but can be broken by torque.</remarks>
  protected ConfigurableJoint srcJoint { get; private set; }
  /// <summary>Target sphere joint.</summary>
  /// <remarks>It doesn't allow linear movements but does allow rotation around any axis. Rotation
  /// can be limited via a configuration parameter <see cref="linkAngleLimit"/>. The joint is
  /// unbreakable by the linear force but can be broken by torque.</remarks>
  protected ConfigurableJoint trgJoint { get; private set; }
  protected ConfigurableJoint strutJoint { get; private set; }
  protected ILinkSource linkSource { get; private set; }
  protected ILinkTarget linkTarget { get; private set; }
  protected float originalLength { get; private set; }

  #region Localizable GUI strings
  protected static string LengthLimitReachedMsg =
      "Link length limit reached: {0:F2} m > {1:F2} m";
  protected static string TargetNodeAngleLimitReachedMsg =
      "Target angle limit reached: {0:F0} deg > {1:F0} deg";
  protected static string SourceNodeAngleLimitReachedMsg =
      "Source angle limit reached: {0:F0} deg > {1:F0} deg";
  #endregion

  #region ILinkJoint implementation
  /// <inheritdoc/>
  public void SetupJoint(ILinkSource source, ILinkTarget target) {
    //FIXME: check for limits
    CleanupJoint();
    linkSource = source;
    linkTarget = target;
    srcJoint = CreateKinematicJointEnd(source.attachNode);
    trgJoint = CreateKinematicJointEnd(target.attachNode);
    StartCoroutine(WaitAndConnectJointEnds());
  }

  /// <inheritdoc/>
  public void CleanupJoint() {
    if (srcJoint != null) {
      srcJoint.gameObject.DestroyGameObjectImmediate();
      srcJoint = null;
    }
    if (trgJoint != null) {
      trgJoint.gameObject.DestroyGameObjectImmediate();
      trgJoint = null;
    }
    strutJoint = null;
    linkSource = null;
    linkTarget = null;
  }

  /// <inheritdoc/>
  public string CheckLengthLimit(ILinkSource source, Transform targetTransform) {
    var length = Vector3.Distance(source.nodeTransform.position, targetTransform.position);
    return length > linkLengthLimit
        ? string.Format(LengthLimitReachedMsg, length, linkLengthLimit)
        : null;
  }

  /// <inheritdoc/>
  public string CheckAngleLimitAtSource(ILinkSource source, Transform targetTransform) {
    var linkVector = targetTransform.position - source.nodeTransform.position;
    var angle = Vector3.Angle(source.nodeTransform.rotation * Vector3.forward, linkVector);
    return angle > linkAngleLimit
        ? string.Format(SourceNodeAngleLimitReachedMsg, angle, linkAngleLimit)
        : null;
  }

  /// <inheritdoc/>
  public string CheckAngleLimitAtTarget(ILinkSource source, Transform targetTransform) {
    var linkVector = source.nodeTransform.position - targetTransform.position;
    var angle = Vector3.Angle(targetTransform.rotation * Vector3.forward, linkVector);
    return angle > linkAngleLimit
        ? string.Format(TargetNodeAngleLimitReachedMsg, angle, linkAngleLimit)
        : null;
  }
  #endregion

  /// <summary>Destroys child objects when joint is destroyed.</summary>
  /// <para>Overridden from <see cref="MonoBehaviour"/>.</para>
  protected virtual void OnDestroy() {
    CleanupJoint();
  }

  #region IModuleInfo implementation
  /// <summary>Returns description for the editor part's browser.</summary>
  /// <remarks>Declared as virtual is <see cref="PartModule"/> and, hence, needs to be overridden.
  /// Though, it's also a part of <see cref="IModuleInfo"/>.</remarks>
  /// <returns>HTML formatted text to show the in GUI.</returns>
  /// FIXME: move strings to constants.
  /// FIXME: is it HTML?
  public override string GetInfo() {
    var sb = new StringBuilder();
    sb.AppendFormat("Link stretch strength: {0:F1} N/m\n", strutBreakForce);
    if (Mathf.Approximately(strutBreakForce, 0)) {
      sb.AppendLine("Link linear strength: UNBREAKABLE");
    } else {
      sb.AppendFormat("Link linear strength: {0:F1} N\n", strutBreakForce);
    }
    if (Mathf.Approximately(strutBreakTorque, 0)) {
      sb.AppendLine("Link rotation strength: UNBREAKABLE");
    } else {
      sb.AppendFormat("Link rotation strength: {0:F1} N\n", strutBreakTorque);
    }
    sb.AppendFormat("Maximum link length: {0:F1} m\n", linkLengthLimit);
    if (Mathf.Approximately(linkAngleLimit, 0)) {
      sb.Append("Rotation freedom: LOCKED\n");
    } else {
      sb.AppendFormat("Joint freedom: {0:F0} deg\n", linkAngleLimit);
    }
    return sb.ToString();
  }

  /// <summary>Returns module title to show in the editor part's details panel.</summary>
  /// <returns>Title of the module.</returns>
  public virtual string GetModuleTitle() {
    return "KAS Joint";
  }

  /// <summary>Unused.</summary>
  /// <returns>Always <c>null</c>.</returns>
  public virtual Callback<Rect> GetDrawModulePanelCallback() {
    return null;
  }

  /// <summary>Unused.</summary>
  /// <returns>Always <c>null</c>.</returns>
  public virtual string GetPrimaryField() {
    return null;
  }
  #endregion

  /// <summary>Creates a game object joined with the attach node.</summary>
  /// <remarks>The created object has kinematic rigidbody and its transform is parented to the
  /// node's owner. This opbject must be promoted to physical once PhysX received the configuration.
  /// </remarks>
  /// <param name="an">Node to attach a new spheric joint to.</param>
  /// <returns>Object that owns the joint.</returns>
  ConfigurableJoint CreateKinematicJointEnd(AttachNode an) {
    //FIXME
    Debug.LogWarningFormat("Make joint for: {0} (id={1}), {2}",
                           an.owner.name, an.owner.flightID, an.id);
    var objJoint = new GameObject("KASJoint");
    objJoint.AddComponent<BrokenJointListener>().host = part;
    var jointRb = objJoint.AddComponent<Rigidbody>();
    jointRb.isKinematic = true;
    objJoint.transform.parent = an.owner.transform;
    objJoint.transform.localPosition = an.position;
    objJoint.transform.localRotation = Quaternion.LookRotation(an.orientation, an.secondaryAxis);
    var joint = objJoint.AddComponent<ConfigurableJoint>();
    KASAPI.JointUtils.ResetJoint(joint);
    KASAPI.JointUtils.SetupSphericalJoint(joint, angleLimit: linkAngleLimit);
    joint.enablePreprocessing = true;
    joint.connectedBody = an.owner.rb;
    return joint;
  }

  /// <summary>Waits for the next fixed update and connects the joint ends.</summary>
  /// <remarks>Waiting is required to have the spherical joints info sent to the PhysX engine. This
  /// must happen when both ends are at their "kinematic" positions to let engine capture the
  /// initial angles. Once it's done it's time to rotate the ends so what they look at each other,
  /// and attach them with a fixed or prismatic joint.</remarks>
  /// <returns><c>null</c> when it's time to terminate.</returns>
  IEnumerator WaitAndConnectJointEnds() {
    // Allow fixed update to have the joint info sent to PhysX. This will capture initial sphere
    // joints rotations.
    //FIXME
    Debug.LogWarning("Wait for sphere joints to populate");
    yield return new WaitForFixedUpdate();

    // Now rotate both sphere joints towards each other, and connect them with a prismatic joint.
    var srcRb = srcJoint.gameObject.GetComponent<Rigidbody>();
    var trgRb = trgJoint.gameObject.GetComponent<Rigidbody>();
    srcJoint.transform.LookAt(trgJoint.transform);
    trgJoint.transform.LookAt(srcJoint.transform);
    strutJoint = srcJoint.gameObject.AddComponent<ConfigurableJoint>();
    KASAPI.JointUtils.ResetJoint(strutJoint);
    KASAPI.JointUtils.SetupPrismaticJoint(strutJoint, springForce: strutStretchSpring);
    originalLength = Vector3.Distance(srcJoint.transform.position, trgJoint.transform.position);
    strutJoint.enablePreprocessing = true;
    strutJoint.connectedBody = trgRb;
    
    // Allow another fixed update to happen to remember strut positions.
    //FIXME
    Debug.LogWarning("Wait for strut joints to populate");
    yield return new WaitForFixedUpdate();

    // Setup link mass. Since it consists of two independent objects distrubute the total mass
    // between them, and shift center of the mass to the middle of the joint. It may produce odd
    // effects, though.
    var linkLength = Vector3.Distance(srcRb.transform.position, trgRb.transform.position);
    var linkMass = massPerMeter * linkLength;
    //FIXME
    Debug.LogWarningFormat("Link: length={0}, mass={1}", linkLength, linkMass);
    srcRb.mass = linkMass / 2;
    srcRb.centerOfMass = Vector3.forward * (linkLength / 2);
    trgRb.mass = linkMass / 2;
    trgRb.centerOfMass = Vector3.forward * (linkLength / 2);

    // Setup breaking forces:
    // - Sphere joints should only check for the limit exhausting forces, so set torque limit only.
    // - Strut joint should only check for the stretch/compress forces, so set breaking force onyl.
    //   Note, that in Unity this kind of force is indistinguishable from the normal acceleration.
    //   I.e. the joint will break even if there is no actualy stretching, but the part's ends got
    //   an impulse.
    // FIXME: Add a new module to support stretching only breaking force.
    var breakTorque =
        Mathf.Approximately(strutBreakTorque, 0) ? Mathf.Infinity : strutBreakTorque;
    strutJoint.breakForce =
        Mathf.Approximately(strutBreakForce, 0) ? Mathf.Infinity : strutBreakForce;
    strutJoint.breakTorque = Mathf.Infinity;
    srcJoint.gameObject.AddComponent<BrokenJointListener>().host = part;
    srcJoint.breakForce = Mathf.Infinity;
    srcJoint.breakTorque = breakTorque;
    trgJoint.gameObject.AddComponent<BrokenJointListener>().host = part;
    trgJoint.breakForce = Mathf.Infinity;
    trgJoint.breakTorque = breakTorque;

    // Re-align the ends in case of parent objects have moved in the last physics frame. Here
    // physics effects may show up.
    srcJoint.transform.LookAt(trgJoint.transform);
    trgJoint.transform.LookAt(srcJoint.transform);

    // Promote source and target rigid bodies to physical. From now on they live in a system of
    // three joints.
    srcRb.isKinematic = false;
    srcJoint.transform.parent = null;
    trgRb.isKinematic = false;
    trgJoint.transform.parent = null;
    
    //FIXME
    Debug.LogWarning("Joint promoted to physics");
    DestroyStockJoint();
  }

  /// <summary>Destroys the joint created by KSP to tie the parts together.</summary>
  /// <remarks>To eliminate the side effects drop the joint only when the alternative is ready for
  /// use.</remarks>
  void DestroyStockJoint() {
    if (linkSource.part.attachJoint != null) {
      //FIXME
      Debug.LogWarning("drop src-to-trg joint");
      linkSource.part.attachJoint.DestroyJoint();
      linkSource.part.attachJoint = null;
    }
  }
}

}  // namespace
