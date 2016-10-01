// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using System.Linq;
using UnityEngine;
using KASAPIv1;
using KSPDev.ModelUtils;
using KSPDev.GUIUtils;

namespace KAS {

/// <summary>
/// Module that keeps all pieces of the link in the model. I.e. it's a physical representation of
/// the part that can link to another part.
/// </summary>
public class KASModuleTelescopicPipeStrut : AbstractJointPart, ILinkRenderer {

  #region Localizable GUI strings
  protected static Message<string> LinkCollidesWithObjectMsg = "Link collides with {0}";
  protected static Message LinkCollidesWithSurfaceMsg = "Link collides with surface";
  #endregion

  // These fields must not be accessed outside of the module. They are declared public only
  // because KSP won't work otherwise. Ancenstors and external callers must access values via
  // interface properties. If property is not there then it means it's *intentionally* restricted
  // for the non-internal consumers.
  #region Persistent fields
  [KSPField(isPersistant = true)]
  public Vector3 parkedOrientation = Vector3.zero;
  [KSPField(isPersistant = true)]
  public float parkedLength = 0;  // If 0 then minimum link length will be used.
  #endregion

  // These fields must not be accessed outside of the module. They are declared public only
  // because KSP won't work otherwise. Ancenstors and external callers must access values via
  // interface properties. If property is not there then it means it's *intentionally* restricted
  // for the non-internal consumers.
  #region Part's config fields
  [KSPField]
  public int pistonsCount = 3;
  [KSPField]
  public float outerPistonDiameter = 0.15f;
  [KSPField]
  public float pistonLength = 0.2f;
  [KSPField]
  public float pistonWallThickness = 0.01f;
  [KSPField]
  public string pistonTexturePath = "";
  [KSPField]
  public float pistonMinShift = 0.02f;
  [KSPField]
  public string parkedOrientationMenu0 = "";
  [KSPField]
  public string parkedOrientationMenu1 = "";
  [KSPField]
  public string parkedOrientationMenu2 = "";
  [KSPField]
  public string rendererName = "";
  #endregion

  // These constants must be in sync with action handler method names.
  #region Event names
  protected const string MenuAction0Name = "ParkedOrientationMenuAction0";
  protected const string MenuAction1Name = "ParkedOrientationMenuAction1";
  protected const string MenuAction2Name = "ParkedOrientationMenuAction2";
  protected const string ExtendAtMaxMenuActionName = "ExtendAtMaxMenuAction";
  protected const string RetractToMinMenuActionName = "RetractToMinMenuAction";
  #endregion

  #region Model name constants
  /// <summary>A transform that is a root for the whole pipe modelset.</summary>
  /// <remarks>It doesn't have to match part's atatch node transform.</remarks>
  protected const string AttachNodeObjName = "AttachNode";
  /// <summary>Name of model that connects pipe with the source part.</summary>
  protected const string SrcPartJointObjName = "srcPartJoint";
  /// <summary>Name of model at the pipe start.</summary>
  protected const string SrcStrutJointObjName = "srcStrutJoint";
  /// <summary>Name of model at the pipe end.</summary>
  protected const string TrgStrutJointObjName = "trgStrutJoint";
  /// <summary>Name of model that connects pipe with the target part.</summary>
  protected const string TrgPartJointObjName = "trgPartJoint";
  #endregion

  protected ILinkSource linkSource { get; private set; }
  #region Model transforms
  /// <summary>Model that connects pipe assembly with the source part.</summary> 
  protected Transform srcPartJoint { get; private set; }
  /// <summary>Pivot axis model at the source part.</summary>
  protected Transform srcPartJointPivot { get; private set; }
  /// <summary>Model at the pipe start.</summary>
  /// <remarks>It's orientation is reversed, and it's positioned so what its pivot axis matches
  /// <see cref="srcPartJointPivot"/>. I.e. forward direction in the local space is
  /// <see cref="Vector3.back"/>.</remarks>
  protected Transform srcStrutJoint { get; private set; }
  /// <summary>Model at the pipe end.</summary>
  protected Transform trgStrutJoint { get; private set; }
  /// <summary>Pivot axis model at the pipe end.</summary>
  protected Transform trgStrutJointPivot { get; private set; }
  /// <summary>Distance of source part joint pivot from it's base.</summary>
  protected float srcJointHandleLength { get; private set; }
  /// <summary>Distance of target part joint pivot from it's base.</summary>
  protected float trgJointHandleLength { get; private set; }
  /// <summary>Pistons that form the strut.</summary>
  protected GameObject[] pistons { get; private set; }
  #endregion

  /// <summary>Tells if source on the part is linked.</summary>
  protected bool isLinked {
    get { return linkSource != null && linkSource.linkState == LinkState.Linked; }
  }
  /// <summary>Minmum link length that doesn't break telescopic pipe renderer.</summary>
  protected float minLinkLength { get; private set; }
  /// <summary>Maximum link length that doesn't break telescopic pipe renderer.</summary>
  protected float maxLinkLength { get; private set; }

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    Events[MenuAction0Name].guiName = ExtractPositionName(parkedOrientationMenu0);
    Events[MenuAction1Name].guiName = ExtractPositionName(parkedOrientationMenu1);
    Events[MenuAction2Name].guiName = ExtractPositionName(parkedOrientationMenu2);
    UpdateMenuItems();  // For editor mode.
  }

  /// <inheritdoc/>
  public override void OnStart(PartModule.StartState state) {
    base.OnStart(state);
    linkSource = part.FindModuleImplementing<ILinkSource>();
    UpdateMenuItems();
    UpdateLinkLengthAndOrientation();
  }

  /// <inheritdoc/>
  public override void OnUpdate() {
    base.OnUpdate();
    //FIXME: check if constant updates needed, and add a threshold.
    if (isStarted) {
      UpdateLink();
    }
  }
  #endregion

  #region ILinkRenderer implemetation
  /// <inheritdoc/>
  public string cfgRendererName { get { return rendererName; } }
  /// <inheritdoc/>
  public virtual Color? colorOverride {
    get { return _colorOverride; }
    set {
      _colorOverride = value;
      Meshes.UpdateMaterials(srcPartJoint.gameObject, newColor: _colorOverride ?? color);
    }
  }
  Color? _colorOverride;
  /// <inheritdoc/>
  public virtual string shaderNameOverride {
    get { return _shaderNameOverride; }
    set {
      _shaderNameOverride = value;
      Meshes.UpdateMaterials(
          srcPartJoint.gameObject, newShaderName: _shaderNameOverride ?? shaderName);
    }
  }
  string _shaderNameOverride;

  //FIXME change to isColliderEnabled.
  /// <inheritdoc/>
  public virtual bool isPhysicalCollider {
    get { return _isPhysicalCollider; }
    set {
      _isPhysicalCollider = value;
      //FIXME
      Debug.LogWarningFormat("Setting collider mode to {0}", value);
      //Colliders.UpdateColliders(srcPartJoint.gameObject, isPhysical: value);
      Colliders.UpdateColliders(srcPartJoint.gameObject, isEnabled: value);
    }
  }
  bool _isPhysicalCollider;

  /// <inheritdoc/>
  public override Transform sourceTransform {
    get { return srcPartJointPivot; }
    set {
      Debug.LogErrorFormat("Dynamic part has a fixed source transform. Cannot set to: {0}", value);
    }
  }
  //FIXME: hide or make part of interface
  public Transform targetTransform {
    get { return _targetTransform; }
    set {
      _targetTransform = value;
      UpdateLinkLengthAndOrientation();
    }
  }
  Transform _targetTransform;
  /// <inheritdoc/>
  public bool isStarted {
    get { return targetTransform != null; }
  }

  /// <inheritdoc/>
  public virtual void StartRenderer(Transform source, Transform target) {
    // Source pivot is fixed for this part. Do a safe check to verify if requestor asked for the
    // right coordinates.
    if (Vector3.SqrMagnitude(source.position - sourceTransform.position) > 0.0005f) {
      Debug.LogErrorFormat(
          "Part's source doesn't match renderer source: pivot={0}, source={1}, err={2}",
          sourceTransform.position,
          source.position,
          Vector3.SqrMagnitude(source.position - sourceTransform.position));
    }
    targetTransform = target;
    UpdateMenuItems();
  }

  /// <inheritdoc/>
  public virtual void StopRenderer() {
    targetTransform = null;
    UpdateMenuItems();
  }

  /// <inheritdoc/>
  public virtual void UpdateLink() {
    // FIXME: update only when diff is big enough
    UpdateLinkLengthAndOrientation();
  }

  /// <inheritdoc/>
  public virtual string CheckColliderHits(Transform source, Transform target) {
    var linkVector = target.position - source.position;
    var hits = Physics.SphereCastAll(
        source.position, outerPistonDiameter / 2, linkVector, GetClampedLinkLength(linkVector),
        (int)(KspLayerMask.PARTS | KspLayerMask.SURFACE | KspLayerMask.KERBALS),
        QueryTriggerInteraction.Ignore);
    foreach (var hit in hits) {
      if (hit.transform.root != source.root && hit.transform.root != target.root) {
        var hitPart = hit.transform.root.GetComponent<Part>();
        // Use partInfo.title to properly display kerbal names.
        return hitPart != null
            ? LinkCollidesWithObjectMsg.Format(hitPart.partInfo.title)
            : LinkCollidesWithSurfaceMsg.ToString();
      }
    }
    return null;
  }
  #endregion

  // FIXME: check colliders.
  #region GUI menu action handlers
  [KSPEvent(guiName = "Pipe position 0", guiActive = true, guiActiveUnfocused = true,
            guiActiveEditor = true, active = false)]
  public void ParkedOrientationMenuAction0() {
    parkedOrientation = ExtractOrientationVector(parkedOrientationMenu0);
    UpdateLinkLengthAndOrientation();
  }

  [KSPEvent(guiName = "Pipe position 1", guiActive = true, guiActiveUnfocused = true,
            guiActiveEditor = true, active = false)]
  public void ParkedOrientationMenuAction1() {
    parkedOrientation = ExtractOrientationVector(parkedOrientationMenu1);
    UpdateLinkLengthAndOrientation();
  }

  [KSPEvent(guiName = "Pipe position 2", guiActive = true, guiActiveUnfocused = true,
            guiActiveEditor = true, active = false)]
  public void ParkedOrientationMenuAction2() {
    parkedOrientation = ExtractOrientationVector(parkedOrientationMenu2);
    UpdateLinkLengthAndOrientation();
  }

  [KSPEvent(guiName = "Extend to max", guiActive = true, guiActiveUnfocused = true,
            guiActiveEditor = true, active = false)]
  public void ExtendAtMaxMenuAction() {
    parkedLength = maxLinkLength;
    UpdateLinkLengthAndOrientation();
  }

  [KSPEvent(guiName = "Retract to min", guiActive = true, guiActiveUnfocused = true,
            guiActiveEditor = true, active = false)]
  public void RetractToMinMenuAction() {
    parkedLength = minLinkLength;
    UpdateLinkLengthAndOrientation();
  }
  #endregion

  #region AbstractProceduralModel implementation
  /// <inheritdoc/>
  protected override void CreatePartModel() {
    // Root for all the links meshes.
    var attachNode = new GameObject(AttachNodeObjName).transform;
    attachNode.parent = partModelTransform;
    attachNode.localPosition = attachNodePosition;
    attachNode.localScale = Vector3.one;
    attachNode.localRotation = Quaternion.LookRotation(attachNodeOrientation);

    // Source part joint model.
    srcPartJoint = CreateStrutJointModel(SrcPartJointObjName);
    Colliders.SetSimpleCollider(srcPartJoint.gameObject, PrimitiveType.Cube);
    Hierarchy.MoveToParent(srcPartJoint, attachNode);
    srcPartJointPivot = Hierarchy.FindTransformInChildren(srcPartJoint, PivotAxileObjName);

    // Source strut joint model.
    srcStrutJoint = CreateStrutJointModel(SrcStrutJointObjName, createAxile: false);
    Colliders.SetSimpleCollider(srcStrutJoint.gameObject, PrimitiveType.Cube);
    var srcStrutPivot = Hierarchy.FindTransformInChildren(srcStrutJoint, PivotAxileObjName);
    srcJointHandleLength = Vector3.Distance(srcStrutJoint.position, srcStrutPivot.position);
    Hierarchy.MoveToParent(srcStrutJoint, srcPartJointPivot,
                           newPosition: new Vector3(0, 0, srcJointHandleLength),
                           newRotation: Quaternion.LookRotation(Vector3.back));

    // Target strut joint model.
    trgStrutJoint = CreateStrutJointModel(TrgStrutJointObjName);
    Colliders.SetSimpleCollider(trgStrutJoint.gameObject, PrimitiveType.Cube);
    trgStrutJointPivot = Hierarchy.FindTransformInChildren(trgStrutJoint, PivotAxileObjName);
    trgJointHandleLength = Vector3.Distance(trgStrutJoint.position, trgStrutJointPivot.position);
    Hierarchy.MoveToParent(trgStrutJoint, srcPartJointPivot);

    // Target part joint model.
    var trgPartJoint = CreateStrutJointModel(TrgStrutJointObjName, createAxile: false);
    Colliders.SetSimpleCollider(trgPartJoint.gameObject, PrimitiveType.Cube);
    var trgPartJointPivot = Hierarchy.FindTransformInChildren(trgPartJoint, PivotAxileObjName);
    Hierarchy.MoveToParent(trgPartJoint, trgStrutJointPivot,
                           newPosition: new Vector3(0, 0, trgJointHandleLength),
                           newRotation: Quaternion.LookRotation(Vector3.back));
      
    // Pistons.
    pistons = new GameObject[pistonsCount];
    var startDiameter = outerPistonDiameter;
    var material = CreateMaterial(GetTexture(pistonTexturePath));
    for (var i = 0; i < pistonsCount; ++i) {
      var piston =
          Meshes.CreateCylinder(startDiameter, pistonLength, material, parent: srcStrutJoint);
      piston.name = "piston" + i;
      Colliders.SetSimpleCollider(piston, PrimitiveType.Capsule);
      // Source strut joint rotation is reversed. All pistons but the last one are relative to the
      // source joint.
      piston.transform.localRotation = Quaternion.LookRotation(Vector3.back);
      startDiameter -= 2 * pistonWallThickness;
      pistons[i] = piston;
    }
    // First piston rigidly attached at the bottom of the source joint model.
    pistons[0].transform.localPosition = new Vector3(0, 0, -pistonLength / 2);
    // Last piston rigidly attached at the bottom of the target joint model.
    Hierarchy.MoveToParent(pistons.Last().transform, trgStrutJoint,
                           newPosition: new Vector3(0, 0, -pistonLength / 2),
                           newRotation: Quaternion.LookRotation(Vector3.forward));
    
    CalculateLengthLimits();

    // Init parked state. It must go after all the models are created.
    parkedOrientation = ExtractOrientationVector(parkedOrientationMenu0);
    if (Mathf.Approximately(parkedLength, 0)) {
      // Cannot get length from the joint module since it may not be existing at the moment.
      parkedLength = minLinkLength;
    }

    UpdateLinkLengthAndOrientation();
  }

  /// <inheritdoc/>
  protected override void LoadPartModel() {
    // Source pivot.
    srcPartJoint = Hierarchy.FindTransformByPath(
        partModelTransform, AttachNodeObjName + "/" + SrcPartJointObjName);
    srcPartJointPivot = Hierarchy.FindTransformInChildren(srcPartJoint, PivotAxileObjName);

    // Source strut joint.
    srcStrutJoint = Hierarchy.FindTransformInChildren(srcPartJointPivot, SrcStrutJointObjName);
    var srcStrutPivot = Hierarchy.FindTransformInChildren(srcStrutJoint, PivotAxileObjName);
    srcJointHandleLength = Vector3.Distance(srcStrutJoint.position, srcStrutPivot.position);

    // Target strut joint.
    trgStrutJoint = Hierarchy.FindTransformInChildren(srcPartJointPivot, TrgStrutJointObjName);
    trgStrutJointPivot = Hierarchy.FindTransformInChildren(trgStrutJoint, PivotAxileObjName);
    trgJointHandleLength = Vector3.Distance(trgStrutJoint.position, trgStrutJointPivot.position);

    // Pistons.
    pistons = new GameObject[pistonsCount];
    for (var i = 0; i < pistonsCount; ++i) {
      pistons[i] = Hierarchy.FindTransformInChildren(partModelTransform, "piston" + i).gameObject;
    }

    CalculateLengthLimits();
    UpdateLinkLengthAndOrientation();
  }
  #endregion

  /// <summary>Adjusts link models to the changed target position.</summary>
  protected virtual void UpdateLinkLengthAndOrientation() {
    if (!isStarted) {
      // Simply align everyting along Z axis, and rotate source pivot according to the settings.
      srcPartJoint.localRotation = Quaternion.identity;
      srcPartJointPivot.localRotation = Quaternion.LookRotation(parkedOrientation);
      trgStrutJoint.localPosition = new Vector3(0, 0, parkedLength - trgJointHandleLength);
      trgStrutJoint.localRotation = Quaternion.identity;
      trgStrutJointPivot.localRotation = Quaternion.identity;
    } else {
      var linkVector = targetTransform.position - sourceTransform.position;
      // Here is the link model hierarchy:
      // srcPartJoint => srcPivot => srcStrutJoint => trgStrutJoint => trgPivot => trgPartJoint.
      // Joints attached via a pivot should be properly aligned againts each other since they are
      // connected with a common pivot axile which is parallel to their X axis.
      // 1. Rotate srcPartJoint around Z axis so what its pivot axile (X) is perpendicular to
      //    the link vector.
      srcPartJoint.rotation = Quaternion.LookRotation(srcPartJoint.forward, -linkVector);
      // 2. Rotate srcPivot around X axis (pivot axile) so what its forward vector points to the
      //    target part attach node.
      srcPartJointPivot.localRotation =
          Quaternion.Euler(Vector3.Angle(linkVector, srcPartJoint.forward), 0, 0);
      // 3. Shift trgStrutJoint along Z axis so what it touches target joint node with the trgPivot
      //    pivot axile. Link length consists of srcStrutJoint and trgStrutJoint model lengths but
      //    the former points backwards, so it doesn't add to the positive Z value.
      trgStrutJoint.localPosition =
          new Vector3(0, 0, GetClampedLinkLength(linkVector) - trgJointHandleLength);
      // 4. Rotate trgStrutJoint around Z axis so what its pivot axile (X) is perpendicular to
      //    the target part attach node.
      trgStrutJoint.rotation =
          Quaternion.LookRotation(trgStrutJoint.forward, targetTransform.forward);
      // 5. Rotate trgPivot around X axis (pivot axile) so what its forward vector points along
      //    target attach node direction.
      trgStrutJointPivot.localRotation =
        Quaternion.Euler(Vector3.Angle(trgStrutJoint.forward, -targetTransform.forward), 0, 0);
    }

    // Distribute pistons between the first and the last while keepin the direction.
    if (pistons.Length > 2) {
      var offset = pistons[0].transform.localPosition.z;
      var scalablePistons = pistons.Length - 1;
      var step = Vector3.Distance(pistons.Last().transform.position, pistons[0].transform.position)
          / scalablePistons;
      for (var i = 1; i < scalablePistons; ++i) {
        offset -= step;  // Pistons are distributed to -Z direction of the pviot.
        pistons[i].transform.localPosition = new Vector3(0, 0, offset);
      }
    }
  }

  /// <summary>Returns link length. Adjusts it to min/max length.</summary>
  /// <param name="linkVector">Link vector.</param>
  /// <returns>Clamped link length</returns>
  /// <seealso cref="minLinkLength"/>
  /// <seealso cref="maxLinkLength"/>
  protected float GetClampedLinkLength(Vector3 linkVector) {
    var linkLength = linkVector.magnitude;
    if (linkLength < minLinkLength) {
      return minLinkLength;
    }
    if (linkLength > maxLinkLength) {
      return maxLinkLength;
    }
    return linkLength;
  }

  #region Private utility methods
  /// <summary>Returns parked menu item name.</summary>
  /// <param name="cfgSetting">String from config of the following format:
  /// <c>X,Y,Z,&lt;menu text&gt;</c>, where <c>X,Y,Z</c> defines direction in node's local
  /// coordinates, and <c>menu text</c> is a string to show in context menu.</param>
  /// <returns></returns>
  string ExtractPositionName(string cfgSetting) {
    var lastCommaPos = cfgSetting.LastIndexOf(',');
    return lastCommaPos != -1
        ? cfgSetting.Substring(lastCommaPos + 1)
        : cfgSetting;
  }

  /// <summary>Returns direction vector for a parked menu item.</summary>
  /// <param name="cfgSetting">String from config of the following format:
  /// <c>X,Y,Z,&lt;menu text&gt;</c>, where <c>X,Y,Z</c> defines direction in node's local
  /// coordinates, and <c>menu text</c> is a string to show in context menu.</param>
  /// <returns></returns>
  Vector3 ExtractOrientationVector(string cfgSetting) {
    var lastCommaPos = cfgSetting.LastIndexOf(',');
    if (lastCommaPos == -1) {
      Debug.LogWarningFormat("Cannot extract direction from string: {0}", cfgSetting);
      return Vector3.forward;
    }
    return ConfigNode.ParseVector3(cfgSetting.Substring(0, lastCommaPos));
  }

  /// <summary>Updates menu item visibility states.</summary>
  void UpdateMenuItems() {
    Events[MenuAction0Name].active = Events[MenuAction0Name].guiName != "" && !isLinked;
    Events[MenuAction1Name].active = Events[MenuAction1Name].guiName != "" && !isLinked;
    Events[MenuAction2Name].active = Events[MenuAction2Name].guiName != "" && !isLinked;
    Events[ExtendAtMaxMenuActionName].active = !isLinked;
    Events[RetractToMinMenuActionName].active = !isLinked;
  }

  /// <summary>Calculates and populates min/max link lengths from the model.</summary>
  void CalculateLengthLimits() {
    minLinkLength =
        srcJointHandleLength
        + pistonLength + (pistonsCount - 1) * pistonMinShift
        + trgJointHandleLength;
    maxLinkLength =
        srcJointHandleLength
        + pistonsCount * (pistonLength - pistonMinShift)
        + trgJointHandleLength;
  }
  #endregion
}

}  // namespace
