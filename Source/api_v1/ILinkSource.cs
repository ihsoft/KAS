// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using UnityEngine;

namespace KASAPIv1 {

/// <summary>A generic source of a KAS link between two parts.</summary>
/// <remarks>
/// Source is the initiator of the link to another part. It holds all the logic on making and
/// maintaining actual connection between two parts. The other end of the connection must be
/// <see cref="ILinkTarget"/> which implements own piece of logic.
/// <para>
/// Link source have a state that defines what it can do (<see cref="linkState"/>). Not all actions
/// allowed in any state. E.g. in order to link source to a target the source must be in state
/// <see cref="LinkState.Linking"/>.
/// </para>
/// <para>
/// Physical joint between the parts is determined by the <see cref="cfgLinkMode"/>. It's a static
/// settings of the part, so one source can only link in one mode. If part needs to link in
/// different modes it must implement multiple modules: one per mode.
/// </para>
/// </remarks>
/// <example>
/// Here is how a third-party mod may establish a link between two parts that implement the right
/// KAS interfaces:
/// <code><![CDATA[
/// using KASAPIv1;
///
/// public class MyModule : PartModule {
///   public void ConnectParts(Part sourcePart, Part targetPart) {
///     var source = sourcePart.FindModuleImplementing<ILinkSource>();
///     var target = sourcePart.FindModuleImplementing<ILinkTarget>();
///     source.StartLinking(GUILinkMode.API);
///     if (!source.LinkToTarget(target)) {
///       Debug.LogError("Cannot link!");
///       source.CancelLinking();
///     } else {
///       Debug.Log("Link successful!");
///     }
///   }
/// }
/// ]]></code>
/// <para>
/// Note that this code uses GUI mode <see cref="GUILinkMode.API"/>. Depending on how linking
/// process needs to be represented in UI this mode can be set to various values.
/// </para>
/// </example>
public interface ILinkSource {

  /// <summary>Part that owns the source.</summary>
  Part part { get; }

  /// <summary>A source link type identifier.</summary>
  /// <remarks>
  /// This type is used to match with compatible targets. Targets of different types will not be
  /// able to connect with the source. Type can be any string, including empty.
  /// </remarks>
  string cfgLinkType { get; }

  /// <summary>Defines link effect on vessel(s) hierarchy.</summary>
  LinkMode cfgLinkMode { get; }
  
  /// <summary>Name of the attach node to connect with.</summary>
  /// <remarks>
  /// Node with such name must not exist in the part config. It will be created right before
  /// establishing a link, and will be destroyed after the link is broken.
  /// <para>
  /// Name is not required to be one of the KSP reserved ones (e.g. "top"). It can be any string. In
  /// fact, it's best to not use standard names to avoid possible conflicts if part config is
  /// upgraded.
  /// </para>
  /// <para>It's up to the implementation to decide what attach node to create.</para>
  /// </remarks>
  /// <seealso cref="nodeTransform"/>
  string cfgAttachNodeName { get; }

  /// <summary>Name of the renderer that draws link in linked state.</summary>
  /// <remarks>
  /// Source will use this renderer to represent linked state. It's expected there is a
  /// <see cref="ILinkRenderer"/> module defined on the part with the matching name. Behavior is
  /// undefined if no such renderer exists on the part.
  /// </remarks>
  string cfgLinkRendererName { get; }

  /// <summary>Attach node used for linking with the target part.</summary>
  /// <remarks>
  /// The node is required to exist only when source is linked to a compatible target. For not
  /// linked parts attach node may not actually exist in the source part.
  /// </remarks>
  /// <seealso cref="cfgAttachNodeName"/>
  AttachNode attachNode { get; }

  /// <summary>Transform that defines position and orientation of the attach node.</summary>
  /// <remarks>This transform must exist even when no actual attach node is created on the part.
  /// <list>
  /// <item>When connecting parts this transform will be used to create part's attach node.</item>
  /// <item>Renderer uses this transform to align meshes.</item>
  /// <item>Joint module uses node transform as source anchor for PhysX joint.</item>
  /// </list>
  /// </remarks>
  Transform nodeTransform { get; }

  /// <summary>Linked target or <c>null</c> if nothing is linked.</summary>
  ILinkTarget linkTarget { get; }

  /// <summary>ID of the linked target part.</summary>
  uint linkTargetPartId { get; }

  /// <summary>Current state of the source.</summary>
  /// <remarks>
  /// The state cannot be affected directly. Different methods change it to different values.
  /// Though, there is strict model of state tranistioning for the source.
  /// <para>If module is not started yet then the persisted state is returned.</para>
  /// </remarks>
  /// TODO(ihsoft): Add state transtion diagram.
  LinkState linkState { get; }

  /// <summary>
  /// Defines if source must not initiate link requests because there is another source module on
  /// the part that has established a link.
  /// </summary>
  /// <remarks>
  /// Setting of this property changes source state: <c>true</c> value changes state to
  /// <see cref="LinkState.Locked"/>; <c>false</c> value changes state to
  /// <see cref="LinkState.Available"/>.
  /// <para>Setting same value to this property doesn't trigger state change events.</para>
  /// <para>
  /// Note, that not any state transition is possible. If transition is invalid then exception is
  /// thrown.
  /// </para>
  /// </remarks>
  /// <seealso cref="linkState"/>
  bool isLocked { get; set; }

  /// <summary>Mode in which a link between soucre and target is being created.</summary>
  /// <seealso cref="StartLinking"/>
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
  bool StartLinking(GUILinkMode mode);

  /// <summary>Cancels linking mode without creating a link.</summary>
  /// <remarks>All sources and targets that were locked on mode start will be unlocked.</remarks>
  void CancelLinking();

  /// <summary>Establishes a link between two parts.</summary>
  /// <remarks>
  /// Source and target parts become tied with a joint but are not required to be joined into a
  /// single vessel.
  /// <para>
  /// Link conditions will be checked via <see cref="CheckCanLinkTo"/> befor creating the link, and
  /// all errors will be reported to the GUI.
  /// </para>
  /// </remarks>
  /// <param name="target">Target to link with.</param>
  /// <returns><c>true</c> if parts were linked successfully.</returns>
  bool LinkToTarget(ILinkTarget target);

  /// <summary>Breaks a link between source and the current target.</summary>
  /// <remarks>Does nothing if there is no link but a warning will be logged in this case.</remarks>
  /// <param name="actorType">
  /// Specifies what initiates the action. Final result of teh action doesn't depend on it but
  /// visual and sound representation may differ for different actors.
  /// </param>
  /// <param name="moveFocusOnTarget">
  /// If <c>true</c> then upon decoupling current vessel focus will be set on the vessel that owns
  /// the link's <i>target</i>. Otherwise, the focus will stay at the source part vessel.
  /// </param>
  void BreakCurrentLink(LinkActorType actorType, bool moveFocusOnTarget = false);

  /// <summary>Verifies if link between the parts can be successful.</summary>
  /// <param name="target">Target to connect with.</param>
  /// <param name="reportToGUI">
  /// If <c>true</c> then errors will be reported to the UI letting user know the link cannot be
  /// made. By default this mode is OFF.
  /// </param>
  /// <param name="reportToLog">
  /// If <c>true</c> then errors will be logged to the logs as warnings. Disabling of such logging
  /// makes sense when caller code just checks for the possibility of the link (e.g. when showing UI
  /// elements). If <paramref name="reportToGUI"/> set to <c>true</c> then errors will be logged
  /// regardless to the setting of this parameter. By default logging mode is ON.
  /// </param>
  /// <returns><c>true</c> if link can be made.</returns>
  bool CheckCanLinkTo(ILinkTarget target, bool reportToGUI = false, bool reportToLog = true);
}

}  // namespace
