// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;

namespace KSPDev.ConfigUtils {

/// <summary>Attribute for the persistent fields that can use custom type protos.</summary>
/// <example>
/// <para>
/// To provide own type proto for (de)serializing the field: 
/// </para>
/// <code><![CDATA[
/// class CustomType {
///   [PersistentCustomField("my/custom/type", typeProto: typeof(MyTypeProto))]
///   public MyType field1;
/// }
/// ]]></code>
/// <para>
/// If your custom type filed is a collection, you need to provide a collection proto handler. It
/// can be one of the existing protos (e.g. <see cref="GenericCollectionTypeProto"/> or a custom
/// one. Keep in mind that the collection fields <i>must</i> be initialized, or else they won't be
/// handled.
/// </para>
/// <code><![CDATA[
/// class CustomTypes {
///   [PersistentCustomField("my/custom/type", collectionProto: typeof(GenericCollectionTypeProto))]
///   public List<string> field1 = new List<string>();
/// }
/// ]]></code>
/// </example>
/// <seealso cref="ConfigAccessor"/>
/// <seealso cref="AbstractOrdinaryValueTypeProto"/>
/// <seealso cref="AbstractCollectionTypeProto"/>.
[AttributeUsage(AttributeTargets.Field)]
public sealed class PersistentCustomFieldAttribute : BasePersistentFieldAttribute {
  /// <summary>Creates attribute for persistent field with custom type protos.</summary>
  /// <param name="cfgPath">The path to the fields's value in the config.</param>
  /// <param name="typeProto">
  /// The custom ordinary type proto. If not set, then <see cref="StandardOrdinaryTypesProto"/> is
  /// used.
  /// </param>
  /// <param name="collectionProto">
  /// The custom collection type proto. Only set if the attribute is used to annotate a collection.
  /// </param>
  /// <seealso cref="ConfigAccessor.GetValueByPath(ConfigNode,string)"/>
  /// <seealso cref="GenericCollectionTypeProto"/>
  public PersistentCustomFieldAttribute(
      string cfgPath, Type typeProto = null, Type collectionProto = null) : base(cfgPath) {
    ordinaryTypeProto = typeProto ?? typeof(StandardOrdinaryTypesProto);
    collectionTypeProto = collectionProto;
  }
}

}  // namespace
