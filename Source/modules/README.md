This folder holds the part modules or their base/abstarct classes.

The naming convention for the classes, that can be used as Mono components, is:

```"KAS" + <type> + <camel-case name>```

Where type can be one of:

* `Link` for the `ILinkSource` or `ILinkTarget` descendants.
* `Joint` for the `ILinkJoint` descendants.
* `Renderer` for the `ILinkRenderer` descendants.
* `Internal` for any class that is needed by a module, but is too huge to be declared within the module. These classes are never public, and they are usually sealed.

The abstract classe names must start from prefix `Abstract`. If the class is not abstract, but is not intended to be used as a part module or a Mono component, it must use prefix `Base`. No other prefixes are allowed!

Anything, that doesn't fit this folder, must be moved either into API or Utils.
