# PerfHammer

PerfHammer is a Unity Tool to quickly optimize assets.

## Features

- Non-destructive optimization: all modifications are done on a copy.
- Atlas an arbitrary amount of textures (e.g. diffuse, emission, normal, masks).
- Trims input textures to UV maps. No more wasteful atlassing.
- Group materials together, so you can still have different materials with the resulting asset.
- Merge unneeded bones. Quickly merge the bones from other armatures to your main armature.

## Depedencies

- [Unity Mesh Simplifier (`com.whinarn.unitymeshsimplifier`)](https://github.com/Whinarn/UnityMeshSimplifier)
- [FbxExporter (`com.unity.formats.fbx`)](https://github.com/Unity-Technologies/com.unity.formats.fbx)

## Installation

Installation of this tool works via the unity package manager.

1. Copy the `.git` HTTPS URL in the `Code` dropdown menu on this repository.
2. Open up the Unity Package Manager in `Window > Package manager`.
3. Add a package with the `+` dropdown and select `Add package from git URL`.
4. Paste in the `.git` URL from step 1.

Similarly, the dependencies can be installed.

1. Add the `https://github.com/Whinarn/UnityMeshSimplifier.git` package.
2. Install `Fbx Exporter` by searching it in the package manager
