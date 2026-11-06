using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Feedback;
using ITKamianets.Engine.Net;
using UnityEngine;

namespace Temnorid.CaravanGuard
{
	/// <summary>
	/// A fixed pad beside the road. Empty, it can be bought into a guard post; built, its archer shoots the
	/// nearest attacker in range; upgraded, it hits harder and reaches farther.
	/// </summary>
	public sealed class GuardPost : MonoBehaviour
	{
		/// <summary>0 is an empty pad, 1 a guard post, 2 an upgraded one.</summary>
		public int Level { get; private set; }

		/// <summary>The marker shown on an empty pad.</summary>
		public GameObject EmptyMarker;
		/// <summary>The built look of level 1 and level 2.</summary>
		public GameObject[] LevelVisuals;
		public GameObject Highlight;
		/// <summary>Where arrows leave the tower.</summary>
		public float MuzzleHeight = 3.4f;

		public bool IsEmpty => Level == 0;
		public bool CanUpgrade => Level >= 1 && Level < GuardStats.MaxLevel;
		public float Range => Level > 0 ? GuardStats.Range(Level) : 0f;

		float _nextShot;

		void Awake()
		{
			SetLevel(0);
			SetSelected(false);
		}

		public void SetLevel(int level)
		{
			Level = Mathf.Clamp(level, 0, GuardStats.MaxLevel);
			if (EmptyMarker != null) EmptyMarker.SetActive(Level == 0);
			if (LevelVisuals == null) return; // a post made in code is given its looks right after it is added
			for (var i = 0; i < LevelVisuals.Length; i++) LevelVisuals[i].SetActive(Level == i + 1);
		}

		public void SetSelected(bool selected)
		{
			if (Highlight != null) Highlight.SetActive(selected);
		}

		void Update()
		{
			if (Level == 0 || Time.time < _nextShot) return;
			var target = Nearest();
			if (target == null) return;

			_nextShot = Time.time + GuardStats.Interval(Level);
			var from = transform.position + Vector3.up * MuzzleHeight;
			var to = target.transform.position + Vector3.up * 0.9f;
			// Online, only the host's guards hurt anybody; a client's archers just shoot at what the host shows.
			if (NetSession.Instance.IsAuthority) target.ApplyDamage(new DamageInfo(GuardStats.Damage(Level), gameObject, to, "arrow"));
			Tracer(from, to);
		}

		/// <summary>The nearest attacker in range: an enemy of the waves, or a bandit of the trade road.</summary>
		Health Nearest()
		{
			Health best = null;
			var bestSqr = Range * Range;
			foreach (var enemy in Enemy.All) Consider(enemy.Health, enemy.transform, ref best, ref bestSqr);
			foreach (var bandit in Bandit.All) Consider(bandit.Health, bandit.transform, ref best, ref bestSqr);
			return best;
		}

		void Consider(Health health, Transform at, ref Health best, ref float bestSqr)
		{
			if (health == null || !health.IsAlive) return;
			var flat = at.position - transform.position;
			flat.y = 0f;
			var sqr = flat.sqrMagnitude;
			if (sqr > bestSqr) return;
			bestSqr = sqr;
			best = health;
		}

		static void Tracer(Vector3 from, Vector3 to)
		{
			var delta = to - from;
			var length = delta.magnitude;
			if (length < 0.1f) return;
			Fx.Pulse(PrimitiveType.Cube, from + delta * 0.5f, Quaternion.LookRotation(delta), new Vector3(0.07f, 0.07f, length), new Vector3(0.015f, 0.015f, length), new Color(1f, 0.9f, 0.5f), 0.14f);
		}
	}
}
