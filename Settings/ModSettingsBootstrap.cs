using Godot;
using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2ShowPlayerHandCards.Data;
using STS2ShowPlayerHandCards.Data.Models;
using STS2ShowPlayerHandCards.Utils;

namespace STS2ShowPlayerHandCards.Settings
{
    internal static class ModSettingsBootstrap
    {
        private static readonly Lock InitLock = new();
        private static bool _initialized;

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
                var contentScaleBinding = ModSettingsBindings.WithDefault(
                    ModSettingsBindings.Global<ModSettings, float>(
                        Const.ModId,
                        ModDataStore.SettingsKey,
                        settings => settings.ContentScale,
                        (settings, value) => settings.ContentScale = value),
                    () => 1.0f);
                var colorBinding = ModSettingsBindings.WithDefault(
                    ModSettingsBindings.Global<ModSettings, string>(
                        Const.ModId,
                        ModDataStore.SettingsKey,
                        settings => settings.HighlightColorHex,
                        (settings, value) => settings.HighlightColorHex = value),
                    () => "#FFD740FF");
                var keywordListBinding = ModSettingsBindings.WithDefault(
                    ModSettingsBindings.Global<ModSettings, List<HighlightKeywordEntry>>(
                        Const.ModId,
                        ModDataStore.SettingsKey,
                        settings => settings.HighlightKeywords,
                        (settings, value) => settings.HighlightKeywords = value),
                    () => []);

                RitsuLibFramework.RegisterModSettings(Const.ModId, page => page
                    .WithModDisplayName(ModSettingsLocalization.T("mod.displayName", "Show Player Hand Cards"))
                    .WithTitle(ModSettingsLocalization.T("page.title", "Settings"))
                    .WithDescription(ModSettingsLocalization.T("page.description",
                        "Adjust teammate hand-card preview scale, toggle key, and keyword-based border highlights."))
                    .AddSection("display", section => section
                        .WithTitle(ModSettingsLocalization.T("section.display", "Display"))
                        .AddKeyBinding(
                            "toggle_key",
                            ModSettingsLocalization.T("toggleKey.label", "Toggle Key"),
                            new ToggleKeyBinding(toggleKeyBinding),
                            allowModifierCombos: true,
                            allowModifierOnly: false,
                            distinguishModifierSides: false,
                            description: ModSettingsLocalization.T("toggleKey.description",
                                "Records a keyboard shortcut, supports modifier combos, and requires a non-modifier key."))
                        .AddSlider(
                            "content_scale",
                            ModSettingsLocalization.T("contentScale.label", "Content Size"),
                            new ContentScaleBinding(contentScaleBinding),
                            0.5f,
                            5.0f,
                            0.05f,
                            value => $"{value:0.00}x",
                            ModSettingsLocalization.T("contentScale.description",
                                "Scales the mini card previews shown beside each teammate."))
                        .AddList(
                            "highlight_keywords",
                            ModSettingsLocalization.T("keywords.label", "Highlight Keywords"),
                            keywordListBinding,
                            () => new HighlightKeywordEntry(),
                            item => ModSettingsText.Literal(string.IsNullOrWhiteSpace(item.Keyword)
                                ? ModSettingsLocalization.Get("keywords.emptyItem", "(empty keyword)")
                                : item.Keyword),
                            item => ModSettingsText.Literal(
                                string.IsNullOrWhiteSpace(item.Keyword)
                                    ? ModSettingsLocalization.Get("keywords.emptyDescription",
                                        "Cards containing this keyword will receive a border.")
                                    : $"Matches card content containing '{item.Keyword}'."),
                            CreateKeywordEditor,
                            ModSettingsStructuredData.Json<HighlightKeywordEntry>(),
                            ModSettingsLocalization.T("keywords.add", "Add Keyword"),
                            ModSettingsLocalization.T("keywords.description",
                                "Keywords are matched case-insensitively against card keywords and hover tips."))
                        .AddColor(
                            "highlight_color",
                            ModSettingsLocalization.T("color.label", "Border Color"),
                            colorBinding,
                            ModSettingsLocalization.T("color.description",
                                "Includes a live preview, RGBA controls, and hex input such as #FFD740FF."))));

                _initialized = true;
            }
        }

        private static Control CreateKeywordEditor(ModSettingsListItemContext<HighlightKeywordEntry> itemContext)
        {
            var edit = new LineEdit
            {
                Text = itemContext.Item.Keyword,
                SelectAllOnFocus = true,
                PlaceholderText = ModSettingsLocalization.Get("keywords.placeholder", "e.g. Exhaust / Ethereal / Retain"),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(260f, 44f),
            };
            edit.AddThemeFontSizeOverride("font_size", 18);
            edit.AddThemeColorOverride("font_color", new Color(1f, 0.964706f, 0.886275f));
            edit.AddThemeStyleboxOverride("normal", CreateInputStyle(false));
            edit.AddThemeStyleboxOverride("focus", CreateInputStyle(true));
            edit.TextSubmitted += value =>
            {
                itemContext.Update(new HighlightKeywordEntry { Keyword = value.Trim() });
                edit.ReleaseFocus();
            };
            edit.FocusExited += () => itemContext.Update(new HighlightKeywordEntry { Keyword = edit.Text.Trim() });
            return edit;
        }

        private sealed class ToggleKeyBinding(IModSettingsValueBinding<string> inner)
            : IDefaultModSettingsValueBinding<string>, IStructuredModSettingsValueBinding<string>
        {
            public string ModId => inner.ModId;
            public string DataKey => inner.DataKey;
            public STS2RitsuLib.Utils.Persistence.SaveScope Scope => inner.Scope;
            public IStructuredModSettingsValueAdapter<string> Adapter =>
                inner is IStructuredModSettingsValueBinding<string> structured
                    ? structured.Adapter
                    : ModSettingsStructuredData.Json<string>();

            public string Read()
            {
                return inner.Read();
            }

            public void Write(string value)
            {
                inner.Write(value);
                InputHandler.CurrentBinding = value;
            }

            public void Save()
            {
                inner.Save();
            }

            public string CreateDefaultValue()
            {
                return inner is IDefaultModSettingsValueBinding<string> defaults
                    ? defaults.CreateDefaultValue()
                    : InputHandler.DefaultToggleBinding;
            }
        }

        private sealed class ContentScaleBinding(IModSettingsValueBinding<float> inner)
            : IDefaultModSettingsValueBinding<float>, IStructuredModSettingsValueBinding<float>
        {
            public string ModId => inner.ModId;
            public string DataKey => inner.DataKey;
            public STS2RitsuLib.Utils.Persistence.SaveScope Scope => inner.Scope;
            public IStructuredModSettingsValueAdapter<float> Adapter =>
                inner is IStructuredModSettingsValueBinding<float> structured
                    ? structured.Adapter
                    : ModSettingsStructuredData.Json<float>();

            public float Read()
            {
                return inner.Read();
            }

            public void Write(float value)
            {
                inner.Write(value);
                HandCardDisplayService.RefreshAll();
            }

            public void Save()
            {
                inner.Save();
            }

            public float CreateDefaultValue()
            {
                return inner is IDefaultModSettingsValueBinding<float> defaults
                    ? defaults.CreateDefaultValue()
                    : 1.0f;
            }
        }

        private static StyleBoxFlat CreateInputStyle(bool focused)
        {
            return new StyleBoxFlat
            {
                BgColor = focused ? new Color(0.10f, 0.14f, 0.19f, 0.98f) : new Color(0.08f, 0.11f, 0.15f, 0.96f),
                BorderColor = focused ? new Color(0.92f, 0.74f, 0.32f, 0.9f) : new Color(0.36f, 0.49f, 0.60f, 0.5f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                CornerRadiusTopLeft = 10,
                CornerRadiusTopRight = 10,
                CornerRadiusBottomLeft = 10,
                CornerRadiusBottomRight = 10,
                ContentMarginLeft = 14,
                ContentMarginTop = 10,
                ContentMarginRight = 14,
                ContentMarginBottom = 10,
            };
        }

    }
}
