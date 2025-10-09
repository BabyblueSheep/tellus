using System;
using MoonWorks;
using MoonWorks.Graphics;

namespace Tellus;

internal class Program
{
	static void Main(string[] args)
	{
		AppInfo appInfo = new
		(
			OrganizationName: "babybluesheep",
			ApplicationName: "Tellus"
		);

		WindowCreateInfo windowCreateInfo = new
		(
			windowTitle: "Game",
			windowWidth: 640,
			windowHeight: 480,
			screenMode: ScreenMode.Windowed,
			systemResizable: true,
			startMaximized: false,
			highDPI: false
		);

		FramePacingSettings framePacingSettings = FramePacingSettings.CreateLatencyOptimized(60);

		TellusGame game = new(appInfo, windowCreateInfo, framePacingSettings);
		game.Run();
	}
}

