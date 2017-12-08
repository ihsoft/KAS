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

  /// <summary>Defines to what parts this source can link to.</summary>
  /// <value>The linking mode.</value>
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

  /// <summary>Transform that defines the position and orientation of the attach node.</summary>
  /// <remarks>
  /// <para>
  /// When connecting the parts, this transform will be used to create a part's attach node.
  /// </para>
  /// <para>
  /// <i>IMPORTANT</i>. The node always has world's scale <c>(1, 1, 1)</c> regardless to the scale
  /// of the part.
  /// </para>
  /// </remarks>
  /// <value>Game object transformation. It's never <c>null</c>.</value>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="StartRenderer"/></example>
  /// <seealso cref="physicalAnchorTransform"/>
  // TODO(ihsoft): Add example from a joint module.
  Transform nodeTransform { get; }

  /// <summary>Transform of the physical joint anchor at the node.</summary>
  /// <remarks>
  /// Normally, the anchor must be a child of the <see cref="nodeTransform"/>. However, when the
  /// logical and the physical positions are the same, this property can return just a
  /// <see cref="nodeTransform"/>.
  /// </remarks>
  /// <value>Game object transformation. It's never <c>null</c>.</value>
  /// <seealso cref="nodeTransform"/>
  Transform physicalAnchorTransform { get; }

  /// <summary>Target of the link.</summary>
  /// <value>Target or <c>null</c> if nothing is linked.</value>
  /// <remarks>It only defined for an established link.</remarks>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="FindTargetFromSource"/></example>
  ILinkTarget linkTarget { get; }

  /// <summary>The persisted ID of the linked target part.</summary>
  /// <value>Flight ID.</value>
  /// <remarks>This value must be available during the vessel loading.</remarks>
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
  bool isLocked { get; }

  /// <summary>Tells if this source is currectly linked with a target.</summary>
  /// <remarks>
  /// This is, basically, a shortcut to check the link state for the availabe state(s).
  /// </remarks>
  /// <value>The current state of the link.</value>
  bool isLinked { get; }

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

  /// <summary>Joint module that manages a physical link.</summary>
  /// <value>The physical joint module on the part.</value>
  ILinkJoint linkJoint { get; }

  /// <summary>Renderer of the link meshes.</summary>
  /// <value>The renderer that represents the link.</value>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="ILinkSourceExample_linkRenderer"/></example>
  ILinkRenderer linkRenderer { get; }

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
  /// <seealso cref="CancelLinking"/>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="ConnectParts"/></example>
  bool StartLinking(GUILinkMode mode, LinkActorType actor);

  /// <summary>Cancels the linking mode without creating a link.</summary>
  /// <remarks>
  /// All the sources and targets, that got locked on the mode start, will be unlocked.
  /// </remarks>
  /// <seealso cref="StartLinking"/>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="ConnectParts"/></example>
  void CancelLinking();

  /// <summary>Establishes a link between two parts.</summary>
  /// <remarks>
  /// <para>
  /// The <see cref="LinkState.Linking"/> mode must be started for this method to succeed.
  /// </para>
  /// <para>
  /// The source and the target parts become associated with each other. How this link is reflected
  /// in the game's physics depends on the parts configuration (the modules it defines).
  /// </para>
  /// <para>
  /// The link conditions will be checked via <see cref="CheckCanLinkTo"/> before creating the link.
  /// If the were errors, they will be reported to the GUI and the link aborted. However, the
  /// linking mode is only ended in case of the successful linking.
  /// </para>
  /// </remarks>
  /// <param name="target">The target to link with.</param>
  /// <returns><c>true</c> if the parts were linked successfully.</returns>
  /// <seealso cref="StartLinking"/>
  /// <seealso cref="BreakCurrentLink"/>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="ConnectParts"/></example>
  bool LinkToTarget(ILinkTarget target);

  /// <summary>Breaks the link between the source and the target.</summary>
  /// <remarks>
  /// It must not be called from the physics update methods (e.g. <c>FixedUpdate</c> or
  /// <c>OnJointBreak</c>) since the link's physical objects may be deleted immediately. If the link
  /// needs to be broken from these methods, use a coroutine to postpone the call till the end of
  /// the frame.
  /// </remarks>
  /// <param name="actorType">
  /// Specifies what initiates the action. The final result of the action doesn't depend on it, but
  /// the visual and sound representations may differ for the different actors.
  /// </param>
  /// <param name="moveFocusOnTarget">
  /// Tells what to do when the link is being broken on an active vessel: upon the separation, the
  /// vessel on either the source or the target part may get the focus. If the link doesn't belong
  /// to the active vessel at the moment of breaking, then the focus is not affected. If this
  /// parameter is <c>true</c>, then upon the decoupling, the vessel focus will be set on the vessel
  /// that owns the link's <i>target</i>. Otherwise, the focus will be set to the source part
  /// vessel.
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
