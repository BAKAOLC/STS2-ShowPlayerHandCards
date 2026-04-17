using Godot;
using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;
using STS2ShowPlayerHandCards.Data;
using STS2ShowPlayerHandCards;
using STS2ShowPlayerHandCards.Data.Models;
using STS2ShowPlayerHandCards.Utils;

namespace STS2ShowPlayerHandCards.Settings
{
    internal static class ModSettingsBootstrap
    {
        private static readonly Lock InitLock = new();
        private static bool _initialized;

        private static readonly string[] CardKeywordOptions =
            ["Exhaust", "Ethereal", "Innate", "Unplayable", "Retain", "Sly", "Eternal"];

        private static readonly string[] CardTypeOptions = ["Attack", "Skill", "Power", "Status", "Curse", "Quest"];

        private static readonly string[] CardRarityOptions =
            ["Basic", "Common", "Uncommon", "Rare", "Ancient", "Event", "Token", "Status", "Curse", "Quest"];

        private static readonly string[] TargetTypeOptions =
        [
            "Self", "AnyEnemy", "AllEnemies", "RandomEnemy", "AnyPlayer", "AnyAlly", "AllAllies", "TargetedNoCreature",
            "Osty",
        ];

        internal static void Initialize()
        {
            lock (InitLock)
            {
                if (_initialized)
                    return;

                var toggleKeyBinding = ModSettingsBindings.WithDefault(
                    ModSettingsBindings.Global<ModSettings, string>(
                        Const.ModId,
                        ModDataStore.SettingsKey,
                        settings => settings.ToggleKey,
                        (settings, value) => settings.ToggleKey = value),
                    () => InputHandler.DefaultToggleBinding);
                var contentScaleBinding = new RefreshingBinding<double>(ModSettingsBindings.WithDefault(
                    ModSettingsBindings.Global<ModSettings, double>(
                        Const.ModId,
                        ModDataStore.SettingsKey,
                        settings => settings.ContentScale,
                        (settings, value) => settings.ContentScale = value),
                    () => 1.0d));
                var offsetXBinding = new RefreshingBinding<double>(ModSettingsBindings.WithDefault(
                    ModSettingsBindings.Global<ModSettings, double>(
                        Const.ModId,
                        ModDataStore.SettingsKey,
                        settings => settings.PositionOffsetX,
                        (settings, value) => settings.PositionOffsetX = value),
                    () => 0d));
                var offsetYBinding = new RefreshingBinding<double>(ModSettingsBindings.WithDefault(
                    ModSettingsBindings.Global<ModSettings, double>(
                        Const.ModId,
                        ModDataStore.SettingsKey,
                        settings => settings.PositionOffsetY,
                        (settings, value) => settings.PositionOffsetY = value),
                    () => 0d));
                var manualPositioningBinding = new RefreshingBinding<bool>(ModSettingsBindings.WithDefault(
                    ModSettingsBindings.Global<ModSettings, bool>(
                        Const.ModId,
                        ModDataStore.SettingsKey,
                        settings => settings.ManualPositioningEnabled,
                        (settings, value) => settings.ManualPositioningEnabled = value),
                    () => false));
                var reserveOriginalWidthBinding = new RefreshingBinding<bool>(ModSettingsBindings.WithDefault(
                    ModSettingsBindings.Global<ModSettings, bool>(
                        Const.ModId,
                        ModDataStore.SettingsKey,
                        settings => settings.ReserveOriginalWidth,
                        (settings, value) => settings.ReserveOriginalWidth = value),
                    () => true));
                var ruleListBinding = new RefreshingBinding<List<HighlightRuleEntry>>(ModSettingsBindings.WithDefault(
                    ModSettingsBindings.Global<ModSettings, List<HighlightRuleEntry>>(
                        Const.ModId,
                        ModDataStore.SettingsKey,
                        settings => settings.HighlightRules,
                        (settings, value) => settings.HighlightRules = value),
                    () => []));

                RitsuLibFramework.RegisterModSettings(Const.ModId, page => page
                    .WithModDisplayName(ModSettingsLocalization.T("mod.displayName", "Show Player Hand Cards"))
                    .WithTitle(ModSettingsLocalization.T("page.title", "Settings"))
                    .WithDescription(ModSettingsLocalization.T("page.description",
                        "Adjust teammate hand-card preview scale, offset, and rule-based border highlights."))
                    .AddSection("display", section => section
                        .WithTitle(ModSettingsLocalization.T("section.display", "Display"))
                        .AddKeyBinding(
                            "toggle_key",
                            ModSettingsLocalization.T("toggleKey.label", "Toggle Key"),
                            new ToggleKeyBinding(toggleKeyBinding),
                            true,
                            true,
                            true,
                            ModSettingsLocalization.T("toggleKey.description",
                                "Records a keyboard shortcut; supports modifier-only bindings, modifier combos, and left/right modifier distinction."))
                        .AddSlider(
                            "content_scale",
                            ModSettingsLocalization.T("contentScale.label", "Content Size"),
                            contentScaleBinding,
                            ModSettings.MinContentScale,
                            ModSettings.MaxContentScale,
                            0.05d,
                            value => $"{value:0.00}x",
                            ModSettingsLocalization.T("contentScale.description",
                                "Scales the mini card previews shown beside each teammate."))
                        .AddSlider(
                            "position_offset_x",
                            ModSettingsLocalization.T("offsetX.label", "Horizontal Offset"),
                            offsetXBinding,
                            ModSettings.MinPositionOffset,
                            ModSettings.MaxPositionOffset,
                            1d,
                            value => value.ToString("0"),
                            ModSettingsLocalization.T("offsetX.description",
                                "Moves the hand-card row horizontally after automatic anchor adjustment."))
                        .AddSlider(
                            "position_offset_y",
                            ModSettingsLocalization.T("offsetY.label", "Vertical Offset"),
                            offsetYBinding,
                            ModSettings.MinPositionOffset,
                            ModSettings.MaxPositionOffset,
                            1d,
                            value => value.ToString("0"),
                            ModSettingsLocalization.T("offsetY.description",
                                "Moves the hand-card row vertically after automatic anchor adjustment."))
                        .AddToggle(
                            "manual_positioning_enabled",
                            ModSettingsLocalization.T("manualPositioning.label", "Enable manual positioning"),
                            manualPositioningBinding,
                            ModSettingsLocalization.T("manualPositioning.description",
                                "Lets you drag each teammate hand row directly during combat and saves the offset by slot."))
                        .AddToggle(
                            "reserve_original_width",
                            ModSettingsLocalization.T("reserveWidth.label", "Reserve original width"),
                            reserveOriginalWidthBinding,
                            ModSettingsLocalization.T("reserveWidth.description",
                                "Keeps width reserved inside the original player info row. Turn this off for pure overlay positioning."))
                        .AddButton(
                            "reset_slot_positions",
                            ModSettingsLocalization.T("resetSlotPositions.label", "Reset dragged positions"),
                            ModSettingsLocalization.T("resetSlotPositions.button", "Reset"),
                            () => HandCardDisplaySettings.ClearSlotOffsets(),
                            ModSettingsButtonTone.Normal,
                            ModSettingsLocalization.T("resetSlotPositions.description",
                                "Clears all saved per-slot hand-row offsets."))
                        .AddList(
                            "highlight_rules",
                            ModSettingsLocalization.T("rules.label", "Highlight Rules"),
                            ruleListBinding,
                            () => new(),
                            GetRuleLabel,
                            GetRuleDescription,
                            CreateRuleEditor,
                            ModSettingsStructuredData.Json<HighlightRuleEntry>(),
                            ModSettingsLocalization.T("rules.add", "Add Rule"),
                            ModSettingsLocalization.T("rules.description",
                                "Each rule can use text, regex, or native template matching."),
                            true,
                            true,
                            CreateRuleHeaderAccessory)));

                _initialized = true;
            }
        }

        private static ModSettingsText GetRuleLabel(HighlightRuleEntry item)
        {
            return ModSettingsText.Literal(item.MatchMode switch
            {
                HighlightMatchMode.Template => string.IsNullOrWhiteSpace(GetTemplateSummary(item))
                    ? ModSettingsLocalization.Get("rules.emptyItem", "(empty rule)")
                    : GetTemplateSummary(item),
                _ => string.IsNullOrWhiteSpace(item.Pattern)
                    ? ModSettingsLocalization.Get("rules.emptyItem", "(empty rule)")
                    : item.Pattern,
            });
        }

        private static ModSettingsText GetRuleDescription(HighlightRuleEntry item)
        {
            var validation = HandCardDisplaySettings.ValidateRule(item);
            if (validation.IsValid)
                return ModSettingsText.Literal(
                    $"{item.MatchMode} · {(item.Enabled ? ModSettingsLocalization.Get("rule.enabled", "Enabled") : ModSettingsLocalization.Get("rule.disabled", "Disabled"))} · {GetRuleColorSummary(item.ColorHex)}");
            var baseText = ModSettingsLocalization.Get(validation.LocalizationKey, "Invalid rule");
            return ModSettingsText.Literal(string.IsNullOrWhiteSpace(validation.Detail)
                ? baseText
                : $"{baseText}: {validation.Detail}");
        }

        private static Control CreateRuleEditor(ModSettingsListItemContext<HighlightRuleEntry> itemContext)
        {
            var row = new VBoxContainer();
            var modeGroup = new ButtonGroup();
            var textModeButton = CreateModeOptionButton(ModSettingsLocalization.Get("mode.text", "Text"),
                HighlightMatchMode.Text, itemContext.Item.MatchMode, modeGroup);
            var regexModeButton = CreateModeOptionButton(ModSettingsLocalization.Get("mode.regex", "Regex"),
                HighlightMatchMode.Regex, itemContext.Item.MatchMode, modeGroup);
            var templateModeButton = CreateModeOptionButton(ModSettingsLocalization.Get("mode.template", "Template"),
                HighlightMatchMode.Template, itemContext.Item.MatchMode, modeGroup);
            var modeRow =
                ModSettingsUiControlTheming.CreateSegmentedButtonRow(textModeButton, regexModeButton,
                    templateModeButton);

            var patternEdit = ModSettingsUiControlTheming.CreateStyledLineEdit(itemContext.Item.Pattern,
                ModSettingsLocalization.Get("rules.placeholder", "e.g. Exhaust / Poison / Retain"));
            var colorPicker = new ModSettingsColorControl(itemContext.Item.ColorHex, value =>
            {
                var updated = CloneRule(itemContext.Item);
                updated.ColorHex = value ?? string.Empty;
                if (RulesEqual(itemContext.Item, updated))
                    return;
                itemContext.Update(updated);
            });
            var keywordGroup = CreateMultiSelectGroup(ModSettingsLocalization.Get("template.keywords", "Keywords"),
                CardKeywordOptions, itemContext.Item.Keywords);
            var typeGroup = CreateMultiSelectGroup(ModSettingsLocalization.Get("template.types", "Types"),
                CardTypeOptions, itemContext.Item.Types);
            var rarityGroup = CreateMultiSelectGroup(ModSettingsLocalization.Get("template.rarities", "Rarities"),
                CardRarityOptions, itemContext.Item.Rarities);
            var targetGroup = CreateMultiSelectGroup(ModSettingsLocalization.Get("template.targets", "Target types"),
                TargetTypeOptions, itemContext.Item.TargetTypes);
            var effectsEdit = ModSettingsUiControlTheming.CreateStyledLineEdit(
                string.Join(", ", itemContext.Item.EffectTerms),
                ModSettingsLocalization.Get("template.effects", "Effects (comma separated)"), height: 38f);
            var enabledToggle = CreateRuleHeaderToggle(itemContext);
            var upgradedToggle = CreateCompactTemplateToggle(itemContext.Item.RequireUpgraded ?? false, value =>
            {
                var updated = CloneRule(itemContext.Item);
                updated.RequireUpgraded = value ? true : null;
                if (RulesEqual(itemContext.Item, updated))
                    return;
                itemContext.Update(updated);
            });
            var playableToggle = CreateCompactTemplateToggle(itemContext.Item.RequirePlayable ?? false, value =>
            {
                var updated = CloneRule(itemContext.Item);
                updated.RequirePlayable = value ? true : null;
                if (RulesEqual(itemContext.Item, updated))
                    return;
                itemContext.Update(updated);
            });
            var validationLabel = new Label
                { AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new(1f, 0.55f, 0.55f) };

            textModeButton.Toggled += pressed =>
            {
                if (pressed) Save();
            };
            regexModeButton.Toggled += pressed =>
            {
                if (pressed) Save();
            };
            templateModeButton.Toggled += pressed =>
            {
                if (pressed) Save();
            };
            HookTextCommit(patternEdit, () => Save());
            HookGroup(keywordGroup, () => Save());
            HookGroup(typeGroup, () => Save());
            HookGroup(rarityGroup, () => Save());
            HookGroup(targetGroup, () => Save());
            HookTextCommit(effectsEdit, () => Save());

            row.AddChild(modeRow);
            row.AddChild(patternEdit);
            row.AddChild(keywordGroup);
            row.AddChild(typeGroup);
            row.AddChild(rarityGroup);
            row.AddChild(targetGroup);
            var requirementRow = ModSettingsUiControlTheming.CreateCompactToggleRow(
                ModSettingsUiControlTheming.CreateCompactToggleField(
                    ModSettingsLocalization.Get("template.upgraded", "Require upgraded"), upgradedToggle),
                ModSettingsUiControlTheming.CreateCompactToggleField(
                    ModSettingsLocalization.Get("template.playable", "Require playable"), playableToggle));
            var colorRow = ModSettingsUiControlTheming.CreateCompactEditorRow(3,
                ModSettingsUiControlTheming.CreateCompactEditorField(
                    ModSettingsLocalization.Get("rule.color", "Rule Color"), colorPicker));

            row.AddChild(effectsEdit);
            row.AddChild(requirementRow);
            row.AddChild(colorRow);
            row.AddChild(validationLabel);
            RefreshVisibility();
            var initialValidation = HandCardDisplaySettings.ValidateRule(itemContext.Item);
            validationLabel.Text = initialValidation.IsValid
                ? string.Empty
                : BuildValidationMessage(initialValidation.LocalizationKey, initialValidation.Detail);
            return row;

            HighlightMatchMode GetSelectedMode()
            {
                if (regexModeButton.ButtonPressed)
                    return HighlightMatchMode.Regex;
                return templateModeButton.ButtonPressed ? HighlightMatchMode.Template : HighlightMatchMode.Text;
            }

            void RefreshVisibility()
            {
                var selectedMode = GetSelectedMode();
                var isTemplate = selectedMode == HighlightMatchMode.Template;
                patternEdit.Visible = !isTemplate;
                keywordGroup.Visible = isTemplate;
                typeGroup.Visible = isTemplate;
                rarityGroup.Visible = isTemplate;
                targetGroup.Visible = isTemplate;
                effectsEdit.Visible = isTemplate;
                upgradedToggle.Visible = isTemplate;
                playableToggle.Visible = isTemplate;
            }

            void Save(bool force = false)
            {
                var updated = new HighlightRuleEntry
                {
                    MatchMode = GetSelectedMode(),
                    Pattern = patternEdit.Text.Trim(),
                    ColorHex = colorPicker.ValueText.Trim(),
                    Enabled = enabledToggle.ButtonPressed,
                    Keywords = GetSelectedValues(keywordGroup),
                    Types = GetSelectedValues(typeGroup),
                    Rarities = GetSelectedValues(rarityGroup),
                    TargetTypes = GetSelectedValues(targetGroup),
                    EffectTerms = ParseCsv(effectsEdit.Text),
                    RequireUpgraded = upgradedToggle.ButtonPressed ? true : null,
                    RequirePlayable = playableToggle.ButtonPressed ? true : null,
                };
                var validation = HandCardDisplaySettings.ValidateRule(updated);
                validationLabel.Text = validation.IsValid
                    ? string.Empty
                    : BuildValidationMessage(validation.LocalizationKey, validation.Detail);
                RefreshVisibility();
                if (!force && RulesEqual(itemContext.Item, updated))
                    return;
                itemContext.Update(updated);
            }
        }

        private static bool RulesEqual(HighlightRuleEntry left, HighlightRuleEntry right)
        {
            return left.Pattern == right.Pattern
                   && left.ColorHex == right.ColorHex
                   && left.Enabled == right.Enabled
                   && left.MatchMode == right.MatchMode
                   && left.RequireUpgraded == right.RequireUpgraded
                   && left.RequirePlayable == right.RequirePlayable
                   && left.Keywords.SequenceEqual(right.Keywords)
                   && left.Types.SequenceEqual(right.Types)
                   && left.Rarities.SequenceEqual(right.Rarities)
                   && left.TargetTypes.SequenceEqual(right.TargetTypes)
                   && left.EffectTerms.SequenceEqual(right.EffectTerms);
        }

        private static Control CreateRuleHeaderAccessory(ModSettingsListItemContext<HighlightRuleEntry> itemContext)
        {
            return CreateRuleHeaderToggle(itemContext);
        }

        private static Button CreateRuleHeaderToggle(ModSettingsListItemContext<HighlightRuleEntry> itemContext)
        {
            var button = ModSettingsUiControlTheming.CreateCompactSettingsToggleButton(
                ModSettingsLocalization.Get("rule.enabled", "Enabled"), itemContext.Item.Enabled);
            button.Toggled += pressed =>
            {
                var updated = CloneRule(itemContext.Item);
                updated.Enabled = pressed;
                if (RulesEqual(itemContext.Item, updated))
                    return;
                itemContext.Update(updated);
            };
            return button;
        }

        private static HighlightRuleEntry CloneRule(HighlightRuleEntry item)
        {
            return new()
            {
                Pattern = item.Pattern,
                ColorHex = item.ColorHex,
                Enabled = item.Enabled,
                MatchMode = item.MatchMode,
                Keywords = [.. item.Keywords],
                Types = [.. item.Types],
                Rarities = [.. item.Rarities],
                TargetTypes = [.. item.TargetTypes],
                EffectTerms = [.. item.EffectTerms],
                RequireUpgraded = item.RequireUpgraded,
                RequirePlayable = item.RequirePlayable,
            };
        }

        private static ModSettingsToggleControl CreateCompactTemplateToggle(bool initialValue, Action<bool> onChanged)
        {
            return ModSettingsUiControlTheming.CreateCompactStateToggle(initialValue, onChanged);
        }

        private static Button CreateModeOptionButton(string text, HighlightMatchMode mode,
            HighlightMatchMode selectedMode,
            ButtonGroup group)
        {
            return ModSettingsUiControlTheming.CreateSegmentedToggleButton(text, mode == selectedMode, group);
        }

        private static string GetTemplateSummary(HighlightRuleEntry item)
        {
            var parts = new List<string>();
            if (item.Keywords.Count > 0) parts.Add($"K:{string.Join("/", item.Keywords)}");
            if (item.Types.Count > 0) parts.Add($"T:{string.Join("/", item.Types)}");
            if (item.Rarities.Count > 0) parts.Add($"R:{string.Join("/", item.Rarities)}");
            if (item.TargetTypes.Count > 0) parts.Add($"G:{string.Join("/", item.TargetTypes)}");
            if (item.EffectTerms.Count > 0) parts.Add($"E:{string.Join("/", item.EffectTerms)}");
            if (item.RequireUpgraded == true) parts.Add("Upgraded");
            if (item.RequirePlayable == true) parts.Add("Playable");
            return string.Join(" + ", parts);
        }

        private static string BuildValidationMessage(string key, string? detail)
        {
            var baseText = ModSettingsLocalization.Get(key, "Invalid rule");
            return string.IsNullOrWhiteSpace(detail) ? baseText : $"{baseText}: {detail}";
        }

        private static string GetRuleColorSummary(string? colorHex)
        {
            return string.IsNullOrWhiteSpace(colorHex)
                ? ModSettingsLocalization.Get("rule.color.default", "Default color")
                : colorHex;
        }

        private static List<string> ParseCsv(string text)
        {
            return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        private static Control CreateMultiSelectGroup(string labelText, IReadOnlyList<string> options,
            IReadOnlyCollection<string> selected)
        {
            var wrapper = new VBoxContainer();
            wrapper.AddThemeConstantOverride("separation", 6);
            wrapper.AddChild(new Label { Text = labelText });
            var grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            grid.AddThemeConstantOverride("h_separation", 8);
            grid.AddThemeConstantOverride("v_separation", 8);
            var selectedSet = new HashSet<string>(selected, StringComparer.OrdinalIgnoreCase);
            foreach (var option in options)
                grid.AddChild(
                    ModSettingsUiControlTheming.CreateSettingsToggleButton(option, selectedSet.Contains(option)));
            wrapper.AddChild(grid);
            return wrapper;
        }


        private static List<string> GetSelectedValues(Control group)
        {
            if (group.GetChildCount() < 2 || group.GetChild(1) is not GridContainer grid)
                return [];
            var result = new List<string>();
            foreach (var child in grid.GetChildren())
                if (child is Button { ButtonPressed: true } button)
                    result.Add(button.Text);
            return result;
        }

        private static void HookGroup(Control group, Action save)
        {
            if (group.GetChildCount() < 2 || group.GetChild(1) is not GridContainer grid)
                return;
            foreach (var child in grid.GetChildren())
                if (child is Button button)
                    button.Toggled += _ => save();
        }

        private static void HookTextCommit(LineEdit edit, Action save)
        {
            edit.TextSubmitted += _ =>
            {
                save();
                edit.ReleaseFocus();
            };
            edit.FocusExited += save;
        }


        private sealed class ToggleKeyBinding(IModSettingsValueBinding<string> inner)
            : IDefaultModSettingsValueBinding<string>, IStructuredModSettingsValueBinding<string>
        {
            public string ModId => inner.ModId;
            public string DataKey => inner.DataKey;
            public SaveScope Scope => inner.Scope;

            public string Read()
            {
                return inner.Read();
            }

            public void Write(string value)
            {
                inner.Write(value);
            }

            public void Save()
            {
                inner.Save();
                Main.ApplyRuntimeHotkeysFromSettings();
            }

            public string CreateDefaultValue()
            {
                return inner is IDefaultModSettingsValueBinding<string> defaults
                    ? defaults.CreateDefaultValue()
                    : InputHandler.DefaultToggleBinding;
            }

            public IStructuredModSettingsValueAdapter<string> Adapter =>
                inner is IStructuredModSettingsValueBinding<string> structured
                    ? structured.Adapter
                    : ModSettingsStructuredData.Json<string>();
        }

        private sealed class RefreshingBinding<T>(IModSettingsValueBinding<T> inner)
            : IDefaultModSettingsValueBinding<T>, IStructuredModSettingsValueBinding<T>
        {
            public string ModId => inner.ModId;
            public string DataKey => inner.DataKey;
            public SaveScope Scope => inner.Scope;

            public T Read()
            {
                return inner.Read();
            }

            public void Write(T value)
            {
                inner.Write(value);
                LayoutSettingsSnapshot.Invalidate();
                HighlightEvaluator.InvalidateRules();
                HandCardDisplayService.RefreshAll();
            }

            public void Save()
            {
                inner.Save();
            }

            public T CreateDefaultValue()
            {
                return inner is IDefaultModSettingsValueBinding<T> defaults ? defaults.CreateDefaultValue() : default!;
            }

            public IStructuredModSettingsValueAdapter<T> Adapter =>
                inner is IStructuredModSettingsValueBinding<T> structured
                    ? structured.Adapter
                    : ModSettingsStructuredData.Json<T>();
        }
    }
}
