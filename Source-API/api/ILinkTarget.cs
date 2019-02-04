// Kerbal Attachment System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace KASAPIv1 {

/// <summary>A generic target of a KAS link between two parts.</summary>
/// <remarks>
/// <para>
/// The target is a sink for a link initiated by the another part's <see cref="ILinkSource"/>.
/// </para>
/// <para>
/// The link target have a state that defines what it can do (<see cref="ILinkPeer.linkState"/>).
/// Not all actions are allowed in any state. The following state diagram tells what the target
/// can do and when:
/// </para>
/// <list type="table">
/// <listheader>
/// <term>Transition</term><description>Action</description>
/// </listheader>
/// <item>
/// <term><see cref="LinkState.Available"/> => <see cref="LinkState.AcceptingLinks"/></term>
/// <description>
/// This target is able to connect to a source that has just initiated a link.
/// </description>
/// </item>
/// <item>
/// <term><see cref="LinkState.Available"/> => <see cref="LinkState.RejectingLinks"/></term>
/// <description>
/// This target cannot connect to a source that has just initiated a link.
/// </description>
/// </item>
/// <item>
/// <term><see cref="LinkState.AcceptingLinks"/> => <see cref="LinkState.Available"/></term>
/// <description>
/// The source module has ended its linking mode without linking to this target.
/// </description>
/// </item>
/// <item>
/// <term><see cref="LinkState.AcceptingLinks"/> => <see cref="LinkState.Linked"/></term>
/// <description>A source from the world has linked to this target.</description>
/// </item>
/// <item>
/// <term><see cref="LinkState.AcceptingLinks"/> => <see cref="LinkState.Locked"/></term>
/// <description>
/// A source from the world has linked to another target on the part that owns this target.
/// </description>
/// </item>
/// <item>
/// <term><see cref="LinkState.Linked"/> => <see cref="LinkState.Available"/></term>
/// <description>The link to this target has been broken by the source.</description>
/// </item>
/// <item>
/// <term><see cref="LinkState.Locked"/> => <see cref="LinkState.Available"/></term>
/// <description>
/// A source from the world has broke a link to another target on the part that owns this
/// target.
/// </description>
/// </item>
/// <item>
/// <term><see cref="LinkState.RejectingLinks"/> => <see cref="LinkState.Available"/></term>
/// <description>
/// A source from the world has ended the linking mode, and the target's part hasn't linked.
/// </description>
/// </item>
/// <item>
/// <term><see cref="LinkState.RejectingLinks"/> => <see cref="LinkState.Locked"/></term>
/// <description>
/// A source from the world has linked to the owner of this target but thru another target.
/// </description>
/// </item>
/// </list>
/// </remarks>
/// <example>See <see cref="ILinkSource"/> for the examples.</example>
public interface ILinkTarget : ILinkPeer {
  /// <summary>Source that maintains the link.</summary>
  /// <value>Source or <c>null</c> if nothing is linked.</value>
  /// <remarks>
  /// <para>
  /// Setting of this property changes the target state:
  /// <list type="bullet">
  /// <item>A non-null value changes the state to <see cref="LinkState.Linked"/>.</item>
  /// <item><c>null</c> value changes the state to <see cref="LinkState.Available"/>.</item>
  /// </list>
  /// </para>
  /// <para>Assigning the same value to this property doesn't trigger a state change event.</para>
  /// <para>
  /// Note, that not any state transition is possible. If the transition is invalid then an
  /// exception is thrown.
  /// </para>
  /// <para>
  /// It's descouraged to assign this property from a code other than an implementation of
  /// <see cref="ILinkSource"/>.
  /// </para>
  /// </remarks>
  /// <seealso cref="ILinkPeer.linkState"/>
  /// <example><code source="Examples/ILinkTarget-Examples.cs" region="FindSourceFromTarget"/></example>
  ILinkSource linkSource { get; set; }
}

}  // namespace
