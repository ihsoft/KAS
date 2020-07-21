// Kerbal Attachment System
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace KASAPIv2 {

/// <summary>A holder for simple source-to-target event.</summary>
public struct KasLinkEventImpl : IKasLinkEvent {
  /// <inheritdoc/>
  public ILinkSource source { get; }

  /// <inheritdoc/>
  public ILinkTarget target { get; }

  /// <inheritdoc/>
  public LinkActorType actor { get; }

  /// <summary>Creates an event info.</summary>
  /// <param name="source">The source that initiated the link.</param>
  /// <param name="target">The target that accepted the link.</param>
  /// <param name="actorType">The actor that did the change.</param>
  public KasLinkEventImpl(ILinkSource source, ILinkTarget target,
                          LinkActorType actorType = LinkActorType.API) {
    this.source = source;
    this.target = target;
    actor = actorType;
  }
}

}  // namespace
