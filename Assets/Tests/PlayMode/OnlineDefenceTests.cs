using System;
using System.Collections;
using System.IO;
using ITKamianets.Engine.Net;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Temnorid.CaravanGuard.Tests
{
	/// <summary>
	/// The real map with this machine as the host and a second defender (a detached session in the same process) as the guest:
	/// the guest's pointer is shown on the host, a placement request is checked and applied by the host and comes back as the
	/// published state, a refused request is answered, and a started wave's attackers and caravan reach the guest as entities.
	/// </summary>
	public class OnlineDefenceTests
	{
		const ushort Port = 7802;

		NetSession _guest;
		string _saveFolder;

		static IEnumerator Until(Func<bool> condition, string what, float seconds = 15f)
		{
			var end = Time.realtimeSinceStartup + seconds;
			while (!condition())
			{
				if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what);
				yield return null;
			}
		}

		[SetUp]
		public void SetUp()
		{
			_saveFolder = Path.Combine(Path.GetTempPath(), "caravan-online-" + Guid.NewGuid());
			CaravanGame.SaveRootOverride = _saveFolder;
		}

		[TearDown]
		public void TearDown()
		{
			CaravanGame.SaveRootOverride = null;
			Time.timeScale = 1f;
			if (_guest != null) { _guest.Leave(); UnityEngine.Object.Destroy(_guest.gameObject); }
			NetSession.Instance.Leave();
			if (Directory.Exists(_saveFolder)) Directory.Delete(_saveFolder, true);
		}

		[UnityTest]
		[Timeout(180000)]
		public IEnumerator A_guest_defends_with_the_host_through_requests_and_published_state()
		{
			var host = NetSession.Instance;
			host.Leave();
			host.GameId = "caravan-guard";
			host.GameVersion = Application.version;
			host.LocalName = "Hosty";
			host.MaxPlayers = 4;
			Assert.IsTrue(host.HostDirect(Port), host.LastError);

			_guest = NetSession.CreateDetached();
			_guest.GameId = "caravan-guard";
			_guest.GameVersion = Application.version;
			_guest.LocalName = "Guesty";
			_guest.LocalColour = 2;
			Assert.IsTrue(_guest.JoinDirect("127.0.0.1:" + Port), _guest.LastError);
			yield return Until(() => host.Players.Count == 2 && _guest.Players.Count == 2 && !_guest.Connecting, "the guest to join");

			string told = null;
			_guest.On(CaravanNet.MessageEvent, (who, text) => told = text);

			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;
			yield return null;

			var game = CaravanGame.Instance;
			game.SaveEnabled = false;
			Assert.IsNotNull(game.Net, "an online journey has the link to other players");
			Assert.IsFalse(game.IsClient);
			game.Begin();
			Assert.AreEqual(CaravanPhase.Planning, game.Phase);

			// The host's state reaches the guest: planning, and the shared coins.
			yield return Until(() => _guest.Entities.ContainsKey(CaravanNet.StateId) && (_guest.Entities[CaravanNet.StateId].State & 0xF) == (int)CaravanPhase.Planning, "the guest to get the state");
			Assert.AreEqual(CaravanSession.StartingCoins, _guest.Entities[CaravanNet.StateId].Value, 0.01f);
			Assert.AreEqual((int)CaravanPhase.Planning, _guest.Entities[CaravanNet.StateId].State & 0xF);

			// The guest's pointer is shown on the host where the guest says.
			var target = new Vector3(12f, 0f, -7f);
			for (var i = 0; i < 6; i++)
			{
				_guest.PublishPose(new NetPose { Position = target, State = 1 });
				yield return new WaitForSeconds(0.1f);
			}
			yield return Until(() => game.Net.CursorOf(_guest.LocalId) != null && (game.Net.CursorOf(_guest.LocalId).position - target).magnitude < 0.5f, "the guest's pointer to be shown");
			Assert.IsTrue(game.Net.CursorOf(_guest.LocalId).gameObject.activeSelf);

			// The guest asks for a guard: the host checks the cost and the spot, applies it, and everybody sees the result.
			_guest.Send(CaravanNet.BuyEvent, "0", true);
			yield return Until(() => game.Pads[0].Level == 1, "the host to place the guest's guard");
			Assert.AreEqual(CaravanSession.StartingCoins - GuardStats.BuyCost, game.Session.Coins);
			yield return Until(() => _guest.Entities.TryGetValue(CaravanNet.PadBase, out var pad) && pad.State == 1, "the guest to see the guard");
			yield return Until(() => _guest.Entities[CaravanNet.StateId].Value == CaravanSession.StartingCoins - GuardStats.BuyCost, "the guest to see the coins drop");
			yield return Until(() => told != null && told.Contains("Guesty"), "the guest to be told who placed it");

			// A request the rules refuse (the post is taken) changes nothing and is answered to the guest.
			told = null;
			_guest.Send(CaravanNet.BuyEvent, "0", true);
			yield return Until(() => told != null, "the refusal");
			Assert.AreEqual(1, game.Pads[0].Level);
			Assert.AreEqual(CaravanSession.StartingCoins - GuardStats.BuyCost, game.Session.Coins);

			// The guest asks for the wave: the attackers and the moving caravan are published to the guest.
			_guest.Send(CaravanNet.WaveEvent, "", true);
			yield return Until(() => game.Phase == CaravanPhase.Wave, "the host to start the wave");
			yield return Until(() => HasEnemy(_guest), "the attackers to reach the guest");
			var start = _guest.Entities[CaravanNet.CaravanId].Yaw;
			yield return Until(() => _guest.Entities[CaravanNet.CaravanId].Yaw > start + 0.5f, "the caravan to move on the guest");
			Assert.AreEqual((int)CaravanPhase.Wave, _guest.Entities[CaravanNet.StateId].State & 0xF);
		}

		static bool HasEnemy(NetSession session)
		{
			foreach (var id in session.Entities.Keys) if (id >= CaravanNet.EnemyBase) return true;
			return false;
		}
	}
}
