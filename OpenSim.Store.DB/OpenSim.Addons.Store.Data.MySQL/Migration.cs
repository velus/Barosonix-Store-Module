using log4net;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
namespace OpenSim.Addons.Store.Data
{
	public class Migration
	{
		private delegate void FlushProc();
		private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
		protected string _type;
		protected DbConnection _conn;
		protected Assembly _assem;
		private Regex _match_old;
		private Regex _match_new;
		public int Version
		{
			get
			{
				return this.FindVersion(this._conn, this._type);
			}
			set
			{
				if (this.Version < 1)
				{
					this.InsertVersion(this._type, value);
					return;
				}
				this.UpdateVersion(this._type, value);
			}
		}
		public Migration()
		{
		}
		public Migration(DbConnection conn, Assembly assem, string subtype, string type)
		{
			this.Initialize(conn, assem, type, subtype);
		}
		public Migration(DbConnection conn, Assembly assem, string type)
		{
			this.Initialize(conn, assem, type, "");
		}
		public void Initialize(DbConnection conn, Assembly assem, string type, string subtype)
		{
			this._type = type;
			this._conn = conn;
			this._assem = assem;
			this._match_old = new Regex(subtype + "\\.(\\d\\d\\d)_" + this._type + "\\.sql");
			string str = string.IsNullOrEmpty(subtype) ? this._type : (this._type + "\\." + subtype);
			this._match_new = new Regex("\\." + str + "\\.migrations(?:\\.(?<ver>\\d+)$|.*)");
		}
		public void InitMigrationsTable()
		{
			int num = this.FindVersion(this._conn, "migrations");
			if (num <= 0)
			{
				if (num < 0)
				{
					this.ExecuteScript("create table migrations(name varchar(100), version int)");
				}
				this.InsertVersion("migrations", 1);
			}
		}
		protected virtual void ExecuteScript(DbConnection conn, string[] script)
		{
			using (DbCommand dbCommand = conn.CreateCommand())
			{
				dbCommand.CommandTimeout = 0;
				for (int i = 0; i < script.Length; i++)
				{
					string text = script[i];
					dbCommand.CommandText = text;
					try
					{
						dbCommand.ExecuteNonQuery();
					}
					catch (Exception ex)
					{
						throw new Exception(ex.Message + " in SQL: " + text);
					}
				}
			}
		}
		protected void ExecuteScript(DbConnection conn, string sql)
		{
			this.ExecuteScript(conn, new string[]
			{
				sql
			});
		}
		protected void ExecuteScript(string sql)
		{
			this.ExecuteScript(this._conn, sql);
		}
		protected void ExecuteScript(string[] script)
		{
			this.ExecuteScript(this._conn, script);
		}
		public void Update()
		{
			this.InitMigrationsTable();
			int num = this.FindVersion(this._conn, this._type);
			SortedList<int, string[]> migrationsAfter = this.GetMigrationsAfter(num);
			if (migrationsAfter.Count < 1)
			{
				return;
			}
			Migration.m_log.InfoFormat("[MIGRATIONS]: Upgrading {0} to latest revision {1}.", this._type, migrationsAfter.Keys[migrationsAfter.Count - 1]);
			Migration.m_log.Info("[MIGRATIONS]: NOTE - this may take a while, don't interrupt this process!");
			foreach (KeyValuePair<int, string[]> current in migrationsAfter)
			{
				int key = current.Key;
				try
				{
					this.ExecuteScript(current.Value);
				}
				catch (Exception ex)
				{
					Migration.m_log.DebugFormat("[MIGRATIONS]: Cmd was {0}", ex.Message.Replace("\n", " "));
					Migration.m_log.Debug("[MIGRATIONS]: An error has occurred in the migration.  If you're running OpenSim for the first time then you can probably safely ignore this, since certain migration commands attempt to fetch data out of old tables.  However, if you're using an existing database and you see database related errors while running OpenSim then you will need to fix these problems manually. Continuing.");
					this.ExecuteScript("ROLLBACK;");
				}
				if (num == 0)
				{
					this.InsertVersion(this._type, key);
				}
				else
				{
					this.UpdateVersion(this._type, key);
				}
				num = key;
			}
		}
		protected virtual int FindVersion(DbConnection conn, string type)
		{
			int result = 0;
			using (DbCommand dbCommand = conn.CreateCommand())
			{
				try
				{
					dbCommand.CommandText = "select version from migrations where name='" + type + "' order by version desc";
					using (DbDataReader dbDataReader = dbCommand.ExecuteReader())
					{
						if (dbDataReader.Read())
						{
							result = Convert.ToInt32(dbDataReader["version"]);
						}
						dbDataReader.Close();
					}
				}
				catch
				{
					result = -1;
				}
			}
			return result;
		}
		private void InsertVersion(string type, int version)
		{
			Migration.m_log.InfoFormat("[MIGRATIONS]: Creating {0} at version {1}", type, version);
			this.ExecuteScript(string.Concat(new object[]
			{
				"insert into migrations(name, version) values('",
				type,
				"', ",
				version,
				")"
			}));
		}
		private void UpdateVersion(string type, int version)
		{
			Migration.m_log.InfoFormat("[MIGRATIONS]: Updating {0} to version {1}", type, version);
			this.ExecuteScript(string.Concat(new object[]
			{
				"update migrations set version=",
				version,
				" where name='",
				type,
				"'"
			}));
		}
		private SortedList<int, string[]> GetMigrationsAfter(int after)
		{
			SortedList<int, string[]> migrations = new SortedList<int, string[]>();
			string[] manifestResourceNames = this._assem.GetManifestResourceNames();
			if (manifestResourceNames.Length == 0)
			{
				return migrations;
			}
			Array.Sort<string>(manifestResourceNames);
			int num = 0;
			Match m = null;
			string text = Array.FindLast<string>(manifestResourceNames, delegate(string nm)
			{
				m = this._match_new.Match(nm);
				return m.Success;
			});
			if (m != null && !string.IsNullOrEmpty(text))
			{
				if (m.Groups.Count <= 1 || !int.TryParse(m.Groups[1].Value, out num) || num > after)
				{
					StringBuilder sb = new StringBuilder(4096);
					int nVersion = -1;
					List<string> script = new List<string>();
					Migration.FlushProc flushProc = delegate
					{
						if (sb.Length > 0)
						{
							script.Add(sb.ToString());
							sb.Length = 0;
						}
						if (nVersion > 0 && nVersion > after && script.Count > 0 && !migrations.ContainsKey(nVersion))
						{
							migrations[nVersion] = script.ToArray();
						}
						script.Clear();
					};
					using (Stream manifestResourceStream = this._assem.GetManifestResourceStream(text))
					{
						using (StreamReader streamReader = new StreamReader(manifestResourceStream))
						{
							int num2 = 0;
							while (!streamReader.EndOfStream)
							{
								string text2 = streamReader.ReadLine();
								num2++;
								if (!string.IsNullOrEmpty(text2) && !text2.StartsWith("#"))
								{
									if (text2.Trim().Equals(":GO", StringComparison.InvariantCultureIgnoreCase))
									{
										if (sb.Length != 0)
										{
											if (nVersion > after)
											{
												script.Add(sb.ToString());
											}
											sb.Length = 0;
										}
									}
									else
									{
										if (text2.StartsWith(":VERSION ", StringComparison.InvariantCultureIgnoreCase))
										{
											flushProc();
											int num3 = text2.IndexOf('#');
											if (num3 >= 0)
											{
												text2 = text2.Substring(0, num3);
											}
											if (!int.TryParse(text2.Substring(9).Trim(), out nVersion))
											{
												Migration.m_log.ErrorFormat("[MIGRATIONS]: invalid version marker at {0}: line {1}. Migration failed!", text, num2);
												break;
											}
										}
										else
										{
											sb.AppendLine(text2);
										}
									}
								}
							}
							flushProc();
							if (after < nVersion)
							{
								after = nVersion;
							}
						}
					}
				}
			}
			string[] array = manifestResourceNames;
			for (int i = 0; i < array.Length; i++)
			{
				string text3 = array[i];
				m = this._match_old.Match(text3);
				if (m.Success)
				{
					int num4 = int.Parse(m.Groups[1].ToString());
					if (num4 > after && !migrations.ContainsKey(num4))
					{
						using (Stream manifestResourceStream2 = this._assem.GetManifestResourceStream(text3))
						{
							using (StreamReader streamReader2 = new StreamReader(manifestResourceStream2))
							{
								string text4 = streamReader2.ReadToEnd();
								migrations.Add(num4, new string[]
								{
									text4
								});
							}
						}
					}
				}
			}
			if (migrations.Count < 1)
			{
				Migration.m_log.DebugFormat("[MIGRATIONS]: {0} data tables already up to date at revision {1}", this._type, after);
			}
			return migrations;
		}
	}
}
