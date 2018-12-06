// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace KASAPIv1 {

/// <summary>Base interface for a KAS joint.</summary>
/// <remarks>
/// <para>
/// Every KAS part <b>must</b> have a joint module that controls how the KAS joints are maintained.
/// </para>
/// <para>
/// This interface is primarily designed for use form the <see cref="ILinkSource"/> implementations.
/// A third-party code must not interact with it directly.
/// </para>
/// </remarks>
public interface ILinkJoint {
  /// <summary>Identifier of the joint on the part.</summary>
  /// <remarks>It's unique in scope of the part.</remarks>
  /// <value>An arbitary string that identifies this joint.</value>
  string cfgJointName { get; }

  /// <summary>Tells the current coupling mode.</summary>
  /// <remarks>
  /// Note, that if this mode set to <c>true</c>, it doesn't mean that the parts are coupled thru
  /// this specific joint module. It only means that the parts, linked via this joint, are
  /// guaranteed to belong to the same vessel, but the coupling can actually be done thru the other
  /// parts.
  /// </remarks>
  /// <value><c>true</c> if the vessels should couple on link (merge them into one).</value>
  /// <seealso cref="SetCoupleOnLinkMode"/>
  bool coupleOnLinkMode { get; }

  /// <summary>Tells if there is a physical joint created.</summary>
  /// <value><c>true</c> if the source and target parts are physically linked.</value>
  bool isLinked { get; }

  /// <summary>Tells the current link source.</summary>
  /// <value>The link's source or <c>null</c> if the link is not established.</value>
  ILinkSource linkSource { get; }

  /// <summary>Tells the current link target.</summary>
  /// <value>The link's target or <c>null</c> if the link is not established.</value>
  ILinkTarget linkTarget { get; }

  /// <summary>Sets up a physical joint between the source and target.</summary>
  /// <remarks>
  /// <para>
  /// This method can be called either to establish a new joint or to restore an existing link on
  /// load.
  /// </para>
  /// <para>
  /// This method will call the <see cref="CheckConstraints"/> method to ensure there are no errors.
  /// If there are some, then the link is not created and the errors are reported to the logs.
  /// </para>
  /// </remarks>
  /// <returns><c>true</c> if joint was successfully created or updated.</returns>
  /// <param name="source">The link's source. This part owns the joint module.</param>
  /// <param name="target">The link's target.</param>
  /// <seealso cref="CheckConstraints"/>
  /// <seealso cref="ILinkSource"/>
  /// <seealso cref="ILinkTarget"/>
  /// <seealso cref="DropJoint"/>
  /// <seealso cref="coupleOnLinkMode"/>
  bool CreateJoint(ILinkSource source, ILinkTarget target);

  /// <summary>Destroys a physical link between the source and the target.</summary>
  /// <remarks>
  /// This is a cleanup method. It must be safe to execute in any joint state, and should not throw
  /// any errors. E.g. it may get called when the part's state is incomplete.
  /// </remarks>
  /// <seealso cref="CreateJoint"/>
  void DropJoint();

  /// <summary>Requests the joint to become unbreakable or normal.</summary>
  /// <remarks>
  /// Normally, joint is set to unbreakable on time warp, but in general callers may do it at any
  /// moment. In unbreakable state joint must behave as a hard connection that cannot be changed or
  /// destructed by any force.</remarks>
  /// <param name="isUnbreakable">If <c>true</c> then joint must become unbreakable.</param>
  void AdjustJoint(bool isUnbreakable = false);

  /// <summary>Changes the current parts couple mode.</summary>
  /// <remarks>
  /// <para>
  /// When both the source and the target peers support coupling, this mode can be arbitrary set or
  /// reset via the joint module. If the new mode is "coupling", and the source and the target
  /// vessels are different, then a coupling action will trigger. If the new mode is "don't couple",
  /// and the source and the target parts are coupled, then a decoupling event is triggered. In all
  /// the other cases it's just a boolean property change.
  /// </para>
  /// <para>
  /// The modules must support the cycles and be ready to pick up the coupling role when the former
  /// part has gave up.
  /// </para>
  /// </remarks>
  /// <param name="isCoupleOnLink">The new settings of the mode.</param>
  /// <returns>
  /// <c>true</c> if the new mode has been accepted. The change may be refused for any reason by the
  /// implementation, and the caller must react accordingly.
  /// </returns>
  /// <seealso cref="coupleOnLinkMode"/>
  /// <seealso cref="ILinkPeer.coupleNode"/>
  bool SetCoupleOnLinkMode(bool isCoupleOnLink);

  /// <summary>Checks if the joint constraints allow the link to be established.</summary>
  /// <param name="source">The possible source of the link.</param>
  /// <param name="target">The possible target of the link.</param>
  /// <returns>
  /// An empty array if the link can be created, or a list of user friendly errors otherwise.
  /// </returns>
  /// <include file="Unity3D_HelpIndex.xml" path="//item[@name='T:UnityEngine.Transform']/*"/>
  string[] CheckConstraints(ILinkSource source, ILinkTarget target);
}

}  // namespace
