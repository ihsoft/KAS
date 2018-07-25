// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace KASAPIv1 {

/// <summary>A holder for simple source-to-target event.</summary>
public struct KasLinkEventImpl : IKasLinkEvent {
  /// <inheritdoc/>
  public ILinkSource source { get { return _source; } }
  readonly ILinkSource _source;

  /// <inheritdoc/>
  public ILinkTarget target { get { return _target; } }
  readonly ILinkTarget _target;

  /// <inheritdoc/>
  public LinkActorType actor { get { return _actor; } }
  LinkActorType _actor;

  /// <summary>Creates an event info.</summary>
  /// <param name="source">The source that initiated the link.</param>
  /// <param name="target">The target that accepted the link.</param>
  /// <param name="actorType">The actor that did the change.</param>
  public KasLinkEventImpl(ILinkSource source, ILinkTarget target,
                          LinkActorType actorType = LinkActorType.API) {
    _source = source;
    _target = target;
    _actor = actorType;
  }
}

}  // namespace
