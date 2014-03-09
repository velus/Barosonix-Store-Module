using log4net;
using MySql.Data.MySqlClient;
using System;
using System.Reflection;
using System.Threading;
namespace OpenSim.Addons.Store.Data.MySQL
{
	public class MySqlFramework
	{
		private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		protected string m_connectionString;
		protected object m_dbLock = new object();
		protected MySqlFramework(string connectionString)
		{
			this.m_connectionString = connectionString;
		}
		protected int ExecuteNonQuery(MySqlCommand cmd)
		{
			object dbLock;
			Monitor.Enter(dbLock = this.m_dbLock);
			int result;
			try
			{
				using (MySqlConnection mySqlConnection = new MySqlConnection(this.m_connectionString))
				{
					mySqlConnection.Open();
					cmd.Connection = mySqlConnection;
					try
					{
						result = cmd.ExecuteNonQuery();
					}
					catch (Exception ex)
					{
						MySqlFramework.m_log.Error(ex.Message, ex);
						result = 0;
					}
				}
			}
			finally
			{
				Monitor.Exit(dbLock);
			}
			return result;
		}
	}
}
