// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Linq;
using UnityEngine;

namespace KSPDev.ModelUtils {

/// <summary>Various tools to deal with procedural colliders.</summary>
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
  /// <remarks>
  /// Intended to create one fast collider at the cost of precision. All the meshes in the parent
  /// childs (including the parent) are processed to produce a boundary box. Then, this box is
  /// applied to the requested primitive type that defines the shape of the collider.
  /// <para>
  /// Note, that rdaius if sphere and capsule is the same in both X and Y axis. If combined boundary
  /// box has any of the dimensions significantly different then it makes sense to choose a
  /// different collider type. Or break down the hirearchy into more colliders.
  /// </para>
  /// </remarks>
  /// <param name="parent">Parent object.</param>
  /// <param name="type">
  /// Type of the primitive mesh which is the best for wrapping all the meshes of the object. Only
  /// <see cref="PrimitiveType.Cube"/>, <see cref="PrimitiveType.Capsule"/>, and
  /// <see cref="PrimitiveType.Sphere"/> are supported.
  /// </param>
  /// <param name="inscribeBoundaryIntoCollider">
  /// If <c>true</c> then collider will define the outer boundaries so what all the meshes are
  /// inside the volume. Otherwise, the combined meshes box will define the outer boundary of the
  /// collider. It only makes sense for the colliders other than <see cref="PrimitiveType.Cube"/>.
  /// </param>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/GameObject.html">
  /// Unity 3D: GameObject</seealso>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/PrimitiveType.html">
  /// Unity 3D: PrimitiveType</seealso>
  public static void SetSimpleCollider(GameObject parent, PrimitiveType type,
                                       bool inscribeBoundaryIntoCollider = true) {
    parent.GetComponentsInChildren<Collider>().ToList()
        .ForEach(UnityEngine.Object.Destroy);

    // Get bounds of all renderers in the parent. The bounds come in world's coordinates, so
    // translate them into parent's local space before encapsulating. 
    var renderers = parent.GetComponentsInChildren<Renderer>();
    var combinedBounds = default(Bounds);
    foreach (var renderer in renderers) {
      var bounds = renderer.bounds;
      bounds.center = parent.transform.InverseTransformPoint(bounds.center);
      bounds.size = parent.transform.rotation.Inverse() * bounds.size;
      combinedBounds.Encapsulate(bounds);
    }

    // Add collider basing on the requested type.
    if (type == PrimitiveType.Cube) {
      var collider = parent.AddComponent<BoxCollider>();
      collider.center = combinedBounds.center;
      collider.size = combinedBounds.size;
    } else if (type == PrimitiveType.Capsule) {
      // TODO(ihsoft): Choose direction so what the volume is minimized.
      var collider = parent.AddComponent<CapsuleCollider>();
      collider.center = combinedBounds.center;
      collider.direction = 2;  // Z axis
      collider.height = combinedBounds.size.z;
      collider.radius = inscribeBoundaryIntoCollider
          ? Mathf.Max(combinedBounds.extents.x, combinedBounds.extents.y)
          : Mathf.Min(combinedBounds.extents.x, combinedBounds.extents.y);
    } else if (type == PrimitiveType.Sphere) {
      var collider = parent.AddComponent<SphereCollider>();
      collider.center = combinedBounds.center;
      collider.radius = inscribeBoundaryIntoCollider
          ? Mathf.Max(combinedBounds.extents.x, combinedBounds.extents.y)
          : Mathf.Min(combinedBounds.extents.x, combinedBounds.extents.y);
    } else {
      Debug.LogErrorFormat("Unsupported collider: {0}. Ignoring", type);
    }
  }

  /// <summary>Sets the specified values to colliders of all the objects in the part's model.
  /// </summary>
  /// <param name="parent">Game object to start searching for renderers from.</param>
  /// <param name="isPhysical">
  /// If <c>true</c> then collider will trigger physical effects. If <c>false</c> then it will only
  /// trigger collision events. When it's <c>null</c> the collider setting won't be changed.
  /// </param>
  /// <param name="isEnabled">
  /// Defines if colliders should be enabled or disabled. When it's <c>null</c> the collider setting
  /// won't be changed.
  /// </param>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Collider.html">Unity3D: Collider
  /// </seealso>
  public static void UpdateColliders(GameObject parent,
                                     bool? isPhysical = null, bool? isEnabled = true) {
    foreach (var collider in parent.GetComponentsInChildren<Collider>()) {
      if (isPhysical.HasValue) {
        collider.isTrigger = !isPhysical.Value;
      }
      if (isEnabled.HasValue) {
        collider.enabled = isEnabled.Value;
      }
    }
  }

  /// <summary>Adds or adjusts a primitive collider on the mesh.</summary>
  /// <remarks>
  /// Type of the primitive collider is chosen basing on the primitive type.
  /// </remarks>
  /// <param name="primitive">Primitive game object to adjust.</param>
  /// <param name="meshSize">Size of the collider in local units.</param>
  /// <param name="colliderType">Determines how a collider type should be selected.</param>
  /// <param name="shapeType">
  /// Type of the primitive when <paramref name="colliderType"/> is
  /// <see cref="PrimitiveCollider.Shape"/>. It will determine the type of the collider. Only
  /// <see cref="PrimitiveType.Cylinder"/>, <see cref="PrimitiveType.Sphere"/>, and
  /// <see cref="PrimitiveType.Cube"/> are supported.
  /// </param>
  public static void AdjustCollider(
      GameObject primitive, Vector3 meshSize, PrimitiveCollider colliderType,
      PrimitiveType? shapeType = null) {
    UnityEngine.Object.Destroy(primitive.GetComponent<Collider>());
    if (colliderType == PrimitiveCollider.Mesh) {
      var collider = primitive.AddComponent<MeshCollider>();
      collider.convex = true;
    } else if (colliderType == PrimitiveCollider.Shape) {
      // FIXME: non tirival scales does't fit simple colliders. Fix it.
      if (shapeType.Value == PrimitiveType.Cylinder) {
        // TODO(ihsoft): Choose direction so what the volume is minimized.
        var collider = primitive.AddComponent<CapsuleCollider>();
        collider.direction = 2;  // Z axis
        collider.height = meshSize.z;  // It's now length.
        collider.radius = meshSize.x;
      } else if (shapeType.Value == PrimitiveType.Sphere) {
        var collider = primitive.AddComponent<SphereCollider>();
        collider.radius = meshSize.x;
      } else if (shapeType.Value == PrimitiveType.Cube) {
        var collider = primitive.AddComponent<BoxCollider>();
        collider.size = meshSize;
      } else {
        Debug.LogWarningFormat("Unknown primitive type {0}. Droppping collider.", shapeType.Value);
      }
    } else if (colliderType != PrimitiveCollider.Bounds) {
      SetSimpleCollider(primitive, PrimitiveType.Cube, inscribeBoundaryIntoCollider: true);
    } else if (colliderType != PrimitiveCollider.None) {
      Debug.LogWarningFormat(
          "Unsupported collider type {0}. Droppping whatever collider part had", colliderType);
    }
  }
}

}  // namespace
