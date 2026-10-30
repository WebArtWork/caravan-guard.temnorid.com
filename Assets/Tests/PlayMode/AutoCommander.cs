using System.Collections.Generic;
using UnityEngine;

namespace Temnorid.CaravanGuard.Tests
{
	/// <summary>
	/// A stand-in player for tests and balance runs: it spends coins on guards for the stretch of road the
	/// caravan is about to cover (new guards first, then upgrades) and starts each wave.
	/// </summary>
	public sealed class AutoCommander
	{
		readonly CaravanGame _game;

		public bool Active = true;

		public AutoCommander(CaravanGame game) => _game = game;

		public void Think()
		{
			if (!Active) return;
			if (_game.Phase != CaravanPhase.Planning && _game.Phase != CaravanPhase.Wave) return;

			var from = _game.Caravan.Progress - 12f;
			var to = (_game.Phase == CaravanPhase.Planning ? _game.StopAfter(_game.Session.WavesCleared) : _game.Caravan.Stop) + 12f;
			var near = new List<GuardPost>();
			foreach (var pad in _game.Pads)
			{
				var along = _game.Road.Project(pad.transform.position);
				if (along >= from && along <= to) near.Add(pad);
			}
			near.Sort((a, b) => Distance(a).CompareTo(Distance(b)));

			// Buy on empty pads first, then upgrade, as long as the purse allows.
			foreach (var pad in near)
				if (pad.IsEmpty && _game.Session.CanAfford(GuardStats.BuyCost)) _game.Buy(pad);
			foreach (var pad in near)
				if (pad.CanUpgrade && _game.Session.CanAfford(GuardStats.UpgradeCost)) _game.Upgrade(pad);

			if (_game.Phase == CaravanPhase.Planning) _game.StartWave();
		}

		// The caravan waits at its stop until the wave is dead, so that is where the fight is: guard it first.
		float Distance(GuardPost pad)
		{
			var stop = _game.Phase == CaravanPhase.Planning ? _game.StopAfter(_game.Session.WavesCleared) : _game.Caravan.Stop;
			return Mathf.Abs(_game.Road.Project(pad.transform.position) - stop);
		}
	}
}
