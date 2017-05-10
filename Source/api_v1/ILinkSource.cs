// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using UnityEngine;

namespace KASAPIv1 {

/// <summary>A generic source of a KAS link between two parts.</summary>
/// <remarks>
/// <para>
/// Source is the initiator of the link to the another part. It holds all the logic on making and
/// maintaining the actual connection between two parts. The other end of the connection must be
/// <see cref="ILinkTarget"/> which implements its own piece of the logic.
/// </para>
/// <para>
/// The link source have a state that defines what it can do (<see cref="linkState"/>). Not all
/// actions are allowed in any state. E.g. in order to link the source to a target the source must
/// be in state <see cref="LinkState.Linking"/>, it will refuse connecting in the other state.
/// </para>
/// <para>
/// A physical joint between the parts is determined by the <see cref="cfgLinkMode"/>. It's a static
/// settings of the part, so one source module can only link in one mode. If the part needs to link
/// in different modes it must implement multiple modules: one per mode.
/// </para>
/// </remarks>
/// <example><code source="Examples/ILinkSource-Examples.cs" region="ConnectParts"/></example>
/// <example><code source="Examples/ILinkSource-Examples.cs" region="DisconnectParts"/></example>
/// <example><code source="Examples/ILinkSource-Examples.cs" region="FindTargetFromSource"/></example>
/// <example><code source="Examples/ILinkSource-Examples.cs" region="FindSourceByAttachNode"/></example>
/// <example>
/// <code source="Examples/ILinkSource-Examples.cs" region="CheckIfConnected"/>
/// <para>
/// Note, that if you only need to know if the two parts are connected in terms of the game logic,
/// you don't need to deal with the KAS modules. For the game the parts connected via KAS are no
/// different from the ones conected in the editor or via the docking nodes.
/// </para>
/// </example>
public interface ILinkSource {

  /// <summary>Part that owns the source.</summary>
  /// <value>Instance of the part.</value>
  Part part { get; }

  /// <summary>Source link type identifier.</summary>
  /// <value>Arbitrary string. Can be empty.</value>
  /// <remarks>
  /// This value is used to find the compatible targets. Targets of the different types will not
  /// be able to connect with the source.
  /// </remarks>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="ConnectNodes"/></example>
  string cfgLinkType { get; }

  /// <summary>Defines the link's effect on the vessel(s) hierarchy.</summary>
  /// <value>Linking mode.</value>
  LinkMode cfgLinkMode { get; }
  
  /// <summary>Name of the attach node to connect with.</summary>
  /// <value>Arbitrary string.</value>
  /// <remarks>
  /// A node with such name must not exist in the part's model. It will be created right before
  /// establishing a link, and will be destroyed after the link is broken.
  /// <para>
  /// The name is not required to be one of the KSP reserved ones (e.g. "top"). It can be any
  /// string. In fact, it's best to not use the standard names to avoid the possible conflicts.
  /// </para>
  /// </remarks>
  /// <seealso cref="nodeTransform"/>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="ConnectNodes"/></example>
  string cfgAttachNodeName { get; }

  /// <summary>Name of the renderer that draws the link.</summary>
  /// <value>Arbitrary string. Can be empty.</value>
  /// <remarks>
  /// The source will find a renderer module using this name as a key. It will be used to draw the
  /// link when connected to the target. The behavior is undefined if there is no renderer found on
  /// the part.
  /// </remarks>
  /// <seealso cref="ILinkRenderer.cfgRendererName"/>
  string cfgLinkRendererName { get; }

  /// <summary>Attach node used for linking with the target part.</summary>
  /// <value>Fully initialized attach node. Can be <c>null</c>.</value>
  /// <remarks>
  /// The node is required to exist only when the source is linked to a compatible target. For the
  /// not linked parts the attach node may not actually exist in the source part.
  /// </remarks>
  /// <seealso cref="cfgAttachNodeName"/>
  AttachNode attachNode { get; }

  /// <summary>Transform that defines the position and orientation of the attach node.</summary>
  /// <value>Game object transformation. It's never <c>null</c>.</value>
  /// <remarks>This transform must exist even when no actual attach node is created on the part.
  /// <list type="bullet">
  /// <item>
  /// When connecting the parts, this transform will be used to create a part's attach node.
  /// </item>
  /// <item>The renderer uses this transform to align the meshes.</item>
  /// <item>The joint module uses a node transform as a source anchor for the PhysX joint.</item>
  /// </list>
  /// </remarks>
  /// <seealso cref="attachNode"/>
  Transform nodeTransform { get; }

  /// <summary>Target of the link.</summary>
  /// <value>Target or <c>null</c> if nothing is linked.</value>
  /// <remarks>Only defined for an established link.</remarks>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="FindTargetFromSource"/></example>
  ILinkTarget linkTarget { get; }

  /// <summary>ID of the linked target part.</summary>
  /// <value>Flight ID.</value>
  /// <remarks>It only makes sense when the link is connected to the target.</remarks>
  uint linkTargetPartId { get; }

  /// <summary>Current state of the source.</summary>
  /// <value>The current state.</value>
  /// <remarks>
  /// <para>
  /// The state cannot be affected directly from the outside. The state is changing in response to
  /// the actions that are implemented by the interface methods.
  /// </para>
  /// </remarks>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="CheckIfSourceCanConnect"/></example>
  // TODO(ihsoft): Add state transtion diagram.
  LinkState linkState { get; }

  /// <summary>Defines if the source can initiate a link.</summary>
  /// <value>Locked state.</value>
  /// <remarks>
  /// <para>
  /// Setting of this property changes the source state: <c>true</c> value changes the state to
  /// <see cref="LinkState.Locked"/>; <c>false</c> value changes the state to
  /// <see cref="LinkState.Available"/>.
  /// </para>
  /// <para>Assigning the same value to this property doesn't trigger a state change event.</para>
  /// <para>
  /// Note, that not any state transition is possible. If the transition is invalid then an
  /// exception is thrown.
  /// </para>
  /// </remarks>
  /// <seealso cref="linkState"/>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="HighlightLocked"/></example>
  bool isLocked { get; set; }

  /// <summary>Mode in which the link between source and target is created.</summary>
  /// <value>GUI mode.</value>
  /// <seealso cref="StartLinking"/>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="ConnectParts"/></example>
  GUILinkMode guiLinkMode { get; }

  /// <summary>Starts linking mode of this source.</summary>
  /// <remarks>
  /// Only one source at time can be linking. If part has more sources or targets they all will get
  /// <see cref="LinkState.Locked"/>.
  /// </remarks>
  /// <param name="mode">
  /// Defines how pending link should be displayed. See <see cref="GUILinkMode"/> for more details.
  /// </param>
  /// <para>
  /// Module can refuse the mode by returning <c>false</c>. Refusing mode
  /// <see cref="GUILinkMode.API"/> is allowed but strongly discouraged. Only refuse this mode when
  /// all other modes are refused too (i.e. source cannot be linked at all).
  /// </para>
  /// <returns><c>true</c> if mode successfully started.</returns>
  /// <seealso cref="guiLinkMode"/>
  /// <seealso cref="CancelLinking"/>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="ConnectParts"/></example>
  bool StartLinking(GUILinkMode mode);

  /// <summary>Cancels linking mode without creating a link.</summary>
  /// <remarks>All sources and targets that were locked on mode start will be unlocked.</remarks>
  /// <seealso cref="StartLinking"/>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="ConnectParts"/></example>
  void CancelLinking();

  /// <summary>Establishes a link between two parts.</summary>
  /// <remarks>
  /// <para>
  /// The source and the target parts become tied with a joint but are not required to be joined
  /// into a single vessel in terms of the parts hierarchy.
  /// </para>
  /// <para>
  /// The link conditions will be checked via <see cref="CheckCanLinkTo"/> before creating the link.
  /// If the were errorsm they will be reported to the GUI and the link aborted. However, the
  /// linking mode is only ended in case of the successful linking.
  /// </para>
  /// </remarks>
  /// <param name="target">Target to link with.</param>
  /// <returns><c>true</c> if the parts were linked successfully.</returns>
  /// <seealso cref="BreakCurrentLink"/>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="ConnectParts"/></example>
  bool LinkToTarget(ILinkTarget target);

  /// <summary>Breaks the link between the source and target.</summary>
  /// <remarks>Does nothing if there is no link but a warning will be logged in this case.</remarks>
  /// <param name="actorType">
  /// Specifies what initiates the action. Final result of the action doesn't depend on it but
  /// visual and sound representation may differ for different actors.
  /// </param>
  /// <param name="moveFocusOnTarget">
  /// If <c>true</c> then upon decoupling current vessel focus will be set on the vessel that owns
  /// the link's <i>target</i>. Otherwise, the focus will stay at the source part vessel.
  /// </param>
  /// <seealso cref="LinkToTarget"/>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="DisconnectParts"/></example>
  void BreakCurrentLink(LinkActorType actorType, bool moveFocusOnTarget = false);

  /// <summary>Verifies if a link between the parts can be successful.</summary>
  /// <param name="target">Target to connect with.</param>
  /// <param name="reportToGUI">
  /// If <c>true</c> then the errors will be reported to the UI letting the user know that the link
  /// cannot be made.
  /// </param>
  /// <param name="reportToLog">
  /// If <c>true</c> then the errors will be logged to the logs as warnings. Disabling of such a
  /// logging makes sense when the caller code only needs to check for the possibility of the link
  /// (e.g. when showing the UI elements). If <paramref name="reportToGUI"/> set to <c>true</c> then
  /// the errors will be logged regardless to the setting of this parameter.
  /// </param>
  /// <returns><c>true</c> if the link can be made.</returns>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="ConnectPartsWithCheck"/></example>
  bool CheckCanLinkTo(ILinkTarget target, bool reportToGUI = false, bool reportToLog = true);
}

}  // namespace
