<!--
 Transformation to expand sepcialdoc tags.

 In order to apply a special tag to a member documentation add the following
 statement in the docs section:
   <include file="SpecialDocTags.xml" path="Tags/<TagName>/*"/>
 where <TagName> is one of the tags defined in SpecialDocTags.xml.

 E.g. to mark a field as a persistent configuration field add the following:
   <include file="SpecialDocTags.xml" path="Tags/PersistentConfigSetting/*"/>

 Note that all special tags are expanded in the result KAS.xml file, which means
 the third parties will see the expanded comments. However, the include statments
 may not be expanded when dealing with the KAS sources, i.e. the IDE will likely
 not show any of the special tags content.
 -->
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output omit-xml-declaration="yes" indent="yes" />

  <xsl:template match="node()|@*">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*" />
    </xsl:copy>
  </xsl:template>
  
  <xsl:template match="//member[descendant::summary-prefix]/summary[1]">
    <summary>
      <xsl:apply-templates select="../summary-prefix/node()" />
      <xsl:apply-templates select="node()" />
    </summary>
  </xsl:template>

  <xsl:template match="//member/summary-prefix">
  </xsl:template>
</xsl:stylesheet>
