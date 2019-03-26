// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Collections.Generic;
using KSPDev.Types;
using KSPDev.LogUtils;
using KSPDev.ProcessingUtils;
using KSPDev.ConfigUtils;
using SaveUpgradePipeline;

namespace KSPDev.ConfigUtils {

/// <summary>Class that describes how to modify a part's config node.</summary>
/// <remarks>
/// IMPORTANT! Every patch must be designed so that if it matched to a part and was applied to, it
/// won't match to the same patched part again. If this condition doesn't stand, then the engine
/// will disable the patch.
/// </remarks>
/// <seealso cref="PartNodePatcher.TestPatch"/>
/// <seealso cref="PartNodePatcher.PatchNode"/>
public class ConfigNodePatch {

  #region Internal fields
  /// <summary>Config URL path from which the pacth was created (if any).</summary>
  string sourceConfigUrl;
  #endregion

  /// <summary>Action to perform on the part or module.</summary>
  public enum PatchAction {
    /// <summary>Add new module.</summary>
    Add,
    /// <summary>Change fields in the part or modules.</summary>
    Fix,
    /// <summary>Drop the part or module altogether.</summary>
    Drop,
  }

  /// <summary>Name of the patch. It must be unique in the game's scope.</summary>
  [PersistentField("name")]
  public string name = "";

  /// <summary>Context in which this patch should be applied.</summary>
  [PersistentField("loadContext")]
  public LoadContext loadContext = LoadContext.Any;

  /// <summary>Tells if all patch processing job must be logged.</summary>
  /// <remarks>Use it to debug your patch. The logs may get noisy and large though.</remarks>
  [PersistentField("verboseLogging")]
  public bool verboseLogging;

  /// <summary>Name of the mod's assembly which owns the part being patched.</summary>
  /// <remarks>
  /// This mod must be installed into the game. Othewrise, the patch will be ignored. If the patch
  /// needs to run regardless to the mod installed (e.g. when deleting the parts), simply leave this
  /// value empty.
  /// <para>
  /// The patches with empty mod name are always handled first, assuming they are designed to drop
  /// the incompatible parts. Don't make "fix" patches with empty mod name since their order of
  /// applying will be random.
  /// </para>
  /// </remarks>
  [PersistentField("modName")]
  public string modName = "";

  /// <summary>Unix Epoch timesmap of when the patch was created.</summary>
  /// <remarks>
  /// It doesn't need to be precise. The main goal of this timestamp is to order patches on the same
  /// part. The patches created earlier will be applied first. This timestamp can also be used to
  /// determine when it makes sense to drop it from the distribution, e.g. if it's too old and not
  /// relevant anymore.
  /// </remarks>
  [PersistentField("patchCreationTimestamp")]
  public double patchCreationTimestamp;

  /// <summary>
  /// Config class that specifies how to check if the part qualifies for patching.
  /// </summary>
  public class TestSection {
    /// <summary>Part's persited fields to check.</summary>
    /// <remarks>
    /// The check is done "one-to-one". In order fro the part to qualify, all its fields must match
    /// exactly to the provided values.
    /// </remarks>
    [PersistentField("PART")]
    public PersistentConfigNode partTests;

    /// <summary>Part modules persisted fields to check.</summary>
    /// <remarks>
    /// The module is found by name, set via field "name". If no index suffix is set, then the first
    /// matched module is tested regardless to how many other modules with the same name exist. The
    /// following index suffixes can be set to control the module
    /// lookup:
    /// <list type="bullet">
    /// <item>
    /// "<c>,&lt;number&gt;</c>" - requires that the named module must be found at the specific
    /// index within the part's persistent state. Index starts from <c>0</c>.
    /// </item>
    /// <item>
    /// "<c>,+&lt;number&gt;</c>" - skips the specified number of modules with the same name. Use
    /// it when multiple modules with the same name are saved.
    /// </item>
    /// </list>
    /// </remarks>
    [PersistentField("MODULE", isCollection = true)]
    public List<PersistentConfigNode> moduleTests = new List<PersistentConfigNode>();
  }

  /// <summary>Section that defines rules for the part to qualify for patching.</summary>
  [PersistentField("TEST")]
  public TestSection testSection;

  /// <summary>Config class that specifies how pacth the qualifying part.</summary>
  public class UpgradeSection {
    /// <summary>Config class that defines patching rules.</summary>
    public class Rules {
      /// <summary>Module name to apply changes to. It can have a lookup suffix.</summary>
      /// <seealso cref="TestSection.moduleTests"/>
      [PersistentField("name")]
      public string name;

      /// <summary>Tells what to do with the matched node.</summary>
      [PersistentField("action")]
      public PatchAction action;

      /// <summary>
      /// Defines the rules to apply to the node's fields if the <see cref="action"/> is
      /// <see cref="PatchAction.Fix"/>.
      /// </summary>
      /// <remarks>
      /// The value is comma separated pairs of "<c>name=value</c>". The "name" designates the field
      /// name within the persisted module state, and "value" specifies a new value. Field name can
      /// have a special prefix symbol that defines a special case or how the new value should be
      /// applied:
      /// <list type="bullet">
      /// <item>
      /// <i>No prefix.</i> Tells to add a new value. The existing value(s) in the persisted state
      /// will not be affected. This can be used to add multiple values to a key.
      /// </item>
      /// <item>
      /// "<c>-</c>". Tells to erase the value(s) or node(s) at the key. There are no checks done
      /// to verify if there is something to delete.
      /// </item>
      /// <item>
      /// "<c>%</c>". Tells to assign a new value to an existing filed or create a new value if none
      /// exists.
      /// </item>
      /// </list>
      /// <para>
      /// The rules are applied in the order they are listed in the patch. It's important to
      /// consider it when applying multiple rules to the same key.       
      /// </para>
      /// </remarks>
      [PersistentField("fieldRule", isCollection = true)]
      public List<string> fieldRules = new List<string>();
    }

    /// <summary>Patching rules for the part config.</summary>
    [PersistentField("PART")]
    public Rules partRules;

    /// <summary>Patching rules for the module configs.</summary>
    /// <remarks>
    /// The modules are patched in order. I.e. first module pacth in the upgrade section will be
    /// applied to the first module in the part's save state. When the module needs to be skipped
    /// from the patch, simply don't provide any rules.
    /// <para>
    /// If the part's action is <see cref="PatchAction.Drop"/>, then the module rules are
    /// ignored.
    /// </para>
    /// </remarks>
    [PersistentField("MODULE", isCollection = true)]
    public List<Rules> moduleRules = new List<Rules>();
  }

  /// <summary>Section that defines how to change the part if it qualifies for patching.</summary>
  [PersistentField("UPGRADE")]
  public UpgradeSection upgradeSection;

  /// <summary>Makes a patch form the config node.</summary>
  /// <remarks>Some sanity checks are done when loading, so this method can throw.</remarks>
  /// <param name="node">The node to read patch from.</param>
  /// <returns>
  /// The patch node. It's never <c>null</c>, but if some fields cannot be read, they will remain
  /// uninitialized.
  /// </returns>
  public static ConfigNodePatch MakeFromConfigNode(ConfigNode node) {
    ArgumentGuard.NotNull(node, "node");
    return MakeFromNodeInternal(node, node);
  }

  /// <summary>Makes a patch form the config.</summary>
  /// <remarks>Some sanity checks are done when loading, so this method can throw.</remarks>
  /// <param name="config">The config to make the pacth from.</param>
  /// <returns>
  /// The patch node. It's never <c>null</c>, but if some fields cannot be read, they will remain
  /// uninitialized.
  /// </returns>
  public static ConfigNodePatch MakeFromConfig(UrlDir.UrlConfig config) {
    ArgumentGuard.NotNull(config, "config");
    var res = MakeFromNodeInternal(config.config, config, url: config.url);
    return res;
  }

  /// <inheritdoc/>
  public override string ToString() {
    return string.Format("[ConfigNodePatch#{0}]", sourceConfigUrl ?? name);
  }

  #region Local utility methods
  /// <summary>Makes pacth from a config node.</summary>
  /// <remarks>
  /// The critical settings will be checked for the sane values. If a bad value found, then the
  /// menthod will throw.
  /// </remarks>
  /// <param name="node">The node to create from.</param>
  /// <param name="context">
  /// The context of node loading. It can be any object, e.g. <c>UrlDir.UrlConfig</c> or
  /// <c>ConfigNode</c>. It's only used for logging errors to give a context for debugging.
  /// </param>
  /// <param name="url">The URL to the node in the game's DB.</param>
  /// <returns>The patch. It's never <c>null.</c></returns>
  static ConfigNodePatch MakeFromNodeInternal(ConfigNode node, object context, string url = null) {
    var patchNode = new ConfigNodePatch();
    if (url != null) {
      patchNode.sourceConfigUrl = url;
    }
    ConfigAccessor.ReadFieldsFromNode(node, patchNode.GetType(), patchNode);
    
    // Sanity check of the test rules.
    Preconditions.ConfValueExists(patchNode.name, "node/name", context: context);
    Preconditions.ConfValueExists(patchNode.testSection, "node/TEST", context: context);
    Preconditions.ConfValueExists(
        patchNode.testSection.partTests, "node/TEST/PART", context: context);
    Preconditions.ConfValueExists(
        patchNode.testSection.partTests.GetValue("name"), "node/TEST/PART/name", context: context);
    foreach (var moduleNode in patchNode.testSection.moduleTests) {
      Preconditions.ConfValueExists(
          moduleNode.GetValue("name"), "node/TEST/MODULE/name", context: context);
    }

    // Sanity check of the upgrade rules 
    Preconditions.ConfValueExists(patchNode.upgradeSection, "node/UPGRADE", context: context);
    Preconditions.ConfValueExists(
        patchNode.upgradeSection.partRules, "node/UPGRADE/PART", context: context);
    Preconditions.OneOf(patchNode.upgradeSection.partRules.action,
                        new[] {PatchAction.Drop, PatchAction.Fix},
                        "node/UPGRADE/PART/action", context: context);
    foreach (var moduleRule in patchNode.upgradeSection.moduleRules) {
      Preconditions.ConfValueExists(moduleRule.name, "node/TEST/MODULE/name", context: context);
    }

    // Check the patch age.
    var patchAgeDays = (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalDays
        - patchNode.patchCreationTimestamp / (24*60*60);
    if (patchAgeDays > 180) {
      DebugEx.Warning("Patch is too old: patch={0}, age={1} days", patchNode, patchAgeDays);
    }

    return patchNode;
  }
  #endregion
}

}  // namespace
