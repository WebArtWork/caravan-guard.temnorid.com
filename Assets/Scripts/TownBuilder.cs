using ITKamianets.Engine.Feedback;
using UnityEngine;

namespace Temnorid.CaravanGuard
{
	/// <summary>
	/// The nearest town, made from primitives: a stone wall with a gate towards the village, towers, houses in two rings round a market square,
	/// and a keep. Nothing to collide with; it is scenery for the traders to walk to.
	/// </summary>
	public static class TownBuilder
	{
		public const float RadiusX = 52f;
		public const float RadiusZ = 40f;

		/// <summary>Builds the town with its middle at <paramref name="centre"/>. The gate is on the west side (towards the village).</summary>
		public static Transform Build(Vector3 centre, Material textMaterial)
		{
			var root = new GameObject("Town").transform;
			root.position = centre;

			var stone = Fx.Material(new Color(0.58f, 0.57f, 0.55f));
			var stoneDark = Fx.Material(new Color(0.45f, 0.44f, 0.45f));
			var wall = Fx.Material(new Color(0.66f, 0.6f, 0.5f));
			var plaster = new[] { Fx.Material(new Color(0.78f, 0.7f, 0.55f)), Fx.Material(new Color(0.72f, 0.62f, 0.5f)), Fx.Material(new Color(0.8f, 0.76f, 0.66f)) };
			var roofs = new[] { Fx.Material(new Color(0.55f, 0.2f, 0.16f)), Fx.Material(new Color(0.4f, 0.28f, 0.2f)), Fx.Material(new Color(0.3f, 0.32f, 0.36f)) };
			var cobble = Fx.Material(new Color(0.5f, 0.48f, 0.46f));
			var flag = Fx.Material(new Color(0.7f, 0.12f, 0.12f));
			var wood = Fx.Material(new Color(0.42f, 0.28f, 0.15f));
			var cloth = new[] { Fx.Material(new Color(0.8f, 0.3f, 0.25f)), Fx.Material(new Color(0.25f, 0.5f, 0.7f)), Fx.Material(new Color(0.85f, 0.75f, 0.3f)) };

			// The ground inside the wall and the market square.
			Piece(PrimitiveType.Cylinder, "Yard", root, new Vector3(0f, 0.04f, 0f), new Vector3(RadiusX * 2f, 0.04f, RadiusZ * 2f), cobble);
			Piece(PrimitiveType.Cylinder, "Square", root, new Vector3(0f, 0.08f, 0f), new Vector3(30f, 0.04f, 24f), stone);

			// The wall: segments round an ellipse, leaving the gate on the west side open.
			const int segments = 56;
			for (var i = 0; i < segments; i++)
			{
				var t0 = 2f * Mathf.PI * i / segments;
				var t1 = 2f * Mathf.PI * (i + 1) / segments;
				var mid = (t0 + t1) * 0.5f;
				if (Mathf.Abs(Mathf.DeltaAngle(mid * Mathf.Rad2Deg, 180f)) < 8f) continue;
				var a = new Vector3(RadiusX * Mathf.Cos(t0), 0f, RadiusZ * Mathf.Sin(t0));
				var b = new Vector3(RadiusX * Mathf.Cos(t1), 0f, RadiusZ * Mathf.Sin(t1));
				var piece = Piece(PrimitiveType.Cube, "Wall", root, (a + b) * 0.5f + Vector3.up * 3f, new Vector3(1.8f, 6f, (b - a).magnitude * 1.04f), wall);
				piece.localRotation = Quaternion.LookRotation(b - a);
			}

			// Towers: the two flanking the gate, and the four on the diagonals.
			var towerAngles = new[] { 172f, 188f, 45f, 135f, 225f, 315f };
			foreach (var degrees in towerAngles)
			{
				var radians = degrees * Mathf.Deg2Rad;
				var at = new Vector3(RadiusX * Mathf.Cos(radians), 0f, RadiusZ * Mathf.Sin(radians));
				Piece(PrimitiveType.Cylinder, "Tower", root, at + Vector3.up * 4.5f, new Vector3(5f, 4.5f, 5f), stone);
				Piece(PrimitiveType.Cylinder, "TowerTop", root, at + Vector3.up * 9.3f, new Vector3(6f, 0.3f, 6f), stoneDark);
				Piece(PrimitiveType.Cube, "TowerRoof", root, at + Vector3.up * 10.4f, new Vector3(3.6f, 1.8f, 3.6f), roofs[0]);
			}

			// Houses in two rings round the square.
			var random = new System.Random(7);
			AddRing(root, 0.40f, 10, plaster, roofs, random);
			AddRing(root, 0.68f, 16, plaster, roofs, random);

			// The keep on the east side, with four turrets and a flag.
			var keep = new Vector3(RadiusX * 0.28f, 0f, 0f);
			Piece(PrimitiveType.Cube, "Keep", root, keep + Vector3.up * 6f, new Vector3(14f, 12f, 12f), stone);
			foreach (var sx in new[] { -1f, 1f })
				foreach (var sz in new[] { -1f, 1f })
					Piece(PrimitiveType.Cylinder, "Turret", root, keep + new Vector3(sx * 7f, 8f, sz * 6f), new Vector3(3.2f, 8f, 3.2f), stoneDark);
			Piece(PrimitiveType.Cube, "Pole", root, keep + new Vector3(0f, 15f, 0f), new Vector3(0.25f, 6f, 0.25f), wood);
			Piece(PrimitiveType.Cube, "Flag", root, keep + new Vector3(1.4f, 17.5f, 0f), new Vector3(2.6f, 1.4f, 0.12f), flag);

			// Market stalls on the square.
			for (var i = 0; i < 5; i++)
			{
				var angle = (i / 5f) * Mathf.PI * 2f + 0.4f;
				var at = new Vector3(Mathf.Cos(angle) * 8.5f - 4f, 0f, Mathf.Sin(angle) * 6f);
				Piece(PrimitiveType.Cube, "StallTable", root, at + Vector3.up * 0.6f, new Vector3(3.2f, 1.2f, 1.6f), wood);
				Piece(PrimitiveType.Cube, "StallCloth", root, at + Vector3.up * 2.6f, new Vector3(3.6f, 0.2f, 2.2f), cloth[i % cloth.Length]);
			}

			// The name above the gate.
			if (textMaterial != null)
			{
				var go = new GameObject("Name");
				go.transform.SetParent(root, false);
				go.transform.localPosition = new Vector3(-RadiusX, 11f, 0f);
				go.AddComponent<TownLabel>();
				var mesh = go.AddComponent<TextMesh>();
				mesh.text = "TOWN";
				mesh.characterSize = 0.5f;
				mesh.fontSize = 64;
				mesh.anchor = TextAnchor.MiddleCenter;
				mesh.alignment = TextAlignment.Center;
				mesh.color = new Color(1f, 0.95f, 0.7f);
				mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
				go.GetComponent<MeshRenderer>().sharedMaterial = textMaterial;
			}
			return root;
		}

		static void AddRing(Transform root, float fraction, int count, Material[] plaster, Material[] roofs, System.Random random)
		{
			for (var i = 0; i < count; i++)
			{
				var angle = (i + 0.5f * (float)random.NextDouble()) / count * Mathf.PI * 2f;
				var x = RadiusX * fraction * Mathf.Cos(angle);
				var z = RadiusZ * fraction * Mathf.Sin(angle);
				// Keep the way from the gate to the square clear.
				if (x < -RadiusX * 0.2f && Mathf.Abs(z) < 7f) continue;
				if (Mathf.Abs(x - RadiusX * 0.28f) < 12f && Mathf.Abs(z) < 10f) continue; // the keep
				var width = 6f + (float)random.NextDouble() * 4f;
				var depth = 6f + (float)random.NextDouble() * 3f;
				var height = 4.2f + (float)random.NextDouble() * 2.6f;
				var yaw = angle * Mathf.Rad2Deg + 90f;
				var house = Piece(PrimitiveType.Cube, "House", root, new Vector3(x, height * 0.5f, z), new Vector3(width, height, depth), plaster[random.Next(plaster.Length)]);
				house.localRotation = Quaternion.Euler(0f, yaw, 0f);
				var roof = Piece(PrimitiveType.Cube, "Roof", root, new Vector3(x, height + 0.7f, z), new Vector3(width + 0.8f, 1.4f, depth + 0.8f), roofs[random.Next(roofs.Length)]);
				roof.localRotation = Quaternion.Euler(0f, yaw, 0f);
			}
		}

		static Transform Piece(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
		{
			var piece = GameObject.CreatePrimitive(type);
			piece.name = name;
			Object.Destroy(piece.GetComponent<Collider>());
			piece.transform.SetParent(parent, false);
			piece.transform.localPosition = localPosition;
			piece.transform.localScale = scale;
			piece.GetComponent<Renderer>().sharedMaterial = material;
			return piece.transform;
		}
	}

	/// <summary>Turns a label towards the camera.</summary>
	public sealed class TownLabel : MonoBehaviour
	{
		void LateUpdate()
		{
			var camera = Camera.main;
			if (camera == null) return;
			var away = transform.position - camera.transform.position;
			if (away.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
		}
	}
}
