// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using KASAPIv1;
using System;
using System.Linq;

namespace KASImpl {

class LinkUtilsImpl : ILinkUtils {
  /// <inheritdoc/>
  public ILinkTarget FindLinkTargetFromSource(ILinkSource source) {
    if (source != null && source.attachNode != null && source.attachNode.attachedPart != null) {
      return source.attachNode.attachedPart.FindModulesImplementing<ILinkTarget>()
          .FirstOrDefault(x => x.attachNode != null && x.attachNode.attachedPart == source.part);
    }
    return null;
  }

  /// <inheritdoc/>
  public ILinkSource FindLinkSourceFromTarget(ILinkTarget target) {
    if (target != null && target.attachNode != null && target.attachNode.attachedPart != null) {
      return target.attachNode.attachedPart.FindModulesImplementing<ILinkSource>()
        .FirstOrDefault(x => x.attachNode != null && x.attachNode.attachedPart == target.part);
    }
    return null;
  }
}

}  // namespace
