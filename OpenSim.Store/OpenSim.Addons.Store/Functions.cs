using System;
using System.Text;
namespace OpenSim.Store
{
	public class Functions
	{
		public string encode(string msg)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(msg);
			string txt = Convert.ToBase64String(bytes);
			string s = strrev(txt);
			byte[] bytes2 = Encoding.UTF8.GetBytes(s);
			return Convert.ToBase64String(bytes2);
		}
		public string decode(string msg)
		{
			byte[] bytes = Convert.FromBase64String(msg);
			string revstring = Encoding.UTF8.GetString(bytes);
			string s = this.strrev(revstring);
			byte[] bytes2 = Convert.FromBase64String(s);
			return Encoding.UTF8.GetString(bytes2);
		}
		public string strrev(string txt)
		{
			string text = "";
			for (int i = 0; i < txt.Length; i++)
			{
				char c = txt[i];
				text = new string(c, 1) + text;
			}
			return text;
		}
	}
}
