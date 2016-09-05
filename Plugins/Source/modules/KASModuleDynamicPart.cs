// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using KASAPIv1;

//FIXME: enrich docs with Unity3d links

namespace KAS {

//FIXME docs
interface IDynamicPart {
  Color cfgColor { get; }
  string cfgShaderName { get; }
  Color? colorOverride { get; set; }
  string shaderNameOverride { get; set; }
  bool isPhysicalCollider { get; set; }
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
  /// <inheritdoc/>
  public virtual Color? colorOverride {
    get { return _colorOverride; }
    set {
      _colorOverride = value;
      UpdateMaterials(newColor: value ?? cfgColor);
    }
  }
  Color? _colorOverride;

  /// <inheritdoc/>
  public virtual string shaderNameOverride {
    get { return _shaderNameOverride; }
    set {
      _shaderNameOverride = value;
      UpdateMaterials(newShaderName: value ?? cfgShaderName);
    }
  }
  string _shaderNameOverride;

  /// <inheritdoc/>
  public virtual bool isPhysicalCollider {
    get { return _isPhysicalCollider; }
    set {
      _isPhysicalCollider = value;
      UpdateColliders(isPhysical: value);
    }
  }
  bool _isPhysicalCollider;
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

  /// <summary>Sets the specified values to material of all the renderers in the part's model.
  /// </summary>
  /// <remarks>Shared material is affected, so if there are unrelated meshes that use the same
  /// material then they will be affected as well. In general, it's a bad idea to share material
  /// between several parts. And it's a good practice to share materials within the same part.
  /// </remarks>
  /// <param name="newShaderName">New shader name. If <c>null</c> then it will not be changed.
  /// </param>
  /// <param name="newColor">Color to set. If <c>null</c> then it will not be changed.</param>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Shader.html">Unity3D: Shader</seealso>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Material.html">Unity3D: Material
  /// </seealso>
  protected virtual void UpdateMaterials(string newShaderName = null, Color? newColor = null) {
    //FIXME: shared material may not work
    foreach (var renderer in partModelTransform.gameObject.GetComponents<Renderer>()) {
      if (newShaderName != null) {
        renderer.sharedMaterial.shader = Shader.Find(newShaderName);
      }
      if (newColor.HasValue) {
        renderer.sharedMaterial.color = newColor.Value;
      }
    }
  }

  /// <summary>Sets the specified values to colliders of all the objects in the part's model.
  /// </summary>
  /// <param name="isPhysical">If <c>true</c> then collider will trigger physical effects. If
  /// <c>false</c> then it will only trigger collision events. If <c>null</c> setting will not be
  /// changed.</param>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Collider.html">Unity3D: Collider
  /// </seealso>
  protected virtual void UpdateColliders(bool? isPhysical = null) {
    foreach (var collider in partModelTransform.gameObject.GetComponents<Collider>()) {
      if (isPhysical.HasValue && collider != null) {
        collider.isTrigger = !isPhysical.Value;
      }
    }
  }
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
    MoveToParent(primitive.transform, parent ?? partModelTransform);
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
  protected void AdjustCollider(
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

  /// <summary>Drops colliders in all children objects, and adds one big collider to the parent. 
  /// </summary>
  /// <remarks>Intended to create one fast collider at the cost of precision. All the meshes in the
  /// objects are processed to obtain a boundary box. Then, this box applied to the requested
  /// primitive type that specifies the shape of the collider.
  /// <para>Note, that sphere collider is always an ideal sphere. If combined boundary box has any
  /// of the dimensions significantly different then it makes sanes to choose different type.</para>
  /// </remarks>
  /// <param name="obj">Parent object.</param>
  /// <param name="type">Type of the primitive mesh which is the best for wrapping all the meshes of
  /// the object.</param>
  /// <param name="inscribeBoundaryIntoCollider">If <c>true</c> then collider will define the outer
  /// boundaries so what all the meshes are inside the volume. Otherwise, the combined meshes box
  /// will define the outer boundary of the collider.</param>
  protected void SetSimpleCollider(GameObject obj, PrimitiveType type,
                                   bool inscribeBoundaryIntoCollider = true) {
    // FIXME: implement.
  }

  /// <summary>Changes transform's parent keeping local postion, rotation and scale.</summary>
  /// <remarks>Normally, Unity preserves world position, rotation and scale when changing parent.
  /// It's convinient when managing game objects but is not desired when constructing a new mdoel.
  /// Use this method when building hierarchy.</remarks>
  /// <param name="child">Transform to change parent for.</param>
  /// <param name="parent">Transform to change parent to.</param>
  /// <param name="newPosition">Local position to set instead of the original one.</param>
  /// <param name="newRotation">Local rotation to set instead of the original one.</param>
  /// <param name="newScale">Local scale to set instead of the original one.</param>
  protected static void MoveToParent(Transform child, Transform parent,
                                     Vector3? newPosition = null,
                                     Quaternion? newRotation = null,
                                     Vector3? newScale = null) {
    var position = newPosition ?? child.localPosition;
    var rotation = newRotation ?? child.localRotation;
    var scale = newScale ?? child.localScale;
    child.parent = parent;
    child.localPosition = position;
    child.localRotation = rotation;
    child.localScale = scale;
  }

  /// <summary>Finds a transform by name down the hierarchy.</summary>
  /// <param name="parent">Transfrom to start from.</param>
  /// <param name="name">Name of the transfrom.</param>
  /// <returns>Found transform or <c>null</c> if nothing is found.</returns>
  protected static Transform FindTransformInChildren(Transform parent, string name) {
    var res = parent.Find(name);
    if (res != null) {
      return res;
    }
    for (var i = parent.childCount - 1; i >= 0; --i) {
      res = FindTransformInChildren(parent.GetChild(i), name);
      if (res != null) {
        return res;
      }
    }
    return null;
  }

  /// <summary>Finds transform treating the name as a hierarchy path.</summary>
  /// <param name="parent">Transfrom to start looking from.</param>
  /// <param name="path">Path to the target. Path elements are separated by "/" symbol.</param>
  /// <returns></returns>
  protected static Transform FindTransformByPath(Transform parent, string path) {
    return FindTransformByPath(parent, path.Split('/'));
  }

  /// <summary>Finds transform treating the name as a hierarchy path.</summary>
  /// <remarks>Elements of the path may specify exact transform name or be one of the following
  /// patterns:
  /// <list>
  /// <item>"*" - any child  will match. I.e. all children of the preceding parent will be checked
  /// for the branch that follows the pattern. First full match will be returned. E.g. if the are
  /// parts "a/b/c" and "a/aa/c" then pattern "a/*/c" will match "a/b/c" since child "b" is the
  /// first in the children list. This pattern can be nested to specify that barcnh is expected to
  /// be found at the exact depth: "a/*/*/c".</item>
  /// <item>"**" - any path will match. I.e. all the branches of the preceding parent will be
  /// checked until one of them is matched the sub-path that follows the pattern. The shortest path
  /// is used in case of multple hits. E.g. if there are paths "a/b/c" and "a/c" then pattern
  /// "a/**/c" will match path "a/c". This pattern cannot be followed by another pattern, but it can
  /// follow "*" pattern, e.g. "a/*/**/c" (get "c" from any branch of "a" given the depth level is
  /// greater than 1).</item>
  /// </list>
  /// <para>Keep in mind that patterns require children scan, and in a worst case scenario all the
  /// hirerachy can be scanned multiple times.</para>
  /// </remarks>
  /// <param name="parent">Transfrom to start looking from.</param>
  /// <param name="names">Path elements.</param>
  /// <returns>Transform or <c>null</c> if nothing found.</returns>
  /// FIXME: move to the KSPDEv core utils library.
  protected static Transform FindTransformByPath(Transform parent, string[] names) {
    if (names.Length == 0) {
      //FIXME
      Debug.LogWarningFormat("** returning final node: {0}", parent);
      return parent;
    }
    //FIXME
    Debug.LogWarningFormat("** searching for [{0}] in: {1}",
                           KSPUtil.PrintCollection(names), parent);
    var name = names[0];
    names = names.Skip(1).ToArray();
    for (var i = parent.childCount - 1; i >= 0; --i) {
      var child = parent.GetChild(i);
      if (name == "*") {
        //FIXME
        Debug.LogWarningFormat("** try finding [{0}] in: {1}",
                               KSPUtil.PrintCollection(names), child);
        var branch = FindTransformByPath(child, names); // slice
        if (branch != null) {
          return branch;
        }
      } else if (name == "**" && child.name == names[0] || child.name == name) {
        if (name == "**") {
          //FIXME
          Debug.LogWarningFormat("** consuming '**' for the sake of shortest path");
          names = names.Skip(1).ToArray();
        }
        //FIXME
        Debug.LogWarningFormat("** recusrively getting [{0}] from: {1}",
                               KSPUtil.PrintCollection(names), child);
        return FindTransformByPath(child, names);
      }
    }
    // If "**" pattern is not found in the children then go thru children branches.
    if (name == "**") {
      Debug.LogWarning("** NOTHING found!!!");
      var nextName = names[0];
      var nextNames = names.Skip(1).ToArray();
      //FIXME
      Debug.LogWarningFormat("** trying {0} in all children of: {1}",
                             nextName, parent);
      //FIXME: use width first scan to minimize the path.
      var newParent = FindTransformInChildren(parent, nextName);
      if (newParent != null) {
        return FindTransformByPath(newParent, nextNames);
      }
    }
    return null;
  }

  protected static Transform FindTransformByPath_OLD(Transform parent, string[] names) {
    Transform res = parent;
    foreach (var name in names) {
      if (name == "*") {
        //foreach (var child in res.ch
        return FindTransformByPath(res, names); //slice
        //
      } else if (name == "**") {
        
      } else {
        res = res.Find(name);
      }
      res = FindTransformInChildren(res, name);
      if (res == null) {
        break;
      }
    }
    return res;
  }

  /// <summary>Creates a material with current color and shader settings.</summary>
  /// <param name="mainTex">Main texture of the material.</param>
  /// <returns>New material.</returns>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Texture2D.html">Unity3D: Texture2D
  /// </seealso>
  /// <seealso href="https://docs.unity3d.com/Manual/MaterialsAccessingViaScript.html">Unity3D:
  /// Dealing with materials from scripts.</seealso>
  protected Material CreateMaterial(Texture2D mainTex) {
    var material = new Material(Shader.Find(shaderNameOverride ?? shaderName));
    material.mainTexture = mainTex;
    material.color = colorOverride ?? color;
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

  /// <summary>Rescales texture so what one sample covers exactly one unit of the primitive length.
  /// </summary>
  /// <remarks>Normally one texture sample covers the whole primitive regardless to its length. By
  /// calling this method you ensure that one sample keeps its ratio comparing to a linear unit. If
  /// primitive is too short to fit the texture then the texture is truncated. If primitive is too
  /// long to be covered by one sample then the texture will be tiled to fill the space.
  /// <para>This methods assumes UV data on the primitive was created for a length of 1m. With this
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
  protected static void RescaleTextureToLength(
      GameObject obj, float lengthUnit = 1.0f, Renderer renderer = null) {
    var newScale = lengthUnit / obj.transform.localScale.z;
    var mr = renderer ?? obj.GetComponent<Renderer>();
    mr.material.mainTextureScale = new Vector2(mr.material.mainTextureScale.x, newScale);
  }
  #endregion
}

}  // namespace
