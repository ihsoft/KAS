// Kerbal Attachment System
// Mod's author: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using UnityEngine;

namespace KASAPIv1 {

/// <summary>
/// The full set of all the public properties that fires the
/// <see cref="IKasPropertyChangeListener.OnKASPropertyChanged"/> event on the
/// <see cref="ILinkCableJoint"/> interface.
/// </summary>
/// <remarks>Keep the property names matching their actual names in the code.</remarks>
public static class ILinkCableJoint_Properties {
  /// <summary>See <see cref="ILinkCableJoint.maxAllowedCableLength"/></summary>
  public const string maxAllowedCableLength = "maxAllowedCableLength";
}

/// <summary>
/// Interafce for a physical cable link. Such links keep the dsitance between the object below the
/// maximum but don't restict any other movements of the objects relative to each other.
/// </summary>
public interface ILinkCableJoint : ILinkJoint {
  /// <summary>Maximum allowed distance between the parts to establish a link.</summary>
  /// <value>Distance in meters.</value>
  float cfgMaxCableLength { get; }

  /// <summary>Rigidbody of the physical cable head.</summary>
  /// <value>The rigibody object, or <c>null</c> if there is no physical head started.</value>
  /// <include file="Unity3D_HelpIndex.xml" path="//item[@name='T:UnityEngine.Rigidbody']/*"/>
  Rigidbody headRb { get; }

  /// <summary>
  /// Maximum possible distance between the source and head/target physical anchors.
  /// </summary>
  /// <remarks>
  /// This is a <i>desired</i> distance, not the actual one used by PhysX! The PhysX library can
  /// apply limits on the min/max values, and adjust them silently. It's discouraged for the
  /// implementations to rely on the joint settings to obtain this value. 
  /// </remarks>
  /// <value>The length in meters.</value>
  /// <seealso cref="headRb"/>
  /// <seealso cref="realCableLength"/>
  /// <seealso cref="ILinkSource.physicalAnchorTransform"/>
  /// <seealso cref="ILinkTarget.physicalAnchorTransform"/>
  /// <seealso cref="StartPhysicalHead"/>
  float maxAllowedCableLength { get; }

  /// <summary>
  /// Returns the actual distance between the source and target/head physical anchors.
  /// </summary>
  /// <remarks>
  /// It's always <c>0</c> if the link is not established and there is no head started. Keep in mind
  /// that the real length is almost never equal to the maximum allowed cable lenght. This is due to
  /// how the PhysX engine works: the joint can only apply a force when it's stretched.
  /// </remarks>
  /// <value>The distance in meters.</value>
  /// <seealso cref="maxAllowedCableLength"/>
  /// <seealso cref="ILinkSource.physicalAnchorTransform"/>
  /// <seealso cref="ILinkTarget.physicalAnchorTransform"/>
  float realCableLength { get; }

  /// <summary>
  /// Attaches the specified object to the source and starts the environmental forces on it.  
  /// </summary>
  /// <remarks>
  /// The cable maximum length will be set to the actual distance between the source and the head.
  /// </remarks>
  /// <param name="source">The source object that owns the head.</param>
  /// <param name="headObjAnchor">
  /// The transform at the head object to attach the cable to. It's also used as a starting point
  /// to find the rigidbody.
  /// </param>
  /// <seealso cref="StopPhysicalHead"/>
  /// <seealso cref="ILinkSource"/>
  /// <seealso cref="maxAllowedCableLength"/>
  /// <seealso cref="realCableLength"/>
  /// <include file="Unity3D_HelpIndex.xml" path="//item[@name='T:UnityEngine.Rigidbody']/*"/>
  /// <include file="Unity3D_HelpIndex.xml" path="//item[@name='T:UnityEngine.Transform']/*"/>
  void StartPhysicalHead(ILinkSource source, Transform headObjAnchor);

  /// <summary>Stops handling the physical head.</summary>
  /// <remarks>
  /// It must not be called from the physics update methods (e.g. <c>FixedUpdate</c> or
  /// <c>OnJointBreak</c>) since the link's physical objects may be deleted immediately. If the link
  /// needs to be broken from these methods, use a coroutine to postpone the call till the end of
  /// the frame.
  /// </remarks>
  /// <seealso cref="StartPhysicalHead"/>
  void StopPhysicalHead();

  /// <summary>
  /// Sets the maximum possible distance between the source and the head/target physical anchors.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Setting the new length may trigger the physical effects if the value is less than the real
  /// cable length, since it will force the engine to pull the objects together. Don't reduce the
  /// length too rapidly to avoid the strong forces applied.
  /// </para>
  /// <para>
  /// The length can be set even when the actual joint doesn't exist. In this case the value will be
  /// applied the next time the joint is created.
  /// </para>
  /// </remarks>
  /// <seealso cref="maxAllowedCableLength"/>
  /// <seealso cref="realCableLength"/>
  void SetCableLength(float length);
}

}  // namespace
