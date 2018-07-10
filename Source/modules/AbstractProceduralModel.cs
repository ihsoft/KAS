// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using KSPDev.ConfigUtils;
using KSPDev.GUIUtils;
using KSPDev.KSPInterfaces;
using KSPDev.LogUtils;
using KSPDev.ModelUtils;
using UnityEngine;

namespace KAS {

/// <summary>Base class for the parts that dynamically create their model on the game load.</summary>
/// <remarks>
/// <para>
/// This class offers a common functionality for creating meshes in the part's model and loading
/// them when needed.
/// </para>
/// <para>
/// The descendants of this module can use the custom persistent fields of groups:
/// </para>
/// <list type="bullet">
/// <item><c>StdPersistentGroups.PartConfigLoadGroup</c></item>
/// <item><c>StdPersistentGroups.PartPersistant</c></item>
/// </list>
/// </remarks>
/// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ConfigUtils.PersistentFieldAttribute']/*"/>
/// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.ConfigUtils.StdPersistentGroups']/*"/>
public abstract class AbstractProceduralModel : PartModule,
    // KSPDev parents.
    IsLocalizableModule,
    // KSPDev syntax sugar interfaces.
    IPartModule {

  /// <summary>Standard KSP part shader name.</summary>
  public const string KspPartShaderName = "KSP/Bumped Specular";

  /// <summary>Name of bump map property in the renderer.</summary>
  protected const string BumpMapProp = "_BumpMap";

  /// <summary>Returns a cached part's model root transform.</summary>
  /// <value>The part's root model.</value>
  /// <remarks>
  /// Attach all your meshes to this transform (eitehr directly or via parents). Otherwise, the new
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

  #region Part's config fields
  /// <summary>Shader to use for meshes by default.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string shaderName = KspPartShaderName;

  /// <summary>Main material color to use for meshes by default.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public Color materialColor = Color.white;
  #endregion

  // Internal cache of the textures used by this renderer (and its descendants).
  readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

  #region IsLocalizableModule implementation
  /// <inheritdoc/>
  public virtual void LocalizeModule() {
    LocalizationLoader.LoadItemsInModule(this);
  }
  #endregion

  #region PartModule overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    ConfigAccessor.CopyPartConfigFromPrefab(this);
    base.OnAwake();
    LocalizeModule();
  }

  /// <inheritdoc/>
  public override void OnStart(PartModule.StartState state) {
    LoadPartModel();
    base.OnStart(state);
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    ConfigAccessor.ReadPartConfig(this, cfgNode: node);
    ConfigAccessor.ReadFieldsFromNode(node, GetType(), this, StdPersistentGroups.PartPersistant);
    base.OnLoad(node);
    if (!PartLoader.Instance.IsReady()) {
      CreatePartModel();
    }
  }

  /// <inheritdoc/>
  public override void OnSave(ConfigNode node) {
    base.OnSave(node);
    ConfigAccessor.WriteFieldsIntoNode(node, GetType(), this, StdPersistentGroups.PartPersistant);
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
  protected Material CreateMaterial(Texture2D mainTex,
                                    Texture2D mainTexNrm = null,
                                    string overrideShaderName = null,
                                    Color? overrideColor = null) {
    var material = new Material(Shader.Find(overrideShaderName ?? shaderName));
    material.mainTexture = mainTex;
    material.color = overrideColor ?? materialColor;
    if (mainTexNrm != null) {
      material.EnableKeyword("_NORMALMAP");
      material.SetTexture(BumpMapProp, mainTexNrm);
    }
    
    return material;
  }

  /// <summary>Gets the texture from either a KSP gamebase or the internal cache.</summary>
  /// <remarks>
  /// It's OK to call this method in the performance demanding code since once the texture is
  /// successfully returned it's cached internally. The subsequent calls won't issue expensive game
  /// database requests.
  /// </remarks>
  /// <param name="textureFileName">
  /// Filename of the texture file. The path is realtive to <c>GameData</c> folder. The name must
  /// not have the file extension.
  /// </param>
  /// <param name="asNormalMap">If <c>true</c> then the texture will be loaded as a bumpmap.</param>
  /// <returns>
  /// The texture. Note that it's a shared object. Don't execute actions on it which you don't want
  /// to affect the other meshes in the game.
  /// </returns>
  /// <seealso href="https://docs.unity3d.com/ScriptReference/Texture2D.html">
  /// Unity3D: Texture2D</seealso>
  protected Texture2D GetTexture(string textureFileName, bool asNormalMap = false) {
    Texture2D texture;
    if (!textures.TryGetValue(textureFileName, out texture)) {
      texture = GameDatabase.Instance.GetTexture(textureFileName, asNormalMap);
      if (texture == null) {
        // Use a "red" texture if no file found.
        HostedDebugLog.Warning(this, "Cannot load texture: {0}", textureFileName);
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
