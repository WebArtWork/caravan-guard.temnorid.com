using System.Text;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.Shell;
using ITKamianets.Engine.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Temnorid.CaravanGuard
{
	/// <summary>
	/// All of the game's screens, drawn with plain uGUI built in code, inside the strips the shell's bars leave free. It only shows
	/// what <see cref="CaravanGame"/> says. Places: Defence (the field with the HUD) and Archers (the posts, coins and the wave log).
	/// </summary>
	public sealed class CaravanHud : MonoBehaviour
	{
		public CaravanGame Game;

		static readonly Color Panel = new Color(0.07f, 0.06f, 0.04f, 0.82f);
		static readonly Color Gold = new Color(0.95f, 0.75f, 0.25f);

		UiBuilder _ui;
		ShellChrome _chrome;
		RectTransform _field;
		GameObject _menu, _hud, _pause, _result, _archers;
		Text _records, _lobby, _coins, _wave, _caravanText, _toast, _hint;
		Text _selTitle, _selInfo, _resultTitle, _resultInfo, _postsText, _logText, _archersHead;
		Image _caravanFill;
		GameObject _selection;
		Button _buy, _startButton, _menuStart, _pauseButton, _retry;
		Text _buyLabel, _startLabel;
		CaravanPhase _shown = (CaravanPhase)(-1);
		bool _shownPaused;
		float _archersAt;
		readonly StringBuilder _text = new StringBuilder();

		void Start()
		{
			_ui = new UiBuilder
			{
				ButtonHighlight = new Color(1f, 0.9f, 0.6f),
				ButtonSelected = new Color(1f, 0.85f, 0.45f),
				ButtonPressed = new Color(0.8f, 0.65f, 0.3f),
			};
			UiBuilder.EnsureEventSystem(transform);
			var canvas = UiBuilder.CreateCanvas(transform);

			_chrome = ShellChrome.Create(transform);
			_chrome.PlaceSelected += OnPlace;

			// Everything below lives in the field: the screen without the shell's bars, and inside a phone's safe area.
			var safe = new GameObject("Safe", typeof(RectTransform), typeof(SafeAreaFitter));
			safe.transform.SetParent(canvas.transform, false);
			var fieldObject = new GameObject("Field", typeof(RectTransform));
			fieldObject.transform.SetParent(safe.transform, false);
			_field = (RectTransform)fieldObject.transform;
			_field.anchorMin = new Vector2(0f, _chrome.BottomFraction);
			_field.anchorMax = new Vector2(1f, 1f - _chrome.TopFraction);
			_field.offsetMin = _field.offsetMax = Vector2.zero;

			BuildMenu(_field);
			BuildHud(_field);
			BuildPause(_field);
			BuildResult(_field);
			BuildArchers(_field);
		}

		void OnPlace(string id)
		{
			if (_archers == null) return;
			var open = id == CaravanShell.ArchersPlace;
			_archers.SetActive(open);
			if (open) RefreshArchers();
		}

		void Update()
		{
			if (Game == null || _menu == null) return;
			var phase = Game.Phase;
			if (phase != _shown || Game.Paused != _shownPaused)
			{
				_shown = phase;
				_shownPaused = Game.Paused;
				_menu.SetActive(phase == CaravanPhase.Menu);
				_hud.SetActive(phase == CaravanPhase.Planning || phase == CaravanPhase.Wave);
				_pause.SetActive(Game.Paused);
				_result.SetActive(phase == CaravanPhase.Victory || phase == CaravanPhase.Defeat);
				if (phase == CaravanPhase.Menu) _records.text = RecordsText();
				if (phase == CaravanPhase.Victory || phase == CaravanPhase.Defeat) FillResult();
				if (phase != CaravanPhase.Menu && _chrome.CurrentPlace != CaravanShell.DefencePlace && !_chrome.ProfileOpen) _chrome.Select(CaravanShell.DefencePlace);
				UiBuilder.Select(Game.Paused ? _pause : phase == CaravanPhase.Menu ? _menu : (phase == CaravanPhase.Victory || phase == CaravanPhase.Defeat) ? _result : null);
			}
			if (phase == CaravanPhase.Menu) RefreshLobby();
			if (_hud.activeSelf) UpdateHud();
			if (phase != CaravanPhase.Menu) _chrome.SetStatus($"Wave {Game.WaveNumber}/{Waves.Count} · {Game.Coins} coins");
			if (_archers.activeSelf && Time.unscaledTime >= _archersAt) RefreshArchers();
		}

		// ---- screens ----

		void BuildMenu(Transform root)
		{
			_menu = UiBuilder.Box("StartScreen", root, new Vector2(0.24f, 0.06f), new Vector2(0.76f, 0.94f), Panel);
			_ui.Label("Title", _menu.transform, "Ready to guard?", 54, TextAnchor.MiddleCenter, new Vector2(0f, 0.82f), new Vector2(1f, 0.98f), Gold);
			_ui.Label("Tagline", _menu.transform, "Place the guards. Bring the wagons home.", 32, TextAnchor.MiddleCenter, new Vector2(0.03f, 0.72f), new Vector2(0.97f, 0.82f), Color.white);
			_records = _ui.Label("Records", _menu.transform, RecordsText(), 30, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.7f), Color.white);
			_lobby = _ui.Label("Lobby", _menu.transform, "", 30, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.4f), new Vector2(0.95f, 0.5f), Gold, wrap: true);
			_menuStart = _ui.Button("Start journey", _menu.transform, new Vector2(0.2f, 0.24f), new Vector2(0.8f, 0.38f), () => Game.Begin());
			_ui.Button("Main menu", _menu.transform, new Vector2(0.2f, 0.09f), new Vector2(0.8f, 0.21f), () => ShellFlow.QuitToMenu());
			var hint = TouchButton.IsNeeded
				? "Tap a post  ·  drag to move the map  ·  pinch to zoom"
				: "W A S D or the screen edge: camera  ·  wheel: zoom  ·  click a post  ·  Space: start wave";
			_ui.Label("Hint", _menu.transform, hint, 26, TextAnchor.MiddleCenter, new Vector2(0.02f, 0f), new Vector2(0.98f, 0.08f), new Color(1f, 1f, 1f, 0.7f));
		}

		void BuildHud(Transform root)
		{
			_hud = new GameObject("Hud", typeof(RectTransform));
			_hud.transform.SetParent(root, false);
			var rect = (RectTransform)_hud.transform;
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = rect.offsetMax = Vector2.zero;

			var top = UiBuilder.Box("TopBar", _hud.transform, new Vector2(0.2f, 0.915f), new Vector2(0.8f, 0.995f), Panel);
			_coins = _ui.Label("Coins", top.transform, "", 36, TextAnchor.MiddleLeft, new Vector2(0.03f, 0f), new Vector2(0.3f, 1f), Gold);
			_wave = _ui.Label("Wave", top.transform, "", 36, TextAnchor.MiddleCenter, new Vector2(0.3f, 0f), new Vector2(0.6f, 1f), Color.white);
			_caravanText = _ui.Label("Caravan", top.transform, "", 28, TextAnchor.MiddleRight, new Vector2(0.6f, 0.42f), new Vector2(0.97f, 1f), Color.white);
			_caravanFill = UiBuilder.Bar("Caravan", top.transform, new Vector2(0.62f, 0.1f), new Vector2(0.97f, 0.38f), new Color(0.3f, 0.8f, 0.35f));

			_toast = _ui.Label("Toast", _hud.transform, "", 32, TextAnchor.MiddleCenter, new Vector2(0.12f, 0.835f), new Vector2(0.88f, 0.91f), Color.white);
			_hint = _ui.Label("Hint", _hud.transform, "", 30, TextAnchor.MiddleCenter, new Vector2(0.22f, 0.77f), new Vector2(0.78f, 0.835f), new Color(1f, 1f, 1f, 0.8f));

			_selection = UiBuilder.Box("Selection", _hud.transform, new Vector2(0.69f, 0.2f), new Vector2(0.99f, 0.72f), Panel);
			_selTitle = _ui.Label("Title", _selection.transform, "", 36, TextAnchor.MiddleCenter, new Vector2(0f, 0.8f), new Vector2(1f, 0.98f), Gold);
			_selInfo = _ui.Label("Info", _selection.transform, "", 28, TextAnchor.UpperLeft, new Vector2(0.06f, 0.4f), new Vector2(0.96f, 0.8f), Color.white, wrap: true);
			_buy = _ui.Button("Buy", _selection.transform, new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.36f), () => Game.BuyOrUpgrade());
			_buyLabel = _buy.GetComponentInChildren<Text>();

			// Big enough for a thumb on a phone: about a seventh of the height.
			_startButton = _ui.Button("Start wave", _hud.transform, new Vector2(0.34f, 0.02f), new Vector2(0.66f, 0.17f), () => Game.StartWave());
			_startLabel = _startButton.GetComponentInChildren<Text>();
			_buyLabel.fontSize = 32;
			_startLabel.fontSize = 34;

			_pauseButton = _ui.Button("Pause", _hud.transform, new Vector2(0.01f, 0.8f), new Vector2(0.11f, 0.915f), () => Game.SetPaused(true));
			_pauseButton.GetComponentInChildren<Text>().fontSize = 32;
		}

		void BuildPause(Transform root)
		{
			_pause = UiBuilder.Box("PauseMenu", root, new Vector2(0.34f, 0.2f), new Vector2(0.66f, 0.8f), Panel);
			_ui.Label("Title", _pause.transform, "PAUSED", 56, TextAnchor.MiddleCenter, new Vector2(0f, 0.74f), new Vector2(1f, 0.97f), Gold);
			_ui.Button("Resume", _pause.transform, new Vector2(0.15f, 0.5f), new Vector2(0.85f, 0.7f), () => Game.SetPaused(false));
			_ui.Button("Restart", _pause.transform, new Vector2(0.15f, 0.27f), new Vector2(0.85f, 0.47f), () => Game.Retry());
			_ui.Button("Main menu", _pause.transform, new Vector2(0.15f, 0.04f), new Vector2(0.85f, 0.24f), () =>
			{
				Game.Save();
				ShellFlow.QuitToMenu();
			});
		}

		void BuildResult(Transform root)
		{
			_result = UiBuilder.Box("Result", root, new Vector2(0.28f, 0.14f), new Vector2(0.72f, 0.86f), Panel);
			_resultTitle = _ui.Label("Title", _result.transform, "", 56, TextAnchor.MiddleCenter, new Vector2(0f, 0.74f), new Vector2(1f, 0.97f), Gold);
			_resultInfo = _ui.Label("Info", _result.transform, "", 32, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.74f), Color.white, wrap: true);
			_retry = _ui.Button("Retry", _result.transform, new Vector2(0.15f, 0.22f), new Vector2(0.85f, 0.38f), () => Game.Retry());
			_ui.Button("Main menu", _result.transform, new Vector2(0.15f, 0.04f), new Vector2(0.85f, 0.19f), () => ShellFlow.QuitToMenu());
		}

		void BuildArchers(Transform root)
		{
			_archers = UiBuilder.Box("Archers", root, new Vector2(0.1f, 0.04f), new Vector2(0.9f, 0.96f), new Color(0.07f, 0.06f, 0.04f, 0.94f));
			_ui.Label("Title", _archers.transform, "Archers", 52, TextAnchor.MiddleCenter, new Vector2(0f, 0.88f), new Vector2(1f, 0.99f), Gold);
			_archersHead = _ui.Label("Head", _archers.transform, "", 32, TextAnchor.MiddleCenter, new Vector2(0.03f, 0.8f), new Vector2(0.97f, 0.88f), Color.white);
			_postsText = _ui.Label("Posts", _archers.transform, "", 32, TextAnchor.UpperLeft, new Vector2(0.04f, 0.2f), new Vector2(0.48f, 0.8f), Color.white);
			_ui.Label("LogTitle", _archers.transform, "Wave log", 34, TextAnchor.UpperLeft, new Vector2(0.52f, 0.73f), new Vector2(0.96f, 0.8f), Gold);
			_logText = _ui.Label("Log", _archers.transform, "", 28, TextAnchor.UpperLeft, new Vector2(0.52f, 0.2f), new Vector2(0.96f, 0.73f), new Color(1f, 1f, 1f, 0.9f), wrap: true);
			_ui.Button("Back to the field", _archers.transform, new Vector2(0.25f, 0.03f), new Vector2(0.75f, 0.16f), () => _chrome.Select(CaravanShell.DefencePlace));
			_archers.SetActive(false);
		}

		// ---- updating ----

		void RefreshLobby()
		{
			var net = NetSession.Instance;
			if (!net.IsOnline)
			{
				UiBuilder.SetText(_lobby, "");
				_menuStart.interactable = true;
				return;
			}
			_text.Clear();
			for (var i = 0; i < net.Players.Count; i++) _text.Append(i > 0 ? ", " : "").Append(net.Players[i].Name);
			UiBuilder.SetText(_lobby, (net.IsHost ? "Defending together: " : "Waiting for the host to start. Defending together: ") + _text);
			_menuStart.interactable = net.IsHost;
		}

		void UpdateHud()
		{
			var coins = Game.Coins;
			UiBuilder.SetText(_coins, $"Coins {coins}");
			UiBuilder.SetText(_wave, $"Wave {Game.WaveNumber} / {Waves.Count}");
			UiBuilder.SetText(_caravanText, $"Caravan {Mathf.CeilToInt(Game.CaravanCurrent)} / {Mathf.RoundToInt(Game.CaravanMax)}");
			UiBuilder.SetFill(_caravanFill, Game.CaravanFraction);

			_toast.text = Time.unscaledTime < Game.ToastUntil ? Game.ToastText : "";
			_pauseButton.gameObject.SetActive(!Game.Online);

			var planning = Game.Phase == CaravanPhase.Planning;
			_startButton.gameObject.SetActive(planning);
			if (planning)
			{
				var final = Game.WaveNumber >= Waves.Count && Game.WavesCleared == Waves.Count - 1;
				var key = TouchButton.IsNeeded ? "" : " (Space)";
				_startLabel.text = final ? "Final wave" + key : $"Start wave {Game.WaveNumber}" + key;
			}
			_hint.text = planning ? (TouchButton.IsNeeded ? "Tap a post to buy or upgrade a guard" : "Click a post to buy or upgrade a guard") : $"Attackers left: {Enemy.All.Count}";

			var post = Game.Selected;
			_selection.SetActive(post != null);
			if (post == null) return;
			var key2 = TouchButton.IsNeeded ? "" : "  [E]";
			if (post.IsEmpty)
			{
				_selTitle.text = "Empty post";
				_selInfo.text = $"A guard here shoots {GuardStats.Damage(1):0} damage every {GuardStats.Interval(1):0.0}s within {GuardStats.Range(1):0} m.";
				_buyLabel.text = $"Buy guard ({GuardStats.BuyCost})" + key2;
				_buy.interactable = coins >= GuardStats.BuyCost;
			}
			else if (post.CanUpgrade)
			{
				_selTitle.text = "Guard, level " + post.Level;
				_selInfo.text = $"Upgrade: {GuardStats.Damage(post.Level + 1):0} damage every {GuardStats.Interval(post.Level + 1):0.00}s within {GuardStats.Range(post.Level + 1):0} m.";
				_buyLabel.text = $"Upgrade ({GuardStats.UpgradeCost})" + key2;
				_buy.interactable = coins >= GuardStats.UpgradeCost;
			}
			else
			{
				_selTitle.text = "Guard, level " + post.Level;
				_selInfo.text = $"{GuardStats.Damage(post.Level):0} damage every {GuardStats.Interval(post.Level):0.00}s within {GuardStats.Range(post.Level):0} m. Fully upgraded.";
				_buyLabel.text = "Fully upgraded";
				_buy.interactable = false;
			}
		}

		void RefreshArchers()
		{
			_archersAt = Time.unscaledTime + 0.25f;
			if (Game == null) return;
			UiBuilder.SetText(_archersHead, Game.Phase == CaravanPhase.Menu
				? "The journey has not started."
				: $"Coins {Game.Coins}   ·   Wave {Game.WaveNumber} / {Waves.Count}   ·   Caravan {Mathf.CeilToInt(Game.CaravanCurrent)} / {Mathf.RoundToInt(Game.CaravanMax)}");

			_text.Clear();
			var built = 0;
			for (var i = 0; i < Game.Pads.Length; i++)
			{
				var level = Game.Pads[i].Level;
				if (level > 0) built++;
				_text.Append("Post ").Append(i + 1).Append(":  ").Append(level == 0 ? "empty" : "guard, level " + level).Append('\n');
			}
			_text.Append('\n').Append(built).Append(" of ").Append(Game.Pads.Length).Append(" posts guarded");
			UiBuilder.SetText(_postsText, _text.ToString());

			_text.Clear();
			var history = Game.History;
			for (var i = Mathf.Max(0, history.Count - 7); i < history.Count; i++) _text.Append(history[i]).Append("\n\n");
			UiBuilder.SetText(_logText, _text.Length == 0 ? "Nothing has happened yet." : _text.ToString());
		}

		void FillResult()
		{
			if (Game.Phase == CaravanPhase.Victory)
			{
				_resultTitle.text = "THE CARAVAN ARRIVED";
				_resultInfo.text = $"All {Waves.Count} waves held. Caravan health left: {Mathf.RoundToInt(Game.CaravanFraction * 100f)}%.\nJourney time {UiBuilder.FormatTime(Game.JourneyTime)}.";
			}
			else
			{
				_resultTitle.text = "THE CARAVAN IS LOST";
				_resultInfo.text = $"The wagons did not make it.\nWaves cleared: {Game.WavesCleared} of {Waves.Count}.";
			}
			_retry.gameObject.SetActive(!Game.Online);
		}

		string RecordsText()
		{
			var s = Game.Session;
			if (s.Plays == 0) return "No journeys yet.\nYour best results will be kept here.";
			return $"Journeys {s.Plays}  ·  Arrived {s.Wins}\nBest: {s.BestWave} of {Waves.Count} waves  ·  {s.BestHealthPercent}% caravan health";
		}
	}
}
