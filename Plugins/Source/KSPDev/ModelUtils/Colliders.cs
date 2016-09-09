// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.
using System;
using System.Linq;
using UnityEngine;

namespace KSPDev.ModelUtils {

public static class Colliders {
  /// <summary>Defines how collisions should be checked on a primitive.</summary>
  public enum PrimitiveCollider {
    /// <summary>No collisions check.</summary>
    None,
    /// <summary>Check collisions basing on the mesh. It's performance expensive.</summary>
    /// <seealso href="https://docs.unity3d.com/ScriptReference/MeshCollider.html">
    /// Unity3D: MeshCollider</seealso>
    Mesh,
    /// <summary>Simple collider which fits the primitive type. It's performance optimized.</summary>
    /// <seealso href="https://docs.unity3d.com/ScriptReference/PrimitiveType.html">
    /// Unity3D: PrimitiveType</seealso>
    Shape,
    /// <summary>Simple collider which wraps all mesh vertexes. It's performance optimized.</summary>
    Bounds,
  }

  /// <summary>Drops colliders in all children objects, and adds one big collider to the parent.
  /// </summary>
  /// <remarks>Intended to create one fast collider at the cost of precision. All the meshes in the
  /// objects are processed to obtain a boundary box. Then, this box applied to the requested
  /// primitive type that specifies the shape of the collider.
  /// <para>Note, that sphere collider is always an ideal sphere. If combined boundary box has any
  /// of the dimensions significantly different then it makes sense to choose different type.</para>
  /// </remarks>
  /// <param name="parent">Parent object.</param>
  /// <param name="type">Type of the primitive mesh which is the best for wrapping all the meshes of
  /// the object.</param>
  /// <param name="inscribeBoundaryIntoCollider">If <c>true</c> then collider will define the outer
  /// boundaries so what all the meshes are inside the volume. Otherwise, the combined meshes box
  /// will define the outer boundary of the collider.</param>
  public static void SetSimpleCollider(GameObject parent, PrimitiveType type,
                                       bool inscribeBoundaryIntoCollider = true) {
    // FIXME: implement.
  }

  /// <summary>Sets the specified values to colliders of all the objects in the part's model.
  /// </summary>
  /// <param name="parent">Game object to start searching for renderers from.</param>
  /// <param name="isPhysical">If <c>true</c> then collider will trigger physical effects. If
  /// <c>false</c> then it will only trigger collision events.</param>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Collider.html">Unity3D: Collider
  /// </seealso>
  public static void UpdateColliders(GameObject parent, bool isPhysical) {
    foreach (var collider in parent.GetComponentsInChildren<Collider>()) {
      collider.isTrigger = !isPhysical;
    }
  }

  //FIXME: docs
  public static void AdjustCollider(
      GameObject primitive, PrimitiveType type, Vector3 meshSize, PrimitiveCollider colliderType) {
    var existingCollider = primitive.GetComponent<Collider>();
    if (colliderType == PrimitiveCollider.None) {
      if (existingCollider != null) {
        existingCollider.gameObject.DestroyGameObjectImmediate();
      }
      return;
    }
    if (colliderType == PrimitiveCollider.Mesh) {
      if (existingCollider != null && existingCollider.GetType() != typeof(MeshCollider)) {
        existingCollider.gameObject.DestroyGameObjectImmediate();
        existingCollider = null;
      }
      if (existingCollider == null) {
        var collider = primitive.AddComponent<MeshCollider>();
        collider.convex = true;
      }
      return;
    }
    if (colliderType == PrimitiveCollider.Shape) {
      // FIXME: non tirival scales does't fit simple colliders. Fix it.
      if (type == PrimitiveType.Cylinder) {
        var collider = primitive.AddComponent<CapsuleCollider>();
        collider.direction = 2;  // Z axis
        collider.height = meshSize.z;  // It's now length.
        collider.radius = meshSize.x;
      } else if (type == PrimitiveType.Sphere) {
        var collider = primitive.AddComponent<SphereCollider>();
        collider.radius = meshSize.x;
      } else if (type == PrimitiveType.Cube) {
        var collider = primitive.AddComponent<BoxCollider>();
        collider.size = meshSize;
      } else {
        Debug.LogWarningFormat("Unknown primitive type {0}. Droppping collider.", type);
        UnityEngine.Object.DestroyImmediate(primitive.GetComponent<Collider>());
      }
    }
    Debug.LogWarningFormat(
        "Unsupported collider type {0}. Droppping whatever collider part had: {1}",
        colliderType, existingCollider);
    if (existingCollider != null) {
      UnityEngine.Object.DestroyImmediate(existingCollider);
    }
  }
}

}  // namespace
