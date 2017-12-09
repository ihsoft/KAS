// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using UnityEngine;

namespace KASAPIv1 {

/// <summary>A generic target of a KAS link between two parts.</summary>
/// <remarks>
/// The target is a sink for a link initiated by the another part's <see cref="ILinkSource"/>.
/// The target logic is very limited and simple. It just remembers the source and does the GUI
/// adjustments as needed.
/// </remarks>
/// <example>See <see cref="ILinkSource"/> for the examples.</example>
public interface ILinkTarget {

  /// <summary>Part that owns the target.</summary>
  /// <value>Instance of the part.</value>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="FindTargetFromSource"/></example>
  Part part { get; }

  /// <summary>Target link type identifier.</summary>
  /// <value>Arbitrary string. Can be empty.</value>
  /// <remarks>
  /// This string is used by the source to find the compatible targets.
  /// </remarks>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="ConnectNodes"/></example>
  string cfgLinkType { get; }

  /// <summary>
  /// The name prefix of the object that specifies the position and orientation of the attach node
  /// which is used to connect with the source.
  /// </summary>
  /// <value>An arbitrary string.</value>
  /// <remarks>
  /// Within the part every module must have a unique node name. This name will be used to create
  /// an object right before establishing a link, and it will be destroyed after the link is broken.
  /// The object is created at the root of the part's model, i.e. it will be affected by the
  /// <c>rescaleFactor</c> tag in the part's config.
  /// </remarks>
  /// <seealso cref="nodeTransform"/>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="ConnectNodes"/></example>
  // TODO(ihsoft): Give examples with the different scale models.
  string cfgAttachNodeName { get; }

  /// <summary>Transform that defines the position and orientation of the attach node.</summary>
  /// <value>Game object transformation. It's never <c>null</c>.</value>
  /// <remarks>
  /// <para>
  /// This transform is used when drawing the visual representation of the link (as a base point).
  /// </para>
  /// <para>
  /// <i>IMPORTANT</i>. The node always has world's scale <c>(1, 1, 1)</c> regardless to the scale
  /// of the part.
  /// </para>
  /// </remarks>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="StartRenderer"/></example>
  /// <seealso cref="physicalAnchorTransform"/>
  Transform nodeTransform { get; }

  /// <summary>Transform of the physical joint anchor at the node.</summary>
  /// <remarks>
  /// This object gets the actual settings when the link is established. The source is in charge on
  /// how to adjust the anchor relative to the attach node transform.
  /// </remarks>
  /// <value>Game object transformation. It's never <c>null</c>.</value>
  /// <seealso cref="nodeTransform"/>
  /// <seealso cref="ILinkSource.targetPhysicalAnchor"/>
  Transform physicalAnchorTransform { get; }

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
  /// <seealso cref="linkState"/>
  /// <example><code source="Examples/ILinkTarget-Examples.cs" region="FindSourceFromTarget"/></example>
  ILinkSource linkSource { get; set; }

  /// <summary>The persisted ID of the linked source part.</summary>
  /// <remarks>This value must be available during the vessel loading.</remarks>
  /// <value>Flight ID.</value>
  uint linkSourcePartId { get; }

  /// <summary>Tells if this target is currectly linked with a source.</summary>
  /// <value>The current state of the link.</value>
  bool isLinked { get; }

  /// <summary>Current state of the target.</summary>
  /// <value>The current state.</value>
  /// <remarks>
  /// <para>
  /// The state cannot be affected directly. The state is changing in response to the actions that
  /// are implemented by the interface methods.
  /// </para>
  /// <para>
  /// There is a strict model of state tranistioning for the target. The implementation must obey
  /// the state transition requirements to be compatible with the other sources and targets. 
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
  /// The source module has ended its linking mode without coupling with this target.
  /// </description>
  /// </item>
  /// <item>
  /// <term><see cref="LinkState.AcceptingLinks"/> => <see cref="LinkState.Linked"/></term>
  /// <description>A source from the world has coupled with this target.</description>
  /// </item>
  /// <item>
  /// <term><see cref="LinkState.AcceptingLinks"/> => <see cref="LinkState.Locked"/></term>
  /// <description>
  /// A source from the world has coupled with another target on the part that owns this target.
  /// </description>
  /// </item>
  /// <item>
  /// <term><see cref="LinkState.Linked"/> => <see cref="LinkState.Available"/></term>
  /// <description>A link with this target has been broken by the source.</description>
  /// </item>
  /// <item>
  /// <term><see cref="LinkState.Locked"/> => <see cref="LinkState.Available"/></term>
  /// <description>
  /// A source from the world has broke the link with another target on the part that owns this
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
  /// A source from the world has coupled with the owner of this target but thru another target.
  /// </description>
  /// </item>
  /// </list>
  /// </para>
  /// </remarks>
  /// <example><code source="Examples/ILinkTarget-Examples.cs" region="StateModel"/></example>
  LinkState linkState { get; }

  /// <summary>Defines if target cannot accept any link requests.</summary>
  /// <value>The locked state.</value>
  /// <seealso cref="linkState"/>
  /// <example><code source="Examples/ILinkTarget-Examples.cs" region="HighlightLocked"/></example>
  bool isLocked { get; }
}

}  // namespace
