using System;
using System.Collections.Generic;
using UnityEngine;

namespace Temnorid.CaravanGuard
{
	/// <summary>One finger as the gesture reader sees it in one frame: where it is, and whether it just touched down or just lifted.</summary>
	public struct TouchPoint
	{
		public int Id;
		public Vector2 Position;
		public bool Began;
		public bool Ended;
	}

	/// <summary>
	/// Tells what the fingers on the screen mean, with no Unity input in it so it can be tested: a short touch that hardly moves is a
	/// <see cref="Tapped"/>, one finger that moves drags the map (<see cref="PanFrom"/> to <see cref="PanTo"/>), two fingers pinch
	/// (<see cref="PinchRatio"/> is the old distance between them over the new one, so below 1 means spread, zoom in) and drag together.
	/// A touch that starts on a button (<see cref="Blocked"/>) is left to the button.
	/// </summary>
	public sealed class TouchGesture
	{
		public float TapSeconds = 0.5f;
		/// <summary>How far in pixels a finger may wander and still be a tap.</summary>
		public float SlopPixels = 20f;
		public Func<Vector2, bool> Blocked;

		public bool Tapped { get; private set; }
		public Vector2 TapPosition { get; private set; }
		public bool Panning { get; private set; }
		public Vector2 PanFrom { get; private set; }
		public Vector2 PanTo { get; private set; }
		public float PinchRatio { get; private set; } = 1f;

		bool _single;
		bool _multi;
		bool _blocked;
		bool _moved;
		int _id;
		float _startTime;
		Vector2 _start;
		Vector2 _previous;
		float _previousDistance;
		Vector2 _previousMid;
		bool _haveMulti;

		/// <summary>Feeds one frame: the fingers that are down, and those that just lifted.</summary>
		public void Feed(float time, IReadOnlyList<TouchPoint> touches)
		{
			Tapped = false;
			Panning = false;
			PinchRatio = 1f;

			var down = 0;
			foreach (var touch in touches) if (!touch.Ended) down++;

			foreach (var touch in touches)
			{
				if (!touch.Began) continue;
				if (touches.Count == 1 && !_multi)
				{
					_single = true;
					_id = touch.Id;
					_start = _previous = touch.Position;
					_startTime = time;
					_moved = false;
					_blocked = Blocked != null && Blocked(touch.Position);
				}
				else
				{
					_multi = true;
					_single = false;
					_haveMulti = false;
				}
			}

			if (_multi)
			{
				Pinch(touches, down);
			}
			else if (_single && !_blocked)
			{
				foreach (var touch in touches)
				{
					if (touch.Id != _id) continue;
					if (touch.Ended)
					{
						if (!_moved && time - _startTime <= TapSeconds)
						{
							Tapped = true;
							TapPosition = touch.Position;
						}
						_single = false;
						break;
					}
					if (!_moved && (touch.Position - _start).magnitude > SlopPixels)
					{
						_moved = true;
						_previous = _start;
					}
					if (_moved)
					{
						Panning = true;
						PanFrom = _previous;
						PanTo = touch.Position;
						_previous = touch.Position;
					}
				}
			}

			if (down == 0)
			{
				_single = false;
				_multi = false;
				_haveMulti = false;
			}
		}

		void Pinch(IReadOnlyList<TouchPoint> touches, int down)
		{
			if (down < 2)
			{
				_haveMulti = false;
				return;
			}
			var first = -1;
			var second = -1;
			for (var i = 0; i < touches.Count; i++)
			{
				if (touches[i].Ended) continue;
				if (first < 0) first = i;
				else if (second < 0) second = i;
			}
			var a = touches[first].Position;
			var b = touches[second].Position;
			var distance = (a - b).magnitude;
			var mid = (a + b) * 0.5f;
			if (_haveMulti && distance > 1f)
			{
				PinchRatio = _previousDistance / distance;
				Panning = true;
				PanFrom = _previousMid;
				PanTo = mid;
			}
			_previousDistance = distance;
			_previousMid = mid;
			_haveMulti = true;
		}
	}
}
