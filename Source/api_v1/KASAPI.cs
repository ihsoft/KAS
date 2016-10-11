// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md
//
// This module is used to build API assembly. Once released to the public the version of this
// assembly cannot change. This guarantees that all the dependent mods will be able to load it. In
// case of a new version of API is released it must inherit from the previous version, and become a
// completely new assembly that is supplied together with the old versions.
//
// It's unspecified how many old versions of the API are to be preserved in the distribution. Mods
// developers should migrate to the newest available API version as soon as possible. In normal
// case every following version is an ancestor of the previous version(s), so the migration should
// be trivial. Though, in case of methods/interfaces deprecation some extra work may be required. 

using System;

// Name of the namespace denotes the API version.
namespace KASAPIv1 {

/// <summary>KAS API, version 1.</summary>
public static class KASAPI {
  /// <summary>Tells if API V1 was loaded and ready to use.</summary>
  public static bool isLoaded;

  /// <summary>KAS joints untils.</summary>
  public static IJointUtils JointUtils;

  /// <summary>KAS attach nodes utils.</summary>
  public static IAttachNodesUtils AttachNodesUtils;

  /// <summary>KAS link utils.</summary>
  public static ILinkUtils LinkUtils;
}

}  // namespace
