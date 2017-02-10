using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;

using guid = System.UInt64;

namespace Botwinder.Entities
{
	public class Operation
	{
		public enum State
		{
			Ready = 0,
			Awaiting,
			AwaitDone,
			Running,
			Finished,
			Canceled
		}

		public CommandArguments CommandArgs = null;
		public State CurrentState = State.Ready;
		public DateTime TimeCreated = DateTime.UtcNow;
		public DateTime TimeStarted = DateTime.MinValue;
		public float AllocatedMemoryStarted = 0f;
		public bool IsLarge = false;

		private Operation(CommandArguments commandArgs, float memory, bool isLarge)
		{
			this.CommandArgs = commandArgs;
			this.AllocatedMemoryStarted = memory;
			this.IsLarge = isLarge;
		}

		/// <summary> Create a new Operation and add it to the queue. </summary>
		public static Operation Create<TUser>(IBotwinderClient<TUser> sender, CommandArguments e, bool isLarge = false) where TUser : UserData, new()
		{
			Operation op = new Operation(e, GC.GetTotalMemory(false) / 1000000f, isLarge);
			sender.CurrentOperations.Add(op);
			return op;
		}

		/// <summary> This is blocking call that will await til there are less than config.MaximumConcurrentOperations.
		/// Returns true if it was canceled, false otherwise. </summary>
		public async Task<bool> Await<TUser>(IBotwinderClient<TUser> sender, Func<Task> OnAwaitStarted) where TUser : UserData, new()
		{
			sender.TotalOperationsSinceStart++;
			Operation alreadyInQueue = sender.CurrentOperations.FirstOrDefault(o => o.CommandArgs.Command.ID == this.CommandArgs.Command.ID && o.CommandArgs.Message.Channel.Id == this.CommandArgs.Message.Channel.Id && o != this);
			if( alreadyInQueue != null )
			{
				sender.CurrentOperations.Remove(this);
				this.CurrentState = State.Canceled;
				return true;
			}

			if( sender.IsContributor(this.CommandArgs.Message.User) || sender.IsContributor(this.CommandArgs.Message.Server.Owner) )
			{
				sender.CurrentOperations.Remove(this);
				sender.CurrentOperations.Insert(0, this);
			}
			else
			{
				int index = 0;
				while(this.CurrentState != State.Canceled && !((index = sender.CurrentOperations.IndexOf(this)) < sender.GlobalConfig.MaximumConcurrentOperations ||
					(index < sender.GlobalConfig.MaximumConcurrentOperations + sender.GlobalConfig.ExtraSmallOperations &&
					 !this.IsLarge && !sender.CurrentOperations.Take(index).Any(op => op.IsLarge))) )
				{
					if( this.CurrentState == State.Ready )
					{
						this.CurrentState = State.Awaiting;
						if( OnAwaitStarted != null )
							await OnAwaitStarted();
					}

					await Task.Delay(1000);
				}
			}

			if( this.CurrentState != State.Canceled )
			{
				this.TimeStarted = DateTime.UtcNow;
				this.CurrentState = State.AwaitDone;
			}

			return this.CurrentState == State.Canceled;
		}

		/// <summary> This is blocking call that will await til the connection is peachy.
		/// Returns true if the operation was canceled, false otherwise. </summary>
		public async Task<bool> AwaitConnection<TUser>(IBotwinderClient<TUser> sender) where TUser: UserData, new()
		{
			while(this.CurrentState != State.Canceled && this.CommandArgs.Message.Client.State != ConnectionState.Connected)
				await Task.Delay(1000);

			return this.CurrentState == State.Canceled;
		}

		/// <summary> Gracefully cancel this operation and remove it from the queue. </summary>
		public void Cancel<TUser>(IBotwinderClient<TUser> sender) where TUser : UserData, new()
		{
			this.CurrentState = State.Canceled;
			sender.CurrentOperations.Remove(this);
		}

		/// <summary> Gracefully finalise this operation and remove it from the list. </summary>
		public void Finalise<TUser>(IBotwinderClient<TUser> sender) where TUser : UserData, new()
		{
			this.CurrentState = State.Finished;
			sender.CurrentOperations.Remove(this);
		}
	}
}
