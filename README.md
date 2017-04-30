# VoxelizeMagicaVoxelXRawData

## Description
Voxelize volume data(.xraw) exported with MagicaVoxel in Unity

MagicaVoxel is a free lightweight 8-bit voxel editor and interactive path tracing renderer
<br/>
(https://voxel.codeplex.com/)

## Demo
1. Type **o xraw** in MagicaVoxel command line to export volume data as .xraw file.
2. import .xraw file in Unity Editor.
3. Select **Tool>irishoak>XRaw2PNGConvertor**, and drag .xraw file into .xraw path column, then push **Create** button.
4. save .png file in Editor.
5. Change Texture format to **RGBA 32bit**
6. Set PNG file to XRawDataTex in XRawVoxelManager.cs.
7. Push **TriggerSetXRawDataParams** checkbox to set parameters.
8. Push **TriggerVoxelize** checkbox to voxelize.

https://youtu.be/S6zzStNM06k

## Requirement
requires a graphics card that can support DX11

## Licence

[MIT](https://github.com/hiroakioishi/VoxelizeMagicaVoxelXRawData/blob/master/license)

## Author

[hiroakioishi](https://github.com/hiroakioishi)
