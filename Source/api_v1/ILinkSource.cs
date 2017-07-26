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
/// <example><code source="Examples/ILinkSource-Examples.cs" region="StateModel"/></example>
public interface ILinkSource {

  /// <summary>Part that owns the source.</summary>
  /// <value>Instance of the part.</value>
  /// <example><code source="Examples/ILinkTarget-Examples.cs" region="FindSourceFromTarget"/></example>
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
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="ConnectParts"/></example>
  LinkMode cfgLinkMode { get; }
  
  /// <summary>
  /// The name prefix of the object that specifies the position and orientation of the attach node
  /// which is used to connect with the target.
  /// </summary>
  /// <value>Arbitrary string.</value>
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

  /// <summary>Name of the renderer that draws the link.</summary>
  /// <value>Arbitrary string. Can be empty.</value>
  /// <remarks>
  /// The source will find a renderer module using this name as a key. It will be used to draw the
  /// link when connected to the target. The behavior is undefined if there is no renderer found on
  /// the part.
  /// </remarks>
  /// <seealso cref="ILinkRenderer.cfgRendererName"/>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="StartRenderer"/></example>
  // TODO(ihsoft): Deprecate in favor of linkRenderer
  string cfgLinkRendererName { get; }

  /// <summary>Attach node used for linking with the target part.</summary>
  /// <value>Fully initialized attach node. Can be <c>null</c>.</value>
  /// <remarks>
  /// The node is required to exist only when the source is linked to a compatible target. For the
  /// not linked parts the attach node may not actually exist in the source part.
  /// </remarks>
  /// <seealso cref="cfgAttachNodeName"/>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="FindSourceAtAttachNode"/></example>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="FindTargetAtAttachNode"/></example>
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
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="StartRenderer"/></example>
  // TODO(ihsoft): Add example from a joint module.
  Transform nodeTransform { get; }

  /// <summary>Position offset of the physical joint anchor at the source.</summary>
  /// <remarks>
  /// Due to the model layout, the anchor for the PhysX joint at the part may not match its
  /// <see cref="nodeTransform"/>. If this is the case, this property gives the adjustment.
  /// </remarks>
  /// <value>
  /// The position in the local space of the source's <see cref="nodeTransform"/>.
  /// </value>
  Vector3 physicalAnchor { get; }

  /// <summary>Target of the link.</summary>
  /// <value>Target or <c>null</c> if nothing is linked.</value>
  /// <remarks>It only defined for an established link.</remarks>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="FindTargetFromSource"/></example>
  ILinkTarget linkTarget { get; }

  /// <summary>ID of the linked target part.</summary>
  /// <value>Flight ID.</value>
  /// <remarks>It only defined for an established link.</remarks>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="ConnectParts"/></example>
  uint linkTargetPartId { get; }

  /// <summary>Current state of the source.</summary>
  /// <value>The current state.</value>
  /// <remarks>
  /// <para>
  /// The state cannot be affected directly from the outside. The state is changing in response to
  /// the actions that are implemented by the interface methods.
  /// </para>
  /// <para>
  /// There is a strict model of state tranistioning for the source. The implementation must obey
  /// the state transition requirements to be compatible with the other sources and targets. 
  /// <list type="table">
  /// <listheader>
  /// <term>Transition</term><description>Action</description>
  /// </listheader>
  /// <item>
  /// <term><see cref="LinkState.Available"/> => <see cref="LinkState.Linking"/></term>
  /// <description>This module has initiated a link <see cref="StartLinking"/>.</description>
  /// </item>
  /// <item>
  /// <term><see cref="LinkState.Available"/> => <see cref="LinkState.RejectingLinks"/></term>
  /// <description>
  /// Some other source module in the world has initiated a link via <see cref="StartLinking"/>.
  /// </description>
  /// </item>
  /// <item>
  /// <term><see cref="LinkState.Linking"/> => <see cref="LinkState.Available"/></term>
  /// <description>
  /// This module has cancelled the linking mode via <see cref="CancelLinking"/>.
  /// </description>
  /// </item>
  /// <item>
  /// <term><see cref="LinkState.Linking"/> => <see cref="LinkState.Linked"/></term>
  /// <description>This module has completed the link via <see cref="LinkToTarget"/>.</description>
  /// </item>
  /// <item>
  /// <term><see cref="LinkState.RejectingLinks"/> => <see cref="LinkState.Available"/></term>
  /// <description>
  /// Some other module, which initiated a link, has cancelled or completed the linking mode.
  /// </description>
  /// </item>
  /// <item>
  /// <term><see cref="LinkState.RejectingLinks"/> => <see cref="LinkState.Locked"/></term>
  /// <description>
  /// Some other module on the same part, which initiated a link, has completed it via
  /// <see cref="LinkToTarget"/>.
  /// <br/>Or there was an explicit lock state change via <see cref="isLocked"/>.
  /// </description>
  /// </item>
  /// <item>
  /// <term><see cref="LinkState.Linked"/> => <see cref="LinkState.Available"/></term>
  /// <description>This module has broke its link via <see cref="BreakCurrentLink"/>.</description>
  /// </item>
  /// <item>
  /// <term><see cref="LinkState.Locked"/> => <see cref="LinkState.Available"/></term>
  /// <description>
  /// Some other module on the same part, which was linked, has broke its link via
  /// <see cref="BreakCurrentLink"/>.
  /// <br/>Or there was an explicit lock state change via <see cref="isLocked"/>.
  /// </description>
  /// </item>
  /// </list>
  /// </para>
  /// </remarks>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="StateModel"/></example>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="CheckIfSourceCanConnect"/></example>
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
  /// <remarks>It only makes sense when the state is <seealso cref="LinkState.Linking"/>.</remarks>
  /// <value>The GUI mode.</value>
  /// <seealso cref="StartLinking"/>
  /// <seealso cref="linkState"/>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="ConnectParts"/></example>
  GUILinkMode guiLinkMode { get; }

  /// <summary>Actor, who has initiated the link.</summary>
  /// <remarks>It only makes sense when the state is <seealso cref="LinkState.Linking"/>.</remarks>
  /// <value>The actor.</value>
  /// <seealso cref="StartLinking"/>
  /// <seealso cref="linkState"/>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="ConnectParts"/></example>
  LinkActorType linkActor { get; }

  /// <summary>Position offset of the physical joint anchor at the target.</summary>
  /// <remarks>
  /// <para>
  /// Due to the model layout, the anchor for the PhysX joint at the part may not match its
  /// <see cref="nodeTransform"/>. If this is the case, this property gives the adjustment.
  /// </para>
  /// <para>
  /// It only makes sense when the state is <seealso cref="LinkState.Linking"/>. Once the link is
  /// established, the target is responsible to report the correct anchor.
  /// </para>
  /// </remarks>
  /// <value>
  /// The position in the local space of the target's <see cref="nodeTransform"/>.
  /// </value>
  Vector3 targetPhysicalAnchor { get; }

  /// <summary>Starts the linking mode of this source.</summary>
  /// <remarks>
  /// <para>
  /// Only one source at the time can be linking. If the part has more sources or targets, they are
  /// expected to become <see cref="LinkState.Locked"/>.
  /// </para>
  /// <para>A module can refuse the mode by returning <c>false</c>.</para>
  /// </remarks>
  /// <param name="mode">
  /// Defines how the pending link should be displayed. See <see cref="GUILinkMode"/> for more
  /// details.
  /// </param>
  /// <param name="actor">Specifies how the action has been initiated.</param>
  /// <returns><c>true</c> if the mode has successfully started.</returns>
  /// <seealso cref="guiLinkMode"/>
  /// <seealso cref="CancelLinking"/>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="ConnectParts"/></example>
  bool StartLinking(GUILinkMode mode, LinkActorType actor);

  /// <summary>Cancels linking mode without creating a link.</summary>
  /// <remarks>All sources and targets that were locked on mode start will be unlocked.</remarks>
  /// <param name="actor">Specifies how the action has been initiated.</param>
  /// <seealso cref="StartLinking"/>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="ConnectParts"/></example>
  void CancelLinking(LinkActorType actor);

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

  /// <summary>Breaks the link between the source and the target.</summary>
  /// <remarks>
  /// <para>
  /// It must not be called from the physics update methods (e.g. <c>FixedUpdate</c> or
  /// <c>OnJointBreak</c>) since the link's physical objects may be deleted immediately. If the link
  /// needs to be broken from these methods, use a coroutine to postpone the call till the end of
  /// the frame.
  /// </para>
  /// <para>Does nothing if there is no link but a warning will be logged in this case.</para>
  /// </remarks>
  /// <param name="actorType">
  /// Specifies what initiates the action. The final result of the action doesn't depend on it but
  /// visual and sound representation may differ for the different actors.
  /// </param>
  /// <param name="moveFocusOnTarget">
  /// If <c>true</c> then upon decoupling current vessel focus will be set on the vessel that owns
  /// the link's <i>target</i>. Otherwise, the focus will stay at the source part vessel.
  /// </param>
  /// <seealso cref="LinkToTarget"/>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="DisconnectParts"/></example>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="ILinkSourceExample_BreakFromPhysyicalMethod"/></example>
  /// <include file="Unity3D_HelpIndex.xml" path="//item[@name='M:UnityEngine.MonoBehaviour.FixedUpdate']"/>
  /// <include file="Unity3D_HelpIndex.xml" path="//item[@name='M:UnityEngine.Joint.OnJointBreak']"/>
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
