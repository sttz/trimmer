# Changelog

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
