using KModkit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

using KtaneBaccarat;

public class BaccaratScript : MonoBehaviour
{
	static int FullDeckSize = 52;

	static int[] PossibleMinimums = {5, 10, 25, 50, 100};

	static int[][][] PossibleMaximums =
	{
		new int[][]
		{
			new int[] {200},
			new int[] {300, 500},
			new int[] {1000},
			new int[] {1500, 2000, 2500},
			new int[] {3000, 5000}
		},
		new int[][]
		{
			new int[] {300, 500},
			new int[] {1000},
			new int[] {1500, 2000},
			new int[] {2500, 3000, 5000},
			new int[] {10000}
		},
		new int[][]
		{
			new int[] {1000},
			new int[] {1500, 2000, 2500},
			new int[] {3000, 5000},
			new int[] {10000},
			new int[] {15000, 20000, 25000}
		},
		new int[][]
		{
			new int[] {1000, 2000},
			new int[] {3000, 5000},
			new int[] {10000},
			new int[] {15000, 20000, 25000},
			new int[] {30000, 50000}
		},
		new int[][]
		{
			new int[] {3000, 5000},
			new int[] {10000},
			new int[] {15000, 20000},
			new int[] {25000, 30000, 50000},
			new int[] {100000, 150000}
		}
	};

	public static int[] Denominations = {1, 5, 25, 100, 500, 1000, 5000, 25000, 100000};

	// Returns a formatted string corresponding to dollars.  Only amounts above $9999 are given commas.
	static string AsDollars(int n)
	{
		if (n == int.MinValue)
		{
			return "-$2,147,483,648";	
		}
		else if (n < 0)
		{
			return "-" + AsDollars(-n);
		}
		else if (n < 10000)
		{
			return "$" + n.ToString();
		}
		else
		{
			return "$" + n.ToString("N0", CultureInfo.InvariantCulture);
		}
	}

	static string AsDollars(float n)
	{
		int cents = (int)Math.Round(n * 100);
		int wholeDollars = cents / 100;
		int centsOnly = cents % 100;

		if (centsOnly == 0)
		{
			return AsDollars(wholeDollars);
		}
		else
		{
			return string.Format("{0}.{1}", AsDollars(wholeDollars), centsOnly);
		}
	}

	public static List<int> ToChipIndices(int value)
	{
		var answer = new List<int>();
		for (int i = Denominations.Count() - 1; i >= 0; i--)
		{
			while (value >= Denominations[i])
			{
				answer.Add(i);
				value -= Denominations[i];
			}
		}

		return answer;
	}

	static Vector3 CardSize = new Vector3(0.024f, 1/30f, 1/30f);
	static float CardThickness = 0.0001125f;
	static float CardEpsilon = 0.00001f;

	public KMAudio Audio;
	public KMBombInfo BombInfo;
	public KMBombModule BombModule;
	public KMColorblindMode ColorblindMode;
	public TextMesh DeckColorLabel;
	
	public Material[] CardMaterials;
	public Material[] ChipMaterials;
	public Material[] DeckMaterials;
	public Material[] InlayMaterials;

	public Renderer CurrentChip;
	public Renderer CurrentInlay;
	public KMSelectable LeftChipArrow;
	public KMSelectable RightChipArrow;

	public KMSelectable[] BettingAreas;
	public GameObject[] ChipStacks;

	public KMSelectable DeckSelectable;
	public TextMesh LimitsText;

	public GameObject PlacardObject;
	public Material[] PlacardMaterials;

	public GameObject DeckLocation;
	public GameObject[] PlayerCardLocations;
	public GameObject[] BankerCardLocations;

	public GameObject Nickel;
	public GameObject Quarter;

	Bet[] Bets;

	Stack<GameObject> CardObjects;

	Deck CurrentDeck;
	int StartingCardIndexAtZeroSolves;
	bool IsUnicorn;
	int PortColumn;

	public int CurrentChipIndex { get; private set; }

	int MinimumBet;
	int MaximumBet;

	bool ColorblindModeActive;

	public bool AcceptsInput { get; private set; }

	static int ModuleIdCounter = 1;
	int ModuleId;

	void Awake()
	{
		ModuleId = ModuleIdCounter++;

		CurrentChipIndex = 0;
		CurrentDeck = null;
		CardObjects = new Stack<GameObject>();

		AcceptsInput = false;

		LeftChipArrow.OnInteract += delegate()
		{
			LeftChipArrow.AddInteractionPunch(0.5f);
			Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
			if (AcceptsInput)
			{
				ChangeDenomination(-1);
			}
			return false;
		};
		LeftChipArrow.OnInteractEnded += delegate()
		{
			Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, transform);
		};
		RightChipArrow.OnInteract += delegate()
		{
			RightChipArrow.AddInteractionPunch(0.5f);
			Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
			if (AcceptsInput)
			{
				ChangeDenomination(1);
			}
			return false;
		};
		RightChipArrow.OnInteractEnded += delegate()
		{
			Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, transform);
		};

		Bets = Enumerable.Range(0, 5).Select(i => new Bet(this, BettingAreas[i], ChipStacks[i])).ToArray();

		DeckSelectable.OnInteract += delegate()
		{
			if (AcceptsInput)
			{
				StartCoroutine(OnDeckClicked());
			}
			return false;
		};
	}

	// Use this for initialization
	void Start()
	{
		BombModule.OnActivate += delegate()
		{
			AcceptsInput = true;
		};

		// Determine table limits
		int minBetIndex = UnityEngine.Random.Range(0, 5);
		int ratioIndex = UnityEngine.Random.Range(0, 5);

		MinimumBet = PossibleMinimums[minBetIndex];
		MaximumBet = PossibleMaximums[minBetIndex][ratioIndex].PickRandom();

		PlacardObject.GetComponent<Renderer>().material = PlacardMaterials[minBetIndex];

		LimitsText.text = string.Format("BACCARAT\nTABLE LIMITS\n{0} - {1}", AsDollars(MinimumBet), AsDollars(MaximumBet));

		ModuleLog("Table limits are {0} to {1}", AsDollars(MinimumBet), AsDollars(MaximumBet));

		int portCount = BombInfo.GetPortCount();
		IsUnicorn = BombInfo.IsIndicatorOn(Indicator.BOB) && (portCount == 8 || portCount == 9);

		if (IsUnicorn)
		{
			ModuleLog("Unicorn condition is in effect, all decks are unshuffled");
		}
		else
		{
			ModuleLog("Ratio of max/min = {0}", MaximumBet / MinimumBet);

			int relevantLetterIndex;

			var serialLetterIndices = BombInfo.GetSerialNumberLetters().Select(c => (c - 'A' + 1) % 32);
			if (serialLetterIndices.Count() == 0)
			{
				relevantLetterIndex = 2;
			}
			else
			{
				switch (minBetIndex)
				{
					case 0:
						relevantLetterIndex = serialLetterIndices.First();
						break;
					case 1:
						relevantLetterIndex = serialLetterIndices.Take(2).Last();
						break;
					case 2:
						relevantLetterIndex = serialLetterIndices.TakeLast(2).First();
						break;
					case 3:
						relevantLetterIndex = serialLetterIndices.Last();
						break;
					default:
						relevantLetterIndex = serialLetterIndices.Sum() % 26;
						if (relevantLetterIndex == 0)
						{
							relevantLetterIndex = 26;
						}
						break;
				}
			}

			int relevantDigit;
			
			var serialDigits = BombInfo.GetSerialNumberNumbers();
			if (serialDigits.Count() == 0)
			{
				relevantDigit = 9;
			}
			else
			{
				switch (ratioIndex)
				{
					case 0:
						relevantDigit = serialDigits.First();
						break;
					case 1:
						relevantDigit = serialDigits.Take(2).Last();
						break;
					case 2:
						relevantDigit = serialDigits.TakeLast(2).First();
						break;
					case 3:
						relevantDigit = serialDigits.Last();
						break;
					default:
						relevantDigit = serialDigits.Sum() % 10;
						break;
				}
			}

			ModuleLog("Relevant characters: {0}, {1}", (char)('A' - 1 + relevantLetterIndex), relevantDigit);

			StartingCardIndexAtZeroSolves = relevantLetterIndex + (relevantDigit % 2) * 26;
			ModuleLog("Starting card index at zero solves: {0}", StartingCardIndexAtZeroSolves);

			var vanillaPorts = new Port[] { Port.DVI, Port.Parallel, Port.PS2, Port.RJ45, Port.Serial, Port.StereoRCA };
			var portCounts = vanillaPorts.Select(BombInfo.GetPortCount).ToArray();
			var maxPortCount = portCounts.Max();
			
			var maximalIndices = Enumerable.Range(0, 6).Where(i => portCounts[i] == maxPortCount);
			if (maximalIndices.Count() == 1)
			{
				PortColumn = maximalIndices.First();
				ModuleLog("Most common vanilla port: {0}", vanillaPorts[PortColumn]);
			}
			else
			{
				ModuleLog("Most common vanilla ports: {0}", maximalIndices.Select(i => vanillaPorts[i]).Join(", "));
				int rawSum = maximalIndices.Sum() + maximalIndices.Count();
				ModuleLog("Sum of column indices is {0}={1}", maximalIndices.Select(i => i + 1).Join("+"), rawSum);
				PortColumn = (rawSum - 1) % 6;
				ModuleLog("Use column {0} ({1})", PortColumn + 1, vanillaPorts[PortColumn]);
			}
		}

		ColorblindModeActive = ColorblindMode.ColorblindModeActive;

		CreateNewDeck();
	}
	
	void ModuleLog(string format, params object[] args)
	{
		var prefix = string.Format("[Baccarat #{0}] ", ModuleId);
		Debug.LogFormat(prefix + format, args);
	}

	// Update is called once per frame
	void Update()
	{
		
	}

	void ChangeDenomination(int amount)
	{
		int newChipIndex = CurrentChipIndex + amount;
		if (newChipIndex >= 0 && newChipIndex < Denominations.Length)
		{
			CurrentChipIndex = newChipIndex;
			CurrentChip.material = ChipMaterials[CurrentChipIndex];
			CurrentInlay.material = InlayMaterials[CurrentChipIndex];
		}
	}

	void CreateNewDeck()
	{
		// Get rid of the old deck
		foreach (var card in CardObjects)
		{
			Destroy(card);
		}
		
		CardObjects.Clear();

		// Create the new deck...
		var exceptions = new Deck[] { CurrentDeck }.Where(l => l != null).Select(l => l.Color);
		var possibleColors = Enum.GetValues(typeof(DeckColor)).Cast<DeckColor>().Except(exceptions);

		var nextColor = possibleColors.PickRandom();
		
		int solvedModuleCount = BombInfo.GetSolvedModuleNames().Count;
		
		ModuleLog("Creating new deck at {0} solves, color is {1}", solvedModuleCount, nextColor);

		if (IsUnicorn)
		{
			ModuleLog("Deck is unshuffled");
			CurrentDeck = new Deck(nextColor);
		}
		else
		{
			int firstCardIndex = (solvedModuleCount + StartingCardIndexAtZeroSolves) % FullDeckSize;
			if (firstCardIndex == 0)
			{
				firstCardIndex = FullDeckSize;
			}

			ModuleLog("Index of first card is {0}", firstCardIndex);

			var lookupTable = new Dictionary<DeckColor, int[]> {
				{ DeckColor.Red, new int[] {20, 33, 26, 41, 12, 31} },
				{ DeckColor.Blue, new int[] {18, 2, 50, 22, 34, 8} },
				{ DeckColor.Green, new int[] {39, 27, 35, 3, 19, 45} },
				{ DeckColor.Silver, new int[] {5, 48, 14, 51, 32, 21} }
			};

			int multiplier = lookupTable[nextColor][PortColumn];

			ModuleLog("Successor multiplier is {0}", multiplier);

			CurrentDeck = new Deck(nextColor, firstCardIndex, multiplier);
		}

		// ...and create the corresponding game objects
		for (int i = 0; i < FullDeckSize; i++)
		{
			var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
			// This card should be positioned relative to the module, since it can move around
			card.transform.parent = transform;
			card.transform.localPosition = DeckLocation.transform.localPosition + Vector3.up * CardThickness * i;
			card.transform.localRotation = Quaternion.AngleAxis(90f, Vector3.right);
			card.transform.localScale = CardSize;
			card.GetComponent<Renderer>().material = DeckMaterials[(int)CurrentDeck.Color];
			card.GetComponent<Renderer>().sortingOrder = -3;

			// The "front" face should start out as blank. No peeking!
			var frontFace = GameObject.CreatePrimitive(PrimitiveType.Quad);
			frontFace.transform.parent = card.transform;
			// Just so the two quads aren't directly on top of each other...
			frontFace.transform.localPosition = Vector3.down * CardEpsilon;
			frontFace.transform.localRotation = Quaternion.AngleAxis(180f, Vector3.up);
			frontFace.transform.localScale = Vector3.one;
			frontFace.GetComponent<Renderer>().material = CardMaterials[0];
			frontFace.GetComponent<Renderer>().sortingOrder = -3;

			CardObjects.Push(card);
		}

		UpdateColorblindLabel();
	}

	IEnumerator ChangeLocalPosition(GameObject obj, Vector3 newLocalPosition, int steps, float delay = 0f)
	{
		yield return new WaitForSeconds(delay);
		var oldPosition = obj.transform.localPosition;
		for (int i = 0; i < steps; i++)
		{
			float t = (i + 1) / (float)steps;
			obj.transform.localPosition = Vector3.Lerp(oldPosition, newLocalPosition, t);
			yield return null;
		}
	}

	IEnumerator FlipCard(GameObject card, float delay = 0f)
	{
		// These have been chosen such that we peak at step 13 and land on the surface of the module at step 30.
		var distances = Enumerable.Range(0, 31).Select(i => -153/3.2e6f * i*i + 1989/1.6e6f * i + 0.0057375f).ToArray();

		yield return new WaitForSeconds(delay);
		
		Audio.PlaySoundAtTransform("flipcard", transform);

		for (int i = 0; i < 30; i++)
		{
			card.transform.localPosition += Vector3.up * (distances[i + 1] - distances[i]);
			card.transform.Rotate(Vector3.up, 6f);
			yield return null;
		}
	}

	IEnumerator OnDeckClicked()
	{
		if (Bets.All(b => b.Value == 0))
		{
			ModuleLog("Are you seriously playing without placing a bet? Come on, it's not like you're betting real money or anything.");
		}
		else
		{
			var bets = Enumerable.Range(0, 5).Where(i => Bets[i].Value > 0).Select(i => string.Format("{0} on {1}", AsDollars(Bets[i].Value), (BetType)i));
			ModuleLog("Betting {0}", bets.Join(", "));
		}

		// Validate player/banker exclusivity
		int playerBetAmount = Bets[(int)BetType.Player].Value;
		int bankerBetAmount = Bets[(int)BetType.Banker].Value;

		if ((playerBetAmount == 0) == (bankerBetAmount == 0))
		{
			BombModule.HandleStrike();
			ModuleLog("Strike! You must bet on either Player or Banker, but not both.");
			yield break;
		}

		// Validate limits
		for (int i = 0; i < Bets.Length; i++)
		{
			int multiplier = (i < 2) ? 1 : 10;
			int adjustedBet = multiplier * Bets[i].Value;
			if (adjustedBet != 0 && (adjustedBet < MinimumBet || adjustedBet > MaximumBet))
			{
				BombModule.HandleStrike();
				float trueMin = MinimumBet / (float)multiplier;
				float trueMax = MaximumBet / (float)multiplier;
				ModuleLog("Strike! Your {0} bet on {1} is not between the expected limits ({2} to {3}).", AsDollars(Bets[i].Value), (BetType)i, AsDollars(trueMin), AsDollars(trueMax));
				yield break;
			}
		}

		// We're ready to deal, so disable input
		AcceptsInput = false;

		// Check for and fix malformed barber poles
		var malformedPoles = Bets.Where(b => b.HasMalformedBarberPole());
		if (malformedPoles.Count() > 0)
		{
			BombModule.HandleStrike();
			ModuleLog("Strike! Multi-chip bets must have the larger chips on the bottom. Fixing before deal...");
		}

		foreach (var bet in malformedPoles)
		{
			yield return StartCoroutine(bet.FixMalformedBarberPole());
		}

		// Deal the cards!
		var playerCards = new List<PlayingCard>();
		var bankerCards = new List<PlayingCard>();

		var playerCardObjects = new List<GameObject>();
		var bankerCardObjects = new List<GameObject>();

		for (int i = 0; i < 4; i++)
		{
			var nextCard = CurrentDeck.DealCard();
			((i % 2 == 0) ? playerCards : bankerCards).Add(nextCard);
			ModuleLog("{0} receives {1}", (i % 2 == 0) ? "Player" : "Banker", nextCard);

			var cardObject = CardObjects.Pop();
			((i % 2 == 0) ? playerCardObjects : bankerCardObjects).Add(cardObject);
			cardObject.transform.GetChild(0).GetComponent<Renderer>().material = CardMaterials[nextCard.MaterialIndex];

			var targetPosition = ((i % 2 == 0) ? PlayerCardLocations : BankerCardLocations)[i / 2].transform.localPosition;
			targetPosition.y += CardThickness * (FullDeckSize - 1);
			StartCoroutine(ChangeLocalPosition(cardObject, targetPosition, 30));

			Audio.PlaySoundAtTransform("dealcard", transform);

			StartCoroutine(FlipCard(cardObject, 1f));

			yield return new WaitForSeconds(0.25f);

			UpdateColorblindLabel();
		}

		yield return new WaitForSeconds(1.5f);

		int playerScore = playerCards.Sum(card => card.BaccaratValue) % 10;
		int bankerScore = bankerCards.Sum(card => card.BaccaratValue) % 10;

		ModuleLog("Player score is {0}", playerScore);
		ModuleLog("Banker score is {0}", bankerScore);

		if (playerScore <= 7 && bankerScore <= 7)
		{
			bool playerDrawsAgain = playerScore <= 5;
			bool bankerDrawsAgain = bankerScore <= 5;

			if (playerDrawsAgain)
			{
				var nextCard = CurrentDeck.DealCard();
				playerCards.Add(nextCard);
				ModuleLog("Player draws third card, receives {0}", nextCard);

				playerScore = playerCards.Sum(card => card.BaccaratValue) % 10;
				ModuleLog("Player score is now {0}", playerScore);

				var cardObject = CardObjects.Pop();
				playerCardObjects.Add(cardObject);
				cardObject.GetComponent<Renderer>().sortingOrder = -2;

				var frontFaceRenderer = cardObject.transform.GetChild(0).GetComponent<Renderer>();
				frontFaceRenderer.material = CardMaterials[nextCard.MaterialIndex];
				frontFaceRenderer.sortingOrder = -1;

				var targetPosition = PlayerCardLocations[2].transform.localPosition;
				targetPosition.y += CardThickness * (FullDeckSize - 1);
				StartCoroutine(ChangeLocalPosition(cardObject, targetPosition, 30));
				StartCoroutine(TurnCard(cardObject, -90f));

				Audio.PlaySoundAtTransform("dealcard", transform);

				StartCoroutine(FlipCard(cardObject, 1f));

				yield return new WaitForSeconds(2f);

				UpdateColorblindLabel();

				int[] whenBankerDraws = {30, 31, 32, 33, 34, 35, 36, 37, 39, 42, 43, 44, 45, 46, 47, 54, 55, 56, 57, 66, 67};
				bankerDrawsAgain = bankerScore <= 2 || whenBankerDraws.Contains(10 * bankerScore + playerCards.Last().BaccaratValue);
			}
			else
			{
				ModuleLog("Player stands with two cards");
			}

			if (bankerDrawsAgain)
			{
				var nextCard = CurrentDeck.DealCard();
				bankerCards.Add(nextCard);
				ModuleLog("Banker draws third card, receives {0}", nextCard);

				bankerScore = bankerCards.Sum(card => card.BaccaratValue) % 10;
				ModuleLog("Banker score is now {0}", bankerScore);

				var cardObject = CardObjects.Pop();
				bankerCardObjects.Add(cardObject);
				cardObject.GetComponent<Renderer>().sortingOrder = -2;

				var frontFaceRenderer = cardObject.transform.GetChild(0).GetComponent<Renderer>();
				frontFaceRenderer.material = CardMaterials[nextCard.MaterialIndex];
				frontFaceRenderer.sortingOrder = -1;

				var targetPosition = BankerCardLocations[2].transform.localPosition;
				targetPosition.y += CardThickness * (FullDeckSize - 1);
				StartCoroutine(ChangeLocalPosition(cardObject, targetPosition, 30));
				StartCoroutine(TurnCard(cardObject, 90f));

				Audio.PlaySoundAtTransform("dealcard", transform);

				StartCoroutine(FlipCard(cardObject, 1f));

				yield return new WaitForSeconds(2f);

				UpdateColorblindLabel();
			}
			else
			{
				ModuleLog("Banker stands with two cards");
			}
		}
		else
		{
			ModuleLog("Natural! No new cards are drawn.");
		}

		int result = Math.Sign(playerScore - bankerScore);
		if (result == 1)
		{
			ModuleLog("Player wins!");
			foreach (var cardObject in playerCardObjects)
			{
				StartCoroutine(ChangeLocalPosition(cardObject, cardObject.transform.localPosition + Vector3.back * 0.01f, 15));
			}
		}
		else if (result == -1)
		{
			ModuleLog("Banker wins!");
			foreach (var cardObject in bankerCardObjects)
			{
				StartCoroutine(ChangeLocalPosition(cardObject, cardObject.transform.localPosition + Vector3.back * 0.01f, 15));
			}
		}
		else if (result == 0)
		{
			ModuleLog("Tie!");
		}

		yield return new WaitForSeconds(0.25f);

		// Resolve all bets and determine what happens next
		int totalProfitInCents = 0;

		int maxStackSizePlaced = 0;
		bool chipsWereRemoved = false;

		var playerBet = Bets[(int)BetType.Player];
		if (playerBet.Value > 0)
		{
			int betValue = playerBet.Value;
			totalProfitInCents += 100 * result * betValue;

			if (result == 1)
			{
				maxStackSizePlaced = Math.Max(maxStackSizePlaced, playerBet.PlaceEarnings(betValue));
				ModuleLog("Player bet wins {0}", AsDollars(betValue));
			}
			else if (result == -1)
			{
				playerBet.Clear();
				chipsWereRemoved = true;
				ModuleLog("Player bet loses {0}", AsDollars(betValue));
			}
			else
			{
				ModuleLog("Player bet pushes");
			}
		}

		var bankerBet = Bets[(int)BetType.Banker];
		if (bankerBet.Value > 0)
		{
			int betValue = bankerBet.Value;
			
			if (result == 1)
			{
				totalProfitInCents -= 100 * betValue;
				bankerBet.Clear();
				chipsWereRemoved = true;
				ModuleLog("Banker bet loses {0}", AsDollars(betValue));
			}
			else if (result == -1)
			{
				totalProfitInCents += 95 * betValue;
				maxStackSizePlaced = Math.Max(maxStackSizePlaced, bankerBet.PlaceEarnings(0.95f * betValue));
				ModuleLog("Banker bet wins {0}", AsDollars(0.95f * betValue));
			}
			else
			{
				ModuleLog("Banker bet pushes");
			}
		}

		var tieBet = Bets[(int)BetType.Tie];
		if (tieBet.Value > 0)
		{
			int betValue = tieBet.Value;
			
			if (result == 0)
			{
				totalProfitInCents += 800 * betValue;
				maxStackSizePlaced = Math.Max(maxStackSizePlaced, tieBet.PlaceEarnings(8 * betValue));
				ModuleLog("Tie bet wins {0}", AsDollars(8 * betValue));
			}
			else
			{
				totalProfitInCents -= 100 * betValue;
				tieBet.Clear();
				chipsWereRemoved = true;
				ModuleLog("Tie bet loses {0}", AsDollars(betValue));
			}
		}

		var playerPairBet = Bets[(int)BetType.PlayerPair];
		if (playerPairBet.Value > 0)
		{
			int betValue = playerPairBet.Value;
			
			if (playerCards[0].Rank == playerCards[1].Rank)
			{
				totalProfitInCents += 1100 * betValue;
				maxStackSizePlaced = Math.Max(maxStackSizePlaced, playerPairBet.PlaceEarnings(11 * betValue));
				ModuleLog("Player Pair bet wins {0}", AsDollars(11 * betValue));
			}
			else
			{
				totalProfitInCents -= 100 * betValue;
				playerPairBet.Clear();
				chipsWereRemoved = true;
				ModuleLog("Player Pair bet loses {0}", AsDollars(betValue));
			}
		}

		var bankerPairBet = Bets[(int)BetType.BankerPair];
		if (bankerPairBet.Value > 0)
		{
			int betValue = bankerPairBet.Value;
			
			if (bankerCards[0].Rank == bankerCards[1].Rank)
			{
				totalProfitInCents += 1100 * betValue;
				maxStackSizePlaced = Math.Max(maxStackSizePlaced, bankerPairBet.PlaceEarnings(11 * betValue));
				ModuleLog("Banker Pair bet wins {0}", AsDollars(11 * betValue));
			}
			else
			{
				totalProfitInCents -= 100 * betValue;
				bankerPairBet.Clear();
				chipsWereRemoved = true;
				ModuleLog("Banker Pair bet loses {0}", AsDollars(betValue));
			}
		}

		// Figure out which sound to play
		if (maxStackSizePlaced > 1)
		{
			Audio.PlaySoundAtTransform("manychips", transform);	
		}
		else if (maxStackSizePlaced == 1)
		{
			Audio.PlaySoundAtTransform("chip", transform);
		}
		else if (chipsWereRemoved)
		{
			Audio.PlaySoundAtTransform("chipclear", transform);
		}

		ModuleLog("Total profit: {0}", AsDollars(totalProfitInCents * 0.01f));

		if (totalProfitInCents > 0)
		{
			BombModule.HandlePass();
			ModuleLog("You made a profit! Module disarmed.");
		}
		else
		{
			if (totalProfitInCents < 0)
			{
				BombModule.HandleStrike();
				ModuleLog("Strike! You lost money that hand.");
			}
			else
			{
				ModuleLog("You broke even. Nothing happens.");
			}

			yield return new WaitForSeconds(1.5f);

			// Reset the module
			playerCardObjects.ForEach(Destroy);
			bankerCardObjects.ForEach(Destroy);
			
			if (Bets.Any(bet => bet.Value > 0))
			{
				Audio.PlaySoundAtTransform("chipclear", transform);
			}

			foreach (var bet in Bets)
			{
				bet.Clear();
			}

			if (CurrentDeck.CardsLeft < 26)
			{
				CreateNewDeck();
				Audio.PlaySoundAtTransform("shuffle", transform);
			}

			AcceptsInput = true;
		}
	}

	IEnumerator TurnCard(GameObject card, float amount, float delay = 0f)
	{
		yield return new WaitForSeconds(delay);
		for (int i = 0; i < 30; i++)
		{
			card.transform.Rotate(Vector3.forward, amount / 30);
			yield return null;
		}
	}

	void UpdateColorblindLabel()
	{
		// The colorblind label should sit on top of the top card
		DeckColorLabel.text = ColorblindModeActive ? CurrentDeck.Color.ToString() : "";
		DeckColorLabel.transform.localPosition = CardObjects.Peek().transform.localPosition + Vector3.up * CardEpsilon;
	}

	#pragma warning disable 414
	string TwitchHelpMessage = "Place a bet with \"!{0} bet <amount> on <location>\". Bet on multiple locations with \"!{0} bet <amount1> on <location1>, <amount2> on <location2>, ...\". Betting locations are \"player\", \"banker\", \"tie\", \"player pair\", and \"banker pair\". Use \"!{0} colorblind\" to activate colorblind mode. Use \"!{0} tilt d\" to get a better view of the table limits.";
	#pragma warning restore 414

	IEnumerator ProcessTwitchCommand(string command)
	{
		if (command.Trim().EqualsIgnoreCase("colorblind") || command.Trim().EqualsIgnoreCase("colourblind"))
		{
			ColorblindModeActive = true;
			UpdateColorblindLabel();
			yield return null;
			yield break;
		}

		if (!command.StartsWith("bet", StringComparison.InvariantCultureIgnoreCase))
		{
			yield break;
		}

		if (!AcceptsInput)
		{
			yield return "sendtochat Unable to place bets at this time.";
			yield break;
		}

		var betAmounts = new Dictionary<BetType, int>();

		var betTypeLookup = new Dictionary<string, BetType>()
		{
			{ "player", BetType.Player },
			{ "banker", BetType.Banker },
			{ "tie", BetType.Tie },
			{ "player pair", BetType.PlayerPair },
			{ "playerpair", BetType.PlayerPair },
			{ "banker pair", BetType.BankerPair },
			{ "bankerpair", BetType.BankerPair }
		};

		foreach (var betCommand in command.Substring(3).ToLowerInvariant().Split(new[] {','}))
		{
			if (betCommand.Trim().Length == 0)
			{
				continue;
			}

			var match = Regex.Match(betCommand.Trim(), "^\\$?(\\d+) +on +(.+)$");
			if (!match.Success)
			{
				yield return string.Format("sendtochat Unable to parse \"{0}\".", betCommand.Trim());
				yield break;
			}

			int? possibleAmount = match.Groups[1].Value.TryParseInt();
			if (possibleAmount == null)
			{
				yield return string.Format("sendtochat Unable to parse number \"{0}\".", match.Groups[1].Value);
				yield break;
			}

			int amount = possibleAmount.Value;

			// If the amount is negative or greater than 1 million, we almost certainly got passed a number outside table limits.
			// In this case, cap the bet at $1 million and await the impending strike.
			if (amount < 0 || amount > 1000000)
			{
				amount = 1000000;
			}

			var locationString = match.Groups[2].Value;
			if (!betTypeLookup.ContainsKey(locationString))
			{
				yield return string.Format("sendtochat Unknown betting location \"{0}\".", locationString);
				yield break;
			}

			var location = betTypeLookup[locationString];
			if (betAmounts.ContainsKey(location))
			{
				yield return string.Format("sendtochat Unable to bet multiple times on {0}.", location.ToString());
				yield break;
			}

			betAmounts[location] = amount;
		}

		if (betAmounts.Count == 0)
		{
			yield return string.Format("sendtochat And exactly what do you want me to bet, genius?");
			yield break;
		}

		// Clear all betting areas
		for (int i = 0; i < Bets.Length; i++)
		{
			if (Bets[i].Value > 0)
			{
				yield return null;
				yield return BettingAreas[i];
				yield return new WaitForSeconds(0.75f);
				yield return BettingAreas[i];
				yield return new WaitForSeconds(0.05f);
			}
		}

		// Place bets
		foreach (var keyValuePair in betAmounts)
		{
			foreach (var denomination in ToChipIndices(keyValuePair.Value))
			{
				// Select the correct denomination
				while (CurrentChipIndex > denomination)
				{
					yield return null;
					yield return new[] { LeftChipArrow };
					yield return new WaitForSeconds(0.05f);
				}
				while (CurrentChipIndex < denomination)
				{
					yield return null;
					yield return new[] { RightChipArrow };
					yield return new WaitForSeconds(0.05f);
				}

				// Place the chip
				var bettingLocation = BettingAreas[(int) keyValuePair.Key]; 
				yield return null;
				yield return new[] { bettingLocation };
				yield return new WaitForSeconds(0.05f);
			}
		}

		// Deal the cards
		yield return null;
		yield return new[] { DeckSelectable };
	}
}
