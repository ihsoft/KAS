// Kerbal Attachment System
// https://forum.kerbalspaceprogram.com/index.php?/topic/142594-15-kerbal-attachment-system-kas-v11
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv2;
using KSPDev.DebugUtils;
using KSPDev.LogUtils;
using System;
using System.Linq;
using UnityEngine;

namespace KAS {

/// <summary>
/// Module for the felxible pipes that bend at a curve, close to the real physical behavior.
/// </summary>
/// <remarks>
/// The form of the pipe is calculated using the Cubic Bezier Curves. This renderer is very CPU
/// intensive, since the Bezier re-calculation can potentially happen in every frame. Not
/// recommended for the low-end GPUs, as the final pipe has an order of magnitude more triangles
/// than a regular "straight" pipe.
/// </remarks>
public class KASRendererBezierPipe : AbstractPipeRenderer {

  #region Part's config fields
  /// <summary>Empiric value that defines how strong the pipe resists bending.</summary>
  /// <remarks>
  /// With bigger values the pipe will try to keep a larger bend radius. The value is required to be
  /// greater than <c>0.0</c>. No maximum limit, but keep it reasonable. Rule of thumb is to keep
  /// it equal to or greater than the pipe's diameter.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  /// <seealso cref="AbstractPipeRenderer.pipeDiameter"/>
  [KSPField]
  [KASDebugAdjustable("Pipe bend resistance")]
  public float pipeBendResistance = 0.7f;

  /// <summary>Recommended number of the adjustable sections in the pipe mesh.</summary>
  /// <remarks>
  /// The bigger values will give better visual quality but impact the performance. This value is
  /// used only as a <i>recommendation</i> for the setting. The implemenation is not required to
  /// create exactly this many sections. This setting defines the "baseline" of the renderer
  /// performance and visual quality. The actual quality settings can affect this value.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [KASDebugAdjustable("Pipe mesh sections")]
  public int pipeMeshSections = 21;

  /// <summary>Number of the segments in the pipe perimeter shape.</summary>
  /// <remarks>
  /// The bigger values will give better visual quality but impact the performance. This value is
  /// used only as a <i>recommendation</i> for the setting. The implemenation is not required to
  /// create exactly this many sections. This setting defines the "baseline" of the renderer
  /// performance and visual quality. The actual quality settings can affect this value.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [KASDebugAdjustable("Pipe shape smoothness")]
  public int pipeShapeSmoothness = 16;

  /// <summary>
  /// Tells if the pipe texture needs to be aligned to the pipe's bend configuration.
  /// </summary>
  /// <remarks>
  /// <para>
  /// When the connected objects move or change their orientation, the curve, that connects them,
  /// bends at different places at a variable angle. It results in non-linear changes to the
  /// section lengths. If this setting is set to <c>true</c>, then the renderer will re-align the
  /// texture to keep every section texture ratio proportional to the others.
  /// </para>
  /// <para>
  /// This is an expensive operation which can be avoided if the pipe texture is monotonic. If the
  /// texture has a well distinguished pattern (e.g. a text), then this option must be enabled.
  /// </para>
  /// <para>Note, that depending on the quality settings, the setting can be <i>ignored</i>.</para>
  /// </remarks>
  /// <seealso cref="AbstractPipeRenderer.pipeTexturePath"/>
  /// <seealso cref="AbstractPipeRenderer.pipeTextureSamplesPerMeter"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [KASDebugAdjustable("Reskin texture")]
  protected bool reskinTexture;

  /// <summary>Number of texture samples on the perimeter.</summary>
  /// <remarks>
  /// Defines how many "sides" there will be on the pipe. Most usable when the texture has some
  /// distinguishable patterns, like text. For the monotonic textures using of just one sample is
  /// good enough.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [KASDebugAdjustable("Texture wraps")]
  public int pipeTextureWraps = 2;
  #endregion

  #region Inheritable fields and properties
  /// <summary>Skinned mesh renderer that controls the pipe mesh.</summary>
  protected SkinnedMeshRenderer pipeSkinnedMeshRenderer { get; private set; }

  /// <summary>Root object of the dynamic pipe mesh(-es).</summary>
  /// <value>The root transform.</value>
  /// <seealso cref="CreatePipeMesh"/>
  /// <seealso cref="DestroyPipeMesh"/>
  protected Transform pipeTransform { get; private set; }

  /// <summary>The <c>t</c> parameter value of the Bezier Curve per mesh bone.</summary>
  /// <remarks>
  /// <para>
  /// This array tells at what point to place the bones. Keep in mind, that the first bone is always
  /// aligned to the <see cref="AbstractPipeRenderer.sourceTransform"/> node. And the last bone must
  /// end up aligning to the <see cref="AbstractPipeRenderer.targetTransform"/> node. The value
  /// range is <c>(0.0, 1.0]</c> (the first value is <i>never</i> zero!).
  /// </para>
  /// <para>
  /// There must be exactly as many offsets, as there are <i>real</i> pipe sections in the mesh!
  /// Each offset is used to align a bone in the mesh.
  /// </para>
  /// </remarks>
  /// <seealso cref="MakeBoneSamples"/>
  /// <seealso cref="pipeMeshSections"/>
  protected float[] boneOffsets;
  #endregion

  #region AbstractPipeRenderer overrides
  /// <inheritdoc/>
  public override void UpdateLink() {
    AlignToCurve();
  }

  /// <inheritdoc/>
  protected override void CreatePartModel() {
  }

  /// <inheritdoc/>
  protected override void LoadPartModel() {
  }

  /// <inheritdoc/>
  protected override Vector3[] GetPipePath(Transform start, Transform end) {
    //FIXME: get all sections
    return new[] {start.position, end.position};
  }

  /// <inheritdoc/>
  /// FIXME: colliders!
  protected override void CreatePipeMesh() {
    DestroyPipeMesh();
    MakeBoneSamples();
    pipeTransform = new GameObject(ModelBasename + "-pipe").transform;
    pipeTransform.parent = sourceTransform;

    // Make the boned cylinder vertices. To properly wrap the texture we need an extra vertex.
    //FIXME: vectrex groups = sections + 1
    int VertexCount = (pipeShapeSmoothness + 1) * pipeMeshSections;
    var vertices = new Vector3[VertexCount];
    var normals = new Vector3[VertexCount];
    var uv = new Vector2[VertexCount];
    var boneWeights = new BoneWeight[VertexCount];
    var vertexIdx = 0;
    var angleStep = 2.0f * Mathf.PI / pipeShapeSmoothness;
    var radius = pipeDiameter / 2;
    for (var j = 0; j < pipeMeshSections; j++) {
      for (var i = 0; i < pipeShapeSmoothness + 1; i++) {
        vertices[vertexIdx] = new Vector3(
            radius * Mathf.Sin(angleStep * i),
            radius * Mathf.Cos(angleStep * i),
            boneOffsets[j]);
        normals[vertexIdx] = new Vector3(
            radius * Mathf.Sin(angleStep * i), radius * Mathf.Cos(angleStep * i), 0);
        if (pipeTextureRescaleMode == PipeTextureRescaleMode.TileFromTarget) {
          uv[vertexIdx] = new Vector2(
              (float)(pipeShapeSmoothness - i) / pipeShapeSmoothness * pipeTextureWraps,
              //(float)(pipeMeshSections - 1 - j) / (pipeMeshSections - 1));
              0.0f);
        } else {
          uv[vertexIdx] = new Vector2(
              (float)i / pipeShapeSmoothness * pipeTextureWraps,
              //(float)j / (pipeMeshSections - 1));
              0.0f);
        }
        boneWeights[vertexIdx].boneIndex0 = j;
        boneWeights[vertexIdx].weight0 = 1.0f;
        vertexIdx++;
      }
    }

    // Make the triangles of the pipe. 
    var triangles = new int[3 * 2 * pipeShapeSmoothness * (pipeMeshSections - 1)];
    var prevBoneIdx = 0;
    var nextBoneIdx = pipeShapeSmoothness + 1;
    var triangleIdx = 0;
    for (var j = 1; j < pipeMeshSections; j++) {
      for (var i = 0; i < pipeShapeSmoothness; i++) {
        triangles[triangleIdx++] = prevBoneIdx + i;
        triangles[triangleIdx++] = nextBoneIdx + i;
        triangles[triangleIdx++] = nextBoneIdx + i + 1;
        triangles[triangleIdx++] = prevBoneIdx + i;
        triangles[triangleIdx++] = nextBoneIdx + i + 1;
        triangles[triangleIdx++] = prevBoneIdx + i + 1;
      }
      prevBoneIdx += pipeShapeSmoothness + 1;
      nextBoneIdx += pipeShapeSmoothness + 1;
    }

    // Create bones. Each bone controls a section divider.
    var bones = new Transform[pipeMeshSections];
    var bindPoses = new Matrix4x4[pipeMeshSections];
    for (var j = 0; j < pipeMeshSections; j++) {
      bones[j] = new GameObject("bone" + j).transform;
      bones[j].parent = pipeTransform;
      bones[j].localRotation = Quaternion.identity;
      //FIXME: use the path offset
      //bones[j].localPosition = Vector3.forward * 0.1f * j;
      bones[j].localPosition = Vector3.forward * boneOffsets[j];
      bindPoses[j] = bones[j].worldToLocalMatrix * pipeTransform.localToWorldMatrix;
    }

    pipeSkinnedMeshRenderer = pipeTransform.gameObject.AddComponent<SkinnedMeshRenderer>();
    pipeSkinnedMeshRenderer.material = CreatePipeMaterial();

    var mesh = new Mesh();
    mesh.vertices = vertices;
    mesh.normals = normals;
    mesh.uv = uv;
    mesh.triangles = triangles;
    mesh.boneWeights = boneWeights;
    mesh.bindposes = bindPoses;

    pipeSkinnedMeshRenderer.bones = bones;
    pipeSkinnedMeshRenderer.sharedMesh = mesh;
    //FIXME: mesh disappears due to the boundary miss. How to fix:
    // - update when Offscreen
    // - recalulcate the boundaries
    // - skinnedMotionVectors
    // - split the big mesh into smaller parts
    // - mesh.MarkDynamic();
    pipeSkinnedMeshRenderer.updateWhenOffscreen = true;  // FIXME: ineffective!

    // Initial pipe setup.
    AlignToCurve();
    if (!reskinTexture) {
      RescaleTextureToLength(forceReskin: true);
    }

    mesh.UploadMeshData(!reskinTexture);
  }

  /// <inheritdoc/>
  protected override void DestroyPipeMesh() {
    if (pipeTransform != null) {
      UnityEngine.Object.Destroy(pipeTransform.gameObject);
      pipeTransform = null;
    }
  }
  #endregion

  #region Inheritable methods
  /// <summary>Creates the Bezier Curve arguments (the <c>t</c> parameter).</summary>
  /// <remarks>
  /// This method is <i>the key</i> to the curve smoothness. Override it if you have a better idea
  /// on how the Bezier Curve should be drawn for the given points.
  /// </remarks>
  /// <seealso cref="boneOffsets"/>
  /// <seealso cref="AbstractPipeRenderer.sourceTransform"/>
  /// <seealso cref="AbstractPipeRenderer.targetTransform"/>
  protected virtual void MakeBoneSamples() {
    var k = pipeMeshSections - 1;  // FIXME: make extra vertices instead fo decreasing sections
    boneOffsets = new float[pipeMeshSections];
    for (var n = 0; n < pipeMeshSections; n++) {
      boneOffsets[n] = (float)n / k;
    }
  }
  #endregion

  #region Local utlilty methods
  /// <summary>
  /// Aligns the skinned mesh renderer bones along a Cubic Bezier Curve between the start and the
  /// end attach nodes.
  /// </summary>
  /// <remarks>
  /// This is a simplified implementation of the Bezier Curve algorythm. We only need 3 points
  /// (cubic curves), so it can be programmed plain simple.   
  /// </remarks>
  /// <seealso href="https://en.wikipedia.org/wiki/B%C3%A9zier_curve"/>
  void AlignToCurve() {
    //FIXME: scale p1&p2 if distance is not enough. consider direction! 
    var p0 = sourceTransform.position;
    var p1 = p0 + sourceTransform.forward * pipeBendResistance;
    var p3 = targetTransform.position;
    var p2 = p3 + targetTransform.forward * pipeBendResistance;
    var p0vector = p1 - p0;
    var p1vector = p2 - p1;
    var p2vector = p3 - p2;
    var bones = pipeSkinnedMeshRenderer.bones;
    for (var i = 0; i < bones.Length; i++) {
      var section = bones[i];
      var t = boneOffsets[i];

      // Simplified implementation for the cubic curves. 
      var p01pos = p0 + p0vector * t;
      var p12pos = p1 + p1vector * t;
      var p23pos = p2 + p2vector * t;
      var p02pos = p01pos + (p12pos - p01pos) * t;
      var p13pos = p12pos + (p23pos - p12pos) * t;
      var elementVector = p13pos - p02pos;
      var elementDir = elementVector.normalized;
      var elementPos = p02pos + elementVector * t;

      section.transform.position = elementPos;
      // Use UP vector from the previous node to reduce artefacts when the pipe is bend at a sharp
      // angle.
      section.transform.rotation = Quaternion.LookRotation(
          elementDir, i == 0 ? sourceTransform.up : bones[i - 1].up);

      // Have the texture rescale setting adjusted.
      RescaleTextureToLength();
    }
  }

  /// <summary>Adjusts the texture on the pipe object to fit the rescale mode.</summary>
  /// <remarks>
  /// It makes sure the texture is properly distrubited thru all the pipe mesh sections.
  /// </remarks>
  void RescaleTextureToLength(bool forceReskin = false) {
    // Find out the real length of the pipe.
    var bones = pipeSkinnedMeshRenderer.bones;
    var linkLength = 0.0f;
    //FIXME: calculate the length in align action.
    for (var i = 1; i < bones.Length; i++) {
      linkLength += (bones[i].position - bones[i - 1].position).magnitude;
    }
    var newScale = linkLength * pipeTextureSamplesPerMeter;
    pipeSkinnedMeshRenderer.material.mainTextureScale =
        new Vector2(pipeSkinnedMeshRenderer.material.mainTextureScale.x, newScale);
    if (pipeSkinnedMeshRenderer.material.HasProperty(BumpMapProp)) {
      var nrmScale = pipeSkinnedMeshRenderer.material.GetTextureScale(BumpMapProp);
      pipeSkinnedMeshRenderer.material.SetTextureScale(
          BumpMapProp, new Vector2(nrmScale.x, newScale));
    }
    // Re-skin the deformed sections.
    if (reskinTexture || forceReskin) {
      var uv = pipeSkinnedMeshRenderer.sharedMesh.uv;
      //FIXME: optimize - use length intervals?
      var currentLength = 0.0f;
      for (var i = 1; i < bones.Length; i++) {
        currentLength += (bones[i].position - bones[i - 1].position).magnitude;
        for (var j = 0; j < pipeShapeSmoothness + 1; j++) {
          var vertexIdx = i * (pipeShapeSmoothness + 1) + j;
          if (pipeTextureRescaleMode == PipeTextureRescaleMode.TileFromTarget) {
            uv[vertexIdx] = new Vector2(uv[vertexIdx].x, 1.0f - currentLength / linkLength);
          } else {
            uv[vertexIdx] = new Vector2(uv[vertexIdx].x, currentLength / linkLength);
          }
        }
      }
      pipeSkinnedMeshRenderer.sharedMesh.uv = uv;
    }
  }
  #endregion
}

}  // namespace
