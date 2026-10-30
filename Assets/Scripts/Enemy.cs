using System;
using System.Collections;
using System.Collections.Generic;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Feedback;
using UnityEngine;

namespace Temnorid.CaravanGuard
{
	/// <summary>
	/// An attacker. It walks in a straight line over the open ground to the nearest wagon and strikes it
	/// until it dies; the guards shoot it on the way. Hit points are the shared <see cref="Health"/>.
	/// </summary>
	[RequireComponent(typeof(Health))]
	public sealed class Enemy : MonoBehaviour
	{
		public static readonly List<Enemy> All = new List<Enemy>();

		/// <summary>Raised when an attacker dies.</summary>
		public static event Action<Enemy> Killed;

		public EnemyKind Kind;
		public Health Health { get; private set; }
		public EnemyStats Stats { get; private set; }

		float _nextStrike;

		void Awake()
		{
			Health = GetComponent<Health>();
			Stats = Enemies.For(Kind);
			Health.Died += OnDied;
		}

		void OnEnable() => All.Add(this);
		void OnDisable() => All.Remove(this);

		/// <summary>Sets what kind of attacker this is. Call right after adding the component.</summary>
		public void Setup(EnemyKind kind)
		{
			Kind = kind;
			Stats = Enemies.For(kind);
			Health.MaxHealth = Stats.Health;
			Health.Team = 2;
			Health.Restore();
			_nextStrike = Time.time + Stats.Interval * UnityEngine.Random.Range(0.3f, 1f);
		}

		void Update()
		{
			if (!Health.IsAlive) return;
			var caravan = CaravanGame.Instance != null ? CaravanGame.Instance.Caravan : null;
			if (caravan == null || !caravan.Health.IsAlive) return;

			var goal = caravan.NearestWagon(transform.position);
			var flat = goal - transform.position;
			flat.y = 0f;
			var distance = flat.magnitude;
			if (distance > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), 10f * Time.deltaTime);

			if (distance > Stats.Reach)
			{
				transform.position += flat / distance * (Stats.Speed * Time.deltaTime);
				return;
			}
			if (Time.time < _nextStrike) return;
			_nextStrike = Time.time + Stats.Interval;
			caravan.Health.ApplyDamage(new DamageInfo(Stats.Damage, gameObject, goal, "strike"));
			Fx.Pulse(PrimitiveType.Sphere, goal + Vector3.up * 1.2f, Quaternion.identity, Vector3.one * 0.2f, Vector3.one * 0.9f, new Color(1f, 0.45f, 0.2f), 0.15f);
		}

		void OnDied(Health health, DamageInfo info)
		{
			All.Remove(this);
			foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
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
	}
}
