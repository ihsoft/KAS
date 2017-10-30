// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;
using KSPDev.GUIUtils;
using KSPDev.KSPInterfaces;
using KSPDev.LogUtils;
using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KAS {

/// <summary>Module that controls a physical cable joint on a KAS part.</summary>
/// <remarks>
/// When creating a link, the cable's <see cref="maxAllowedCableLength"/> is set to the actual
/// distance between the objects at the moment of creation. The colliders on the objects are enabled
/// by default, i.e. the source and the target can collide.
/// </remarks>
public class KASModuleCableJointBase : KASModuleJointBase,
    // KSP interfaces.
    IModuleInfo,
    // KAS interfaces.
    ILinkCableJoint,
    // KSPDev syntax sugar interfaces.
    IKSPDevModuleInfo {

  #region Localizable GUI strings. Next ID=#kasLOC_09005
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.ForceType']/*"/>
  protected readonly static Message<ForceType> CableSpringStrengthInfo = new Message<ForceType>(
      "#kasLOC_09002",
      defaultTemplate: "Spring force: <<1>>",
      description: "Info string in the editor for the cable spring force setting. The argument is"
      + " of type ForceType.",
      example: "Cable break force: 1.2 kN");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  new protected readonly static Message ModuleTitle = new Message(
      "#kasLOC_09004",
      defaultTemplate: "KAS Cable",
      description: "Title of the module to present in the editor details window.");
  #endregion

  #region Persistent fields
  /// <summary>Maximum length of the cable on the joint.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  [KSPField(isPersistant = true)]
  float persistedCableLength;
  #endregion

  #region ILinkCableJoint CFG properties
  /// <inheritdoc/>
  public float cfgMaxCableLength { get { return maxLinkLength; } }
  #endregion

  #region ILinkCableJoint properties
  /// <inheritdoc/>
  public SpringJoint cableJointObj { get; private set; }

  /// <inheritdoc/>
  public Rigidbody headRb { get; private set; }

  /// <inheritdoc/>
  public ILinkSource headSource { get; private set; }

  /// <inheritdoc/>
  public Transform headPhysicalAnchorObj { get; private set; }

  /// <inheritdoc/>
  public virtual float maxAllowedCableLength {
    get { return persistedCableLength; }
    set {
      persistedCableLength = value;
      if (cableJointObj != null) {
        cableJointObj.maxDistance = value;
      }
      part.Modules.OfType<IKasPropertyChangeListener>().ToList().ForEach(x =>
          x.OnKASPropertyChanged(this as ILinkCableJoint,
                                 ILinkCableJoint_Properties.maxAllowedCableLength));
    }
  }

  /// <inheritdoc/>
  public float realCableLength {
    get {
      var source = headSource ?? linkSource;
      if (cableJointObj != null && source != null) {
        return Vector3.Distance(
            source.part.rb.transform.TransformPoint(cableJointObj.anchor),
            cableJointObj.connectedBody.transform.TransformPoint(cableJointObj.connectedAnchor));
      }
      return 0;
    }
  }
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
  #endregion

  #region Inheritable properties
  /// <summary>Tells if the physical head is started and active.</summary>
  /// <value>The status of the physical head.</value>
  protected bool isHeadStarted { get { return headSource != null; } }
  #endregion

  #region KASModuleJointBase overrides
  /// <inheritdoc/>
  protected override void AttachParts() {
    // Intentionally skip the base method since it would create a rigid link.
    CreateCableJoint(
        linkSource.part.gameObject, linkSource.physicalAnchorTransform.position,
        linkTarget.part.rb, linkTarget.physicalAnchorTransform.position);
  }

  /// <inheritdoc/>
  protected override void DetachParts() {
    base.DetachParts();
    Object.Destroy(cableJointObj);
    cableJointObj = null;
    headSource = null;
    headRb = null;
  }
  #endregion

  #region ILinkCableJoint implementation
  /// <inheritdoc/>
  public void StartPhysicalHead(ILinkSource source, Transform headObjAnchor) {
    //FIXME: add the physical head module here.
    headRb = headObjAnchor.GetComponentInParent<Rigidbody>();
    if (isHeadStarted || isLinked || headRb == null) {
      HostedDebugLog.Error(this,
          "Bad link state for the physical head start: isLinked={0}, isHeadStarted={1}, hasRb=[2}",
          isLinked, isHeadStarted, headRb != null);
      return;
    }
    headSource = source;
    headPhysicalAnchorObj = headObjAnchor;

    // Attach the head to the source.
    CreateCableJoint(
        source.part.gameObject, source.physicalAnchorTransform.position,
        headRb, headObjAnchor.position);
  }

  /// <inheritdoc/>
  public void StopPhysicalHead() {
    headRb = null;
    headSource = null;
    headPhysicalAnchorObj = null;
    DestroyImmediate(cableJointObj);
    cableJointObj = null;
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
  /// <summary>Connects two rigidbodies with a spring joint.</summary>
  /// <remarks>
  /// The link max length is set to <see cref="maxAllowedCableLength"/>. If it's shorter than the
  /// real distance between the objects, then the actual length is used to avoid the physical
  /// effects.
  /// </remarks>
  /// <param name="srcObj">
  /// The game object owns the source rigid body. It will also own the joint.
  /// </param>
  /// <param name="srcAnchor">The anchor point for the joint at the source in world space.</param>
  /// <param name="tgtRb">The rigidbody of the target.</param>
  /// <param name="tgtAnchor">The anchor point for the joint at the target in world space.</param>
  void CreateCableJoint(GameObject srcObj, Vector3 srcAnchor, Rigidbody tgtRb, Vector3 tgtAnchor) {
    cableJointObj = srcObj.gameObject.AddComponent<SpringJoint>();
    cableJointObj.spring = cableSpringForce;
    cableJointObj.damper = cableSpringDamper;
    cableJointObj.autoConfigureConnectedAnchor = false;
    cableJointObj.anchor = srcObj.transform.InverseTransformPoint(srcAnchor);
    cableJointObj.connectedBody = tgtRb;
    cableJointObj.connectedAnchor = tgtRb.transform.InverseTransformPoint(tgtAnchor);
    cableJointObj.maxDistance = Mathf.Max(realCableLength, maxAllowedCableLength);
    if (cableJointObj.maxDistance > maxAllowedCableLength) {
      // Keep it consistent in case of an adjustment has been made.
      maxAllowedCableLength = cableJointObj.maxDistance;
    }
    SetBreakForces(cableJointObj);
  }
  #endregion
}

}  // namespace
