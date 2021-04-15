# Changelog

### 1.2.1 (2021-04-15)
* Fix detection of first scene when building with a custom scene list
* Add option to append build if possible<br>
  (Requires Unity 2019.4.21 or later, will error on outdated patch builds)
* Add error when reference was not registered on `ProfileContainer`

### 1.2.0 (2020-12-05)
* Bumped minor version because of API changes that can affect Option implementations:
  * Added `OptionInclusion.Build`, which can break existing inclusion checks
  * Changed the virtual method signature of `Option.IsAvailable`
* Mac App Store Distro: Add uploading of builds, fix linking GameKit framework on Big Sur
* Fix Options that support only some of a profile's platforms being included with all of them
* Fix inclusion checks that broke with the addition of `OptionInclusion.Build`

### 1.1.5 (2020-12-05)
* Fix Mac App Store and Notarization distros not re-signing .bundle packages

### 1.1.4 (2020-12-03)
* Fix `OptionHelper.InjectFeature` not working for Options with `OptionCapabilities.ConfiguresBuild`

### 1.1.3 (2020-11-19)
* `OptionCapabilities.ConfiguresBuild` options now get `OptionInclusion.Build` if the build target is supported (instead of always `OptionInclusion.Remove`)
* Workaround for Unity 2019.4.10+ not creating a fresh build if one to append to doesn't exist

### 1.1.2 (2020-08-13)
* Fix OptionKeyStore trying to show a dialog in batch mode
* Don't show a notice / error if OptionKeyStore is not configured

### 1.1.1 (2020-08-12)
* Fix error during Cloud Build because UnityEngine.CloudBuild cannot be found
* Fix exception during build when no profile is set
* Move dummy BuildManifestObject out of global namespace
* Exclude Il2CPP symbols folder in Zip Distro (BackUpThisFolder_ButDontShipItWithYourGame)

### 1.1.0 (2019-10-22)
* Add distro to notarize macOS builds, notarization also integrates with other distros
* Add support for «-buildTarget NAME» command line option to build only a single target
* Fix macOS signing fails because Unity copies meta files with plugin bundles to player
* Fix BuildOptions enum not being saved immediately

### 1.0.2 (2019-09-27)
* Fixed usage of TypeCache editor-only API in builds

### 1.0.1 (2019-09-10)
* Fix edits of variants not saving properly (add, remove and editing parameters)
* Add spacing before Build category on Unity 2019.2+, where aligning it to the bottom isn't possible anymore
* Hide Open button in inspector header of profiles
* Changed default ini filename to «trimmer.ini»
* Remove stray Debug.Log

### 1.0.0 (2019-07-28)
* Initial Release
