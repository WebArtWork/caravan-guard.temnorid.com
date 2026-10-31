using System.Collections.Generic;
using ITKamianets.Engine.Input;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.Shell;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Temnorid.CaravanGuard
{
	/// <summary>
	/// Makes the journey shared. The host runs everything (the rules stay in <see cref="CaravanGame"/> and
	/// <see cref="CaravanSession"/>) and publishes it about ten times a second as entities:
	/// <list type="bullet">
	/// <item>id 0, <see cref="KindState"/>: coins (Value), phase and waves cleared (State);</item>
	/// <item>ids 1 to 10, <see cref="KindPad"/>: the guard post's level (State);</item>
	/// <item>id 500, <see cref="KindCaravan"/>: lead wagon position, progress in metres (Yaw), health (Value), stop point in decimetres (State);</item>
	/// <item>ids from 1000, the three attacker kinds: position, yaw, health (Value).</item>
	/// </list>
	/// A client asks with events (<c>cg.buy</c> and <c>cg.up</c> with the post number, <c>cg.wave</c>); the host checks the rules and the
	/// result comes back as the entities above and a <c>cg.msg</c> line. Everybody's pointer is shown to the others as a pose (the ground
	/// point under it). Each person's camera is their own.
	/// </summary>
	public sealed class CaravanNet : MonoBehaviour
	{
		public const int KindState = 300;
		public const int KindPad = 100;
		public const int KindCaravan = 200;
		public const int StateId = 0;
		public const int PadBase = 1;
		public const int CaravanId = 500;
		public const int EnemyBase = 1000;

		public const string BuyEvent = "cg.buy";
		public const string UpgradeEvent = "cg.up";
		public const string WaveEvent = "cg.wave";
		public const string MessageEvent = "cg.msg";

		const float PublishEvery = 0.1f;
		const float PoseEvery = 1f / 12f;
		const float StaleAfter = 1.5f;

		CaravanGame _game;
		NetSession _net;
		bool _ready;
		float _nextPublish;
		float _nextPose;
		int _nextEnemy = EnemyBase;
		float _targetProgress;
		float _targetStop;
		bool _haveCaravan;
		Vector3 _lastGround;

		readonly List<NetEntity> _list = new List<NetEntity>();
		readonly Dictionary<int, Enemy> _puppets = new Dictionary<int, Enemy>();
		readonly Dictionary<int, float> _seen = new Dictionary<int, float>();
		readonly HashSet<int> _gone = new HashSet<int>();
		readonly Dictionary<ulong, Transform> _cursors = new Dictionary<ulong, Transform>();
		readonly List<int> _scratch = new List<int>();

		public bool IsClient => _net != null && _net.IsClient;
		public bool IsHost => _net != null && _net.IsHost;
		public string LocalName => _net != null ? _net.LocalName : "";
		public int NextEnemyId() => _nextEnemy++;

		/// <summary>The marker that shows another player's pointer, for tests.</summary>
		public Transform CursorOf(ulong player) => _cursors.TryGetValue(player, out var cursor) ? cursor : null;

		public void Init(CaravanGame game)
		{
			if (_ready) return;
			_ready = true;
			_game = game;
			_net = NetSession.Instance;
			_net.On(BuyEvent, OnBuy);
			_net.On(UpgradeEvent, OnUpgrade);
			_net.On(WaveEvent, OnWave);
			_net.On(MessageEvent, OnMessage);
			_net.Disconnected += OnDisconnected;
			_net.EntityUpdated += OnEntity;
			_net.EntityGone += OnGone;
			_net.PlayerLeft += OnPlayerLeft;
			Enemy.Killed += OnKilled;
			if (_net.IsClient) foreach (var entity in new List<NetEntity>(_net.Entities.Values)) OnEntity(entity);
		}

		void OnDestroy()
		{
			if (!_ready) return;
			_net.Off(BuyEvent, OnBuy);
			_net.Off(UpgradeEvent, OnUpgrade);
			_net.Off(WaveEvent, OnWave);
			_net.Off(MessageEvent, OnMessage);
			_net.Disconnected -= OnDisconnected;
			_net.EntityUpdated -= OnEntity;
			_net.EntityGone -= OnGone;
			_net.PlayerLeft -= OnPlayerLeft;
			Enemy.Killed -= OnKilled;
		}

		// ---- what a client asks ----

		public void RequestWave() => _net.Send(WaveEvent, "", true);

		/// <summary>A client asks the host to buy a guard on, or upgrade, a post. True when the request was sent.</summary>
		public bool RequestPost(GuardPost post, bool buy)
		{
			var index = System.Array.IndexOf(_game.Pads, post);
			if (index < 0) return false;
			_net.Send(buy ? BuyEvent : UpgradeEvent, index.ToString(), true);
			return true;
		}

		// ---- the host checks and applies ----

		GuardPost PostFrom(ulong sender, string payload)
		{
			if (!_net.IsHost || _net.Find(sender) == null) return null;
			if (!int.TryParse(payload, out var index) || index < 0 || index >= _game.Pads.Length) return null;
			return _game.Pads[index];
		}

		void OnBuy(ulong sender, string payload)
		{
			var post = PostFrom(sender, payload);
			if (post != null) _game.Buy(post, sender);
		}

		void OnUpgrade(ulong sender, string payload)
		{
			var post = PostFrom(sender, payload);
			if (post != null) _game.Upgrade(post, sender);
		}

		void OnWave(ulong sender, string payload)
		{
			if (_net.IsHost && _net.Find(sender) != null) _game.StartWave();
		}

		/// <summary>Host: tells everybody something (a guard placed, a wave cleared, the journey won or lost).</summary>
		public void Broadcast(string text)
		{
			if (_net.IsHost) _net.Send(MessageEvent, text);
		}

		/// <summary>Host: tells one player something (their request was refused).</summary>
		public void Tell(ulong player, string text)
		{
			if (_net.IsHost) _net.SendTo(player, MessageEvent, text);
		}

		void OnMessage(ulong sender, string text)
		{
			if (_net.IsClient) _game.ShowToast(text);
		}

		void OnDisconnected(string reason)
		{
			// The host closed the game or the connection dropped: nothing left to defend together.
			ShellFlow.QuitToMenu();
		}

		void OnPlayerLeft(NetPlayer player)
		{
			_game.ShowToast(player.Name + " left the defence.");
			if (_cursors.TryGetValue(player.Id, out var cursor) && cursor != null) Destroy(cursor.gameObject);
			_cursors.Remove(player.Id);
		}

		void OnKilled(Enemy enemy)
		{
			if (_net.IsHost && !enemy.Remote && enemy.NetId != 0) _net.RemoveEntity(enemy.NetId);
		}

		// ---- the host publishes ----

		void Update()
		{
			if (!_ready || _net == null || !_net.IsOnline) return;
			if (_net.IsHost && Time.unscaledTime >= _nextPublish)
			{
				_nextPublish = Time.unscaledTime + PublishEvery;
				Publish();
			}
			if (_net.IsClient) ShowCaravan();
			if (_net.IsClient) DropStale();

			if (Time.unscaledTime >= _nextPose)
			{
				_nextPose = Time.unscaledTime + PoseEvery;
				PublishPointer();
			}
			ShowPointers();
		}

		void Publish()
		{
			_list.Clear();
			var session = _game.Session;
			_list.Add(new NetEntity { Id = StateId, Kind = KindState, Value = session.Coins, State = (int)_game.Phase | (session.WavesCleared << 4) });
			for (var i = 0; i < _game.Pads.Length; i++)
			{
				var pad = _game.Pads[i];
				_list.Add(new NetEntity { Id = PadBase + i, Kind = KindPad, Position = pad.transform.position, State = pad.Level });
			}
			var caravan = _game.Caravan;
			_list.Add(new NetEntity
			{
				Id = CaravanId,
				Kind = KindCaravan,
				Position = caravan.transform.position,
				Yaw = caravan.Progress,
				Value = caravan.Health.Current,
				State = Mathf.RoundToInt(caravan.Stop * 10f),
			});
			foreach (var enemy in Enemy.All)
			{
				if (enemy.Remote || enemy.NetId == 0 || !enemy.Health.IsAlive) continue;
				_list.Add(new NetEntity { Id = enemy.NetId, Kind = (int)enemy.Kind, Position = enemy.transform.position, Yaw = enemy.transform.eulerAngles.y, Value = enemy.Health.Current });
			}
			_net.PublishEntities(_list);
		}

		// ---- a client shows ----

		void OnEntity(NetEntity entity)
		{
			if (_game == null || !_net.IsClient) return;
			switch (entity.Kind)
			{
				case KindState:
					_game.MirrorPhase((CaravanPhase)(entity.State & 0xF), (entity.State >> 4) & 0xF, Mathf.RoundToInt(entity.Value));
					break;
				case KindPad:
					var index = entity.Id - PadBase;
					if (index >= 0 && index < _game.Pads.Length && _game.Pads[index].Level != entity.State) _game.Pads[index].SetLevel(entity.State);
					break;
				case KindCaravan:
					_targetProgress = entity.Yaw;
					_targetStop = entity.State / 10f;
					_game.MirrorCaravanHealth(entity.Value);
					_haveCaravan = true;
					break;
				default:
					ShowEnemy(entity);
					break;
			}
		}

		void ShowEnemy(NetEntity entity)
		{
			if (entity.Kind < 0 || entity.Kind > (int)EnemyKind.Rider || _gone.Contains(entity.Id)) return;
			if (!_puppets.TryGetValue(entity.Id, out var enemy) || enemy == null)
			{
				enemy = EnemyFactory.Create((EnemyKind)entity.Kind, entity.Position, _game.EnemyRoot);
				enemy.MakeRemote();
				enemy.NetId = entity.Id;
				enemy.gameObject.AddComponent<RemoteBody>();
				_puppets[entity.Id] = enemy;
			}
			_seen[entity.Id] = Time.unscaledTime;
			enemy.GetComponent<RemoteBody>().Apply(entity);
			enemy.ShowHealth(entity.Value);
		}

		void OnGone(int id)
		{
			_gone.Add(id);
			if (_puppets.TryGetValue(id, out var enemy) && enemy != null) enemy.Vanish();
			_puppets.Remove(id);
			_seen.Remove(id);
		}

		// The host stops publishing what is gone; if the "gone" message was lost, the picture is dropped after a moment.
		void DropStale()
		{
			_scratch.Clear();
			foreach (var pair in _seen)
				if (Time.unscaledTime - pair.Value > StaleAfter) _scratch.Add(pair.Key);
			foreach (var id in _scratch) OnGone(id);
		}

		void ShowCaravan()
		{
			if (!_haveCaravan) return;
			var caravan = _game.Caravan;
			var progress = caravan.Progress;
			progress = Mathf.Abs(_targetProgress - progress) > 8f ? _targetProgress : Mathf.Lerp(progress, _targetProgress, 1f - Mathf.Exp(-Time.deltaTime / 0.15f));
			caravan.Show(progress, _targetStop);
		}

		// ---- pointers ----

		bool PointerGround(out Vector3 point)
		{
			point = default;
			var view = _game.View;
			if (view == null) return false;
			Vector2 position;
			if (Application.isMobilePlatform)
			{
				var touch = Touchscreen.current;
				if (touch == null || !touch.primaryTouch.press.isPressed) return false;
				position = touch.primaryTouch.position.ReadValue();
			}
			else position = EngineInput.Default.UI.Point.ReadValue<Vector2>();
			if (position == Vector2.zero || position.x < 0f || position.y < 0f || position.x > Screen.width || position.y > Screen.height) return false;
			return view.TryGroundPoint(position, out point);
		}

		void PublishPointer()
		{
			var visible = PointerGround(out var ground);
			if (visible) _lastGround = ground;
			_net.PublishPose(new NetPose { Position = _lastGround, State = visible ? 1 : 0 });
		}

		void ShowPointers()
		{
			foreach (var player in _net.Players)
			{
				if (player.Id == _net.LocalId) continue;
				if (!_net.TryGetPose(player.Id, out var pose)) continue;
				if (!_cursors.TryGetValue(player.Id, out var cursor) || cursor == null)
				{
					cursor = MakeCursor(player);
					_cursors[player.Id] = cursor;
				}
				cursor.GetComponent<RemoteBody>().Apply(pose);
				cursor.gameObject.SetActive(pose.State == 1);
			}
		}

		static Transform MakeCursor(NetPlayer player)
		{
			var colour = ShellConfig.Current.ColourOf(player.Colour);
			var root = new GameObject("Pointer " + player.Name);
			root.AddComponent<RemoteBody>();
			var material = ITKamianets.Engine.Feedback.Fx.Material(colour);

			var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			ring.name = "Ring";
			Destroy(ring.GetComponent<Collider>()); // never in the way of a click
			ring.transform.SetParent(root.transform, false);
			ring.transform.localPosition = new Vector3(0f, 0.15f, 0f);
			ring.transform.localScale = new Vector3(2.2f, 0.05f, 2.2f);
			ring.GetComponent<Renderer>().sharedMaterial = material;

			var pin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			pin.name = "Pin";
			Destroy(pin.GetComponent<Collider>());
			pin.transform.SetParent(root.transform, false);
			pin.transform.localPosition = new Vector3(0f, 1.5f, 0f);
			pin.transform.localScale = new Vector3(0.25f, 1.5f, 0.25f);
			pin.GetComponent<Renderer>().sharedMaterial = material;

			var label = new GameObject("Name");
			label.transform.SetParent(root.transform, false);
			label.transform.localPosition = new Vector3(0f, 3.6f, 0f);
			var text = label.AddComponent<TextMesh>();
			text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
			text.text = player.Name;
			text.fontSize = 64;
			text.characterSize = 0.12f;
			text.anchor = TextAnchor.MiddleCenter;
			text.color = colour;
			label.AddComponent<FaceCamera>();
			return root.transform;
		}

		sealed class FaceCamera : MonoBehaviour
		{
			void LateUpdate()
			{
				var camera = Camera.main;
				if (camera != null) transform.rotation = camera.transform.rotation;
			}
		}
	}
}
