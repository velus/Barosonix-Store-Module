using OpenMetaverse;
using System;
namespace OpenSim.Addons.Store.Data
{
	public class TransactionData
	{
		private UUID m_uuid;
		private string m_sender = string.Empty;
		private string m_receiver = string.Empty;
		private string m_ruuid = string.Empty;
		private string m_mbuuid = string.Empty;
		private string m_iname = string.Empty;
		private int m_amount;
		public UUID TransUUID
		{
			get
			{
				return this.m_uuid;
			}
			set
			{
				this.m_uuid = value;
			}
		}
		public string Region
		{
			get
			{
				return this.m_ruuid;
			}
			set
			{
				this.m_ruuid = value;
			}
		}
		public string Box
		{
			get
			{
				return this.m_mbuuid;
			}
			set
			{
				this.m_mbuuid = value;
			}
		}
		public string Item
		{
			get
			{
				return this.m_iname;
			}
			set
			{
				this.m_iname = value;
			}
		}
		public string Sender
		{
			get
			{
				return this.m_sender;
			}
			set
			{
				this.m_sender = value;
			}
		}
		public string Receiver
		{
			get
			{
				return this.m_receiver;
			}
			set
			{
				this.m_receiver = value;
			}
		}
		public int Amount
		{
			get
			{
				return this.m_amount;
			}
			set
			{
				this.m_amount = value;
			}
		}
	}
}
