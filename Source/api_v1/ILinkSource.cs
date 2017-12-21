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
/// maintaining the actual connection between the two parts. The other end of the connection must be
/// <see cref="ILinkTarget"/> which implements its own piece of the logic.
/// </para>
/// <para>
/// The link source have a state that defines what it can do (<see cref="ILinkPeer.linkState"/>).
/// Not all actions are allowed in any state. The following state diagram tells what the source
/// can do and when:
/// </para>
/// <list type="table">
/// <listheader>
/// <term>Transition</term><description>Action</description>
/// </listheader>
/// <item>
/// <term><see cref="LinkState.Available"/> => <see cref="LinkState.Linking"/></term>
/// <description>
/// This module has initiated a link via the <see cref="StartLinking"/> method call.
/// </description>
/// </item>
/// <item>
/// <term><see cref="LinkState.Available"/> => <see cref="LinkState.RejectingLinks"/></term>
/// <description>
/// Some other source module in the world has initiated a link.
/// </description>
/// </item>
/// <item>
/// <term><see cref="LinkState.Linking"/> => <see cref="LinkState.Available"/></term>
/// <description>
/// This module has cancelled the linking mode via the <see cref="CancelLinking"/> method call.
/// </description>
/// </item>
/// <item>
/// <term><see cref="LinkState.Linking"/> => <see cref="LinkState.Linked"/></term>
/// <description>
/// This module has completed the link via the <see cref="LinkToTarget"/> method call.
/// </description>
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
/// Some other module on the same part, which initiated a link, has completed it via the
/// <see cref="LinkToTarget"/> method call.
/// </description>
/// </item>
/// <item>
/// <term><see cref="LinkState.Linked"/> => <see cref="LinkState.Available"/></term>
/// <description>
/// This module has broke its link via the <see cref="BreakCurrentLink"/> method call.
/// </description>
/// </item>
/// <item>
/// <term><see cref="LinkState.Locked"/> => <see cref="LinkState.Available"/></term>
/// <description>
/// Some other module on the same part, which was linked, has broke its link via the
/// <see cref="BreakCurrentLink"/> method call.
/// </description>
/// </item>
/// </list>
/// </remarks>
/// <example><code source="Examples/ILinkSource-Examples.cs" region="ConnectParts"/></example>
/// <example><code source="Examples/ILinkSource-Examples.cs" region="DisconnectParts"/></example>
/// <example><code source="Examples/ILinkSource-Examples.cs" region="FindTargetFromSource"/></example>
/// <example>
/// <code source="Examples/ILinkSource-Examples.cs" region="CheckIfConnected"/>
/// </example>
/// <example><code source="Examples/ILinkSource-Examples.cs" region="StateModel"/></example>
public interface ILinkSource : ILinkPeer {

  /// <summary>Defines to what parts this source can link to.</summary>
  /// <value>The linking mode.</value>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="ConnectParts"/></example>
  LinkMode cfgLinkMode { get; }
  
  /// <summary>Target of the link.</summary>
  /// <value>Target or <c>null</c> if nothing is linked.</value>
  /// <remarks>It only defined for an established link.</remarks>
  /// <example><code source="Examples/ILinkSource-Examples.cs" region="FindTargetFromSource"/></example>
  ILinkTarget linkTarget { get; }

  /// <summary>Position offset of the physical joint anchor at the target.</summary>
  /// <remarks>
  /// <para>
  /// Due to the model layout, the anchor for the PhysX joint at the part may not match its
  /// <see cref="ILinkPeer.nodeTransform"/>. If this is the case, this property gives the
  /// adjustment.
  /// </para>
  /// <para>
  /// It only makes sense when the state is <seealso cref="LinkState.Linking"/>. Once the link is
  /// established, the target is responsible to report the correct anchor.
  /// </para>
  /// </remarks>
  /// <value>
  /// The position in the local space of the target's <see cref="ILinkPeer.nodeTransform"/>.
  /// </value>
  /// <seealso cref="ILinkPeer"/>
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
