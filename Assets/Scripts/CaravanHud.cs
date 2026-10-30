using ITKamianets.Engine.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Temnorid.CaravanGuard
{
	/// <summary>All of the game's screens, drawn with plain uGUI built in code. It only shows what <see cref="CaravanGame"/> says.</summary>
	public sealed class CaravanHud : MonoBehaviour
	{
		public CaravanGame Game;

		static readonly Color Panel = new Color(0.07f, 0.06f, 0.04f, 0.82f);
		static readonly Color Gold = new Color(0.95f, 0.75f, 0.25f);

		UiBuilder _ui;
		GameObject _menu, _hud, _pause, _result;
		Text _records, _coins, _wave, _caravanText, _toast, _hint;
		Text _selTitle, _selInfo, _resultTitle, _resultInfo;
		Image _caravanFill;
		GameObject _selection;
		Button _buy, _startButton;
		Text _buyLabel, _startLabel;
		CaravanPhase _shown = (CaravanPhase)(-1);
		bool _shownPaused;

		void Start()
		{
			_ui = new UiBuilder
			{
				ButtonHighlight = new Color(1f, 0.9f, 0.6f),
				ButtonSelected = new Color(1f, 0.85f, 0.45f),
				ButtonPressed = new Color(0.8f, 0.65f, 0.3f),
			};
			UiBuilder.EnsureEventSystem(transform);
			var root = UiBuilder.CreateCanvas(transform).transform;
			BuildMenu(root);
			BuildHud(root);
			BuildPause(root);
			BuildResult(root);
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
				UiBuilder.Select(Game.Paused ? _pause : phase == CaravanPhase.Menu ? _menu : (phase == CaravanPhase.Victory || phase == CaravanPhase.Defeat) ? _result : null);
			}
			if (_hud.activeSelf) UpdateHud();
		}

		// ---- screens ----

		void BuildMenu(Transform root)
		{
			_menu = UiBuilder.Box("StartScreen", root, new Vector2(0.28f, 0.14f), new Vector2(0.72f, 0.88f), Panel);
			_ui.Label("Title", _menu.transform, "TEMNORID CARAVAN GUARD", 50, TextAnchor.MiddleCenter, new Vector2(0f, 0.8f), new Vector2(1f, 0.98f), Gold);
			_ui.Label("Tagline", _menu.transform, "Place the guards. Bring the wagons home.", 30, TextAnchor.MiddleCenter, new Vector2(0.03f, 0.7f), new Vector2(0.97f, 0.8f), Color.white);
			_records = _ui.Label("Records", _menu.transform, RecordsText(), 28, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.46f), new Vector2(0.95f, 0.68f), Color.white);
			_ui.Button("Start journey", _menu.transform, new Vector2(0.25f, 0.28f), new Vector2(0.75f, 0.42f), () => Game.Begin());
			_ui.Button("Quit", _menu.transform, new Vector2(0.25f, 0.12f), new Vector2(0.75f, 0.24f), () => Game.SaveAndQuit());
			_ui.Label("Hint", _menu.transform, "W A S D or the screen edge: camera  ·  wheel: zoom  ·  click a post  ·  Space: start wave", 22, TextAnchor.MiddleCenter, new Vector2(0.02f, 0f), new Vector2(0.98f, 0.1f), new Color(1f, 1f, 1f, 0.7f));
		}

		void BuildHud(Transform root)
		{
			_hud = new GameObject("Hud", typeof(RectTransform));
			_hud.transform.SetParent(root, false);
			var rect = (RectTransform)_hud.transform;
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = rect.offsetMax = Vector2.zero;

			var top = UiBuilder.Box("TopBar", _hud.transform, new Vector2(0.25f, 0.92f), new Vector2(0.75f, 0.995f), Panel);
			_coins = _ui.Label("Coins", top.transform, "", 34, TextAnchor.MiddleLeft, new Vector2(0.03f, 0f), new Vector2(0.3f, 1f), Gold);
			_wave = _ui.Label("Wave", top.transform, "", 34, TextAnchor.MiddleCenter, new Vector2(0.3f, 0f), new Vector2(0.62f, 1f), Color.white);
			_caravanText = _ui.Label("Caravan", top.transform, "", 24, TextAnchor.MiddleRight, new Vector2(0.62f, 0.45f), new Vector2(0.97f, 1f), Color.white);
			_caravanFill = UiBuilder.Bar("Caravan", top.transform, new Vector2(0.64f, 0.12f), new Vector2(0.97f, 0.4f), new Color(0.3f, 0.8f, 0.35f));

			_toast = _ui.Label("Toast", _hud.transform, "", 30, TextAnchor.MiddleCenter, new Vector2(0.15f, 0.84f), new Vector2(0.85f, 0.91f), Color.white);
			_hint = _ui.Label("Hint", _hud.transform, "", 26, TextAnchor.MiddleCenter, new Vector2(0.2f, 0.01f), new Vector2(0.8f, 0.07f), new Color(1f, 1f, 1f, 0.75f));

			_selection = UiBuilder.Box("Selection", _hud.transform, new Vector2(0.76f, 0.3f), new Vector2(0.99f, 0.62f), Panel);
			_selTitle = _ui.Label("Title", _selection.transform, "", 32, TextAnchor.MiddleCenter, new Vector2(0f, 0.74f), new Vector2(1f, 0.98f), Gold);
			_selInfo = _ui.Label("Info", _selection.transform, "", 24, TextAnchor.UpperLeft, new Vector2(0.06f, 0.4f), new Vector2(0.96f, 0.74f), Color.white, wrap: true);
			_buy = _ui.Button("Buy", _selection.transform, new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.36f), () => Game.BuyOrUpgrade());
			_buyLabel = _buy.GetComponentInChildren<Text>();

			_startButton = _ui.Button("Start wave", _hud.transform, new Vector2(0.39f, 0.085f), new Vector2(0.61f, 0.16f), () => Game.StartWave());
			_startLabel = _startButton.GetComponentInChildren<Text>();
			_buyLabel.fontSize = 26;
			_startLabel.fontSize = 28;
		}

		void BuildPause(Transform root)
		{
			_pause = UiBuilder.Box("PauseMenu", root, new Vector2(0.36f, 0.28f), new Vector2(0.64f, 0.72f), Panel);
			_ui.Label("Title", _pause.transform, "PAUSED", 54, TextAnchor.MiddleCenter, new Vector2(0f, 0.7f), new Vector2(1f, 0.96f), Gold);
			_ui.Button("Resume", _pause.transform, new Vector2(0.2f, 0.42f), new Vector2(0.8f, 0.62f), () => Game.SetPaused(false));
			_ui.Button("Restart", _pause.transform, new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.38f), () => Game.Retry());
			_ui.Button("Quit", _pause.transform, new Vector2(0.2f, 0.02f), new Vector2(0.8f, 0.16f), () => Game.SaveAndQuit());
		}

		void BuildResult(Transform root)
		{
			_result = UiBuilder.Box("Result", root, new Vector2(0.3f, 0.25f), new Vector2(0.7f, 0.78f), Panel);
			_resultTitle = _ui.Label("Title", _result.transform, "", 56, TextAnchor.MiddleCenter, new Vector2(0f, 0.72f), new Vector2(1f, 0.97f), Gold);
			_resultInfo = _ui.Label("Info", _result.transform, "", 30, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.4f), new Vector2(0.95f, 0.72f), Color.white, wrap: true);
			_ui.Button("Retry", _result.transform, new Vector2(0.15f, 0.2f), new Vector2(0.85f, 0.34f), () => Game.Retry());
			_ui.Button("Quit", _result.transform, new Vector2(0.15f, 0.04f), new Vector2(0.85f, 0.16f), () => Game.SaveAndQuit());
		}

		// ---- updating ----

		void UpdateHud()
		{
			var session = Game.Session;
			var caravan = Game.Caravan;
			UiBuilder.SetText(_coins, $"Coins {session.Coins}");
			UiBuilder.SetText(_wave, $"Wave {Game.WaveNumber} / {Waves.Count}");
			UiBuilder.SetText(_caravanText, $"Caravan {Mathf.CeilToInt(caravan.Health.Current)} / {Mathf.RoundToInt(caravan.Health.MaxHealth)}");
			UiBuilder.SetFill(_caravanFill, caravan.Health.Fraction);

			_toast.text = Time.unscaledTime < Game.ToastUntil ? Game.ToastText : "";

			var planning = Game.Phase == CaravanPhase.Planning;
			_startButton.gameObject.SetActive(planning);
			if (planning) _startLabel.text = Game.WaveNumber >= Waves.Count && session.WavesCleared == Waves.Count - 1 ? "Final wave (Space)" : $"Start wave {Game.WaveNumber} (Space)";
			_hint.text = planning ? "Click a post to buy or upgrade a guard" : $"Attackers left: {Enemy.All.Count}";

			var post = Game.Selected;
			_selection.SetActive(post != null);
			if (post == null) return;
			if (post.IsEmpty)
			{
				_selTitle.text = "Empty post";
				_selInfo.text = $"A guard here shoots {GuardStats.Damage(1):0} damage every {GuardStats.Interval(1):0.0}s within {GuardStats.Range(1):0} m.";
				_buyLabel.text = $"Buy guard ({GuardStats.BuyCost})  [E]";
				_buy.interactable = session.CanAfford(GuardStats.BuyCost);
			}
			else if (post.CanUpgrade)
			{
				_selTitle.text = "Guard, level " + post.Level;
				_selInfo.text = $"Upgrade: {GuardStats.Damage(post.Level + 1):0} damage every {GuardStats.Interval(post.Level + 1):0.00}s within {GuardStats.Range(post.Level + 1):0} m.";
				_buyLabel.text = $"Upgrade ({GuardStats.UpgradeCost})  [E]";
				_buy.interactable = session.CanAfford(GuardStats.UpgradeCost);
			}
			else
			{
				_selTitle.text = "Guard, level " + post.Level;
				_selInfo.text = $"{GuardStats.Damage(post.Level):0} damage every {GuardStats.Interval(post.Level):0.00}s within {GuardStats.Range(post.Level):0} m. Fully upgraded.";
				_buyLabel.text = "Fully upgraded";
				_buy.interactable = false;
			}
		}

		void FillResult()
		{
			var session = Game.Session;
			if (Game.Phase == CaravanPhase.Victory)
			{
				_resultTitle.text = "THE CARAVAN ARRIVED";
				_resultInfo.text = $"All {Waves.Count} waves held. Caravan health left: {Mathf.RoundToInt(Game.Caravan.Health.Fraction * 100f)}%.\nJourney time {UiBuilder.FormatTime(Game.JourneyTime)}.";
			}
			else
			{
				_resultTitle.text = "THE CARAVAN IS LOST";
				_resultInfo.text = $"The wagons did not make it.\nWaves cleared: {session.WavesCleared} of {Waves.Count}.";
			}
		}

		string RecordsText()
		{
			var s = Game.Session;
			if (s.Plays == 0) return "No journeys yet.\nYour best results will be kept here.";
			return $"Journeys {s.Plays}  ·  Arrived {s.Wins}\nBest: {s.BestWave} of {Waves.Count} waves  ·  {s.BestHealthPercent}% caravan health";
		}
	}
}
