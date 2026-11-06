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
				// One player: sign in, then straight onto the trade road between the village and the town. No landing screen, no lobby,
				// no Profile for now.
				Multiplayer = false,
				Profile = false,
				SkipLanding = true,
				GameScene = "Main",
				Accent = new Color(0.29f, 0.565f, 0.72f),
			};
		}
	}
}
