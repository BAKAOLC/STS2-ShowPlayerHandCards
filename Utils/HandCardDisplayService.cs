using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;

namespace STS2ShowPlayerHandCards.Utils
{
    public static partial class HandCardDisplayService
    {
        private static readonly Dictionary<NMultiplayerPlayerState, CardDisplayContainer> Containers = [];
        private static readonly List<CardPile> SubscribedHands = [];
        private static readonly Action HandContentsChangedHandler = static () => RefreshAll();
        private static bool _subscribed;
        private static bool _hidden;

        public static bool IsHidden
        {
            get => _hidden;
            set
            {
                if (_hidden == value) return;
                _hidden = value;
                UpdateVisibility();
            }
        }

        public static void ToggleVisibility()
        {
            IsHidden = !IsHidden;
        }

        private static void UpdateVisibility()
        {
            foreach (var container in Containers.Values) container.SetHidden(_hidden);
        }

        public static void EnsureSubscribed()
        {
            if (_subscribed) return;
            _subscribed = true;
            CombatManager.Instance.CombatSetUp += OnCombatSetUp;
            CombatManager.Instance.CombatEnded += _ => HideAll();
            CombatManager.Instance.TurnStarted += _ => RefreshAll();
        }

        public static void SubscribeCurrentCombat()
        {
            var run = NRun.Instance;
            if (run?.GlobalUi?.MultiplayerPlayerContainer == null) return;

            UnsubscribeCurrentCombat();

            var container = run.GlobalUi.MultiplayerPlayerContainer;
            for (var i = 0; i < container.GetChildCount(); i++)
            {
                if (container.GetChild(i) is not NMultiplayerPlayerState ps) continue;
                var player = ps.Player;
                if (player == null || LocalContext.IsMe(player)) continue;
                var pcs = player.PlayerCombatState;
                if (pcs == null) continue;
                var hand = pcs.Hand;
                hand.ContentsChanged += HandContentsChangedHandler;
                SubscribedHands.Add(hand);
            }
        }

        private static void UnsubscribeCurrentCombat()
        {
            foreach (var hand in SubscribedHands)
                hand.ContentsChanged -= HandContentsChangedHandler;
            SubscribedHands.Clear();
        }

        private static void ApplyMiniTeammateCardDescription(NCard nCard, CardModel model)
        {
            if (!nCard.IsNodeReady())
                return;
            var label = nCard.GetNodeOrNull<MegaRichTextLabel>("%DescriptionLabel");
            if (label == null)
                return;
            var text = model.GetDescriptionForPile(PileType.Hand, model.CurrentTarget);
            label.SetTextAutoSize("[center]" + text + "[/center]");
        }

        public static void RefreshAll()
        {
            try
            {
                if (!CombatManager.Instance.IsInProgress)
                {
                    ClearDisplayContainersOnly();
                    return;
                }

                var run = NRun.Instance;
                if (run?.GlobalUi?.MultiplayerPlayerContainer == null) return;
                var container = run.GlobalUi.MultiplayerPlayerContainer;

                for (var i = 0; i < container.GetChildCount(); i++)
                {
                    if (container.GetChild(i) is not NMultiplayerPlayerState ps) continue;
                    RefreshPlayer(ps);
                }
            }
            catch (Exception ex)
            {
                Main.Logger.Error($"Failed to refresh hand card display: {ex.Message}");
            }
        }

        public static void HideAll()
        {
            UnsubscribeCurrentCombat();
            ClearDisplayContainersOnly();
        }

        private static void ClearDisplayContainersOnly()
        {
            foreach (var c in Containers.Values) c.Cleanup();
            Containers.Clear();
        }

        private static void OnCombatSetUp(CombatState combatState)
        {
            SubscribeCurrentCombat();
            RefreshAll();
        }

        private static void RefreshPlayer(NMultiplayerPlayerState ps)
        {
            var player = ps.Player;
            if (player?.PlayerCombatState == null || player.Creature.IsDead || LocalContext.IsMe(player))
            {
                if (!Containers.TryGetValue(ps, out var old)) return;
                old.Cleanup();
                Containers.Remove(ps);
                return;
            }

            if (!Containers.TryGetValue(ps, out var display) || !GodotObject.IsInstanceValid(display) ||
                display.IsReleased)
            {
                if (display != null && GodotObject.IsInstanceValid(display) && !display.IsReleased)
                    display.Cleanup();
                display = new(ps);
                Containers[ps] = display;
                display.Initialize();
            }

            display.RefreshCards(player.PlayerCombatState.Hand.Cards);
        }

        private partial class CardDisplayContainer : HBoxContainer
        {
            private readonly List<MiniCard> _cards = [];
            private readonly NMultiplayerPlayerState? _playerState;
            private bool _isHidden;
            private Control? _spacer;

            public CardDisplayContainer(NMultiplayerPlayerState playerState)
            {
                _playerState = playerState;
                Name = "HandCardDisplayContainer";
                MouseFilter = MouseFilterEnum.Ignore;
                AddThemeConstantOverride("separation", (int)HandCardDisplaySettings.GetCardSpacing());
            }

            public CardDisplayContainer()
            {
            }

            public bool IsReleased { get; private set; }

            public void Initialize()
            {
                _spacer = new()
                {
                    Name = "HandCardSpacer",
                    CustomMinimumSize = Vector2.Zero,
                    MouseFilter = MouseFilterEnum.Ignore,
                };

                if (_playerState == null) return;

                var topContainer = _playerState.GetNode<HBoxContainer>("TopInfoContainer");
                topContainer.AddChild(_spacer);
                _playerState.AddChild(this);
                SetHidden(_hidden);
                RefreshLayout();
            }

            public override void _Process(double delta)
            {
                if (IsReleased || _spacer == null || !_spacer.IsInsideTree()) return;
                GlobalPosition = _spacer.GlobalPosition + HandCardDisplaySettings.GetAutoOffset() +
                                 HandCardDisplaySettings.GetUserOffset();
            }

            private void RefreshLayout()
            {
                AddThemeConstantOverride("separation", (int)HandCardDisplaySettings.GetCardSpacing());
                var scaledSize = HandCardDisplaySettings.GetScaledCardSize();
                var width = HandCardDisplaySettings.GetContentWidth(_cards.Count);
                CustomMinimumSize = new(width, scaledSize.Y);
                _spacer?.CustomMinimumSize = _isHidden ? Vector2.Zero : new(width, 0f);
                Visible = !_isHidden && _cards.Count > 0;
            }

            public void SetHidden(bool hidden)
            {
                _isHidden = hidden;
                RefreshLayout();
            }

            public void RefreshCards(IReadOnlyList<CardModel> cards)
            {
                if (IsReleased) return;

                while (_cards.Count > cards.Count)
                {
                    _cards[^1].Dispose();
                    _cards.RemoveAt(_cards.Count - 1);
                }

                for (var i = 0; i < cards.Count; i++)
                    if (i < _cards.Count)
                    {
                        _cards[i].SetCard(cards[i]);
                    }
                    else
                    {
                        var mc = new MiniCard(cards[i]);
                        AddChild(mc.Wrapper);
                        _cards.Add(mc);
                    }

                RefreshLayout();
            }

            public void Cleanup()
            {
                if (IsReleased) return;
                IsReleased = true;
                foreach (var mc in _cards) mc.Dispose();
                _cards.Clear();
                _spacer?.QueueFree();
                _spacer = null;
                QueueFree();
            }
        }

        private sealed class MiniCard : IDisposable
        {
            private CardModel? _card;
            private Control? _highlightOverlay;
            private NCard? _nCard;

            public MiniCard(CardModel card)
            {
                Wrapper = new()
                {
                    CustomMinimumSize = HandCardDisplaySettings.GetScaledCardSize(),
                    Size = HandCardDisplaySettings.GetScaledCardSize(),
                    MouseFilter = Control.MouseFilterEnum.Stop,
                    ClipContents = false,
                };
                Wrapper.MouseEntered += OnMouseEntered;
                Wrapper.MouseExited += OnMouseExited;
                SetCard(card);
            }

            public Control Wrapper { get; }

            public void Dispose()
            {
                OnMouseExited();
                if (_nCard != null && GodotObject.IsInstanceValid(_nCard))
                {
                    _nCard.QueueFree();
                    _nCard = null;
                }

                if (GodotObject.IsInstanceValid(Wrapper))
                    Wrapper.QueueFree();
            }

            public void SetCard(CardModel card)
            {
                _card = card;
                Wrapper.CustomMinimumSize = HandCardDisplaySettings.GetScaledCardSize();
                Wrapper.Size = HandCardDisplaySettings.GetScaledCardSize();

                if (_nCard != null && GodotObject.IsInstanceValid(_nCard))
                {
                    _nCard.QueueFree();
                    _nCard = null;
                }

                if (_highlightOverlay != null && GodotObject.IsInstanceValid(_highlightOverlay))
                {
                    _highlightOverlay.QueueFree();
                    _highlightOverlay = null;
                }

                try
                {
                    _nCard = NCard.Create(card);
                    if (_nCard == null) return;

                    var scaledSize = HandCardDisplaySettings.GetScaledCardSize();
                    _nCard.PivotOffset = Vector2.Zero;
                    _nCard.Scale = Vector2.One * HandCardDisplaySettings.GetMiniCardScale();
                    _nCard.Position = scaledSize / 2f;
                    _nCard.MouseFilter = Control.MouseFilterEnum.Ignore;
                    Wrapper.AddChild(_nCard);

                    if (HandCardDisplaySettings.TryGetHighlightColor(card, out var color))
                    {
                        _highlightOverlay = CreateHighlightOverlay(color);
                        Wrapper.AddChild(_highlightOverlay);
                    }

                    Callable.From(() =>
                    {
                        if (_nCard == null || !GodotObject.IsInstanceValid(_nCard) || _card == null)
                            return;
                        ApplyMiniTeammateCardDescription(_nCard, _card);
                        PropagateMouseIgnore(_nCard);
                    }).CallDeferred();
                }
                catch (Exception ex)
                {
                    Main.Logger.Error($"Failed to create mini card: {ex.Message}");
                }
            }

            private static Control CreateHighlightOverlay(Color color)
            {
                var overlay = new Panel
                {
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                overlay.AddThemeStyleboxOverride("panel", new StyleBoxFlat
                {
                    DrawCenter = false,
                    BorderColor = color,
                    BorderWidthLeft = 3,
                    BorderWidthTop = 3,
                    BorderWidthRight = 3,
                    BorderWidthBottom = 3,
                    CornerRadiusTopLeft = 8,
                    CornerRadiusTopRight = 8,
                    CornerRadiusBottomLeft = 8,
                    CornerRadiusBottomRight = 8,
                });
                return overlay;
            }

            private static void PropagateMouseIgnore(Control node)
            {
                node.MouseFilter = Control.MouseFilterEnum.Ignore;
                foreach (var child in node.GetChildren())
                    if (child is Control c)
                        PropagateMouseIgnore(c);
            }

            private void OnMouseEntered()
            {
                if (_card == null || !GodotObject.IsInstanceValid(Wrapper)) return;
                try
                {
                    var tipSet = NHoverTipSet.CreateAndShow(Wrapper, new CardHoverTip(_card), HoverTipAlignment.Right);
                    tipSet.SetFollowOwner();
                }
                catch (Exception ex)
                {
                    Main.Logger.Error($"Failed to show card hover tip: {ex.Message}");
                }
            }

            private void OnMouseExited()
            {
                try
                {
                    if (GodotObject.IsInstanceValid(Wrapper))
                        NHoverTipSet.Remove(Wrapper);
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}
