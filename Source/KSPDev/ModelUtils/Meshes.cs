// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Linq;
using UnityEngine;

namespace KSPDev.ModelUtils {

/// <summary>Various tools to deal with procedural meshes.</summary>
public static class Meshes {
  /// <summary>
  /// Rescales texture so what one sample covers exactly one unit of the primitive length.
  /// </summary>
  /// <remarks>
  /// Normally one texture sample covers the whole primitive regardless to its length. By calling
  /// this method you ensure that one sample keeps its ratio comparing to a linear unit. If
  /// primitive is too short to fit the texture then the texture is truncated. If primitive is too
  /// long to be covered by one sample then the texture will be tiled to fill the space.
  /// <para>
  /// This methods assumes UV data on the primitive was created for a length of 1m. With this
  /// assumption in mind the Z axis of the local scale is cosidered "the length".
  /// <see cref="CreatePrimitive"/> method guarantees that newly created primitive always has length
  /// of one meter but if primitive was created by other means its default length can be different.
  /// </para>
  /// </remarks>
  /// <param name="obj">Game object to adjust material on. Z axis scale gives the length.</param>
  /// <param name = "lengthUnit">Length to be completly covered by one sample of the texture.
  /// </param>
  /// <param name="renderer">Specific renderer to adjust texture in. If <c>null</c> then first
  /// renderer on the object will be updated. Note, that getting renderer from the object is an
  /// expensive operation. When performance is the key it makes sense caching the renderer, and
  /// passing it in every rescale call.</param>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Renderer.html">Unity3D: Renderer
  /// </seealso>
  public static void RescaleTextureToLength(
      GameObject obj, float lengthUnit = 1.0f, Renderer renderer = null) {
    var newScale = lengthUnit / obj.transform.localScale.z;
    var mr = renderer ?? obj.GetComponent<Renderer>();
    mr.material.mainTextureScale = new Vector2(mr.material.mainTextureScale.x, newScale);
  }

  /// <summary>
  /// Sets the specified values to material of all the renderers in the part's model.
  /// </summary>
  /// <remarks>
  /// Shared material is affected, so if there are unrelated meshes that use the same material then
  /// they will be affected as well. In general, it's a bad idea to share material between several
  /// parts. And it's a good practice to share materials within the same part.
  /// </remarks>
  /// <param name="parent">Game object to start searching for renderers from.</param>
  /// <param name="newShaderName">
  /// New shader name. If <c>null</c> then it will not be changed.
  /// </param>
  /// <param name="newColor">Color to set. If <c>null</c> then it will not be changed.</param>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Shader.html">Unity3D: Shader</seealso>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Material.html">Unity3D: Material
  /// </seealso>
  public static void UpdateMaterials(GameObject parent,
                                     string newShaderName = null, Color? newColor = null) {
    if (newShaderName != null || newColor.HasValue) {
      foreach (var renderer in parent.GetComponentsInChildren<Renderer>()) {
        if (newShaderName != null) {
          renderer.sharedMaterial.shader = Shader.Find(newShaderName);
        }
        if (newColor.HasValue) {
          renderer.sharedMaterial.color = newColor.Value;
        }
      }
    }
  }

  /// <summary>Creates a cylinder.</summary>
  /// <param name="diameter">XY of the cylinder.</param>
  /// <param name="length">Z-axis of the cylinder.</param>
  /// <param name="material">Material for the primitive.</param>
  /// <param name="parent">Parent transfrom to atatch primitive to.</param>
  /// <param name="colliderType">Type of the collider to create on the primitive.</param>
  /// <returns>Sphere game object.</returns>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Material.html">Unity3D: Material
  /// </seealso>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Transform.html">Unity3D: Transform
  /// </seealso>
  public static GameObject CreateCylinder(
      float diameter, float length, Material material,Transform parent,
      Colliders.PrimitiveCollider colliderType = Colliders.PrimitiveCollider.None) {
    // Default length scale is 2.0.
    var scale = new Vector3(diameter, diameter, length / 2);
    var obj = CreatePrimitive(PrimitiveType.Cylinder, scale, material, parent: parent);
    Colliders.AdjustCollider(obj, scale, colliderType, shapeType: PrimitiveType.Cylinder);
    return obj;
  }

  /// <summary>Creates a box.</summary>
  /// <param name="width">X-axis of the box.</param>
  /// <param name="height">Y-axis of the box.</param>
  /// <param name="length">Z-axis of the box.</param>
  /// <param name="material">Material for the primitive.</param>
  /// <param name="parent">Parent transfrom to atatch primitive to.</param>
  /// <param name="colliderType">Type of the collider to create on the primitive.</param>
  /// <returns>Sphere game object.</returns>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Material.html">Unity3D: Material
  /// </seealso>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Transform.html">Unity3D: Transform
  /// </seealso>
  public static GameObject CreateBox(
      float width, float height, float length, Material material, Transform parent,
      Colliders.PrimitiveCollider colliderType = Colliders.PrimitiveCollider.None) {
    var scale = new Vector3(width, height, length);
    var obj = CreatePrimitive(PrimitiveType.Cube, scale, material, parent: parent);
    Colliders.AdjustCollider(obj, scale, colliderType, shapeType: PrimitiveType.Cube);
    return obj;
  }

  /// <summary>Creates an ideal sphere.</summary>
  /// <param name="diameter">Diameter of the sphere.</param>
  /// <param name="material">Material for the primitive.</param>
  /// <param name="parent">Parent transfrom to atatch primitive to.</param>
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
  /// <param name="type">Type of the primitive.</param>
  /// <param name="meshScale">
  /// Scale to bring all mesh vertices to. New primitive have base size of 1m but some shapes may
  /// have exceptions (e.g. height of a cylinder is 2m). The scale is applied on the mesh, i.e. it's
  /// applied on the vertices, not the transform.</param>
  /// <param name="material">Material to use for the primitive.</param>
  /// <param name="parent">Parent transform to attach primitive to.</param>
  /// <returns>Game object of the new primitive.</returns>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/GameObject.CreatePrimitive.html">
  /// Unity3D: GameObject.CreatePrimitive</seealso>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Material.html">Unity3D: Material
  /// </seealso>
  public static GameObject CreatePrimitive(
      PrimitiveType type, Vector3 meshScale, Material material, Transform parent) {
    var primitive = GameObject.CreatePrimitive(type);
    UnityEngine.Object.DestroyImmediate(primitive.GetComponent<Collider>());
    Hierarchy.MoveToParent(primitive.transform, parent);
    primitive.GetComponent<Renderer>().material = material;

    // Make object's Z axis its length. For this rotate around X axis.
    var meshRotation =
        type == PrimitiveType.Cylinder ? Quaternion.Euler(90, 0, 0) : Quaternion.identity;
    TranslateMesh(primitive, rotation: meshRotation, scale: meshScale);
    return primitive;
  }

  /// <summary>Translates meshes's verticies.</summary>
  /// <remarks>
  /// This is different from setting postion, rotation and scale to the transform. This method
  /// actually changes vetricies in the mesh. It's not performance effective, so avoid doing it
  /// frequiently.
  /// </remarks>
  /// <param name="model">Model object to change mesh in.</param>
  /// <param name="offset">
  /// Offset for the verticies. If not specified then offset is zero. Offset is added <i>after</i>
  /// scale and rotation have been applied.  
  /// </param>
  /// <param name="rotation">
  /// Rotation for the verticies. If not set then no rotation is added.
  /// </param>
  /// <param name="scale">
  /// Scale for the vertex positions. If not specified then scale is not affected.
  /// </param>
  public static void TranslateMesh(GameObject model,
                                   Vector3? offset = null, Quaternion? rotation = null,
                                   Vector3? scale = null) {
    var meshPosition = offset ?? Vector3.zero;
    var meshRotation = rotation ?? Quaternion.identity;
    var meshScale = scale ?? Vector3.one;
    var meshFilter = model.GetComponent<MeshFilter>();
    // For some reason shared mesh refuses to properly react to the vertices updates (Unity
    // optimization?), so create a mesh copy and adjust it. It results in a loss of a bit of
    // performance and memory.
    var mesh = meshFilter.mesh;  // Do NOT use sharedMesh here!
    // Changing of mesh vertices/normals *must* follow read/modify/store contract. Read Unity docs
    // for more details.
    var vertices = mesh.vertices;
    var normals = mesh.normals;
    
    for (var i = 0; i < mesh.vertexCount; ++i) {
      vertices[i] = meshRotation * vertices[i];
      vertices[i].Scale(meshScale);
      vertices[i] += meshPosition;
      normals[i] = meshRotation * normals[i];
    }
    mesh.vertices = vertices;
    mesh.normals = normals;
    mesh.RecalculateBounds();
    mesh.RecalculateNormals();
    mesh.Optimize();  // We're not going to modify it further.
    meshFilter.sharedMesh = mesh;
  }
}

}  // namespace
