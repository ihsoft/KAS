// Kerbal Attachment System
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using UnityEngine;

namespace KASAPIv1 {

/// <summary>
/// Interface for a physical cable link. Such links keep the dsitance between the object below the
/// maximum, but don't restict any other movements of the objects relative to each other.
/// </summary>
/// <remarks>
/// <para>
/// The specifics of this module is that the distance between the linked parts becomes variable.
/// Once the link is created, the distance limit is set to the actual distance between the source
/// and target. This limit won't allow the objects to separate too far from each other, but the
/// objects will be allowed to come closer. The code can adjust the limit once the joint is
/// created.
/// </para>
/// <para>
/// Due to the specifics of handling this kind of joints in PhysX, the real distance between the
/// objects <i>can</i> become greater than the distance limit. In fact, if there are forces that try
/// to separate the objects, then the actual distance will always be a bit more than the limit. Do
/// not expect this difference to have any meaning, it depends on the PhysX engine and can be
/// anything.
/// </para>
/// </remarks>
/// <seealso cref="deployedCableLength"/>
/// <seealso cref="realCableLength"/>
/// <seealso cref="SetCableLength"/>
public interface ILinkCableJoint : ILinkJoint {
  /// <summary>Maximum allowed distance between the parts to establish a link.</summary>
  /// <value>Distance in meters. It's constant and doesn't depend on the joint state.</value>
  float cfgMaxCableLength { get; }

  /// <summary>Rigidbody of the physical cable head.</summary>
  /// <value>The rigibody object, or <c>null</c> if there is no physical head started.</value>
  /// <include file="Unity3D_HelpIndex.xml" path="//item[@name='T:UnityEngine.Rigidbody']/*"/>
  Rigidbody headRb { get; }

  /// <summary>
  /// Maximum possible distance between the source and head/target physical anchors.
  /// </summary>
  /// <remarks>
  /// This is a <i>desired</i> distance. The engine will try to keep it equal or less to this value,
  /// but depending on the forces that affect the objects, this distance may be never reached.
  /// Various implementations can adjust this value, but not greater than
  /// <see cref="cfgMaxCableLength"/>.
  /// </remarks>
  /// <value>
  /// The length in meters. Always positive, if the PhysX joint is created. Zero, otherwise.
  /// </value>
  /// <seealso cref="headRb"/>
  /// <seealso cref="realCableLength"/>
  /// <seealso cref="StartPhysicalHead"/>
  /// <seealso cref="SetCableLength"/>
  float deployedCableLength { get; }

  /// <summary>
  /// Returns the actual distance between the source and target/head physical anchors.
  /// </summary>
  /// <remarks>
  /// It's always <c>0</c> if the link is not established and there is no head started. Keep in mind
  /// that the real length is almost never equal to the deployed cable lenght. This is due to how
  /// the PhysX engine works: the force can only be applied when the joint is stretched.
  /// </remarks>
  /// <value>
  /// The distance in meters. Always positive, if the PhysX joint is created. Zero, otherwise.
  /// </value>
  /// <seealso cref="deployedCableLength"/>
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
  /// <seealso cref="deployedCableLength"/>
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
  /// Calling for this method doesn't have any effect if the PhysX joint is not created. When a
  /// brand new joint is created, it always has the distance limit set to the actual distance
  /// between the physical objects. I.e. this method must be called <i>after</i> the physical joint
  /// is created.
  /// </para>
  /// </remarks>
  /// <param name="length">
  /// The new length. The value must be in range <c>[0; cfgMaxCableLength]</c>. If the value is not
  /// within the limits, then it's rounded to the closest boundary. Also, there are special values:
  /// <list type="bullet">
  /// <item>
  /// <c>PositiveInfinity</c>. Set the length to the maximum possible value, configured via
  /// <see cref="cfgMaxCableLength"/>.
  /// </item>
  /// <item>
  /// <c>NegativeInfinity</c>. Set the limit to the real distance, but only if the real distance is
  /// less than the current limit. When the real distance is greater than the limit, it means the
  /// cable is under a strain due to the physical forces, and nothing will be changed to not trigger
  /// extra effects.
  /// </item>
  /// </list>
  /// </param>
  /// <seealso cref="cfgMaxCableLength"/>
  /// <seealso cref="deployedCableLength"/>
  /// <seealso cref="realCableLength"/>
  void SetCableLength(float length);
}

}  // namespace
