:: https://github.com/libsdl-org/SDL_shadercross/actions
:: this script and the .csproj msbuild are inspired by https://github.com/FosterFramework/Foster/blob/main/Framework/Content/compile.sh

@echo off
setlocal enabledelayedexpansion

for %%f in (.\*) do (
	set fullName=%%f
	set fullNameNoExtesion=!fullName:~0,-5%!
	set fullExtension=!fullName:~-9%!
	set hlsl=!fullExtension:~-4%!
	set type=!fullExtension:~0,4%!
	
	if "!hlsl!"=="hlsl" (
		if "!type!"=="comp" (
			set shaderType="compute"
		) else if "!type!"=="vert" (
			set shaderType="vertex"
		) else if "!type!"=="frag" (
			set shaderType="fragment"
		)
		
		echo !fullName!
		
		..\bin\shadercross.exe ".\!fullNameNoExtesion!.hlsl" -s HLSL -d SPIRV -e "main" -t !shaderType! -o "..\Compiled\!fullNameNoExtesion!.spv"

		..\bin\shadercross.exe "..\Compiled\!fullNameNoExtesion!.spv" -s SPIRV -d DXBC -e "main" -t !shaderType! -o "..\Compiled\!fullNameNoExtesion!.dxbc"
		..\bin\shadercross.exe "..\Compiled\!fullNameNoExtesion!.spv" -s SPIRV -d DXIL -e "main" -t !shaderType! -o "..\Compiled\!fullNameNoExtesion!.dxil"
		..\bin\shadercross.exe "..\Compiled\!fullNameNoExtesion!.spv" -s SPIRV -d MSL -e "main" -t !shaderType! -o "..\Compiled\!fullNameNoExtesion!.msl"
	)
)