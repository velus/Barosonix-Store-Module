using OpenMetaverse;
using System;
namespace OpenSim.Addons.Store.Data
{
	public static class DBGuid
	{
		public static UUID FromDB(object id)
		{
			if (id == null || id == DBNull.Value)
			{
				return UUID.Zero;
			}
			if (id.GetType() == typeof(Guid))
			{
				return new UUID((Guid)id);
			}
			if (id.GetType() == typeof(byte[]))
			{
				if (((byte[])id).Length == 0)
				{
					return UUID.Zero;
				}
				if (((byte[])id).Length == 16)
				{
					return new UUID((byte[])id, 0);
				}
			}
			else
			{
				if (id.GetType() == typeof(string))
				{
					if (((string)id).Length == 0)
					{
						return UUID.Zero;
					}
					if (((string)id).Length == 36)
					{
						return new UUID((string)id);
					}
				}
			}
			throw new Exception("Failed to convert db value to UUID: " + id.ToString());
		}
	}
}
