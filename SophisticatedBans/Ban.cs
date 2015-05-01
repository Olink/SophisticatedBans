using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using TShockAPI;
using TShockAPI.DB;

namespace SophisticatedBans
{
    class Ban
    {
        public Ban()
        {
            IPv4Address = "";
            UserId = -2;
            UserAccountName = "";
            CharacterName = "";
        }

        public Ban(string ip, int id, string uan, string name)
        {
            IPv4Address = ip;
            UserId = id;
            UserAccountName = uan;
            CharacterName = name;
        }

        //Methods for bans
        public String IPv4Address { get; set; }
        public int UserId { get; set; }
        public String UserAccountName { get; set; }
        public String CharacterName { get; set; }

        //Ban information
        public DateTime BanIssued { get; set; }
        public Int64 BanExpires { get; set; }
        public String BanReason { get; set; }
        public User Banner { get; set; }

        public bool HasExpired()
        {
            return BanIssued.AddSeconds(BanExpires) <= DateTime.Now;
        }
    }
}
