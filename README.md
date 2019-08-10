<img src="https://sttz.ch/trimmer/images/icon.svg" width="100" />

# Trimmer

An editor, build and player configuration framework for the Unity game engine.

### Introduction

Unity supports deploying your projects to dozens of platforms and provides a powerful editor to develop your project. Supporting many platforms becomes complicated very fast and adjusting configuration in builds requires a lot of scaffolding.

Trimmer provides that scaffolding as a flexible framework, that allows to quickly adjust the configuration of your project through its whole lifecycle: In the editor during development, during the build process and in the built player.

Trimmer makes it easy to create Options with a few lines of code. It provides a sensible default baseline but allows complex configurations when you need it.

Trimmer is a non-invasive framework. Instead of having to integrate it into your code, you write small Option adapters that hook your existing systems into Trimmer. Options that are not used are not compiled into builds and when a build doesn't contain any Options, Trimmer removes itself from the build completely. Trimmer also makes it easy to conditionally compile your own code.

In the editor, Trimmer provides a simple GUI interface to configure your Options for when you play your project and for the different builds your project requires. In the player, Trimmer provides optional loading of a configuration file and a in-game prompt that can configure the same Options as in the editor. Using Build Profiles, you can decide which Options are only available in the editor and which can also be configured in a given build.

#### Features
* Write options easily with only a couple lines of code
* Visually edit options in the editor and the player
* Conditionally compile only the features that you need
* Automate building and post-process your scenes during build
* Create and build profiles with different settings and features

### [Full Documentation](https://sttz.ch/trimmer/)

### Screenshots

<img src="https://sttz.ch/trimmer/images/build_profile.png" width="325" /> <img src="https://sttz.ch/trimmer/images/editor_profile.png" width="325" />

### License
Trimmer is licensed under the MIT license.
