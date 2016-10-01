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
public abstract class AbstractJointPart : AbstractProceduralModel {
  // These fileds must not be accessed outside of the module. They are declared public only
  // because KSP won't work otherwise. Ancenstors and external callers must access values via
  // interface properties. If property is not there then it means it's *intentionally* restricted
  // for the non-internal consumers.
  //FIXME drop atatch node fields, move to descendats, and rename
  #region Part's config fields
  [KSPField]
  public string jointTexturePath = "";
  [KSPField]
  public Vector3 attachNodePosition = Vector3.zero;
  [KSPField]
  public Vector3 attachNodeOrientation = Vector3.up;
  #endregion

  /// <summary>Returns transform of the actual joint model.</summary>
  /// <remarks>It's not just a position anchor. It's a transform of the real object that represents
  /// the joint model. Its rotation will be adjusted when establishing/updating the link.</remarks>
  /// FIXME: dones't seem we use it. DROP!
  public abstract Transform sourceTransform { get; set; }

  /// <summary>
  /// Name of the transform that is used to conenct two levers to form a complete joint. 
  /// </summary>
  protected const string PivotAxileObjName = "PivotAxile";

  #region Model sizes. Be CAREFUL modifying them!
  // These constants make joint model looking solid. Do NOT change them unless you fully understand
  // what is "joint base", "clutch holder" and "clutch". The values are interconnected, so changing
  // one will likely require adjusting some others.
  const float JointBaseDiameter = 0.10f;
  const float JointBaseHeigth = 0.02f;
  const float ClutchHolderThikness = 0.02f;
  const float ClutchHolderWidth = 0.10f;
  const float ClutchHolderLength = 0.05f + 0.01f;
  const float ClutchThikness = 0.03f;
  const float ClutchAxileDiameter = 0.03f;
  const float ClutchAxleExtent = 0.005f;
  const float ClutchAxileLength = 2 * (ClutchThikness + ClutchAxleExtent);
  #endregion

  /// <summary>Dynamically creates model for a joint lever.</summary>
  /// <remarks>Transfrom where two levers can connect is named <see cref="PivotAxileObjName"/>. To
  /// make a complete joint model align pivot axiles of the levers, and rotate one of the levers 180
  /// degrees around Z axis to match the clutches.
  /// <para>All details of the model get populated with main texure <see cref="jointTexturePath"/>.
  /// </para>
  /// <para>Model won't have any colliders setup. Consider using
  /// <see cref="Colliders.SetSimpleCollider"/> on the newly created model to enable collider.
  /// </para>
  /// </remarks>
  /// <param name="transformName">Trasnfrom name of the new lever. Use different names for the
  /// levers to be able loading them on part model load.</param>
  /// <param name="createAxile">If <c>true</c> then axile model will be created, and it will be the
  /// axile tansfrom. Otherwise, the axile transfrom will be an emopty object. Only one lever in the
  /// connection should have axile model.</param>
  /// <returns>Newly created joint lever model. In order to be visible and accessible on the part
  /// the models must be attached to <see cref="partModelTransform"/>.</returns>
  protected Transform CreateStrutJointModel(string transformName, bool createAxile = true) {
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
        (ClutchHolderLength + JointBaseHeigth) / 2);

    // The clutch.
    var clutch = Meshes.CreateCylinder(
        ClutchHolderWidth, ClutchThikness, material, parent: clutchHolder.transform);
    clutch.name = "clutch";
    clutch.transform.localRotation = Quaternion.LookRotation(Vector3.left);
    clutch.transform.localPosition =
        new Vector3(-(ClutchThikness - ClutchHolderThikness) / 2, 0, ClutchHolderLength / 2);

    // Axile inside the clutch to join with the opposite joint clutch.
    var pivotTransform = new GameObject(PivotAxileObjName).transform;
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
      pivotTransform.localPosition = pivotTransform.InverseTransformPoint(
          clutchHolder.transform.TransformPoint(
              new Vector3(-clutchHolder.transform.localPosition.x, 0, ClutchHolderLength / 2)));
    }

    return jointTransform;
  }
}

}  // namespace
