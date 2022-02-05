# P3diTools
Tools for manipulating and compiling `p3di` models

## Creating `p3di` models

Use the [Blender export addon](https://github.com/Parzivail-Modding-Team/GalaxiesParzisStarWarsMod/blob/master/resources/blender_addons/io_scene_p3di.py).

### Blender conventions

* Up is +Z. This is converted to +Y during compilation.
* Forward is +Y. This is converted to +Z during compilation.
* One pixel is 0.1m (10cm).
* The name of the Object becomes the name of the submesh.
* All Objects **must** have their *rotation* and *scale* transformations **applied** (Ctrl+A, Rotation & Scale) before export.
  * The origin of an Object becomes the rotation point of a submesh in a compiled model.
* **Sockets** are defined by adding Empties of type Arrows (Add, Empty, Arrows).
  * The name of the Empty becomes the name of the socket.
  * The axes as drawn directly correspond to the local coordinate space of the ingame object.
* Objects may use the scene hierarchy to define rotation-based parenting.
  * This applies to both Empties (sockets) and Objects (submeshes).

## Compiling `p3di` models into `p3d` models and `p3dr` rigs

Download the [latest release](https://github.com/Parzivail-Modding-Team/P3diTools/releases) and run `P3diTools compile --help`

### Example command:

```
P3diTools compile -mrs my_model.p3di
```
Generates the following output files in the working directory:
* `my_model.p3d`
* `my_model.p3dr`

#### Breakdown

* The `compile` tool
* The option chain `-mrs`
  * `m` option generates a model
  * `r` option generates a rig
  * `s` option snaps pixels to the grid using the default resolution of 128x and epsilon of 0.1 (Run `P3diTools compile --help` for all options)
* The input file `my_model.p3di`

## Generating a texture map

```
P3diTools genmap -r 128 my_model.p3di map.png
```

* The `genmap` tool
* The option chain `-r 128`
  * `r 128` option option sets a texture resolution of 128x128
* The input file `my_model.p3di`
* The output file `map.png`

UV vertex snapping is also available for `genmap`. Run `P3diTools genmap --help` for all options.
