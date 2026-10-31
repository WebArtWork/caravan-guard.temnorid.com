using System.Collections.Generic;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Core;
using ITKamianets.Engine.Feedback;
using ITKamianets.Engine.Input;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.TopDown;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Temnorid.CaravanGuard
{
	public enum CaravanPhase
	{
		Menu,
		/// <summary>Between waves: the caravan waits and the player buys and upgrades guards.</summary>
		Planning,
		Wave,
		Victory,
		Defeat,
	}

	/// <summary>
	/// Runs one journey: the player buys and upgrades guards on the pads, starts each wave, the caravan
	/// moves on while attackers come for it, and the journey ends when the caravan reaches the end of the
	/// road after the last wave (victory) or its health reaches zero (defeat).
	/// <para>
	/// Online, the same scene is shared: the host runs everything above (the rules stay here, in <see cref="CaravanSession"/>
	/// and this class) and <see cref="CaravanNet"/> publishes it; a client only shows it and asks the host to buy, upgrade
	/// or start a wave. <see cref="NetSession.IsAuthority"/> is true alone and on the host.
	/// </para>
	/// </summary>
	[DefaultExecutionOrder(-100)]
	public sealed class CaravanGame : MonoBehaviour
	{
		/// <summary>Where saves go instead of the persistent data path. Tests set it before loading the scene.</summary>
		public static string SaveRootOverride;

		/// <summary>The id of the person asking when it is this machine's own player.</summary>
		public const ulong Self = ulong.MaxValue;

		public static CaravanGame Instance { get; private set; }

		[Header("Scene")]
		public OverheadCamera View;
		public Caravan Caravan;
		public Transform[] RoadPoints;
		public GuardPost[] Pads;
		public SpawnPoint[] Spawns;
		public Transform EnemyRoot;

		public bool SaveEnabled = true;
		/// <summary>Start in Planning instead of the start screen. Tests and bots set it.</summary>
		public bool SkipMenu;

		const string Log = "CaravanGuard";

		CaravanSaveStore _store;
		InputAction _startWave;
		readonly List<EnemyKind> _queue = new List<EnemyKind>();
		int _queued;
		float _spawnClock;
		float _spawnInterval;
		int _remoteCoins;
		int _remoteCleared;
		float _remoteHealth;

		public CaravanSession Session { get; private set; }
		public RoadPath Road { get; private set; }
		public CaravanPhase Phase { get; private set; } = CaravanPhase.Menu;
		public bool Paused { get; private set; }
		public GuardPost Selected { get; private set; }

		/// <summary>The wave in progress or about to start, 1 to <see cref="Waves.Count"/>.</summary>
		public int WaveNumber => Mathf.Min(WavesCleared + 1, Waves.Count);
		public float JourneyTime { get; private set; }
		public bool NewBest { get; private set; }

		public string ToastText { get; private set; } = "";
		public float ToastUntil { get; private set; }

		/// <summary>The last things that were said (the toasts), newest last: the wave log.</summary>
		public readonly List<string> History = new List<string>();

		/// <summary>The link to the other players of an online journey. Null when playing alone.</summary>
		public CaravanNet Net { get; private set; }

		public bool Online => Net != null;
		/// <summary>An online player who is not the host: the host runs the journey and this machine shows it.</summary>
		public bool IsClient => Net != null && Net.IsClient;

		/// <summary>The shared coins. A client shows what the host says.</summary>
		public int Coins => IsClient ? _remoteCoins : Session.Coins;
		public int WavesCleared => IsClient ? _remoteCleared : Session.WavesCleared;
		public float CaravanCurrent => IsClient ? _remoteHealth : Caravan.Health.Current;
		public float CaravanMax => Caravan.Health.MaxHealth;
		public float CaravanFraction => CaravanMax > 0f ? Mathf.Clamp01(CaravanCurrent / CaravanMax) : 0f;

		void Awake()
		{
			Instance = this;
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = true; // W A S D pan the camera

			var map = EngineInput.Default.Asset.FindActionMap("Caravan")
				?? EngineInput.Default.AddMap("Caravan", m => m.AddAction("StartWave", InputActionType.Button, "<Keyboard>/space", groups: EngineInputActions.Groups.KeyboardMouse));
			_startWave = map.FindAction("StartWave");

			var points = new Vector3[RoadPoints.Length];
			for (var i = 0; i < points.Length; i++) points[i] = RoadPoints[i].position;
			Road = new RoadPath(points);

			Session = new CaravanSession();
			_store = new CaravanSaveStore(SaveRootOverride);
			Session.Load(_store.Load());

			Caravan.Place(Road, StartProgress);
			Caravan.Health.Died += OnCaravanDied;
			if (View != null) View.Focus = Road.PointAt(StartProgress + 10f);
			if (View != null) gameObject.AddComponent<CaravanTouchView>().Init(this, View);

			// Online, the host runs the journey (waves, attackers, coins, the caravan) and everybody else shows it.
			if (NetSession.Instance.IsOnline) Net = gameObject.AddComponent<CaravanNet>();
		}

		void Start()
		{
			if (Net != null) Net.Init(this);
			if (SkipMenu) Begin();
		}

		void OnEnable() => Enemy.Killed += OnEnemyKilled;
		void OnDisable() => Enemy.Killed -= OnEnemyKilled;

		void OnDestroy()
		{
			if (Instance == this) Instance = null;
		}

		void OnApplicationQuit() => Save();

		/// <summary>Where the lead wagon starts: far enough in that every wagon is on the road.</summary>
		public float StartProgress => Caravan.Spacing * (Caravan.Wagons.Length - 1) + 1f;

		/// <summary>Where the caravan stops after clearing <paramref name="wavesCleared"/> waves.</summary>
		public float StopAfter(int wavesCleared) => Mathf.Lerp(StartProgress, Road.Length, Mathf.Clamp01((wavesCleared + 1f) / Waves.Count));

		// ---- the journey ----

		/// <summary>Leaves the start screen and begins the journey. Only alone or on the host.</summary>
		public void Begin()
		{
			if (Phase != CaravanPhase.Menu || IsClient) return;
			Session.BeginJourney();
			Phase = CaravanPhase.Planning;
			Caravan.Stop = StartProgress;
			Announce("Place guards on the posts beside the road, then start the first wave.");
			Save();
		}

		/// <summary>Starts the next wave. A client asks the host, which decides.</summary>
		public void StartWave()
		{
			if (Phase != CaravanPhase.Planning || Paused) return;
			if (IsClient)
			{
				Net.RequestWave();
				return;
			}
			var plan = Waves.Plan(Session.WavesCleared);
			_queue.Clear();
			for (var i = 0; i < plan.Raiders; i++) _queue.Add(EnemyKind.Raider);
			for (var i = 0; i < plan.Wolves; i++) _queue.Add(EnemyKind.Wolf);
			for (var i = 0; i < plan.Riders; i++) _queue.Add(EnemyKind.Rider);
			Shuffle(_queue, 1000 + Session.WavesCleared);
			_queued = 0;
			_spawnClock = 0f;
			_spawnInterval = plan.SpawnSeconds / Mathf.Max(1, plan.Count);

			Caravan.Stop = StopAfter(Session.WavesCleared);
			Phase = CaravanPhase.Wave;
			Select(null);
			if (View != null) View.Focus = Road.PointAt((Caravan.Progress + Caravan.Stop) * 0.5f); // show the stretch the caravan is about to cover
			Announce($"Wave {WaveNumber} of {Waves.Count}: the caravan moves out!");
			EngineLog.Info(Log, $"Wave {WaveNumber} started: {plan.Count} attackers.");
		}

		void Update()
		{
			// The camera pans at the screen edge only while playing, and not while the pointer is on a button.
			var playing = Phase == CaravanPhase.Planning || Phase == CaravanPhase.Wave;
			if (View != null) View.EdgePan = playing && !Paused && !Application.isMobilePlatform && !TouchHeld && !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
			if (!playing) return;
			if (EngineInput.Default.UI.Pause.WasPressedThisFrame()) SetPaused(!Paused);
			if (Paused) return;

			JourneyTime += Time.deltaTime;
			HandleClick();
			if (EngineInput.Default.Player.Interact.WasPressedThisFrame()) BuyOrUpgrade();
			if (Phase == CaravanPhase.Planning)
			{
				if (_startWave.WasPressedThisFrame()) StartWave();
				return;
			}
			if (IsClient) return; // the host moves the caravan and the attackers
			TickWave(Time.deltaTime);
		}

		void TickWave(float dt)
		{
			Caravan.Advance(dt);

			_spawnClock += dt;
			while (_queued < _queue.Count && _spawnClock >= _queued * _spawnInterval)
			{
				Spawn(_queue[_queued]);
				_queued++;
			}

			if (_queued >= _queue.Count && Enemy.All.Count == 0 && Caravan.AtStop) ClearWave();
		}

		void Spawn(EnemyKind kind)
		{
			var point = PickSpawn();
			var offset = new Vector3(Random.Range(-2.5f, 2.5f), 0f, Random.Range(-2.5f, 2.5f));
			var enemy = EnemyFactory.Create(kind, point + offset, EnemyRoot);
			enemy.NetId = Net != null ? Net.NextEnemyId() : 0;
		}

		/// <summary>A spawn point ahead of the caravan, in the stretch it is about to cover.</summary>
		Vector3 PickSpawn()
		{
			var from = Caravan.Progress - 6f;
			var to = Caravan.Stop + 30f;
			var options = new List<SpawnPoint>();
			foreach (var spawn in Spawns) if (spawn.RoadDistance >= from && spawn.RoadDistance <= to) options.Add(spawn);
			if (options.Count == 0) options.AddRange(Spawns);
			return options[Random.Range(0, options.Count)].transform.position;
		}

		void ClearWave()
		{
			Session.ClearWave();
			if (Session.WavesCleared >= Waves.Count)
			{
				Phase = CaravanPhase.Victory;
				Session.Win(Caravan.Health.Fraction);
				NewBest = true;
				Save();
				StopPlay();
				Announce("The caravan arrived! The journey is won.");
				EngineLog.Info(Log, $"The caravan arrived in {JourneyTime:0.0}s with {Caravan.Health.Fraction:P0} health.");
				return;
			}
			Phase = CaravanPhase.Planning;
			Save();
			if (View != null) View.Focus = Road.PointAt(StopAfter(Session.WavesCleared) - 25f); // the next stretch, where the new guards go
			Announce($"Wave cleared! +{Waves.ClearBonus} coins. Strengthen the guard, then start the next wave.");
		}

		void OnEnemyKilled(Enemy enemy)
		{
			if (enemy.Remote) return;
			var reward = enemy.Stats.Reward;
			Session.Earn(reward);
			Fx.Pulse(PrimitiveType.Sphere, enemy.transform.position + Vector3.up * 1.2f, Quaternion.identity, Vector3.one * 0.3f, Vector3.one * 0.05f, new Color(1f, 0.82f, 0.2f), 0.5f);
		}

		void OnCaravanDied(Health health, DamageInfo info)
		{
			if (IsClient) return;
			if (Phase != CaravanPhase.Wave && Phase != CaravanPhase.Planning) return;
			Phase = CaravanPhase.Defeat;
			Save();
			StopPlay();
			Announce("The caravan was destroyed.");
			EngineLog.Info(Log, "The caravan was destroyed.");
		}

		// ---- the pads ----

		void HandleClick()
		{
			var input = EngineInput.Default;
			if (!input.UI.Click.WasPressedThisFrame()) return;
			if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return; // a finger: CaravanTouchView tells a tap from a drag or a pinch
			if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
			TryPick(input.UI.Point.ReadValue<Vector2>());
		}

		/// <summary>True while a finger is on the screen.</summary>
		static bool TouchHeld => Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed;

		/// <summary>A tap on the screen: selects the post under it, or clears the selection when there is none.</summary>
		public void Tap(Vector2 screenPosition)
		{
			if (Phase != CaravanPhase.Planning && Phase != CaravanPhase.Wave) return;
			if (Paused) return;
			if (!TryPick(screenPosition)) Select(null);
		}

		/// <summary>Selects the guard post under a screen position. False when there is none there.</summary>
		public bool TryPick(Vector2 screenPosition)
		{
			var camera = Camera.main;
			if (camera == null) return false;
			if (!Physics.Raycast(camera.ScreenPointToRay(screenPosition), out var hit, 500f)) return false;
			var post = hit.collider.GetComponentInParent<GuardPost>();
			if (post == null) return false;
			Select(post);
			return true;
		}

		public void Select(GuardPost post)
		{
			if (Selected != null) Selected.SetSelected(false);
			Selected = post;
			if (Selected != null) Selected.SetSelected(true);
		}

		/// <summary>Buys a guard on the selected empty pad, or upgrades the selected guard. A client asks the host.</summary>
		public bool BuyOrUpgrade()
		{
			if (Selected == null) return false;
			if (IsClient) return Net.RequestPost(Selected, Selected.IsEmpty);
			return Selected.IsEmpty ? Buy(Selected) : Upgrade(Selected);
		}

		/// <summary>Buys a guard on an empty pad. Only where the rules run (alone, or the host); <paramref name="by"/> is the player who asked.</summary>
		public bool Buy(GuardPost post, ulong by = Self)
		{
			if (IsClient) return false;
			if (Phase != CaravanPhase.Planning && Phase != CaravanPhase.Wave) return false;
			if (post == null) return false;
			if (!post.IsEmpty)
			{
				if (by != Self) Tell(by, "That post already has a guard.");
				return false;
			}
			if (!Session.TryBuyGuard())
			{
				Tell(by, $"A guard costs {GuardStats.BuyCost} coins.");
				return false;
			}
			post.SetLevel(1);
			var who = Actor(by);
			Announce(who == null ? "Guard placed." : $"{who} placed a guard on post {PostNumber(post)}.");
			return true;
		}

		public bool Upgrade(GuardPost post, ulong by = Self)
		{
			if (IsClient) return false;
			if (Phase != CaravanPhase.Planning && Phase != CaravanPhase.Wave) return false;
			if (post == null || !post.CanUpgrade) return false;
			if (!Session.TryUpgradeGuard(post.Level))
			{
				Tell(by, $"An upgrade costs {GuardStats.UpgradeCost} coins.");
				return false;
			}
			post.SetLevel(post.Level + 1);
			var who = Actor(by);
			Announce(who == null ? "Guard upgraded." : $"{who} upgraded the guard on post {PostNumber(post)}.");
			return true;
		}

		/// <summary>The one-based number of a post, as shown to the players.</summary>
		public int PostNumber(GuardPost post) => System.Array.IndexOf(Pads, post) + 1;

		/// <summary>The name of the player who did something, or null when playing alone.</summary>
		string Actor(ulong by)
		{
			if (Net == null) return null;
			if (by == Self) return Net.LocalName;
			var player = NetSession.Instance.Find(by);
			return player != null ? player.Name : "Someone";
		}

		/// <summary>Says something to one player: on this machine, or to the client that asked.</summary>
		void Tell(ulong by, string text)
		{
			if (by == Self || Net == null) ShowToast(text);
			else Net.Tell(by, text);
		}

		/// <summary>Says something to everybody: here, and to every client when hosting.</summary>
		void Announce(string text)
		{
			ShowToast(text);
			Net?.Broadcast(text);
		}

		// ---- what a client is told by the host ----

		/// <summary>Online client: the host's phase, waves cleared and coins. Starts, ends and records the journey here the way the host does.</summary>
		internal void MirrorPhase(CaravanPhase phase, int cleared, int coins)
		{
			_remoteCoins = coins;
			if (phase == Phase && cleared == _remoteCleared) return;
			var previous = Phase;
			if (previous == CaravanPhase.Menu && phase != CaravanPhase.Menu) Session.BeginJourney();
			while (Session.WavesCleared < cleared) Session.ClearWave();
			_remoteCleared = cleared;
			Phase = phase;

			switch (phase)
			{
				case CaravanPhase.Planning:
					if (previous == CaravanPhase.Wave && View != null) View.Focus = Road.PointAt(StopAfter(cleared) - 25f);
					if (previous == CaravanPhase.Menu) Save();
					break;
				case CaravanPhase.Wave:
					Select(null);
					if (View != null) View.Focus = Road.PointAt((Caravan.Progress + StopAfter(cleared)) * 0.5f);
					break;
				case CaravanPhase.Victory:
					Session.Win(CaravanFraction);
					NewBest = true;
					Save();
					StopPlay();
					break;
				case CaravanPhase.Defeat:
					Save();
					StopPlay();
					break;
			}
		}

		internal void MirrorCaravanHealth(float health) => _remoteHealth = health;

		// ---- pause, retry, quit ----

		public void SetPaused(bool paused)
		{
			if (Phase != CaravanPhase.Planning && Phase != CaravanPhase.Wave) return;
			if (Net != null) return; // others are playing: nothing to pause
			Paused = paused;
			Time.timeScale = paused ? 0f : 1f;
			if (paused) Save();
		}

		void StopPlay() => EngineInput.Default.PlayerEnabled = true;

		public void Retry()
		{
			if (Net != null) return; // a shared journey ends at the main menu
			Time.timeScale = 1f;
			SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
		}

		public void ShowToast(string text)
		{
			ToastText = text;
			ToastUntil = Time.unscaledTime + 4f;
			History.Add(text);
			if (History.Count > 40) History.RemoveAt(0);
		}

		public void Save()
		{
			if (SaveEnabled && Session != null) _store.Save(Session.ToSave());
		}

		static void Shuffle(List<EnemyKind> list, int seed)
		{
			var random = new System.Random(seed);
			for (var i = list.Count - 1; i > 0; i--)
			{
				var j = random.Next(i + 1);
				(list[i], list[j]) = (list[j], list[i]);
			}
		}
	}
}
