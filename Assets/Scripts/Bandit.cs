using System;
using System.Collections;
using System.Collections.Generic;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Feedback;
using UnityEngine;

namespace Temnorid.CaravanGuard
{
	/// <summary>
	/// A bandit. It comes out of the wild beside the road, walks to the nearest trader and strikes it until one of them dies; the guard posts
	/// shoot it on the way. With no trader left to rob it gives up and slips away.
	/// </summary>
	[RequireComponent(typeof(Health))]
	public sealed class Bandit : MonoBehaviour
	{
		public static readonly List<Bandit> All = new List<Bandit>();

		/// <summary>A bandit was killed (by the guards).</summary>
		public static event Action<Bandit> Killed;

		public const float Speed = 3.4f;
		public const float Damage = 5f;
		public const float Interval = 1.2f;
		public const float Reach = 2.2f;
		public const int Reward = 8;
		const float GiveUpSeconds = 10f;

		public Health Health { get; private set; }

		float _nextStrike;
		float _idle;
		bool _gone;

		public static Bandit Create(Vector3 position, Transform parent)
		{
			var root = new GameObject("Bandit");
			if (parent != null) root.transform.SetParent(parent, false);
			root.transform.position = position;
			root.AddComponent<Health>();
			var bandit = root.AddComponent<Bandit>();
			bandit.Health.MaxHealth = 38f;
			bandit.Health.Team = 2;
			bandit.Health.Restore();
			bandit._nextStrike = Time.time + Interval * UnityEngine.Random.Range(0.3f, 1f);

			var cloth = Fx.Material(new Color(0.22f, 0.14f, 0.14f));
			var skin = Fx.Material(new Color(0.8f, 0.6f, 0.46f));
			var steel = Fx.Material(new Color(0.7f, 0.72f, 0.78f));
			var red = Fx.Material(new Color(0.65f, 0.1f, 0.1f));
			var t = root.transform;
			Piece(PrimitiveType.Capsule, "Body", t, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), cloth);
			Piece(PrimitiveType.Sphere, "Head", t, new Vector3(0f, 1.7f, 0.02f), new Vector3(0.38f, 0.38f, 0.38f), skin);
			Piece(PrimitiveType.Cube, "Mask", t, new Vector3(0f, 1.66f, 0.2f), new Vector3(0.4f, 0.14f, 0.05f), red);
			Piece(PrimitiveType.Cube, "Sabre", t, new Vector3(0.5f, 1.0f, 0.4f), new Vector3(0.07f, 0.07f, 0.9f), steel);
			var bar = root.AddComponent<HealthBar3D>();
			bar.Height = 2.2f;
			bar.Width = 0.9f;
			return bandit;
		}

		void Awake() => Health = GetComponent<Health>();

		void Start() => Health.Died += OnDied;

		void OnEnable() => All.Add(this);

		void OnDisable() => All.Remove(this);

		void Update()
		{
			if (_gone || !Health.IsAlive) return;

			var target = NearestTrader();
			if (target == null)
			{
				_idle += Time.deltaTime;
				if (_idle >= GiveUpSeconds) Disappear();
				return;
			}
			_idle = 0f;

			var goal = target.transform.position;
			var flat = goal - transform.position;
			flat.y = 0f;
			var distance = flat.magnitude;
			if (distance > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), 10f * Time.deltaTime);

			if (distance > Reach)
			{
				transform.position += flat / distance * (Speed * Time.deltaTime);
				return;
			}
			if (Time.time < _nextStrike) return;
			_nextStrike = Time.time + Interval;
			target.Health.ApplyDamage(new DamageInfo(Damage, gameObject, goal, "strike"));
			Fx.Pulse(PrimitiveType.Sphere, goal + Vector3.up * 1.2f, Quaternion.identity, Vector3.one * 0.2f, Vector3.one * 0.9f, new Color(1f, 0.45f, 0.2f), 0.15f);
		}

		Trader NearestTrader()
		{
			Trader best = null;
			var bestSqr = float.MaxValue;
			foreach (var trader in Trader.All)
			{
				if (!trader.IsAlive) continue;
				var sqr = (trader.transform.position - transform.position).sqrMagnitude;
				if (sqr < bestSqr)
				{
					bestSqr = sqr;
					best = trader;
				}
			}
			return best;
		}

		void Disappear()
		{
			_gone = true;
			All.Remove(this);
			Destroy(gameObject);
		}

		void OnDied(Health health, DamageInfo info)
		{
			_gone = true;
			All.Remove(this);
			Killed?.Invoke(this);
			StartCoroutine(Fall());
		}

		IEnumerator Fall()
		{
			var start = transform.rotation;
			var end = start * Quaternion.Euler(90f, 0f, 0f);
			for (var t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
			{
				transform.rotation = Quaternion.Slerp(start, end, t);
				yield return null;
			}
			yield return new WaitForSeconds(1.2f);
			Destroy(gameObject);
		}

		static void Piece(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
		{
			var piece = GameObject.CreatePrimitive(type);
			piece.name = name;
			Destroy(piece.GetComponent<Collider>());
			piece.transform.SetParent(parent, false);
			piece.transform.localPosition = position;
			piece.transform.localScale = scale;
			piece.GetComponent<Renderer>().sharedMaterial = material;
		}
	}
}
