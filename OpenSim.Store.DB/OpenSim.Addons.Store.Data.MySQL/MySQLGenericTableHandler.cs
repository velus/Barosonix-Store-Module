using MySql.Data.MySqlClient;
using OpenMetaverse;
using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
namespace OpenSim.Addons.Store.Data.MySQL
{
	public class MySQLGenericTableHandler<T> : MySqlFramework where T : class, new()
	{
		protected Dictionary<string, FieldInfo> m_Fields = new Dictionary<string, FieldInfo>();
		protected List<string> m_ColumnNames;
		protected string m_Realm;
		protected FieldInfo m_DataField;
		protected virtual Assembly Assembly
		{
			get
			{
				return base.GetType().Assembly;
			}
		}
		public MySQLGenericTableHandler(string connectionString, string realm, string storeName) : base(connectionString)
		{
			this.m_Realm = realm;
			this.m_connectionString = connectionString;
			if (storeName != string.Empty)
			{
				using (MySqlConnection mySqlConnection = new MySqlConnection(this.m_connectionString))
				{
					mySqlConnection.Open();
					Migration migration = new Migration(mySqlConnection, this.Assembly, storeName);
					migration.Update();
				}
			}
			Type typeFromHandle = typeof(T);
			FieldInfo[] fields = typeFromHandle.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public);
			if (fields.Length == 0)
			{
				return;
			}
			FieldInfo[] array = fields;
			for (int i = 0; i < array.Length; i++)
			{
				FieldInfo fieldInfo = array[i];
				if (fieldInfo.Name != "Data")
				{
					this.m_Fields[fieldInfo.Name] = fieldInfo;
				}
				else
				{
					this.m_DataField = fieldInfo;
				}
			}
		}
		private void CheckColumnNames(IDataReader reader)
		{
			if (this.m_ColumnNames != null)
			{
				return;
			}
			List<string> list = new List<string>();
			DataTable schemaTable = reader.GetSchemaTable();
			foreach (DataRow dataRow in schemaTable.Rows)
			{
				if (dataRow["ColumnName"] != null && !this.m_Fields.ContainsKey(dataRow["ColumnName"].ToString()))
				{
					list.Add(dataRow["ColumnName"].ToString());
				}
			}
			this.m_ColumnNames = list;
		}
		public virtual T[] Get(string field, string key)
		{
			return this.Get(new string[]
			{
				field
			}, new string[]
			{
				key
			});
		}
		public virtual T[] Get(string[] fields, string[] keys)
		{
			if (fields.Length != keys.Length)
			{
				return new T[0];
			}
			List<string> list = new List<string>();
			T[] result;
			using (MySqlCommand mySqlCommand = new MySqlCommand())
			{
				for (int i = 0; i < fields.Length; i++)
				{
					mySqlCommand.Parameters.AddWithValue(fields[i], keys[i]);
					list.Add("`" + fields[i] + "` = ?" + fields[i]);
				}
				string arg = string.Join(" and ", list.ToArray());
				string commandText = string.Format("select * from {0} where {1}", this.m_Realm, arg);
				mySqlCommand.CommandText = commandText;
				result = this.DoQuery(mySqlCommand);
			}
			return result;
		}
		protected T[] DoQuery(MySqlCommand cmd)
		{
			List<T> list = new List<T>();
			using (MySqlConnection mySqlConnection = new MySqlConnection(this.m_connectionString))
			{
				mySqlConnection.Open();
				cmd.Connection = mySqlConnection;
				using (IDataReader dataReader = cmd.ExecuteReader())
				{
					if (dataReader == null)
					{
						return new T[0];
					}
					this.CheckColumnNames(dataReader);
					while (dataReader.Read())
					{
						T t = Activator.CreateInstance<T>();
						foreach (string current in this.m_Fields.Keys)
						{
							if (!(dataReader[current] is DBNull))
							{
								if (this.m_Fields[current].FieldType == typeof(bool))
								{
									int num = Convert.ToInt32(dataReader[current]);
									this.m_Fields[current].SetValue(t, num != 0);
								}
								else
								{
									if (this.m_Fields[current].FieldType == typeof(UUID))
									{
										this.m_Fields[current].SetValue(t, DBGuid.FromDB(dataReader[current]));
									}
									else
									{
										if (this.m_Fields[current].FieldType == typeof(int))
										{
											int num2 = Convert.ToInt32(dataReader[current]);
											this.m_Fields[current].SetValue(t, num2);
										}
										else
										{
											this.m_Fields[current].SetValue(t, dataReader[current]);
										}
									}
								}
							}
						}
						if (this.m_DataField != null)
						{
							Dictionary<string, string> dictionary = new Dictionary<string, string>();
							foreach (string current2 in this.m_ColumnNames)
							{
								dictionary[current2] = dataReader[current2].ToString();
								if (dictionary[current2] == null)
								{
									dictionary[current2] = string.Empty;
								}
							}
							this.m_DataField.SetValue(t, dictionary);
						}
						list.Add(t);
					}
				}
			}
			return list.ToArray();
		}
		public virtual T[] Get(string where)
		{
			T[] result;
			using (MySqlCommand mySqlCommand = new MySqlCommand())
			{
				string commandText = string.Format("select * from {0} where {1}", this.m_Realm, where);
				mySqlCommand.CommandText = commandText;
				result = this.DoQuery(mySqlCommand);
			}
			return result;
		}
		public virtual bool Store(T row)
		{
			bool result;
			using (MySqlCommand mySqlCommand = new MySqlCommand())
			{
				List<string> list = new List<string>();
				List<string> list2 = new List<string>();
				foreach (FieldInfo current in this.m_Fields.Values)
				{
					list.Add(current.Name);
					list2.Add("?" + current.Name);
					if (current.GetValue(row) == null)
					{
						throw new NullReferenceException(string.Format("[MYSQL GENERIC TABLE HANDLER]: Trying to store field {0} for {1} which is unexpectedly null", current.Name, row));
					}
					mySqlCommand.Parameters.AddWithValue(current.Name, current.GetValue(row).ToString());
				}
				if (this.m_DataField != null)
				{
					Dictionary<string, string> dictionary = (Dictionary<string, string>)this.m_DataField.GetValue(row);
					foreach (KeyValuePair<string, string> current2 in dictionary)
					{
						list.Add(current2.Key);
						list2.Add("?" + current2.Key);
						mySqlCommand.Parameters.AddWithValue("?" + current2.Key, current2.Value);
					}
				}
				string commandText = string.Concat(new string[]
				{
					string.Format("replace into {0} (`", this.m_Realm),
					string.Join("`,`", list.ToArray()),
					"`) values (",
					string.Join(",", list2.ToArray()),
					")"
				});
				mySqlCommand.CommandText = commandText;
				if (base.ExecuteNonQuery(mySqlCommand) > 0)
				{
					result = true;
				}
				else
				{
					result = false;
				}
			}
			return result;
		}
		public virtual bool Delete(string field, string key)
		{
			return this.Delete(new string[]
			{
				field
			}, new string[]
			{
				key
			});
		}
		public virtual bool Delete(string[] fields, string[] keys)
		{
			if (fields.Length != keys.Length)
			{
				return false;
			}
			List<string> list = new List<string>();
			bool result;
			using (MySqlCommand mySqlCommand = new MySqlCommand())
			{
				for (int i = 0; i < fields.Length; i++)
				{
					mySqlCommand.Parameters.AddWithValue(fields[i], keys[i]);
					list.Add("`" + fields[i] + "` = ?" + fields[i]);
				}
				string arg = string.Join(" and ", list.ToArray());
				string commandText = string.Format("delete from {0} where {1}", this.m_Realm, arg);
				mySqlCommand.CommandText = commandText;
				result = (base.ExecuteNonQuery(mySqlCommand) > 0);
			}
			return result;
		}
		public long GetCount(string field, string key)
		{
			return this.GetCount(new string[]
			{
				field
			}, new string[]
			{
				key
			});
		}
		public long GetCount(string[] fields, string[] keys)
		{
			if (fields.Length != keys.Length)
			{
				return 0L;
			}
			List<string> list = new List<string>();
			long result;
			using (MySqlCommand mySqlCommand = new MySqlCommand())
			{
				for (int i = 0; i < fields.Length; i++)
				{
					mySqlCommand.Parameters.AddWithValue(fields[i], keys[i]);
					list.Add("`" + fields[i] + "` = ?" + fields[i]);
				}
				string arg = string.Join(" and ", list.ToArray());
				string commandText = string.Format("select count(*) from {0} where {1}", this.m_Realm, arg);
				mySqlCommand.CommandText = commandText;
				object value = this.DoQueryScalar(mySqlCommand);
				result = Convert.ToInt64(value);
			}
			return result;
		}
		public long GetCount(string where)
		{
			long result;
			using (MySqlCommand mySqlCommand = new MySqlCommand())
			{
				string commandText = string.Format("select count(*) from {0} where {1}", this.m_Realm, where);
				mySqlCommand.CommandText = commandText;
				object value = this.DoQueryScalar(mySqlCommand);
				result = Convert.ToInt64(value);
			}
			return result;
		}
		public object DoQueryScalar(MySqlCommand cmd)
		{
			object result;
			using (MySqlConnection mySqlConnection = new MySqlConnection(this.m_connectionString))
			{
				mySqlConnection.Open();
				cmd.Connection = mySqlConnection;
				result = cmd.ExecuteScalar();
			}
			return result;
		}
	}
}
