# License

All the graphics source files are published under Public Domain License. You
are free to use them for any purpose you want.

FIXME: mention forum thread

# Applications and mods

All the sources are stored in the GIMP format (.xcf), it's a _free_ graphics
editor. Get the most fresh version to be able handling the files. It can be
downloaded here:

https://www.gimp.org/downloads/

In order to work with the DDS files (import/export), download the appropriate
plug-in:

http://registry.gimp.org/node/70

# Hints

* Avoid releasing textures in a non-DDS format. The game will have to convert
  them anyways. It will take some time during the loading, but what is more
  important, it can reduce the textures quality. By storing your textures in
  DDS format, you ensure that in the game they will look exactly as designed.
* The DDS textures must be square (the width is equal to the height), and the
  dimension must be a multiple of power of two. E.g. 16, 32, 64, 128, 256, 512,
  1024, etc. Scale your textures before saving, or else they will be scaled by
  the game with a quality you can't predict.
* When exporting the final DDS texture, make sure you've selected the
  "generate mipmaps" option. __Never__ choose "use existing mipmaps" since your
  changes may not get reflected in the scaled versions of the texture!
* Keep in mind that DDS textures are "up side down". I.e. if your source was a
  PNG or JPG, then you need to flip the texture vertically before saving it as
  DDS.
* Prefer DX1 format over DX5 when possible. The former takes much less space in
  the video memory. However, if the alpha channel is more than a simple
  "visible/not visible" bit, then DX5 is the only choice.
* In many models the alpha channel of the main texture is used to define the
  material refelection ratio. If you see a texture with an alpha channel close
  to 0, it's because the model's material is not supposed to shine as a mirror.
  In GIMP you can disable the alpha channel to see the texture and comfortably
  editing it.
* When a source file has a layer group "output", then this texture needs a
  special attention when saved as DDS due to there is an alpha mask which
  controls the reflections. You can disable the alpha channel when editing the
  texture. Make all your changes in the layers inside the layer group. When
  ready to export, merge the visible layers in the group, enable the alpha and
  export. Make sure the merged version is not saved!
