Copyright © 2016 Radka Janek, [rhea-ayase.eu](http://rhea-ayase.eu)

![alt CC BY-NC-SA 4.0 license](https://i.creativecommons.org/l/by-nc-sa/4.0/88x31.png)

[Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License](https://creativecommons.org/licenses/by-nc-sa/4.0/)



## The Botwinder
Please take a look at our website to see what's the bot about, full list of features, invite and configuration: [http://botwinder.info](http://botwinder.info)

## Contributors:

* Please read the [Contributing file](CONTRIBUTING.md) before you start =)
* Please excuse the hacks. The project is full of them after the last few weeks (February 2017)

## Project structure

The Botwinder project is split into four repositories:
* `Botwinder.core` - private repo with two most critical files containing code related to the core client and some sensitive code (antispam, etc...)
* `Botwinder.discord` - public (this) repo with almost all of the code of the bot.
* `Botwinder.web` - public repo with the code of the `botwinder.info` website.
* `Botwinder.status` - private code of the status page, which is essentially unchanged [eZ Server Monitor](https://github.com/shevabam/ezservermonitor-web) with just some modules repositioned, and customised configuration.

This bot is using hacked up and modified [Discord.NET](https://github.com/RogueException/Discord.Net) library. These changes should never see the light of the day. Hacks over hacks made in a rush - but it works, and without memoryleaks.

Oh and keep in mind that this project is undergoing major changes right now, such as porting it from netframework to netcore, porting the serialisation from json to sql, and also from the old Discord.NET library to the new one. Don't be surprised if I randomly submit a commit that will change all the files a lot. Join our Discord server to stay up-to-date =)
