// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using KASAPIv1;
using KSPDev.ModelUtils;

//FIXME: enrich docs with Unity3d links

namespace KAS {

//FIXME docs
interface IDynamicPart {
  Color cfgColor { get; }
  string cfgShaderName { get; }
//  Color? colorOverride { get; set; }
//  string shaderNameOverride { get; set; }
//  bool isPhysicalCollider { get; set; }
}

/// <summary>Base renderer for parts that dynamically create their model on load.</summary>
/// <remarks>This class offers common functionality for creating meshes in the model and loading
/// them when needed.</remarks>
public abstract class KASModuleDynamicPart : PartModule, IDynamicPart {
  //FIXME doc
  public const string KspPartShaderName = "KSP/Bumped Specular";

  #region Config settings.  
  /// <inheritdoc/>
  public virtual Color cfgColor { get; private set; }
  /// <inheritdoc/>
  public virtual string cfgShaderName { get; private set; }
  #endregion

  #region IDynamicPartRenderer properties
//  /// <inheritdoc/>
//  public virtual Color? colorOverride {
//    get { return _colorOverride; }
//    set {
//      _colorOverride = value;
//      UpdateMaterials(newColor: value ?? cfgColor);
//    }
//  }
//  Color? _colorOverride;
//
//  /// <inheritdoc/>
//  public virtual string shaderNameOverride {
//    get { return _shaderNameOverride; }
//    set {
//      _shaderNameOverride = value;
//      UpdateMaterials(newShaderName: value ?? cfgShaderName);
//    }
//  }
//  string _shaderNameOverride;
//
//  /// <inheritdoc/>
//  public virtual bool isPhysicalCollider {
//    get { return _isPhysicalCollider; }
//    set {
//      _isPhysicalCollider = value;
//      UpdateColliders(isPhysical: value);
//    }
//  }
//  bool _isPhysicalCollider;
  #endregion

  /// <summary>Returns cached model root transform.</summary>
  /// <remarks>Attach all your meshes to this transform (directly or via parents). Otherwise, the
  /// new meshes will be ignored by the part's model!</remarks>
  protected Transform partModelTransform {
    get {
      if (_partModelTransform == null) {
        _partModelTransform = part.FindModelTransform("model");
        //FIXME
        Debug.LogWarningFormat("** Found part model: {0}", _partModelTransform);
      }
      return _partModelTransform;
    }
  }
  Transform _partModelTransform;

  // These fields must not be accessed outside of the module. They are declared public only
  // because KSP won't work otherwise. Ancenstors and external callers must access values via
  // interface properties. If property is not there then it means it's *intentionally* restricted
  // for the non-internal consumers.
  #region Part's config fields
  [KSPField]
  public string shaderName = KspPartShaderName;
  [KSPField]
  public LinkCollider colliderType = LinkCollider.None;
  [KSPField]
  public Color color = Color.white;
  #endregion

  // Internal cache of the textures used by this renderer (and its descendants).
  readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
  // Full list of the renderers of this dynamic part.
  readonly Dictionary<GameObject, Renderer> renderers = new Dictionary<GameObject, Renderer>();

  #region PartModule overrides
  public override void OnAwake() {
    base.OnAwake();
    //FIXME
    Debug.LogWarningFormat("** ON AWAKE: {0}", part.name);
    if (HighLogic.LoadedScene != GameScenes.LOADING) {
      LoadPartModel();
    }
  }

  public override void OnStart(PartModule.StartState state) {
    base.OnStart(state);
    //FIXME
    Debug.LogWarningFormat("** ON START: {0}", part.name);
  }

  public override void OnLoad(ConfigNode node) {
    //FIXME
    Debug.LogWarningFormat("** ON LOAD: {0}", part.name);
    base.OnLoad(node);
    if (HighLogic.LoadedScene == GameScenes.LOADING) {
      CreatePartModel();
    }
  }
  #endregion

  #region Overridable functionality
  /// <summary>Creates part's model.</summary>
  /// <remarks>Called when it's time to create meshes in the part's model.</remarks>
  /// <seealso cref="partModelTransform"/>
  protected abstract void CreatePartModel();

  /// <summary>Loads part's model.</summary>
  /// <remarks>Called when parts is being instantiated.</remarks>
  /// <seealso cref="partModelTransform"/>
  protected abstract void LoadPartModel();
  #endregion

  #region Utility methods for the descendants
  //FIXME docs  
  protected GameObject CreateCylinder(float diameter, float length, Material material,
                                      Transform parent = null,
                                      LinkCollider colliderType = LinkCollider.None) {
    // Default length is 2m.
    return CreatePrimitive(
        PrimitiveType.Cylinder, new Vector3(diameter, diameter, length / 2), material,
        parent: parent);
  }

  // Width is measured along X axis. Height is measured along Y axis. Length is aligned to Z axis.
  //FIXME docs  
  protected GameObject CreateBox(float width, float height, float length, Material material,
                                 Transform parent = null,
                                 LinkCollider colliderType = LinkCollider.None) {
    return CreatePrimitive(
        PrimitiveType.Cube, new Vector3(width, height, length), material,
        parent: parent);
  }

  /// <summary>Creates an ideal sphere.</summary>
  /// <param name="diameter">Diameter of the sphere.</param>
  /// <param name="material">Material for the primitive.</param>
  /// <param name="parent">Parent transfrom to atatch primitive to. If <c>null</c> then new
  /// primitive will be attached to the model.</param>
  /// <param name="colliderType">Type of the collider to create on the primitive.</param>
  /// <returns>Sphere game object.</returns>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Material.html">Unity3D: Material
  /// </seealso>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Transform.html">Unity3D: Transform
  /// </seealso>
  protected GameObject CreateSphere(float diameter, Material material,
                                    Transform parent = null,
                                    LinkCollider colliderType = LinkCollider.None) {
    var obj = CreatePrimitive(
        PrimitiveType.Sphere, new Vector3(diameter, diameter, diameter), material,
        parent: parent);
    AdjustCollider(
        obj, PrimitiveType.Sphere, new Vector3(diameter, diameter, diameter), colliderType);
    return obj;
  }

  /// <summary>Creates a primitive mesh and attaches it to the model.</summary>
  /// <remarks>For <see cref="PrimitiveType.Cylinder"/> Z and Y axis will be swapped to make Z
  /// "the length".
  /// <para>Collider on the primitive will be destroyed. Consider using <see cref="AdjustCollider"/>
  /// to setup the right collider when needed.</para>
  /// </remarks>
  /// <param name="type">Type of the primitive.</param>
  /// <param name="meshScale">Scale to bring all mesh vertices to. New primitive have base size of
  /// 1m but some shapes may have exceptions (e.g. height of a cylinder is 2m). The scale is applied
  /// on the mesh, i.e. it's applied on the vertices, not the transform.</param>
  /// <param name="material">Material to substitute the default one. Consider using
  /// <see cref="CreateMaterial"/> to obtain it.</param>
  /// <param name="parent">Parent transform to attach primitive to. If <c>null</c> then new
  /// primitive will be attached to the part's model.</param>
  /// <returns>Game object of the new primitive.</returns>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/GameObject.CreatePrimitive.html">
  /// Unity3D: GameObject.CreatePrimitive</seealso>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Material.html">Unity3D: Material
  /// </seealso>
  /// FIXME: handle collider type
  /// FIXME: drop type in favor of specialized methods.
  protected GameObject CreatePrimitive(PrimitiveType type, Vector3 meshScale, Material material,
                                       Transform parent = null) {
    var primitive = GameObject.CreatePrimitive(type);
    DestroyImmediate(primitive.GetComponent<Collider>());
    Hierarchy.MoveToParent(primitive.transform, parent ?? partModelTransform);
    primitive.GetComponent<Renderer>().material = material;

    // Make object's Z axis its length. For this rotate around X axis.
    //var meshRotation = Quaternion.Euler(90, 0, 0);
    var meshRotation =
        type == PrimitiveType.Cylinder ? Quaternion.Euler(90, 0, 0) : Quaternion.identity;
    var meshFilter = primitive.GetComponent<MeshFilter>();
    // For some reason shared mesh refuses to properly react to the vertices updates (Unity
    // optimziation?), so create a mesh copy and adjust it. It results in a loss of a bit of
    // performance and memory but given it's only done on the scene load it's fine.
    var mesh = meshFilter.mesh;  // Do NOT use sharedMesh here!
    // Changing of mesh vertices/normals *must* follow read/modify/store contract. Read Unity docs
    // for more details.
    var vertices = mesh.vertices;
    var normals = mesh.normals;
    for (var i = 0; i < mesh.vertexCount; ++i) {
      vertices[i] = meshRotation * vertices[i];
      vertices[i].Scale(meshScale);
      normals[i] = meshRotation * normals[i];
    }
    mesh.vertices = vertices;
    mesh.normals = normals;
    mesh.RecalculateBounds();
    mesh.RecalculateNormals();
    mesh.Optimize();  // We're not going to modify it further.
    meshFilter.sharedMesh = mesh;

    return primitive;
  }

  //FIXME: docs
  protected static void AdjustCollider(
      GameObject primitive, PrimitiveType type, Vector3 meshSize, LinkCollider colliderType) {
    var existingCollider = primitive.GetComponent<Collider>();
    if (colliderType == LinkCollider.None) {
      if (existingCollider != null) {
        existingCollider.gameObject.DestroyGameObjectImmediate();
      }
      return;
    }
    if (colliderType == LinkCollider.Mesh) {
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
    if (colliderType == LinkCollider.Shape) {
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
        DestroyImmediate(primitive.GetComponent<Collider>());
      }
    }
    Debug.LogWarningFormat(
        "Unsupported collider type {0}. Droppping whatever collider part had: {1}",
        colliderType, existingCollider);
    if (existingCollider != null) {
      existingCollider.gameObject.DestroyGameObjectImmediate();
    }
  }

  /// <summary>Creates a material with current color and shader settings.</summary>
  /// <param name="mainTex">Main texture of the material.</param>
  /// <returns>New material.</returns>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Texture2D.html">Unity3D: Texture2D
  /// </seealso>
  /// <seealso href="https://docs.unity3d.com/Manual/MaterialsAccessingViaScript.html">Unity3D:
  /// Dealing with materials from scripts.</seealso>
  protected Material CreateMaterial(Texture2D mainTex) {
    var material = new Material(Shader.Find(shaderName));
    material.mainTexture = mainTex;
    material.color = color;
    return material;
  }

  /// <summary>Gets the texture from either KSP gamebase or the internal cache.</summary>
  /// <remarks>It's OK to call this method in the performance demanding code since once texture is
  /// successfully returned it's cached internally. The subsequent calls won't issue expensive game
  /// database requests.</remarks>
  /// <param name="textureFileName">Filename of the texture file. The path is realtive to "GameData"
  /// folder. Can be PNG or DDS.</param>
  /// <param name="asNormalMap">if <c>true</c> then etxture will be loaded as a bumpmap.</param>
  /// <returns>The texture. Note that it's a shared object. Don't execute actions on it which you
  /// don't want to affect other meshes in the game.</returns>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Texture2D.html">Unity3D: Texture2D
  /// </seealso>
  protected Texture2D GetTexture(string textureFileName, bool asNormalMap = false) {
    var texName = textureFileName + (asNormalMap ? "_NormalMap" : "");
    Texture2D tubeTexture;
    if (!textures.TryGetValue(textureFileName, out tubeTexture)) {
      tubeTexture = GameDatabase.Instance.GetTexture(textureFileName, asNormalMap);
      if (tubeTexture == null) {
        // Use "red" texture if no file fiound.
        Debug.LogWarningFormat("Cannot load texture: {0}", textureFileName);
        tubeTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
        tubeTexture.SetPixels(new[] {Color.red});
        tubeTexture.Apply();
      }
      tubeTexture.Compress(true /* highQuality */);
      textures[textureFileName] = tubeTexture;
    }
    return tubeTexture;
  }
  #endregion
}

}  // namespace
