using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Botwinder.Entities;

using guid = System.UInt64;

namespace Botwinder.Modules
{
	public interface IModule
	{
		/// <summary> Initialise the module, startup call only. </summary>
		/// <returns> Return a list of Commands for this module. </returns>
		List<Command> Init<TUser>(IBotwinderClient<TUser> client) where TUser: UserData, new();

		/// <summary> Main Update loop for this module. Do whatever you want. </summary>
		Task Update<TUser>(IBotwinderClient<TUser> client) where TUser: UserData, new();
		bool UpdateInProgress{ get; set; }

		//TODO OnMessageReceived //e.g. Cookies!

		event EventHandler<ModuleExceptionArgs> HandleException;
	}

	public class ModuleExceptionArgs
	{
		public Exception Exception{ get; private set; }
		public string Data{ get; private set; }

		public ModuleExceptionArgs(Exception exception, string data)
		{
			this.Exception = exception;
			this.Data = data;
		}
	}
}
