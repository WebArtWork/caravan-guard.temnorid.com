using ITKamianets.Engine.Combat;
using UnityEngine;

namespace Temnorid.CaravanGuard
{
	/// <summary>
	/// The caravan: a few wagons that follow the road one behind the other and share one pool of hit points.
	/// <see cref="Progress"/> is how far the lead wagon is along the road in metres.
	/// </summary>
	[RequireComponent(typeof(Health))]
	public sealed class Caravan : MonoBehaviour
	{
		public Transform[] Wagons;
		public float Spacing = 4.6f;
		public float Speed = 2.0f;

		Health _health;

		/// <summary>The caravan's hit points. Fetched on first use, because the game wakes up before the caravan does.</summary>
		public Health Health => _health != null ? _health : _health = GetComponent<Health>();

		public RoadPath Road { get; private set; }
		public float Progress { get; private set; }

		/// <summary>The most distant point of the road the caravan may reach at the moment.</summary>
		public float Stop { get; set; }

		public bool AtStop => Progress >= Stop - 0.01f;

		/// <summary>Puts the caravan on the road with the lead wagon at <paramref name="progress"/> metres.</summary>
		public void Place(RoadPath road, float progress)
		{
			Road = road;
			Progress = progress;
			Stop = progress;
			Layout();
		}

		/// <summary>Online client: shows the host's caravan at <paramref name="progress"/> metres, stopping at <paramref name="stop"/>.</summary>
		public void Show(float progress, float stop)
		{
			if (Road == null) return;
			Progress = progress;
			Stop = stop;
			Layout();
		}

		/// <summary>Moves the lead wagon along the road toward <see cref="Stop"/>.</summary>
		public void Advance(float deltaTime)
		{
			if (Road == null || !Health.IsAlive) return;
			Progress = Mathf.MoveTowards(Progress, Stop, Speed * deltaTime);
			Layout();
		}

		void Layout()
		{
			for (var i = 0; i < Wagons.Length; i++)
			{
				var distance = Mathf.Max(0f, Progress - i * Spacing);
				var wagon = Wagons[i];
				wagon.position = Road.PointAt(distance);
				var direction = Road.DirectionAt(distance);
				if (direction.sqrMagnitude > 0.001f) wagon.rotation = Quaternion.LookRotation(direction);
			}
			transform.position = Road.PointAt(Progress);
		}

		/// <summary>The wagon position closest to <paramref name="from"/>.</summary>
		public Vector3 NearestWagon(Vector3 from)
		{
			var best = Wagons[0].position;
			var bestSqr = float.MaxValue;
			foreach (var wagon in Wagons)
			{
				var sqr = (wagon.position - from).sqrMagnitude;
				if (sqr < bestSqr)
				{
					bestSqr = sqr;
					best = wagon.position;
				}
			}
			return best;
		}
	}
}
