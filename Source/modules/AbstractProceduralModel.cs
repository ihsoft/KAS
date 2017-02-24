// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using KSPDev.KSPInterfaces;
using KSPDev.ModelUtils;
using UnityEngine;

namespace KAS {

/// <summary>Base class for parts that dynamically create their model on game load.</summary>
/// <remarks>
/// This class offers common functionality for creating meshes in the model and loading them when
/// needed.
/// </remarks>
public abstract class AbstractProceduralModel : PartModule, IPartModule {
  /// <summary>Standard KSP part shader name.</summary>
  public const string KspPartShaderName = "KSP/Bumped Specular";

  /// <summary>Name of bump map property in the renderer.</summary>
  protected const string BumpMapProp = "_BumpMap";

  /// <summary>Returns cached part's model root transform.</summary>
  /// <remarks>
  /// Attach all your meshes to this transform (directly or via parents). Otherwise, the new meshes
  /// will be ignored by the part's model!
  /// </remarks>
  protected Transform partModelTransform {
    get {
      if (_partModelTransform == null) {
        _partModelTransform = Hierarchy.GetPartModelTransform(part);
      }
      return _partModelTransform;
    }
  }
  Transform _partModelTransform;

  #region Part's config fields
  /// <summary>Config setting. Shader to use for meshes by default.</summary>
  /// <remarks>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public string shaderName = KspPartShaderName;
  /// <summary>Config setting. Main material color to use for meshes by default.</summary>
  /// <remarks>
  /// This is a <see cref="KSPField"/> annotated field. It's handled by the KSP core and must
  /// <i>not</i> be altered directly. Moreover, in spite of it's declared <c>public</c> it must not
  /// be accessed outside of the module.
  /// </remarks>
  /// <seealso href="https://kerbalspaceprogram.com/api/class_k_s_p_field.html">
  /// KSP: KSPField</seealso>
  [KSPField]
  public Color materialColor = Color.white;
  #endregion

  // Internal cache of the textures used by this renderer (and its descendants).
  readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnStart(PartModule.StartState state) {
    LoadPartModel();
    base.OnStart(state);
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    base.OnLoad(node);
    if (!PartLoader.Instance.IsReady()) {
      CreatePartModel();
    }
  }
  #endregion

  #region Abstract methods
  /// <summary>Creates part's model.</summary>
  /// <remarks>Called when it's time to create meshes in the part's model.</remarks>
  /// <seealso cref="partModelTransform"/>
  protected abstract void CreatePartModel();

  /// <summary>Loads part's model.</summary>
  /// <remarks>Called when a new part is being instantiated.</remarks>
  /// <seealso cref="partModelTransform"/>
  protected abstract void LoadPartModel();
  #endregion

  #region Protected utility methods
  /// <summary>Creates a material with default color and shader settings.</summary>
  /// <param name="mainTex">Main texture of the material.</param>
  /// <param name="normals">Optional. Normals texture.</param>
  /// <param name="overrideShaderName">
  /// Optional. Shader name to use instead of the module's one.</param>
  /// <param name="overrideColor">Optional. Color to use instead of main module's color.
  /// </param>
  /// <returns>New material.</returns>
  /// <seealso cref="shaderName"/>
  /// <seealso cref="materialColor"/>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Texture2D.html">
  /// Unity3D: Texture2D</seealso>
  /// <seealso href="https://docs.unity3d.com/Manual/MaterialsAccessingViaScript.html">
  /// Unity3D: Dealing with materials from scripts.</seealso>
  /// FIXME: docs
  protected Material CreateMaterial(Texture2D mainTex,
                                    Texture2D normals = null,
                                    string overrideShaderName = null,
                                    Color? overrideColor = null) {
    var material = new Material(Shader.Find(overrideShaderName ?? shaderName));
    material.mainTexture = mainTex;
    material.color = overrideColor ?? materialColor;
    if (normals != null) {
      material.EnableKeyword("_NORMALMAP");
      material.SetTexture(BumpMapProp, normals);
    }
    
    return material;
  }

  /// <summary>Gets the texture from either KSP gamebase or the internal cache.</summary>
  /// <remarks>
  /// It's OK to call this method in the performance demanding code since once texture is
  /// successfully returned it's cached internally. The subsequent calls won't issue expensive game
  /// database requests.
  /// </remarks>
  /// <param name="textureFileName">
  /// Filename of the texture file. The path is realtive to "GameData" folder. Can be PNG or DDS.
  /// </param>
  /// <param name="asNormalMap">If <c>true</c> then texture will be loaded as a bumpmap.</param>
  /// <returns>
  /// The texture. Note that it's a shared object. Don't execute actions on it which you don't want
  /// to affect other meshes in the game.
  /// </returns>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Texture2D.html">
  /// Unity3D: Texture2D</seealso>
  protected Texture2D GetTexture(string textureFileName, bool asNormalMap = false) {
    var texKey = textureFileName + (asNormalMap ? "_NormalMap" : "");
    Texture2D texture;
    if (!textures.TryGetValue(textureFileName, out texture)) {
      texture = GameDatabase.Instance.GetTexture(textureFileName, asNormalMap);
      if (texture == null) {
        // Use "red" texture if no file found.
        Debug.LogWarningFormat("Cannot load texture: {0}", textureFileName);
        texture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
        texture.SetPixels(new[] {Color.red});
        texture.Apply();
        texture.Compress(highQuality: false);
      }
      textures[textureFileName] = texture;
    }
    return texture;
  }
  #endregion
}

}  // namespace
