#define UseBotwinderCore //Compile without the Core.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using Botwinder.Modules;
using Botwinder.Entities;
using Discord;

#if UseBotwinderCore
using Botwinder.Core;
#endif

using guid = System.UInt64;

namespace Botwinder.Bot
{

#if !UseBotwinderCore //Compile without the Core.
#pragma warning disable 1998, 67
	public class BotwinderClient<TUser>: IBotwinderClient<TUser> where TUser : UserData, new()
	{
		/// <summary> Add your custom commands to this list. </summary>
		public event EventHandler<List<Command>> InitCommands;
		/// <summary> Execute only once when the first successful connection is established. </summary>
		public event EventHandler OnConnected;
		/// <summary> Main Update loop. This runs on fixed framerate specified in the config, if all shards are connected. </summary>
		public event EventHandler OnUpdate;
		public event EventHandler<UserEventArgs> OnUserJoinedServer;
		public event EventHandler<UserEventArgs> OnUserLeftServer;
		public event EventHandler<MessageEventArgs> OnPrivateMessageReceived;
		public event EventHandler<MessageEventArgs> OnMessageReceived;
		public event EventHandler<MessageUpdatedEventArgs> OnMessageEdited;
		public event EventHandler<Message> OnMessageDeleted;
		public event EventHandler<Message> OnMentioned;
		public event EventHandler<Command> OnCommandExecuted;
		public event EventHandler<Exception> OnException;
		public event EventHandler<string> OnLoggedInfo;

		public DateTime TimeStarted{ get; set; }
		public GlobalConfig GlobalConfig{ get; set; }
		public DiscordClient[] Clients{ get; set; }
		public Dictionary<guid, Server<TUser>> Servers{ get; set; }
		public Object ServersLock{ get; set; }
		public List<Operation> CurrentOperations{ get; set; }
		public int TotalOperationsSinceStart{ get; set; }

		public Dictionary<guid, List<guid>> ClearedMessageIDs{ get; set; }

		public BotwinderClient(string configFolder = GlobalConfig.DefaultFolder, int shardId = -1){}
		public async Task Connect(){}
		public void Wait(){}
		public Server GetServer(guid id){ return null; }
		public Server<TUser> GetServerData(guid id){ return null; }

		public bool IsGlobalAdmin(User user){ return false; }
		public bool IsContributor(User user){ return false; }
		public async Task PerformMaintenance(){}
		public void AddCommands(List<Command> commands){}

		public void AddException(Exception exception, string context){}
		public void LogException(Exception exception, CommandArguments e = null, string context = null){}
		public void UserBanned(User user, Server discordServer, DateTimeOffset timeUntil, string reason = "Unknown", bool kickedOnly = false, User bannedBy = null){}
		public async Task Ban(guid userID, Server<TUser> server, long banDurationHours, string reason, bool silent, bool deleteMessages, User bannedBy = null){}
		public async Task UnBan(guid userID, Server<TUser> server){}
		public async Task<bool> MuteUser(Server<TUser> server, User user, User mutedBy = null, bool unmuteAfterDelay = false){ return false; }
		public async Task<bool> UnmuteUser(Server<TUser> server, User user, User unmutedBy = null, bool dontChangeConfig = false){ return false; }
	}
#pragma warning restore 1998, 67
#endif


	class MainClass
	{
		protected static BotwinderClient<UserData> Bot = null;
		protected const string LockFile = "BotwinderConnectionLockFile";

		protected static List<IModule> Modules = new List<IModule>();
		protected static int ModulesUpdateIndex = -1;

		public static void Main(string[] args)
		{
			int shardId = -1;
			if( args != null && (args.Length == 1 && int.TryParse(args[0], out shardId)) ||
								(args.Length == 2 && int.TryParse(args[1], out shardId)) )
				Bot = new BotwinderClient<UserData>(shardId: shardId);
			else
				Bot = new BotwinderClient<UserData>();

			Bot.OnConnected +=  (sender, e) => OnConnected();
			Bot.OnUpdate += async (sender, e) => await Update();
			Bot.InitCommands += (sender, e) => InitCommands(e);
			Bot.OnMentioned += (sender, e) => MentionReceived(e);
			Bot.OnUserJoinedServer += (sender, e) => OnUserJoinedServer(e);
			Bot.OnLoggedInfo += (sender, e) => Console.WriteLine(e.Replace("Info:", "Discord:"));
			Bot.OnException += (sender, e) => Exception(e);
			Bot.OnPrivateMessageReceived += async (sender, e) => {
				if( Verification.Get() == null )
					return;
				if( !e.Message.RawText.StartsWith(Bot.GlobalConfig.CommandCharacter) && !await Verification.Get().VerifyUserHash(Bot, e.User, e.Message.RawText.Trim()) )
					await e.Message.Channel.SendMessageSafe("I'm sorry but I do not understand that. I'm just a bot.\n_(If you are trying to verify yourself, then the code was invalid.)_");
			};

			CreateAllModules();


			if( shardId == -1 )
			{
				Bot.Connect().Wait();
			}
			else
			{
				while(File.Exists(LockFile))
					Task.Delay(Utils.Random.Next(500, 1000)).Wait();
				File.AppendAllText(LockFile, shardId.ToString() + "\n"); //HACK

				Bot.Connect().Wait();

				Task.Delay(Utils.Random.Next(5000, 10000)).Wait();
				File.Delete(LockFile);
			}

			Bot.Wait();
		}

		protected static void Exception(Exception e, string additionalData = null)
		{
			Console.WriteLine("Botwinder Exception: " + e.Message);
			Console.WriteLine("Botwinder Stack: " + e.StackTrace);
			if( !string.IsNullOrEmpty(additionalData) )
				Console.WriteLine(additionalData);
			Console.WriteLine("..........." + e.Message);

			if( e.Message == "Received close code 1001" ) //TODO - unhack this...
			{
				Environment.Exit(0);
			}
		}

		protected static void CreateAllModules()
		{
			Modules.Add(new Patchnotes());
			Modules.Add(new Events());
			Modules.Add(new Giveaways());
			Modules.Add(new LivestreamNotifications());
			Modules.Add(new Meetings());
			Modules.Add(new Polls());
			Modules.Add(new Verification());
			Modules.Add(new TimeAtWork());
			foreach(IModule module in Modules)
				module.HandleException += (sender, e) => Bot.AddException(e.Exception, e.Data);
		}

		protected static void OnConnected()
		{
			try
			{
				//Init all modules
				List<Command> newCommands = new List<Command>();
				foreach(IModule module in Modules)
				{
					try
					{
						newCommands.AddRange(module.Init(Bot));
					} catch(Exception e)
					{
						Bot.LogException(e, null, "Module.Init failed for " + module.GetType().ToString());
					}
				}
				Bot.AddCommands(newCommands);

			} catch(Exception e)
			{
				Bot.LogException(e);
			}
		}

		protected static async Task Update()
		{
			//Update modules

			if( ++ModulesUpdateIndex >= Modules.Count )
				ModulesUpdateIndex = 0;
			try
			{
				//Bot.GetServer(Bot.GlobalConfig.MainServerID).GetChannel(Bot.GlobalConfig.MainLogChannelID).SendMessageSafe("Running update for " + Modules[ModulesUpdateIndex].ToString());
				await Modules[ModulesUpdateIndex].Update(Bot);
			} catch(Exception e)
			{
				Bot.LogException(e, null, "Module.Update failed for "+ Modules[ModulesUpdateIndex].GetType().ToString());
			}
		}

		protected static async Task Restart(Discord.Channel channel)
		{
			await channel.SendMessageSafe("Okay then, see you soon!");
			await Task.Delay(TimeSpan.FromSeconds(2f));
			await Task.Delay(TimeSpan.FromSeconds(3f));

			await Bot.PerformMaintenance();
		}

		protected static async void OnUserJoinedServer(Discord.UserEventArgs e)
		{
			try
			{
				Server<UserData> server = null;
				if( Bot.GlobalConfig.RedditEnabled && Bot.Servers.ContainsKey(e.Server.Id) && (server = Bot.Servers[e.Server.Id]) != null && server.ServerConfig.VerifyOnWelcome && server.ServerConfig.VerifyRoleID != 0 && e.Server.GetRole(server.ServerConfig.VerifyRoleID) != null )
				{
					do{
						await Task.Delay(TimeSpan.FromSeconds(10f)); //Wait for connection.
					} while(Verification.Get() == null);
					await Verification.Get().VerifyUserPM(e.User, server);
				}
			} catch(Exception exception)
			{
				if( exception.GetType() == typeof(Discord.Net.HttpException) )
					Bot.AddException(null, exception.Message +" - OnUserJoinedServer.Verify");
				else
					Bot.AddException(exception, "OnUserJoinedServer.Verify");
			}
		}

		protected static async void MentionReceived(Discord.Message message)
		{
			try
			{
				if( Bot.IsGlobalAdmin(message.User) )
				{
					if( (new Regex(".*(compile|restart|update).*", RegexOptions.IgnoreCase)).Match(message.RawText).Success )
						await Restart(message.Channel);
					else if( (new Regex(".*you (back|there|awake).*", RegexOptions.IgnoreCase)).Match(message.RawText).Success )
						await message.Channel.SendMessageSafe("Yes I am!");
					else if( (new Regex(".*get back.*", RegexOptions.IgnoreCase)).Match(message.RawText).Success )
						await message.Channel.SendMessageSafe("Simplified Artificial Intelligence has returned!");
					else if( (new Regex(".*wake up.*", RegexOptions.IgnoreCase)).Match(message.RawText).Success )
						await message.Channel.SendMessageSafe("I am awake >_>");
					else if( (new Regex(".*(type|typing).*", RegexOptions.IgnoreCase)).Match(message.RawText).Success )
					{
						string path = Path.Combine("data", "typing.gif");
						await message.Channel.SendIsTyping();
						if( File.Exists(path) )
							await message.Channel.SendFile(path);
					}
					else if( (new Regex(".*((red ?hat)|rhel|bwel|botwinderos|linux).*", RegexOptions.IgnoreCase)).Match(message.RawText).Success )
					{
						string path = Path.Combine("data", "redhat.png");
						await message.Channel.SendIsTyping();
						if( File.Exists(path) )
							await message.Channel.SendFile(path);
					}
				}
			} catch(Exception e)
			{
				Bot.LogException(e, null, "MentionReceived failed");
			}
		}

		protected static void InitCommands(List<Command> commands)
		{
			commands.AddRange(Commands.GetAdminCommands(Bot));
			commands.AddRange(Commands.GetModCommands(Bot));
			commands.AddRange(Commands.GetPublicCommands(Bot));

/* ...cause i'm too lazy to type it or copypaste it from somewhere else...
// !something
			newCommand = new Command("something");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "placeholderDescription";
			newCommand.OnExecute += async (sender, e) =>{
			};
			commands.Add(newCommand);
*/
		}
	}
}
