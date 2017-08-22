﻿// Kerbal Attachment System
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
/// When creating a link via <see cref="CreateJoint"/> the cable <see cref="maxAllowedCableLength"/>
/// is set to the actual distance between the objects at the moment of creation. The colliders on
/// the objects are enabled by default, i.e. the source and the target can collide.
/// </remarks>
public class KASModuleCableJointBase : PartModule,
    // KSP interfaces.
    IModuleInfo,
    // KAS interfaces.
    ILinkCableJoint,
    // KSPDev syntax sugar interfaces.
    IPartModule, IsDestroyable, IKSPDevModuleInfo {

  #region Localizable GUI strings. Next ID=#kasLOC_09005
  /// <include file="SpecialDocTags.xml" path="Tags/Message2/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.DistanceType']/*"/>
  protected readonly static Message<DistanceType, DistanceType> MaxLengthLimitReachedMsg =
      new Message<DistanceType, DistanceType>(
          "#kasLOC_09000",
          defaultTemplate: "Distance is too long: <<1>> > <<2>>",
          description: "Message to display when the link cannot be established because the distance"
          + " as larger than the maximum cable length."
          + "\nArgument <<1>> is the attempted length of type DistanceType."
          + "\nArgument <<2>> is the cable's maximum setting of type DistanceType.",
          example: "Distance is too long: 2.33 m > 1.22 m");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.DistanceType']/*"/>
  protected readonly static Message<DistanceType> CableMaxLengthInfo = new Message<DistanceType>(
      "#kasLOC_09001",
      defaultTemplate: "Cable length: <<1>>",
      description: "Info string in the editor for the maximum cable length setting. The argument is"
      + " of type DistanceType.",
      example: "Cable length: 12.5 m");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.ForceType']/*"/>
  protected readonly static Message<ForceType> CableSpringStrengthInfo = new Message<ForceType>(
      "#kasLOC_09002",
      defaultTemplate: "Spring force: <<1>>",
      description: "Info string in the editor for the cable spring force setting. The argument is"
      + " of type ForceType.",
      example: "Cable break force: 1.2 kN");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.ForceType']/*"/>
  protected readonly static Message<ForceType> CableBreakhForceInfo = new Message<ForceType>(
      "#kasLOC_09003",
      defaultTemplate: "Cable break force: <<1>>",
      description: "Info string in the editor for the cable break force setting. The argument is of"
      + " type ForceType.",
      example: "Cable break force: 1.2 kN");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  protected readonly static Message ModuleTitle = new Message(
      "#kasLOC_09004",
      defaultTemplate: "KAS Cable",
      description: "Title of the module to present in the editor details window.");
  #endregion

  #region Persistent fields
  /// <summary>Maximum length of the cable on the joint.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>
  [KSPField(isPersistant = true)]
  float cableLength;
  #endregion

  #region ILinkCableJoint CFG properties
  /// <inheritdoc/>
  public string cfgJointName { get { return jointName; } }

  /// <inheritdoc/>
  public float cfgMaxCableLength { get { return maxCableLength; } }

  /// <inheritdoc/>
  public float cfgCableSpringForce { get { return cableSpringForce; } }

  /// <inheritdoc/>
  public float cfgCableBreakForce { get { return cableBreakForce; } }
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
    get { return cableLength; }
    set {
      cableLength = value;
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
  /// <summary>See <see cref="cfgJointName"/>.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string jointName = "";

  /// <summary>See <see cref="cfgMaxCableLength"/>.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float maxCableLength;

  /// <summary>See <see cref="cfgCableSpringForce"/>.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float cableSpringForce;

  /// <summary>Damper force to apply to stop the oscillations.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float cableSpringDamper = 0.1f;

  /// <summary>See <see cref="cfgCableBreakForce"/>.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float cableBreakForce;
  #endregion

  #region Inheritable properties
  /// <summary>Source of the link.</summary>
  /// <value>The link module on the source part.</value>
  protected ILinkSource linkSource { get; private set; }

  /// <summary>Target of the link.</summary>
  /// <value>The link module on the target part.</value>
  protected ILinkTarget linkTarget { get; private set; }

  /// <summary>Tells if there is a physical joint created.</summary>
  /// <value><c>true</c> if the source and target parts are physically linked.</value>
  protected bool isLinked { get; private set; }

  /// <summary>Tells if the physical head is started and active.</summary>
  /// <value>The status of the physical head.</value>
  protected bool isHeadStarted { get { return headSource != null; } }
  #endregion

  #region ILinkCableJoint implementation
  /// <inheritdoc/>
  public virtual bool CreateJoint(ILinkSource source, ILinkTarget target) {
    var errors = CheckConstraints(source, target.nodeTransform);
    if (errors.Length > 0) {
      HostedDebugLog.Error(this, "Cannot create joint: {0}", DbgFormatter.C2S(errors));
      return false;
    }
    if (isLinked) {
      HostedDebugLog.Warning(this, "The joint is already linked. Drop it!");
      DropJoint();
    }
    if (isHeadStarted) {
      HostedDebugLog.Warning(this, "Starting joint when the physical head is active. Drop it!");
      StopPhysicalHead();
    }
    linkSource = source;
    linkTarget = target;
    CreateCableJoint(
        source.part.gameObject, source.physicalAnchorTransform.position,
        target.part.rb, target.physicalAnchorTransform.position);
    isLinked = true;
    return true;
  }

  /// <inheritdoc/>
  public virtual void DropJoint() {
    linkSource = null;
    linkTarget = null;
    isLinked = false;
    Object.Destroy(cableJointObj);
    cableJointObj = null;
    headSource = null;
    headRb = null;
  }

  /// <inheritdoc/>
  public virtual string[] CheckConstraints(ILinkSource source, Transform targetTransform) {
    var length = Vector3.Distance(
        source.physicalAnchorTransform.position,
        targetTransform.TransformPoint(source.targetPhysicalAnchor));
    return length > cfgMaxCableLength
        ? new[] { MaxLengthLimitReachedMsg.Format(length, cfgMaxCableLength) }
        : new string[0];
  }

  /// <inheritdoc/>
  public void StartPhysicalHead(ILinkSource source, Transform headObjAnchor) {
    //FIXME: add the physical head module here.
    if (isHeadStarted) {
      HostedDebugLog.Warning(
          this, "Physical head is already started. Stopping the old one.");
      StopPhysicalHead();
    }
    if (isLinked) {
      HostedDebugLog.Warning(this, "Joint is alreadfy estabslished. Break it!");
      DropJoint();
    }
    headRb = headObjAnchor.GetComponentInParent<Rigidbody>();
    if (headRb == null) {
      HostedDebugLog.Warning(this, "Cannot find rigid body from: {0}. Adding new.", headObjAnchor);
      headRb = headObjAnchor.gameObject.AddComponent<Rigidbody>();
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
    sb.AppendLine(CableMaxLengthInfo.Format(maxCableLength));
    sb.AppendLine(CableSpringStrengthInfo.Format(cableSpringForce));
    sb.AppendLine(CableBreakhForceInfo.Format(cableBreakForce));
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

  #region IsDestroyable implementation
  /// <inheritdoc/>
  public virtual void OnDestroy() {
    // This can happen in case of the part external destruction (e.g. explosion).
    DropJoint();
  }
  #endregion

  IEnumerator OnJointBreak(float breakForce) {
    // Don't save the object since it will be nulled in case of destruction.
    var cableJointExisted = cableJointObj != null;
    var formerParent = part.parent;
    yield return new WaitForEndOfFrame();
    if (cableJointExisted && cableJointObj == null) {
      HostedDebugLog.Info(this, "Cable joint broken with force {0}", breakForce);
      if (linkSource != null) {
        linkSource.BreakCurrentLink(
            LinkActorType.Physics,
            moveFocusOnTarget: FlightGlobals.ActiveVessel != linkSource.part.vessel);
      }
      if (formerParent != null && part.parent == null) {
        HostedDebugLog.Info(this, "Restore the parts joint to: {0}", formerParent);
        part.Couple(formerParent);
      }
    }
  }

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
    cableJointObj.breakForce = cableBreakForce;
    cableJointObj.breakTorque = Mathf.Infinity;  // Cable is not sensitive to the rotations. 
  }
  #endregion
}

}  // namespace
