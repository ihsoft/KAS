// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv1;

namespace KASImpl {

/// <summary>Defines global events that are triggered by KAS.</summary>
/// <remarks>Try to keep subscriptions to these events at the bare minimum. Too many listeners may
/// impact performance at the moment of actual event triggering.</remarks>
public class KasEventsImpl : IKasEvents {
  /// <inheritdoc/>
  public EventData<ILinkSource> OnStartLinking { get { return _onStartLinking; } }
  readonly EventData<ILinkSource> _onStartLinking =
      new EventData<ILinkSource>("KASOnStartLinking");

  /// <inheritdoc/>
  public EventData<ILinkSource> OnStopLinking { get { return _onStopLinking; } }
  readonly EventData<ILinkSource> _onStopLinking =
      new EventData<ILinkSource>("KASOnStopLinking");

  /// <inheritdoc/>
  public EventData<IKasLinkEvent> OnLinkCreated { get { return _onLinkCreated; } }
  readonly EventData<IKasLinkEvent> _onLinkCreated =
      new EventData<IKasLinkEvent>("KASOnLinkCreated");

  /// <inheritdoc/>
  public EventData<IKasLinkEvent> OnLinkBroken { get { return _onLinkBroken; } }
  readonly EventData<IKasLinkEvent> _onLinkBroken =
      new EventData<IKasLinkEvent>("KASOnLinkBroken");
}

}  // namespace
