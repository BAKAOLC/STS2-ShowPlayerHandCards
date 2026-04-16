using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
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
        private static readonly Dictionary<CardPile, HandSubscription> SubscribedHands = [];
        private static readonly HashSet<NMultiplayerPlayerState> PendingRefresh = [];
        private static readonly Action FlushPendingRefreshHandler = static () => FlushPendingRefresh();
        private static bool _subscribed;
        private static bool _hidden;
        private static bool _flushScheduled;

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
                SubscribeHand(pcs.Hand, ps);
            }
        }

        private static void SubscribeHand(CardPile hand, NMultiplayerPlayerState playerState)
        {
            if (SubscribedHands.ContainsKey(hand)) return;
            Action handler = () => MarkPlayerDirty(playerState);
            hand.ContentsChanged += handler;
            SubscribedHands[hand] = new(playerState, handler);
        }

        private static void UnsubscribeCurrentCombat()
        {
            foreach (var (hand, subscription) in SubscribedHands)
                hand.ContentsChanged -= subscription.Handler;
            SubscribedHands.Clear();
            PendingRefresh.Clear();
            _flushScheduled = false;
        }

        private static void MarkPlayerDirty(NMultiplayerPlayerState playerState)
        {
            PendingRefresh.Add(playerState);
            ScheduleFlush();
        }

        private static void ScheduleFlush()
        {
            if (_flushScheduled) return;
            _flushScheduled = true;
            Callable.From(FlushPendingRefreshHandler).CallDeferred();
        }

        private static void FlushPendingRefresh()
        {
            _flushScheduled = false;

            try
            {
                if (!CombatManager.Instance.IsInProgress)
                {
                    PendingRefresh.Clear();
                    ClearDisplayContainersOnly();
                    return;
                }

                foreach (var ps in PendingRefresh)
                {
                    if (!GodotObject.IsInstanceValid(ps)) continue;
                    RefreshPlayer(ps);
                }
            }
            catch (Exception ex)
            {
                Main.Logger.Error($"Failed to refresh hand card display: {ex.Message}");
            }
            finally
            {
                PendingRefresh.Clear();
            }
        }

        private readonly record struct HandSubscription(NMultiplayerPlayerState PlayerState, Action Handler);

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
            if (!CombatManager.Instance.IsInProgress)
            {
                PendingRefresh.Clear();
                _flushScheduled = false;
                ClearDisplayContainersOnly();
                return;
            }

            var run = NRun.Instance;
            if (run?.GlobalUi?.MultiplayerPlayerContainer == null) return;

            var container = run.GlobalUi.MultiplayerPlayerContainer;
            for (var i = 0; i < container.GetChildCount(); i++)
            {
                if (container.GetChild(i) is not NMultiplayerPlayerState ps) continue;
                PendingRefresh.Add(ps);
            }

            ScheduleFlush();
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
            private Vector2 _dragStartMouse;
            private Vector2 _dragStartOffset;
            private bool _isDragging;
            private bool _isHidden;
            private Control? _spacer;
            private Vector2 _lastAnchorPosition = new(float.NaN, float.NaN);
            private Vector2 _lastAnchorSize;
            private int _lastCardCount = -1;
            private int _lastSnapshotVersion = -1;

            public CardDisplayContainer(NMultiplayerPlayerState playerState)
            {
                _playerState = playerState;
                Name = "HandCardDisplayContainer";
                MouseFilter = MouseFilterEnum.Pass;
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
                if (IsReleased || _playerState == null || _spacer == null || !_spacer.IsInsideTree()) return;

                var anchorPosition = _spacer.GlobalPosition;
                var anchorSize = _spacer.Size;
                var snapshotVersion = LayoutSettingsSnapshot.Current.Version;

                if (!_isDragging
                    && anchorPosition == _lastAnchorPosition
                    && anchorSize == _lastAnchorSize
                    && _cards.Count == _lastCardCount
                    && snapshotVersion == _lastSnapshotVersion)
                    return;

                _lastAnchorPosition = anchorPosition;
                _lastAnchorSize = anchorSize;
                _lastCardCount = _cards.Count;
                _lastSnapshotVersion = snapshotVersion;
                GlobalPosition = ResolveDisplayPosition();
            }

            public override void _GuiInput(InputEvent @event)
            {
                HandleDragInput(@event);
                base._GuiInput(@event);
            }

            private void HandleDragInput(InputEvent @event)
            {
                if (!HandCardDisplaySettings.IsManualPositioningEnabled() || _playerState == null)
                    return;

                switch (@event)
                {
                    case InputEventMouseButton { ButtonIndex: MouseButton.Left } mouseButton:
                        if (mouseButton.Pressed)
                        {
                            _isDragging = true;
                            _dragStartMouse = mouseButton.GlobalPosition;
                            _dragStartOffset = HandCardDisplaySettings.GetSlotOffset(GetSlotIndex());
                            GetViewport().SetInputAsHandled();
                        }
                        else if (_isDragging)
                        {
                            _isDragging = false;
                            GetViewport().SetInputAsHandled();
                        }

                        break;
                    case InputEventMouseMotion mouseMotion when _isDragging:
                        var deltaPosition = mouseMotion.GlobalPosition - _dragStartMouse;
                        HandCardDisplaySettings.SetSlotOffset(GetSlotIndex(), _dragStartOffset + deltaPosition);
                        GetViewport().SetInputAsHandled();
                        break;
                }
            }

            private void RefreshLayout()
            {
                var snapshot = LayoutSettingsSnapshot.Current;
                AddThemeConstantOverride("separation", (int)snapshot.CardSpacing);
                var width = snapshot.GetContentWidth(_cards.Count);
                CustomMinimumSize = new(width, snapshot.ScaledCardSize.Y);
                _spacer?.CustomMinimumSize = !_isHidden && snapshot.ReserveOriginalWidth
                    ? new(width, 0f)
                    : Vector2.Zero;
                Visible = !_isHidden && _cards.Count > 0;
            }

            private Vector2 ResolveDisplayPosition()
            {
                if (_playerState == null || _spacer == null)
                    return GlobalPosition;

                var snapshot = LayoutSettingsSnapshot.Current;
                var contentSize = new Vector2(snapshot.GetContentWidth(_cards.Count),
                    snapshot.ScaledCardSize.Y);
                var anchorRect = new Rect2(_spacer.GlobalPosition, _spacer.Size);
                if (anchorRect.Size == Vector2.Zero)
                {
                    var topContainer = _playerState.GetNode<Control>("TopInfoContainer");
                    anchorRect = new(topContainer.GlobalPosition, topContainer.Size);
                }

                var viewportRect = GetViewport().GetVisibleRect();
                var basePosition =
                    HandCardDisplaySettings.ResolveAutoPosition(anchorRect, contentSize, anchorRect, viewportRect);
                return basePosition + snapshot.UserOffset + snapshot.GetSlotOffset(GetSlotIndex());
            }

            private int GetSlotIndex()
            {
                return _playerState?.GetIndex() ?? -1;
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
                        var mc = new MiniCard(cards[i], HandleDragInput);
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
            private readonly Action<InputEvent>? _dragInputHandler;
            private CardModel? _card;
            private Control? _highlightOverlay;
            private NCard? _nCard;

            public MiniCard(CardModel card, Action<InputEvent>? dragInputHandler)
            {
                _dragInputHandler = dragInputHandler;
                Wrapper = new()
                {
                    CustomMinimumSize = HandCardDisplaySettings.GetScaledCardSize(),
                    Size = HandCardDisplaySettings.GetScaledCardSize(),
                    MouseFilter = Control.MouseFilterEnum.Stop,
                    ClipContents = false,
                };
                Wrapper.MouseEntered += OnMouseEntered;
                Wrapper.MouseExited += OnMouseExited;
                Wrapper.GuiInput += OnWrapperGuiInput;
                SetCard(card);
            }

            public Control Wrapper { get; }

            public void Dispose()
            {
                OnMouseExited();
                if (GodotObject.IsInstanceValid(Wrapper))
                    Wrapper.GuiInput -= OnWrapperGuiInput;

                ReleaseNCardToPool();
                ReleaseHighlightOverlay();

                if (GodotObject.IsInstanceValid(Wrapper))
                    Wrapper.QueueFree();
            }

            public void SetCard(CardModel card)
            {
                _card = card;
                var scaledSize = HandCardDisplaySettings.GetScaledCardSize();
                Wrapper.CustomMinimumSize = scaledSize;
                Wrapper.Size = scaledSize;

                try
                {
                    if (!TryReuseNCard(card, scaledSize))
                        RebuildNCard(card, scaledSize);

                    UpdateHighlightOverlay(card);

                    Callable.From(ApplyDeferredCardVisuals).CallDeferred();
                }
                catch (Exception ex)
                {
                    Main.Logger.Error($"Failed to set mini card: {ex.Message}");
                }
            }

            private bool TryReuseNCard(CardModel card, Vector2 scaledSize)
            {
                if (_nCard == null || !GodotObject.IsInstanceValid(_nCard))
                    return false;

                _nCard.Scale = Vector2.One * HandCardDisplaySettings.GetMiniCardScale();
                _nCard.Position = scaledSize / 2f;
                _nCard.Model = card;
                return true;
            }

            private void RebuildNCard(CardModel card, Vector2 scaledSize)
            {
                ReleaseNCardToPool();

                _nCard = NCard.Create(card);
                if (_nCard == null) return;

                _nCard.PivotOffset = Vector2.Zero;
                _nCard.Scale = Vector2.One * HandCardDisplaySettings.GetMiniCardScale();
                _nCard.Position = scaledSize / 2f;
                _nCard.MouseFilter = Control.MouseFilterEnum.Ignore;
                Wrapper.AddChild(_nCard);
            }

            private void ApplyDeferredCardVisuals()
            {
                if (_nCard == null || !GodotObject.IsInstanceValid(_nCard) || _card == null)
                    return;
                _nCard.UpdateVisuals(PileType.Hand, CardPreviewMode.Normal);
                ApplyMiniTeammateCardDescription(_nCard, _card);
                PropagateMouseIgnore(_nCard);
            }

            private void UpdateHighlightOverlay(CardModel card)
            {
                if (!HandCardDisplaySettings.TryGetHighlightColor(card, out var color))
                {
                    ReleaseHighlightOverlay();
                    return;
                }

                if (_highlightOverlay != null && GodotObject.IsInstanceValid(_highlightOverlay))
                {
                    SetOverlayBorderColor(_highlightOverlay, color);
                    return;
                }

                _highlightOverlay = CreateHighlightOverlay(color);
                Wrapper.AddChild(_highlightOverlay);
            }

            private void ReleaseNCardToPool()
            {
                if (_nCard == null) return;
                if (GodotObject.IsInstanceValid(_nCard))
                    _nCard.QueueFreeSafely();
                _nCard = null;
            }

            private void ReleaseHighlightOverlay()
            {
                if (_highlightOverlay == null) return;
                if (GodotObject.IsInstanceValid(_highlightOverlay))
                    _highlightOverlay.QueueFree();
                _highlightOverlay = null;
            }

            private void OnWrapperGuiInput(InputEvent @event)
            {
                _dragInputHandler?.Invoke(@event);
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

            private static void SetOverlayBorderColor(Control overlay, Color color)
            {
                if (overlay.GetThemeStylebox("panel") is not StyleBoxFlat style)
                    return;
                if (style.BorderColor == color) return;
                style.BorderColor = color;
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
