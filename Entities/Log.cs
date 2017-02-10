using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;

using guid = System.UInt64;

namespace Botwinder.Entities
{
	public class Log
	{
		public static class Type
		{
			public const int Info = 1 << 0;
			public const int Debug = 1 << 1;
			public const int Exceptions = 1 << 2;
			public const int DeletedMessages = 1 << 3;
			public const int EditedMessages = 1 << 4;
			public const int ReceivedMessages = 1 << 5;
			public const int ExecutedCommands = 1 << 6;
			public const int Warning = 1 << 7;
		}

		private string Filename = "log.txt";
		private Object Lock = new Object();

		public Log(string filename)
		{
			this.Filename = filename;
		}

		public void LogExceptionAsync(Exception e, string additionalData = "")
		{
			Task.Run(() => LogException(e, additionalData));
		}
		private void LogException(Exception e, string additionalData = "")
		{
			lock(this.Lock)
			{
				using(StreamWriter writer = File.AppendText(this.Filename))
				{
					writer.WriteLine("Exception: " + e.Message);
					if( !string.IsNullOrWhiteSpace(additionalData) )
						writer.WriteLine("    Data: " + additionalData);
					writer.WriteLine("    Stack: " + e.StackTrace);
					writer.Flush();
				}
			}
		}

		public void LogMessageAsync(string text, bool singleLine = false)
		{
			Task.Run(() => LogMessage(text, singleLine));
		}
		public void LogMessage(string text, bool singleLine = false)
		{
			lock(this.Lock)
			{
				using(StreamWriter writer = File.AppendText(this.Filename))
				{
					if( singleLine )
						writer.Write(text);
					else
						writer.WriteLine(text);
					writer.Flush();
				}
			}
		}

		public async Task ArchiveList( List<Message> messages, bool niceFormatting = false, bool markdownList = false )
		{
			await Task.Delay(1000);
			lock(this.Lock)
			{}

			using(StreamWriter writer = File.AppendText(this.Filename))
			{
				for(int i = messages.Count - 1; i >= 0; i--)
				{
					string timestamp = Utils.GetTimestamp(messages[i].Timestamp);

					if( niceFormatting )
					{
						if( messages[i].User == null )
						{
							writer.WriteLine((markdownList ? "* " : "") + "DeletedUser:");
							writer.WriteLine((markdownList ? "    * " : "") + "  " + messages[i].Text);
						}
						else
						{
							guid userID = messages[i].User.Id;
							writer.WriteLine((markdownList ? "* " : "") + (!string.IsNullOrWhiteSpace(messages[i].User.Nickname) ? messages[i].User.Nickname : messages[i].User.Name) + ":");

							for(; i >= 0; i--)
							{
								if( !string.IsNullOrWhiteSpace(messages[i].Text) )
									writer.WriteLine((markdownList ? "    * " : "  ") + messages[i].Text);

								if( messages[i].Attachments != null && messages[i].Attachments.Length > 0 )
									foreach(Message.Attachment attachment in messages[i].Attachments)
										writer.WriteLine((markdownList ? "    * " : "  ") + attachment.Url);

								if( i > 0 && (messages[i - 1].User == null || messages[i - 1].User.Id != userID) )
									break;
							}
						}
						writer.WriteLine();
					}
					else
					{
						writer.WriteLine(string.Format((markdownList ? "    * " : "") + "{0} | {1} | {2}: {3}", timestamp, (messages[i].User == null ? "null" : messages[i].User.Id.ToString()), (messages[i].User == null ? "null" : messages[i].User.Name), messages[i].RawText));
						if( messages[i].Attachments != null && messages[i].Attachments.Length > 0 )
							foreach(Message.Attachment attachment in messages[i].Attachments)
								writer.WriteLine("  attachment: " + attachment.Url);
					}

					await Task.Yield();
				}
			}
		}

		public string GetFile()
		{
			return File.ReadAllText(this.Filename);
		}

		public void OverwriteFile(string newContent)
		{
			File.WriteAllText(this.Filename, newContent);
		}
	}
}
