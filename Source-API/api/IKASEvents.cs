// Kerbal Attachment System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace KASAPIv1 {

/// <summary>Defines global events that are triggered by KAS.</summary>
/// <remarks>
/// Each collection is a list of callbacks that are called when the triggering event has happen.
/// The subscribers should add themselves into the appropriate list to get notified. If subscriber
/// object is being destroyed, then it <i>must</i> remove itself from the lists! Otherwise, the NRE
/// will be thrown and the subscribers downstream will not get the notification.
/// </remarks>
/// <example><code source="Examples/IKasEvents-Examples.cs" region="KasEventsExample1"/></example>
public interface IKasEvents {
  /// <summary>Triggers when a source has initiated linking mode.</summary>
  /// <remarks>The argument of the callback is the link source that started the mode.</remarks>
  /// <value>Collection to add or remove a callback.</value>
  /// <example><code source="Examples/IKasEvents-Examples.cs" region="KasEventsExample1"/></example>
  EventData<ILinkSource> OnStartLinking { get; }

  /// <summary>Triggers when a source has stopped linking mode.</summary>
  /// <remarks>The argument of the callback is the link source that ended the mode.</remarks>
  /// <value>Collection to add or remove a callback.</value>
  /// <example><code source="Examples/IKasEvents-Examples.cs" region="KasEventsExample1"/></example>
  EventData<ILinkSource> OnStopLinking { get; }

  /// <summary>Triggers when a link between two parts has been successfully established.</summary>
  /// <remarks>
  /// <para>The argument of the callback is a KAS event object that describes the link.</para>
  /// <para>
  /// Consider using <see cref="ILinkStateEventListener.OnKASLinkedState"/> when this state change
  /// is needed in scope of just one part.
  /// </para>
  /// </remarks>
  /// <value>Collection to add or remove a callback.</value>
  /// <example><code source="Examples/IKasEvents-Examples.cs" region="KasEventsExample1"/></example>
  EventData<IKasLinkEvent> OnLinkCreated { get; }

  /// <summary>Triggers when a link between two parts has been broken.</summary>
  /// <remarks>
  /// <para>The argument of the callback is a KAS event object that describes the link.</para>
  /// <para>
  /// Consider using <see cref="ILinkStateEventListener.OnKASLinkedState"/> when this state change
  /// is needed in scope of just one part.
  /// </para>
  /// </remarks>
  /// <value>Collection to add or remove a callback.</value>
  /// <example><code source="Examples/IKasEvents-Examples.cs" region="KasEventsExample1"/></example>
  EventData<IKasLinkEvent> OnLinkBroken { get; }
}

}  // namespace
