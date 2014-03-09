using log4net;
using Mono.Addins;
using Nini.Config;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Addons.Store.Data;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading;

[assembly: Addin("WebStoreRegionModule", "0.1")]
[assembly: AddinDependency("OpenSim", "0.5")]

namespace OpenSim.Store
{
	[Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "WebStoreRegionModule")]
	public class StoreModule : ISharedRegionModule, IRegionModuleBase
	{
		private static readonly ILog m_log = LogManager.GetLogger (MethodBase.GetCurrentMethod ().DeclaringType);
		private IMoneyModule m_MoneyModule;
		private IMessageTransferModule m_TransferModule;
		private IInventoryService inventoryService;
		private bool m_Enabled;
		private Functions m_Funcs;
		private UUID transID;
		private IStoreData m_Database;
		private bool m_debugEnabled;
		private List<Scene> m_Scenes = new List<Scene> ();

		public string Name {
			get {
				return "WebStoreRegionModule";
			}
		}

		public Type ReplaceableInterface {
			get {
				return null;
			}
		}

		public void Initialise (IConfigSource config)
		{
			IConfig config2 = config.Configs ["WebStore"];
			if (config2 != null) {
				StoreModule.m_log.Debug ("[Web.Store Region Module]: Initializing..");
				m_Enabled = true;
				string @string = config2.GetString ("StorageProvider", "");
				string string2 = config2.GetString ("ConnectionString", "");
				string string3 = config2.GetString ("Realm", "store_transactions");
				this.m_Funcs = new Functions ();
				if (@string == string.Empty || string2 == string.Empty) {
					m_Enabled = false;
					m_log.ErrorFormat ("[Web.Store.Region.Module]: missing service specifications Not Enabled", new object[0]);
					return;
				}
				m_Database = ServerUtils.LoadPlugin<IStoreData> (@string, new object[]
				{
					string2,
					string3
				});
				MainConsole.Instance.Commands.AddCommand ("Debug", false, "Web Store Debug", "Web Store Debug <true|false>", "This setting turns on Web Store Debug", new CommandDelegate (this.HandleDebugStoreVerbose));
			}
		}

		private void HandleDebugStoreVerbose (object modules, string[] args)
		{
			if (args.Length < 4) {
				MainConsole.Instance.Output ("Usage: Web Store Debug <true|false>");
				return;
			}
			bool debugEnabled = false;
			if (!bool.TryParse (args [3], out debugEnabled)) {
				MainConsole.Instance.Output ("Usage: Web Store Debug <true|false>");
				return;
			}
			m_debugEnabled = debugEnabled;
			MainConsole.Instance.OutputFormat ("Web Store Debug set to {0}", new object[]
			{
				m_debugEnabled
			});
		}

		public void AddRegion (Scene scene)
		{
			if (!m_Enabled) {
				return;
			}

			if (m_debugEnabled) m_log.DebugFormat("[WebStoreRegionModule]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);
			
			scene.EventManager.OnNewClient += OnNewClient;
			scene.EventManager.OnIncomingInstantMessage += OnGridInstantMessage;

			lock (m_Scenes)
			{
				m_Scenes.Add(scene);
			}
		}

		public void PostInitialise ()
		{
			if (!m_Enabled) {
				return;
			}
			MainServer.Instance.AddXmlRPCHandler ("grid_store_transaction_message", new XmlRpcMethod (this.processStoreTransactionMessage));
			MainServer.Instance.AddXmlRPCHandler ("grid_store_delivery_message", new XmlRpcMethod (this.processStoreDeliveryMessage));
		}

		public void RegionLoaded (Scene scene)
		{
		}

		public void RemoveRegion (Scene scene)
		{
			if (!m_Enabled)
				return;
			
			if (m_debugEnabled) m_log.DebugFormat("[WebStoreRegionModule]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);
			
			scene.EventManager.OnNewClient -= OnNewClient;
			scene.EventManager.OnIncomingInstantMessage -= OnGridInstantMessage;
			
			lock (m_Scenes)
			{
				m_Scenes.Remove(scene);
			}
		}

		public void Close ()
		{
			if (!m_Enabled)
				return;
			
			if (m_debugEnabled) m_log.Debug("[WebStoreRegionModule]: Shutting down WebStoreRegionModule module.");
		}

		private TransactionData FetchTrans (UUID tranid)
		{
			return new TransactionData
			{
				Amount = m_Database.FetchTrans(tranid).Amount,
				Receiver = m_Database.FetchTrans(tranid).Receiver,
				Sender = m_Database.FetchTrans(tranid).Sender,
				Box = m_Database.FetchTrans(tranid).Box,
				Item = m_Database.FetchTrans(tranid).Item
			};
		}

		private void OnNewClient (IClientAPI client)
		{
			DoStoreFolderCheck (client);
			client.OnInstantMessage += OnInstantMessage;
		}

		private void DoStoreFolderCheck (IClientAPI client)
		{
			bool hasfolder = false;
			List<InventoryFolderBase> m_invbase = new List<InventoryFolderBase> ();
			Scene scene = (Scene)client.Scene;
			inventoryService = scene.InventoryService;
			m_invbase = inventoryService.GetInventorySkeleton (client.AgentId);
			InventoryFolderBase rootFolder = inventoryService.GetRootFolder (client.AgentId);
			InventoryFolderBase folder = new InventoryFolderBase();
			foreach (InventoryFolderBase current in m_invbase) {
				if (current.Name.ToString () == "Web Store Items") {
					hasfolder = true;
				}
			}
			if (!hasfolder) {
				UUID id = UUID.Random ();
				folder = new InventoryFolderBase (id, "Web Store Items", client.AgentId, 8, rootFolder.ID, rootFolder.Version);
				inventoryService.AddFolder (folder);
			}

			ScenePresence avatar = null;
			if (scene.TryGetScenePresence(client.AgentId, out avatar))
			{ 
				scene.SendInventoryUpdate(avatar.ControllingClient, rootFolder, true, false);
			}
		}

		private void OnGridInstantMessage (GridInstantMessage msg)
		{
			OnInstantMessage (null, msg);
		}

		private XmlRpcResponse processStoreTransactionMessage (XmlRpcRequest request, IPEndPoint remoteClient)
		{
			XmlRpcResponse xmlRpcResponse = new XmlRpcResponse ();
			Hashtable hashtable = new Hashtable ();
			try {
				if (request.Params.Count > 0) {
					Hashtable hashtable2 = (Hashtable)request.Params [0];
					string msg = (string)hashtable2 ["TransID"];
					string val = m_Funcs.decode (msg);
					transID = (UUID)val;
					TransactionData transactionData = FetchTrans (transID);
					UUID uUID = (UUID)transactionData.Sender;
					UUID to = (UUID)transactionData.Receiver;
					int amount = transactionData.Amount;
					string text = "";
					foreach (Scene current in m_Scenes) {
						ScenePresence scenePresence = current.GetScenePresence (uUID);
						if (scenePresence != null && !scenePresence.IsChildAgent) {
							text = dotransaction (uUID, to, amount, 5011, "Web Store Purchase");
						}
					}
					if (text == "transaction ok") {
						hashtable ["success"] = "true";
						hashtable ["result"] = text;
					} else {
						hashtable ["success"] = "false";
						hashtable ["result"] = text;
					}
				}
			} catch (Exception exception) {
				m_log.Error ("[Web.Store.Region.Module]: Caught unexpected exception:", exception);
			}
			xmlRpcResponse.Value = hashtable;
			return xmlRpcResponse;
		}

		private XmlRpcResponse processStoreDeliveryMessage (XmlRpcRequest request, IPEndPoint remoteClient)
		{
			XmlRpcResponse xmlRpcResponse = new XmlRpcResponse ();
			Hashtable hashtable = new Hashtable ();
			try {
				if (request.Params.Count > 0) {
					Hashtable hashtable2 = (Hashtable)request.Params [0];
					string msg = (string)hashtable2 ["TransID"];
					string val = m_Funcs.decode (msg);
					this.transID = (UUID)val;
					TransactionData transactionData = FetchTrans (transID);
					UUID buyer = (UUID)transactionData.Sender;
					UUID box = (UUID)transactionData.Box;
					string item = transactionData.Item;
					string text = dodelivery (buyer, box, item);
					if (text == "Item Delivered") {
						hashtable ["success"] = "true";
						hashtable ["result"] = text;
					} else {
						hashtable ["success"] = "false";
						hashtable ["result"] = text;
					}
				}
			} catch (Exception exception) {
				m_log.Error ("[Web.Store.Region.Module]: Caught unexpected exception:", exception);
			}
			xmlRpcResponse.Value = hashtable;
			return xmlRpcResponse;
		}

		private string dotransaction (UUID from, UUID to, int amount, int type, string message)
		{
			IClientAPI clientAPI = LocateClient (from);
			Scene scene = (Scene)clientAPI.Scene;
			m_MoneyModule = scene.RequestModuleInterface<IMoneyModule> ();
			m_TransferModule = scene.RequestModuleInterface<IMessageTransferModule> ();
			if (m_MoneyModule == null) {
				return "No Money Module Configured";
			}
			bool flag = m_MoneyModule.AmountCovered (from, amount);
			if (flag) {
				scene.ProcessMoneyTransferRequest (from, to, amount, type, message);
				if (m_TransferModule != null) {
					GridInstantMessage im = new GridInstantMessage(
						scene, UUID.Zero, "Web Store",
						from,
						(byte)InstantMessageDialog.MessageFromAgent,
						"You made a Web Store purchase your item will be delivered shortly", false,
						new Vector3());
					m_TransferModule.SendInstantMessage (im, delegate(bool success) {
					});
				}
				m_Database.UpdateTranPaid (transID);
				return "transaction ok";
			}
			return "Insuficient Funds";
		}

		private string dodelivery (UUID buyer, UUID box, string item)
		{
			List<UUID> list = new List<UUID> ();

			SceneObjectPart mbox = LocateBox (box);
			Scene current = LocateBoxScene (box);

			m_TransferModule = current.RequestModuleInterface<IMessageTransferModule> ();
			TaskInventoryItem inventoryItem = mbox.Inventory.GetInventoryItem (item);

			if (inventoryItem != null && inventoryItem.Type == 6) {
				list.Add (inventoryItem.ItemID);
			}



			UUID uUID = MoveInventory (current,buyer, item, mbox, list);
			m_Database.UpdateTranSession (transID, uUID);
			if (this.m_TransferModule != null) {

				byte[] copyIDBytes = uUID.GetBytes();
				byte[] binaryBucket = new byte[1 + copyIDBytes.Length];
				binaryBucket[0] = (byte)AssetType.Folder;
				Array.Copy(copyIDBytes, 0, binaryBucket, 1, copyIDBytes.Length);
				//byte[] bucket = new byte[] { (byte)AssetType.Folder };
				Vector3 absolutePosition = mbox.AbsolutePosition;

				GridInstantMessage im 
					= new GridInstantMessage(
						current, 
						mbox.OwnerID, 
						"Web Store", 
						buyer, 
						(byte)InstantMessageDialog.InventoryOffered,
						false,
						item, 
						uUID,
						false, 
						absolutePosition,
						binaryBucket,
						true);

				//GridInstantMessage im = new GridInstantMessage(
				//	current, mbox.OwnerID, mbox.Name,
				//	buyer,
				//	(byte)InstantMessageDialog.InventoryOffered,false,
				//	string.Format ("'{0}'", item),uUID, false,
				//	absolutePosition,bucket,false);
				//GridInstantMessage im = new GridInstantMessage (current, mbox.OwnerID, mbox.Name, buyer, 9, false, string.Format ("'{0}'", item), uUID, false, absolutePosition, binaryBucket, false);
				m_TransferModule.SendInstantMessage (im, delegate(bool success) {
				});
				m_Database.UpdateTranOffered (transID);
			}


			return "Item Delivered";
		}

		private InventoryItemBase CreateAgentInventoryItemFromTask(Scene scene,UUID destAgent, SceneObjectPart part, UUID itemId)
		{
			TaskInventoryItem taskItem = part.Inventory.GetInventoryItem(itemId);
			
			if (null == taskItem)
			{
				m_log.ErrorFormat(
					"[PRIM INVENTORY]: Tried to retrieve item ID {0} from prim {1}, {2} for creating an avatar"
					+ " inventory item from a prim's inventory item "
					+ " but the required item does not exist in the prim's inventory",
					itemId, part.Name, part.UUID);
				
				return null;
			}
			
			if ((destAgent != taskItem.OwnerID) && ((taskItem.CurrentPermissions & (uint)OpenSim.Framework.PermissionMask.Transfer) == 0))
			{
				return null;
			}
			
			InventoryItemBase agentItem = new InventoryItemBase();
			
			agentItem.ID = UUID.Random();
			agentItem.CreatorId = taskItem.CreatorID.ToString();
			agentItem.CreatorData = taskItem.CreatorData;
			agentItem.Owner = destAgent;
			agentItem.AssetID = taskItem.AssetID;
			agentItem.Description = taskItem.Description;
			agentItem.Name = taskItem.Name;
			agentItem.AssetType = taskItem.Type;
			agentItem.InvType = taskItem.InvType;
			agentItem.Flags = taskItem.Flags;
			
			if ((part.OwnerID != destAgent) && scene.Permissions.PropagatePermissions())
			{
				agentItem.BasePermissions = taskItem.BasePermissions & (taskItem.NextPermissions | (uint)OpenSim.Framework.PermissionMask.Move);
				if (taskItem.InvType == (int)InventoryType.Object)
					agentItem.CurrentPermissions = agentItem.BasePermissions & (((taskItem.CurrentPermissions & 7) << 13) | (taskItem.CurrentPermissions & (uint)OpenSim.Framework.PermissionMask.Move));
				else
					agentItem.CurrentPermissions = agentItem.BasePermissions & taskItem.CurrentPermissions;
				
				agentItem.Flags |= (uint)InventoryItemFlags.ObjectSlamPerm;
				agentItem.NextPermissions = taskItem.NextPermissions;
				agentItem.EveryOnePermissions = taskItem.EveryonePermissions & (taskItem.NextPermissions | (uint)OpenSim.Framework.PermissionMask.Move);
				agentItem.GroupPermissions = taskItem.GroupPermissions & taskItem.NextPermissions;
			}
			else
			{
				agentItem.BasePermissions = taskItem.BasePermissions;
				agentItem.CurrentPermissions = taskItem.CurrentPermissions;
				agentItem.NextPermissions = taskItem.NextPermissions;
				agentItem.EveryOnePermissions = taskItem.EveryonePermissions;
				agentItem.GroupPermissions = taskItem.GroupPermissions;
			}
			

			
			return agentItem;
		}

		public UUID MoveInventory(Scene scene,UUID destID, string category, SceneObjectPart host, List<UUID> items)
		{
			InventoryFolderBase StoreFolder = new InventoryFolderBase();
			List<InventoryFolderBase> m_invbase = new List<InventoryFolderBase> ();
			m_invbase = inventoryService.GetInventorySkeleton (destID);
			foreach (InventoryFolderBase current in m_invbase) {
				if (current.Name.ToString () == "Web Store Items") {
					StoreFolder = current;
				}
			}
			
			UUID newFolderID = UUID.Random();
			
			InventoryFolderBase newFolder = new InventoryFolderBase(newFolderID, category, destID, -1, StoreFolder.ID, StoreFolder.Version);
			inventoryService.AddFolder(newFolder);
			
			foreach (UUID itemID in items)
			{
				InventoryItemBase agentItem = CreateAgentInventoryItemFromTask(scene,destID, host, itemID);
				
				if (agentItem != null)
				{
					agentItem.Folder = newFolderID;
					
					scene.AddInventoryItem(agentItem);
				}
			}

			ScenePresence avatar = null;
			if (scene.TryGetScenePresence(destID, out avatar))
			{
				scene.SendInventoryUpdate(avatar.ControllingClient, StoreFolder, true, false);
				scene.SendInventoryUpdate(avatar.ControllingClient, newFolder, false, true);
			}
			
			return newFolderID;
		}

		private IClientAPI LocateClient (UUID agentID)
		{
			foreach (Scene current in m_Scenes) {
					ScenePresence scenePresence = current.GetScenePresence (agentID);
					if (scenePresence != null && !scenePresence.IsChildAgent) {
						return scenePresence.ControllingClient;
					}
				}
			return null;
		}

		private SceneObjectPart LocateBox (UUID box)
		{
			foreach (Scene current in m_Scenes) {
				SceneObjectPart sceneObjectPart = current.GetSceneObjectPart (box);
				if (sceneObjectPart != null) {
					return sceneObjectPart;
				}

			}
			return null;
		}

		private Scene LocateBoxScene (UUID box)
		{
			foreach (Scene current in m_Scenes) {
				SceneObjectPart sceneObjectPart = current.GetSceneObjectPart (box);
				if (sceneObjectPart != null) {
					return current;
				}
			}
			return null;
		}

		private void OnInstantMessage (IClientAPI client, GridInstantMessage im)
		{
			UUID tranid = default(UUID);
			UUID session = default(UUID);
			new UUID (im.toAgentID);
			if (im.dialog == 10) {
				string val = im.imSessionID.ToString ();
				session = (UUID)val;
				bool flag = m_Database.CheckSessionExists (session);
				if (flag) {
					tranid = m_Database.FetchTransFromSession (session);
					m_Database.UpdateTranAccepted (tranid);
				}
			}
			if (im.dialog == 11) {
				string val2 = im.imSessionID.ToString ();
				session = (UUID)val2;
				bool flag = m_Database.CheckSessionExists (session);
				if (flag) {
					tranid = m_Database.FetchTransFromSession (session);
					m_Database.UpdateTranDeclined (tranid);
				}
			}
		}
	}
}
