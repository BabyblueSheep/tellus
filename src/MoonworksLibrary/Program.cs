using System;
using MoonWorks;
using MoonWorks.Graphics;

namespace MoonworksLibrary;

internal class Program
{
	static void Main(string[] args)
	{
		AppInfo appInfo = new
		(
			OrganizationName: "babybluesheep",
			ApplicationName: "MoonworksLibrary"
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

		MoonworksLibraryGame game = new(appInfo, windowCreateInfo, framePacingSettings);
		game.Run();
	}
}

