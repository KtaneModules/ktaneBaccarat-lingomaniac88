using System.Collections.Generic;
using System.Linq;

namespace KtaneBaccarat
{
	public enum DeckColor
	{
		Red,
		Blue,
		Green,
		Silver
	}

	class Deck
	{
		public static PlayingCard[] UnshuffledOrder = Enumerable.Range(1, 13).Select(r => new PlayingCard(r, Suit.Heart))
			.Concat(Enumerable.Range(1, 13).Select(r => new PlayingCard(r, Suit.Club)))
			.Concat(Enumerable.Range(1, 13).Reverse().Select(r => new PlayingCard(r, Suit.Diamond)))
			.Concat(Enumerable.Range(1, 13).Reverse().Select(r => new PlayingCard(r, Suit.Spade)))
			.ToArray();

		public readonly DeckColor Color;
		
		private Queue<PlayingCard> Cards;

		public int CardsLeft
		{
			get
			{
				return Cards.Count;
			}
		}

		// Creates an unshuffled deck.
		public Deck(DeckColor color)
		{
			Color = color;
			Cards = new Queue<PlayingCard>(UnshuffledOrder);
		}

		public Deck(DeckColor color, int firstIndex, int factor)
		{
			Color = color;
			
			var indices = new List<int>();
			indices.Add(firstIndex);
			while (indices.Count < 52)
			{
				indices.Add((indices.Last() * factor) % 53);
			}

			Cards = new Queue<PlayingCard>(indices.Select(i => UnshuffledOrder[i - 1]));
		}

		public PlayingCard DealCard()
		{
			return Cards.Dequeue();
		}
	}
}