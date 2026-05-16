using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards;
using STS2ShowPlayerHandCards.Data;
using STS2ShowPlayerHandCards.Data.Models;

namespace STS2ShowPlayerHandCards.Utils
{
    /// <summary>
    ///     Read-only view of layout-relevant settings. Rebuilt on demand when
    ///     the underlying settings change; callers can compare Version to
    ///     detect changes cheaply.
    /// </summary>
    internal sealed class LayoutSettingsSnapshot
    {
        private const float BaseMiniCardScale = 0.065f;
        private const float BaseCardYOffset = 4f;
        private const float BaseCardSpacing = 1f;

        private static LayoutSettingsSnapshot? _current;
        private static int _version;

        private LayoutSettingsSnapshot(
            int version,
            float miniCardScale,
            Vector2 scaledCardSize,
            float cardSpacing,
            Vector2 legacyAutoOffset,
            Vector2 userOffset,
            bool manualPositioningEnabled,
            bool reserveOriginalWidth,
            Dictionary<int, Vector2> slotOffsets)
        {
            Version = version;
            MiniCardScale = miniCardScale;
            ScaledCardSize = scaledCardSize;
            CardSpacing = cardSpacing;
            LegacyAutoOffset = legacyAutoOffset;
            UserOffset = userOffset;
            ManualPositioningEnabled = manualPositioningEnabled;
            ReserveOriginalWidth = reserveOriginalWidth;
            SlotOffsets = slotOffsets;
        }

        public int Version { get; }
        public float MiniCardScale { get; }
        public Vector2 ScaledCardSize { get; }
        public float CardSpacing { get; }
        public Vector2 LegacyAutoOffset { get; }
        public Vector2 UserOffset { get; }
        public bool ManualPositioningEnabled { get; }
        public bool ReserveOriginalWidth { get; }
        public Dictionary<int, Vector2> SlotOffsets { get; }

        public static LayoutSettingsSnapshot Current => _current ??= Build();

        public static void Invalidate()
        {
            _version++;
            _current = null;
        }

        public Vector2 GetSlotOffset(int slotIndex)
        {
            return SlotOffsets.TryGetValue(slotIndex, out var offset) ? offset : Vector2.Zero;
        }

        public float GetContentWidth(int count)
        {
            if (count <= 0) return 0f;
            return count * ScaledCardSize.X + (count - 1) * CardSpacing;
        }

        private static LayoutSettingsSnapshot Build()
        {
            var settings = ModDataStore.Get<ModSettings>(ModDataStore.SettingsKey);
            var clampedScale = (float)Math.Clamp(settings.ContentScale,
                ModSettings.MinContentScale, ModSettings.MaxContentScale);

            var miniCardScale = BaseMiniCardScale * clampedScale;
            var scaledCardSize = NCard.defaultSize * miniCardScale;
            var cardSpacing = Mathf.Max(BaseCardSpacing,
                Mathf.Round(BaseCardSpacing * Mathf.Sqrt(clampedScale)));
            var baseHeight = NCard.defaultSize.Y * BaseMiniCardScale;
            var extraHeight = Mathf.Max(0f, scaledCardSize.Y - baseHeight);
            var legacyAutoOffset = new Vector2(0f, BaseCardYOffset - extraHeight * 0.18f);
            var userOffset = new Vector2((float)settings.PositionOffsetX, (float)settings.PositionOffsetY);

            var slotOffsets = new Dictionary<int, Vector2>(settings.SlotOffsets.Count);
            foreach (var entry in settings.SlotOffsets)
                slotOffsets[entry.SlotIndex] = new((float)entry.OffsetX, (float)entry.OffsetY);

            return new(
                _version,
                miniCardScale,
                scaledCardSize,
                cardSpacing,
                legacyAutoOffset,
                userOffset,
                settings.ManualPositioningEnabled,
                settings.ReserveOriginalWidth,
                slotOffsets);
        }
    }
}
