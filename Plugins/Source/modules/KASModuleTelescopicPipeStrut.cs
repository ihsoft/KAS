// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using System.Linq;
using TestScripts;
using UnityEngine;
using KASAPIv1;
using HighlightingSystem;
using KSPDev.ModelUtils;
using KSPDev.GUIUtils;

namespace KAS {

// FIXME: docs
// FIXME: move model logic into a base class. maybe
public class KASModuleTelescopicPipeStrut : AbstractJointPart, ILinkRenderer {
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
  #endregion

  // These fields must not be accessed outside of the module. They are declared public only
  // because KSP won't work otherwise. Ancenstors and external callers must access values via
  // interface properties. If property is not there then it means it's *intentionally* restricted
  // for the non-internal consumers.
  #region Part's config fields
  [KSPField(isPersistant = true)]
  public Vector3 parkedOrientation = Vector3.zero;
  [KSPField(isPersistant = true)]
  public float parkedLength = 0; // If 0 then minimum link length will be used.
  [KSPField]
  public string rendererName = "";
  #endregion

  // These constants must be in sync with action handler methods names.
  protected const string parkedOrientationMenuAction0 = "ParkedOrientationMenuAction0";
  protected const string parkedOrientationMenuAction1 = "ParkedOrientationMenuAction1";
  protected const string parkedOrientationMenuAction2 = "ParkedOrientationMenuAction2";

  protected ILinkSource linkSource { get; private set; }
  protected bool isLinked {
    get { return linkSource != null && linkSource.linkState == LinkState.Linked; }
  }

  #region Localizable messages
  protected static Message<string> LinkCollidesWithObjectMsg = "Link would collide with {0}";
  protected static Message LinkCollidesWithSurfaceMsg = "Link would collide with surface";
  #endregion

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    //FIXME
    Debug.LogWarningFormat("**** ONAWAKE: pistons {0}, name={1}, pistons={2}",
                           pistonsCount, part.name,
                           pistons != null ? "" + pistons.Length : "NULL");
    base.OnAwake();
    Events[parkedOrientationMenuAction0].guiName = ExtractPositionName(parkedOrientationMenu0);
    Events[parkedOrientationMenuAction1].guiName = ExtractPositionName(parkedOrientationMenu1);
    Events[parkedOrientationMenuAction2].guiName = ExtractPositionName(parkedOrientationMenu2);
    UpdateMenuItems();  // For editor mode.
  }

  /// <inheritdoc/>
  public override void OnStart(PartModule.StartState state) {
    //FIXME
    Debug.LogWarningFormat("**** OnStart: name={0}", part.name);
    base.OnStart(state);
    linkSource = part.FindModuleImplementing<ILinkSource>();

    var linkJoint = part.FindModuleImplementing<ILinkJoint>();
    linkJoint.cfgMinLinkLength = minLinkLength;
    linkJoint.cfgMaxLinkLength = maxLinkLength;
    //FIXME
    Debug.LogWarningFormat("*** Set length: min={0}, max={1}",
                           linkJoint.cfgMinLinkLength,
                           linkJoint.cfgMaxLinkLength);

    UpdateMenuItems();
    UpdateLinkLengthAndOrientation();
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    //FIXME
    Debug.LogWarningFormat("**** ONLOAD: pistons {0}, name={1}", pistonsCount, part.name);
    base.OnLoad(node);
  }
  
  public override void OnInitialize() {
    Debug.LogWarningFormat("**** OnInitialize: name={0}", part.name);
    base.OnInitialize();
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
    //FIXME make threshold a constant
    if (Vector3.SqrMagnitude(source.position - sourceTransform.position) > 0.0005f) {
      Debug.LogErrorFormat(
          "Part's source doesn't match renderer source: pivot={0}, source={1}, err={2}",
          sourceTransform.position,
          source.position,
          Vector3.SqrMagnitude(source.position - sourceTransform.position));
    }
    targetTransform = target;
  }

  /// <inheritdoc/>
  public virtual void StopRenderer() {
    targetTransform = null;
  }

  /// <inheritdoc/>
  public virtual void UpdateLink() {
    UpdateLinkLengthAndOrientation();
  }

  /// <inheritdoc/>
  public virtual string CheckColliderHits(Transform source, Transform target) {
    var direction = target.position - source.position;
    var hits = Physics.SphereCastAll(
        source.position, outerPistonDiameter, direction, direction.magnitude,
        //FIXME
        //Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        (int)(KspLayerMask.PARTS | KspLayerMask.SURFACE | KspLayerMask.KERBALS),
        QueryTriggerInteraction.Ignore);
    foreach (var hit in hits) {
//      Debug.LogWarningFormat("** trace0-1: layer={0}", hit.collider.gameObject.layer);
//      Debug.LogWarningFormat("** trace0-2: {0}", hit.transform);
//      Debug.LogWarningFormat("** trace0-3: {0}", hit.transform.root);
//      Debug.LogWarningFormat("** trace0-6: {0}", hit.rigidbody);
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
  #region Action handlers
  [KSPEvent(guiName = "Pipe position 0", guiActive = true, guiActiveUnfocused = true,
            guiActiveEditor = false, active = false)]
  public void ParkedOrientationMenuAction0() {
    if (!isLinked) {
      parkedOrientation = ExtractOrientationVector(parkedOrientationMenu0);
      UpdateLinkLengthAndOrientation();
    }
  }

  [KSPEvent(guiName = "Pipe position 1", guiActive = true, guiActiveUnfocused = true,
            guiActiveEditor = false, active = false)]
  public void ParkedOrientationMenuAction1() {
    if (!isLinked) {
      parkedOrientation = ExtractOrientationVector(parkedOrientationMenu1);
      UpdateLinkLengthAndOrientation();
    }
  }

  [KSPEvent(guiName = "Pipe position 2", guiActive = true, guiActiveUnfocused = true,
            guiActiveEditor = false, active = false)]
  public void ParkedOrientationMenuAction2() {
    if (!isLinked) {
      parkedOrientation = ExtractOrientationVector(parkedOrientationMenu2);
      UpdateLinkLengthAndOrientation();
    }
  }

  [KSPEvent(guiName = "Extend to max", guiActiveEditor = true, active = true)]
  public void ExtendAtMaxMenuAction() {
    if (!isLinked) {
      parkedLength = maxLinkLength;
      UpdateLinkLengthAndOrientation();
    }
  }

  [KSPEvent(guiName = "Retract to min", guiActiveEditor = true, active = true)]
  public void RetractToMinMenuAction() {
    if (!isLinked) {
      parkedLength = minLinkLength;
      UpdateLinkLengthAndOrientation();
    }
  }
  #endregion

  // FIXME: docs for all below
  protected GameObject[] pistons;
  protected const string SrcPartJointObjName = "srcPartJoint";
  protected const string SrcStrutJointObjName = "srcStrutJoint";
  protected const string TrgStrutJointObjName = "trgStrutJoint";
  protected const string TrgPartJointObjName = "trgPartJoint";

  protected Transform srcPartJoint { get; private set; }
  protected Transform srcPartJointPivot { get; private set; }
  protected Transform srcStrutJoint { get; private set; }
  protected Transform trgStrutJoint { get; private set; }
  protected Transform trgStrutJointPivot { get; private set; }
  protected float srcJointHandleLength { get; private set; }
  protected float trgJointHandleLength { get; private set; }
  /// <summary>Maximum possible link length with this part.</summary>
  /// FIXME: populate it on model load/create
  protected float maxLinkLength {
    get {
      return
          srcJointHandleLength
          + pistonsCount * (pistonLength - pistonMinShift)
          + trgJointHandleLength;
    }
  }
  /// <summary>Minimum possible link length with this part.</summary>
  /// FIXME: populate it on model load/create
  protected float minLinkLength {
    get {
      return
          srcJointHandleLength
          + pistonLength + (pistonsCount - 1) * pistonMinShift
          + trgJointHandleLength;
    }
  }

  /// <summary>
  /// 
  /// </summary>
  protected const string AttachNodeObjName = "AttachNode";
  // FIXME: docs
  protected Transform CreateAttachNodeTransform() {
    var node = new GameObject(AttachNodeObjName).transform;
    node.parent = partModelTransform;
    node.localPosition = attachNodePosition;
    node.localScale = Vector3.one;
    node.localRotation = Quaternion.LookRotation(attachNodeOrientation);
    return node;
  }

  #region AbstractProceduralModel implementation
  /// <inheritdoc/>
  protected override void CreatePartModel() {
    //FIXME: figure out attach node form joint piviot and its holder length  
    var attachNode = CreateAttachNodeTransform();

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

    // Init parked state. It must go after all the models are created.
    parkedOrientation = ExtractOrientationVector(parkedOrientationMenu0);
    if (Mathf.Approximately(parkedLength, 0)) {
      parkedLength = minLinkLength;
    }
    UpdateLinkLengthAndOrientation();
  }

  /// <inheritdoc/>
  protected override void LoadPartModel() {
    //FIXME
    Debug.LogWarningFormat("** Load model: pistons={0}, name={1}", pistonsCount, part.name);

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
      trgStrutJoint.localPosition = new Vector3(0, 0, linkVector.magnitude - trgJointHandleLength);
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
//      //FIXME
//      Debug.LogWarningFormat("** piston step {0}, piston[0] pos {1}, piston[last] pos {2}, dist {3}, pistons {4}",
//                             step,
//                             pistons[0].transform.position,
//                             pistons.Last().transform.position,
//                             Vector3.Distance(pistons.Last().transform.position, pistons[0].transform.position),
//                             pistons.Length);
      for (var i = 1; i < scalablePistons; ++i) {
        offset -= step;  // Pistons are distributed to -Z direction of the pviot.
        pistons[i].transform.localPosition = new Vector3(0, 0, offset);
      }
    }
  }

  #region Private utility methods
  string ExtractPositionName(string cfgDirectionString) {
    var lastCommaPos = cfgDirectionString.LastIndexOf(',');
    return lastCommaPos != -1
        ? cfgDirectionString.Substring(lastCommaPos + 1)
        : cfgDirectionString;
  }

  Vector3 ExtractOrientationVector(string cfgDirectionString) {
    var lastCommaPos = cfgDirectionString.LastIndexOf(',');
    if (lastCommaPos == -1) {
      Debug.LogWarningFormat("Cannot extract direction from string: {0}", cfgDirectionString);
      return Vector3.forward;
    }
    return ConfigNode.ParseVector3(cfgDirectionString.Substring(0, lastCommaPos));
  }

  void UpdateMenuItems() {
    Events[parkedOrientationMenuAction0].active =
        ExtractPositionName(parkedOrientationMenu0) != "" && !isLinked;
    Events[parkedOrientationMenuAction0].guiActiveEditor =
        Events[parkedOrientationMenuAction0].active;
    Events[parkedOrientationMenuAction1].active =
        ExtractPositionName(parkedOrientationMenu1) != "" && !isLinked;
    Events[parkedOrientationMenuAction1].guiActiveEditor =
        Events[parkedOrientationMenuAction1].active;
    Events[parkedOrientationMenuAction2].active =
        ExtractPositionName(parkedOrientationMenu2) != "" && !isLinked;
    Events[parkedOrientationMenuAction2].guiActiveEditor =
        Events[parkedOrientationMenuAction2].active;
  }
  #endregion
}

}  // namespace
