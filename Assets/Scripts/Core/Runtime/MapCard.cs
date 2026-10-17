using System;

namespace Richman.Core
{
    public enum MapCardEffect
    {
        ReceiveMoney,
        PayBank,
        Hospitalize,
        Imprison
    }

    public sealed class MapCard
    {
        public MapCard(string id, string deckId, string title, string description, MapCardEffect effect, int amount = 0)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A card id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(deckId)) throw new ArgumentException("A deck id is required.", nameof(deckId));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A card title is required.", nameof(title));
            if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("A card description is required.", nameof(description));
            if ((effect == MapCardEffect.ReceiveMoney || effect == MapCardEffect.PayBank || effect == MapCardEffect.Hospitalize || effect == MapCardEffect.Imprison) && amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Id = id;
            DeckId = deckId;
            Title = title;
            Description = description;
            Effect = effect;
            Amount = amount;
        }

        public string Id { get; }
        public string DeckId { get; }
        public string Title { get; }
        public string Description { get; }
        public MapCardEffect Effect { get; }
        public int Amount { get; }
    }
}
