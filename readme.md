# PerfHammer

```text
        _,
      ,/ ]
    ,/  /'
   /  /'
 ,/   \                             By Nara
 |    |__________________,,,,,---------======,
/|    |                  | / / / / / / / / / |
\|    |__________________|/ / / / / / / / / /|
 |____|'                 `````---------======'
 ./  \. . ._ .  ._ . . ._. .  . .  . . . ._ .
 |    | |)|_ |) |_ |-| |_| |\/| |\/| |_| |_ |)
 |____| | |_ |\ |  | | | | |  | |  | | | |_ |\
```

PerfHammer is a Unity Tool to quickly optimize assets.

## Features

- Non-destructive optimization: all modifications are done on a copy.
- Atlas an arbitrary amount of textures (e.g. diffuse, emission, normal, masks).
- Trims input textures to UV maps. No more wasteful atlassing.
- Group materials together, so you can still have different materials with the resulting asset.
- Merge unneeded bones. Quickly merge the bones from other armatures to your main armature.

## Depedencies

- [Git](https://git-scm.com/download/win)
- [Unity Mesh Simplifier (`com.whinarn.unitymeshsimplifier`)](https://github.com/Whinarn/UnityMeshSimplifier)
- [FbxExporter (`com.unity.formats.fbx`)](https://github.com/Unity-Technologies/com.unity.formats.fbx)

## Installation

Installation of this tool works via the unity package manager which requires [Git](https://git-scm.com/download/win) to work.

### Installing Dependencies

1. Install [Git](https://git-scm.com/download/win).
2. Close down all instances of Unity.
3. Restart Unity Hub.
4. Start Unity.
5. Open up the Unity Package Manager in `Window > Package manager`.
6. Add a package with the `+` dropdown in the top left and select `Add package from git.
7. Add the `https://github.com/Whinarn/UnityMeshSimplifier.git` package.
8. Install `Fbx Exporter` by searching it in the package manager

### Installing PerfHammer

1. Open up the Unity Package Manager in `Window > Package manager`.
2. Add a package with the `+` dropdown and select `Add package from git URL`.
3. Add the `.git` HTTPS URL in the `Code` dropdown menu on this repository.

### Updating PerfHammer

Since everything is still very experimental, I won't be incrementing the SemVer

## To-Do's

In roughly descending priority:

- If there's an animator, make the body skinned mesh renderer a child of the animator.
- Multiply color to main texture.
- Transfer blend shape values over after merging.
- Fix the same bone showing up in duplicate armatures.
- Clean up bone game objects after merging.
- Allow texture up/down scaling.
- Easier install/update process via an update manager (might or might not be aimed specifically for VRChat).
- Make a video guide.
