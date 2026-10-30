using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Feedback;
using ITKamianets.Engine.Graybox;
using UnityEngine;

namespace Temnorid.CaravanGuard
{
	/// <summary>Makes attackers from primitives at run time. Every one carries the asset id of the model that will replace it.</summary>
	public static class EnemyFactory
	{
		public static Enemy Create(EnemyKind kind, Vector3 position, Transform parent = null)
		{
			var stats = Enemies.For(kind);
			var root = new GameObject(stats.Name);
			if (parent != null) root.transform.SetParent(parent, false);
			root.transform.position = position;
			root.AddComponent<AssetIdTag>().Id = stats.AssetId;

			var capsule = root.AddComponent<CapsuleCollider>();
			capsule.radius = 0.45f;
			capsule.height = kind == EnemyKind.Wolf ? 0.9f : 1.8f;
			capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);

			root.AddComponent<Health>();
			var enemy = root.AddComponent<Enemy>();
			enemy.Setup(kind);

			switch (kind)
			{
				case EnemyKind.Wolf: BuildWolf(root.transform); break;
				case EnemyKind.Rider: BuildRider(root.transform); break;
				default: BuildRaider(root.transform); break;
			}
			var bar = root.AddComponent<HealthBar3D>();
			bar.Height = kind == EnemyKind.Rider ? 2.9f : kind == EnemyKind.Wolf ? 1.3f : 2.2f;
			bar.Width = kind == EnemyKind.Rider ? 1.4f : 0.9f;
			return enemy;
		}

		static void BuildRaider(Transform root)
		{
			var cloth = Fx.Material(new Color(0.55f, 0.18f, 0.14f));
			var skin = Fx.Material(new Color(0.85f, 0.65f, 0.5f));
			var steel = Fx.Material(new Color(0.7f, 0.72f, 0.78f));
			Piece(PrimitiveType.Capsule, "Body", root, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), cloth);
			Piece(PrimitiveType.Sphere, "Head", root, new Vector3(0f, 1.7f, 0.02f), new Vector3(0.38f, 0.38f, 0.38f), skin);
			Piece(PrimitiveType.Cube, "Sabre", root, new Vector3(0.5f, 1.0f, 0.4f), new Vector3(0.07f, 0.07f, 0.9f), steel);
		}

		static void BuildWolf(Transform root)
		{
			var fur = Fx.Material(new Color(0.45f, 0.45f, 0.5f));
			var dark = Fx.Material(new Color(0.12f, 0.1f, 0.12f));
			Piece(PrimitiveType.Capsule, "Body", root, new Vector3(0f, 0.55f, 0f), new Vector3(0.5f, 0.7f, 0.5f), fur).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
			Piece(PrimitiveType.Sphere, "Head", root, new Vector3(0f, 0.7f, 0.75f), new Vector3(0.38f, 0.34f, 0.45f), fur);
			Piece(PrimitiveType.Cube, "Tail", root, new Vector3(0f, 0.65f, -0.8f), new Vector3(0.1f, 0.1f, 0.5f), dark);
			foreach (var x in new[] { -0.18f, 0.18f })
				foreach (var z in new[] { -0.45f, 0.45f })
					Piece(PrimitiveType.Cube, "Leg", root, new Vector3(x, 0.2f, z), new Vector3(0.1f, 0.4f, 0.1f), dark);
		}

		static void BuildRider(Transform root)
		{
			var horse = Fx.Material(new Color(0.4f, 0.26f, 0.14f));
			var cloth = Fx.Material(new Color(0.2f, 0.3f, 0.5f));
			var skin = Fx.Material(new Color(0.85f, 0.65f, 0.5f));
			var dark = Fx.Material(new Color(0.12f, 0.1f, 0.12f));
			Piece(PrimitiveType.Cube, "HorseBody", root, new Vector3(0f, 1.0f, 0f), new Vector3(0.7f, 0.75f, 1.9f), horse);
			Piece(PrimitiveType.Cube, "HorseNeck", root, new Vector3(0f, 1.45f, 1.0f), new Vector3(0.35f, 0.9f, 0.4f), horse).transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
			Piece(PrimitiveType.Cube, "HorseHead", root, new Vector3(0f, 1.8f, 1.35f), new Vector3(0.3f, 0.3f, 0.6f), horse);
			foreach (var x in new[] { -0.22f, 0.22f })
				foreach (var z in new[] { -0.7f, 0.7f })
					Piece(PrimitiveType.Cube, "Leg", root, new Vector3(x, 0.35f, z), new Vector3(0.14f, 0.7f, 0.14f), dark);
			Piece(PrimitiveType.Capsule, "Rider", root, new Vector3(0f, 1.85f, -0.1f), new Vector3(0.5f, 0.55f, 0.5f), cloth);
			Piece(PrimitiveType.Sphere, "Head", root, new Vector3(0f, 2.4f, -0.08f), new Vector3(0.32f, 0.32f, 0.32f), skin);
		}

		static GameObject Piece(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
		{
			var piece = GameObject.CreatePrimitive(type);
			piece.name = name;
			Object.Destroy(piece.GetComponent<Collider>());
			piece.transform.SetParent(parent, false);
			piece.transform.localPosition = position;
			piece.transform.localScale = scale;
			piece.GetComponent<Renderer>().sharedMaterial = material;
			return piece;
		}
	}
}
