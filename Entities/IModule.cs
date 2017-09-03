using System;
using System.Threading.Tasks;
using System.Collections.Generic;

using guid = System.Int64;

namespace Botwinder.entities
{
	public interface IModule
	{
		/// <summary> Correctly log exceptions. </summary>
		Func<Exception, string, guid, Task> HandleException{ get; set; }

		/// <summary> Initialise the module, startup call only. </summary>
		/// <returns> Return a list of Commands for this module. </returns>
		Task<List<Command>> Init<TUser>(IBotwinderClient<TUser> client) where TUser: UserData, new();

		/// <summary> Main Update loop for this module. Do whatever you want. </summary>
		Task Update<TUser>(IBotwinderClient<TUser> client) where TUser: UserData, new();
	}
}
