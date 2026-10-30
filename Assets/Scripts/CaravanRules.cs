using System;
using ITKamianets.Engine.Save;
using UnityEngine;

namespace Temnorid.CaravanGuard
{
	public enum EnemyKind
	{
		Raider,
		Wolf,
		Rider,
	}

	/// <summary>What one kind of attacker can do. Numbers are tuned with the balance test.</summary>
	public sealed class EnemyStats
	{
		public string Name;
		public float Health;
		public float Speed;
		public float Damage;
		public float Interval;
		/// <summary>How close to a wagon it must be to bite or strike.</summary>
		public float Reach;
		public int Reward;
		public string AssetId;
	}

	public static class Enemies
	{
		static readonly EnemyStats Raider = new EnemyStats { Name = "Raider", Health = 40f, Speed = 3.2f, Damage = 3f, Interval = 1.3f, Reach = 2.6f, Reward = 8, AssetId = "units/raider" };
		static readonly EnemyStats Wolf = new EnemyStats { Name = "Wolf", Health = 20f, Speed = 5.4f, Damage = 2f, Interval = 0.9f, Reach = 2.4f, Reward = 6, AssetId = "monsters/wolf" };
		static readonly EnemyStats Rider = new EnemyStats { Name = "Steppe rider", Health = 75f, Speed = 4.8f, Damage = 6f, Interval = 1.5f, Reach = 3.0f, Reward = 14, AssetId = "units/steppe-rider" };

		public static EnemyStats For(EnemyKind kind)
		{
			switch (kind)
			{
				case EnemyKind.Wolf: return Wolf;
				case EnemyKind.Rider: return Rider;
				default: return Raider;
			}
		}
	}

	/// <summary>The one guard type: a guard post with an archer. Level 1 is bought, level 2 is the upgrade.</summary>
	public static class GuardStats
	{
		public const int MaxLevel = 2;
		public const int BuyCost = 50;
		public const int UpgradeCost = 40;

		public static float Damage(int level) => level >= 2 ? 18f : 11f;
		public static float Range(int level) => level >= 2 ? 19f : 15f;
		public static float Interval(int level) => level >= 2 ? 0.7f : 0.8f;
	}

	/// <summary>How many attackers of each kind come in one wave and how long they take to arrive.</summary>
	public struct WaveDef
	{
		public int Raiders;
		public int Wolves;
		public int Riders;
		public float SpawnSeconds;

		public int Count => Raiders + Wolves + Riders;
	}

	public static class Waves
	{
		public const int Count = 3;
		public const int ClearBonus = 40;

		public static WaveDef Plan(int index)
		{
			switch (index)
			{
				case 0: return new WaveDef { Raiders = 8, SpawnSeconds = 18f };
				case 1: return new WaveDef { Raiders = 8, Wolves = 6, SpawnSeconds = 22f };
				default: return new WaveDef { Raiders = 10, Wolves = 6, Riders = 4, SpawnSeconds = 26f };
			}
		}
	}

	/// <summary>What is kept between journeys: how many were played and won, and the best result.</summary>
	[Serializable]
	public sealed class CaravanSave
	{
		public int plays;
		public int wins;
		/// <summary>The most waves cleared in one journey, 0 to <see cref="Waves.Count"/>.</summary>
		public int bestWave;
		/// <summary>The most caravan health left after a won journey, in percent.</summary>
		public int bestHealthPercent;
	}

	/// <summary>
	/// The coins and records of one player with no Unity objects in them: what a guard costs, what a wave
	/// pays, and what is saved. The scene (pads, caravan, attackers) lives in <see cref="CaravanGame"/>.
	/// </summary>
	public sealed class CaravanSession
	{
		public const int StartingCoins = 180;

		public int Coins { get; private set; }
		public int WavesCleared { get; private set; }
		public int Plays { get; private set; }
		public int Wins { get; private set; }
		public int BestWave { get; private set; }
		public int BestHealthPercent { get; private set; }

		/// <summary>Starts a journey: the purse is reset, the records are kept.</summary>
		public void BeginJourney()
		{
			Coins = StartingCoins;
			WavesCleared = 0;
			Plays++;
		}

		public bool CanAfford(int cost) => Coins >= cost;

		/// <summary>Pays for a new guard. False when the purse is too small.</summary>
		public bool TryBuyGuard()
		{
			if (Coins < GuardStats.BuyCost) return false;
			Coins -= GuardStats.BuyCost;
			return true;
		}

		/// <summary>Pays for upgrading a guard that is at <paramref name="level"/>. False when it is maxed or too dear.</summary>
		public bool TryUpgradeGuard(int level)
		{
			if (level < 1 || level >= GuardStats.MaxLevel || Coins < GuardStats.UpgradeCost) return false;
			Coins -= GuardStats.UpgradeCost;
			return true;
		}

		public void Earn(int coins) => Coins += Math.Max(0, coins);

		/// <summary>A wave was cleared: the bonus is paid and the record is updated.</summary>
		public void ClearWave()
		{
			WavesCleared++;
			Coins += Waves.ClearBonus;
			BestWave = Math.Max(BestWave, WavesCleared);
		}

		/// <summary>The caravan arrived with <paramref name="healthFraction"/> of its health.</summary>
		public void Win(float healthFraction)
		{
			Wins++;
			BestHealthPercent = Math.Max(BestHealthPercent, Mathf.RoundToInt(Mathf.Clamp01(healthFraction) * 100f));
		}

		public CaravanSave ToSave() => new CaravanSave { plays = Plays, wins = Wins, bestWave = BestWave, bestHealthPercent = BestHealthPercent };

		public void Load(CaravanSave save)
		{
			if (save == null) return;
			Plays = Math.Max(0, save.plays);
			Wins = Math.Max(0, save.wins);
			BestWave = Mathf.Clamp(save.bestWave, 0, Waves.Count);
			BestHealthPercent = Mathf.Clamp(save.bestHealthPercent, 0, 100);
		}
	}

	/// <summary>Reads and writes the <see cref="CaravanSave"/> in the game's save slot.</summary>
	public sealed class CaravanSaveStore
	{
		const string Slot = "default";
		const int PayloadVersion = 1;

		readonly SaveStore _store;

		public CaravanSaveStore(string rootDirectory = null)
		{
			_store = new SaveStore("caravan-guard", rootDirectory);
		}

		/// <summary>The saved game, or null when there is none yet or it cannot be read.</summary>
		public CaravanSave Load()
		{
			var result = _store.Load<CaravanSave>(Slot, PayloadVersion);
			return result.Ok ? result.Value : null;
		}

		public void Save(CaravanSave save) => _store.Save(Slot, save, PayloadVersion);
	}
}
