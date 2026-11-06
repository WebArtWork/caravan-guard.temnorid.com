using System;
using System.Collections;
using System.Collections.Generic;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Feedback;
using UnityEngine;

namespace Temnorid.CaravanGuard
{
	/// <summary>
	/// A trader walking the road between the village and the town with a handcart of goods. If it gets to the other end the trade is done; if
	/// bandits catch it, it falls and the goods are lost.
	/// </summary>
	[RequireComponent(typeof(Health))]
	public sealed class Trader : MonoBehaviour
	{
		public static readonly List<Trader> All = new List<Trader>();

		/// <summary>A trader reached the end of the road alive.</summary>
		public static event Action<Trader> Arrived;

		/// <summary>Bandits killed a trader.</summary>
		public static event Action<Trader> Lost;

		public Health Health { get; private set; }

		RoadPath _road;
		float _distance;
		float _direction;
		float _speed;
		float _lane;
		bool _done;

		public bool IsAlive => !_done && Health != null && Health.IsAlive;

		/// <summary>Makes a trader at <paramref name="from"/> metres along the road, walking towards the far end (<paramref name="towardsTown"/>) or the village.</summary>
		public static Trader Create(RoadPath road, bool towardsTown, float from, Transform parent, System.Random random)
		{
			var root = new GameObject(towardsTown ? "Trader (to town)" : "Trader (to village)");
			if (parent != null) root.transform.SetParent(parent, false);
			root.AddComponent<Health>();
			var trader = root.AddComponent<Trader>();
			trader._road = road;
			trader._distance = from;
			trader._direction = towardsTown ? 1f : -1f;
			trader._speed = 2.3f + (float)random.NextDouble() * 0.8f;
			trader._lane = (towardsTown ? -1f : 1f) * (0.9f + (float)random.NextDouble() * 0.8f);
			trader.Health.MaxHealth = 50f;
			trader.Health.Team = 1;
			trader.Health.Restore();
			trader.Place();

			var coats = new[] { new Color(0.55f, 0.35f, 0.2f), new Color(0.25f, 0.4f, 0.55f), new Color(0.5f, 0.5f, 0.25f), new Color(0.45f, 0.25f, 0.4f) };
			Build(root.transform, coats[random.Next(coats.Length)]);
			var bar = root.AddComponent<HealthBar3D>();
			bar.Height = 2.3f;
			bar.Width = 0.9f;
			return trader;
		}

		void Awake() => Health = GetComponent<Health>();

		void OnEnable() => All.Add(this);

		void OnDisable() => All.Remove(this);

		void Start() => Health.Died += OnDied;

		void Update()
		{
			if (!IsAlive) return;
			_distance += _direction * _speed * Time.deltaTime;
			if (_distance >= _road.Length - 1f || _distance <= 1f)
			{
				_done = true;
				All.Remove(this);
				Arrived?.Invoke(this);
				Destroy(gameObject);
				return;
			}
			Place();
		}

		void Place()
		{
			var forward = _road.DirectionAt(_distance) * _direction;
			var right = Vector3.Cross(Vector3.up, forward);
			transform.position = _road.PointAt(_distance) + right * _lane;
			if (forward.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(forward);
		}

		void OnDied(Health health, DamageInfo info)
		{
			if (_done) return;
			_done = true;
			All.Remove(this);
			Lost?.Invoke(this);
			StartCoroutine(Fall());
		}

		IEnumerator Fall()
		{
			var start = transform.rotation;
			var end = start * Quaternion.Euler(0f, 0f, 85f);
			for (var t = 0f; t < 1f; t += Time.deltaTime / 0.4f)
			{
				transform.rotation = Quaternion.Slerp(start, end, t);
				yield return null;
			}
			yield return new WaitForSeconds(3f);
			Destroy(gameObject);
		}

		// A walker in front of a small handcart: a body, a head, a hat, a sack, two wheels.
		static void Build(Transform root, Color coat)
		{
			var skin = Fx.Material(new Color(0.85f, 0.67f, 0.52f));
			var cloth = Fx.Material(coat);
			var wood = Fx.Material(new Color(0.42f, 0.28f, 0.15f));
			var sack = Fx.Material(new Color(0.78f, 0.7f, 0.5f));
			var dark = Fx.Material(new Color(0.15f, 0.12f, 0.1f));

			Piece(PrimitiveType.Capsule, "Body", root, new Vector3(0f, 0.9f, 0.6f), new Vector3(0.65f, 0.9f, 0.65f), cloth);
			Piece(PrimitiveType.Sphere, "Head", root, new Vector3(0f, 1.7f, 0.62f), new Vector3(0.36f, 0.36f, 0.36f), skin);
			Piece(PrimitiveType.Cylinder, "Hat", root, new Vector3(0f, 1.95f, 0.62f), new Vector3(0.5f, 0.06f, 0.5f), wood);
			// The cart behind the walker.
			Piece(PrimitiveType.Cube, "Bed", root, new Vector3(0f, 0.65f, -0.9f), new Vector3(1.2f, 0.25f, 1.7f), wood);
			Piece(PrimitiveType.Cube, "Sack", root, new Vector3(-0.2f, 1.0f, -0.9f), new Vector3(0.6f, 0.5f, 0.7f), sack);
			Piece(PrimitiveType.Cube, "Crate", root, new Vector3(0.3f, 1.0f, -1.1f), new Vector3(0.5f, 0.45f, 0.5f), wood);
			Piece(PrimitiveType.Cube, "Shaft", root, new Vector3(0f, 0.7f, -0.0f), new Vector3(0.12f, 0.1f, 1.6f), wood);
			foreach (var x in new[] { -0.7f, 0.7f })
				Piece(PrimitiveType.Cylinder, "Wheel", root, new Vector3(x, 0.4f, -0.9f), new Vector3(0.8f, 0.07f, 0.8f), dark).localRotation = Quaternion.Euler(0f, 0f, 90f);
		}

		static Transform Piece(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
		{
			var piece = GameObject.CreatePrimitive(type);
			piece.name = name;
			Destroy(piece.GetComponent<Collider>());
			piece.transform.SetParent(parent, false);
			piece.transform.localPosition = position;
			piece.transform.localScale = scale;
			piece.GetComponent<Renderer>().sharedMaterial = material;
			return piece.transform;
		}
	}
}
