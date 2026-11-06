using System.Collections.Generic;
using ITKamianets.Engine.Core;
using ITKamianets.Engine.Feedback;
using ITKamianets.Engine.Input;
using ITKamianets.Engine.TopDown;
using Temnorid.Village;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Temnorid.CaravanGuard
{
	/// <summary>
	/// The living world of the game: the player's village on one side, the nearest town on the other, a road between them that traders walk all
	/// day. Now and then bandits come out of the wild and attack the traders; the player covers the road with guard posts (bought with the coins
	/// the trade and the dead bandits bring). The player is a commander seen from above and only buys and upgrades guards. This game is not linked to
	/// Lords yet: the village is the standard one.
	/// </summary>
	[DefaultExecutionOrder(-100)]
	public sealed class RealmGame : MonoBehaviour
	{
		public static RealmGame Instance { get; private set; }

		public OverheadCamera View;
		public VillageView Village;
		[Tooltip("The text material the town's name uses.")]
		public Material TextMaterial;

		const string Log = "CaravanGuard";
		const int TradeIncome = 4;
		const float PostSpacing = 34f;
		const float PostSide = 7f;

		public CaravanSession Session { get; private set; }
		public RoadPath Road { get; private set; }
		public GuardPost Selected { get; private set; }
		public int Delivered { get; private set; }
		public int Lost { get; private set; }
		public int Raids { get; private set; }
		public bool Ready { get; private set; }
		public IReadOnlyList<GuardPost> Posts => _posts;

		public string ToastText { get; private set; } = "";
		public float ToastUntil { get; private set; }

		readonly List<GuardPost> _posts = new List<GuardPost>();
		Transform _actors;
		readonly System.Random _random = new System.Random(5);
		float _nextTrader, _nextRaid;
		bool _fromVillage = true;

		void Awake()
		{
			Instance = this;
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = true; // W A S D pan the camera
			Session = new CaravanSession();
			Session.BeginJourney();
		}

		void Start()
		{
			// Our village, in the middle of the map. It is the standard village: this game is not linked to Lords yet.
			var session = VillageSource.CreateDefault(null, "Standard village.", SharedStock.SystemClock());
			var data = session.Data;
			if (data.WallLevel < 2) data.WallLevel = 2;
			Village.Build(data);
			foreach (var plot in Village.Plots) if (plot.Root != null) plot.Root.gameObject.SetActive(false); // the farm's fields lie where the road goes

			var rx = data.VillageRx;
			var townCentre = new Vector3(rx + 290f, 0f, 0f);
			var gateOfTown = townCentre + new Vector3(-TownBuilder.RadiusX, 0f, 0f);
			TownBuilder.Build(townCentre, TextMaterial);

			// The road: from the village's east gate to the town's west gate, with a few bends.
			Road = new RoadPath(new[]
			{
				new Vector3(rx - 4f, 0f, 0f),
				new Vector3(rx + 55f, 0f, -24f),
				new Vector3(rx + 125f, 0f, 18f),
				new Vector3(rx + 190f, 0f, -16f),
				new Vector3(gateOfTown.x + 4f, 0f, 0f),
			});
			BuildRoad();
			BuildPosts();

			_actors = new GameObject("Actors").transform;
			if (View != null)
			{
				View.Bounds = new Rect(-rx - 40f, -75f, rx + 40f + townCentre.x + TownBuilder.RadiusX + 60f, 150f);
				View.MinDistance = 18f;
				View.MaxDistance = 110f;
				View.Distance = 60f;
				View.Focus = Road.PointAt(60f);
				View.Snap();
			}

			// Traders are already on the road when the game opens.
			for (var i = 0; i < 4; i++) Trader.Create(Road, i % 2 == 0, Road.Length * (0.15f + 0.2f * i), _actors, _random);
			_nextTrader = Time.time + 6f;
			_nextRaid = Time.time + 22f;
			Ready = true;
			Toast("Traders walk the road between your village and the town. Guard it.");
			EngineLog.Info(Log, $"The world is ready: road {Road.Length:0} m, {_posts.Count} guard posts.");
		}

		void OnEnable()
		{
			Trader.Arrived += OnArrived;
			Trader.Lost += OnLost;
			Bandit.Killed += OnBanditKilled;
		}

		void OnDisable()
		{
			Trader.Arrived -= OnArrived;
			Trader.Lost -= OnLost;
			Bandit.Killed -= OnBanditKilled;
		}

		void OnDestroy()
		{
			if (Instance == this) Instance = null;
		}

		// ---- the world's make ----

		void BuildRoad()
		{
			var sand = Fx.Material(new Color(0.64f, 0.53f, 0.37f));
			var root = new GameObject("Road").transform;
			for (var i = 0; i < Road.Count; i++)
			{
				var point = Road.Point(i);
				Piece(PrimitiveType.Cylinder, "Joint", root, point + Vector3.up * 0.05f, new Vector3(6f, 0.05f, 6f), sand);
				if (i == Road.Count - 1) continue;
				var next = Road.Point(i + 1);
				var delta = next - point;
				var piece = Piece(PrimitiveType.Cube, "Stretch", root, (point + next) * 0.5f + Vector3.up * 0.04f, new Vector3(6f, 0.08f, delta.magnitude), sand);
				piece.rotation = Quaternion.LookRotation(delta);
			}
		}

		void BuildPosts()
		{
			var root = new GameObject("GuardPosts").transform;
			var number = 0;
			for (var d = 40f; d < Road.Length - 30f; d += PostSpacing)
			{
				var side = number % 2 == 0 ? 1f : -1f;
				var right = Vector3.Cross(Vector3.up, Road.DirectionAt(d));
				_posts.Add(GuardPostFactory.Create(root, Road.PointAt(d) + right * side * PostSide, ++number));
			}
		}

		// ---- the days ----

		void Update()
		{
			if (!Ready) return;

			View.EdgePan = !Application.isMobilePlatform && !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
			HandleClick();
			if (EngineInput.Default.Player.Interact.WasPressedThisFrame()) BuyOrUpgrade();

			if (Time.time >= _nextTrader && Trader.All.Count < 12)
			{
				_nextTrader = Time.time + 8f + (float)_random.NextDouble() * 5f;
				Trader.Create(Road, _fromVillage, _fromVillage ? 2f : Road.Length - 2f, _actors, _random);
				_fromVillage = !_fromVillage;
			}

			if (Time.time >= _nextRaid) Raid();
		}

		/// <summary>Bandits come out of the wild beside a stretch of the road.</summary>
		void Raid()
		{
			Raids++;
			_nextRaid = Time.time + 35f + (float)_random.NextDouble() * 20f;
			var count = Mathf.Min(8, 2 + Raids / 3);
			var along = Road.Length * (0.25f + 0.6f * (float)_random.NextDouble());
			var right = Vector3.Cross(Vector3.up, Road.DirectionAt(along));
			var side = _random.Next(2) == 0 ? 1f : -1f;
			var origin = Road.PointAt(along) + right * side * 34f;
			for (var i = 0; i < count; i++)
			{
				var offset = new Vector3(Random.Range(-5f, 5f), 0f, Random.Range(-5f, 5f));
				Bandit.Create(origin + offset, _actors);
			}
			var place = along < Road.Length * 0.4f ? "near the village" : along > Road.Length * 0.65f ? "near the town" : "in the middle of the road";
			Toast($"Bandits attack the traders {place}!");
			EngineLog.Info(Log, $"Raid {Raids}: {count} bandits at {along:0} m.");
		}

		void OnArrived(Trader trader)
		{
			Delivered++;
			Session.Earn(TradeIncome);
		}

		void OnLost(Trader trader)
		{
			Lost++;
			Toast("A trader was killed. The goods are lost.");
		}

		void OnBanditKilled(Bandit bandit)
		{
			Session.Earn(Bandit.Reward);
			Fx.Pulse(PrimitiveType.Sphere, bandit.transform.position + Vector3.up * 1.2f, Quaternion.identity, Vector3.one * 0.3f, Vector3.one * 0.05f, new Color(1f, 0.82f, 0.2f), 0.5f);
		}

		// ---- the guard posts ----

		void HandleClick()
		{
			var input = EngineInput.Default;
			if (!input.UI.Click.WasPressedThisFrame()) return;
			if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
			var camera = Camera.main;
			if (camera == null) return;
			var point = input.UI.Point.ReadValue<Vector2>();
			if (Physics.Raycast(camera.ScreenPointToRay(point), out var hit, 600f))
			{
				var post = hit.collider.GetComponentInParent<GuardPost>();
				if (post != null) { Select(post); return; }
			}
			Select(null);
		}

		public void Select(GuardPost post)
		{
			if (Selected != null) Selected.SetSelected(false);
			Selected = post;
			if (Selected != null) Selected.SetSelected(true);
		}

		/// <summary>Buys a guard on the selected empty post, or upgrades the selected guard.</summary>
		public bool BuyOrUpgrade()
		{
			if (Selected == null) return false;
			if (Selected.IsEmpty)
			{
				if (!Session.TryBuyGuard()) { Toast($"A guard costs {GuardStats.BuyCost} coins."); return false; }
				Selected.SetLevel(1);
				Toast("Guard placed.");
				return true;
			}
			if (!Selected.CanUpgrade) return false;
			if (!Session.TryUpgradeGuard(Selected.Level)) { Toast($"An upgrade costs {GuardStats.UpgradeCost} coins."); return false; }
			Selected.SetLevel(Selected.Level + 1);
			Toast("Guard upgraded.");
			return true;
		}

		public void Toast(string text)
		{
			ToastText = text;
			ToastUntil = Time.unscaledTime + 4f;
		}

		static Transform Piece(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
		{
			var piece = GameObject.CreatePrimitive(type);
			piece.name = name;
			Destroy(piece.GetComponent<Collider>());
			piece.transform.SetParent(parent, false);
			piece.transform.position = position;
			piece.transform.localScale = scale;
			piece.GetComponent<Renderer>().sharedMaterial = material;
			return piece.transform;
		}
	}

	/// <summary>Makes a guard post (an empty pad that can be bought into a tower with an archer) from primitives at run time.</summary>
	public static class GuardPostFactory
	{
		public static GuardPost Create(Transform parent, Vector3 position, int number)
		{
			var stoneDark = Fx.Material(new Color(0.42f, 0.41f, 0.44f));
			var marker = Fx.Material(new Color(0.85f, 0.75f, 0.35f));
			var wood = Fx.Material(new Color(0.45f, 0.3f, 0.16f));
			var stone = Fx.Material(new Color(0.55f, 0.54f, 0.56f));
			var cloth = Fx.Material(new Color(0.2f, 0.4f, 0.75f));
			var gold = Fx.Material(new Color(1f, 0.8f, 0.2f));
			var skin = Fx.Material(new Color(0.88f, 0.7f, 0.55f));
			var glow = Fx.Material(new Color(1f, 0.95f, 0.4f));

			var pad = new GameObject("Post_" + number);
			pad.transform.SetParent(parent, false);
			pad.transform.position = position;
			var collider = pad.AddComponent<CapsuleCollider>();
			collider.radius = 2.2f;
			collider.height = 5f;
			collider.center = new Vector3(0f, 2.5f, 0f);
			var post = pad.AddComponent<GuardPost>();

			var empty = new GameObject("Empty").transform;
			empty.SetParent(pad.transform, false);
			Part(PrimitiveType.Cylinder, "Disc", empty, new Vector3(0f, 0.08f, 0f), new Vector3(3.6f, 0.08f, 3.6f), stoneDark);
			Part(PrimitiveType.Cube, "MarkA", empty, new Vector3(0f, 0.18f, 0f), new Vector3(1.6f, 0.06f, 0.3f), marker);
			Part(PrimitiveType.Cube, "MarkB", empty, new Vector3(0f, 0.18f, 0f), new Vector3(0.3f, 0.06f, 1.6f), marker);

			var level1 = new GameObject("Level1").transform;
			level1.SetParent(pad.transform, false);
			Part(PrimitiveType.Cylinder, "Base", level1, new Vector3(0f, 0.08f, 0f), new Vector3(3.6f, 0.08f, 3.6f), stoneDark);
			Part(PrimitiveType.Cylinder, "Tower", level1, new Vector3(0f, 1.4f, 0f), new Vector3(1.8f, 1.4f, 1.8f), wood);
			Part(PrimitiveType.Cylinder, "Platform", level1, new Vector3(0f, 2.9f, 0f), new Vector3(2.5f, 0.1f, 2.5f), wood);
			Part(PrimitiveType.Capsule, "Archer", level1, new Vector3(0f, 3.7f, 0f), new Vector3(0.6f, 0.7f, 0.6f), cloth);
			Part(PrimitiveType.Sphere, "Head", level1, new Vector3(0f, 4.55f, 0f), new Vector3(0.36f, 0.36f, 0.36f), skin);

			var level2 = new GameObject("Level2").transform;
			level2.SetParent(pad.transform, false);
			Part(PrimitiveType.Cylinder, "Base", level2, new Vector3(0f, 0.08f, 0f), new Vector3(3.6f, 0.08f, 3.6f), stoneDark);
			Part(PrimitiveType.Cylinder, "Tower", level2, new Vector3(0f, 1.9f, 0f), new Vector3(2.0f, 1.9f, 2.0f), stone);
			Part(PrimitiveType.Cylinder, "Platform", level2, new Vector3(0f, 3.9f, 0f), new Vector3(2.9f, 0.12f, 2.9f), stone);
			foreach (var corner in new[] { new Vector2(-1.2f, -1.2f), new Vector2(1.2f, -1.2f), new Vector2(-1.2f, 1.2f), new Vector2(1.2f, 1.2f) })
				Part(PrimitiveType.Cube, "Merlon", level2, new Vector3(corner.x, 4.2f, corner.y), new Vector3(0.5f, 0.6f, 0.5f), stone);
			Part(PrimitiveType.Capsule, "Archer", level2, new Vector3(0f, 4.7f, 0f), new Vector3(0.6f, 0.7f, 0.6f), cloth);
			Part(PrimitiveType.Sphere, "Head", level2, new Vector3(0f, 5.55f, 0f), new Vector3(0.36f, 0.36f, 0.36f), skin);
			Part(PrimitiveType.Cube, "Pole", level2, new Vector3(0f, 5.5f, 1.1f), new Vector3(0.08f, 2.2f, 0.08f), wood);
			Part(PrimitiveType.Cube, "Flag", level2, new Vector3(0.4f, 6.2f, 1.1f), new Vector3(0.8f, 0.5f, 0.05f), gold);

			var highlight = new GameObject("Highlight").transform;
			highlight.SetParent(pad.transform, false);
			Part(PrimitiveType.Cylinder, "Ring", highlight, new Vector3(0f, 0.04f, 0f), new Vector3(4.6f, 0.03f, 4.6f), glow);

			post.EmptyMarker = empty.gameObject;
			post.LevelVisuals = new[] { level1.gameObject, level2.gameObject };
			post.Highlight = highlight.gameObject;
			post.MuzzleHeight = 4.2f;
			post.SetLevel(0);
			post.SetSelected(false);
			return post;
		}

		static void Part(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
		{
			var part = GameObject.CreatePrimitive(type);
			part.name = name;
			Object.Destroy(part.GetComponent<Collider>());
			part.transform.SetParent(parent, false);
			part.transform.localPosition = localPosition;
			part.transform.localScale = scale;
			part.GetComponent<Renderer>().sharedMaterial = material;
		}
	}
}
