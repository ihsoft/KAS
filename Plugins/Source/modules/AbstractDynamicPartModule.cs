// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md
using System;
using System.Linq;
using System.Collections.Generic;
using KSPDev.KSPInterfaces;
using UnityEngine;

namespace KAS {

/// <summary>Base class for parts that dynamically create their model on game load.</summary>
/// <remarks>This class offers common functionality for creating meshes in the model and loading
/// them when needed.</remarks>
public abstract class AbstractDynamicPartModule : PartModule, IPartModule {
  /// <summary>Standard KSP part shader name.</summary>
  public const string KspPartShaderName = "KSP/Bumped Specular";

  /// <summary>Returns cached model root transform.</summary>
  /// <remarks>Attach all your meshes to this transform (directly or via parents). Otherwise, the
  /// new meshes will be ignored by the part's model!</remarks>
  protected Transform partModelTransform {
    get {
      if (_partModelTransform == null) {
        _partModelTransform = part.FindModelTransform("model");
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
  public Color color = Color.white;
  #endregion

  // Internal cache of the textures used by this renderer (and its descendants).
  readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    //FIXME
    Debug.LogWarningFormat("** ON AWAKE: {0}", part.name);
    if (HighLogic.LoadedScene != GameScenes.LOADING) {
      LoadPartModel();
    }
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    //FIXME
    Debug.LogWarningFormat("** ON LOAD: {0}", part.name);
    base.OnLoad(node);
    if (HighLogic.LoadedScene == GameScenes.LOADING) {
      CreatePartModel();
    }
  }

  /// <inheritdoc/>
  public override void OnStart(PartModule.StartState state) {
    base.OnStart(state);
    //FIXME
    Debug.LogWarningFormat("** ON START: {0}", part.name);
  }

  /// <inheritdoc/>
  public override void OnInitialize() {
    base.OnInitialize();
    //FIXME
    Debug.LogWarningFormat("** ON INITIALIZE: {0}", part.name);
  }

  public override void OnActive() {
    base.OnActive();
    //FIXME
    Debug.LogWarningFormat("** ON ACTIVE: {0}", part.name);
  }

  public override void OnInactive() {
    base.OnInactive();
    //FIXME
    Debug.LogWarningFormat("** ON INACTIVE: {0}", part.name);
  }
  #endregion

  #region Customizable methods
  /// <summary>Creates part's model.</summary>
  /// <remarks>Called when it's time to create meshes in the part's model.</remarks>
  /// <seealso cref="partModelTransform"/>
  protected abstract void CreatePartModel();

  /// <summary>Loads part's model.</summary>
  /// <remarks>Called when part is being instantiated.</remarks>
  /// <seealso cref="partModelTransform"/>
  protected abstract void LoadPartModel();
  #endregion

  #region Protected utility methods
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
  /// <param name="asNormalMap">If <c>true</c> then texture will be loaded as a bumpmap.</param>
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
        // Use "red" texture if no file found.
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
