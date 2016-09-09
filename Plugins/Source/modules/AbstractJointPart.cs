// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using System.Linq;
using UnityEngine;
using KSPDev.ModelUtils;

namespace KAS {

// FIXME: docs
public abstract class AbstractJointPart : AbstractDynamicPartModule {
  // These fileds must not be accessed outside of the module. They are declared public only
  // because KSP won't work otherwise. Ancenstors and external callers must access values via
  // interface properties. If property is not there then it means it's *intentionally* restricted
  // for the non-internal consumers.
  #region Part's config fields
  [KSPField]
  public string jointTexturePath = "";
  [KSPField]
  public Vector3 attachNodePosition = new Vector3(0, 0.0362f, 0);
  [KSPField]
  public Vector3 attachNodeOrientation = new Vector3(0, 1, 0);
  #endregion

  /// <summary>Returns transform of the actual joint model.</summary>
  /// <remarks>It's not just a position anchor. It's a transform of the real object that represents
  /// the joint model. Its rotation will be adjusted when establishing/updating the link.</remarks>
  public abstract Transform sourceTransform { get; set; }

  // FIXME: docs
  protected const string PivotAxileObjName = "PivotAxile";
  // FIXME: docs
  protected const string AttachNodeObjName = "AttachNode";

  #region Model sizes. Be CAREFUL modifying them!
  // These constants make joint model looking solid. Do NOT change them unless you fully understand
  // what is "joint base", "clutch holder" and "clutch". The values are interconnected, so changing
  // one will likely require adjusting some others.
  const float JointBaseDiameter = 0.10f;
  const float JointBaseHeigth = 0.02f;
  const float ClutchHolderThikness = 0.02f;
  const float ClutchHolderWidth = 0.10f;
  const float ClutchHolderLength = JointBaseHeigth + 0.05f + 0.01f;  // For max pipe 0.15f.
  const float ClutchThikness = 0.03f;
  const float ClutchAxileDiameter = 0.03f;
  const float ClutchAxleExtent = 0.005f;
  const float ClutchAxileLength = 2 * (ClutchThikness + ClutchAxleExtent);
  #endregion

  //FIXME implement
  public void MakeJointAttachNode() {
  }
  // FIXME: docs
  public void DestroyJointAttachNode() {
  }

  // FIXME: docs
  protected Transform CreateAttachNodeTransform() {
    var node = new GameObject(AttachNodeObjName).transform;
    node.parent = partModelTransform;
    node.localPosition = attachNodePosition;
    node.localScale = Vector3.one;
    node.localRotation = Quaternion.LookRotation(attachNodeOrientation);
    return node;
  }

  // FIXME: docs
  protected Transform CreateStrutJointModel(string transformName, bool createAxile = true) {
    // FIXME: use different materials.
    // FIXME: deal with collider
    var material = CreateMaterial(GetTexture(jointTexturePath));
    var jointTransform = new GameObject(transformName).transform;

    // Socket cap.
    var jointBase = Meshes.CreateBox(
        JointBaseDiameter, JointBaseDiameter, JointBaseHeigth, material, parent: jointTransform);
    jointBase.name = "base";
    jointBase.transform.localPosition = new Vector3(0, 0, JointBaseHeigth / 2);

    // Holding bar for the clutcth.
    var clutchHolder = Meshes.CreateBox(
        ClutchHolderThikness, ClutchHolderWidth, ClutchHolderLength, material,
        parent: jointBase.transform);
    clutchHolder.name = "clutchHolder";
    clutchHolder.transform.localPosition = new Vector3(
        ClutchHolderThikness / 2 + (ClutchThikness - ClutchHolderThikness),
        0,
        ClutchHolderLength / 2);

    // The clutch.
    var clutch = Meshes.CreateCylinder(
        ClutchHolderWidth, ClutchThikness, material, parent: clutchHolder.transform);
    clutch.name = "clutch";
    clutch.transform.localRotation = Quaternion.LookRotation(Vector3.left);
    clutch.transform.localPosition =
        new Vector3(-(ClutchThikness - ClutchHolderThikness) / 2, 0, ClutchHolderLength / 2);

    // Axile inside the clutch to join with the opposite part clutch.
    //FIXME
    var pivotTransform = new GameObject(PivotAxileObjName).transform;
    //var pivotTransform = new GameObject(pivotName).transform;
    pivotTransform.parent = jointTransform.transform;
    if (createAxile) {
      var clutchAxile = Meshes.CreateCylinder(
          ClutchAxileDiameter, ClutchAxileLength, material, parent: clutchHolder.transform);
      clutchAxile.name = "axile";
      clutchAxile.transform.localRotation = Quaternion.LookRotation(Vector3.left);
      clutchAxile.transform.localPosition =
          new Vector3(-clutchHolder.transform.localPosition.x, 0, ClutchHolderLength / 2);
      pivotTransform.localPosition =
          pivotTransform.InverseTransformPoint(clutchAxile.transform.position);
    } else {
//      const float stubAxileLength = ClutchThikness + 2 * ClutchAxleExtent;
//      var clutchAxile = CreateCylinder(
//          ClutchAxileDiameter, stubAxileLength, material, parent: clutchHolder.transform);
//      clutchAxile.name = "axile";
//      clutchAxile.transform.localRotation = Quaternion.LookRotation(Vector3.left);
//      clutchAxile.transform.localPosition =
//          new Vector3(-clutchHolder.transform.localPosition.x, 0, stubAxileLength / 2);
//      pivotTransform.localPosition =
//          pivotTransform.InverseTransformPoint(clutchAxile.transform.position);
      pivotTransform.localPosition = pivotTransform.InverseTransformPoint(
          clutchHolder.transform.TransformPoint(
              new Vector3(-clutchHolder.transform.localPosition.x, 0, ClutchHolderLength / 2)));
    }

    return jointTransform;
  }
}

}  // namespace
