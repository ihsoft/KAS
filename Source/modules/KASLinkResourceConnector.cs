// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv2;
using KSPDev.ConfigUtils;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.LogUtils;
using KSPDev.MathUtils;
using KSPDev.ModelUtils;
using KSPDev.PartUtils;
using KSPDev.ResourceUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KAS {

/// <summary>Module which trasnfer resources between two linked vessels.</summary>
/// <seealso cref="KASLinkSourcePhysical"/>
// Next localization ID: #kasLOC_12017
[PersistentFieldsDatabase("KAS/settings/KASConfig", "")]
public sealed class KASLinkResourceConnector : KASLinkSourcePhysical,
    // KAS interfaces.
    IHasGUI {

  #region Localizable GUI strings.
  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message WindowTitleTxt = new Message(
      "#kasLOC_12000",
      defaultTemplate: "Resource Transfer",
      description: "The title of the resource transfer dialog.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<string> resourceName = new Message<string>(
      "#kasLOC_12001",
      defaultTemplate: "<<1>>",
      description: "The resource in the transfer options table. Its main purpose is dealing"
      + " with the Lingoona modifiers, applied to the resource name."
      + "\nArgument <<1>> is the full localized resource name with the Lingoona modifiers"
      + " (if any).");

  /// <include file="SpecialDocTags.xml" path="Tags/Message2/*"/>
  static readonly Message<PercentFixedType, string> compoundResourceName =
      new Message<PercentFixedType, string>(
          "#kasLOC_12002",
          defaultTemplate: "<<1>> <<2>>",
          description: "The string to present for a fuel mixture component."
          + "\nArgument <<1>> is the percent ratio of the component in the mixture of type"
          + " PercentType."
          + "\nArgument <<2>> is the abbreviated localized resource name with the Lingoona"
          + "modifiers (if any).",
          example: "45 % Ox");

  /// <include file="SpecialDocTags.xml" path="Tags/Message2/*"/>
  static readonly Message<CompactNumberType, CompactNumberType> resourceAmounts =
      new Message<CompactNumberType, CompactNumberType>(
          "#kasLOC_12003",
          defaultTemplate: "<gui:min:100,0><<1>> / <<2>>",
          description: "The status string saying current and maximum amounts of the resource in the"
          + " vessel. The gui tags are suggested to define the minimum size of the text, to avoid"
          + " the dialog flickering when the resource is being transferred."
          + "\nArgument <<1>> is the current amount of type CompactNumberType."
          + "\nArgument <<1>> is the maximum amount (capacity) of type CompactNumberType.",
          example: "2.56 / 1,234");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<CompactNumberType> TransferSpeedTxt = new Message<CompactNumberType>(
      "#kasLOC_12004",
      defaultTemplate: "Current transfer speed: <<1>> units per second",
      description: "The information string that tells what is the selected or calculated tarnsfer"
      + " speed is.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message CloseDialogBtn = new Message(
      "#kasLOC_12005",
      defaultTemplate: "Close dialog",
      description: "The caption on the button that closes the trsnafer dialog.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<string> OwnerVesselTxt = new Message<string>(
      "#kasLOC_12006",
      defaultTemplate: "Owner (left): <<1>>",
      description: "The string that tells which vessels owns the resource transfer part. Its stats"
      + " are displayed on the left side of the dialog."
      + "\nArgument <<1>> is the name of the owner vessel.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<string> ConnectedVesselTxt = new Message<string>(
      "#kasLOC_12007",
      defaultTemplate: "Connected (right): <<1>>",
      description: "The string that tells which vessels is connected to the resource transfer part."
      + " Its stats are displayed on the right side of the dialog."
      + "\nArgument <<1>> is the name of the connected vessel.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<string> MixtureHint = new Message<string>(
      "#kasLOC_12008",
      defaultTemplate: "A mixture of components: <<1>>",
      description: "The hint to explain the mixture of the fuel components to transfer."
      + "\nArgument <<1>> is the comma-separated list of the component names.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message AutoScaleToggleTxt = new Message(
      "#kasLOC_12009",
      defaultTemplate: "Auto scale transfer speed",
      description: "The caption for the control that enables the mode, which automatically deducts"
      + " the speed of the reasource transfer.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<CompactNumberType> AutoScaleToggleHint = new Message<CompactNumberType>(
      "#kasLOC_12010",
      defaultTemplate: "The speed will be set so that the transfer is complete in <<1>> seconds",
      description: "The GUI hint that explains what will happen if the auto-speed options is"
      + " chosen.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message LeftToRigthToggleHint = new Message(
      "#kasLOC_12011",
      defaultTemplate: "Trigger transfer from the connected vessel to the owner",
      description: "The hint text to explain the button action that starts transferring the"
      + " resource from the connected vessel to the owner of the resource transfer part.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message LeftToRigthButtonHint = new Message(
      "#kasLOC_12012",
      defaultTemplate: "Transfer from the connected vessel to the owner",
      description: "The hint text to explain the button action that does transferring the"
      + " resource from the connected vessel to the owner of the resource transfer part. When the"
      + " button is released, the transfer stops.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message RightToLeftToggleHint = new Message(
      "#kasLOC_12013",
      defaultTemplate: "Trigger transfer from the owner vessel to the connected vessel",
      description: "The hint text to explain the button action that starts transferring the"
      + " resource from the owner of the resources transfer part to the connected vessel.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message RightToLeftButtonHint = new Message(
      "#kasLOC_12014",
      defaultTemplate: "Transfer from the owner vessel to the connected vessel",
      description: "The hint text to explain the button action that does transferring the"
      + " resource from the owner of the resource transfer part to the connected vessel. When the"
      + " button is released, the transfer stops.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message NotAvailableInDockedMode = new Message(
      "#kasLOC_12016",
      defaultTemplate: "Not available in the docked mode",
      description: "The message to present in the transfer dialog when the parts are docked."
      + " Hence, the stock game functionality must be used to transfer the resources.");
  #endregion

  #region Part's config fields
  /// <summary>The maximum allowed speed of transferring a resource.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [KASDebugAdjustable("Transfer speed")]
  public float maxTransferSpeed = 20.0f;

  /// <summary>
  /// The duration of the complete transfer when the speed is selected automatically.
  /// </summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [KASDebugAdjustable("Auto speed duration threshold")]
  public float autolSpeedTransferDuration = 4.0f;

  /// <summary>
  /// Pattern to find the model which will be rotating around X-axis when the hose is
  /// extended/retracted.
  /// </summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public string rotatingWinchCylinderModel = "";

  /// <summary>
  /// The total length of the cilinder on the outer radius. It's used to calculate the ratio of how
  /// significantly the cylinder need to rotate when 1m of hose is extended/retarted.
  /// </summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [KASDebugAdjustable("Rotating winch model perimeter")]
  public float cylinderPerimeterLength = 1.0f;

  /// <summary>
  /// The full list of the resources that this part can transfer in the undocked mode. Anything
  /// beyond this list will be ignored.
  /// </summary>
  /// <remarks>
  /// <see cref="resourceOverride"/> is ignored when the allowed resources list is set.
  /// </remarks>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [PersistentField("allowedResource", isCollection = true,
                   group = StdPersistentGroups.PartConfigLoadGroup)]
  public List<string> allowedResource = new List<string>();

  /// <summary>
  /// The list of the resources that will be forcibly allowed or disallowed for the transfer via
  /// the part.
  /// </summary>
  /// <remarks>
  /// <para>
  /// This settings is only handled when no specific resources are defined for the part. In this
  /// case all the resources on the vessel are allowed to be moved, except the resources that are:
  /// </para>
  /// <list type="bullet">
  /// <item>Not material. I.e. their unit cost or volume is <i>zero</i>.</item>
  /// <item>Not allowed for pumping (e.g. "solid fuel").</item>
  /// </list>
  /// <para>
  /// To override the rule above, the override can be used. Lis the names of the resources with a
  /// prefix to tell how to handle the resource: prefix "+" means the resource must be allowed to
  /// move no matter what; prefix "-" means the resource(s) must not be allowed to move.
  /// </para>
  /// <para>
  /// The simplest example is <c>ElectricCharge</c> resource, which is not material (no volume).
  /// To allow it on the part, add a positive override: <c>+ElectricCharge</c>. Similary, to
  /// disallow a resource, add a negative override: <c>-LiquidFuel</c>.
  /// </para>
  /// </remarks>
  /// <seealso cref="resourceOverride"/>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [PersistentField("resourceOverride", isCollection = true,
                   group = StdPersistentGroups.PartConfigLoadGroup)]
  public List<string> resourceOverride = new List<string>();

  /// <summary>Container for the fuel mixutre component.</summary>
  public class FuelMixtureComponent {
    /// <summary>Name of the resource.</summary>
    [PersistentField("name")]
    public string name = "";

    /// <summary>
    /// Weight of the component in the mixture. It can be any number, it will be scaled down to
    /// <c>1.0</c> to get the percentage.
    /// </summary>
    [PersistentField("ratio")]
    public double ratio;
  }

  /// <summary>Container for the fuel mixture.</summary>
  public class FuelMixture {
    /// <summary>The mixuture components.</summary>
    [PersistentField("component", isCollection = true)]
    public List<FuelMixtureComponent> components = new List<FuelMixtureComponent>();
  }

  /// <summary>List of the supported fuel mixtures.</summary>
  /// <remarks>
  /// The mixture will only be presented if <i>all</i> of the components are present in any of the
  /// vessels.
  /// </remarks>
  [PersistentField("RTS/fuelMixture", isCollection = true)]
  public List<FuelMixture> fuelMixtures = new List<FuelMixture>();
  #endregion

  #region Context menu events/actions
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true, guiActiveUnfocused = true)]
  [LocalizableItem(
      tag = "#kasLOC_12015",
      defaultTemplate = "Open GUI",
      description = "The context menu event that opens the resources transfer GUI.")]
  public void OpenGUIEvent() {
    if (isLinked && !isGUIOpen) {
      isGUIOpen = true;
      SetPendingTransferOption(null);
      resourceListNeedsUpdate = true;
      MaybeUpdateResourceOptionList();
    }
  }
  #endregion

  #region Local fields & properties
  /// <summary>Actual screen position of the console window.</summary>
  Rect windowRect = new Rect(100, 100, 1, 1);
  
  /// <summary>A title bar location.</summary>
  Rect titleBarRect = new Rect(0, 0, 10000, 20);

  /// <summary>A list of actions to apply at the end of the GUI frame.</summary>
  readonly GuiActionsList guiActions = new GuiActionsList();

  /// <summary>Style to draw a control of the minimum size.</summary>
  static readonly GUILayoutOption MinSizeLayout = GUILayout.ExpandWidth(false);

  /// <summary>Tells if GUI is open.</summary>
  bool isGUIOpen;

  /// <summary>GUI table to align resource names and quantities.</summary>
  /// <remarks>Left Name + Left Amount + Right Amount + Right Name</remarks>
  readonly GUILayoutStringTable guiResourcesTable = new GUILayoutStringTable(4);

  /// <summary>Defintion of all the resources for the both linked vessels.</summary>
  ResourceTransferOption[] resourceRows = new ResourceTransferOption[0];

  /// <summary>Index of the vessels resources.</summary>
  Dictionary<int, ResourceTransferOption> resourceRowsHash =
      new Dictionary<int, ResourceTransferOption>();

  /// <summary>The currently behaving resource transfer.</summary>
  ResourceTransferOption pendingOption;

  /// <summary>The current resource transafer speed.</summary>
  float transferSpeed = 1.0f;

  /// <summary>Tells if the transfer speed can be managed by the code.</summary>
  bool autoScaleSpeed;

  /// <summary>Model of the cylinder to rotate when the hose is extended/retracted.</summary>
  /// <remarks>Can be <c>null</c>.</remarks>
  Transform rotaingCylinder;

  /// <summary>
  /// Tells if the resources options need to be refreshed from the attached vessels.
  /// </summary>
  bool resourceListNeedsUpdate;

  /// <summary>Last time the resoucres counts were updated in GUI.</summary>
  float lastResourcesGUIUpdate;

  /// <summary>The timeout to update the resoucres countes in GUI in seconds.</summary>
  /// <remarks>It's a performance affecting settings.</remarks>
  const float TRANSFER_STATE_UPDATE_PERIOD = 0.1f;
  #endregion

  #region Cached values
  Part currentFromPart;
  double[] currentFromPartCapacities;
  double[] currentFromPartAmounts;
  Part currentToPart;
  double[] currentToPartCapacities;
  double[] currentToPartAmounts;
  #endregion

  #region GUI styles & contents
  GUIStyle guiNoWrapCenteredStyle;
  GUIStyle guiResourceStyle;
  GUIStyle guiTransferBtnStyle;
  GUIContent autoScaleToggleCnt;
  GUIContent leftToRigthToggleCnt;
  GUIContent leftToRigthButtonCnt;
  GUIContent rightToLeftToggleCnt;
  GUIContent rightToLeftButtonCnt;
  #endregion

  #region Local types
  /// <summary>Helper class to hold the data for the transfer option selected.</summary>
  class ResourceTransferOption {
    public readonly int[] resources;
    public readonly double[] resourceRatios;
    public readonly double[] leftAmounts;
    public readonly double[] leftCapacities;
    public readonly double[] rightAmounts;
    public readonly double[] rightCapacities;
    public readonly GUIContent caption = new GUIContent();
    public readonly GUIContent leftInfo = new GUIContent();
    public readonly GUIContent rightInfo = new GUIContent();

    public bool canMoveRightToLeft;
    public bool canMoveLeftToRight;
    public double previousUpdate;
    
    readonly int hashCode;

    public bool leftToRightTransferToggle {
      get { return _leftToRightTransferToggle; }
      set { UpdateTransferTriggerFlag(ref _leftToRightTransferToggle, value); }
    }
    bool _leftToRightTransferToggle;

    public bool leftToRightTransferPress {
      get { return _leftToRightTransferPress; }
      set { UpdateTransferTriggerFlag(ref _leftToRightTransferPress, value); }
    }
    bool _leftToRightTransferPress;

    public bool rightToLeftTransferToggle {
      get { return _rightToLeftTransferToggle; }
      set { UpdateTransferTriggerFlag(ref _rightToLeftTransferToggle, value); }
    }
    bool _rightToLeftTransferToggle;

    public bool rightToLeftTransferPress {
      get { return _rightToLeftTransferPress; }
      set { UpdateTransferTriggerFlag(ref _rightToLeftTransferPress, value); }
    }
    bool _rightToLeftTransferPress;

    /// <inheritdoc/>
    public override int GetHashCode() {
      return hashCode;
    }

    /// <summary>Makes the transfer option.</summary>
    /// <param name="availabeResources"></param>
    /// <param name="resourceRatio"></param>
    public ResourceTransferOption(
        IEnumerable<int> availabeResources, IEnumerable<double> resourceRatio) {
      resources = availabeResources.ToArray();
      hashCode = resources.Aggregate((t, v) => ((t << 3) | (t >> 29)) ^ v);
      resourceRatios = resourceRatio.ToArray();
      leftAmounts = new double[resources.Length];
      leftCapacities = new double[resources.Length];
      rightAmounts = new double[resources.Length];
      rightCapacities = new double[resources.Length];
      UpdateStaticStrings();
    }

    /// <summary>Updates the GUI strings that don't depend on the amounts/capacities.</summary>
    public void UpdateStaticStrings() {
      if (resources.Length == 1) {
        caption.text = resourceName.Format(
            StockResourceNames.GetResourceTitle(resources[0], removeLingoonaTags: false));
      } else {
        var texts = new string[resources.Length];
        var totalAmount = resourceRatios.Sum();
        for (var i = 0; i < resources.Length; i++) {
          texts[i] = compoundResourceName.Format(
              resourceRatios[i] / totalAmount,
              StockResourceNames.GetResourceAbbreviation(resources[i], removeLingoonaTags: false));
        }
        var resourceNames = resources.Select(r => StockResourceNames.GetResourceTitle(r)).ToArray();
        caption.text = string.Join("\n", texts);
        caption.tooltip = MixtureHint.Format(string.Join(" + ", resourceNames));
      }
    }

    /// <summary>Aborts any transfers that were in progress.</summary>
    public void StopAllTransfers() {
      _leftToRightTransferPress = false;
      _leftToRightTransferToggle = false;
      _rightToLeftTransferPress = false;
      _rightToLeftTransferToggle = false;
    }

    /// <summary>Updates the transfer trigger flag.</summary>
    /// <remarks>Ensures that the values are not updated when the property hasn't changed.</remarks>
    void UpdateTransferTriggerFlag(ref bool property, bool newValue) {
      if (property != newValue) {
        if (property) {
          StopAllTransfers();
        }
        property = newValue;
        if (newValue) {
          previousUpdate = Planetarium.GetUniversalTime();
        }
      }
    }
  }
  #endregion

  #region KASLinkSourcePhysical overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    GameEvents.onVesselWasModified.Add(OnVesselUpdated);
    GameEvents.onVesselDestroy.Add(OnVesselUpdated);
    GameEvents.onVesselCreate.Add(OnVesselUpdated);
  }

  /// <inheritdoc/>
  public override void OnDestroy() {
    base.OnDestroy();
    GameEvents.onVesselWasModified.Remove(OnVesselUpdated);
    GameEvents.onVesselDestroy.Remove(OnVesselUpdated);
    GameEvents.onVesselCreate.Remove(OnVesselUpdated);
  }

  /// <inheritdoc/>
  public override void OnLoad(ConfigNode node) {
    base.OnLoad(node);
    rotaingCylinder = Hierarchy.FindPartModelByPath(part, rotatingWinchCylinderModel);
    ConfigAccessor.ReadFieldsInType(GetType(), this);
  }

  /// <inheritdoc/>
  public override void OnUpdate() {
    base.OnUpdate();
    if (rotaingCylinder != null) {
      if (cableJoint.realCableLength > float.Epsilon) {
        var angle = 360.0f
            * (cableJoint.realCableLength % cylinderPerimeterLength) / cylinderPerimeterLength;
        rotaingCylinder.localRotation = Quaternion.Euler(angle, 0, 0);
      }
    }
  }

  /// <inheritdoc/>
  public override void LocalizeModule() {
    base.LocalizeModule();
    resourceRows.ToList().ForEach(x => x.UpdateStaticStrings());

    autoScaleToggleCnt = new GUIContent(
        AutoScaleToggleTxt, AutoScaleToggleHint.Format(autolSpeedTransferDuration));
    leftToRigthToggleCnt = new GUIContent("<<", LeftToRigthToggleHint);
    leftToRigthButtonCnt = new GUIContent("<", LeftToRigthButtonHint);
    rightToLeftToggleCnt = new GUIContent(">>", RightToLeftToggleHint);
    rightToLeftButtonCnt = new GUIContent(">", RightToLeftButtonHint);

    // Force the strings loading since their guiTags are used in GUI.
    resourceName.LoadLocalization();
    resourceAmounts.LoadLocalization();
  }
  
  /// <inheritdoc/>
  public override void UpdateContextMenu() {
    base.UpdateContextMenu();

    PartModuleUtils.SetupEvent(this, OpenGUIEvent, e => {
      e.active = linkTarget != null && !linkTarget.part.vessel.isEVA;
    });
  }

  /// <inheritdoc/>
  protected override void PhysicalLink() {
    base.PhysicalLink();
    SetCableLength(float.PositiveInfinity);
  }
  #endregion

  #region IHasGUI implementation
  /// <inheritdoc/>
  public void OnGUI() {
    isGUIOpen &= linkTarget != null;
    if (Time.timeScale <= float.Epsilon) {
      return;  // No events and menu in the paused mode.
    }
    if (isGUIOpen) {
      windowRect = GUILayout.Window(
          GetInstanceID(), windowRect, TransferResourcesWindowFunc, WindowTitleTxt,
          GUILayout.MaxHeight(1), GUILayout.MaxWidth(1));
    }
  }
  #endregion

  #region GUI methods
  /// <summary>Shows a window that displays the resource transfer controls.</summary>
  /// <param name="windowId">Window ID.</param>
  void TransferResourcesWindowFunc(int windowId) {
    // Allow the window to be dragged by its title bar.
    GuiWindow.DragWindow(ref windowRect, titleBarRect);

    MakeGuiStyles();

    // In the docked mode the players must use the stock transfer mechanism.
    if (vessel == linkTarget.part.vessel) {
      GUILayout.Label(NotAvailableInDockedMode, new GUIStyle(GUI.skin.label) { wordWrap = false });
      if (GUILayout.Button(CloseDialogBtn, MinSizeLayout)) {
        isGUIOpen = false;
      }
      SetPendingTransferOption(null);  // Cancel all transfers.
      return;
    }

    if (guiActions.ExecutePendingGuiActions()) {
      MaybeUpdateResourceOptionList();
      guiResourcesTable.UpdateFrame();
      if (pendingOption != null) {
        if (!DoTransfer()) {
          SetPendingTransferOption(null);  // Cancel all transfers.
        }
      }
      UpdateResourcesTransferGui();
    }
    
    GUILayout.Label(OwnerVesselTxt.Format(vessel.vesselName), GUI.skin.box);
    GUILayout.Label(ConnectedVesselTxt.Format(linkTarget.part.vessel.vesselName), GUI.skin.box);
    for (var i = resourceRows.Length - 1; i >= 0; i--) {
      var row = resourceRows[i];
      guiResourcesTable.StartNewRow();
      using (new GUILayout.HorizontalScope()) {
        guiResourcesTable.AddTextColumn(
            row.caption, guiResourceStyle, minWidth: resourceName.guiTags.minWidth);
        guiResourcesTable.AddTextColumn(
            row.leftInfo, guiNoWrapCenteredStyle, minWidth: resourceAmounts.guiTags.minWidth);
        using (new GuiEnabledStateScope(row.canMoveRightToLeft)) {
          row.rightToLeftTransferToggle = GUILayoutButtons.Toggle(
              row.rightToLeftTransferToggle, leftToRigthToggleCnt, guiTransferBtnStyle, null,
              GuiActionUpdateTransferItem, GuiActionUpdateTransferItem, guiActions);
          row.rightToLeftTransferPress = GUILayoutButtons.Push(
              row.rightToLeftTransferPress, leftToRigthButtonCnt, guiTransferBtnStyle, null,
              GuiActionUpdateTransferItem, GuiActionUpdateTransferItem, guiActions);
        }
        using (new GuiEnabledStateScope(row.canMoveLeftToRight)) {
          row.leftToRightTransferPress = GUILayoutButtons.Push(
              row.leftToRightTransferPress, rightToLeftButtonCnt, guiTransferBtnStyle, null,
              GuiActionUpdateTransferItem, GuiActionUpdateTransferItem, guiActions);
          row.leftToRightTransferToggle = GUILayoutButtons.Toggle(
              row.leftToRightTransferToggle, rightToLeftToggleCnt, guiTransferBtnStyle, null,
              GuiActionUpdateTransferItem, GuiActionUpdateTransferItem, guiActions);
        }
        guiResourcesTable.AddTextColumn(
            row.rightInfo, guiNoWrapCenteredStyle, minWidth: resourceAmounts.guiTags.minWidth);
        guiResourcesTable.AddTextColumn(
            row.caption, guiResourceStyle, minWidth: resourceName.guiTags.minWidth);
      }
    }

    // Resource transfer speed.
    autoScaleSpeed = GUILayoutButtons.Toggle(
        autoScaleSpeed, autoScaleToggleCnt, GUI.skin.toggle, null,
        MaybeAutoScaleSpeed, null, guiActions);
    using (new GuiEnabledStateScope(!autoScaleSpeed)) {
      transferSpeed = GUILayout.HorizontalSlider(transferSpeed, 0f, maxTransferSpeed);
      if (transferSpeed < float.Epsilon && pendingOption != null) {
        guiActions.Add(() => SetPendingTransferOption(null));  // Cancel all transfers.
      }
    }
    GUILayout.Label(TransferSpeedTxt.Format(transferSpeed));

    using (new GUILayout.HorizontalScope()) {
      if (GUILayout.Button(CloseDialogBtn, MinSizeLayout)) {
        guiActions.Add(() => isGUIOpen = false);
      }
      GUILayout.Label("");
      GUI.Label(GUILayoutUtility.GetLastRect(), GUI.tooltip);
    }
  }

  /// <summary>Finds the currently active option and makes it active.</summary>
  void GuiActionUpdateTransferItem() {
    var row = resourceRows.FirstOrDefault(r =>
        r != pendingOption && (r.leftToRightTransferPress || r.leftToRightTransferToggle
                               || r.rightToLeftTransferPress || r.rightToLeftTransferToggle));
    SetPendingTransferOption(row);
    MaybeAutoScaleSpeed();
  }

  /// <summary>Creates the styles. Only does it once.</summary>
  void MakeGuiStyles() {
    if (guiNoWrapCenteredStyle == null) {
      guiNoWrapCenteredStyle = new GUIStyle(GUI.skin.box);
      guiNoWrapCenteredStyle.wordWrap = false;
      guiNoWrapCenteredStyle.alignment = TextAnchor.MiddleCenter;
      guiResourceStyle = new GUIStyle(guiNoWrapCenteredStyle);
      guiTransferBtnStyle = new GUIStyle(GUI.skin.button);
      guiTransferBtnStyle.alignment = TextAnchor.MiddleCenter;
      guiTransferBtnStyle.stretchHeight = true;
    }
  }
  #endregion

  #region Local utility methods
  /// <summary>
  /// If the auto-scale options is chosen, finds the scale at which the whole amount of teh resource
  /// will be transferred in a definite duration.
  /// </summary>
  void MaybeAutoScaleSpeed() {
    if (!autoScaleSpeed || pendingOption == null) {
      return;
    }
    // Determine the maximum unscaled amount to transfer.
    var maxUnscaledAmount = double.PositiveInfinity;
    for (var i = pendingOption.resources.Length - 1; i >= 0; i--) {
      var unit = pendingOption.resourceRatios[i];
      var resource = pendingOption.resources[i];
      var amount = currentFromPartAmounts[i] / unit;
      if (amount < maxUnscaledAmount) {
        maxUnscaledAmount = amount;
      }
      var capacity = (currentToPartCapacities[i] - currentToPartAmounts[i]) / unit;
      if (capacity < maxUnscaledAmount) {
        maxUnscaledAmount = capacity;
      }
    }

    transferSpeed = Mathf.Min(maxTransferSpeed, (float) maxUnscaledAmount / autolSpeedTransferDuration);
  }
  
  /// <summary>Does actual resource transfer on the selected option.</summary>
  /// <remarks>This method must be performance optimized since it called each frame.</remarks>
  bool DoTransfer() {
    var updateDelta = Planetarium.GetUniversalTime() - pendingOption.previousUpdate;
    pendingOption.previousUpdate = Planetarium.GetUniversalTime();
    if (updateDelta < float.Epsilon) {
      return true;  // Cannot do transfer, but the state must not be reset yet.
    }

    // Below a tricky logic starts. It's intended to properly work with the mixtures of multiple
    // resources. When moving a mixture, we should know in advance how much amount of each component
    // can be transferred before the capacity/reserve limit hit.
    var resources = pendingOption.resources;
    var moveAmounts = new double[pendingOption.resources.Length];
    for (var i = moveAmounts.Length - 1; i >= 0; i--) {
      moveAmounts[i] = transferSpeed * pendingOption.resourceRatios[i] * updateDelta;
    }
    // Now, check if each component request transfer can be fulfilled.
    var scale = 1.0;
    for (var i = moveAmounts.Length - 1; i >= 0; i--) {
      currentFromPart.GetConnectedResourceTotals(
          resources[i], ResourceFlowMode.ALL_VESSEL_BALANCE,
          out currentFromPartAmounts[i], out currentFromPartCapacities[i]);
      currentToPart.GetConnectedResourceTotals(
          resources[i], ResourceFlowMode.ALL_VESSEL_BALANCE,
          out currentToPartAmounts[i], out currentToPartCapacities[i]);
      var amount = moveAmounts[i];
      if (amount > currentFromPartAmounts[i]) {
        amount = currentFromPartAmounts[i];
      }
      if (amount > currentToPartCapacities[i] - currentToPartAmounts[i]) {
        amount = currentToPartCapacities[i] - currentToPartAmounts[i];
      }
      var newScale = amount / moveAmounts[i];
      if (newScale < scale) {
        scale = newScale;
      }
    }
    // Do the transfer with respect to the scale.
    for (var i = moveAmounts.Length - 1; i >= 0; i--) {
      var resource = resources[i];
      var amount = scale * moveAmounts[i];
      var actualAmount = currentFromPart.RequestResource(
          resource, amount, ResourceFlowMode.ALL_VESSEL_BALANCE);
      currentToPart.RequestResource(
          resource, -actualAmount, ResourceFlowMode.ALL_VESSEL_BALANCE);
    }
    return Mathd.AreSame(scale, 1.0);
  }

  /// <summary>Updates GUI for all the resources.</summary>
  /// <remarks>
  /// To not waste too much CPU, this method opdates by timer. However, when an instant update is
  /// needed, it can be requested via the parameter.
  /// </remarks>
  /// <param name="force">Tells if GUI must be upadted regardless to the timer.</param>
  void UpdateResourcesTransferGui(bool force = false) {
    if (!force && Time.unscaledTime - lastResourcesGUIUpdate < TRANSFER_STATE_UPDATE_PERIOD) {
      return;
    }
    lastResourcesGUIUpdate = Time.unscaledTime;
    for (var i = resourceRows.Length - 1; i >= 0; i--) {
      UpdateOptionTransferGui(resourceRows[i]);
    }
  }

  /// <summary>Updates the resources amounts and the transfer states in GUI.</summary>
  /// <remarks>This method must be performance optimized.</remarks>
  /// <param name="resOption">The resource transfer option to update.</param>
  void UpdateOptionTransferGui(ResourceTransferOption resOption) {
    var leftInfoString = "";
    var rightInfoString = "";
    resOption.canMoveRightToLeft = true;
    resOption.canMoveLeftToRight = true;
    for (var i = 0; i < resOption.resources.Length; i++) {
      part.GetConnectedResourceTotals(
          resOption.resources[i], ResourceFlowMode.ALL_VESSEL_BALANCE,
          out resOption.leftAmounts[i], out resOption.leftCapacities[i]);
      leftInfoString += (i > 0 ? "\n" : "")
          + CompactNumberType.Format(resOption.leftAmounts[i])
          + " / "
          + CompactNumberType.Format(resOption.leftCapacities[i]);
      linkTarget.part.GetConnectedResourceTotals(
          resOption.resources[i], ResourceFlowMode.ALL_VESSEL_BALANCE,
          out resOption.rightAmounts[i], out resOption.rightCapacities[i]);
      rightInfoString += (i > 0 ? "\n" : "")
          + CompactNumberType.Format(resOption.rightAmounts[i])
          + " / "
          + CompactNumberType.Format(resOption.rightCapacities[i]);
      if (resOption.rightAmounts[i] < double.Epsilon
          || resOption.leftAmounts[i] >= resOption.leftCapacities[i]) {
        resOption.canMoveRightToLeft = false;
      }
      if (resOption.leftAmounts[i] < double.Epsilon
          || resOption.rightAmounts[i] >= resOption.rightCapacities[i]) {
        resOption.canMoveLeftToRight = false;
      }
    }
    resOption.leftInfo.text = leftInfoString;
    resOption.rightInfo.text = rightInfoString;
  }

  /// <summary>
  /// Makes the list of all fuels and mixtures that can be moved between the linked vessels.
  /// </summary>
  /// <remarks>This is a very expensive operation.</remarks>
  void MaybeUpdateResourceOptionList() {
    if (!resourceListNeedsUpdate) {
      return; // Nothing to do.
    }
    resourceListNeedsUpdate = false;
    HostedDebugLog.Fine(this, "Refreshing resources...");

    // Gather all the resources that *both* vessel have.
    var leftResources = new HashSet<int>(
        vessel.parts
            .SelectMany(p => p.Resources)
            .Select(r => r.info.id));
    var rightResources = new HashSet<int>(
        linkTarget.part.vessel.parts
            .SelectMany(p => p.Resources)
            .Select(r => r.info.id));
    var availableResources = leftResources
        .Union(rightResources)
        .Distinct()
        .ToList();

    // Find the predefined resources that the part can pump between the vessels.
    var allowedResourceIds = allowedResource
        .Select(x => StockResourceNames.GetId(x))
        .ToArray();

    if (allowedResourceIds.Length == 0) {
      // If no specific resources set, then allow all the vessel resources that are material and
      // not restricted for pumping. Allow overriding to include/exclude a specific resource. 
      var overrideEnabled = resourceOverride
          .Where(x => x.Length > 0 && x[0] == '+')
          .Select(x => StockResourceNames.GetId(x.Substring(1)))
          .ToArray();
      var overrideDisabled = resourceOverride
          .Where(x => x.Length > 0 && x[0] == '-')
          .Select(x => StockResourceNames.GetId(x.Substring(1)));
      var nonMovableIds = PartResourceLibrary.Instance.resourceDefinitions
          .Cast<PartResourceDefinition>()
          .Where(d => overrideEnabled.IndexOf(d.id) == -1
                      && (d.unitCost < float.Epsilon
                          || d.volume < float.Epsilon
                          || d.resourceTransferMode == ResourceTransferMode.NONE))
          .Select(d => d.id)
          .Union(overrideDisabled)
          .ToArray();
      allowedResourceIds = availableResources
          .Where(x => nonMovableIds.IndexOf(x) == -1)
          .ToArray();
    }
    var movableResources = availableResources
        .Where(id => allowedResourceIds.IndexOf(id) != -1)
        // The GUI function will render the list in the reversed order.
        .OrderByDescending(id => id)
        .Select(id => new ResourceTransferOption(new[] {id}, new[] {1.0}))
        .ToList();

    // Add the mixtures.
    var availableMixtures = fuelMixtures
        .Where(m =>
            m.components.All(c => availableResources.Contains(StockResourceNames.GetId(c.name))));
    foreach (var mixture in availableMixtures) {
      movableResources.Insert(0, new ResourceTransferOption(
          mixture.components.Select(x => StockResourceNames.GetId(x.name)).ToArray(),
          mixture.components.Select(x => x.ratio).ToArray()));
    }

    resourceRows = movableResources
        .Select(resource => resourceRowsHash.ContainsKey(resource.GetHashCode())
            ? resourceRowsHash[resource.GetHashCode()]
            : resource)
        .ToArray();
    resourceRowsHash = resourceRows.ToDictionary(r => r.GetHashCode());
  }

  /// <summary>Sets the currently transferring option. Erasing the previous one.</summary>
  /// <param name="newOption">The new option or <c>null</c>.</param>
  void SetPendingTransferOption(ResourceTransferOption newOption) {
    if (newOption != pendingOption && pendingOption != null) {
      pendingOption.StopAllTransfers();
    }
    pendingOption = newOption;
    if (pendingOption == null && autoScaleSpeed) {
      transferSpeed = 1.0f;
    }
    if (pendingOption != null) {
      if (pendingOption.leftToRightTransferPress || pendingOption.leftToRightTransferToggle) {
        currentFromPart = part;
        currentFromPartCapacities = pendingOption.leftCapacities;
        currentFromPartAmounts = pendingOption.leftAmounts;
        currentToPart = linkTarget.part;
        currentToPartCapacities = pendingOption.rightCapacities;
        currentToPartAmounts = pendingOption.rightAmounts;
      } else {
        currentFromPart = linkTarget.part;
        currentFromPartCapacities = pendingOption.rightCapacities;
        currentFromPartAmounts = pendingOption.rightAmounts;
        currentToPart = part;
        currentToPartCapacities = pendingOption.leftCapacities;
        currentToPartAmounts = pendingOption.leftAmounts;
      }
    } else {
      currentFromPartCapacities = null;
      currentFromPartAmounts = null;
      currentToPartCapacities = null;
      currentToPartAmounts = null;
    }
    UpdateResourcesTransferGui(force: true);
  }

  /// <summary>
  /// Forces an update of the list of the available resources. It's an expensive operation.
  /// </summary>
  void OnVesselUpdated(Vessel v) {
    resourceListNeedsUpdate = true;
  }
  #endregion
}

}  // namespace
