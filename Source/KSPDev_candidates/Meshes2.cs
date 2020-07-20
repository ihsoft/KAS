// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using UnityEngine;

// ReSharper disable once CheckNamespace
namespace KSPDev.ModelUtils {

/// <summary>Various tools to deal with procedural meshes.</summary>
public static class Meshes2 {
  /// <summary>Creates a cylinder.</summary>
  /// <param name="diameter">XY of the cylinder.</param>
  /// <param name="length">Z-axis of the cylinder.</param>
  /// <param name="material">Material for the primitive.</param>
  /// <param name="parent">Parent transform to attach primitive to.</param>
  /// <param name="colliderType">Type of the collider to create on the primitive.</param>
  /// <returns>Sphere game object.</returns>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Material.html">Unity3D: Material
  /// </seealso>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Transform.html">Unity3D: Transform
  /// </seealso>
  public static GameObject CreateCylinder(
      float diameter, float length, Material material, Transform parent,
      Colliders.PrimitiveCollider colliderType = Colliders.PrimitiveCollider.None) {
    // Default length scale is 2.0.
    var obj = CreatePrimitive(
        PrimitiveType.Cylinder, new Vector3(diameter, diameter, length / 2),
        material, parent: parent);
    Colliders.AdjustCollider(
        obj, new Vector3(diameter, diameter, length),
        colliderType, shapeType: PrimitiveType.Cylinder);
    return obj;
  }

  /// <summary>Creates an ideal sphere.</summary>
  /// <param name="diameter">Diameter of the sphere.</param>
  /// <param name="material">Material for the primitive.</param>
  /// <param name="parent">Parent transform to attach primitive to.</param>
  /// <param name="colliderType">Type of the collider to create on the primitive.</param>
  /// <returns>Sphere game object.</returns>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Material.html">Unity3D: Material
  /// </seealso>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Transform.html">Unity3D: Transform
  /// </seealso>
  public static GameObject CreateSphere(
      float diameter, Material material, Transform parent,
      Colliders.PrimitiveCollider colliderType = Colliders.PrimitiveCollider.None) {
    var scale =  new Vector3(diameter, diameter, diameter);
    var obj = CreatePrimitive(PrimitiveType.Sphere, scale, material, parent: parent);
    Colliders.AdjustCollider(obj, scale, colliderType, shapeType: PrimitiveType.Sphere);
    return obj;
  }

  /// <summary>Creates a primitive mesh and attaches it to the model.</summary>
  /// <remarks>
  /// For <see cref="PrimitiveType.Cylinder"/> Z and Y axis will be swapped to make Z "the length".
  /// <para>
  /// Collider on the primitive will be destroyed. Consider using
  /// <see cref="Colliders.AdjustCollider"/> to setup the right collider when needed.
  /// </para>
  /// </remarks>
  /// <param name="type">The type of the primitive.</param>
  /// <param name="meshScale">
  /// The scale to bring all the mesh vertices to. The scale is applied on the mesh, i.e. it's
  /// applied on the vertices, not the transform.
  /// </param>
  /// <param name="material">The material to use for the primitive.</param>
  /// <param name="parent">The parent transform to attach the primitive to.</param>
  /// <returns>The game object of the new primitive.</returns>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/GameObject.CreatePrimitive.html">
  /// Unity3D: GameObject.CreatePrimitive</seealso>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Material.html">Unity3D: Material
  /// </seealso>
  public static GameObject CreatePrimitive(
      PrimitiveType type, Vector3 meshScale, Material material, Transform parent) {
    var primitive = GameObject.CreatePrimitive(type);
    var collider = primitive.GetComponent<Collider>();
    collider.enabled = false;
    Object.Destroy(collider);
    Hierarchy.MoveToParent(primitive.transform, parent);
    primitive.GetComponent<Renderer>().material = material;

    // Make object's Z axis its length. For this rotate around X axis.
    var meshRotation =
        type == PrimitiveType.Cylinder ? Quaternion.Euler(90, 0, 0) : Quaternion.identity;
    Meshes.TranslateMesh(primitive, rotation: meshRotation, scale: meshScale);
    return primitive;
  }
}

}  // namespace
