// Kerbal Attachment System
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using KSPDev.PartUtils;
using KSPDev.ProcessingUtils;
using UnityEngine;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable once CheckNamespace
namespace KAS {

/// <summary>Base class for the parts that dynamically create their model on the game load.</summary>
/// <remarks>
/// This class offers a common functionality for creating meshes in the part's model and loading
/// them when needed.
/// </remarks>
public abstract class AbstractProceduralModel : AbstractPartModule {

  #region Public constant strings
  /// <summary>Standard KSP part shader name.</summary>
  public const string KspPartShaderName = "KSP/Bumped Specular";

  /// <summary>Name of bump map property in the renderer.</summary>
  /// <remarks>Only bump shaders support it.</remarks>
  /// <seealso cref="KspPartShaderName"/>
  public const string BumpMapPropName = "_BumpMap";

  /// <summary>Name of bump map with specular property in the renderer.</summary>
  /// <remarks>Only bump specular shaders support it.</remarks>
  /// <seealso cref="KspPartShaderName"/>
  public const string BumpSpecMapPropName = "_BumpSpecMap";

  /// <summary>Name of the material shininess in the renderer.</summary>
  public const string ShininessPropName = "_Shininess";

  /// <summary>Shininess property index in the renderer.</summary>
  public static readonly int ShininessProp = Shader.PropertyToID(ShininessPropName);
  #endregion

  #region Inhertable utility methods
  /// <summary>Returns a cached part's model root transform.</summary>
  /// <value>The part's root model.</value>
  /// <remarks>
  /// Attach all your meshes to this transform (either directly or via parents). Otherwise, the new
  /// meshes will be ignored by the part's model!
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

  /// <summary>The scale of the part models.</summary>
  /// <remarks>
  /// The scale of the part must be "even", i.e. all the components in the scale vector must be
  /// equal. If they are not, then the renderer behavior may be inconsistent.
  /// </remarks>
  /// <value>The scale to be applied to all the components.</value>
  protected float baseScale {
    get {
      if (_baseScale < 0) {
        var scale = partModelTransform.lossyScale;
        if (Mathf.Abs(scale.x - scale.y) > 1e-05 || Mathf.Abs(scale.x - scale.z) > 1e-05) {
          HostedDebugLog.Error(this, "Uneven part scale is not supported: {0}",
                               DbgFormatter.Vector(scale));
        }
        _baseScale = scale.x;
      }
      return _baseScale;
    }
  }
  float _baseScale = -1;  // Negative means uninitialized.
  #endregion

  #region Part's config fields
  /// <summary>Shader to use for meshes by default.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Shader name")]
  public string shaderName = KspPartShaderName;

  /// <summary>Main material color to use for meshes by default.</summary>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Material color")]
  public Color materialColor = Color.white;

  /// <summary>Tells if the normals map should be used as bump specular map.</summary>
  /// <remarks>The texture must be made in appropriate way to be compatible!</remarks>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <seealso cref="BumpMapPropName"/>
  /// <seealso cref="BumpSpecMapPropName"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Use NRM texture as bump specular")]
  public bool isBumpSpecMap;

  /// <summary>Sets the material Shininess.</summary>
  /// <remarks>
  /// Refer to the Unity editor for details. This value is passed "as is" to the shader. Default
  /// value <c>-1.0</c> means "don't set anything". Whatever is the default value of the shader, it
  /// will be used.
  /// </remarks>
  /// <include file="../SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Material shininess")]
  public float materialShininess = -1f;
  #endregion

  #region Local fields and properties
  // Internal cache of the textures used by this renderer (and its descendants).
  readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

  /// <summary>The shader to sue if no suitable shaders were found.</summary>
  /// <see cref="GetShader"/>
  const string FallbackShaderName = "Standard";
  #endregion

  #region Inherited methods
  /// <summary>Creates a material with default color and shader settings.</summary>
  /// <param name="mainTex">Main texture of the material.</param>
  /// <param name="mainTexNrm">Normals texture.</param>
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
  protected virtual Material CreateMaterial(Texture2D mainTex,
                                            Texture2D mainTexNrm = null,
                                            string overrideShaderName = null,
                                            Color? overrideColor = null) {
    var material = new Material(GetShader(overrideShaderName: overrideShaderName));
    material.mainTexture = mainTex;
    material.color = overrideColor ?? materialColor;
    if (mainTexNrm != null) {
      material.EnableKeyword("_NORMALMAP");
      material.SetTexture(isBumpSpecMap ? BumpSpecMapPropName : BumpMapPropName, mainTexNrm);
    }
    if (materialShininess >= 0) {
      material.SetFloat(ShininessProp, materialShininess);
    }
    return material;
  }
  #endregion

  #region Protected utility methods
  /// <summary>Get the module's model shader.</summary>
  /// <remarks>Implements a fallback logic to not crash if the shader is not found.</remarks>
  /// <param name="overrideShaderName">
  /// The alternative name of the shader to prefer. If set, then the module's shader name is ignored
  /// in favor of the one provided.
  /// </param>
  /// <returns>The requested shader, or a fallback share. It's never <c>null</c>.</returns>
  protected Shader GetShader(string overrideShaderName = null) {
    var shaderToFind = overrideShaderName ?? shaderName;
    var shader = Shader.Find(shaderToFind);
    if (shader == null) {
      // Fallback if the shader cannot be found.
      HostedDebugLog.Error(this, "Cannot find shader: {0}. Using default.", shaderToFind);
      shader = Shader.Find(FallbackShaderName);
      Preconditions.NotNull(
          shader,
          message: "Failed to create a fallback shader: " + FallbackShaderName,
          context: this);
    }
    return shader;
  }

  /// <summary>Gets the texture from either a KSP game base or the internal cache.</summary>
  /// <remarks>
  /// It's OK to call this method in the performance demanding code since once the texture is
  /// successfully returned it's cached internally. The subsequent calls won't issue expensive game
  /// database requests.
  /// </remarks>
  /// <param name="textureFileName">
  /// Filename of the texture file. The path is relative to <c>GameData</c> folder. The name must
  /// not have the file extension.
  /// </param>
  /// <param name="asNormalMap">If <c>true</c> then the texture will be loaded as a bumpmap.</param>
  /// <param name="notFoundFillColor">
  /// The color of the simulated texture in case of the asset was not found in the game database. By
  /// default a red colored texture will be created.
  /// </param>
  /// <returns>
  /// The texture. Note that it's a shared object. Don't execute actions on it which you don't want
  /// to affect the other meshes in the game.
  /// </returns>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Texture2D.html">
  /// Unity3D: Texture2D</seealso>
  protected Texture2D GetTexture(string textureFileName, bool asNormalMap = false,
                                 Color? notFoundFillColor = null) {
    Texture2D texture;
    if (!textures.TryGetValue(textureFileName, out texture)) {
      texture = GameDatabase.Instance.GetTexture(textureFileName, asNormalMap);
      if (texture == null) {
        // Use a "red" texture if no file found.
        HostedDebugLog.Warning(this, "Cannot load texture: {0}", textureFileName);
        texture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
        texture.SetPixels(new[] {notFoundFillColor ?? Color.red});
        texture.Apply();
        texture.Compress(highQuality: false);
      }
      textures[textureFileName] = texture;
    }
    return texture;
  }

  /// <summary>Shortcut to load an optional normals map.</summary>
  /// <param name="textureFileName">The name of the texture or <c>null</c>.</param>
  /// <returns>The normals map or <c>null</c> if no name was provided.</returns>
  protected Texture2D GetNormalMap(string textureFileName) {
    return !string.IsNullOrEmpty(textureFileName)
        ? GetTexture(textureFileName, asNormalMap: true, notFoundFillColor: Color.black)
        : null;
  }

  /// <summary>Adjusts the bump/specular map in material.</summary>
  /// <remarks>
  /// The material is expected to be created via this module. It's safe to call this method even if
  /// the material doesn't have any bump map texture.
  /// </remarks>
  /// <param name="material">The material to update.</param>
  /// <param name="an">
  /// The callback to apply on the texture if it's found. The only argument of the callback is the
  /// texture property name.
  /// </param>
  /// <seealso cref="CreateMaterial"/>
  /// <seealso cref="isBumpSpecMap"/>
  protected void SetBumpMap(Material material, Action<string> an) {
    var propName = isBumpSpecMap ? BumpSpecMapPropName : BumpMapPropName;
    if (material.HasProperty(propName)) {
      an(propName);
    }
  }
  #endregion
}

}  // namespace
