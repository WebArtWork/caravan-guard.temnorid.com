using System;
using System.Collections.Generic;
using UnityEngine;

namespace Temnorid.CaravanGuard
{
	/// <summary>The road as a line of points on the ground: where the caravan is at a given distance, and how far along a spot is.</summary>
	public sealed class RoadPath
	{
		readonly Vector3[] _points;
		readonly float[] _distance;

		public int Count => _points.Length;
		public float Length { get; }

		public RoadPath(IList<Vector3> points)
		{
			if (points == null || points.Count < 2) throw new ArgumentException("A road needs at least two points.");
			_points = new Vector3[points.Count];
			_distance = new float[points.Count];
			for (var i = 0; i < points.Count; i++)
			{
				_points[i] = new Vector3(points[i].x, 0f, points[i].z);
				if (i > 0) _distance[i] = _distance[i - 1] + Vector3.Distance(_points[i - 1], _points[i]);
			}
			Length = _distance[_distance.Length - 1];
		}

		public Vector3 Point(int index) => _points[index];

		/// <summary>The point on the road <paramref name="distance"/> metres from its start (clamped to the road).</summary>
		public Vector3 PointAt(float distance)
		{
			var segment = SegmentAt(distance);
			var length = _distance[segment + 1] - _distance[segment];
			var t = length > 0f ? Mathf.Clamp01((distance - _distance[segment]) / length) : 0f;
			return Vector3.Lerp(_points[segment], _points[segment + 1], t);
		}

		/// <summary>The direction of travel at <paramref name="distance"/>.</summary>
		public Vector3 DirectionAt(float distance)
		{
			var segment = SegmentAt(distance);
			return (_points[segment + 1] - _points[segment]).normalized;
		}

		/// <summary>How many metres along the road the nearest point to <paramref name="position"/> is.</summary>
		public float Project(Vector3 position)
		{
			var best = 0f;
			var bestSqr = float.MaxValue;
			for (var i = 0; i < _points.Length - 1; i++)
			{
				var a = _points[i];
				var ab = _points[i + 1] - a;
				var t = Mathf.Clamp01(Vector3.Dot(new Vector3(position.x, 0f, position.z) - a, ab) / ab.sqrMagnitude);
				var closest = a + ab * t;
				var sqr = (new Vector3(position.x, 0f, position.z) - closest).sqrMagnitude;
				if (sqr < bestSqr)
				{
					bestSqr = sqr;
					best = _distance[i] + ab.magnitude * t;
				}
			}
			return best;
		}

		int SegmentAt(float distance)
		{
			for (var i = 0; i < _points.Length - 2; i++)
				if (distance < _distance[i + 1]) return i;
			return _points.Length - 2;
		}
	}
}
