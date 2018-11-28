// Kerbal Attachment System
// https://forum.kerbalspaceprogram.com/index.php?/topic/142594-15-kerbal-attachment-system-kas-v11
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace KASAPIv2 {

/// <summary>
/// Mode of adjusting the main texture (aznd its normals map) when the pipe length is changed.
/// </summary>
//FIXME: hide into the abstract pipe
public enum PipeTextureRescaleMode {
  /// <summary>
  /// Texture stretches to the pipe's size. The resolution of the texture per meter of the link's
  /// length is chnaging as the link's length is updating.
  /// </summary>
  Stretch,

  /// <summary>
  /// Texture is tiled starting from the source to the target. The resolution of the texture per
  /// meter of the link's length is kept constant and depends on the part's settings.
  /// </summary>
  TileFromSource,

  /// <summary>
  /// Texture is tiled starting from the target to the source. The resolution of the texture per
  /// meter of the link's length is kept constant and depends on the part's settings.
  /// </summary>
  TileFromTarget,
}

}  // namespace
