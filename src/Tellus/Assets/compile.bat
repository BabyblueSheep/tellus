:: https://github.com/libsdl-org/SDL_shadercross/actions


.\bin\shadercross.exe ".\Source\TexturedQuad.vert.hlsl" -s HLSL -d SPIRV -e "main" -t vertex -o ".\Compiled\TexturedQuad.vert.spv"

.\bin\shadercross.exe ".\Compiled\TexturedQuad.vert.spv" -s SPIRV -d DXBC -e "main" -t vertex -o ".\Compiled\TexturedQuad.vert.dxbc"
.\bin\shadercross.exe ".\Compiled\TexturedQuad.vert.spv" -s SPIRV -d DXIL -e "main" -t vertex -o ".\Compiled\TexturedQuad.vert.dxil"
.\bin\shadercross.exe ".\Compiled\TexturedQuad.vert.spv" -s SPIRV -d MSL -e "main" -t vertex -o ".\Compiled\TexturedQuad.vert.msl"


.\bin\shadercross.exe ".\Source\TexturedQuad.frag.hlsl" -s HLSL -d SPIRV -e "main" -t fragment -o ".\Compiled\TexturedQuad.frag.spv"

.\bin\shadercross.exe ".\Compiled\TexturedQuad.frag.spv" -s SPIRV -d DXBC -e "main" -t fragment -o ".\Compiled\TexturedQuad.frag.dxbc"
.\bin\shadercross.exe ".\Compiled\TexturedQuad.frag.spv" -s SPIRV -d DXIL -e "main" -t fragment -o ".\Compiled\TexturedQuad.frag.dxil"
.\bin\shadercross.exe ".\Compiled\TexturedQuad.frag.spv" -s SPIRV -d MSL -e "main" -t fragment -o ".\Compiled\TexturedQuad.frag.msl"


.\bin\shadercross.exe ".\Source\SpriteBatch.comp.hlsl" -s HLSL -d SPIRV -e "main" -t compute -o ".\Compiled\SpriteBatch.comp.spv"

.\bin\shadercross.exe ".\Compiled\SpriteBatch.comp.spv" -s SPIRV -d DXBC -e "main" -t compute -o ".\Compiled\SpriteBatch.comp.dxbc"
.\bin\shadercross.exe ".\Compiled\SpriteBatch.comp.spv" -s SPIRV -d DXIL -e "main" -t compute -o ".\Compiled\SpriteBatch.comp.dxil"
.\bin\shadercross.exe ".\Compiled\SpriteBatch.comp.spv" -s SPIRV -d MSL -e "main" -t compute -o ".\Compiled\SpriteBatch.comp.msl"


.\bin\shadercross.exe ".\Source\ComputeCollisions.comp.hlsl" -s HLSL -d SPIRV -e "main" -t compute -o ".\Compiled\ComputeCollisions.comp.spv"

.\bin\shadercross.exe ".\Compiled\ComputeCollisions.comp.spv" -s SPIRV -d DXBC -e "main" -t compute -o ".\Compiled\ComputeCollisions.comp.dxbc"
.\bin\shadercross.exe ".\Compiled\ComputeCollisions.comp.spv" -s SPIRV -d DXIL -e "main" -t compute -o ".\Compiled\ComputeCollisions.comp.dxil"
.\bin\shadercross.exe ".\Compiled\ComputeCollisions.comp.spv" -s SPIRV -d MSL -e "main" -t compute -o ".\Compiled\ComputeCollisions.comp.msl"