using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using TShockAPI;
using TShockAPI.DB;

namespace SophisticatedBans
{
    class SqlException : Exception
    {
        public SqlException(String message) : base(message)
        { }
    }

    class BanDatabase
    {
        private IDbConnection database;

        public BanDatabase(IDbConnection db)
        {
            database = db;

            var table = new SqlTable("Bans",
                                     new SqlColumn("RowID", MySqlDbType.Int32) { Primary = true, AutoIncrement = true},
			                         new SqlColumn("IP", MySqlDbType.String, 16),
			                         new SqlColumn("ID", MySqlDbType.Int32),
									 new SqlColumn("UserAccountName", MySqlDbType.VarChar, 32),
                                     new SqlColumn("CharacterName", MySqlDbType.VarChar, 20),
                                     new SqlColumn("BanningUser", MySqlDbType.Int32),
                                     new SqlColumn("Issued", MySqlDbType.Int64),
                                     new SqlColumn("Expiration", MySqlDbType.Int64),
                                     new SqlColumn("Reason", MySqlDbType.Text)
				);
			var creator = new SqlTableCreator(db,
			                                  db.GetSqlType() == SqlType.Sqlite
			                                  	? (IQueryBuilder) new SqliteQueryCreator()
			                                  	: new MysqlQueryCreator());
			creator.EnsureTableStructure(table);
        }

        public void InsertBan(Ban ban)
        {
            if (database.Query("INSERT INTO Bans (IP, ID, UserAccountName, CharacterName, BanningUser, Issued, Expiration, Reason) VALUES (@0, @1, @2, @3, @4, @5, @6, @7)", 
                               ban.IPv4Address, 
                               ban.UserId, 
                               ban.UserAccountName, 
                               ban.CharacterName,
                               ban.Banner,
                               Utils.ConvertToUnixTime(ban.BanIssued),
                               ban.BanExpires,
                               ban.BanReason) != 1)
            {
                throw new SqlException("Failed to insert ban.");
            }
        }

        public IEnumerable<Ban> GetBans(Ban lookup)
        {
            List<Ban> bans = new List<Ban>();

			//" OR @2 LIKE Bans.UserAccountName OR @3 LIKE Bans.CharacterName";
	        string queryWhere = "";
	        int index = 0;
			List<object> args = new List<object>();
	        if (!String.IsNullOrEmpty(lookup.IPv4Address))
	        {
		        queryWhere += "@" + index.ToString() + " LIKE Bans.IP";
		        index++;
		        args.Add(lookup.IPv4Address);
	        }
	        
			if (lookup.UserId != -1)
	        {
		        if (queryWhere != "")
			        queryWhere += " OR ";
				queryWhere += "ID = @" + index.ToString();
				index++;
				args.Add(lookup.UserId);
	        }
	        
			if (!String.IsNullOrEmpty(lookup.UserAccountName))
	        {
		        if (queryWhere != "")
			        queryWhere += " OR ";
		        queryWhere += "@" + index.ToString() + " LIKE Bans.UserAccountName";
				index++;
				args.Add(lookup.UserAccountName);
	        }
	        
			if (!String.IsNullOrEmpty(lookup.CharacterName))
	        {
		        if (queryWhere != "")
			        queryWhere += " OR ";
		        queryWhere += "@" + index.ToString() + " LIKE Bans.CharacterName";
				index++;
				args.Add(lookup.CharacterName);
	        }

	        var query = String.Format("SELECT * FROM Bans WHERE {0}", queryWhere);

            using (var reader = database.QueryReader(query, args.ToArray()))
            {
                while (reader.Read())
                {
                    Ban b = new Ban(reader.Get<string>("IP"),
                        reader.Get<int>("ID"),
                        reader.Get<string>("UserAccountName"),
                        reader.Get<string>("CharacterName"));
                    b.Banner = TShock.Users.GetUserByID(reader.Get<int>("BanningUser"));
                    b.BanIssued = Utils.UnixTimeToDateTime(reader.Get<Int64>("Issued"));
                    b.BanExpires = reader.Get<Int64>("Issued");
                    bans.Add(b);
                }
            }

            return bans;
        }
    }
}
