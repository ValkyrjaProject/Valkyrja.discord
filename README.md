Copyright © 2016 Radka Gustavsson, [rhea.dev](https://rhea.dev)

[![Build Status](https://img.shields.io/endpoint.svg?url=https%3A%2F%2Factions-badge.atrox.dev%2FValkyrjaProject%2FValkyrja.discord%2Fbadge&style=flat)](https://actions-badge.atrox.dev/ValkyrjaProject/Valkyrja.discord/goto)

## The Valkyrja
Please take a look at our website to see what's the bot about, full list of features, invite and configuration: [http://valkyrja.app](http://valkyrja.app)

## Contributors:

* Please read the [Contributing file](CONTRIBUTING.md) before you start =)
* Fork this repository, and then clone it recursively to get the Core library as well: `git clone --recursive git@github.com:YOURUSERNAME/Valkyrja.discord.git`
* Nuke your nuget cache `rm -rf ~/.nuget/packages/discord.net*` (google the location for windows...)
* Remove `Valkyrja.secure` project reference from the `.sln` file
* Comment out the `#define UsingValkyrjaSecure` in `Program.cs`

## Project structure

The Valkyrja project is split into these repositories:
* `Valkyrja.core` - Core client code.
* `Valkyrja.discord` - Most of the Valkyrja's features.
* `Valkyrja.secure` - Private repository containing sensitive code, such as antispam.
* `Valkyrja.service` - Separate bot to manage Valkyrja.discord and other systemd services.
* `Valkyrja.monitoring` - Separate bot for offsite monitoring of Valkyrja.discord.
* `Valkyrja.web` - The `valkyrja.app` website.
* `Valkyrja.status` - The `status.valkyrja.app` page - only slightly modified [eZ Server Monitor](https://github.com/shevabam/ezservermonitor-web)

