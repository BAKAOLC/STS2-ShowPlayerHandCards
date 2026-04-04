using Godot;
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
    /// <summary>
    ///     Displays teammate hand cards as scaled-down full NCard images
    ///     next to the multiplayer player list.
    ///     Uses the spacer-in-layout + sync-position pattern so the actual
    ///     card container is a direct child of the player state node and
    ///     won't be clipped by the TopInfoContainer's box.
    /// </summary>
    public static partial class HandCardDisplayService
    {
        private const float CardSpacing = 1f;
        private const float CardYOffset = 4f;
        private static readonly Dictionary<NMultiplayerPlayerState, CardDisplayContainer> Containers = [];
        private static readonly List<CardPile> SubscribedHands = [];
        private static readonly Action<CardModel> HandChangedHandler = static _ => RefreshAll();
        private static bool _subscribed;
        private static bool _hidden;

        /// <summary>
        ///     Gets or sets whether the hand card display is currently hidden.
        /// </summary>
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

        /// <summary>
        ///     Toggles the visibility of the hand card display.
        /// </summary>
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

        /// <summary>
        ///     Called from the Postfix of SetUpCombat. Since CombatSetUp event
        ///     has already fired by the time the Postfix runs, we must subscribe
        ///     to hand events here directly for the current combat.
        /// </summary>
        public static void SubscribeCurrentCombat()
        {
            UnsubscribeCurrentCombat();

            var run = NRun.Instance;
            if (run?.GlobalUi?.MultiplayerPlayerContainer == null) return;
            var container = run.GlobalUi.MultiplayerPlayerContainer;

            for (var i = 0; i < container.GetChildCount(); i++)
            {
                if (container.GetChild(i) is not NMultiplayerPlayerState ps) continue;
                var player = ps.Player;
                if (player == null || LocalContext.IsMe(player)) continue;
                var pcs = player.PlayerCombatState;
                if (pcs == null) continue;
                var hand = pcs.Hand;
                hand.CardAdded += HandChangedHandler;
                hand.CardRemoved += HandChangedHandler;
                SubscribedHands.Add(hand);
            }
        }

        private static void UnsubscribeCurrentCombat()
        {
            foreach (var hand in SubscribedHands)
            {
                hand.CardAdded -= HandChangedHandler;
                hand.CardRemoved -= HandChangedHandler;
            }

            SubscribedHands.Clear();
        }

        public static void RefreshAll()
        {
            try
            {
                if (!CombatManager.Instance.IsInProgress)
                {
                    HideAll();
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

        /// <summary>
        ///     Actual HBoxContainer holding mini cards.
        ///     Lives as a direct child of NMultiplayerPlayerState (not inside TopInfoContainer)
        ///     so it won't be clipped. A spacer Control inside TopInfoContainer reserves
        ///     the layout space, and _Process syncs position every frame.
        /// </summary>
        private partial class CardDisplayContainer : HBoxContainer
        {
            private readonly List<MiniCard> _cards = [];
            private readonly NMultiplayerPlayerState? _playerState;

            private bool _isHidden;
            private float _lastWidth;
            private Control? _spacer;

            public CardDisplayContainer(NMultiplayerPlayerState playerState)
            {
                _playerState = playerState;
                Name = "HandCardDisplayContainer";
                MouseFilter = MouseFilterEnum.Ignore;
                AddThemeConstantOverride("separation", (int)CardSpacing);
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
                Resized += OnResized;

                SetHidden(_hidden);
            }

            public override void _Process(double delta)
            {
                if (IsReleased) return;
                if (_spacer != null && _spacer.IsInsideTree())
                    GlobalPosition = _spacer.GlobalPosition + new Vector2(0, CardYOffset);
            }

            private void OnResized()
            {
                _lastWidth = Size.X;
                if (_spacer != null && !_isHidden)
                    _spacer.CustomMinimumSize = new(Size.X, 0);
            }

            public void SetHidden(bool hidden)
            {
                _isHidden = hidden;
                Visible = !hidden;
                _spacer?.CustomMinimumSize = hidden ? Vector2.Zero : new(_lastWidth, 0);
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

                Visible = cards.Count > 0;
            }

            public void Cleanup()
            {
                if (IsReleased) return;
                IsReleased = true;
                Resized -= OnResized;
                foreach (var mc in _cards) mc.Dispose();
                _cards.Clear();
                _spacer?.QueueFree();
                _spacer = null;
                QueueFree();
            }
        }

        /// <summary>
        ///     A full NCard scaled down inside a fixed-size wrapper.
        ///     The Wrapper handles all mouse interaction; the NCard and its
        ///     children are set to MouseFilter.Ignore so there is no
        ///     mismatch between visual and interactive areas.
        /// </summary>
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
                if (_card == card) return;
                _card = card;

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
                    if (HandCardDisplaySettings.ShouldHighlight(card))
                    {
                        _highlightOverlay = CreateHighlightOverlay();
                        Wrapper.AddChild(_highlightOverlay);
                    }

                    Callable.From(() =>
                    {
                        if (_nCard == null || !GodotObject.IsInstanceValid(_nCard)) return;
                        _nCard.UpdateVisuals(PileType.Hand, CardPreviewMode.Normal);
                        PropagateMouseIgnore(_nCard);
                    }).CallDeferred();
                }
                catch (Exception ex)
                {
                    Main.Logger.Error($"Failed to create mini card: {ex.Message}");
                }
            }

            private static Control CreateHighlightOverlay()
            {
                var overlay = new Panel
                {
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                overlay.AddThemeStyleboxOverride("panel", new StyleBoxFlat
                {
                    DrawCenter = false,
                    BorderColor = HandCardDisplaySettings.GetHighlightColor(),
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
                    var tipSet = NHoverTipSet.CreateAndShow(
                        Wrapper, new CardHoverTip(_card), HoverTipAlignment.Right);
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
                    /* ignored */
                }
            }
        }
    }
}
