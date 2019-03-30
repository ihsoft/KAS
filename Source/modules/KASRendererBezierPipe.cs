// Kerbal Attachment System
// https://forum.kerbalspaceprogram.com/index.php?/topic/142594-15-kerbal-attachment-system-kas-v11
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using UnityEngine;

using KSPDev.LogUtils;

namespace KAS {

/// <summary>
/// Module for the felxible pipes that bend at a curve, close to the real physical behavior.
/// </summary>
/// <remarks>
/// The form of the pipe is calculated using the Cubic Bezier curves. This renderer is CPU
/// intensive, since the Bezier re-calculation can potentially happen in every frame. Not
/// recommended for the low-end GPUs, as the final pipe has an order of magnitude more triangles
/// than a regular "straight" pipe.
/// <para>
/// This renderer behaves <i>bad</i> if the angle at the source or target is greater than 90
/// degrees. Do <i>not</i> use it with the unconstrained joints (like cables).
/// </para>
/// </remarks>
/// <seealso cref="KASJointTwoEndsSphere"/>
/// <seealso cref="KASJointRigid"/>
public sealed class KASRendererBezierPipe : KASRendererPipe {

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
  [Debug.KASDebugAdjustable("Pipe bend resistance")]
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
  [Debug.KASDebugAdjustable("Pipe mesh sections")]
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
  [Debug.KASDebugAdjustable("Pipe shape smoothness")]
  public int pipeShapeSmoothness = 16;

  /// <summary>Number of texture samples on the perimeter.</summary>
  /// <remarks>
  /// Defines how many "sides" there will be on the pipe. Most usable when the texture has some
  /// distinguishable patterns, like text. For the monotonic textures using of just one sample is
  /// good enough.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Texture wraps")]
  public int pipeTextureWraps = 2;
  #endregion

  #region Local fields and properties
  /// <summary>Skinned renderer for the pipe.</summary>
  /// <value>The skinned renderer or <c>null</c> if none exists.</value>
  SkinnedMeshRenderer pipeSkinnedMeshRenderer {
    get { return pipeMeshRenderer as SkinnedMeshRenderer; }
  }

  /// <summary>The <c>t</c> parameter of the Bezier Curve. Per mesh bone.</summary>
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
  float[] boneOffsets;
  #endregion

  #region KASRendererPipe overrides
  /// <inheritdoc/>
  protected override void SetupPipe(Transform fromObj, Transform toObj) {
    // Purposely not calling the base since it would try to align a stright pipe. 
    AlignToCurve(fromObj, toObj);
  }

  /// <inheritdoc/>
  protected override void CreateLinkPipe() {
    // Relacing the base that creates a stright pipe.
    DestroyPipeMesh();
    MakeBoneSamples();
    pipeTransform = new GameObject(ModelBasename + "-pipe").transform;
    pipeTransform.parent = sourceTransform;

    // Make the boned cylinder vertices. To properly wrap the texture we need an extra vertex.
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
              0.0f);
        } else {
          uv[vertexIdx] = new Vector2(
              (float)i / pipeShapeSmoothness * pipeTextureWraps,
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
      bones[j].localPosition = Vector3.forward * boneOffsets[j];
      bindPoses[j] = bones[j].worldToLocalMatrix * pipeTransform.localToWorldMatrix;
    }

    pipeMeshRenderer = pipeTransform.gameObject.AddComponent<SkinnedMeshRenderer>();
    pipeSkinnedMeshRenderer.sharedMaterial = pipeMaterial;

    var mesh = new Mesh();
    mesh.vertices = vertices;
    mesh.normals = normals;
    mesh.uv = uv;
    mesh.triangles = triangles;
    mesh.boneWeights = boneWeights;
    mesh.bindposes = bindPoses;

    pipeSkinnedMeshRenderer.bones = bones;
    pipeSkinnedMeshRenderer.sharedMesh = mesh;
    pipeSkinnedMeshRenderer.updateWhenOffscreen = true;

    // Initial pipe setup.
    AlignToCurve(sourceJointNode.pipeAttach, targetJointNode.pipeAttach);
    RescaleMeshSectionTextures();
    mesh.UploadMeshData(false);
    if (pipeColliderIsPhysical) {
      CreateColliders();
    }

    // Have the overrides applied if any.
    colorOverride = colorOverride;
    shaderNameOverride = shaderNameOverride;
    isPhysicalCollider = isPhysicalCollider;
  }

  /// <inheritdoc/>
  protected override Vector3[] GetPipePath(Transform start, Transform end) {
    if (pipeColliderIsPhysical) {
      AlignToCurve(sourceJointNode.pipeAttach, targetJointNode.pipeAttach);
      return pipeSkinnedMeshRenderer.bones
          .Select(x => x.position)
          .ToArray();
    }
    return new Vector3[0];
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
  void AlignToCurve(Transform fromObj, Transform toObj) {
    var p0 = fromObj.position;
    var p1 = p0 + fromObj.forward * pipeBendResistance;
    var p3 = toObj.position;
    var p2 = p3 + toObj.forward * pipeBendResistance;
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
          elementDir, i == 0 ? fromObj.up : bones[i - 1].up);
    }

    // Have the texture rescale setting adjusted.
    RescaleMeshSectionTextures();
  }

  /// <summary>Adjusts the texture on the pipe object to fit the rescale mode.</summary>
  /// <remarks>
  /// It makes sure the texture is properly distrubited thru all the pipe mesh sections.
  /// </remarks>
  void RescaleMeshSectionTextures() {
    // Find out the real length of the pipe.
    var bones = pipeSkinnedMeshRenderer.bones;
    var linkLength = 0.0f;
    for (var i = 0; i < bones.Length - 1; i++) {
      linkLength += (bones[i].position - bones[i + 1].position).magnitude;
    }
    RescalePipeTexture(pipeTransform, linkLength, renderer: pipeMeshRenderer);
    // Re-skin the deformed sections.
    var uv = pipeSkinnedMeshRenderer.sharedMesh.uv;
    var currentLength = 0.0f;
    for (var i = 0; i < bones.Length - 1; i++) {
      for (var j = 0; j < pipeShapeSmoothness + 1; j++) {
        var vertexIdx = i * (pipeShapeSmoothness + 1) + j;
        if (pipeTextureRescaleMode == PipeTextureRescaleMode.TileFromTarget) {
          uv[vertexIdx] = new Vector2(uv[vertexIdx].x, 1.0f - currentLength / linkLength);
        } else {
          uv[vertexIdx] = new Vector2(uv[vertexIdx].x, currentLength / linkLength);
        }
      }
      currentLength += (bones[i].position - bones[i + 1].position).magnitude;
    }
    // The very last UV must always be either 0 or 1, so don't trust the float logic.
    for (var j = 0; j < pipeShapeSmoothness + 1; j++) {
      var vertexIdx = (bones.Length - 1) * (pipeShapeSmoothness + 1) + j;
      if (pipeTextureRescaleMode == PipeTextureRescaleMode.TileFromTarget) {
        uv[vertexIdx] = new Vector2(uv[vertexIdx].x, 0.0f);
      } else {
        uv[vertexIdx] = new Vector2(uv[vertexIdx].x, 1.0f);
      }
    }
    pipeSkinnedMeshRenderer.sharedMesh.uv = uv;
  }

  /// <summary>Creates the Bezier Curve arguments (the <c>t</c> parameter).</summary>
  /// <seealso cref="boneOffsets"/>
  void MakeBoneSamples() {
    var k = pipeMeshSections - 1;
    boneOffsets = new float[pipeMeshSections];
    for (var n = 0; n < pipeMeshSections; n++) {
      boneOffsets[n] = (float)n / k;
    }
  }

  /// <summary>Creates capsule colliders per each section between the bones.</summary>
  void CreateColliders() {
    var bones = pipeSkinnedMeshRenderer.bones;
    // TODO(ihsoft): Check the first and the last bone for the half-sphere collision. 
    for (var i = 0; i < bones.Length - 1; ++i) {
      var bone = bones[i];
      var otherBone = bones[i + 1];
      var capsule = bone.transform.gameObject.AddComponent<CapsuleCollider>();
      capsule.direction = 2; // Z-axis
      var boneDistance = (bone.position - otherBone.position).magnitude;
      capsule.center = new Vector3(0, 0, boneDistance / 2);
      // The capsules from the adjustent bones should "connect" at the half-sphere center.
      capsule.height = boneDistance + pipeDiameter;
      capsule.radius = pipeDiameter / 2;
    }
  }
  #endregion
}

}  // namespace
