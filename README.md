# P3diTools
Tools for manipulating and compiling `p3di` models

## Creating `p3di` models

Use the [Blender export addon](https://github.com/Parzivail-Modding-Team/GalaxiesParzisStarWarsMod/blob/master/resources/blender_addons/io_scene_p3di.py).

## Compiling `p3di` models into `p3d` models and `p3dr` rigs

Download the [latest release](https://github.com/Parzivail-Modding-Team/P3diTools/releases) and run `P3diTools compile --help`

### Example command:

```
P3diTools -mrs my_model.p3di
```
Generates the following output files in the working directory:
* `my_model.p3d`
* `my_model.p3dr`

#### Breakdown

* The option chain `-mrs`
  * `m` option generates a model
  * `r` option generates a rig
  * `s` option snaps pixels to the grid using the default resolution of 128x and epsilon of 0.1 (see `--help` for setting these options)
* The input file `my_model.p3di`
