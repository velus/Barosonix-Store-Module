using log4net;
using Nwc.XmlRpc;
using System;
using System.Collections;
using System.Net;
using System.Reflection;
namespace OpenSim.Store
{
	public class StoreServiceConnector
	{
		private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		public static Hashtable SendTransaction(string url, string trans, bool debug)
		{
			Hashtable hashtable = new Hashtable();
			Hashtable hashtable2 = new Hashtable();
			hashtable2["TransID"] = trans;
			XmlRpcRequest xmlRpcRequest = new XmlRpcRequest("grid_store_transaction_message", new ArrayList
			{
				hashtable2
			});
			try
			{
				XmlRpcResponse xmlRpcResponse = xmlRpcRequest.Send(url, 10000);
				Hashtable hashtable3 = (Hashtable)xmlRpcResponse.Value;
				if (hashtable3.ContainsKey("success"))
				{
					if ((string)hashtable3["success"] == "true")
					{
						string value = (string)hashtable3["result"];
						hashtable.Add("success", "true");
						hashtable.Add("result", value);
						if (debug)
						{
							StoreServiceConnector.m_log.DebugFormat("[Web Store Debug] Success", new object[0]);
						}
					}
					else
					{
						string value2 = (string)hashtable3["result"];
						hashtable.Add("success", "false");
						hashtable.Add("result", value2);
						if (debug)
						{
							StoreServiceConnector.m_log.DebugFormat("[Web Store Debug] Fail", new object[0]);
						}
					}
				}
				else
				{
					hashtable.Add("success", "false");
					hashtable.Add("result", "The region server did not respond!");
					StoreServiceConnector.m_log.DebugFormat("[Web Store Robust Module]: No response from Region Server! {0}", url);
				}
			}
			catch (WebException ex)
			{
				hashtable.Add("success", "false");
				StoreServiceConnector.m_log.ErrorFormat("[STORE]: Error sending transaction to {0} the host didn't respond " + ex.ToString(), url);
			}
			return hashtable;
		}
		public static Hashtable DoDelivery(string url, string trans, bool debug)
		{
			Hashtable hashtable = new Hashtable();
			Hashtable hashtable2 = new Hashtable();
			hashtable2["TransID"] = trans;
			XmlRpcRequest xmlRpcRequest = new XmlRpcRequest("grid_store_delivery_message", new ArrayList
			{
				hashtable2
			});
			try
			{
				XmlRpcResponse xmlRpcResponse = xmlRpcRequest.Send(url, 10000);
				Hashtable hashtable3 = (Hashtable)xmlRpcResponse.Value;
				if (hashtable3.ContainsKey("success"))
				{
					if ((string)hashtable3["success"] == "true")
					{
						string value = (string)hashtable3["result"];
						hashtable.Add("success", "true");
						hashtable.Add("result", value);
						if (debug)
						{
							StoreServiceConnector.m_log.DebugFormat("[Web Store Debug] Success", new object[0]);
						}
					}
					else
					{
						string value2 = (string)hashtable3["result"];
						hashtable.Add("success", "false");
						hashtable.Add("result", value2);
						if (debug)
						{
							StoreServiceConnector.m_log.DebugFormat("[Web Store Debug] Fail", new object[0]);
						}
					}
				}
				else
				{
					hashtable.Add("success", "false");
					hashtable.Add("result", "The region server did not respond!");
					StoreServiceConnector.m_log.DebugFormat("[Web Store Robust Module]: No response from Region Server! {0}", url);
				}
			}
			catch (WebException ex)
			{
				hashtable.Add("success", "false");
				StoreServiceConnector.m_log.ErrorFormat("[STORE]: Error sending transaction to {0} the host didn't respond " + ex.ToString(), url);
			}
			return hashtable;
		}
	}
}
