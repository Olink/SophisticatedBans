using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Bson;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;

namespace SophisticatedBans
{
    [ApiVersion(1,17)]
    public class SophisticatedBans : TerrariaPlugin
    {
        public Config Config = new Config();
        private const string configPath = "SophisticatedBans.json";
        private IDbConnection database = null;
        private BanDatabase banManager = null;

        public override string Author
        {
            get { return "OlinkXZ, Sponsered by WhiteXZ"; }
        }

        public override string Description
        {
            get { return "The Ban System that should have been, not that abominable mess that is TShock."; }
        }

        public override string Name
        {
            get { return "Sophisticated Bans"; }
        }

        public override Version Version
        {
            get { return new Version(1, 0, 0, 0); }
        }

        public SophisticatedBans(Main game) : base(game)
        {
            
        }

        public override void Initialize()
        {
            if (File.Exists(configPath))
            {
                Config = Config.Read(configPath);
            }
            Config.Write(configPath);

            if (Config.StorageType.ToLower() == "sqlite")
            {
                string sql = Path.Combine(TShock.SavePath, "SophisticatedBans.sqlite");
                database = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));
            }
            else if (Config.StorageType.ToLower() == "mysql")
            {
                try
                {
                    var hostport = Config.MySqlHost.Split(':');
                    database = new MySqlConnection();
                    database.ConnectionString =
                        String.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
                            hostport[0],
                            hostport.Length > 1 ? hostport[1] : "3306",
                            Config.MySqlDbName,
                            Config.MySqlUsername,
                            Config.MySqlPassword
                            );
                }
                catch (MySqlException ex)
                {
                    ServerApi.LogWriter.PluginWriteLine(this, ex.ToString(), TraceLevel.Error);
                    throw new Exception("MySql not setup correctly");
                }
            }
            else
            {
                throw new Exception("Invalid storage type");
            }

            banManager = new BanDatabase(database);

            RegisterHandlers();
            RegisterCommands();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DeregisterHandlers();
            }
            base.Dispose(disposing);
        }

        private void RegisterHandlers()
        {
            ServerApi.Hooks.ServerJoin.Register(this, HandleConnection);
        }

        private void RegisterCommands()
        {
            Commands.ChatCommands.Add(new Command("managebans", HandleBans, "sban"));
        }

        private void DeregisterHandlers()
        {
            ServerApi.Hooks.ServerJoin.Deregister(this, HandleConnection);
        }

        //Hook Handlers
        private void HandleConnection(JoinEventArgs args)
        {
            Ban ban = new Ban(Netplay.serverSock[args.Who].tcpClient.Client.RemoteEndPoint.ToString().Split(':')[0], -1, "", Main.player[args.Who].name);
            var bans = banManager.GetBans(ban).ToList();
            if (bans.Count > 0)
            {
                TShock.Players[args.Who].Disconnect(String.Format("Banned: {0}", String.IsNullOrEmpty(ban.BanReason) ? "No reason specified." : ban.BanReason));
                args.Handled = true;
            }
        }

        //Command Handlers
        private const string ADD_BAN = "add";
		private const string FIND_BAN = "find";
        private void HandleBans(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                ShowUsage(args.Player);
            }
            else
            {
                switch (args.Parameters[0])
                {
                    case ADD_BAN:
                        AddBan(args);
                        break;
					case FIND_BAN:
		                FindBan(args);
		                break;
                    default:
                        ShowUsage(args.Player);
                        break;
                }
            }
        }

        private void AddBan(CommandArgs args)
        {
            if (args.Parameters.Count < 3 || args.Parameters.Count % 2 != 1)
            {
                ShowAddUsage(args.Player);
            }
            else
            {
	            Ban ban = new Ban();
	            try
	            {
					ParseArguments(args.Parameters.GetRange(1, args.Parameters.Count - 1), out ban);
		            ban.Banner = TShock.Users.GetUserByID(args.Player.UserID);
		            ban.BanIssued = DateTime.Now;
					banManager.InsertBan(ban);
					args.Player.SendSuccessMessage("Successfully added ban.");
	            }
				catch (ArgumentException e)
				{
					args.Player.SendErrorMessage(e.Message);
				}
            }
        }

	    private void FindBan(CommandArgs args)
	    {
		    if (args.Parameters.Count < 3 || args.Parameters.Count%2 != 1)
		    {
			    ShowFindUsage(args.Player);
		    }
		    else
		    {
				Ban ban = new Ban();
			    try
			    {
					ParseArguments(args.Parameters.GetRange(1, args.Parameters.Count - 1), out ban);
					if (ban.Banner != null)
				    {
					    try
					    {
							User u = TShock.Users.GetUser(ban.Banner);
							if (u != null)
								ban.Banner = u;
					    }
					    catch (UserNotExistException e)
					    {
						    ban.Banner = null;
					    }
				    }
				    List<Ban> bans = banManager.GetBans(ban).ToList();
				    foreach (Ban b in bans)
				    {
					    string searchParams = "";
					    if (!String.IsNullOrEmpty(b.IPv4Address))
						    searchParams = b.IPv4Address;
					    if (!String.IsNullOrEmpty(b.UserAccountName))
					    {
						    if (searchParams != "")
							    searchParams += " ";
						    searchParams += "Account Name - " + b.UserAccountName;
					    }
						if (b.UserId >= 0)
						{
							if (searchParams != "")
								searchParams += " ";
							searchParams += "User ID - " + b.UserId;
						}
						if (!String.IsNullOrEmpty(b.CharacterName))
						{
							if (searchParams != "")
								searchParams += " ";
							searchParams += "Character Name - " + b.CharacterName;
						}

					    string duration = b.BanExpires > 0 ? "until " + b.BanIssued.AddSeconds(b.BanExpires).ToString() : "forever";
					    string reason = String.IsNullOrEmpty(b.BanReason) ? "for no reason specified" : b.BanReason;
						args.Player.SendInfoMessage("[{6}] {0}{1} banned {2} on {3} {4} {5}.", b.Banner == null ? "Console" : b.Banner.Name, b.Banner == null ? "" : "(" + b.Banner.ID + ")", searchParams, b.BanIssued.ToString(), duration, reason, b.RowId);
				    }
			    }
			    catch (ArgumentException e)
			    {
				    args.Player.SendErrorMessage(e.Message);
			    }
		    }
	    }

		private const string IP_PARAM = "-ip";
		private const string ID_PARAM = "-id";
		private const string UAN_PARAM = "-a";
		private const string CN_PARAM = "-n";
		private const string EXPIR_PARAM = "-e";
		private const string REASON_PARAM = "-r";
	    private const string BANNER_PARAM = "-b";
	    private void ParseArguments(List<String> args, out Ban ban)
	    {
		    ban = new Ban();
			for (int i = 0; i < args.Count; i++)
			{
				string key = args[i].ToLower();
				string value = args[++i];
				switch (key)
				{
					case IP_PARAM:
						ban.IPv4Address = value;
						break;
					case ID_PARAM:
						int userid = -1;
						if (!Int32.TryParse(value, out userid))
						{
							throw new ArgumentException("UserID Param requires a numerical value.");
						}
						ban.UserId = userid;
						break;
					case UAN_PARAM:
						ban.UserAccountName = value;
						break;
					case CN_PARAM:
						ban.CharacterName = value;
						break;
					case EXPIR_PARAM:
						Int64 expires = -1;
						if (!ParseExpiration(value, out expires))
						{
							throw new ArgumentException("Expiration Param requires '1y2M3d4h5m6s' format, with at least one of the specifiers.");
						}
						ban.BanExpires = expires;
						break;
					case REASON_PARAM:
						ban.BanReason = value;
						break;
					case BANNER_PARAM:
						User u = new User();
						int id = -1;
						u.Name = value;
						if (Int32.TryParse(value, out id))
						{
							u.ID = id;
						}
						ban.Banner = u;
						break;
					default:
						throw new ArgumentException("The argument {0} is undefined.", key);
				}
			}
	    }

        private const string YEAR_PARAM = "y";
        private const string MONTH_PARAM = "M";
        private const string DAY_PARAM = "d";
        private const string HOUR_PARAM = "h";
        private const string MINUTE_PARAM = "m";
        private const string SECOND_PARAM = "s";
        private bool ParseExpiration(String expiration, out Int64 expires)
        {
            var now = DateTime.Now;
            var expireDate = DateTime.Now;
            expires = -1;

            Regex reg = new Regex(@"(\d)+([a-zA-Z])");
            var matches = reg.Matches(expiration);
            if (matches.Count != 0)
            {
                foreach (Match match in matches)
                {
                    int value = -1;
                    switch (match.Groups[2].Value)
                    {
                        case YEAR_PARAM:
                            if (!Int32.TryParse(match.Groups[1].Value, out value))
                            {
                                return false;
                            }
                            expireDate = expireDate.AddYears(value);
                            break;
                        case MONTH_PARAM:
                            value = -1;
                            if (!Int32.TryParse(match.Groups[1].Value, out value))
                            {
                                return false;
                            }
                            expireDate = expireDate.AddMonths(value);
                            break;
                        case DAY_PARAM:
                            value = -1;
                            if (!Int32.TryParse(match.Groups[1].Value, out value))
                            {
                                return false;
                            }
                            expireDate = expireDate.AddDays(value);
                            break;
                        case HOUR_PARAM:
                            value = -1;
                            if (!Int32.TryParse(match.Groups[1].Value, out value))
                            {
                                return false;
                            }
                            expireDate = expireDate.AddHours(value);
                            break;
                        case MINUTE_PARAM:
                            value = -1;
                            if (!Int32.TryParse(match.Groups[1].Value, out value))
                            {
                                return false;
                            }
                            expireDate = expireDate.AddMinutes(value);
                            break;
                        case SECOND_PARAM:
                            value = -1;
                            if (!Int32.TryParse(match.Groups[1].Value, out value))
                            {
                                return false;
                            }
                            expireDate = expireDate.AddSeconds(value);
                            break;
                        default:
                            return false;
                    }
                }
                expires = (Int64)(expireDate - now).TotalSeconds;
                return true;
            }

            return false;
        }

        private void ShowUsage(TSPlayer player)
        {
	        ShowAddUsage(player);
			ShowFindUsage(player);
        }
        
        private void ShowAddUsage(TSPlayer player)
        {
            player.SendErrorMessage("Usage: /sban add [-ip xxx.xxx.xxx.xxx] [-id 1] [-a 'login name'] [-n 'player name'] [-e '4d5h'] [-r 'This is the reason they were banned.']");
        }

		private void ShowFindUsage(TSPlayer player)
		{
			player.SendErrorMessage("Usage: /sban find [-ip xxx.xxx.xxx.xxx] [-id 1] [-a 'login name'] [-n 'player name'] [-e '4d5h'] [-r 'This is the reason they were banned.'] [-b AccountName/UserID]");
		}
    }
}
