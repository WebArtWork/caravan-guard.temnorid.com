using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Temnorid.CaravanGuard.Tests
{
	public class TouchGestureTests
	{
		static TouchPoint P(int id, float x, float y, bool began = false, bool ended = false) => new TouchPoint { Id = id, Position = new Vector2(x, y), Began = began, Ended = ended };

		[Test]
		public void A_short_touch_that_stays_put_is_a_tap()
		{
			var g = new TouchGesture();
			g.Feed(0f, new List<TouchPoint> { P(0, 100, 100, began: true) });
			Assert.IsFalse(g.Tapped);
			g.Feed(0.1f, new List<TouchPoint> { P(0, 102, 101) });
			g.Feed(0.2f, new List<TouchPoint> { P(0, 102, 101, ended: true) });
			Assert.IsTrue(g.Tapped);
			Assert.AreEqual(new Vector2(102, 101), g.TapPosition);
		}

		[Test]
		public void A_touch_that_down_and_up_in_one_frame_is_a_tap()
		{
			var g = new TouchGesture();
			g.Feed(0f, new List<TouchPoint> { P(0, 50, 60, began: true, ended: true) });
			Assert.IsTrue(g.Tapped);
		}

		[Test]
		public void A_long_press_is_not_a_tap()
		{
			var g = new TouchGesture();
			g.Feed(0f, new List<TouchPoint> { P(0, 100, 100, began: true) });
			g.Feed(2f, new List<TouchPoint> { P(0, 100, 100, ended: true) });
			Assert.IsFalse(g.Tapped);
		}

		[Test]
		public void One_finger_that_moves_drags_the_map_and_is_not_a_tap()
		{
			var g = new TouchGesture();
			g.Feed(0f, new List<TouchPoint> { P(0, 100, 100, began: true) });
			g.Feed(0.05f, new List<TouchPoint> { P(0, 160, 100) });
			Assert.IsTrue(g.Panning);
			Assert.AreEqual(new Vector2(100, 100), g.PanFrom);
			Assert.AreEqual(new Vector2(160, 100), g.PanTo);
			g.Feed(0.1f, new List<TouchPoint> { P(0, 200, 100) });
			Assert.AreEqual(new Vector2(160, 100), g.PanFrom);
			g.Feed(0.15f, new List<TouchPoint> { P(0, 200, 100, ended: true) });
			Assert.IsFalse(g.Tapped);
		}

		[Test]
		public void Two_fingers_pinch_and_do_not_tap()
		{
			var g = new TouchGesture();
			g.Feed(0f, new List<TouchPoint> { P(0, 100, 100, began: true) });
			g.Feed(0.01f, new List<TouchPoint> { P(0, 100, 100), P(1, 200, 100, began: true) });
			Assert.AreEqual(1f, g.PinchRatio, 0.001f);
			g.Feed(0.05f, new List<TouchPoint> { P(0, 50, 100), P(1, 250, 100) });
			Assert.AreEqual(0.5f, g.PinchRatio, 0.001f, "fingers spread to twice the distance: the camera comes closer");
			g.Feed(0.1f, new List<TouchPoint> { P(0, 50, 100, ended: true), P(1, 250, 100) });
			g.Feed(0.15f, new List<TouchPoint> { P(1, 250, 100, ended: true) });
			Assert.IsFalse(g.Tapped);
		}

		[Test]
		public void A_touch_that_starts_on_a_button_is_left_alone()
		{
			var g = new TouchGesture { Blocked = p => p.x < 50 };
			g.Feed(0f, new List<TouchPoint> { P(0, 10, 10, began: true) });
			g.Feed(0.05f, new List<TouchPoint> { P(0, 90, 10) });
			Assert.IsFalse(g.Panning);
			g.Feed(0.1f, new List<TouchPoint> { P(0, 90, 10, ended: true) });
			Assert.IsFalse(g.Tapped);
		}
	}
}
