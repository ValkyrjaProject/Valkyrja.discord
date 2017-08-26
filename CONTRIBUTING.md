## Contributors:

As a contributor you can directly commit to the project. Please create a new branch for everything, and then submit pull-request. This is done easily right here on the page if you're not awesome enough in the CLI.

1. Create a new branch. This can be done easily on github. [e.g.](https://i.imgur.com/EDtnZ56.png)
  1. Naming convention: `<type of branch>-<name of your contribution>` where
    * `<type of branch>` is generally either `feature`, `improvement` or `fix` (similar to issue labels)
    * `<name of your contribution>` would e whatever your code is going to be about, where it should use camelCase. In case of an issue, just add the ticket number.
  2. Examples:
    * `feature-commandOverrides` (without an issue)
    * `improvement-123-youtubeNotifications` (for issue `#123`)
    * `fix-123` (for issue `#123`) _Please don't use **just** the number for bigger features, add some title to know what's that about without having to look it up._
2. Commit your code properly into your branch as you work on it.
  1. Recommended IDE to write your code (You can also refer to [fedoraloves.net](http://fedoraloves.net) for further information on C# in Fedora Linux.)
    * [Jetbrains Rider](https://www.jetbrains.com/rider) - Windows, Linux and Mac. Prefered choice and active contributors will receive a license from Rhea.
    * [Visual Studio Code](https://code.visualstudio.com) - Windows, Linux and Mac.
    * Standard Visual Studio is not recommended, however you can use it if you prefer. There are issues ;)
    * MonoDevelop - Mono only as of writing of this document, won't work with netcore.
    * Xamarin - Do not use this ever.
  2. Follow our naming conventions and code style guide below. (Set up your IDE for it...)
  3. Discuss your problems and ideas with our awesome dev team on Discord, to further improve them!
3. Test your code.
  1. [Jetbrains Rider](https://www.jetbrains.com/rider) can nicely build, debug and run both mono (`1.0 code`) and netcore (`2.0 code`) on both Windows and Linux. VS, VSCode, MonoDevelop or Xamarin are not recommended for debugging.
  2. You will be given [beta-token](http://inviteb.botwinder.info) by Rhea, which you can use either on your own server, or in Jefi's Nest.
4. Submit PullRequest when you're done. This can be done easily on github. e.g. [1.](https://i.imgur.com/vF1uSMm.png) [2.](https://i.imgur.com/mbNvr3c.png)
  1. New features or improvements or any other large changes should go into the `dev` branch.
  2. Really tiny fixes and typos, or tiny improvements of a response message, etc, can go straight into `master`. If in doubt ask.
  3. If there is an issue for your PR, make sure to mention the `#number` in the title.

### Outside contributors:

The workflow for outside contribution is recommended to be the same, we don't bite :P

The only difference is that you would first fork the repository, then follow all the other stuff and eventually submit a PR from your fork, into our appropriate branch.

## Solution file

* The solution as-is won't compile for you, unless you have `Botwinder.core` repository cloned into your `Botwinder.discord/Core` directory.
* Further you have to kick the missing Secure project out of the solution file, remove the dependency in `Bot/Botwinder.discord.csproj`, and comment out the `#define UsingBotwinderSecure` in `Bot/Program.cs`
* Do not submit any of these changes, otherwise you will screw up our build!

## Code style and Naming Conventions

Just a few guidelines about the code:

* If you're writing a new module, try to write summary for public and internal methods.
* Use PascalCase for public member properties and `this` notation for private ones. Treat internal as public, and protected as private. Treat constants as public as well, ie PascalCase.
* Always immediately initialise variables.
* Always explicitly declare whether methods are public or private. If they are async, this keyword should be second (or third in case of static methods `public static async Task ...`)
* Never return `void` with async methods unless you know what you're doing. Return `Task` instead of `void`.
  ```cs
    
  public class BunnehClient<TUser>: IClient<TUser> where TUser: UserData, new()
  {
    public enum ConnectionState
    {
      None = 0,
      Bad,
      Good,
      Peachy
    }
    
    internal const int LoopLimit = 60;
    
    public ConnectionState State = ConnectionState.None;
    
    internal int LoopCount{ get; private set; } = 0;
  
		
    /// <summary> This is blocking call that will await til the connection is peachy.
    /// Returns true if the operation was canceled, false otherwise. </summary>
    public async Task<bool> AwaitConnection<TUser>(TUser user) where TUser: UserData, new()
    {
      while( this.State != ConnectionState.Peachy )
      {
        if( this.LoopCount++ >= this.LoopLimit )
          return true;

        await Task.Delay(1000);
      }
  
      await user.SendMessageAsync("You have been connected!");
      return false;
    }
  }

  ```

Please try to set-up your IDE to handle this for you:

* Use tabs, do not expand to spaces.
* **Always** use explicit types. **Do Not Use `var`!**
* Set the IDE to remove trailing whitespace, it triggers OCD...
* Default VS-style will try to format your code in rather weird way that is a little irational in my opinion. Please follow the above displayed format: `if( something )`. (Note that the VS style would place spaces for if statement this way: `if (something)`)

### Import Jetbrains Rider configuration

You can just import [my Jetbrains Rider settings](https://cloud.rhea-ayase.eu/s/VCl0MmI1qMbNCIP) =)

