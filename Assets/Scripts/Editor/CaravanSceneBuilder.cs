using System.Collections.Generic;
using ITKamianets.Engine.Combat;
using ITKamianets.Engine.Graybox;
using ITKamianets.Engine.Graybox.EditorTools;
using ITKamianets.Engine.TopDown;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Temnorid.CaravanGuard.EditorTools
{
	/// <summary>
	/// Builds the whole map from Unity primitives: grass, one winding road with a gate at its end, ten guard
	/// posts beside it, attacker spawn points, the wagons, trees and rocks, the overhead camera and the screens.
	/// Run it from the menu (Temnorid > CaravanGuard > Build Scene) or the command line
	/// (<c>-executeMethod Temnorid.CaravanGuard.EditorTools.CaravanSceneBuilder.Build</c>); it rebuilds
	/// <c>Assets/Scenes/Main.unity</c> from scratch each time, so do not edit that scene by hand.
	/// </summary>
	public static class CaravanSceneBuilder
	{
		const string ScenePath = "Assets/Scenes/Main.unity";
		const string MaterialDir = "Assets/Materials";

		static readonly Vector3[] RoadPoints =
		{
			new Vector3(-62f, 0f, -34f), new Vector3(-40f, 0f, -34f), new Vector3(-24f, 0f, -14f), new Vector3(-2f, 0f, -12f),
			new Vector3(14f, 0f, 8f), new Vector3(38f, 0f, 10f), new Vector3(52f, 0f, 30f), new Vector3(66f, 0f, 44f),
		};

		static readonly float[] PadDistances = { 20f, 36f, 52f, 68f, 84f, 100f, 116f, 132f, 146f, 158f };
		static readonly float[] SpawnDistances = { 30f, 60f, 90f, 120f, 150f };

		[MenuItem("Temnorid/CaravanGuard/Build Scene")]
		public static void Build()
		{
			PlayerSettings.productName = "Temnorid Caravan Guard";
			PlayerSettings.companyName = "Temnorid";
			PlayerSettings.bundleVersion = "0.1.0";
			PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.itkamianets.caravanguard");
			PlayerSettings.runInBackground = true;
			PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;

			var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
			RenderSettings.ambientLight = new Color(0.55f, 0.56f, 0.6f);

			var road = new RoadPath(RoadPoints);
			var points = BuildTerrain(road, out var roadPointTransforms);
			var pads = BuildPads(road);
			var spawns = BuildSpawns(road);
			BuildDecor(road, pads, spawns);
			var caravan = BuildCaravan(road);
			var view = BuildCameraAndLight();

			var gameObject = new GameObject("Game");
			var game = gameObject.AddComponent<CaravanGame>();
			game.View = view;
			game.Caravan = caravan;
			game.RoadPoints = roadPointTransforms;
			game.Pads = pads;
			game.Spawns = spawns;
			game.EnemyRoot = new GameObject("Attackers").transform;
			var hud = gameObject.AddComponent<CaravanHud>();
			hud.Game = game;

			EditorSceneManager.SaveScene(scene, ScenePath);
			EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
			AssetDatabase.SaveAssets();
			Debug.Log("[CaravanGuard] Scene built: " + ScenePath + " (road " + road.Length.ToString("0") + " m, " + pads.Length + " posts, " + points + ")");
		}

		// ---- terrain, road, gate ----

		static string BuildTerrain(RoadPath road, out Transform[] roadPointTransforms)
		{
			var grass = Mat("Grass", new Color(0.36f, 0.5f, 0.27f));
			var sand = Mat("Road", new Color(0.66f, 0.55f, 0.38f));
			var stone = Mat("Stone", new Color(0.55f, 0.54f, 0.56f));
			var wood = Mat("Wood", new Color(0.45f, 0.3f, 0.16f));
			var banner = Mat("Banner", new Color(0.7f, 0.15f, 0.12f));

			var ground = Box(null, "Ground", new Vector3(0f, -0.25f, 5f), new Vector3(180f, 0.5f, 140f), grass);
			ground.AddComponent<AssetIdTag>().Id = "terrain/grass";

			var roadRoot = new GameObject("Road").transform;
			roadRoot.gameObject.AddComponent<AssetIdTag>().Id = "props/road";
			roadPointTransforms = new Transform[road.Count];
			for (var i = 0; i < road.Count; i++)
			{
				var point = new GameObject("RoadPoint" + i).transform;
				point.SetParent(roadRoot, false);
				point.position = road.Point(i);
				roadPointTransforms[i] = point;
				Prim(PrimitiveType.Cylinder, "Joint" + i, roadRoot, road.Point(i) + Vector3.up * 0.05f, new Vector3(5.2f, 0.05f, 5.2f), sand);
				if (i == road.Count - 1) continue;
				var a = road.Point(i);
				var b = road.Point(i + 1);
				var segment = Box(roadRoot, "Segment" + i, (a + b) * 0.5f + Vector3.up * 0.05f, new Vector3(5.2f, 0.1f, Vector3.Distance(a, b)), sand);
				segment.transform.rotation = Quaternion.LookRotation(b - a);
				Object.DestroyImmediate(segment.GetComponent<Collider>());
			}

			// The market gate at the end of the road.
			var end = road.Point(road.Count - 1);
			var direction = road.DirectionAt(road.Length);
			var gate = new GameObject("MarketGate").transform;
			gate.position = end + direction * 5f;
			gate.rotation = Quaternion.LookRotation(direction);
			gate.gameObject.AddComponent<AssetIdTag>().Id = "props/market-gate";
			Prim(PrimitiveType.Cube, "PillarL", gate, new Vector3(-4.2f, 3f, 0f), new Vector3(1.4f, 6f, 1.4f), stone);
			Prim(PrimitiveType.Cube, "PillarR", gate, new Vector3(4.2f, 3f, 0f), new Vector3(1.4f, 6f, 1.4f), stone);
			Prim(PrimitiveType.Cube, "Lintel", gate, new Vector3(0f, 6.4f, 0f), new Vector3(10.6f, 1.2f, 1.6f), stone);
			Prim(PrimitiveType.Cube, "Banner", gate, new Vector3(0f, 4.8f, -0.5f), new Vector3(5f, 1.8f, 0.1f), banner);
			Prim(PrimitiveType.Cube, "Pole", gate, new Vector3(0f, 5.7f, -0.45f), new Vector3(5.4f, 0.12f, 0.12f), wood);
			return road.Count + " road points";
		}

		// ---- guard posts, attacker spawns ----

		static GuardPost[] BuildPads(RoadPath road)
		{
			var stoneDark = Mat("StoneDark", new Color(0.42f, 0.41f, 0.44f));
			var marker = Mat("PadMark", new Color(0.85f, 0.75f, 0.35f));
			var wood = Mat("Wood", new Color(0.45f, 0.3f, 0.16f));
			var stone = Mat("Stone", new Color(0.55f, 0.54f, 0.56f));
			var cloth = Mat("Cloth", new Color(0.2f, 0.4f, 0.75f));
			var gold = Mat("Gold", new Color(1f, 0.8f, 0.2f));
			var skin = Mat("Skin", new Color(0.88f, 0.7f, 0.55f));
			var glow = Mat("Highlight", new Color(1f, 0.95f, 0.4f));

			var root = new GameObject("GuardPosts").transform;
			var list = new List<GuardPost>();
			for (var i = 0; i < PadDistances.Length; i++)
			{
				var side = i % 2 == 0 ? 1f : -1f;
				var direction = road.DirectionAt(PadDistances[i]);
				var right = Vector3.Cross(Vector3.up, direction);
				var position = road.PointAt(PadDistances[i]) + right * side * 6.5f;

				var pad = new GameObject("Post_" + (i + 1));
				pad.transform.SetParent(root, false);
				pad.transform.position = position;
				pad.AddComponent<AssetIdTag>().Id = "props/guard-post";
				var collider = pad.AddComponent<CapsuleCollider>();
				collider.radius = 2.2f;
				collider.height = 5f;
				collider.center = new Vector3(0f, 2.5f, 0f);
				var post = pad.AddComponent<GuardPost>();

				var empty = new GameObject("Empty").transform;
				empty.SetParent(pad.transform, false);
				Part(PrimitiveType.Cylinder, "Disc", empty, new Vector3(0f, 0.08f, 0f), new Vector3(3.6f, 0.08f, 3.6f), stoneDark);
				Part(PrimitiveType.Cube, "MarkA", empty, new Vector3(0f, 0.18f, 0f), new Vector3(1.6f, 0.06f, 0.3f), marker);
				Part(PrimitiveType.Cube, "MarkB", empty, new Vector3(0f, 0.18f, 0f), new Vector3(0.3f, 0.06f, 1.6f), marker);

				var level1 = new GameObject("Level1").transform;
				level1.SetParent(pad.transform, false);
				Part(PrimitiveType.Cylinder, "Base", level1, new Vector3(0f, 0.08f, 0f), new Vector3(3.6f, 0.08f, 3.6f), stoneDark);
				Part(PrimitiveType.Cylinder, "Tower", level1, new Vector3(0f, 1.4f, 0f), new Vector3(1.8f, 1.4f, 1.8f), wood);
				Part(PrimitiveType.Cylinder, "Platform", level1, new Vector3(0f, 2.9f, 0f), new Vector3(2.5f, 0.1f, 2.5f), wood);
				var archer = Part(PrimitiveType.Capsule, "Archer", level1, new Vector3(0f, 3.7f, 0f), new Vector3(0.6f, 0.7f, 0.6f), cloth);
				archer.AddComponent<AssetIdTag>().Id = "units/archer-guard";
				Part(PrimitiveType.Sphere, "Head", level1, new Vector3(0f, 4.55f, 0f), new Vector3(0.36f, 0.36f, 0.36f), skin);

				var level2 = new GameObject("Level2").transform;
				level2.SetParent(pad.transform, false);
				Part(PrimitiveType.Cylinder, "Base", level2, new Vector3(0f, 0.08f, 0f), new Vector3(3.6f, 0.08f, 3.6f), stoneDark);
				Part(PrimitiveType.Cylinder, "Tower", level2, new Vector3(0f, 1.9f, 0f), new Vector3(2.0f, 1.9f, 2.0f), stone);
				Part(PrimitiveType.Cylinder, "Platform", level2, new Vector3(0f, 3.9f, 0f), new Vector3(2.9f, 0.12f, 2.9f), stone);
				foreach (var corner in new[] { new Vector2(-1.2f, -1.2f), new Vector2(1.2f, -1.2f), new Vector2(-1.2f, 1.2f), new Vector2(1.2f, 1.2f) })
					Part(PrimitiveType.Cube, "Merlon", level2, new Vector3(corner.x, 4.2f, corner.y), new Vector3(0.5f, 0.6f, 0.5f), stone);
				var archer2 = Part(PrimitiveType.Capsule, "Archer", level2, new Vector3(0f, 4.7f, 0f), new Vector3(0.6f, 0.7f, 0.6f), cloth);
				archer2.AddComponent<AssetIdTag>().Id = "units/archer-guard";
				Part(PrimitiveType.Sphere, "Head", level2, new Vector3(0f, 5.55f, 0f), new Vector3(0.36f, 0.36f, 0.36f), skin);
				Part(PrimitiveType.Cube, "Pole", level2, new Vector3(0f, 5.5f, 1.1f), new Vector3(0.08f, 2.2f, 0.08f), wood);
				Part(PrimitiveType.Cube, "Flag", level2, new Vector3(0.4f, 6.2f, 1.1f), new Vector3(0.8f, 0.5f, 0.05f), gold);

				var highlight = new GameObject("Highlight").transform;
				highlight.SetParent(pad.transform, false);
				Part(PrimitiveType.Cylinder, "Ring", highlight, new Vector3(0f, 0.04f, 0f), new Vector3(4.6f, 0.03f, 4.6f), glow);

				post.EmptyMarker = empty.gameObject;
				post.LevelVisuals = new[] { level1.gameObject, level2.gameObject };
				post.Highlight = highlight.gameObject;
				post.MuzzleHeight = 4.2f;
				list.Add(post);
			}
			return list.ToArray();
		}

		static SpawnPoint[] BuildSpawns(RoadPath road)
		{
			var flag = Mat("Banner", new Color(0.7f, 0.15f, 0.12f));
			var wood = Mat("Wood", new Color(0.45f, 0.3f, 0.16f));
			var root = new GameObject("Spawns").transform;
			var list = new List<SpawnPoint>();
			foreach (var distance in SpawnDistances)
			{
				var right = Vector3.Cross(Vector3.up, road.DirectionAt(distance));
				foreach (var side in new[] { 1f, -1f })
				{
					var position = road.PointAt(distance) + right * side * 38f;
					position.x = Mathf.Clamp(position.x, -78f, 78f);
					position.z = Mathf.Clamp(position.z, -56f, 64f);
					var spawn = new GameObject("Spawn_" + list.Count);
					spawn.transform.SetParent(root, false);
					spawn.transform.position = position;
					var point = spawn.AddComponent<SpawnPoint>();
					point.RoadDistance = road.Project(position);
					Part(PrimitiveType.Cube, "Pole", spawn.transform, new Vector3(0f, 2f, 0f), new Vector3(0.15f, 4f, 0.15f), wood);
					Part(PrimitiveType.Cube, "Flag", spawn.transform, new Vector3(0.5f, 3.4f, 0f), new Vector3(1f, 0.7f, 0.05f), flag);
					list.Add(point);
				}
			}
			return list.ToArray();
		}

		// ---- trees, rocks ----

		static void BuildDecor(RoadPath road, GuardPost[] pads, SpawnPoint[] spawns)
		{
			var trunk = Mat("Wood", new Color(0.45f, 0.3f, 0.16f));
			var leaves = Mat("Leaves", new Color(0.2f, 0.42f, 0.2f));
			var rock = Mat("Rock", new Color(0.5f, 0.5f, 0.52f));
			var root = new GameObject("Decor").transform;
			var random = new System.Random(7);

			var trees = 0;
			var rocks = 0;
			for (var attempt = 0; attempt < 400 && (trees < 30 || rocks < 12); attempt++)
			{
				var position = new Vector3((float)(random.NextDouble() * 150 - 75), 0f, (float)(random.NextDouble() * 112 - 50));
				if (Vector3.Distance(position, road.PointAt(road.Project(position))) < 9f) continue;
				var blocked = false;
				foreach (var pad in pads) if (Vector3.Distance(pad.transform.position, position) < 6f) blocked = true;
				foreach (var spawn in spawns) if (Vector3.Distance(spawn.transform.position, position) < 5f) blocked = true;
				if (blocked) continue;

				if (trees < 30 && (rocks >= 12 || random.NextDouble() < 0.7))
				{
					var tree = new GameObject("Tree_" + trees).transform;
					tree.SetParent(root, false);
					tree.position = position;
					tree.gameObject.AddComponent<AssetIdTag>().Id = "resources/wood-node";
					var height = 3.4f + (float)random.NextDouble() * 1.6f;
					Part(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0f, height * 0.4f, 0f), new Vector3(0.7f, height * 0.4f, 0.7f), trunk);
					Part(PrimitiveType.Sphere, "Crown", tree, new Vector3(0f, height * 0.95f, 0f), new Vector3(3.2f, 3f, 3.2f), leaves);
					trees++;
				}
				else if (rocks < 12)
				{
					var stone = Prim(PrimitiveType.Sphere, "Rock_" + rocks, root, position + Vector3.up * 0.5f, new Vector3(2.4f, 1.4f, 1.9f), rock);
					stone.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
					stone.AddComponent<AssetIdTag>().Id = "resources/stone-node";
					rocks++;
				}
			}
		}

		// ---- the caravan ----

		static Caravan BuildCaravan(RoadPath road)
		{
			var wood = Mat("WagonWood", new Color(0.5f, 0.32f, 0.16f));
			var canvas = Mat("Canvas", new Color(0.92f, 0.88f, 0.75f));
			var dark = Mat("Dark", new Color(0.14f, 0.12f, 0.12f));
			var cloth = Mat("WagonCloth", new Color(0.7f, 0.2f, 0.15f));

			var root = new GameObject("Caravan");
			var caravan = root.AddComponent<Caravan>();
			var health = root.GetComponent<Health>(); // added by Caravan's RequireComponent
			health.MaxHealth = 500f;
			health.Team = 1;
			health.Restore();

			var group = new GameObject("Wagons").transform;
			var wagons = new Transform[3];
			for (var i = 0; i < wagons.Length; i++)
			{
				var wagon = new GameObject("Wagon_" + (i + 1)).transform;
				wagon.SetParent(group, false);
				wagon.gameObject.AddComponent<AssetIdTag>().Id = "props/wagon";
				Part(PrimitiveType.Cube, "Bed", wagon, new Vector3(0f, 1.0f, 0f), new Vector3(2.2f, 0.7f, 3.8f), wood);
				Part(PrimitiveType.Cube, "Cover", wagon, new Vector3(0f, 2.0f, -0.2f), new Vector3(2.0f, 1.3f, 3.2f), canvas);
				Part(PrimitiveType.Cube, "Stripe", wagon, new Vector3(0f, 2.0f, -0.2f), new Vector3(2.04f, 0.25f, 3.24f), cloth);
				foreach (var x in new[] { -1.2f, 1.2f })
					foreach (var z in new[] { -1.2f, 1.2f })
					{
						var wheel = Part(PrimitiveType.Cylinder, "Wheel", wagon, new Vector3(x, 0.6f, z), new Vector3(1.2f, 0.12f, 1.2f), dark);
						wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
					}
				wagons[i] = wagon;
			}
			caravan.Wagons = wagons;
			return caravan;
		}

		// ---- camera and light ----

		static OverheadCamera BuildCameraAndLight()
		{
			var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
			var camera = cameraObject.AddComponent<Camera>();
			camera.clearFlags = CameraClearFlags.SolidColor;
			camera.backgroundColor = new Color(0.55f, 0.7f, 0.85f);
			camera.fieldOfView = 45f;
			camera.nearClipPlane = 0.3f;
			camera.farClipPlane = 400f;
			cameraObject.AddComponent<AudioListener>();
			var view = cameraObject.AddComponent<OverheadCamera>();
			view.Bounds = new Rect(-70f, -45f, 140f, 100f);
			view.Pitch = 58f;
			view.MinDistance = 16f;
			view.MaxDistance = 58f;
			view.Distance = 36f;
			view.Focus = new Vector3(-50f, 0f, -26f);

			var sun = new GameObject("Sun");
			sun.transform.rotation = Quaternion.Euler(52f, -30f, 0f);
			var light = sun.AddComponent<Light>();
			light.type = LightType.Directional;
			light.intensity = 1.1f;
			light.color = new Color(1f, 0.96f, 0.88f);
			light.shadows = LightShadows.Soft;
			return view;
		}

		// ---- helpers (shared: 3d-unity-graybox) ----

		static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material) => GrayboxBuilder.Box(parent, name, position, scale, material);

		static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool keepCollider = false) => GrayboxBuilder.Prim(type, name, parent, position, scale, material, keepCollider);

		/// <summary>A visual piece in a local position with no collider (so only the pad's own collider takes clicks).</summary>
		static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
		{
			var piece = GrayboxBuilder.Prim(type, name, parent, Vector3.zero, scale, material);
			piece.transform.localPosition = localPosition;
			return piece;
		}

		static Material Mat(string name, Color colour) => GrayboxBuilder.Mat(name, colour, MaterialDir);
	}
}
