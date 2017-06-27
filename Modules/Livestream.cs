/*
 * Copyright Radka Janek aka RheaAyase www.ayase.eu - modifications only with explicit permission.
 */
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using Botwinder.Entities;
using Newtonsoft.Json.Linq;
using guid = System.UInt64;

namespace Botwinder.Modules
{
	public class LivestreamNotifications: JsonModule<LivestreamNotifications.LivestreamConfig>
	{
		public class LivestreamConfig
		{
			public LivestreamChannel[] Channels;
			public string TwitchClientId;
		}


		public class LivestreamChannel
		{
			public string ChannelName = "";
			public ServiceType Type;
			public guid[] DiscordChannelIDs;
			public bool IsLive = false;
			public DateTime LastSeenLive = DateTime.MinValue;

			public LivestreamChannel(string channelName, ServiceType type, guid discordChannelID)
			{
				this.ChannelName = channelName;
				this.Type = type;
				this.DiscordChannelIDs = new guid[1];
				this.DiscordChannelIDs[0] = discordChannelID;
			}
		}

		public enum ServiceType
		{
			None,
			//Youtube,
			Twitch,
			Hitbox,
			Mixer
		}

		public class StreamInfo
		{
			public LivestreamChannel ChannelConfig = null;
			public string DisplayName = "";
			public string Game = "";
			public string StreamTitle = "";
			public string Url = "";
			public bool IsLive = false;

			public StreamInfo(LivestreamChannel channelConfig, bool isLive, string displayName = "", string game = "", string title = "", string url = "")
			{
				this.ChannelConfig = channelConfig;
				this.IsLive = isLive;
				this.DisplayName = string.IsNullOrWhiteSpace(displayName) ? channelConfig.ChannelName : displayName;
				this.Game = game;
				this.StreamTitle = title;
				this.Url = url;
			}
		}

		public static class Constants
		{
			public const string YoutubeBaseUrl = "";
			public const string YoutubeIsLive = "";
			public const string YoutubeIsLiveTrue = "";
			public const string YoutubeGame = "";
			public const string YoutubeTitle = "";

			public const string TwitchStreamUrl = "https://api.twitch.tv/kraken/streams/";
			public const string TwitchChannelUrl = "https://api.twitch.tv/kraken/channels/";
			/// <summary> null if not live </summary>
			public const string TwitchStream = "stream"; //object
			public const string TwitchChannel = "channel"; //object
			public const string TwitchDisplayName = "display_name";
			public const string TwitchGame = "game";
			public const string TwitchTitle = "status";
			public const string TwitchUrl = "url";

			public const string HitboxBaseUrl = "https://api.hitbox.tv/media/live/";
			public const string HitboxStream = "livestream";
			public const string HitboxIsLive = "media_is_live"; //string "1" or "0"
			public const string HitboxDisplayName = "media_display_name";
			public const string HitboxGame = "category_name";
			public const string HitboxTitle = "media_status";
			public const string HitboxUrl = "media_name"; //"channel_link"; -> that doesn't work...

			public const string MixerBaseUrl = "https://mixer.com/api/v1/channels/";
			public const string MixerIsLive = "online"; //bool
			public const string MixerDisplayName = "token";
			public const string MixerGameParent = "type"; //object
			public const string MixerGame = "name";
			public const string MixerTitle = "name";
		}


		protected override string Filename => "livestream.json";

		protected const float EventCooldown = 300f;


		public ConcurrentDictionary<string, LivestreamChannel> ChannelDictionary;


		public override List<Command> Init<TUser>(IBotwinderClient<TUser> client)
		{
			List<Command> commands;
			Command newCommand;

			if( !client.GlobalConfig.LivestreamEnabled )
			{
				commands = new List<Command>();
				newCommand = new Command("livestreamAdd");
				newCommand.Type = Command.CommandType.ChatOnly;
				newCommand.Description = "Add a channel to watch list, to send a short notification message in \"this\" channel, whenever they go live. Supported services are: `twitch`, `hitbox` & `mixer` (more will be added soon.) Example: `livestreamAdd twitch RheaAyase` (Use of this command and bot's response will be deleted for your convenience.)";
				newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator;
				newCommand.OnExecute += async (sender, e) => {
					await e.Message.Channel.SendMessageSafe("Livestream notifications are currently disabled for technical difficulties. Please be patient, we are working on it.");
				};
				commands.Add(newCommand);
				newCommand = newCommand.CreateCopy("livestreamRemove");
				newCommand.Description = "Remove a livestream from notifications (added by `livestreamAdd`.) Example usage: `livestreamRemove twitch RheaAyase`";
				commands.Add(newCommand);
				newCommand = newCommand.CreateCopy("livestreamList");
				newCommand.Description = "Display a list of livestream notifications for the channel it's used in.";
				commands.Add(newCommand);
				return commands;
			}

			commands = base.Init<TUser>(client);

			this.ChannelDictionary = new ConcurrentDictionary<string, LivestreamChannel>();
			if( this.Data != null && this.Data.Channels != null )
			{
				foreach( LivestreamChannel data in this.Data.Channels )
				{
					this.ChannelDictionary.Add(data.ChannelName + data.Type.ToString(), data);
				}
			}

// !livestreamAdd
			newCommand = new Command("livestreamAdd");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Add a channel to watch list, to send a short notification message in \"this\" channel, whenever they go live. Supported services are: `twitch`, `hitbox` & `mixer` (more will be added soon.) Example: `livestreamAdd twitch RheaAyase` (Use of this command and bot's response will be deleted for your convenience.)";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator;
			newCommand.OnExecute += async (sender, e) =>{
				Discord.Message responseMessage;
				if( e.MessageArgs == null || e.MessageArgs.Length < 2 )
				{
					responseMessage = await e.Message.Channel.SendMessage(string.Format("We Need Channel Name! O_O\nExample: `{0}{1} twitch RheaAyase`", e.Server.ServerConfig.CommandCharacter, e.Command.ID));
				}
				else
				{
					ServiceType type =  ServiceType.None;
					if( e.MessageArgs[0].ToLower() == ServiceType.Twitch.ToString().ToLower() )
						type = ServiceType.Twitch;
					if( e.MessageArgs[0].ToLower() == ServiceType.Hitbox.ToString().ToLower() )
						type = ServiceType.Hitbox;
					if( e.MessageArgs[0].ToLower() == ServiceType.Mixer.ToString().ToLower() )
						type = ServiceType.Mixer;

					if( type == ServiceType.None )
					{
						responseMessage = await e.Message.Channel.SendMessage("Invalid streaming service.");
					}
					else if( await AddChannel(e.MessageArgs[1].ToLower(), type, e.Message.Channel.Id) )
					{
						responseMessage = await e.Message.Channel.SendMessage(string.Format("I will tell the world whenever {0} starts streaming!", e.MessageArgs[1]));
					}
					else
					{
						responseMessage = await e.Message.Channel.SendMessage("I have failed you master - I couldn't find this channel. :frowning:");
					}
				}

				await Task.Delay(TimeSpan.FromSeconds(30f));
				await e.Message.Delete();
				if(responseMessage != null)
					await responseMessage.Delete();
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("livestreamadd"));
			commands.Add(newCommand.CreateAlias("streamAdd"));

// !livestreamRemove
			newCommand = new Command("livestreamRemove");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Remove a livestream from notifications (added by `livestreamAdd`.) Example usage: `livestreamRemove twitch RheaAyase`";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator;
			newCommand.OnExecute += async (sender, e) =>{
				Discord.Message responseMessage;
				if( e.MessageArgs == null || e.MessageArgs.Length < 2 )
				{
					responseMessage = await e.Message.Channel.SendMessage(string.Format("Please, I beg you! Tell me what stream to remove!\nExample: `{0}{1} twitch RheaAyase`", e.Server.ServerConfig.CommandCharacter, e.Command.ID));
				}
				else
				{
					ServiceType type =  ServiceType.None;
					if( e.MessageArgs[0] == ServiceType.Twitch.ToString().ToLower() )
						type = ServiceType.Twitch;
					if( e.MessageArgs[0] == ServiceType.Hitbox.ToString().ToLower() )
						type = ServiceType.Hitbox;
					if( e.MessageArgs[0] == ServiceType.Mixer.ToString().ToLower() )
						type = ServiceType.Mixer;

					if( type == ServiceType.None )
						responseMessage = await e.Message.Channel.SendMessage("That's an invalid streaming service!");
					else
					{
						RemoveChannel(e.MessageArgs[1].ToLower(), type, e.Message.Channel.Id);
						responseMessage = await e.Message.Channel.SendMessage("Fine, I will watch alone!");
					}
				}

				await Task.Delay(TimeSpan.FromSeconds(30f));
				await e.Message.Delete();
				if(responseMessage != null)
					await responseMessage.Delete();
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("livestreamremove"));
			commands.Add(newCommand.CreateAlias("streamRemove"));

// !livestreamList
			newCommand = new Command("livestreamList");
			newCommand.Type = Command.CommandType.ChatOnly;
			newCommand.Description = "Display a list of livestream notifications for the channel it's used in.";
			newCommand.RequiredPermissions = Command.PermissionType.ServerOwner | Command.PermissionType.Admin | Command.PermissionType.Moderator;
			newCommand.OnExecute += async (sender, e) =>{
				Discord.Message responseMessage;
				string message = "An error haz happened. Please poke <@"+client.GlobalConfig.OwnerIDs[0]+"> :<";
				string content = "";

				if( !this.ChannelDictionary.Any() )
					message = "There are no notifications for this channel.";
				else
				{
					foreach(KeyValuePair<string, LivestreamChannel> pair in this.ChannelDictionary)
					{
						if( pair.Value.DiscordChannelIDs.Contains(e.Message.Channel.Id) )
						{
							StreamInfo info = await GetStreamInfo(pair.Value);
							if( info != null )
								content += string.Format("\n{0}: {1} ({2})", info.DisplayName, info.IsLive ? "online" : "offline", pair.Value.Type.ToString());
						}
					}
					if( !string.IsNullOrWhiteSpace(content) )
					{
						message = "Livestream notifications in this channel:\n```xl";
						message += content;
						message += "\n```";
					}
					else
					{
						message = "There are no notifications for this channel.";
					}
				}

				responseMessage = await e.Message.Channel.SendMessage(message);

				await Task.Delay(TimeSpan.FromSeconds(30f));
				await e.Message.Delete();
				if(responseMessage != null)
					await responseMessage.Delete();
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("livestreamlist"));
			commands.Add(newCommand.CreateAlias("streamList"));


			return commands;
		}

		protected override void Save()
		{
			this.Data.Channels = new LivestreamChannel[this.ChannelDictionary.Count];
			this.ChannelDictionary.Values.CopyTo(this.Data.Channels, 0);

			base.Save();
		}

		public override async Task Update<TUser>(IBotwinderClient<TUser> client)
		{
			if( !client.GlobalConfig.LivestreamEnabled || this.ChannelDictionary == null || !this.ChannelDictionary.Any() || this.UpdateInProgress )
				return;

			this.UpdateInProgress = true;

			foreach(KeyValuePair<string, LivestreamChannel> pair in this.ChannelDictionary)
			{
				if( pair.Value.DiscordChannelIDs == null || pair.Value.DiscordChannelIDs.Length == 0 )
					continue;

				try
				{
					StreamInfo info = await GetStreamInfo(pair.Value);

					if( info != null && pair.Value.IsLive != info.IsLive )
					{
						pair.Value.IsLive = info.IsLive;

						if( (DateTime.UtcNow - pair.Value.LastSeenLive).TotalSeconds > EventCooldown )
						{
							if( info.IsLive )
							{
								await OnStarted(client, info);
							}
							else if( !info.IsLive )
							{
								await OnStopped(client, info);
							}
						}

						pair.Value.LastSeenLive = DateTime.UtcNow;
						SaveAsync();
					}
				} catch(Exception e)
				{
					TriggerException(this, new ModuleExceptionArgs(e, pair.Value.ChannelName +" | "+ pair.Value.Type));
				}
			}

			this.UpdateInProgress = false;
		}

		private async Task OnStarted<TUser>(IBotwinderClient<TUser> client, StreamInfo info) where TUser: UserData, new()
		{
			foreach(guid id in info.ChannelConfig.DiscordChannelIDs)
			{
				Discord.Channel channel = client.Clients.SelectMany(c => c.Servers).SelectMany(s => s.AllChannels).FirstOrDefault(c => c.Id == id);

				if( channel != null )
				{
					await channel.SendMessageSafe(string.Format("**{0} is Live!**\n{1}: {2}\n<{3}>",
						info.DisplayName, info.Game, info.StreamTitle, info.Url
					));
				}
			}
		}

#pragma warning disable 1998
		private async Task OnStopped<TUser>(IBotwinderClient<TUser> client, StreamInfo info) where TUser: UserData, new()
		{
		}
#pragma warning restore 1998

		/// <summary> Returns false, if channelName doesn't exist. </summary>
		public async Task<bool> AddChannel(string channelName, ServiceType type, guid discordChannelID)
		{
			LivestreamChannel channel = null;
			try{
				if( await GetStreamInfo(new LivestreamChannel(channelName, type, discordChannelID)) == null )
					return false;

				if( this.ChannelDictionary.ContainsKey(channelName + type.ToString()) )
				{
					channel = this.ChannelDictionary[channelName + type.ToString()];
					if( channel.DiscordChannelIDs == null || !channel.DiscordChannelIDs.Contains(discordChannelID) )
					{
						if( channel.DiscordChannelIDs == null )
							channel.DiscordChannelIDs = new guid[1];
						else
							Array.Resize(ref channel.DiscordChannelIDs, channel.DiscordChannelIDs.Length + 1);

						channel.DiscordChannelIDs[channel.DiscordChannelIDs.Length - 1] = discordChannelID;
					}
				}
				else
				{
					channel = new LivestreamChannel(channelName, type, discordChannelID);
					this.ChannelDictionary.Add(channelName + type.ToString(), channel);
				}

				SaveAsync();
			} catch(Exception e)
			{
				TriggerException(this, new ModuleExceptionArgs(e, channelName +" | "+ type));
				return false;
			}
			return true;
		}

		public void RemoveChannel(string channelName, ServiceType type, guid discordChannelID)
		{
			if( !this.ChannelDictionary.ContainsKey(channelName + type.ToString()) )
				return;

			LivestreamChannel channel = this.ChannelDictionary[channelName + type.ToString()];
			if( channel.DiscordChannelIDs == null || channel.DiscordChannelIDs.Length == 0 )
				return;

			List<guid> list = new List<guid>(channel.DiscordChannelIDs);
			list.Remove(discordChannelID);
			channel.DiscordChannelIDs = list.ToArray();

			SaveAsync();
		}


		public async Task<StreamInfo> GetStreamInfo(LivestreamChannel channelConfig) //TODO move this code where it belongs - in the StreamInfo class...
		{
			StreamInfo info = null;
			if( channelConfig == null || string.IsNullOrWhiteSpace(channelConfig.ChannelName) )
				throw new ArgumentException("Invalid LivestreamChannel config.");

			string uri = GetChannelURI(channelConfig);
			JObject jObject;

			switch(channelConfig.Type)
			{
			case ServiceType.Twitch:
				{
					jObject = await RequestJson(uri, "Client-ID: "+ this.Data.TwitchClientId);
					JToken stream = null;
					JObject channel = await RequestJson(Constants.TwitchChannelUrl + channelConfig.ChannelName, "Client-ID: "+ this.Data.TwitchClientId);
					if( channel == null ) //channel is null if the twitchchannel does not exist.
						break;

					bool isLive = jObject != null && (stream = jObject.GetValue(Constants.TwitchStream)) != null && stream.HasValues;// && stream.Value<int>("viewers") > 1;  //Stream is not live, if jObject or stream are null.
					info = new StreamInfo(channelConfig, isLive, channel.Value<string>(Constants.TwitchDisplayName), channel.Value<string>(Constants.TwitchGame), channel.Value<string>(Constants.TwitchTitle), channel.Value<string>(Constants.TwitchUrl));
				}break;
			case ServiceType.Hitbox:
				{
					jObject = await RequestJson(uri);
					JToken stream = null;
					JToken channel = null;
					if( jObject == null || (stream = jObject.Value<JToken>(Constants.HitboxStream)) == null )
						break;

					channel = stream.ElementAtOrDefault(0);
					info = new StreamInfo(channelConfig, channel.Value<string>(Constants.HitboxIsLive) == "1", channel.Value<string>(Constants.HitboxDisplayName), channel.Value<string>(Constants.HitboxGame), channel.Value<string>(Constants.HitboxTitle), "http://www.hitbox.tv/" + channelConfig.ChannelName);
				}break;
			case ServiceType.Mixer:
				{
					jObject = await RequestJson(uri);
					JToken game = null;
					if( jObject == null || (game = jObject.Value<JToken>(Constants.\GameParent)) == null || !game.Any() )
						break;

					info = new StreamInfo(channelConfig, jObject.Value<bool>(Constants.MixerIsLive), jObject.Value<string>(Constants.MixerDisplayName), game.Value<string>(Constants.MixerGame), jObject.Value<string>(Constants.MixerTitle), "https://mixer.com/" + channelConfig.ChannelName);
				}break;
			default:
				throw new NotImplementedException(channelConfig.Type.GetType().ToString() + " is not implemented.");
			}

			return info;
		}

		protected static string GetChannelURI(LivestreamChannel channelConfig)
		{
			switch(channelConfig.Type)
			{
			case ServiceType.Twitch:
				return Constants.TwitchStreamUrl + channelConfig.ChannelName;
			case ServiceType.Hitbox:
				return Constants.HitboxBaseUrl + channelConfig.ChannelName;
			case ServiceType.Mixer:
				return Constants.MixerBaseUrl + channelConfig.ChannelName;
			default:
				throw new NotImplementedException(channelConfig.Type.GetType().ToString() + " is not implemented.");
			}
		}

		protected static async Task<JObject> RequestJson(string uri, params string[] headers)
		{
			WebRequest request = WebRequest.Create(uri);
			request.Method = "GET";
			request.ContentType = "application/json";
			if( headers != null && headers.Length > 0 )
				for(int i = 0; i < headers.Length; i++)
					request.Headers.Add(headers[i]);

			WebResponse response;
			try{
				response = await request.GetResponseAsync();
			} catch(Exception e)
			{
				if( e.GetType() == typeof( WebException ) )
					return null;

				throw;
			}

			Stream stream = response.GetResponseStream();
			StreamReader reader = new StreamReader(stream);
			string json = reader.ReadToEnd().ToString();
			JObject jObject = null;
			if( !string.IsNullOrWhiteSpace(json) )
			{
				jObject = JObject.Parse(json);
			}

			reader.Dispose();
			stream.Dispose();
			response.Dispose();

			return jObject;
		}
	}
}
