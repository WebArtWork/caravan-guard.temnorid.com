using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Temnorid.CaravanGuard.Tests
{
	/// <summary>
	/// Plays whole journeys with <see cref="AutoCommander"/> to check the numbers. Explicit: run it by name
	/// (<c>-testFilter Temnorid.CaravanGuard.Tests.BalanceTests.Bots_play_whole_journeys</c>); it writes
	/// <c>Builds/balance.txt</c>.
	/// </summary>
	public class BalanceTests
	{
		[UnityTest]
		[Explicit]
		[Timeout(1200000)]
		public IEnumerator Bots_play_whole_journeys()
		{
			var report = new StringBuilder();
			var wins = 0;
			const int Runs = 6;
			for (var run = 0; run < Runs; run++)
			{
				CaravanGame.SaveRootOverride = Path.Combine(Path.GetTempPath(), "caravan-balance-" + System.Guid.NewGuid());
				yield return SceneManager.LoadSceneAsync("Main");
				yield return null;
				var game = CaravanGame.Instance;
				game.SaveEnabled = false;
				game.Begin();
				Random.InitState(run * 17 + 3);
				Time.timeScale = 8f;
				var commander = new AutoCommander(game);

				var start = Time.realtimeSinceStartup;
				while (game.Phase != CaravanPhase.Victory && game.Phase != CaravanPhase.Defeat && Time.realtimeSinceStartup - start < 300f)
				{
					commander.Think();
					yield return null;
				}
				var posts = 0;
				var upgraded = 0;
				foreach (var pad in game.Pads)
				{
					if (!pad.IsEmpty) posts++;
					if (pad.Level == GuardStats.MaxLevel) upgraded++;
				}
				if (game.Phase == CaravanPhase.Victory) wins++;
				else
				{
					report.AppendLine($"  lost at progress {game.Caravan.Progress:0} of stop {game.Caravan.Stop:0}, {Enemy.All.Count} attackers alive:");
					foreach (var enemy in Enemy.All)
					{
						var nearest = float.MaxValue;
						foreach (var pad in game.Pads)
							if (!pad.IsEmpty) nearest = Mathf.Min(nearest, Vector3.Distance(pad.transform.position, enemy.transform.position));
						report.AppendLine($"    {enemy.Kind} hp {enemy.Health.Current:0}/{enemy.Health.MaxHealth:0} at road {game.Road.Project(enemy.transform.position):0}, nearest guard {nearest:0} m");
					}
					foreach (var pad in game.Pads)
						if (!pad.IsEmpty) report.AppendLine($"    post {pad.name} L{pad.Level} at road {game.Road.Project(pad.transform.position):0}");
				}
				report.AppendLine($"run {run}: {game.Phase}, waves cleared {game.Session.WavesCleared}, caravan {game.Caravan.Health.Fraction:P0}, posts {posts} (upgraded {upgraded}), coins left {game.Session.Coins}, {game.JourneyTime:0}s");
			}
			report.AppendLine($"won {wins} of {Runs}");
			Directory.CreateDirectory("Builds");
			File.WriteAllText("Builds/balance.txt", report.ToString());
			Time.timeScale = 1f;
			CaravanGame.SaveRootOverride = null;
			Assert.Pass(report.ToString());
		}
	}
}
