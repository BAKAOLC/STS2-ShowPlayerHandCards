using System.Linq.Expressions;
using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2ShowPlayerHandCards.Utils
{
    internal static class LemonSpireInterop
    {
        private const string HelperTypeName = "lemonSpire2.PlayerStateEx.PanelProvider.PlayerPanelChatHelper";
        private const string RemoteUiFlashKindTypeName = "lemonSpire2.PlayerStateEx.RemoteFlash.RemoteUiFlashKind";
        private const string HandCardShareLocEntryKey = "LEMONSPIRE.chat.handCardShare";
        private static readonly Lazy<CardOperation?> SendHandCardToChat = new(CreateSendHandCardToChat);
        private static readonly Lazy<CardOperation?> RequestHandCardFlash = new(CreateRequestHandCardFlash);

        public static bool TrySendHandCardToChat(Player player, CardModel card)
        {
            return TryInvoke(SendHandCardToChat.Value, player, card);
        }

        public static bool TryRequestHandCardFlash(Player player, CardModel card)
        {
            return TryInvoke(RequestHandCardFlash.Value, player, card);
        }

        private static bool TryInvoke(CardOperation? operation, Player player, CardModel card)
        {
            if (operation == null) return false;

            try
            {
                return operation(player, card);
            }
            catch
            {
                return false;
            }
        }

        private static CardOperation? CreateSendHandCardToChat()
        {
            var helperType = ResolveHelperType();
            var method = helperType?.GetMethod(
                "SendCardToChat",
                BindingFlags.Public | BindingFlags.Static,
                null,
                [typeof(Player), typeof(string), typeof(CardModel)],
                null);
            if (method == null) return null;

            var send = method.CreateDelegate<Action<Player, string, CardModel>>();
            return (player, card) =>
            {
                send(player, HandCardShareLocEntryKey, card);
                return true;
            };
        }

        private static CardOperation? CreateRequestHandCardFlash()
        {
            var helperType = ResolveHelperType();
            var kindType = ResolveLemonSpireType(RemoteUiFlashKindTypeName);
            if (helperType == null || kindType == null) return null;

            var method = helperType.GetMethod(
                "RequestRemoteFlash",
                BindingFlags.Public | BindingFlags.Static,
                null,
                [typeof(Player), kindType, typeof(CardModel)],
                null);
            if (method == null) return null;

            var kind = Enum.Parse(kindType, "HandCard");
            var player = Expression.Parameter(typeof(Player), "player");
            var card = Expression.Parameter(typeof(CardModel), "card");
            var call = Expression.Call(method, player, Expression.Constant(kind, kindType), card);
            var request = Expression.Lambda<Action<Player, CardModel>>(call, player, card).Compile();

            return (targetPlayer, targetCard) =>
            {
                request(targetPlayer, targetCard);
                return true;
            };
        }

        private static Type? ResolveHelperType()
        {
            return ResolveLemonSpireType(HelperTypeName);
        }

        private static Type? ResolveLemonSpireType(string typeName)
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(typeName, false))
                .FirstOrDefault(type => type != null);
        }

        private delegate bool CardOperation(Player player, CardModel card);
    }
}
