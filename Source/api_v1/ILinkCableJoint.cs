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

  /// <summary>Physical joint object that connects source to the target.</summary>
  /// <value>The PhysX joint that connects the parts.</value>
  /// <include file="Unity3D_HelpIndex.xml" path="//item[@name='T:UnityEngine.SpringJoint']/*"/>
  SpringJoint cableJointObj { get; }

  /// <summary>Rigidbody of the physical cable head.</summary>
  /// <value>The rigibody object, or <c>null</c> if there is no physical head started.</value>
  /// <include file="Unity3D_HelpIndex.xml" path="//item[@name='T:UnityEngine.Rigidbody']/*"/>
  Rigidbody headRb { get; }

  /// <summary>Source that owns the physical head.</summary>
  /// <value>The source, or <c>null</c> if the head is not started.</value>
  /// <seealso cref="ILinkSource"/>
  ILinkSource headSource { get; }

  /// <summary>Head's transform at which the cable is attached.</summary>
  /// <value>The anchor of the physical head, or <c>null</c> if the head is not started.</value>
  /// <include file="Unity3D_HelpIndex.xml" path="//item[@name='T:UnityEngine.Transform']/*"/>
  Transform headPhysicalAnchorObj { get; }

  /// <summary>
  /// Maximum possible distance between the source's physical and head/target physical anchors.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Reducing the value of this property may trigger the physical effects if the value is less than
  /// <see cref="realCableLength"/>, a physical effect will trigger. Don't reduce the value too
  /// rapidly since it will apply a higher force on the connected objects.
  /// </para>
  /// <para>
  /// This value will be used when establishing a link to a physical head. If it's lower than the
  /// actual distance between the objects, then the real distance will be used instead.   
  /// </para>
  /// <para>
  /// When this property is changed, it fires a notification for name
  /// <see cref="ILinkCableJoint_Properties.maxAllowedCableLength"/>.
  /// </para>
  /// </remarks>
  /// <value>The length in meters.</value>
  /// <seealso cref="headRb"/>
  /// <seealso cref="ILinkSource.physicalAnchorTransform"/>
  /// <seealso cref="ILinkTarget.physicalAnchorTransform"/>
  /// <seealso cref="headPhysicalAnchorObj"/>
  /// <seealso cref="StartPhysicalHead"/>
  float maxAllowedCableLength { get; set; }

  /// <summary>
  /// Returns the actual distance between the source and target/head physical anchors.
  /// </summary>
  /// <remarks>
  /// It's always <c>0</c> if the link is not established and there is no head
  /// started.
  /// </remarks>
  /// <value>The distance in meters.</value>
  /// <seealso cref="ILinkSource.physicalAnchorTransform"/>
  /// <seealso cref="ILinkTarget.physicalAnchorTransform"/>
  /// <seealso cref="headPhysicalAnchorObj"/>
  float realCableLength { get; }

  /// <summary>
  /// Attaches the specified object to the source and starts the environmental forces on it.  
  /// </summary>
  /// <param name="source">The source object that owns the head.</param>
  /// <param name="headObjAnchor">
  /// The transform at the head object to attach the cable to. It's also used as a starting point
  /// to find the rigidbody.
  /// </param>
  /// <seealso cref="StopPhysicalHead"/>
  /// <seealso cref="ILinkSource"/>
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
}

}  // namespace
