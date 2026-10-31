using ITKamianets.Engine.Shell;
using UnityEngine;

namespace Temnorid.CaravanGuard
{
	/// <summary>What the shared shell (splash, landing, lobby, top and bottom bars) needs to know about this game.</summary>
	public static class CaravanShell
	{
		public const string Repo = "WebArtWork/caravan-guard.temnorid.com";
		public const string DefencePlace = "defence";
		public const string ArchersPlace = "archers";

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		static void Configure()
		{
			ShellConfig.Current = new ShellConfig
			{
				Title = "Temnorid Caravan Guard",
				Tagline = "Place the guards. Bring the wagons home.",
				GameId = "caravan-guard",
				Repo = Repo,
				InstallerSuffix = "-Setup.exe",
				MaxPlayers = 4,
				Multiplayer = true,
				GameScene = "Main",
				Accent = new Color(0.29f, 0.565f, 0.72f),
				Places =
				{
					new ShellPlace(DefencePlace, "Defence", "D"),
					new ShellPlace(ArchersPlace, "Archers", "A"),
				},
			};
		}
	}
}
