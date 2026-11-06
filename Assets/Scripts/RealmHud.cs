using ITKamianets.Engine.Shell;
using ITKamianets.Engine.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Temnorid.CaravanGuard
{
	/// <summary>
	/// The screen of the trade road: the coins and the trade's tally at the top left, what the selected guard post can do at the bottom, and the
	/// news in the middle (a raid, a lost trader). The place name is in the middle of the shell's top bar. Plain uGUI built in code.
	/// </summary>
	public sealed class RealmHud : MonoBehaviour
	{
		public RealmGame Game;

		static readonly Color Panel = new Color(0.06f, 0.05f, 0.09f, 0.78f);
		static readonly Color Gold = new Color(1f, 0.82f, 0.3f);

		UiBuilder _ui;
		ShellChrome _chrome;
		Text _tally, _toast, _post;
		GameObject _buy;
		Text _buyLabel;

		void Start()
		{
			_ui = ShellUi.Builder();
			_ui.ButtonFontSize = 34;
			UiBuilder.EnsureEventSystem(transform);

			_chrome = ShellChrome.Create(transform);
			_chrome.SetTitle("Trade road");

			var canvas = UiBuilder.CreateCanvas(transform, "HudCanvas");
			canvas.sortingOrder = 0;
			var safe = new GameObject("Safe", typeof(RectTransform), typeof(SafeAreaFitter));
			safe.transform.SetParent(canvas.transform, false);
			var hud = new GameObject("Hud", typeof(RectTransform));
			hud.transform.SetParent(safe.transform, false);
			var rect = (RectTransform)hud.transform;
			rect.anchorMin = new Vector2(0f, _chrome.BottomFraction);
			rect.anchorMax = new Vector2(1f, 1f - _chrome.TopFraction);
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
			var root = hud.transform;

			var panel = UiBuilder.Box("Tally", root, new Vector2(0.015f, 0.84f), new Vector2(0.27f, 0.985f), Panel);
			panel.GetComponent<Image>().raycastTarget = false;
			_tally = _ui.Label("Text", panel.transform, "", 30, TextAnchor.MiddleLeft, new Vector2(0.06f, 0.05f), new Vector2(0.96f, 0.95f), Color.white);

			_toast = _ui.Label("Toast", root, "", 40, TextAnchor.MiddleCenter, new Vector2(0.15f, 0.78f), new Vector2(0.85f, 0.88f), Gold);

			_post = _ui.Label("Post", root, "", 32, TextAnchor.LowerCenter, new Vector2(0.25f, 0.115f), new Vector2(0.75f, 0.2f), Color.white);
			var button = _ui.Button("Buy", root, new Vector2(0.38f, 0.02f), new Vector2(0.62f, 0.105f), () => Game.BuyOrUpgrade());
			_buy = button.gameObject;
			_buyLabel = button.GetComponentInChildren<Text>();
			_buy.SetActive(false);

			_ui.Label("Hint", root, "WASD or the screen edge: move  ·  wheel: zoom  ·  click a guard post, then E", 24, TextAnchor.LowerLeft, new Vector2(0.015f, 0.01f), new Vector2(0.5f, 0.06f), new Color(1f, 1f, 1f, 0.7f));
		}

		void Update()
		{
			if (Game == null || _tally == null) return;
			SetText(_tally, $"Coins  {Game.Session.Coins}\nDelivered  {Game.Delivered}    Lost  {Game.Lost}");
			SetText(_toast, Time.unscaledTime < Game.ToastUntil ? Game.ToastText : "");

			var post = Game.Selected;
			_buy.SetActive(post != null && (post.IsEmpty || post.CanUpgrade));
			if (post == null)
			{
				SetText(_post, "");
				return;
			}
			if (post.IsEmpty)
			{
				SetText(_post, "An empty guard post");
				SetText(_buyLabel, $"Buy guard  ({GuardStats.BuyCost})");
			}
			else
			{
				SetText(_post, $"Guard, level {post.Level}  ·  reach {post.Range:0} m");
				SetText(_buyLabel, post.CanUpgrade ? $"Upgrade  ({GuardStats.UpgradeCost})" : "");
			}
		}

		static void SetText(Text label, string value)
		{
			if (label != null && label.text != value) label.text = value;
		}
	}
}
