using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KtaneBaccarat
{
	class Bet
	{
		static float ChipThickness = 0.0033f * 0.375f;
		static float ChipInlayDepth = 0.0004f;
		static float ChipSidestepDistance = 0.039f * 0.375f;
		static int SlideFrames = 20;

		BaccaratScript Owner;
		KMSelectable Selectable;
		GameObject ChipStack;

		Coroutine ActiveCoroutine;

		List<GameObject> Chips;
		List<int> ChipIndices;

		public int Value
		{
			get
			{
				return ChipIndices.Select(i => BaccaratScript.Denominations[i]).Sum();
			}
		}

		public Bet(BaccaratScript owner, KMSelectable selectable, GameObject chipStack)
		{
			Owner = owner;
			Selectable = selectable;
			ChipStack = chipStack;

			Chips = new List<GameObject>();
			ChipIndices = new List<int>();

			Selectable.OnInteract += delegate()
			{
				if (Owner.AcceptsInput)
				{
					if (ActiveCoroutine != null)
					{
						Owner.StopCoroutine(ActiveCoroutine);
					}
					ActiveCoroutine = Owner.StartCoroutine(CheckForLongPress());
				}
				return false;
			};
			Selectable.OnInteractEnded += delegate()
			{
				if (Owner.AcceptsInput)
				{
					if (ActiveCoroutine != null)
					{
						Owner.StopCoroutine(ActiveCoroutine);
						ActiveCoroutine = null;
						
						// Short press, add a new chip
						float chipRotation = UnityEngine.Random.Range(0f, 360f);

						// The new chip will keep the textures of the active chip
						var newChip = BaccaratScript.Instantiate(Owner.CurrentChip.gameObject, ChipStack.transform);
						newChip.transform.localScale *= 0.5f;
						newChip.transform.localPosition = Vector3.up * ChipThickness * ChipIndices.Count;
						newChip.transform.localEulerAngles = new Vector3(-90, 0, chipRotation);

						newChip.transform.GetChild(0).Rotate(Vector3.forward, chipRotation);

						Chips.Add(newChip);
						ChipIndices.Add(Owner.CurrentChipIndex);

						Owner.Audio.PlaySoundAtTransform("chip", Owner.transform);
					}
				}
			};
		}

		IEnumerator CheckForLongPress()
		{
			yield return new WaitForSeconds(0.5f);
			bool stillActive = (ActiveCoroutine != null);
			ActiveCoroutine = null;
			if (stillActive)
			{
				Clear();
			}
		}

		public void Clear()
		{
			bool hadValue = Value > 0;

			// Long press, remove all chips
			for (int i = 0; i < ChipStack.transform.childCount; i++)
			{
				BaccaratScript.Destroy(ChipStack.transform.GetChild(i).gameObject);
			}

			Chips.Clear();
			ChipIndices.Clear();

			if (hadValue)
			{
				Owner.Audio.PlaySoundAtTransform("chipclear", Owner.transform);
			}
		}

		public IEnumerator FixMalformedBarberPole()
		{
			var denominationIndices = new Dictionary<int, List<int>>();
			for (int i = 0; i < ChipIndices.Count; i++)
			{
				int denomination = ChipIndices[i];

				if (!denominationIndices.ContainsKey(denomination))
				{
					denominationIndices[denomination] = new List<int>();
				}
				denominationIndices[denomination].Add(i);
			}

			// Each pass represents a subset of chips we pull out to get them in sorted order.
			var passes = new List<List<int>>();
			foreach (var denominationIndex in denominationIndices.Keys.OrderByDescending(i => i))
			{
				var theseIndices = denominationIndices[denominationIndex];

				if (passes.Count == 0)
				{
					passes.Add(new List<int>(theseIndices));
				}
				else
				{
					var pieces = theseIndices.ToLookup(i => i > passes.Last().Last());
					passes.Last().AddRange(pieces[true]);

					if (pieces[false].Count() > 0)
					{
						passes.Add(pieces[false].ToList());
					}
				}
			}

			if (passes.Count < 2)
			{
				yield break;
			}

			var chipsLeftToBring = new List<GameObject>(Chips);

			int numberOfChipsBroughtOver = 0;

			// Handle everything except the last pass
			foreach (var pass in passes.Take(passes.Count() - 1))
			{
				var chipsToPull = pass.Select(i => Chips[i]).ToList();
				
				// Step 1: pull the chips out
				for (int j = 0; j < SlideFrames; j++)
				{
					chipsToPull.ForEach(obj => {
						obj.transform.localPosition += Vector3.right * ChipSidestepDistance / SlideFrames;
					});
					yield return null;
				}

				// Step 2: bring the pulled chips down and the rest of the stack up
				chipsLeftToBring = chipsLeftToBring.Except(chipsToPull).ToList();

				var newStackGoalPositions = Enumerable.Range(0, chipsToPull.Count).Select(i => {
					var chip = chipsToPull[i];
					var baseVector = Vector3.ProjectOnPlane(chip.transform.localPosition, Vector3.up);
					return baseVector + Vector3.up * ChipThickness * (numberOfChipsBroughtOver + i);
				}).ToArray();

				var newStackSteps = Enumerable.Range(0, chipsToPull.Count).Select(i => {
					var start = chipsToPull[i].transform.localPosition;
					var goal = newStackGoalPositions[i];
					return Enumerable.Range(1, SlideFrames).Select(j => Vector3.Lerp(start, goal, j / (float)SlideFrames)).ToArray();
				}).ToArray();

				var oldStackGoalPositions = Enumerable.Range(0, chipsLeftToBring.Count).Select(i => {
					var chip = chipsLeftToBring[i];
					var baseVector = Vector3.ProjectOnPlane(chip.transform.localPosition, Vector3.up);
					return baseVector + Vector3.up * ChipThickness * (numberOfChipsBroughtOver + chipsToPull.Count + i);
				}).ToArray();

				var oldStackSteps = Enumerable.Range(0, chipsLeftToBring.Count).Select(i => {
					var start = chipsLeftToBring[i].transform.localPosition;
					var goal = oldStackGoalPositions[i];
					return Enumerable.Range(1, SlideFrames).Select(j => Vector3.Lerp(start, goal, j / (float)SlideFrames)).ToArray();
				}).ToArray();

				for (int i = 0; i < SlideFrames; i++)
				{
					for (int j = 0; j < chipsToPull.Count; j++)
					{
						chipsToPull[j].transform.localPosition = newStackSteps[j][i];
					}
					for (int j = 0; j < chipsLeftToBring.Count; j++)
					{
						chipsLeftToBring[j].transform.localPosition = oldStackSteps[j][i];
					}

					yield return null;
				}

				numberOfChipsBroughtOver += chipsToPull.Count;
			}

			// Last pass: return the chips we pulled back to the original stack
			var chipsPulled = Chips.Except(chipsLeftToBring).ToList();

			for (int j = 0; j < SlideFrames; j++)
			{
				chipsPulled.ForEach(obj => {
					obj.transform.localPosition -= Vector3.right * ChipSidestepDistance / SlideFrames;
				});
				yield return null;
			}
		}

		// A malformed barber pole is where a chip sits on top of a chip with a smaller value.
		public bool HasMalformedBarberPole()
		{
			int index = BaccaratScript.Denominations.Length;
			foreach (int nextChipIndex in ChipIndices)
			{
				if (nextChipIndex > index)
				{
					return true;
				}
				index = nextChipIndex;
			}
			return false;
		}

		// Returns the number of chips placed.
		public int PlaceEarnings(int value)
		{
			var chipIndices = BaccaratScript.ToChipIndices(value);

			for (int i = 0; i < chipIndices.Count; i++)
			{
				float chipRotation = UnityEngine.Random.Range(0f, 360f);

				// The new chip will keep the textures of the active chip, so rewrite them
				var newChip = BaccaratScript.Instantiate(Owner.CurrentChip.gameObject, ChipStack.transform);
				newChip.GetComponent<Renderer>().material = Owner.ChipMaterials[chipIndices[i]];
				newChip.transform.localScale *= 0.5f;
				newChip.transform.localPosition = Vector3.up * ChipThickness * i + Vector3.left * ChipSidestepDistance;
				newChip.transform.localEulerAngles = new Vector3(-90, 0, chipRotation);

				var inlayTransform = newChip.transform.GetChild(0);
				inlayTransform.gameObject.GetComponent<Renderer>().material = Owner.InlayMaterials[chipIndices[i]];
				inlayTransform.Rotate(Vector3.forward, chipRotation);
			}

			return chipIndices.Count;
		}

		// Returns the number of chips and coins placed.
		public int PlaceEarnings(float value)
		{
			int cents = (int)Math.Round(value * 100);
			int wholeDollars = cents / 100;
			int centsOnly = cents % 100;

			int itemsPlaced = PlaceEarnings(wholeDollars);

			var position = Vector3.up * (ChipThickness * itemsPlaced - ChipInlayDepth) + Vector3.left * ChipSidestepDistance;
			var coinOffset = Vector2.zero;

			while (centsOnly >= 25) {
				var quarter = BaccaratScript.Instantiate(Owner.Quarter, ChipStack.transform);
				quarter.SetActive(true);
				quarter.transform.localScale *= 0.375f;
				quarter.transform.localPosition = position + new Vector3(coinOffset.x, 0, coinOffset.y);
				quarter.transform.localEulerAngles = new Vector3(0, UnityEngine.Random.Range(0f, 360f), 0);

				// Flip with 50% chance
				if (UnityEngine.Random.Range(0, 2) == 0)
				{
					quarter.transform.Rotate(Vector3.forward, 180);
					quarter.transform.localPosition += Vector3.up * 0.00175f * 0.375f;
				}

				centsOnly -= 25;
				coinOffset = 0.0001f * UnityEngine.Random.insideUnitCircle;
				position += Vector3.up * 0.00175f * 0.375f;

				itemsPlaced++;
			}

			while (centsOnly >= 5) {
				var nickel = BaccaratScript.Instantiate(Owner.Nickel, ChipStack.transform);
				nickel.SetActive(true);
				nickel.transform.localScale *= 0.375f;
				nickel.transform.localPosition = position + new Vector3(coinOffset.x, 0, coinOffset.y);
				nickel.transform.localEulerAngles = new Vector3(0, UnityEngine.Random.Range(0f, 360f), 0);

				// Flip with 50% chance
				if (UnityEngine.Random.Range(0, 2) == 0)
				{
					nickel.transform.Rotate(Vector3.forward, 180);
					nickel.transform.localPosition += Vector3.up * 0.00195f * 0.375f;
				}

				centsOnly -= 5;
				coinOffset = 0.0001f * UnityEngine.Random.insideUnitCircle;
				position += Vector3.up * 0.00195f * 0.375f;

				itemsPlaced++;
			}

			return itemsPlaced;
		}
	}

	enum BetType
	{
		Player,
		Banker,
		Tie,
		PlayerPair,
		BankerPair
	}
}