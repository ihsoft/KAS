// Kerbal Attachment System
// Mod idea: KospY (http://forum.kerbalspaceprogram.com/index.php?/profile/33868-kospy/)
// Module author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KSPDev.GUIUtils;
using KSPDev.PartUtils;
using KSPDev.ResourceUtils;
using KSPDev.MathUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KAS {

/// <summary>Module which trasnfer resources between two linked vessels.</summary>
/// <seealso cref="KASLinkSourcePhysical"/>
// Next localization ID: #kasLOC_12016
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
      description: "The resource in the transfer options table. Its main purpose is dealing dealing"
      + " with the Lingoona modifiers, applied to the resource name."
      + "\nArgument <<1>> is the full localized resource name with the Lingoona modifiers"
      + " (if any).");

  /// <include file="SpecialDocTags.xml" path="Tags/Message2/*"/>
  static readonly Message<PercentType, string> compoundResourceName =
      new Message<PercentType, string>(
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
      defaultTemplate: "Owner: <<1>>",
      description: "The string that tells which vessels owns the resource transfer part."
      + "\nArgument <<1>> is the name of the owner vessel.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  static readonly Message<string> ConnectedVesselTxt = new Message<string>(
      "#kasLOC_12007",
      defaultTemplate: "Connected: <<1>>",
      description: "The string that tells which vessels is connected to the resource transfer part."
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
  #endregion

  #region Part's config fields
  /// <summary>The maximum allowed speed of transferring a resource.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float maxTransferSpeed = 20.0f;

  /// <summary>
  /// The duration of the complete transfer when the speed is selected automatically.
  /// </summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  public float autolSpeedTransferDuration = 4.0f;
  #endregion

  #region Context menu events/actions
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActive = true, guiActiveUnfocused = true)]
  [LocalizableItem(
      tag = "#kasLOC_12015",
      defaultTemplate = "Open GUI",
      description = "The context menu event that opens the resources transfer GUI.")]
  public void OpenGUIEvent() {
    if (isLinked && vessel != linkTarget.part.vessel) {
      isGUIOpen = true;
      leftVessel = vessel;
      rightVessel = linkTarget.part.vessel;
      UpdateResourceOptionList();
    } else {
      isGUIOpen = false;
      leftVessel = null;
      rightVessel = null;
    }
  }
  #endregion

  #region Local fields & properties
  /// <summary>Actual screen position of the console window.</summary>
  static Rect windowRect = new Rect(100, 100, 1, 1);
  
  /// <summary>A title bar location.</summary>
  static Rect titleBarRect = new Rect(0, 0, 10000, 20);

  /// <summary>A list of actions to apply at the end of the GUI frame.</summary>
  static readonly GuiActionsList guiActions = new GuiActionsList();

  /// <summary>Style to draw a control of the minimum size.</summary>
  static readonly GUILayoutOption MinSizeLayout = GUILayout.ExpandWidth(false);

  /// <summary>Tells if GUI is open.</summary>
  bool isGUIOpen;

  /// <summary>GUI table to align resource names and quantities.</summary>
  /// <remarks>Left Name + Left Amount + Right Amount + Right Name</remarks>
  readonly GUILayoutStringTable guiResourcesTable = new GUILayoutStringTable(4);

  /// <summary>Defintion of all the resources for the both linked vessels.</summary>
  ResourceTransferOption[] resourceRows;

  /// <summary>Index of the vessels resources.</summary>
  Dictionary<int, ResourceTransferOption> resourceRowsHash =
      new Dictionary<int, ResourceTransferOption>();

  /// <summary>The vessel that owns the module. The owner.</summary>
  Vessel leftVessel;

  /// <summary>The vessel that is connected to the module. The target.</summary>
  Vessel rightVessel;

  /// <summary>The currently behaving resource transfer.</summary>
  ResourceTransferOption pendingOption;

  /// <summary>The current resource transafer speed.</summary>
  float transferSpeed = 1.0f;

  /// <summary>Tells if the transfer speed can be managed by the code.</summary>
  bool autoScaleSpeed;

  /// <summary>List of all fuel types that cannot be moved.</summary>
  /// TODO(ihsoft): Make it configurable.
  int[] unmovableResources = {
      StockResourceNames.GetId(StockResourceNames.SolidFuel)
  };
  #endregion

  #region GUI styles & contents
  GUIStyle guiNoWrapStyle;
  GUIStyle guiNoWrapCenteredStyle;
  GUIStyle guiResourceStyle;
  GUIStyle guiTransferBtnStyle;
  GUIStyle guiLabelCenteredStyle;
  GUIContent autoScaleToggleCnt;
  GUIContent leftToRigthToggleCnt;
  GUIContent leftToRigthButtonCnt;
  GUIContent rightToLeftToggleCnt;
  GUIContent rightToLeftButtonCnt;
  #endregion

  #region Local types
  class ResourceTransferOption {
    public readonly int[] resources;
    public readonly double[] resourceRatios;
    public readonly double[] leftAmounts;
    public readonly double[] leftCapacities;
    public readonly double[] rightAmounts;
    public readonly double[] rightCapacities;

    //FIXME: can be omitted. we work in a glbal context.
    public readonly Part leftPart;
    public readonly Part rightPart;

    //FIXME: make readonly and create once
    public GUIContent caption;
    public GUIContent leftInfo;
    public GUIContent rightInfo;

    public bool canMoveRightToLeft;
    public bool canMoveLeftToRight;
    public float previousUpdate;

    public bool leftToRightTransferPending {
      get { return _leftToRightTransferPending; }
      set {
        if (value != _leftToRightTransferPending) {
          if (value) {
            StopAllTransfers();
          }
          previousUpdate = Time.time;
          _leftToRightTransferPending = value;
        }
      }
    }
    bool _leftToRightTransferPending;

    public bool leftToRightTransferCurrent {
      get { return _leftToRightTransferCurrent; }
      set {
        if (value != _leftToRightTransferCurrent) {
          if (value) {
            StopAllTransfers();
          }
          previousUpdate = Time.time;
          _leftToRightTransferCurrent = value;
        }
      }
    }
    bool _leftToRightTransferCurrent;

    public bool rightToLeftTransferCurrent {
      get { return _rightToLeftTransferCurrent; }
      set {
        if (value != _rightToLeftTransferCurrent) {
          if (value) {
            StopAllTransfers();
          }
          previousUpdate = Time.time;
          _rightToLeftTransferCurrent = value;
        }
      }
    }
    bool _rightToLeftTransferCurrent;
      
    public bool rightToLeftTransferPending {
      get { return _rightToLeftTransferPending; }
      set {
        if (value != _rightToLeftTransferPending) {
          if (value) {
            StopAllTransfers();
          }
          previousUpdate = Time.time; //FIXME: only if value is true
          _rightToLeftTransferPending = value;
        }
      }
    }
    bool _rightToLeftTransferPending;

    public override int GetHashCode() {
      return resources.Sum();
    }

    public ResourceTransferOption(
        KASLinkResourceConnector parent,
        IEnumerable<int> availabeResources, IEnumerable<double> resourceRatio) {
      leftPart = parent.part;
      rightPart = parent.linkTarget.part;
      resources = availabeResources.ToArray();
      resourceRatios = resourceRatio.ToArray();
      leftAmounts = new double[resources.Length];
      leftCapacities = new double[resources.Length];
      rightAmounts = new double[resources.Length];
      rightCapacities = new double[resources.Length];
      UpdateStrings();
    }

    //FIXME: call it from localization callback
    public void UpdateStrings() {
      //FIXME: move to init/localization method
      resourceAmounts.LoadLocalization();  // Kick-on the template loading.
      if (resources.Length == 1) {
        caption = new GUIContent(
            resourceName.Format(StockResourceNames.GetResourceTitle(
                resources[0], removeLingoonaTags: false)));
      } else {
        var texts = new string[resources.Length];
        var totalAmount = resourceRatios.Sum();
        for (var i = 0; i < resources.Length; i++) {
          texts[i] = compoundResourceName.Format(
              100.0 * resourceRatios[i] / totalAmount,
              StockResourceNames.GetResourceAbbreviation(resources[i], removeLingoonaTags: false));
        }
        var resourceNames = resources.Select(r => StockResourceNames.GetResourceTitle(r)).ToArray();
        caption = new GUIContent(
            string.Join("\n", texts), MixtureHint.Format(string.Join(" + ", resourceNames)));
      }
    }

    public void StopAllTransfers() {
      _leftToRightTransferCurrent = false;
      _leftToRightTransferPending = false;
      _rightToLeftTransferCurrent = false;
      _rightToLeftTransferPending = false;
    }
  }
  #endregion

  #region KASLinkSourcePhysical overrides
  /// <inheritdoc/>
  public override void LocalizeModule() {
    base.LocalizeModule();
    if (resourceRows != null) {
      foreach (var row in resourceRows) {
        row.UpdateStrings();
      }
    }
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
      e.active = linkTarget != null && vessel != linkTarget.part.vessel
                 && !linkTarget.part.vessel.isEVA;
    });
  }
  #endregion

  #region IHasGUI implementation
  /// <inheritdoc/>
  public void OnGUI() {
    isGUIOpen &= linkTarget != null && vessel != linkTarget.part.vessel;
    if (isGUIOpen) {
      windowRect = GUILayout.Window(0, windowRect, TransferResourcesWindowFunc, WindowTitleTxt,
                                    GUILayout.MaxHeight(1), GUILayout.MaxWidth(1));
    }
  }
  #endregion

  #region GUI methods
  /// <summary>Shows a window that displays the resource transfer controls.</summary>
  /// <param name="windowId">Window ID.</param>
  void TransferResourcesWindowFunc(int windowId) {
    MakeGuiStyles();

    if (guiActions.ExecutePendingGuiActions()) {
      guiResourcesTable.UpdateFrame();
      //TODO(ihsoft): Update resources on the vessel events.
      UpdateResourceOptionList();
      if (pendingOption != null) {
        if (!DoTransfer()) {
          SetPendingTransferOption(null);  // Cancel all transfers.
        }
      }
      //TODO(ihsoft): Check for an update timeout, don't do it in each frame.
      for (var i = resourceRows.Length - 1; i >= 0; i--) {
        UpdateTransferState(resourceRows[i]);
      }
    }
    
    GUILayout.Label(OwnerVesselTxt.Format(leftVessel.vesselName), GUI.skin.box);
    GUILayout.Label(ConnectedVesselTxt.Format(rightVessel.vesselName), GUI.skin.box);
    for (var i = resourceRows.Length - 1; i >= 0; i--) {
      var row = resourceRows[i];
      guiResourcesTable.StartNewRow();
      using (new GUILayout.HorizontalScope()) {
        guiResourcesTable.AddTextColumn(
            row.caption, guiResourceStyle, minWidth: resourceName.guiTags.minWidth);
        guiResourcesTable.AddTextColumn(
            row.leftInfo, guiNoWrapCenteredStyle, minWidth: resourceAmounts.guiTags.minWidth);
        using (new GuiEnabledStateScope(row.canMoveRightToLeft)) {
          row.rightToLeftTransferPending = GUILayoutButtons.Toggle(
              row.rightToLeftTransferPending, leftToRigthToggleCnt, guiTransferBtnStyle, null,
              GuiActionUpdateTransferItem, GuiActionUpdateTransferItem, guiActions);
          row.rightToLeftTransferCurrent = GUILayoutButtons.Push(
              row.rightToLeftTransferCurrent, leftToRigthButtonCnt, guiTransferBtnStyle, null,
              GuiActionUpdateTransferItem, GuiActionUpdateTransferItem, guiActions);
        }
        using (new GuiEnabledStateScope(row.canMoveLeftToRight)) {
          row.leftToRightTransferCurrent = GUILayoutButtons.Push(
              row.leftToRightTransferCurrent, rightToLeftButtonCnt, guiTransferBtnStyle, null,
              GuiActionUpdateTransferItem, GuiActionUpdateTransferItem, guiActions);
          row.leftToRightTransferPending = GUILayoutButtons.Toggle(
              row.leftToRightTransferPending, rightToLeftToggleCnt, guiTransferBtnStyle, null,
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

    // Allow the window to be dragged by its title bar.
    GuiWindow.DragWindow(ref windowRect, titleBarRect);
  }

  void GuiActionUpdateTransferItem() {
    var row = resourceRows.FirstOrDefault(r =>
        r != pendingOption && (r.leftToRightTransferCurrent || r.leftToRightTransferPending
                               || r.rightToLeftTransferCurrent || r.rightToLeftTransferPending));
    SetPendingTransferOption(row);
    MaybeAutoScaleSpeed();
  }

  void MakeGuiStyles() {
    if (guiNoWrapStyle == null) {
      guiNoWrapStyle = new GUIStyle(GUI.skin.box);
      guiNoWrapStyle.wordWrap = false;
      guiNoWrapStyle.alignment = TextAnchor.MiddleLeft;
      guiNoWrapCenteredStyle = new GUIStyle(guiNoWrapStyle);
      guiNoWrapCenteredStyle.alignment = TextAnchor.MiddleCenter;
      guiResourceStyle = new GUIStyle(guiNoWrapCenteredStyle);
      guiTransferBtnStyle = new GUIStyle(GUI.skin.button);
      guiTransferBtnStyle.alignment = TextAnchor.MiddleCenter;
      guiTransferBtnStyle.stretchHeight = true;
      guiLabelCenteredStyle = new GUIStyle(GUI.skin.label);
      guiLabelCenteredStyle.alignment = TextAnchor.MiddleCenter;
    }
  }
  #endregion

  #region Local utility methods
  void MaybeAutoScaleSpeed() {
    if (!autoScaleSpeed || pendingOption == null) {
      return;
    }
    //FIXME: pre-cache selections somehow
    double[] fromPartCapacities;
    double[] fromPartAmounts;
    double[] toPartCapacities;
    double[] toPartAmounts;
    if (pendingOption.leftToRightTransferCurrent || pendingOption.leftToRightTransferPending) {
      fromPartCapacities = pendingOption.leftCapacities;
      fromPartAmounts = pendingOption.leftAmounts;
      toPartCapacities = pendingOption.rightCapacities;
      toPartAmounts = pendingOption.rightAmounts;
    } else {
      fromPartCapacities = pendingOption.rightCapacities;
      fromPartAmounts = pendingOption.rightAmounts;
      toPartCapacities = pendingOption.leftCapacities;
      toPartAmounts = pendingOption.leftAmounts;
    }

    // Determine the maximum unscaled amount to transfer.
    var maxUnscaledAmount = double.PositiveInfinity;
    for (var i = pendingOption.resources.Length - 1; i >= 0; i--) {
      var unit = pendingOption.resourceRatios[i];
      var resource = pendingOption.resources[i];
      var amount = fromPartAmounts[i] / unit;
      if (amount < maxUnscaledAmount) {
        maxUnscaledAmount = amount;
      }
      var capacity = (toPartCapacities[i] - toPartAmounts[i]) / unit;
      if (capacity < maxUnscaledAmount) {
        maxUnscaledAmount = capacity;
      }
    }

    transferSpeed = Mathf.Min(maxTransferSpeed, (float) maxUnscaledAmount / autolSpeedTransferDuration);
  }
  
  /// <summary>Does actual resource transfer on the selected option.</summary>
  /// <remarks>This method must be performance optimized since it called each frame.</remarks>
  bool DoTransfer() {
    var updateDelta = Time.time - pendingOption.previousUpdate;
    pendingOption.previousUpdate = Time.time;
    if (updateDelta < float.Epsilon) {
      return true;  // Cannot do transfer, but the state must not be reset yet.
    }
    
    //FIXME: pre-cache selections somehow
    Part fromPart;
    double[] fromPartCapacities;
    double[] fromPartAmounts;
    Part toPart;
    double[] toPartCapacities;
    double[] toPartAmounts;
    if (pendingOption.leftToRightTransferCurrent || pendingOption.leftToRightTransferPending) {
      fromPart = pendingOption.leftPart;
      fromPartCapacities = pendingOption.leftCapacities;
      fromPartAmounts = pendingOption.leftAmounts;
      toPart = pendingOption.rightPart;
      toPartCapacities = pendingOption.rightCapacities;
      toPartAmounts = pendingOption.rightAmounts;
    } else if (pendingOption.rightToLeftTransferCurrent || pendingOption.rightToLeftTransferPending) {
      fromPart = pendingOption.rightPart;
      fromPartCapacities = pendingOption.rightCapacities;
      fromPartAmounts = pendingOption.rightAmounts;
      toPart = pendingOption.leftPart;
      toPartCapacities = pendingOption.leftCapacities;
      toPartAmounts = pendingOption.leftAmounts;
    } else {
      fromPart = null;
      fromPartCapacities = null;
      fromPartAmounts = null;
      toPart = null;
      toPartCapacities = null;
      toPartAmounts = null;
      return false;
    }

    // Below a tricky logic starts. It's intendent to properly work with the mixtures of multiple
    // resources. When moving a mixture we should know in advance how much amount of each component
    // can be transferred before the capacity/reserve limit hit.
    var resources = pendingOption.resources;
    var moveAmounts = new double[pendingOption.resources.Length];
    for (var i = moveAmounts.Length - 1; i >= 0; i--) {
      moveAmounts[i] = transferSpeed * pendingOption.resourceRatios[i] * updateDelta;
    }
    // Now, check if each component request transfer can be fulfilled.
    var scale = 1.0;
    for (var i = moveAmounts.Length - 1; i >= 0; i--) {
      fromPart.GetConnectedResourceTotals(
          resources[i], out fromPartAmounts[i], out fromPartCapacities[i]);
      toPart.GetConnectedResourceTotals(
          resources[i], out toPartAmounts[i], out toPartCapacities[i]);
      var amount = moveAmounts[i];
      if (amount > fromPartAmounts[i]) {
        amount = fromPartAmounts[i];
      }
      if (amount > toPartCapacities[i] - toPartAmounts[i]) {
        amount = toPartCapacities[i] - toPartAmounts[i];
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
      var actualAmount = fromPart.RequestResource(resource, amount);
      toPart.RequestResource(resource, -actualAmount);
    }
    return Mathd.AreSame(scale, 1.0);
  }

  /// <summary>Updates the resources amounts and the transfer states.</summary>
  /// <remarks>This method must be performance optimized.</remarks>
  /// <param name="resOption">The resource transfer option to update.</param>
  void UpdateTransferState(ResourceTransferOption resOption) {
    var leftInfoString = "";
    var rightInfoString = "";
    resOption.canMoveRightToLeft = true;
    resOption.canMoveLeftToRight = true;
    for (var i = 0; i < resOption.resources.Length; i++) {
      resOption.leftPart.GetConnectedResourceTotals(
          resOption.resources[i], out resOption.leftAmounts[i], out resOption.leftCapacities[i]);
      leftInfoString += (i > 0 ? "\n" : "")
          + CompactNumberType.Format(resOption.leftAmounts[i])
          + " / "
          + CompactNumberType.Format(resOption.leftCapacities[i]);
      resOption.rightPart.GetConnectedResourceTotals(
          resOption.resources[i], out resOption.rightAmounts[i], out resOption.rightCapacities[i]);
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
    resOption.leftInfo = new GUIContent(leftInfoString); 
    resOption.rightInfo = new GUIContent(rightInfoString);
  }

  /// <summary>
  /// Makes the list of all fuels and mixtures that can be moved between the linked vessels.
  /// </summary>
  void UpdateResourceOptionList() {
    var leftResources = new HashSet<int>(leftVessel.parts
        .SelectMany(p => p.Resources)
        .Select(r => r.info.id));
    var rightResources = new HashSet<int>(rightVessel.parts
        .SelectMany(p => p.Resources)
        .Select(r => r.info.id));
    var allResources = leftResources
        .Union(rightResources)
        .Distinct()
        .OrderByDescending(x => x)  // The GUI function will render the list in the reveresd order.
        .ToList();
    var resources = allResources
        .Where(id => unmovableResources.IndexOf(id) == -1)
        .Select(id => new ResourceTransferOption(this, new[] {id}, new[] {1.0}))
        .ToList();

    // Add known mixtures.
    // TODO(ihsoft): Load them from the config.
    if (allResources.Contains(StockResourceNames.GetId(StockResourceNames.LiquidFuel))
        && allResources.Contains(StockResourceNames.GetId(StockResourceNames.Oxidizer))) {
      resources.Insert(0, new ResourceTransferOption(
          this,
          new[] {
            StockResourceNames.GetId(StockResourceNames.LiquidFuel),
            StockResourceNames.GetId(StockResourceNames.Oxidizer)
          },
          new[] { 0.9, 1.1 }));
    }
    
    resourceRows = resources
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
  }
  #endregion
}

}  // namespace
