using System;
using MoonWorks;
using MoonWorks.Graphics;

namespace TellusExampleProject;

internal class Program
{
	static void Main(string[] args)
	{
		AppInfo appInfo = new
		(
			OrganizationName: "babybluesheep",
			ApplicationName: "Tellus Example Project"
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

		FramePacingSettings framePacingSettings = FramePacingSettings.CreateUncapped(60, 6);

		var game = new CollisionGame(appInfo, windowCreateInfo, framePacingSettings);
		game.Run();
	}
}

