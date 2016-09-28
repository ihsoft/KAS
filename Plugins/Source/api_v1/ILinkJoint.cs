// Kerbal Attachment System API
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// API design and implemenation: igor.zavoychinskiy@gmail.com
// License: https://github.com/KospY/KAS/blob/master/LICENSE.md

using System;
using UnityEngine;

namespace KASAPIv1 {

//FIXME: docs and samples
public interface ILinkJoint {
  /// <summary>Minimum allowed distance between parts to establish a link.</summary>
  /// <remarks>If <c>0</c> then no limit for minimum value is applied.</remarks>
  float cfgMinLinkLength { get; }
  /// <summary>Maximum allowed distance between parts to establish a link.</summary>
  /// <remarks>If <c>0</c> then no limit for maximum value is applied.</remarks>
  float cfgMaxLinkLength { get; }
  /// <summary>Breaking force for the strut connecting the two parts.</summary>
  /// <remarks>If <c>0</c> then stock joint settings defines the value. If it's a positive number
  /// then it defines a maximum possible strength of the link. Actual strength is a minimum of three
  /// values:
  /// <list>
  /// <item>This setting.</item>
  /// <item>Source part attach strength.</item>
  /// <item>target part attach strength.</item>
  /// </list>
  /// <para>With this approach link will break before the linked parts get ripped off the vessels.
  /// </para>
  /// </remarks>
  float cfgLinkBreakForce { get; }
  /// <summary>Breaking torque for the link connecting the two parts.</summary>
  /// <remarks><see cref="cfgLinkBreakForce"/></remarks>
  float cfgLinkBreakTorque { get; }
  /// <summary>
  /// Maximum allowed angle between attach node normal and the link at the source part.
  /// </summary>
  /// <remarks>If <c>0</c> then angle is not checked.</remarks>
  int cfgSourceLinkAngleLimit { get; }
  /// <summary>
  /// Maximum allowed angle between attach node normal and the link at the target part.
  /// </summary>
  /// <remarks>If <c>0</c> then angle is not checked.</remarks>
  int cfgTargetLinkAngleLimit { get; }

  /// <summary>Sets up a physical link between source and target.</summary>
  /// <remarks>If parts are docked then there is a <see cref="PartJoint"/> created by the KSP core.
  /// The implementation must either adjust it or drop it alltogether.
  /// <para>For the docked parts "source" is always a child to the "target". That said,
  /// <c>source.part.attachJoint</c> is the KSP managed joint which the implementation may want to
  /// adjust.</para>
  /// <para><paramref name="source"/> and <paramref name="target"/> may be not linked at this 
  /// moment. Do <b>not</b> expect them be aware about each other.</para>
  /// </remarks>
  /// <param name="source">Link source. This part owns the joint.</param>
  /// <param name="target">Link target.</param>
  /// FIXME: fix desc, require valid joint to exist. maybe
  void CreateJoint(ILinkSource source, ILinkTarget target);

  /// <summary>Destroys a physical link between source and target.</summary>
  /// <remarks>If there is no joint created then this call is NO-OP.</remarks>
  void DropJoint();

  /// <summary>Requests joint to become unbreakable or normal.</summary>
  /// <remarks>Normally, joint is set to unbreakable on time warp, but in general callers may do it
  /// at any moment. In unbreakable state joint must behave as a hard connection that cannot be
  /// changed or destructed by any force.</remarks>
  /// <param name="isUnbreakable">If <c>true</c> then joint must become unbreakable.</param>
  void AdjustJoint(bool isUnbreakable = false);

  /// <summary>Checks if link length is within the limits.</summary>
  /// <param name="source">Source that probes the link.</param>
  /// <param name="targetTransform">Target of the link to check the length of.</param>
  /// <returns>An error message if link is over limit or <c>null</c> otherwise.</returns>
  /// FIXME: use ILinkTarget once kerbal gets it.
  string CheckLengthLimit(ILinkSource source, Transform targetTransform);

  /// <summary>Checks if link angle at the source joint is within the limits.</summary>
  /// <param name="source">Source that probes the link.</param>
  /// <param name="targetTransform">Target of the link to check the angle against.</param>
  /// <returns>An error message if angle is over limit or <c>null</c> otherwise.</returns>
  string CheckAngleLimitAtSource(ILinkSource source, Transform targetTransform);

  /// <summary>Checks if link angle at the target joint is within the limits.</summary>
  /// <param name="source">Source that probes the link.</param>
  /// <param name="targetTransform">Target of the link to check the angle against.</param>
  /// <returns>An error message if angle is over limit or <c>null</c> otherwise.</returns>
  string CheckAngleLimitAtTarget(ILinkSource source, Transform targetTransform);
}

}  // namespace
