# Changelog

### 2.0.0-pre.8 (2023-04-07)
* NotarizationDistro: Update to use notarytool instead of altool (part of XCode 13+)
* SteamDistro: Make password optional and fix locating `steamcmd` from a SDK subdirectory
* iOSDistro: Fix build error when using an Apple Silicon Mac
* OptionVersion: If share build number is set to "In Project", always set build number of all targets
* OptionEnum: Explicitly limit generic arguments to enums
* Fix incorrect use of 7zip's `-r` option in ZipDistro that could include additional folders
* Fix `Option.EditorProfile` not set in options with `ExecuteInEditMode` capability

### 2.0.0-pre.7 (2023-01-25)
* Add "Enable Runtime Logging" and "Rebuild Content" sub-options to "Build Addressables" (@JesseTG)
* Fix Zip Distro not finding Unity-bundled 7-Zip on Windows (@JesseTG)
* Fix exception when building scenes with Addressables
* Fix command line build not exiting if build profile cannot be found or is not set
* Fix `OnBuildError` is not called if `PrepareBuild` throws an exception (@JesseTG)

### 2.0.0-pre.6 (2022-05-18)
* Zip Distro: Add rule to exclude new name of Burst debug info folder (*_BurstDebugInformation_DoNotShip)

### 2.0.0-pre.5 (2022-04-21)
* Trimmer Server:
  * Improve using custom commands
  * Improve handling of long messages
  * Default to IPv4, add option to use IPv6
* Added RawFile format option to Zip Distro, this allows to upload a single file
  with Upload Distro without compressing it first
* Zip Distro will now also append the version to the root directory inside the
  created archive if option is enabled

### 2.0.0-pre.3 (2022-03-15)
* Fix exception in Editor Profile GUI during play mode (caused by Version Option)
* iOS Distro: Add option to allow Xcode to register new devices

### 2.0.0-pre.2 (2021-10-29)
* iOS Distro: Add option to allow Xcode to automatically update provisioning
* iOS Distro: Simplify status messages
* Fix compatibility with Unity 2021.2

### 2.0.0-pre.1 (2021-08-19)
* Switch active build target before builds
* Add preference to restore active build target after builds
* Use Unity's progress API to report build and distro progress
* Do not change scripting defines in player settings on Unity 2020.1+
* Add Option to build Addressables content before a player build
* Batch Build replaces Meta Distro and can be used for profiles and distros
* Add iOS Distro to build and upload iOS apps
* Update Version Option to accommodate more project setups
* Build APIs are now asynchronous and require `ScriptableObject`-based completion listeners
* Distros use async/await instead of custom editor coroutines
* Throw an exception when command line build fails to produce a non-zero exit code
* Use `BuildReport` where possible in Options and in the build APIs
* Add `OnBuildError` callback for Options (@JesseTG)
* Use Unity's own build GUID in `BuildInfo`
* Require Unity 2019.4+

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
