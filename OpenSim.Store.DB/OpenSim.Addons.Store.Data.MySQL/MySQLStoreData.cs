using MySql.Data.MySqlClient;
using OpenMetaverse;
using System;
using System.Data;
namespace OpenSim.Addons.Store.Data.MySQL
{
	public class MySQLStoreData : MySQLGenericTableHandler<StoreData>, IStoreData
	{
		public MySQLStoreData(string connectionString, string realm) : base(connectionString, realm, "Store_Trans")
		{
			this.m_Realm = realm;
			this.m_connectionString = connectionString;
		}
		public void UpdateTranPaid(UUID tranid)
		{
			string text = tranid.ToString();
			using (MySqlCommand mySqlCommand = new MySqlCommand())
			{
				mySqlCommand.CommandText = string.Format("update {0} set paid=NOW() where `UUID`=?tranID", this.m_Realm);
				mySqlCommand.Parameters.AddWithValue("?tranID", text.ToString());
				base.ExecuteNonQuery(mySqlCommand);
			}
		}
		public void UpdateTranAccepted(UUID tranid)
		{
			string text = tranid.ToString();
			using (MySqlCommand mySqlCommand = new MySqlCommand())
			{
				mySqlCommand.CommandText = string.Format("update {0} set accepted =NOW() where `UUID`=?tranID", this.m_Realm);
				mySqlCommand.Parameters.AddWithValue("?tranID", text.ToString());
				base.ExecuteNonQuery(mySqlCommand);
			}
		}
		public void UpdateTranDeclined(UUID tranid)
		{
			string text = tranid.ToString();
			using (MySqlCommand mySqlCommand = new MySqlCommand())
			{
				mySqlCommand.CommandText = string.Format("update {0} set declined =NOW() where `UUID`=?tranID", this.m_Realm);
				mySqlCommand.Parameters.AddWithValue("?tranID", text.ToString());
				base.ExecuteNonQuery(mySqlCommand);
			}
		}
		public void UpdateTranOffered(UUID tranid)
		{
			string text = tranid.ToString();
			using (MySqlCommand mySqlCommand = new MySqlCommand())
			{
				mySqlCommand.CommandText = string.Format("update {0} set offered =NOW() where `UUID`=?tranID", this.m_Realm);
				mySqlCommand.Parameters.AddWithValue("?tranID", text.ToString());
				base.ExecuteNonQuery(mySqlCommand);
			}
		}
		public void UpdateTranSession(UUID tranid, UUID session)
		{
			string text = tranid.ToString();
			string text2 = session.ToString();
			using (MySqlCommand mySqlCommand = new MySqlCommand())
			{
				mySqlCommand.CommandText = string.Format("update {0} set session = ?sessID where `UUID`=?tranID", this.m_Realm);
				mySqlCommand.Parameters.AddWithValue("?tranID", text.ToString());
				mySqlCommand.Parameters.AddWithValue("?sessID", text2.ToString());
				base.ExecuteNonQuery(mySqlCommand);
			}
		}
		public bool CheckTranExists(UUID tranid)
		{
			bool result;
			using (MySqlConnection mySqlConnection = new MySqlConnection(this.m_connectionString))
			{
				mySqlConnection.Open();
				using (MySqlCommand mySqlCommand = new MySqlCommand("select * from `" + this.m_Realm + "` where UUID = ?tranID", mySqlConnection))
				{
					mySqlCommand.Parameters.AddWithValue("?tranID", tranid.ToString());
					IDataReader dataReader = mySqlCommand.ExecuteReader();
					if (dataReader.Read())
					{
						result = true;
					}
					else
					{
						result = false;
					}
				}
			}
			return result;
		}
		public bool CheckSessionExists(UUID session)
		{
			bool result;
			using (MySqlConnection mySqlConnection = new MySqlConnection(this.m_connectionString))
			{
				mySqlConnection.Open();
				using (MySqlCommand mySqlCommand = new MySqlCommand("select * from `" + this.m_Realm + "` where session = ?sessID", mySqlConnection))
				{
					mySqlCommand.Parameters.AddWithValue("?sessID", session.ToString());
					IDataReader dataReader = mySqlCommand.ExecuteReader();
					if (dataReader.Read())
					{
						result = true;
					}
					else
					{
						result = false;
					}
				}
			}
			return result;
		}
		public TransactionData FetchTrans(UUID tranid)
		{
			TransactionData transactionData = new TransactionData();
			using (MySqlConnection mySqlConnection = new MySqlConnection(this.m_connectionString))
			{
				mySqlConnection.Open();
				using (MySqlCommand mySqlCommand = new MySqlCommand("select * from `" + this.m_Realm + "` where UUID = ?tranID", mySqlConnection))
				{
					mySqlCommand.Parameters.AddWithValue("?tranID", tranid.ToString());
					IDataReader dataReader = mySqlCommand.ExecuteReader();
					if (dataReader.Read())
					{
						transactionData.Receiver = (string)dataReader["receiver"];
						transactionData.Sender = (string)dataReader["sender"];
						transactionData.Amount = (int)dataReader["amount"];
						transactionData.Region = (string)dataReader["mbrid"];
						transactionData.Box = (string)dataReader["mbkey"];
						transactionData.Item = (string)dataReader["itemname"];
					}
				}
			}
			return transactionData;
		}
		public UUID FetchTransFromSession(UUID session)
		{
			UUID result = default(UUID);
			using (MySqlConnection mySqlConnection = new MySqlConnection(this.m_connectionString))
			{
				mySqlConnection.Open();
				using (MySqlCommand mySqlCommand = new MySqlCommand("select * from `" + this.m_Realm + "` where session = ?sessionID", mySqlConnection))
				{
					mySqlCommand.Parameters.AddWithValue("?sessionID", session.ToString());
					IDataReader dataReader = mySqlCommand.ExecuteReader();
					if (dataReader.Read())
					{
						string val = dataReader["UUID"].ToString();
						result = (UUID)val;
					}
				}
			}
			return result;
		}
	}
}
