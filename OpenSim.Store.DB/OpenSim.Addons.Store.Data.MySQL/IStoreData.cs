using OpenMetaverse;
using System;
namespace OpenSim.Addons.Store.Data
{
	public interface IStoreData
	{
		bool CheckTranExists(UUID tranid);
		bool CheckSessionExists(UUID session);
		void UpdateTranDeclined(UUID tranid);
		void UpdateTranAccepted(UUID tranid);
		void UpdateTranPaid(UUID tranid);
		void UpdateTranOffered(UUID tranid);
		void UpdateTranSession(UUID tranid, UUID session);
		UUID FetchTransFromSession(UUID session);
		TransactionData FetchTrans(UUID tranid);
	}
}
