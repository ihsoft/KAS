// Kerbal Attachment System
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv2;

// ReSharper disable once CheckNamespace
namespace KASImpl {

/// <summary>Defines global events that are triggered by KAS.</summary>
/// <remarks>Try to keep subscriptions to these events at the bare minimum. Too many listeners may
/// impact performance at the moment of actual event triggering.</remarks>
public class KasEventsImpl : IKasEvents {
  /// <inheritdoc/>
  public EventData<ILinkSource> OnStartLinking { get; } =
    new EventData<ILinkSource>("KASOnStartLinking");

  /// <inheritdoc/>
  public EventData<ILinkSource> OnStopLinking { get; } =
    new EventData<ILinkSource>("KASOnStopLinking");

  /// <inheritdoc/>
  public EventData<IKasLinkEvent> OnLinkCreated { get; } =
    new EventData<IKasLinkEvent>("KASOnLinkCreated");

  /// <inheritdoc/>
  public EventData<IKasLinkEvent> OnLinkBroken { get; } =
    new EventData<IKasLinkEvent>("KASOnLinkBroken");
}

}  // namespace
