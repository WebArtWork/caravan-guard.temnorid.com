using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Temnorid.CaravanGuard.Tests
{
	public class CaravanRulesTests
	{
		[Test]
		public void A_journey_starts_with_the_starting_purse()
		{
			var session = new CaravanSession();
			session.BeginJourney();
			Assert.AreEqual(CaravanSession.StartingCoins, session.Coins);
			Assert.AreEqual(0, session.WavesCleared);
			Assert.AreEqual(1, session.Plays);
		}

		[Test]
		public void Buying_a_guard_costs_coins_and_fails_when_the_purse_is_too_small()
		{
			var session = new CaravanSession();
			session.BeginJourney();
			var bought = 0;
			while (session.TryBuyGuard()) bought++;
			Assert.AreEqual(CaravanSession.StartingCoins / GuardStats.BuyCost, bought);
			Assert.AreEqual(CaravanSession.StartingCoins - bought * GuardStats.BuyCost, session.Coins);
			Assert.Less(session.Coins, GuardStats.BuyCost, "the purse is too small for one more");
		}

		[Test]
		public void Upgrading_costs_coins_and_only_works_from_level_one()
		{
			var session = new CaravanSession();
			session.BeginJourney();
			session.Earn(100);
			Assert.IsFalse(session.TryUpgradeGuard(0), "an empty pad cannot be upgraded");
			Assert.IsTrue(session.TryUpgradeGuard(1));
			Assert.AreEqual(CaravanSession.StartingCoins + 100 - GuardStats.UpgradeCost, session.Coins);
			Assert.IsFalse(session.TryUpgradeGuard(GuardStats.MaxLevel), "a fully upgraded guard stays as it is");
		}

		[Test]
		public void Clearing_a_wave_pays_a_bonus_and_updates_the_best_wave()
		{
			var session = new CaravanSession();
			session.BeginJourney();
			session.ClearWave();
			session.ClearWave();
			Assert.AreEqual(CaravanSession.StartingCoins + 2 * Waves.ClearBonus, session.Coins);
			Assert.AreEqual(2, session.WavesCleared);
			Assert.AreEqual(2, session.BestWave);

			session.BeginJourney();
			session.ClearWave();
			Assert.AreEqual(2, session.BestWave, "the best wave of an earlier journey stays");
		}

		[Test]
		public void Upgraded_guards_hit_harder_reach_farther_and_shoot_faster()
		{
			Assert.Greater(GuardStats.Damage(2), GuardStats.Damage(1));
			Assert.Greater(GuardStats.Range(2), GuardStats.Range(1));
			Assert.Less(GuardStats.Interval(2), GuardStats.Interval(1));
		}

		[Test]
		public void Waves_get_bigger_and_bring_new_attackers()
		{
			Assert.AreEqual(3, Waves.Count);
			Assert.Less(Waves.Plan(0).Count, Waves.Plan(1).Count);
			Assert.Less(Waves.Plan(1).Count, Waves.Plan(2).Count);
			Assert.AreEqual(0, Waves.Plan(0).Wolves);
			Assert.Greater(Waves.Plan(2).Riders, 0);
		}

		[Test]
		public void Every_attacker_kind_has_stats()
		{
			foreach (EnemyKind kind in System.Enum.GetValues(typeof(EnemyKind)))
			{
				var stats = Enemies.For(kind);
				Assert.Greater(stats.Health, 0f, kind.ToString());
				Assert.Greater(stats.Speed, 0f, kind.ToString());
				Assert.Greater(stats.Reward, 0, kind.ToString());
				Assert.IsNotEmpty(stats.AssetId, kind.ToString());
			}
		}

		[Test]
		public void Records_survive_a_save_and_load()
		{
			var folder = Path.Combine(Path.GetTempPath(), "caravan-save-" + System.Guid.NewGuid());
			try
			{
				var session = new CaravanSession();
				session.BeginJourney();
				session.ClearWave();
				session.ClearWave();
				session.ClearWave();
				session.Win(0.62f);
				new CaravanSaveStore(folder).Save(session.ToSave());

				var loaded = new CaravanSession();
				loaded.Load(new CaravanSaveStore(folder).Load());
				Assert.AreEqual(1, loaded.Plays);
				Assert.AreEqual(1, loaded.Wins);
				Assert.AreEqual(3, loaded.BestWave);
				Assert.AreEqual(62, loaded.BestHealthPercent);
			}
			finally
			{
				if (Directory.Exists(folder)) Directory.Delete(folder, true);
			}
		}

		[Test]
		public void A_missing_save_loads_as_nothing()
		{
			var folder = Path.Combine(Path.GetTempPath(), "caravan-save-" + System.Guid.NewGuid());
			Assert.IsNull(new CaravanSaveStore(folder).Load());
			var session = new CaravanSession();
			session.Load(null);
			Assert.AreEqual(0, session.Plays);
		}
	}

	public class RoadPathTests
	{
		static RoadPath Corner() => new RoadPath(new[] { new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f) });

		[Test]
		public void The_length_is_the_sum_of_the_segments()
		{
			Assert.AreEqual(20f, Corner().Length, 0.001f);
		}

		[Test]
		public void A_distance_gives_a_point_on_the_road_and_clamps_at_the_ends()
		{
			var road = Corner();
			Assert.AreEqual(new Vector3(4f, 0f, 0f), road.PointAt(4f));
			Assert.AreEqual(new Vector3(10f, 0f, 3f), road.PointAt(13f));
			Assert.AreEqual(new Vector3(0f, 0f, 0f), road.PointAt(-5f));
			Assert.AreEqual(new Vector3(10f, 0f, 10f), road.PointAt(99f));
		}

		[Test]
		public void The_direction_follows_the_segment()
		{
			var road = Corner();
			Assert.AreEqual(Vector3.right, road.DirectionAt(2f));
			Assert.AreEqual(Vector3.forward, road.DirectionAt(15f));
		}

		[Test]
		public void A_spot_beside_the_road_projects_to_its_distance()
		{
			var road = Corner();
			Assert.AreEqual(6f, road.Project(new Vector3(6f, 0f, -3f)), 0.001f);
			Assert.AreEqual(14f, road.Project(new Vector3(13f, 0f, 4f)), 0.001f);
		}

		[Test]
		public void A_road_needs_two_points()
		{
			Assert.Throws<System.ArgumentException>(() => new RoadPath(new[] { Vector3.zero }));
		}
	}
}
