namespace KtaneBaccarat
{
	enum Suit
	{
		Club,
		Diamond,
		Heart,
		Spade
	}

	class PlayingCard
	{
		private static string[] rankStrings = "A 2 3 4 5 6 7 8 9 10 J Q K".Split(' ');
		private static string suitCharacters = "♣♦♥♠";

		public readonly int Rank;
		public readonly Suit Suit;

		public int BaccaratValue
		{
			get
			{
				return (Rank >= 10) ? 0 : Rank;
			}
		}

		public int MaterialIndex
		{
			get
			{
				switch (Suit)
				{
					case Suit.Heart:
						return Rank;
					case Suit.Club:
						return Rank + 13;
					case Suit.Diamond:
						return 40 - Rank;
					case Suit.Spade:
						return 53 - Rank;
					default:
						return 0;
				}
			}
		}

		public PlayingCard(int rank, Suit suit)
		{
			Rank = rank;
			Suit = suit;
		}

		override public string ToString()
		{
			return rankStrings[Rank - 1] + suitCharacters[(int)Suit];
		}
	}
}