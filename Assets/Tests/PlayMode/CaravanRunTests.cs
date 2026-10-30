using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Temnorid.CaravanGuard.Tests
{
	public class CaravanRunTests
	{
		string _saveFolder;

		[SetUp]
		public void SetUp()
		{
			_saveFolder = Path.Combine(Path.GetTempPath(), "caravan-playmode-" + System.Guid.NewGuid());
			CaravanGame.SaveRootOverride = _saveFolder;
		}

		[TearDown]
		public void TearDown()
		{
			CaravanGame.SaveRootOverride = null;
			Time.timeScale = 1f;
			if (Directory.Exists(_saveFolder)) Directory.Delete(_saveFolder, true);
		}

		static IEnumerator Load()
		{
			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;
			yield return null;
		}

		[UnityTest]
		public IEnumerator The_map_has_a_road_posts_spawns_and_wagons_and_waits_on_the_start_screen()
		{
			yield return Load();
			var game = CaravanGame.Instance;
			game.SaveEnabled = false;

			Assert.AreEqual(CaravanPhase.Menu, game.Phase);
			Assert.Greater(game.Road.Length, 100f);
			Assert.AreEqual(10, game.Pads.Length);
			Assert.AreEqual(3, game.Caravan.Wagons.Length);
			Assert.GreaterOrEqual(game.Spawns.Length, 6);
			foreach (var pad in game.Pads) Assert.IsTrue(pad.IsEmpty, pad.name + " starts empty");
			Assert.AreEqual(game.StartProgress, game.Caravan.Progress, 0.01f);
		}

		[UnityTest]
		public IEnumerator Buying_and_upgrading_guards_costs_coins_and_changes_the_post()
		{
			yield return Load();
			var game = CaravanGame.Instance;
			game.SaveEnabled = false;
			game.Begin();

			var pad = game.Pads[0];
			Assert.IsTrue(game.Buy(pad));
			Assert.AreEqual(1, pad.Level);
			Assert.AreEqual(CaravanSession.StartingCoins - GuardStats.BuyCost, game.Session.Coins);
			Assert.IsFalse(game.Buy(pad), "a post cannot be bought twice");

			Assert.IsTrue(game.Upgrade(pad));
			Assert.AreEqual(2, pad.Level);
			Assert.IsFalse(game.Upgrade(pad), "level two is the last");
		}

		[UnityTest]
		public IEnumerator Clicking_a_post_selects_it_and_clicking_the_ground_does_not()
		{
			yield return Load();
			var game = CaravanGame.Instance;
			game.SaveEnabled = false;
			game.Begin();
			yield return null;

			var camera = Camera.main;
			var post = game.Pads[0];
			var onPost = camera.WorldToScreenPoint(post.transform.position + Vector3.up * 1f);
			Assert.IsTrue(game.TryPick(onPost), "a click on the post picks it");
			Assert.AreSame(post, game.Selected);

			var onGround = camera.WorldToScreenPoint(game.Road.PointAt(game.Road.Length * 0.5f));
			Assert.IsFalse(game.TryPick(onGround), "a click on the road picks nothing");
			Assert.AreSame(post, game.Selected, "the selection stays when nothing is picked");

			Assert.IsTrue(game.BuyOrUpgrade(), "the selected empty post is bought");
			Assert.AreEqual(1, post.Level);
		}

		[UnityTest]
		[Timeout(180000)]
		public IEnumerator A_guard_shoots_an_attacker_in_range_and_the_kill_pays()
		{
			yield return Load();
			var game = CaravanGame.Instance;
			game.SaveEnabled = false;
			game.Begin();
			game.Buy(game.Pads[0]);

			var coins = game.Session.Coins;
			var enemy = EnemyFactory.Create(EnemyKind.Raider, game.Pads[0].transform.position + new Vector3(6f, 0f, 0f), game.EnemyRoot);
			var start = Time.time;
			while (enemy != null && enemy.Health.IsAlive && Time.time - start < 20f) yield return null;

			Assert.IsFalse(enemy != null && enemy.Health.IsAlive, "the guard killed the raider");
			Assert.AreEqual(coins + Enemies.For(EnemyKind.Raider).Reward, game.Session.Coins);
		}

		[UnityTest]
		[Timeout(240000)]
		public IEnumerator With_no_guards_the_caravan_is_lost()
		{
			yield return Load();
			var game = CaravanGame.Instance;
			game.SaveEnabled = false;
			game.Begin();
			Time.timeScale = 6f;
			game.StartWave();

			var start = Time.realtimeSinceStartup;
			while (game.Phase == CaravanPhase.Wave && Time.realtimeSinceStartup - start < 200f) yield return null;

			Assert.AreEqual(CaravanPhase.Defeat, game.Phase);
			Assert.IsFalse(game.Caravan.Health.IsAlive);
		}

		[UnityTest]
		[Timeout(300000)]
		public IEnumerator A_sensible_player_brings_the_caravan_home_and_the_win_is_saved()
		{
			yield return Load();
			var game = CaravanGame.Instance;
			game.SaveEnabled = true;
			game.Begin();
			Time.timeScale = 6f;
			var commander = new AutoCommander(game);

			var start = Time.realtimeSinceStartup;
			while (game.Phase != CaravanPhase.Victory && game.Phase != CaravanPhase.Defeat && Time.realtimeSinceStartup - start < 250f)
			{
				commander.Think();
				yield return null;
			}

			Assert.AreEqual(CaravanPhase.Victory, game.Phase, "the caravan arrived");
			Assert.AreEqual(Waves.Count, game.Session.WavesCleared);
			Assert.AreEqual(game.Road.Length, game.Caravan.Progress, 0.1f);

			var saved = new CaravanSaveStore(_saveFolder).Load();
			Assert.IsNotNull(saved);
			Assert.AreEqual(1, saved.wins);
			Assert.AreEqual(Waves.Count, saved.bestWave);
		}
	}
}
