Copyright © 2016 Radka Janek, [rhea-ayase.eu](http://rhea-ayase.eu)

![alt CC BY-NC-SA 4.0 license](https://i.creativecommons.org/l/by-nc-sa/4.0/88x31.png)

[Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License](https://creativecommons.org/licenses/by-nc-sa/4.0/)



## The Botwinder
Please take a look at our website to see what's the bot about, full list of features, invite and configuration: [http://botwinder.info](http://botwinder.info)

## Contributors:

* Please read the [Contributing file](CONTRIBUTING.md) before you start =)
* Fork this repository, and then clone it recursively to get the Core library as well: `git clone --recursive git@github.com:YOURUSERNAME/Botwinder.discord.git`
* Nuke your nuget cache `rm -rf ~/.nuget/packages/discord.net*` (google the location for windows...)
* Remove `Botwinder.secure` project reference from the `.sln` file
* Comment out the `#define UsingBotwinderSecure` in `Program.cs`

## Project structure

The Botwinder project is split into six repositories:
* `Botwinder.core` - Core client code.
* `Botwinder.discord` - Most of the Botwinder's features.
* `Botwinder.secure` - Private repository containing sensitive code, such as antispam.
* `Botwinder.service` - Separate bot to manage Botwinder.discord and other systemd services.
* `Botwinder.web` - The `botwinder.info` website.
* `Botwinder.status` - The `status.botwinder.info` page - only slightly modified [eZ Server Monitor](https://github.com/shevabam/ezservermonitor-web)

