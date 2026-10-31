using System.Collections.Generic;
using ITKamianets.Engine.TopDown;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Temnorid.CaravanGuard
{
	/// <summary>
	/// The overhead camera's touch route, kept in the game because the camera package only reads keys, the screen edge and the wheel:
	/// drag with one finger to move the map, pinch to zoom, tap a post to select it. Mouse and keyboard play as before.
	/// </summary>
	public sealed class CaravanTouchView : MonoBehaviour
	{
		const int MaxFingers = 10;

		CaravanGame _game;
		OverheadCamera _view;
		readonly TouchGesture _gesture = new TouchGesture();
		readonly List<TouchPoint> _points = new List<TouchPoint>();
		readonly Vector2[] _last = new Vector2[MaxFingers];
		readonly List<RaycastResult> _hits = new List<RaycastResult>();

		public void Init(CaravanGame game, OverheadCamera view)
		{
			_game = game;
			_view = view;
			_gesture.Blocked = OverUi;
			_gesture.SlopPixels = Mathf.Max(14f, (Screen.dpi > 0f ? Screen.dpi : 160f) * 0.1f);
		}

		bool OverUi(Vector2 position)
		{
			if (EventSystem.current == null) return false;
			_hits.Clear();
			EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, _hits);
			return _hits.Count > 0;
		}

		void Update()
		{
			var screen = Touchscreen.current;
			if (screen == null || _view == null) return;
			_points.Clear();
			var touches = screen.touches;
			for (var i = 0; i < touches.Count && i < MaxFingers; i++)
			{
				var press = touches[i].press;
				var pressed = press.isPressed;
				var ended = press.wasReleasedThisFrame;
				if (!pressed && !ended) continue;
				if (pressed) _last[i] = touches[i].position.ReadValue();
				_points.Add(new TouchPoint { Id = i, Position = _last[i], Began = press.wasPressedThisFrame, Ended = ended && !pressed });
			}
			if (_points.Count == 0 && !_gesture.Panning) return;

			_gesture.Feed(Time.unscaledTime, _points);
			var playing = _game.Phase == CaravanPhase.Planning || _game.Phase == CaravanPhase.Wave;
			if (!playing || _game.Paused) return;

			if (_gesture.Tapped) _game.Tap(_gesture.TapPosition);
			if (_gesture.PinchRatio != 1f) _view.Distance = Mathf.Clamp(_view.Distance * _gesture.PinchRatio, _view.MinDistance, _view.MaxDistance);
			if (_gesture.Panning) Pan(_gesture.PanFrom, _gesture.PanTo);
		}

		// The ground under the finger stays under the finger: move the focus by how far the ground point moved.
		void Pan(Vector2 from, Vector2 to)
		{
			if (!_view.TryGroundPoint(from, out var a) || !_view.TryGroundPoint(to, out var b)) return;
			var focus = _view.Focus + (a - b);
			focus.x = Mathf.Clamp(focus.x, _view.Bounds.xMin, _view.Bounds.xMax);
			focus.z = Mathf.Clamp(focus.z, _view.Bounds.yMin, _view.Bounds.yMax);
			focus.y = 0f;
			_view.Focus = focus;
		}
	}
}
