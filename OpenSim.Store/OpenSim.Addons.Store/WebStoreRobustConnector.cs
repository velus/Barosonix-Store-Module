using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Addons.Store.Data;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Base;
using OpenSim.Server.Handlers.Base;
using OpenSim.Services.Interfaces;
using System;
using System.Collections;
using System.Net;
using System.Reflection;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
namespace OpenSim.Store
{
	public class WebStoreRobustConnector : ServiceConnector
	{
		private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		private Functions m_Funcs;
		private IUserAccountService m_UserAccountService;
		private IPresenceService m_PresenceService;
		private IGridService m_GridService;
		private IStoreData m_Database;
		private string m_ConfigName;
		private bool m_debugEnabled;
		public bool m_Enabled;
		public WebStoreRobustConnector(IConfigSource config, IHttpServer server, string configName) : base(config, server, configName)
		{
			if (configName != string.Empty)
			{
				m_ConfigName = configName;
			}
			IConfig config2 = config.Configs["WebStore"];
			if (config2 == null)
			{
				this.m_Enabled = false;
				WebStoreRobustConnector.m_log.DebugFormat("[Web.Store.Robust.Connector]: Configuration Error Not Enabled", new object[0]);
				return;
			}
			this.m_Enabled = true;
			string @string = config2.GetString("StorageProvider", string.Empty);
			string string2 = config2.GetString("ConnectionString", string.Empty);
			string string3 = config2.GetString("Realm", "store_transactions");
			string string4 = config2.GetString("GridService", string.Empty);
			string string5 = config2.GetString("UserAccountService", string.Empty);
			string string6 = config2.GetString("PresenceService", string.Empty);
			if (@string == string.Empty || string2 == string.Empty || string4 == string.Empty || string5 == string.Empty || string6 == string.Empty)
			{
				this.m_Enabled = false;
				WebStoreRobustConnector.m_log.ErrorFormat("[Web.Store.Robust.Connector]: missing service specifications Not Enabled", new object[0]);
				return;
			}
			object[] args = new object[]
			{
				config
			};
			this.m_GridService = ServerUtils.LoadPlugin<IGridService>(string4, args);
			this.m_UserAccountService = ServerUtils.LoadPlugin<IUserAccountService>(string5, args);
			this.m_PresenceService = ServerUtils.LoadPlugin<IPresenceService>(string6, args);
			this.m_Database = ServerUtils.LoadPlugin<IStoreData>(@string, new object[]
			{
				string2,
				string3
			});
			this.m_Funcs = new Functions();
			WebStoreRobustConnector.m_log.DebugFormat("[Web.Store.Robust.Connector]: Initialzing", new object[0]);
			if (MainConsole.Instance != null)
			{
				MainConsole.Instance.Commands.AddCommand("Debug", false, "Web Store Debug", "Web Store Debug <true|false>", "This setting turns on Web Store Debug", new CommandDelegate(this.HandleDebugStoreVerbose));
			}
			server.AddXmlRPCHandler("ProcessTransaction", new XmlRpcMethod(this.ProcessTransaction));
		}
		private void HandleDebugStoreVerbose(object modules, string[] args)
		{
			if (args.Length < 4)
			{
				MainConsole.Instance.Output("Usage: Web Store Debug <true|false>");
				return;
			}
			bool debugEnabled = false;
			if (!bool.TryParse(args[3], out debugEnabled))
			{
				MainConsole.Instance.Output("Usage: Web Store Debug <true|false>");
				return;
			}
			this.m_debugEnabled = debugEnabled;
			MainConsole.Instance.OutputFormat("Web Store Debug set to {0}", new object[]
			{
				this.m_debugEnabled
			});
		}
		private TransactionData FetchTrans(UUID tranid)
		{
			return new TransactionData
			{
				Sender = this.m_Database.FetchTrans(tranid).Sender,
				Region = this.m_Database.FetchTrans(tranid).Region
			};
		}
		private XmlRpcResponse ProcessTransaction(XmlRpcRequest request, IPEndPoint remoteClient)
		{
			XmlRpcResponse xmlRpcResponse = new XmlRpcResponse();
			Hashtable hashtable = new Hashtable();
			if (request.Params.Count > 0)
			{
				Hashtable hashtable2 = (Hashtable)request.Params[0];
				string text = (string)hashtable2["TransID"];
				string val = this.m_Funcs.decode(text);
				UUID tranid = (UUID)val;
				bool flag = this.m_Database.CheckTranExists(tranid);
				if (flag)
				{
					TransactionData transactionData = this.FetchTrans(tranid);
					UUID uUID = (UUID)transactionData.Sender;
					UUID regionID = (UUID)transactionData.Region;
					if (this.m_UserAccountService.GetUserAccount(UUID.Zero, uUID) == null)
					{
						hashtable["success"] = false;
						hashtable["error"] = "Invalid Buyer UUID";
					}
					else
					{
						Hashtable hashtable3 = new Hashtable();
						Hashtable hashtable4 = new Hashtable();
						PresenceInfo[] agents = this.m_PresenceService.GetAgents(new string[]
						{
							uUID.ToString()
						});
						if (agents != null && agents.Length > 0)
						{
							PresenceInfo[] array = agents;
							for (int i = 0; i < array.Length; i++)
							{
								PresenceInfo presenceInfo = array[i];
								if (presenceInfo.RegionID != UUID.Zero)
								{
									GridRegion regionByUUID = this.m_GridService.GetRegionByUUID(UUID.Zero, presenceInfo.RegionID);
									if (this.m_debugEnabled)
									{
										WebStoreRobustConnector.m_log.DebugFormat("[Web Store Debug]: Found {0} in {1}", uUID, presenceInfo.RegionID);
									}
									if (regionByUUID != null)
									{
										hashtable3 = StoreServiceConnector.SendTransaction(regionByUUID.ServerURI, text, this.m_debugEnabled);
									}
								}
							}
							if ((string)hashtable3["success"] == "true")
							{
								GridRegion regionByUUID2 = this.m_GridService.GetRegionByUUID(UUID.Zero, regionID);
								if (regionByUUID2 != null)
								{
									hashtable4 = StoreServiceConnector.DoDelivery(regionByUUID2.ServerURI, text, this.m_debugEnabled);
								}
								if ((string)hashtable4["success"] == "true")
								{
								}
							}
							else
							{
								string value = (string)hashtable3["result"];
								hashtable["success"] = false;
								hashtable["error"] = value;
							}
						}
						else
						{
							hashtable["success"] = false;
							hashtable["error"] = "You Must Be Logged In!";
						}
					}
				}
				else
				{
					hashtable["success"] = false;
					hashtable["error"] = "Invalid Transaction ID!";
				}
			}
			else
			{
				hashtable["success"] = false;
				hashtable["error"] = "Incorrect Parameter Count!";
			}
			xmlRpcResponse.Value = hashtable;
			return xmlRpcResponse;
		}
	}
}
