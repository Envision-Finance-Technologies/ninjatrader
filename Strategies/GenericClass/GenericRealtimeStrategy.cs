#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;

using System.IO;
using System.Net;
using NinjaTraderServices;
using System.Globalization;
using HtmlAgilityPack;
using System.Threading;
using MongoDB.Bson;
using NinjaTrader.Custom.Strategies;
using System.Windows.Threading;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Newtonsoft.Json;
using MongoDB.Bson.Serialization.Attributes;

#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
    public class GenericRealtimeStrategy : Strategy
    {
        //Identifiers
        public string EnterLongName;
        public string EnterShortName;
        public string StoplossName;
        public string ProfitTargetName;
        public string ExitLongName;
        public string ExitShortName;
        public string StrategyId;
        public string InstrumentName;
        public string StrategyVersionName;
        public string Temporality;
        public string Direction;

        //Object Representing 
        private Order entryOrder;
        private Order exitOrder;
        public PriceOrderExit StopLoss;
        public PriceOrderExit ProfitTarget;
        OrderEntryF LastOrderPending;
        OrderEntryF orderEntryObj;
        OrderExitFile orderExitObj;

        //Parameters 
        public double riskLevel;
        public double StopLossPercent;
        public double ProfitTargetPercent;


        //Log Json
        public bool LogJson;
        public string path;
        private Account MyAccount;
        public string pathDirectory;
        DatabaseJson Database = new DatabaseJson();

        //Telegram Messages
        TelegramBots Telegram = new TelegramBots();
        public string AlertMessageTelegram, ListTelegram, TemplateTelegramMessage;

        [XmlIgnore]
        public Dictionary<string, string> ParametersList = new Dictionary<string, string>();
        public bool TestTelegram;
        //Recovery of orders
        public bool searchDone;
        public bool PendingExitOrders;
        public bool GenerateLogPrice;
        public bool ResetRedeundanciesValidate;
        public bool searchCompressionStop;
        //Data Series index
        public int CurrentMainSeries, CurrentSecondarySeries, CurrentTertiarySeries;

        //Rollover Involved
        private Dictionary<string, long> ContractData;
        private Dictionary<int, int> ContractSession;
        public bool expiredBar, rolloverDisconnect;
        private bool exitOnRollover;
        public bool entryOnRollover;
        public string ExitLongRolloverName, EnterLongRolloverName;
        public string ExitShortRolloverName, EnterShortRolloverName;
        public DateTime rolloverDate;

        //Level II
        private MyAddOnTab_MarketDepth myAddOnTab;
        private List<long> VolsMarket;
        private List<double> PriceMarket;

        //Helpers
        public int positionSize;

        public bool StopLossSwitch, ProfitTargetSwitch, TrailingStopSwitch;
        public ExecutionSwitch executionSwitch;
        public OrderSwitch orderSwitch;
        public TemporalityProfitSwitch temporalityProfitSwitch;

        protected ReorderSwitch reorderObj;
        protected System.Timers.Timer timer;
        private Order timerOrder;
        private Execution lastestExecution;

        public int Redundancies;
        public decimal StopCompressed, divCompressed, price_diff_compress;

        protected Order order_pending;

        private List<IndicatorsLog> indicatorsLogs;

        #region global
        private string trades_collection = "LiveOperation", trades_server = "server_ninjatrader", indicator_log_collection= "IndicatorsLog", indicator_log_server = "server_ninjatrader";
        //private string trades_collection = "RealtimeDevelopmentTests", trades_server = "server_ninjatrader", indicator_log_collection= "IndicatorsLog", indicator_log_server = "server_test";
        #endregion

        #region OnStateChange
        public void StartDefault()
        {
            Calculate = Calculate.OnBarClose;
            EntriesPerDirection = 1;
            EntryHandling = EntryHandling.AllEntries;
            IsExitOnSessionCloseStrategy = false;
            IsFillLimitOnTouch = false;
            MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
            OrderFillResolution = OrderFillResolution.Standard;
            Slippage = 1;
            StartBehavior = StartBehavior.WaitUntilFlat;
            TimeInForce = TimeInForce.Gtc;
            TraceOrders = false;
            RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
            StopTargetHandling = StopTargetHandling.PerEntryExecution;
            BarsRequiredToTrade = 0;
            IsInstantiatedOnEachOptimizationIteration = true;
            IncludeTradeHistoryInBacktest = true;
            CurrentMainSeries = 0;
            CurrentSecondarySeries = 1;
            CurrentTertiarySeries = 2;
            SaveTrade = true;
            StopLossSwitch = true;
            executionSwitch = ExecutionSwitch.Common;
            orderSwitch = OrderSwitch.Common;
            emergencyPosition = EmergencyPosition.None;
            // telegramNotification = false;
            TestTelegram = false;
            temporalityProfitSwitch = TemporalityProfitSwitch.Secondary;
            EntryPriceLog = 0;
            QuantityLog = 0;
            LogPosition = false;
            DateGLog = DateTime.Now;
            SaveInDb = false;
            GenerateLogPrice = false;
            //reorderObj = ReorderSwitch.None;
            SetOrderQuantity = SetOrderQuantity.Strategy;
            StartBehavior = StartBehavior.ImmediatelySubmit;
            Redundancies = 0;
            ResetRedeundanciesValidate = false;
            searchCompressionStop = false;
            //IsResetOnNewTradingDays = null;
        }

        public void StartConfigure()
        {
            MyAccount = Account;
            riskLevel = 0;
            StopCompressed = 0;

            InstrumentName = System.Text.RegularExpressions.Regex.Match(Instrument.MasterInstrument.Name, @"^([\w\-]+)").ToString();

            TemplateTelegramMessage = String.Join(
                    "\n",
                    (Account.Name == "Sim101" ? "[Incubation" : "[Realtime") + " " + Account.Name + "]",
                    "Estrategia: [StrategyName]",
                    "Tipo de orden: [SignalName]",
                    "Instrumento: [Instrument]",
                    "Fecha: [Time]",
                    "Precio: [Price]",
                    "Cantidad: [Quantity]",
                    "StopLoss: [SLPrice]",
                    "Target: [TPrice]"
                    );

            string[] separator = new string[] { "\\" };
            var path = Directory.GetCurrentDirectory();
            var pathSplit = path.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            pathSplit[pathSplit.Count() - 1] = "files\\" + MyAccount.Name + "\\" + InstrumentName;
            pathDirectory = String.Join("\\", pathSplit);

        }

        public void SearchStrategyId()
        {
            this.pathDirectory += "\\" + Temporality;
            StrategyId = Database.GetIdStrategy(StrategyVersionName, Temporality, Direction, InstrumentName, "server_skynet", "StrategySpecs");
            this.Description = StrategyId;
            riskLevel = Database.GetRiskStrategy(StrategyVersionName, Temporality, Direction, InstrumentName, "server_skynet", "StrategySpecs") * 100;
        }

        public void GettingRealtimeOrder()
        {
            if (entryOrder != null)
                entryOrder = GetRealtimeOrder(entryOrder);
            if (exitOrder != null)
                exitOrder = GetRealtimeOrder(exitOrder);
        }

        /// <summary>
        /// Retorna true si lo crea y false si ya existe
        /// </summary>
        /// <param name="group"></param>
        /// <param name="profit"></param>
        /// <param name="stop"></param>
        /// <param name="redundancies"></param>
        /// <returns></returns>
        public bool CreateHumanSpecsDocument(string group, double profit, double stop, int redundancies)
        {
            MongoClient client_remote = new MongoClient(Database.GetUriDb());
            IMongoDatabase db_remote = client_remote.GetDatabase("server_skynet");
            var collection = db_remote.GetCollection<BsonDocument>("HumanTouchSpecs");
            var builder = Builders<BsonDocument>.Filter;

            //double pb = 0.33;

            var filter = builder.Eq("strategy_name", StrategyVersionName) & builder.Eq("temporality", Temporality) & builder.Eq("market", group) & builder.Eq("direction", Direction) & builder.Eq("instrument", InstrumentName) & builder.Eq("parameter_combination.0", profit) & builder.Eq("parameter_combination.1", stop) & builder.Eq("portfolio_type", "normal") & builder.Eq("ninja_trader", "Operativa");

            var result = collection.Find(filter).FirstOrDefault();

            if (result == null)
            {
                //No existe el registro, asi que procedemos a crearlo.
                var document = new BsonDocument {
                   { "_id", ObjectId.GenerateNewId() },
                   { "strategy_name", StrategyVersionName },
                   { "temporality", Temporality },
                   { "market", group },
                   { "direction", Direction },
                   { "instrument", InstrumentName },
                   { "parameter_combination", new BsonArray { { profit }, { stop } } },
                   { "parameter_names", new BsonArray { { "profit_target" }, { "stop_loss" } } },
                   { "portfolio_type", "normal"},
                   { "ninja_trader", "Operativa"},
                   { "type_id", 18 },
                   { "number_of_redundancies", redundancies }
                };

                collection.InsertOne(document);
                var id = document["_id"];
                //Print("Collection id: " + id);
                StrategyId = document["_id"].ToString();
                Description = StrategyId;
                Redundancies = redundancies;
                return true;
            }
            else
            {
                //Print("RESULT: " + result["_id"]);
                StrategyId = result["_id"].ToString();
                Description = StrategyId;
                Redundancies = redundancies;
                return false;
            }
        }

        public int UpdateRedundancies(int redundancies, bool resetRedundancies) 
        {
            MongoClient client_remote = new MongoClient(Database.GetUriDb());
            IMongoDatabase db_remote = client_remote.GetDatabase("server_skynet");
            var collection = db_remote.GetCollection<BsonDocument>("HumanTouchSpecs");
            var builder = Builders<BsonDocument>.Filter;
            var fields = Builders<BsonDocument>.Projection.Include("entry_date");
            var filter = builder.Eq("_id", ObjectId.Parse(StrategyId));

            var result = collection.Find(filter).FirstOrDefault();
            
            if (result != null)
            {
                //resetear
                if (resetRedundancies)
                {
                    var update = Builders<BsonDocument>.Update.Set("number_of_redundancies", redundancies);
                    collection.UpdateOne(filter, update);
                    ResetRedeundanciesValidate = false;
                    return redundancies;
                }
                else
                {

                    //Print("Redundancies: " + result["number_of_redundancies"]);
                    var n_red = Convert.ToInt32(result["number_of_redundancies"]);

                    if (n_red != 0)
                    {
                        n_red = n_red - 1;
                        var update = Builders<BsonDocument>.Update.Set("number_of_redundancies", n_red);
                        collection.UpdateOne(filter, update);
                    }
                    //var new_redundancies = 
                    return n_red;
                }
            }
            else return redundancies;
        }

        public string FindEntryDate(string id)
        { 
            var date = "";
            MongoClient client_remote = new MongoClient(Database.GetUriDb());
            IMongoDatabase db_remote = client_remote.GetDatabase(trades_server);
            var collection = db_remote.GetCollection<BsonDocument>(trades_collection);
            var builder = Builders<BsonDocument>.Filter;
            var fields = Builders<BsonDocument>.Projection.Include("entry_date");

            var sort = Builders<BsonDocument>.Sort.Descending("entry_date");
            var filter = builder.Eq("strategy_id", id) & !builder.Eq("status.1", "Completado");
            var result = collection.Find(filter).Project<BsonDocument>(fields).Sort(sort).Limit(1).ToList().FirstOrDefault();

            //Print("result:" + result);

            //Print("result find entry: " + result["entry_date"].ToString());
            if(result != null)
                date = result["entry_date"].ToString();

            return date;
        }

        public void ConfigureFuture()
        {
            if (Instruments[CurrentMainSeries].MasterInstrument.InstrumentType == InstrumentType.Future)
            {
                ContractData = new Dictionary<string, long>();
                ContractSession = new Dictionary<int, int>();
                rolloverDisconnect = false;
                expiredBar = false;
                orderSwitch = OrderSwitch.Future;
                executionSwitch = ExecutionSwitch.Future;

                string nextContract = GetNextContract(Instruments[CurrentMainSeries]);

                AddDataSeries(nextContract, BarsPeriods[CurrentMainSeries]);
                AddDataSeries(nextContract, BarsPeriods[CurrentSecondarySeries]);
                rolloverDate = GetRolloverDate(Instruments[CurrentMainSeries]);
            }

        }

        #endregion

        #region Emergency Position
        public void ActiveEmergencyPosition(int Series)
        {
            if (emergencyPosition != EmergencyPosition.None)
            {
                switch (emergencyPosition)
                {
                    case EmergencyPosition.EnterLong:
                        if (Position.MarketPosition == MarketPosition.Flat && (Direction == "Long" || Direction == "Long/Short") )
                        {
                            positionSize = GetPositionSize(BarsArray[CurrentMainSeries].Instrument.MasterInstrument, Closes[CurrentMainSeries][0]);
                            AlertMessageTelegram = TemplateTelegramMessage;
                            ParametersList.Clear();
                            EnterLong(Series, positionSize, EnterLongName);
                        }
                        emergencyPosition = EmergencyPosition.None;
                        break;
                    case EmergencyPosition.EnterShort:
                        if (Position.MarketPosition == MarketPosition.Flat && (Direction == "Short" || Direction == "Long/Short"))
                        {
                            positionSize = GetPositionSize(BarsArray[CurrentMainSeries].Instrument.MasterInstrument, Closes[CurrentMainSeries][0]);
                            AlertMessageTelegram = TemplateTelegramMessage;
                            ParametersList.Clear();
                            EnterShort(Series, positionSize, EnterShortName);
                        }
                        emergencyPosition = EmergencyPosition.None;
                        break;
                    case EmergencyPosition.ExitLong:
                        if (Position.MarketPosition == MarketPosition.Long && StopLoss != null)
                        {
                            AlertMessageTelegram = TemplateTelegramMessage;
                            ParametersList.Clear();
                            ExitLong(Series, StopLoss.Quantity, EnterLongName.Replace("_EnterLong", "_ExitLong"), EnterLongName);
                        }
                        emergencyPosition = EmergencyPosition.None;
                        break;
                    case EmergencyPosition.ExitShort:
                        if (Position.MarketPosition == MarketPosition.Short && StopLoss != null)
                        {
                            AlertMessageTelegram = TemplateTelegramMessage;
                            ParametersList.Clear();
                            ExitShort(Series, StopLoss.Quantity, EnterShortName.Replace("_EnterShort", "_ExitShort"), EnterShortName);
                        }
                        emergencyPosition = EmergencyPosition.None;
                        break;

                }
            }
        }

        #endregion

        #region Recovery of orders

        public void GenerateLog(double EntryPriceLog, int QuantityLog, DateTime DateGLog, bool SaveInDb)
        {
            //var result = Database.DeleteFile(pathDirectory, this.Name);

            var LastOrderPendingLog = Database.GetLastOrderMongo(StrategyId, Account.Name, trades_server, trades_collection);  //.GetLastOrderEntry(pathDirectory, this.Name);
            if (LastOrderPendingLog != null)
                LastOrderPendingLog = LastOrderPendingLog.Status.Last() != "Completado" && LastOrderPendingLog.Quantity.Last() > 0 ? LastOrderPendingLog : null;

            if (LastOrderPendingLog == null)
            {
                int quantity = QuantityLog;
                var time_c = DateGLog;
                orderEntryObj = new OrderEntryF()
                {
                    Name = Direction == "Long" ? EnterLongName : EnterShortName,
                    InstrumentName = InstrumentName,
                    Date = time_c,
                    Price = EntryPriceLog,
                    Status = new List<string> { "Pendiente" },
                    OrderType = Direction,
                    Quantity = new List<int> { quantity },
                    EntryRollover = false,
                    AccountName = Account.Name,
                    IdStrategy = StrategyId,
                    ExtraParameters = new BsonDocument()
                };

                if (StopLossSwitch)
                {
                    StopLoss = new PriceOrderExit()
                    {
                        Date = time_c,
                        Name = StoplossName,
                        SignalName = orderEntryObj.Name,
                        Quantity = quantity,
                        Price = Direction == "Long" ? EntryPriceLog * (1 - StopLossPercent / 100)
                                                       : EntryPriceLog * (1 + StopLossPercent / 100)

                    };
                    orderEntryObj.ExtraParameters.Add("current_stop_loss", StopLoss.Price);
                    orderEntryObj.StoplossList = new List<PriceOrderExit> { StopLoss };
                }
                else
                {
                    StopLoss = null;
                }

                if (ProfitTargetSwitch)
                {
                    ProfitTarget = new PriceOrderExit()
                    {
                        Date = time_c,
                        Name = ProfitTargetName,
                        SignalName = orderEntryObj.Name,
                        Quantity = quantity,
                        Price = Direction == "Long" ? EntryPriceLog * (1 + ProfitTargetPercent / 100)
                                                       : EntryPriceLog * (1 - ProfitTargetPercent / 100)
                    };
                    orderEntryObj.ExtraParameters.Add("current_profit_target", ProfitTarget.Price);
                    orderEntryObj.ProfitTargetList = new List<PriceOrderExit> { ProfitTarget };

                }
                else
                {
                    ProfitTarget = null;
                }
                //Pendiente de Cambio
                //result = Database.AddNewOrderEntryObject(pathDirectory, this.Name, orderEntryObj);
                //result = Database.UpdatePriceOrdersExit(pathDirectory, this.Name, StopLoss, ProfitTarget);

                //if (SaveInDb)
                //{
                if (SaveTrade)
                {
                    var result = SaveMongoOrders(orderEntryObj, trades_server, trades_collection);
                    Print("SaveMongoOrder Log: " + result);
                }
                //}
                

            }
            LogPosition = false;
            EntryPriceLog = 0;
            QuantityLog = 0;
            GenerateLogPrice = true;
            SaveInDb = false;

        }

        public void RecoveryLastOrderPending(int Series)
        {
            if (!searchDone)
            {
                if (LogPosition == true && EntryPriceLog > 0 && QuantityLog > 0)
                {
                    DateGLog = new DateTime(DateGLog.Year, DateGLog.Month, DateGLog.Day, TimeGLog.Hours, TimeGLog.Minutes, TimeGLog.Seconds);
                    GenerateLog(EntryPriceLog, QuantityLog, DateGLog, SaveInDb);
                }
                LastOrderPending = Database.GetLastOrderMongo(StrategyId, Account.Name, trades_server, trades_collection);  //.GetLastOrderEntry(pathDirectory, this.Name);
                if (LastOrderPending != null)
                    LastOrderPending = LastOrderPending.Status.Last() != "Completado" && LastOrderPending.Quantity.Last() > 0 ? LastOrderPending : null;
                searchDone = true;
            }

            if (LastOrderPending != null)
            {
                if (Time[0] >= LastOrderPending.Date)
                {
                    if (GenerateLogPrice)
                    {
                        if (Close[0] > (LastOrderPending.Price * (1 - 0.002 / 100)) &&
                            Close[0] < (LastOrderPending.Price * (1 + 0.002 / 100)))
                        {
                            if (LastOrderPending.Name == EnterLongName || LastOrderPending.Name == EnterLongRolloverName)
                            {
                                //var ask = GetCurrentAsk();
                                //EnterLongLimit(Series, false, LastOrderPending.Quantity.Last(), ask, LastOrderPending.Name);
                                Print("Recupere");
								EnterLong(Series, LastOrderPending.Quantity.Last(), LastOrderPending.Name);
                            }
                            else if (LastOrderPending.Name == EnterShortName || LastOrderPending.Name == EnterShortRolloverName)
                            {
                                //var bid = GetCurrentBid();
                                //EnterShortLimit(Series, false, LastOrderPending.Quantity.Last(), bid, LastOrderPending.Name);
                                Print("Recupere");
                                
								EnterShort(Series, LastOrderPending.Quantity.Last(), LastOrderPending.Name);
                            }
                            //Pendiente de Cambio
                            //var result = Database.DeleteFile(pathDirectory, this.Name);
                            //result = Database.AddNewOrderEntryObject(pathDirectory, this.Name, LastOrderPending);
                            //result = Database.UpdatePriceOrdersExit(pathDirectory, this.Name, LastOrderPending.StoplossList != null ? LastOrderPending.StoplossList.LastOrDefault() : null,
                            //   LastOrderPending.ProfitTargetList != null ? LastOrderPending.ProfitTargetList.LastOrDefault() : null);

                            PendingExitOrders = true;
                            LastOrderPending = null;
                            GenerateLogPrice = false;
                        }
                    }
                    else
                    {
                        if (LastOrderPending.Name == EnterLongName || LastOrderPending.Name == EnterLongRolloverName)
                        {
                            //var ask = GetCurrentAsk();
                            //EnterLongLimit(Series, false, LastOrderPending.Quantity.Last(), ask, LastOrderPending.Name);
                                Print("Recupere");
                            
							EnterLong(Series, LastOrderPending.Quantity.Last(), LastOrderPending.Name);
                        }
                        else if (LastOrderPending.Name == EnterShortName || LastOrderPending.Name == EnterShortRolloverName)
                        {
                            //var bid = GetCurrentBid();
                            //EnterShortLimit(Series, false, LastOrderPending.Quantity.Last(), bid, LastOrderPending.Name);
                                Print("Recupere");
                           
							EnterShort(Series, LastOrderPending.Quantity.Last(), LastOrderPending.Name);
                        }

                        PendingExitOrders = true;
                        LastOrderPending = null;
                    }
                    
                }
            }
        }

        public void RecoveryExits(string NameAlert)
        {
            if (PendingExitOrders)
            {
                Print("Tiene order pendiente");
                LastOrderPending = Database.GetLastOrderMongo(StrategyId, Account.Name, trades_server, trades_collection);
                if (LastOrderPending != null)
                    LastOrderPending = LastOrderPending.Status.Last() != "Completado" && LastOrderPending.Quantity.Last() > 0 ? LastOrderPending : null;
                string alert = "Conexión restablecida.Quantity: " + LastOrderPending.Quantity.Last();
                if (StopLossSwitch)
                {
                    StopLoss = LastOrderPending.StoplossList.Last();
                    alert += ", Stoploss: " + StopLoss.Price;
                    Print("[" + NameAlert + "] - Stoploss pendiente: " + StopLoss.Quantity + "  " + StopLoss.Price);
                }

                if (ProfitTargetSwitch)
                {
                    ProfitTarget = LastOrderPending.ProfitTargetList.Last();
                    alert += ", Target: " + ProfitTarget.Price;
                    Print("[" + NameAlert + "] - Target pendiente: " + ProfitTarget.Quantity + "  " + ProfitTarget.Price);
                }

                if (LastOrderPending != null && StrategyVersionName.Contains("human_touch"))
                {
                    if (LastOrderPending.ExtraParameters.GetValue("price_compress") != null)
                    {
                        StopCompressed = Convert.ToDecimal(LastOrderPending.ExtraParameters.GetValue("price_compress"));

                        divCompressed = Convert.ToDecimal(LastOrderPending.ExtraParameters.GetValue("div_compress"));

                        price_diff_compress = Convert.ToDecimal(LastOrderPending.ExtraParameters.GetValue("price_diff_compress"));

                        //Print("StopCompressed " + StopCompressed + "\n" + "divCompressed: " + divCompressed + "\n" + "price_diff_compress: " + price_diff_compress);
                    }
                }

                Alert("Conexion Restablecida", Priority.High, alert, NinjaTrader.Core.Globals.InstallDir + @"\sounds\Alert1.wav", 10, Brushes.Black, Brushes.Yellow);
                PendingExitOrders = false;
                orderEntryObj = LastOrderPending;
                LastOrderPending = null;
            }
        }

        public OrderEntryF getLastEntryOrder()
        {
            return Database.GetLastOrderMongo(StrategyId, Account.Name, trades_server, trades_collection);
        }

        #endregion

        #region StopLossConditions
        public void StopLossLongCondition(int Series, bool Test = false)
        {
            if (StopLoss != null)
            {
                if (Close[0] <= StopLoss.Price || Test == true)
                {
                    if (State == State.Realtime)
                    {
                        AlertMessageTelegram = TemplateTelegramMessage;
                        ParametersList.Clear();
                        ExitLong(Series, StopLoss.Quantity, StopLoss.Name, StopLoss.SignalName);
                    }

                }
            }
        }

        public bool StopLossLongBidCondition(int Series, bool Test = false)
        {
            if (StopLoss != null)
            {
                var bid = GetCurrentBid();
                if (Close[0] <= StopLoss.Price || Test == true)
                {
                    if (State == State.Realtime)
                    {
                        if (bid > 0)
                        {
                            AlertMessageTelegram = TemplateTelegramMessage;
                            ParametersList.Clear();
                            ParametersList.Add("Bid", bid.ToString());

                            ExitLongStopMarket(Series, true, StopLoss.Quantity, bid, StopLoss.Name, StopLoss.SignalName);
                            return true;
                        }
                    }

                }
            }
            return false;
        }

        public bool StopLossLongBidCondition(int Series, ReorderSwitch reorder, bool Test = false)
        {
            if (StopLossSwitch && StopLoss != null)
            {
                if (State == State.Realtime)
                {
                    var bid = GetCurrentBid();
                    if (bid > 0)
                    {
                        AlertMessageTelegram = TemplateTelegramMessage;
                        if (reorder == ReorderSwitch.StopLoss)
                        {
                            if (ParametersList.ContainsKey("New Bid"))
                            {
                                ParametersList["New Bid"] = bid.ToString();
                            }
                            else
                            {
                                ParametersList.Add("New Bid", bid.ToString());
                                ParametersList.Add("Delayed StopLoss", "True");
                            }
                        }
                        else if (Close[0] <= StopLoss.Price || Test)
                        {
                            ParametersList.Clear();
                            ParametersList.Add("Bid", bid.ToString());
                        }
                        else return false;

                        //timer.Start();
                        ExitLongLimit(Series, true, StopLoss.Quantity, bid, StopLoss.Name, StopLoss.SignalName);
                        return true;
                    }
                }
            }
            return false;
        }

        public void StopLossShortCondition(int Series, bool Test = false)
        {
            if (StopLoss != null)
            {
                if (Close[0] >= StopLoss.Price || Test == true)
                {
                    if (State == State.Realtime)
                    {
                        AlertMessageTelegram = TemplateTelegramMessage;
                        ParametersList.Clear();
                        ExitShort(Series, StopLoss.Quantity, StopLoss.Name, StopLoss.SignalName);
                    }

                }
            }
        }

        public bool StopLossShortAskCondition(int Series, bool Test = false)
        {
            if (StopLoss != null)
            {
                var ask = GetCurrentAsk();

                if (Close[0] >= StopLoss.Price || Test == true)
                {
                    if (State == State.Realtime)
                    {
                        if (ask > 0)
                        {

                            AlertMessageTelegram = TemplateTelegramMessage;
                            ParametersList.Clear();
                            ParametersList.Add("Ask", ask.ToString());

                            ExitShortStopMarket(Series, true, StopLoss.Quantity, ask, StopLoss.Name, StopLoss.SignalName);
                            //Print("Salida");
                            //Print("Ask: " + ask);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public bool StopLossShortAskCondition(int Series, ReorderSwitch reorder, bool Test = false)
        {
            if (StopLossSwitch && StopLoss != null)
            {
                if (State == State.Realtime)
                {
                    var ask = GetCurrentAsk();
                    if (ask > 0)
                    {
                        AlertMessageTelegram = TemplateTelegramMessage;
                        if (reorder == ReorderSwitch.StopLoss)
                        {
                            if (ParametersList.ContainsKey("New Ask"))
                            {
                                ParametersList["New Ask"] = ask.ToString();
                            }
                            else
                            {
                                ParametersList.Add("New Ask", ask.ToString());
                                ParametersList.Add("Delayed StopLoss", "True");
                            }
                        }
                        else if (Close[0] >= StopLoss.Price || Test)
                        {
                            ParametersList.Clear();
                            ParametersList.Add("Ask", ask.ToString());
                        }
                        else return false;

                        //timer.Start();
                        ExitShortLimit(Series, true, StopLoss.Quantity, ask, StopLoss.Name, StopLoss.SignalName);
                        return true;
                    }
                }
            }
            return false;
        }
        #endregion

        #region ProfitTargetConditions
        public void ProfitTargetLongCondition(int Series, bool Test = false)
        {
            if (ProfitTarget != null)
            {
                if (Close[0] >= ProfitTarget.Price || Test == true)
                {
                    if (State == State.Realtime)
                    {
                        AlertMessageTelegram = TemplateTelegramMessage;
                        ParametersList.Clear();
                        ExitLong(Series, ProfitTarget.Quantity, ProfitTarget.Name, ProfitTarget.SignalName);
                    }

                }
            }
        }

        public bool ProfitTargetLongBidCondition(int Series, bool Test = false)
        {
            if (ProfitTarget != null)
            {
                var bid = GetCurrentBid();
                if (Close[0] >= ProfitTarget.Price || Test == true)
                {
                    if (State == State.Realtime)
                    {
                        if (bid > 0)
                        {
                            AlertMessageTelegram = TemplateTelegramMessage;
                            ParametersList.Clear();
                            ParametersList.Add("Bid", bid.ToString());

                            ExitLongStopMarket(Series, true, ProfitTarget.Quantity, bid, ProfitTarget.Name, ProfitTarget.SignalName);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public bool ProfitTargetLongBidCondition(int Series, ReorderSwitch reorder, bool Test = false)
        {
            if (ProfitTargetSwitch && ProfitTarget != null)
            {
                if (State == State.Realtime)
                {
                    var bid = GetCurrentBid();
                    if (bid > 0)
                    {
                        AlertMessageTelegram = TemplateTelegramMessage;
                        if (reorder == ReorderSwitch.ProfitTarget)
                        {
                            if (ParametersList.ContainsKey("New Bid"))
                            {
                                ParametersList["New Bid"] = bid.ToString();
                            }
                            else
                            {
                                ParametersList.Add("New Bid", bid.ToString());
                                ParametersList.Add("Delayed ProfitTarget", "True");
                            }
                        }
                        else if (Close[0] >= ProfitTarget.Price || Test)
                        {
                            ParametersList.Clear();
                            ParametersList.Add("Bid", bid.ToString());
                        }
                        else return false;

                        //timer.Start();
                        ExitLongLimit(Series, true, ProfitTarget.Quantity, bid, ProfitTarget.Name, ProfitTarget.SignalName);
                        return true;
                    }
                }
            }
            return false;
        }

        public void ProfitTargetShortCondition(int Series, bool Test = false)
        {
            if (ProfitTarget != null)
            {
                if (Close[0] <= ProfitTarget.Price || Test == true)
                {
                    if (State == State.Realtime)
                    {
                        AlertMessageTelegram = TemplateTelegramMessage;
                        ParametersList.Clear();
                        ExitShort(Series, ProfitTarget.Quantity, ProfitTarget.Name, ProfitTarget.SignalName);
                    }

                }
            }
        }

        public bool ProfitTargetShortAskCondition(int Series, bool Test = false)
        {
            if (ProfitTarget != null)
            {
                var ask = GetCurrentAsk();

                if (Close[0] <= ProfitTarget.Price || Test == true)
                {
                    if (State == State.Realtime)
                    {
                        if (ask > 0)
                        {
                            AlertMessageTelegram = TemplateTelegramMessage;
                            ParametersList.Clear();
                            ParametersList.Add("Ask", ask.ToString());

                            ExitShortStopMarket(Series, true, ProfitTarget.Quantity, ask, ProfitTarget.Name, ProfitTarget.SignalName);
                            //Print("Ask: " + ask);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public bool ProfitTargetShortAskCondition(int Series, ReorderSwitch reorder, bool Test = false)
        {
            if (ProfitTargetSwitch && ProfitTarget != null)
            {
                if (State == State.Realtime)
                {
                    var ask = GetCurrentAsk();
                    if (ask > 0)
                    {
                        AlertMessageTelegram = TemplateTelegramMessage;
                        if (reorder == ReorderSwitch.ProfitTarget)
                        {
                            if (ParametersList.ContainsKey("New Ask"))
                            {
                                ParametersList["New Ask"] = ask.ToString();
                            }
                            else
                            {
                                ParametersList.Add("New Ask", ask.ToString());
                                ParametersList.Add("Delayed ProfitTarget", "True");
                            }
                        }
                        else if (Close[0] <= ProfitTarget.Price || Test)
                        {
                            ParametersList.Clear();
                            ParametersList.Add("Ask", ask.ToString());
                        }
                        else return false;

                        //timer.Start();
                        ExitShortLimit(Series, true, ProfitTarget.Quantity, ask, ProfitTarget.Name, ProfitTarget.SignalName);
                        return true;
                    }
                }
            }
            return false;
        }
        
        #endregion

        #region TrailingStopConditions
        public void TrailingStopLongCondition()
        {
            
            if (StopLoss != null)
            {
                //Print("TRAILING STOP: " + StopLossPercent);
                double trailing = Close[0] - (Close[0] * (StopLossPercent / 100));
                
                if (trailing > StopLoss.Price)
                {
                    //  stopobj = new PriceOrderExit(StopLoss);
                    string stopObj = Newtonsoft.Json.JsonConvert.SerializeObject(StopLoss);
                    PriceOrderExit StopLossDuplicate = Newtonsoft.Json.JsonConvert.DeserializeObject<PriceOrderExit>(stopObj);
                    StopLossDuplicate.Date = Time[0];
                    StopLossDuplicate.Price = trailing;
                    StopLoss = StopLossDuplicate;

                    var entry = CheckLiveOperation(orderEntryObj, trades_server, trades_collection);
                    orderEntryObj.StoplossList = new List<PriceOrderExit> { StopLoss };
                    bool operationExist = entry.ExtraParameters.Contains("operation");
                   
                    if (operationExist) 
                    {
                        if (!orderEntryObj.ExtraParameters.Contains("operation")) 
                        {
                            var operation = entry.ExtraParameters.GetValue("operation");
                            orderEntryObj.ExtraParameters.Add("operation", operation);
                        }
                    }
                   
                    var orderSave = ChangeOrdersMongo(orderEntryObj, trades_server, trades_collection);
                    //var orderSave = Database.UpdatePriceOrdersExit(pathDirectory, this.Name, StopLoss, null);
                    orderSave = Database.ChangeCurrentStoploss(StopLoss.Price, StrategyId, MyAccount.Name, trades_server, trades_collection);
                }
            }
        }

        public void TrailingStopShortCondition()
        {
            if (StopLoss != null)
            {
                double trailing = Close[0] + (Close[0] * (StopLossPercent / 100));
                if (trailing < StopLoss.Price)
                {
                    string stopObj = Newtonsoft.Json.JsonConvert.SerializeObject(StopLoss);
                    PriceOrderExit StopLossDuplicate = Newtonsoft.Json.JsonConvert.DeserializeObject<PriceOrderExit>(stopObj);
                    StopLossDuplicate.Date = Time[0];
                    StopLossDuplicate.Price = trailing;
                    StopLoss = StopLossDuplicate;

                    var entry = CheckLiveOperation(orderEntryObj, trades_server, trades_collection);
                    orderEntryObj.StoplossList = new List<PriceOrderExit> { StopLoss };
                    bool operationExist = entry.ExtraParameters.Contains("operation");

                    if (operationExist)
                    {
                        if (!orderEntryObj.ExtraParameters.Contains("operation"))
                        {
                            var operation = entry.ExtraParameters.GetValue("operation");
                            orderEntryObj.ExtraParameters.Add("operation", operation);
                        }
                    }

                    var orderSave = ChangeOrdersMongo(orderEntryObj, trades_server, trades_collection);
                    orderSave = Database.ChangeCurrentStoploss(StopLoss.Price, StrategyId, MyAccount.Name, trades_server, trades_collection);
                }
            }
        }

        public void CompressionStopLongCondition()
        {
            if (StopLoss != null)
            {
                //Print("StopLoss.Price: " + StopLoss.Price + "\ndivCompressed: " + divCompressed);
                //Print("Close[0]: " + Close[0]);
                if (Close[0] >= (double)StopCompressed) 
                {
                    double trailing = Close[0] - (Close[0] * (((double)divCompressed * StopLossPercent) / 100));
                    
                    //precio a llegar
                    StopCompressed = StopCompressed + price_diff_compress;
                    divCompressed = divCompressed / 2;
                    //divCompressed = divCompressed * (decimal)StopLossPercent;

                    orderEntryObj.ExtraParameters.Remove("price_compress");
                    orderEntryObj.ExtraParameters.Add("price_compress", StopCompressed);

                    orderEntryObj.ExtraParameters.Remove("div_compress");
                    orderEntryObj.ExtraParameters.Add("div_compress", divCompressed);

                    string stopObj = Newtonsoft.Json.JsonConvert.SerializeObject(StopLoss);
                    PriceOrderExit StopLossDuplicate = Newtonsoft.Json.JsonConvert.DeserializeObject<PriceOrderExit>(stopObj);
                    StopLossDuplicate.Date = Time[0];

                    StopLossDuplicate.Price = trailing;
                    StopLoss = StopLossDuplicate;
                    orderEntryObj.StoplossList = new List<PriceOrderExit> { StopLoss };

                    var orderSave = ChangeOrdersMongo(orderEntryObj, trades_server, trades_collection);
                    //var orderSave = Database.UpdatePriceOrdersExit(pathDirectory, this.Name, StopLoss, null);
                    orderSave = Database.ChangeCurrentStoploss(StopLoss.Price, StrategyId, MyAccount.Name, trades_server, trades_collection);
                }
                else
                {
                    TrailingStopLongCondition();            
                }
            }
        }

        public void CompressionStopShortCondition()
        {

            if (StopLoss != null)
            {
                //Print("StopLoss.Price: " + StopLoss.Price + "\ndivCompressed: " + divCompressed);
                //Print("Close[0]: " + Close[0]);
                if (Close[0] <= (double)StopCompressed)
                {
                    //double trailing = Close[0] + (Close[0] * (StopLossPercent / 100));
                    double trailing = Close[0] + (Close[0] * (((double)divCompressed * StopLossPercent) / 100));

                    //precio a llegar
                    StopCompressed = StopCompressed - price_diff_compress;
                    divCompressed = divCompressed / 2;
                    //divCompressed = divCompressed * (decimal)StopLossPercent;

                    orderEntryObj.ExtraParameters.Remove("price_compress");
                    orderEntryObj.ExtraParameters.Add("price_compress", StopCompressed);

                    orderEntryObj.ExtraParameters.Remove("div_compress");
                    orderEntryObj.ExtraParameters.Add("div_compress", divCompressed);

                    string stopObj = Newtonsoft.Json.JsonConvert.SerializeObject(StopLoss);
                    PriceOrderExit StopLossDuplicate = Newtonsoft.Json.JsonConvert.DeserializeObject<PriceOrderExit>(stopObj);
                    StopLossDuplicate.Date = Time[0];

                    StopLossDuplicate.Price = trailing;
                    StopLoss = StopLossDuplicate;
                    orderEntryObj.StoplossList = new List<PriceOrderExit> { StopLoss };

                    var orderSave = ChangeOrdersMongo(orderEntryObj, trades_server, trades_collection);
                    //var orderSave = Database.UpdatePriceOrdersExit(pathDirectory, this.Name, StopLoss, null);
                    orderSave = Database.ChangeCurrentStoploss(StopLoss.Price, StrategyId, MyAccount.Name, trades_server, trades_collection);
                }
                else
                {
                    TrailingStopShortCondition();
                }
            }
        }
        #endregion

        /// <summary>
        /// EnterLongLimitLvII 
        /// </summary>
        /// <param name="barsInProgress"></param>
        /// <param name="signalName"></param>
        /// <param name="slippage"></param>
        /// <returns></returns>
        #region EnterLongLimitLvII
        public Order EnterLongLimitLvII(int barsInProgress, string signalName, double slippage)
        {
            positionSize = GetPositionSize(BarsArray[CurrentMainSeries].Instrument.MasterInstrument, Closes[CurrentMainSeries][0]);

            Order enterlong = new Order();
            
            Task task = Task.Factory.StartNew(() =>
            {

                Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate
                {
                    // your code
                    #region Call MarketDeptTabMethod
                    this.myAddOnTab = new MyAddOnTab_MarketDepth(BarsArray[CurrentMainSeries].Instrument);
                    if (myAddOnTab != null) { }
                    //Print("Antes del error");

                    myAddOnTab.Cleanup();
                    #endregion
                });
            });

            //Thread.Sleep(10000);
            Thread.Sleep(2000);

            long Qa = myAddOnTab.listMarketDepthLevelAskVolume[0];
            int lv = 1;
            slippage = (myAddOnTab.ListMarketDepthLevelAskPrice[0] * slippage) / 100;
            slippage = myAddOnTab.ListMarketDepthLevelAskPrice[0] + slippage; // Ask suma

            var enterLevel = false;
            //Print("Slippage: " + slippage);

            //Print("PriceAsk LEVEL 1: " + this.myAddOnTab.ListMarketDepthLevelAskPrice[0]);
            //Print("PriceAsk LEVEL 2: " + this.myAddOnTab.ListMarketDepthLevelAskPrice[1]);
            //Print("PriceAsk LEVEL 3: " + this.myAddOnTab.ListMarketDepthLevelAskPrice[2]);
            //Print("PriceAsk LEVEL 4: " + this.myAddOnTab.ListMarketDepthLevelAskPrice[3]);
            //Print("PriceAsk LEVEL 5: " + this.myAddOnTab.ListMarketDepthLevelAskPrice[4]);
            //Print("PriceAsk LEVEL 6: " + this.myAddOnTab.ListMarketDepthLevelAskPrice[5]);
            //Print("PriceAsk LEVEL 7: " + this.myAddOnTab.ListMarketDepthLevelAskPrice[6]);
            //Print("PriceAsk LEVEL 8: " + this.myAddOnTab.ListMarketDepthLevelAskPrice[7]);
            //Print("PriceAsk LEVEL 9: " + this.myAddOnTab.ListMarketDepthLevelAskPrice[8]);
            //Print("PriceAsk LEVEL 10: " + this.myAddOnTab.ListMarketDepthLevelAskPrice[9]);

            // si el Qa y el Price es mayor a 0 validar todo
            if (Qa > 0 && myAddOnTab.ListMarketDepthLevelAskPrice[0] > 0)
            {
                if (Qa >= positionSize)
                {
                    //NIVEL 1
                    //Print("Nivel: " + lv);
                    //Print("Qa: " + Qa);
                    //Print("PositionSize: " + positionSize);
                    //Print("PriceAskMarket: " + myAddOnTab.ListMarketDepthLevelAskPrice[0]);
                    
                    ParametersList.Add("Order book level", lv.ToString());
                    ParametersList.Add("Ask", myAddOnTab.ListMarketDepthLevelAskPrice[0].ToString());
                    EnterLongLimit(CurrentSecondarySeries, false, positionSize, myAddOnTab.ListMarketDepthLevelAskPrice[0], signalName);
                    enterLevel = true;
                    lv = 1;
                }
                else
                {
                    for (var i = 1; i < 10; i++)
                    {
                        lv += 1;
                        // verificar que el precio de cualquier nivel sea menor al slippage calculado con el nivel 1
                        if (myAddOnTab.ListMarketDepthLevelAskPrice[i] <= slippage && enterLevel == false)
                        {
                            Qa += myAddOnTab.listMarketDepthLevelAskVolume[i];
                            //Print("Nivel: " + lv);
                            //Print("Qa: " + Qa);
                            //Print("PositionSize: " + positionSize);
                            //Print("PriceAskMarket: " + myAddOnTab.ListMarketDepthLevelAskPrice[i]);

                            if (Qa >= positionSize)
                            {
                                // Entra
                                ParametersList.Add("Order book level", lv.ToString());
                                ParametersList.Add("Ask", myAddOnTab.ListMarketDepthLevelAskPrice[i].ToString());
                                enterlong = EnterLongLimit(CurrentSecondarySeries, false, positionSize, myAddOnTab.ListMarketDepthLevelAskPrice[i], signalName);
                                enterLevel = true;
                                break;
                            }
                            else{/* sigue al siguiente nivel*/}
                        }
                        else if (enterLevel == false)
                        {
                            ParametersList.Add("Order book level", (lv-1).ToString());
                            ParametersList.Add("Ask", myAddOnTab.ListMarketDepthLevelAskPrice[i-1].ToString());
                            enterlong = EnterLongLimit(CurrentSecondarySeries, false, (int)Qa, myAddOnTab.ListMarketDepthLevelAskPrice[i-1], signalName);
                            enterLevel = true;
                            //lv = i + 1;
                            break;
                        }
                    }
                }

                if (!enterLevel && positionSize >= Qa)
                {
                    ParametersList.Add("Order book level", lv.ToString());
                    ParametersList.Add("Bid", myAddOnTab.ListMarketDepthLevelAskPrice[9].ToString());
                    enterlong = EnterLongLimit(CurrentSecondarySeries, false, (int)Qa, myAddOnTab.ListMarketDepthLevelAskPrice[9], signalName);
                    enterLevel = true;
                }
            }
            //Print("Fin Nivel: " + lv);
            return enterlong;
        }
        #endregion

        #region EnterShortLimitLvII
        /// <summary>
        /// EnterShortLimitLvII 
        /// </summary>
        /// <param name="barsInProgress"></param>
        /// <param name="signalName"></param>
        /// <param name="slippage"></param>
        /// <returns></returns>
        public Order EnterShortLimitLvII(int barsInProgress, string signalName, double slippage)
        {
            Order entershort = new Order();
            
            Task task = Task.Factory.StartNew(() =>
            {
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate
                {
                    // your code
                    #region Call MarketDeptTabMethod
                    this.myAddOnTab = new MyAddOnTab_MarketDepth(BarsArray[CurrentMainSeries].Instrument);
                    if (myAddOnTab != null) { }
                    //Print("Antes del error");

                    myAddOnTab.Cleanup();
                    #endregion
                });
                //Your code here
            });

            //Thread.Sleep(10000);
            Thread.Sleep(2000);

            //PriceMarket = new List<double>();
            //PriceMarket.Add(myAddOnTab.ListMarketDepthLevelBidPrice[0]);
            //PriceMarket.Add(myAddOnTab.ListMarketDepthLevelBidPrice[1]);
            //PriceMarket.Add(myAddOnTab.ListMarketDepthLevelBidPrice[2]);
            //PriceMarket.Add(myAddOnTab.ListMarketDepthLevelBidPrice[3]);
            //PriceMarket.Add(myAddOnTab.ListMarketDepthLevelBidPrice[4]);

            positionSize = GetPositionSize(BarsArray[CurrentMainSeries].Instrument.MasterInstrument, Closes[CurrentMainSeries][0]);
            //Print("Position Size: " + positionSize);

            //VolsMarket = new List<long>();
            //VolsMarket.Add(myAddOnTab.listMarketDepthLevelBidVolume[0]);
            //VolsMarket.Add(myAddOnTab.listMarketDepthLevelBidVolume[1]);
            //VolsMarket.Add(myAddOnTab.listMarketDepthLevelBidVolume[2]);
            //VolsMarket.Add(myAddOnTab.listMarketDepthLevelBidVolume[3]);
            //VolsMarket.Add(myAddOnTab.listMarketDepthLevelBidVolume[4]);

            long Qa = myAddOnTab.listMarketDepthLevelBidVolume[0];
            int lv = 1;
            slippage = (myAddOnTab.ListMarketDepthLevelBidPrice[0] * slippage) / 100;
            slippage = myAddOnTab.ListMarketDepthLevelBidPrice[0] - slippage; // Bid resta

            var enterLevel = false;
            //Print("Slippage: " + slippage);
            //Print("PriceBid LEVEL 1: " + this.myAddOnTab.ListMarketDepthLevelBidPrice[0]);
            //Print("PriceBid LEVEL 2: " + this.myAddOnTab.ListMarketDepthLevelBidPrice[1]);
            //Print("PriceBid LEVEL 3: " + this.myAddOnTab.ListMarketDepthLevelBidPrice[2]);
            //Print("PriceBid LEVEL 4: " + this.myAddOnTab.ListMarketDepthLevelBidPrice[3]);
            //Print("PriceBid LEVEL 5: " + this.myAddOnTab.ListMarketDepthLevelBidPrice[4]);
            //Print("PriceBid LEVEL 6: " + this.myAddOnTab.ListMarketDepthLevelBidPrice[5]);
            //Print("PriceBid LEVEL 7: " + this.myAddOnTab.ListMarketDepthLevelBidPrice[6]);
            //Print("PriceBid LEVEL 8: " + this.myAddOnTab.ListMarketDepthLevelBidPrice[7]);
            //Print("PriceBid LEVEL 9: " + this.myAddOnTab.ListMarketDepthLevelBidPrice[8]);
            //Print("PriceBid LEVEL 10: " + this.myAddOnTab.ListMarketDepthLevelBidPrice[9]);

            // si el Qa y el Price es mayor a 0 validar todo
            if (Qa > 0 && myAddOnTab.ListMarketDepthLevelBidPrice[0] > 0)
            {
                if (Qa >= positionSize)
                {
                    //NIVEL 1
                    //Print("Nivel: " + lv);
                    //Print("Qa: " + Qa);
                    //Print("PositionSize: " + positionSize);
                    //Print("PriceBidMarket: " + myAddOnTab.ListMarketDepthLevelBidPrice[0]);

                    ParametersList.Add("Order book level", lv.ToString());
                    ParametersList.Add("Bid", myAddOnTab.ListMarketDepthLevelBidPrice[0].ToString());
                    EnterShortLimit(barsInProgress, false, positionSize, myAddOnTab.ListMarketDepthLevelBidPrice[0], signalName);
                    enterLevel = true;
                    lv = 1;
                }
                else
                {
                    for (var i = 1; i < 10; i++)
                    {
                        lv += 1;
                        // si el precio de cualquier nivel es mayor al slippage calculado con el nivel 1
                        if (myAddOnTab.ListMarketDepthLevelBidPrice[i] >= slippage && enterLevel == false)
                        {
                            Qa += myAddOnTab.listMarketDepthLevelBidVolume[i];
                            //Print("Nivel: " + lv);
                            //Print("Qa: " + Qa);
                            //Print("PositionSize: " + positionSize);
                            //Print("PriceBidMarket: " + myAddOnTab.ListMarketDepthLevelBidPrice[i]);

                            if (Qa >= positionSize)
                            {
                                // Entra
                                ParametersList.Add("Order book level", lv.ToString());
                                ParametersList.Add("Bid", myAddOnTab.ListMarketDepthLevelBidPrice[i].ToString());
                                entershort = EnterShortLimit(barsInProgress, false, positionSize, myAddOnTab.ListMarketDepthLevelBidPrice[i], signalName);
                                enterLevel = true;
                                break;
                            }
                            else
                            {
                                // sigue al siguiente nivel
                            }
                        }
                        else if (enterLevel == false)
                        {
                            ParametersList.Add("Order book level", (lv-1).ToString());
                            ParametersList.Add("Bid", myAddOnTab.ListMarketDepthLevelBidPrice[i - 1].ToString());
                            entershort = EnterShortLimit(barsInProgress, false, (int)Qa, myAddOnTab.ListMarketDepthLevelBidPrice[i - 1], signalName);
                            enterLevel = true;
                            //lv = i + 1;
                            break;
                        }
                    }
                }

                if (!enterLevel && positionSize >= Qa) 
                {
                    ParametersList.Add("Order book level", lv.ToString());
                    ParametersList.Add("Bid", myAddOnTab.ListMarketDepthLevelBidPrice[9].ToString());
                    entershort = EnterShortLimit(barsInProgress, false, (int)Qa, myAddOnTab.ListMarketDepthLevelBidPrice[9], signalName);
                    enterLevel = true;
                }
                

            //Print("Fin Nivel: " + lv);
            }
            return entershort;

        }

        #endregion

        #region OnOrderUpdate
        protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string nativeError)
        {
            switch (orderSwitch)
            {
                case OrderSwitch.Future:
                    OnOrderUpdateFuture(order, limitPrice, stopPrice, quantity, filled, averageFillPrice, orderState, time, error, nativeError);
                    break;
                case OrderSwitch.Common:
                default:
                    OnOrderUpdateCommon(order, limitPrice, stopPrice, quantity, filled, averageFillPrice, orderState, time, error, nativeError);
                    break;
            }
            
        }

        private void OnOrderUpdateCommon(Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string nativeError)
        {
            if (order.Name == EnterLongName || order.Name == EnterShortName)
            {
                entryOrder = order;
                //timerOrder = order;

                if (order.OrderState == OrderState.Cancelled)
                {
                    if (order.Filled <= 0)
                    {
                        entryOrder = null;
                        order_pending = null;
                        //reorderObj = ReorderSwitch.Entry;
                    } 
                    //else
                    //{
                    //    OnExecutionUpdateCommon(lastestExecution, lastestExecution.ExecutionId, lastestExecution.Price, lastestExecution.Quantity, lastestExecution.MarketPosition, lastestExecution.OrderId, time);
                    //}
                }
                //else if (order.OrderState == OrderState.Filled)
                //{
                //    reorderObj = ReorderSwitch.None;
                //}

                if (order.OrderState == OrderState.CancelPending || order.OrderState == OrderState.CancelSubmitted)
                {
                    order_pending = null;
                }
                else
                {
                    if (State == State.Realtime)
                    {
                        order_pending = order;
                    }
                }
            }
            else
            {
                exitOrder = order;
                //timerOrder = order;

                if (order.OrderState == OrderState.Cancelled)
                {
                    if (order.Filled <= 0)
                    {
                        exitOrder = null;
                        order_pending = null;
                        //if (order.Name == ExitLongName || order.Name == ExitShortName)
                        //{
                        //    reorderObj = ReorderSwitch.Exit;
                        //}
                        //else if (order.Name == StoplossName)
                        //{
                        //    reorderObj = ReorderSwitch.StopLoss;
                        //}
                        //else if (order.Name == ProfitTargetName)
                        //{
                        //    reorderObj = ReorderSwitch.ProfitTarget;
                        //}
                    }
                    //else
                    //{
                    //    OnExecutionUpdateCommon(lastestExecution, lastestExecution.ExecutionId, lastestExecution.Price, lastestExecution.Quantity, lastestExecution.MarketPosition, lastestExecution.OrderId, time);
                    //}
                }

                if (order.OrderState == OrderState.CancelPending || order.OrderState == OrderState.CancelSubmitted)
                {
                    order_pending = null;
                }
                else
                {
                    if (State == State.Realtime && (order.Name == ExitLongName || order.Name == ExitShortName))
                    {
                        order_pending = order;
                    }
                }
            }
        }

        private void OnOrderUpdateFuture(Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string nativeError)
        {
            if (order.Name == EnterLongName || order.Name == EnterShortName || order.Name == EnterLongRolloverName || order.Name == EnterShortRolloverName)
            {
                entryOrder = order;
                //timerOrder = order;

                if (order.OrderState == OrderState.Cancelled)
                {
                    if (order.Filled <= 0)
                    {
                        entryOrder = null;
                        order_pending = null;
                        //reorderObj = ReorderSwitch.Entry;
                    }
                    else
                    {
                        OnExecutionUpdateFuture(lastestExecution, lastestExecution.ExecutionId, lastestExecution.Price, lastestExecution.Quantity, lastestExecution.MarketPosition, lastestExecution.OrderId, time);
                    }
                }
                
                if (order.OrderState == OrderState.CancelPending || order.OrderState == OrderState.CancelSubmitted)
                {
                    order_pending = null;
                }
                else
                {
                    if (State == State.Realtime)
                    {
                        order_pending = order;
                        //Print("OnOrderUpdateFuture OrderState != Cancelled ENTRY order_pending = " + order_pending);
                    }
                }
            }
            else
            {
                exitOrder = order;
                //timerOrder = order;

                if (order.OrderState == OrderState.Cancelled)
                {
                    if (order.Filled <= 0)
                    {
                        exitOrder = null;
                        order_pending = null;
                        //if (order.Name == ExitLongName || order.Name == ExitShortName || order.Name == ExitLongRolloverName || order.Name == ExitShortRolloverName)
                        //{
                        //    reorderObj = ReorderSwitch.Exit;
                        //}
                        //else if (order.Name == StoplossName)
                        //{
                        //    reorderObj = ReorderSwitch.StopLoss;
                        //}
                        //else if (order.Name == ProfitTargetName)
                        //{
                        //    reorderObj = ReorderSwitch.ProfitTarget;
                        //}
                    }
                    else
                    {
                        OnExecutionUpdateFuture(lastestExecution, lastestExecution.ExecutionId, lastestExecution.Price, lastestExecution.Quantity, lastestExecution.MarketPosition, lastestExecution.OrderId, time);
                    }
                }

                if (order.OrderState == OrderState.CancelPending || order.OrderState == OrderState.CancelSubmitted)
                {
                    order_pending = null;
                }
                else
                {
                    if (State == State.Realtime && (order.Name == ExitLongName || order.Name == ExitShortName || order.Name == ExitLongRolloverName || order.Name == ExitShortRolloverName))
                    {
                        order_pending = order;
                    }
                }
            }
        }
        #endregion

        #region OnExecutionUpdate
        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            lastestExecution = execution;
            switch (executionSwitch)
            {
                case ExecutionSwitch.Future:
                    OnExecutionUpdateFuture(execution, executionId, price, quantity, marketPosition, orderId, time);
                    break;
                case ExecutionSwitch.Common:
                default:
                    OnExecutionUpdateCommon(execution, executionId, price, quantity, marketPosition, orderId, time);
                    break;
            }

        }

        public bool SaveMongoOrders(OrderEntryF orderEnrtyF, string nameDatabase, string nameCollection)
        {
            var response = true;
            try
            {
                BsonClassMap.RegisterClassMap<OrderEntryF>();
            }
            catch (Exception)
            {
            }

            try
            {
                //var credential = new DatabaseJson().GetUriDb();
                MongoClient client_remote = new MongoClient(Database.GetUriDb());
                IMongoDatabase db_remote = client_remote.GetDatabase(nameDatabase);
                //var client = new MongoClient(MongoUrl.Create(credential));
                //IMongoDatabase db = client.GetDatabase(nameDatabase);
                var collection = db_remote.GetCollection<OrderEntryF>(nameCollection);

                collection.InsertOne(orderEnrtyF);
            }
            catch (Exception ex)
            {
                response = false;
            }
            return response;
        }

        public bool UpdateMongoOrders(OrderEntryF orderEnrtyF, string nameDatabase, string nameCollection)
        {
            var response = true;
            try
            {
                BsonClassMap.RegisterClassMap<OrderEntryF>();
            }
            catch (Exception)
            {
            }

            try
            {
                //var credential = new DatabaseJson().GetUriDb();
                MongoClient client_remote = new MongoClient(Database.GetUriDb());
                IMongoDatabase db_remote = client_remote.GetDatabase(nameDatabase);
                var collection = db_remote.GetCollection<OrderEntryF>(nameCollection);
                var builder = Builders<OrderEntryF>.Filter;
                //var sort = Sort

                var filter = builder.Eq(x => x.Name, orderEnrtyF.Name) & builder.Eq(x => x.InstrumentName, orderEnrtyF.InstrumentName) & builder.Eq(x => x.AccountName, orderEnrtyF.AccountName) & builder.Eq(x => x.IdStrategy, orderEnrtyF.IdStrategy) & !builder.Eq("status.1", "Completado");
                
                var result = collection.Find(filter).FirstOrDefault();

                if (result == null)
                {
                    //Crear nuevo registro parti filled
                    ///Print("Crea");
                    collection.InsertOne(orderEnrtyF);
                }
                else
                {
                    //Actualizar registro
                    //Print("Reemplaza");

                    orderEnrtyF.Id = result.Id;
                    collection.FindOneAndReplace(filter, orderEnrtyF);
                }
            }
            catch (Exception ex)
            {
                Print("Error: " + ex.Message);
                response = false;
            }
            return response;
        }

        public OrderEntryF CheckLiveOperation(OrderEntryF orderEnrtyF, string nameDatabase, string nameCollection) 
        {
            try
            {
                
                BsonClassMap.RegisterClassMap<OrderEntryF>();
            }
            catch (Exception)
            {
            }

            OrderEntryF result = new OrderEntryF();

            try
            {//TODO
                //var credential = new DatabaseJson().GetUriDb();
                MongoClient client_remote = new MongoClient(Database.GetUriDb());
                IMongoDatabase db_remote = client_remote.GetDatabase(nameDatabase);
                var collection = db_remote.GetCollection<OrderEntryF>(nameCollection);
                var builder = Builders<OrderEntryF>.Filter;

                //double pb = 0.33;

                var filter = builder.Eq(x => x.Name, orderEnrtyF.Name) & builder.Eq(x => x.InstrumentName, orderEnrtyF.InstrumentName) & builder.Eq(x => x.AccountName, orderEnrtyF.AccountName) & builder.Eq(x => x.IdStrategy, orderEnrtyF.IdStrategy) & !builder.Eq("status.1", "Completado");

                result = collection.Find(filter).FirstOrDefault();

            }
            catch (Exception ex)
            {
                result = null;
            }
            return result;
        }

        public bool UpdateExitOrders(OrderEntryF replacement, string nameDatabase, string nameCollection)
        {
            try
            {
                BsonClassMap.RegisterClassMap<OrderEntryF>();
            }
            catch (Exception)
            {
            }

            try
            {
                //Print("SALIDA ID: " + replacement.Id);
                //DatabaseJson database = new DatabaseJson();
                MongoClient client_remote = new MongoClient(Database.GetUriDb());
                IMongoDatabase db_remote = client_remote.GetDatabase(nameDatabase);
                var collection = db_remote.GetCollection<OrderEntryF>(nameCollection);
                var builder = Builders<OrderEntryF>.Filter.Eq(x => x.Id, replacement.Id);
                var result = collection.ReplaceOne(builder, replacement);

                //Print("Salida: " + result.IsAcknowledged);

                return result.IsAcknowledged;
            }
            catch (Exception ex)
            {
                Print("Salida Exception: " + ex.Message);
                return false;
            }
        }

        public bool ChangeOrdersMongo(OrderEntryF replacement, string nameDatabase, string nameCollection)
        {
            try
            {
                BsonClassMap.RegisterClassMap<OrderEntryF>();
            }
            catch (Exception)
            {
            }

            try
            {
                //DatabaseJson database = new DatabaseJson();
                MongoClient client_remote = new MongoClient(Database.GetUriDb());
                IMongoDatabase db_remote = client_remote.GetDatabase(nameDatabase);
                var collection = db_remote.GetCollection<OrderEntryF>(nameCollection);
                var builder = Builders<OrderEntryF>.Filter.Eq(x => x.Id, replacement.Id);
                
                //var credential = new DatabaseJson().GetUriDb();
                //var client = new MongoClient(MongoUrl.Create(credential));
                //IMongoDatabase db = client.GetDatabase(nameDatabase);
                //var filters = new List<FilterDefinition<OrderEntryF>>();
                //filters.Add(Builders<OrderEntryF>.Filter.Eq(x => x.Id, replacement.Id));
                //var combineFilters = Builders<OrderEntryF>.Filter.And(filters);
                var result = collection.ReplaceOne(builder, replacement);
                return result.IsAcknowledged;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private void OnExecutionUpdateCommon(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            try
            {
                if (entryOrder != null && entryOrder == execution.Order)
                {
                    Print("\nEntrada C - Strategy Name: " + StrategyVersionName + "\nTime: " + Time[0] + "\nOrderState: " + execution.Order.OrderState.ToString() + "\nFilled: " + execution.Order.Filled.ToString() + "\nPrice: " + execution.Order.AverageFillPrice + "\nAccount: " + Account.Name + "\nInstrument: " + InstrumentName);

                    ExecutionsLog log = new ExecutionsLog()
                    {
                        OperationType = "entry_" + Direction + "_" + execution.Order.Name + "_common",
                        StrategyName = StrategyVersionName,
                        StrategyId = this.StrategyId,
                        BarTime = Time[0],
                        ExecutionTime = time,
                        OrderState = execution.Order.OrderState.ToString(),
                        Filled = execution.Order.Filled,
                        CurrentPrice = price,
                        AveragePrice = execution.Order.AverageFillPrice,
                        Account = this.Account.Name,
                        Symbol = InstrumentName
                    };
                    SaveExecutionsLog(log);
                    if (execution.Order.OrderState == OrderState.Filled || execution.Order.OrderState == OrderState.PartFilled || (execution.Order.OrderState == OrderState.Cancelled && execution.Order.Filled > 0))
                    {
                        if (State == State.Realtime)
                        {
                            if (order_pending != null && execution.Order == order_pending)
                            {
                                order_pending = null;
                            }
                            //timerOrder = null;
                            //try
                            //{
                            //    timer.Stop();
                            //    reorderObj = ReorderSwitch.None;
                            //}
                            //catch (Exception e)
                            //{
                            //    Telegram.SendMessageInChannelEWMDevelopment(e.Message);
                            //}

                            orderEntryObj = new OrderEntryF()
                            {
                                Name = execution.Order.Name,
                                InstrumentName = InstrumentName,
                                Date = execution.Time,
                                Price = execution.Order.AverageFillPrice,
                                Status = new List<string> { "Pendiente" },
                                OrderType = execution.Order.IsLong ? "Long" : (execution.Order.IsShort ? "Short" : null),
                                Quantity = new List<int> { execution.Order.Filled },
                                EntryRollover = false,
                                AccountName = Account.Name,
                                IdStrategy = StrategyId,
                                ExtraParameters = new BsonDocument()
                            };

                            if (execution.Order.IsLong)
                                StopCompressed = (decimal)((StopLossPercent / 100) * execution.Order.AverageFillPrice) + (decimal)execution.Order.AverageFillPrice;
                            else if (execution.Order.IsShort)
                                StopCompressed = (decimal)execution.Order.AverageFillPrice - (decimal)((StopLossPercent / 100) * execution.Order.AverageFillPrice);

                            price_diff_compress = (decimal)((StopLossPercent / 100) * execution.Order.AverageFillPrice);

                            divCompressed = (decimal)0.5;

                            orderEntryObj.ExtraParameters.Add("price_compress", StopCompressed);

                            orderEntryObj.ExtraParameters.Add("div_compress", divCompressed);

                            orderEntryObj.ExtraParameters.Add("price_diff_compress", price_diff_compress);

                            orderEntryObj.ExtraParameters.Add("order_state", execution.Order.OrderState.ToString());

                            //var exists = orderEntryObj.ExtraParameters.Contains("order_state");

                            var liveOperationResult = CheckLiveOperation(orderEntryObj, trades_server, trades_collection);

                            if (liveOperationResult != null && execution.Order.OrderState.ToString() == "PartFilled") 
                            {
                                //Merge - PartFilled
                                orderEntryObj.Quantity.Add(orderEntryObj.Quantity.LastOrDefault() + liveOperationResult.Quantity.LastOrDefault());

                            }

                            //if (orderEntryObj.ExtraParameters.GetValue("order_state").AsString == "PartFilled") 
                            //{
                            //    var liveOperationResult = CheckLiveOperation(orderEntryObj, trades_server, trades_collection);
                            //}

                            if (StopLossSwitch)
                            {
                                StopLoss = new PriceOrderExit()
                                {
                                    Date = execution.Time,
                                    Name = StoplossName,
                                    SignalName = entryOrder.Name,
                                    Quantity = execution.Order.Filled,
                                    Price = execution.Order.IsLong ? execution.Order.AverageFillPrice * (1 - StopLossPercent / 100)
                                                                   : execution.Order.AverageFillPrice * (1 + StopLossPercent / 100)

                                };

                                if (liveOperationResult != null && execution.Order.OrderState.ToString() == "PartFilled")
                                {
                                    //Merge
                                    StopLoss.Quantity = orderEntryObj.Quantity.LastOrDefault();
                                }
                                
                                orderEntryObj.ExtraParameters.Add("current_stop_loss", StopLoss.Price);
                                orderEntryObj.StoplossList = new List<PriceOrderExit> { StopLoss };


                            }
                            else
                            {
                                StopLoss = null;
                            }

                            if (ProfitTargetSwitch)
                            {
                                ProfitTarget = new PriceOrderExit()
                                {
                                    Date = execution.Time,
                                    Name = ProfitTargetName,
                                    SignalName = entryOrder.Name,
                                    Quantity = execution.Order.Filled,
                                    Price = execution.Order.IsLong ? execution.Order.AverageFillPrice * (1 + ProfitTargetPercent / 100)
                                                                   : execution.Order.AverageFillPrice * (1 - ProfitTargetPercent / 100)
                                };

                                if (liveOperationResult != null && execution.Order.OrderState.ToString() == "PartFilled")
                                {
                                    //Merge
                                    ProfitTarget.Quantity = orderEntryObj.Quantity.LastOrDefault();
                                }

                                orderEntryObj.ExtraParameters.Add("current_profit_target", ProfitTarget.Price);
                                orderEntryObj.ProfitTargetList = new List<PriceOrderExit> { ProfitTarget };
                            }
                            else
                            {
                                ProfitTarget = null;
                            }

                            orderEntryObj.ExtraParameters.Add("entry_condition_parameters", string.Join(", ", ParametersList.Select(x => x.Key + ": " + x.Value)));
                            bool orderSave = false;
                            
                            //Only Real Time
                            if (SaveTrade)
                            {
                                orderSave = UpdateMongoOrders(orderEntryObj, trades_server, trades_collection);
                                Print("SaveMongoOrder Entrada C: " + orderSave);
                            }

                            //orderSave = Database.UpdatePriceOrdersExit(pathDirectory, this.Name, StopLoss, ProfitTarget);

                            ReplaceTemplate(execution);
                            if (ProfitTarget != null && temporalityProfitSwitch == TemporalityProfitSwitch.Main)
                            {
                                AlertMessageTelegram += "\n\nNota: Sale por PT en temporalidad primaria";
                            }
                            ListTelegram = "\n\nParametros\n\n" + string.Join("\n", ParametersList.Select(x => x.Key + ": " + x.Value));
                            //if (!File.Exists(pathDirectory + "\\" + this.Name + ".json"))
                            //{
                            //    existLog = "\n\nNo se guardo el log de esta operacion.";
                            //}
                            //Telegram.SendMessageInChannelEWMDevelopment(AlertMessageTelegram + ListTelegram + existLog);
                            //if (!TestTelegram && telegramNotification)
                            //{
                            //    Telegram.SendMessageInChannelEWMReport(AlertMessageTelegram + ListTelegram);
                            //}
                            //if (TelegramDev)
                            //{
                            //    Telegram.SendMessageInChannelEWMDevelopment(AlertMessageTelegram + ListTelegram + existLog);
                            //}
                            //if (TelegramLive)
                            //{
                            //    Telegram.SendMessageInChannelEWMReport(AlertMessageTelegram + ListTelegram + existLog);
                            //}
                            //if (TelegramInc)
                            //{
                            //    Telegram.SendMessageInChannelEWMIncubation(AlertMessageTelegram + ListTelegram + existLog);
                            //}
                        }
                        positionSize = execution.Order.Filled;

                        if (execution.Order.OrderState.ToString() != "PartFilled")
                            entryOrder = null;
                    }
                }

                if (exitOrder != null && exitOrder == execution.Order)
                {
                    Print("\n Salida C - Strategy Name: " + StrategyVersionName + "\nTime: " + Time[0] + "\nOrderState: " + execution.Order.OrderState.ToString() + "\nFilled: " + execution.Order.Filled.ToString() + "\nPrice: " + execution.Order.AverageFillPrice + "\nAccount: " + Account.Name + "\nInstrument: " + InstrumentName);

                    ExecutionsLog log = new ExecutionsLog() { 
                        OperationType = "exit_" + Direction + "_" + execution.Order.Name + "_common",
                        StrategyName = StrategyVersionName,
                        StrategyId = this.StrategyId,
                        BarTime = Time[0],
                        ExecutionTime = time,
                        OrderState = execution.Order.OrderState.ToString(),
                        Filled = execution.Order.Filled,
                        CurrentPrice = price,
                        AveragePrice = execution.Order.AverageFillPrice,
                        Account = this.Account.Name,
                        Symbol = InstrumentName
                    };
                    SaveExecutionsLog(log);

                    if (execution.Order.OrderState == OrderState.Filled || execution.Order.OrderState == OrderState.PartFilled || (execution.Order.OrderState == OrderState.Cancelled && execution.Order.Filled > 0))
                    {
                        if (State == State.Realtime)
                        {
                            if (order_pending != null && execution.Order == order_pending)
                            {
                                order_pending = null;
                            }
                            //timerOrder = null;

                            //try
                            //{
                            //    timer.Stop();
                            //    if (execution.Order.OrderState == OrderState.Cancelled && execution.Order.Filled > 0)
                            //    {
                            //        positionSize = execution.Order.Quantity - execution.Order.Filled;
                            //        if (execution.Order.Name == ExitLongName || execution.Order.Name == ExitShortName)
                            //        {
                            //            reorderObj = ReorderSwitch.Exit;
                            //        }
                            //        else if (execution.Order.Name == StoplossName)
                            //        {
                            //            reorderObj = ReorderSwitch.StopLoss;
                            //        }
                            //        else if (execution.Order.Name == ProfitTargetName)
                            //        {
                            //            reorderObj = ReorderSwitch.ProfitTarget;
                            //        }
                            //    }
                            //    else if (execution.Order.OrderState == OrderState.Filled)
                            //    {
                            //        reorderObj = ReorderSwitch.None;
                            //    }
                            //}
                            //catch (Exception e)
                            //{
                            //    Telegram.SendMessageInChannelEWMDevelopment(e.Message);
                            //}



                            double Commission = 0;
                            double MaePercent = 0;
                            double ProfitCurrency = 0;

                            if (SystemPerformance.RealTimeTrades.Any(x => x.Exit.Order.OrderId == orderId))
                            {
                                //Trade lastTrade = SystemPerformance.RealTimeTrades[SystemPerformance.RealTimeTrades.Count - 1];
                                Trade lastTrade = SystemPerformance.RealTimeTrades.Where(x => x.Exit.Order.OrderId == orderId).Last();
                                IEnumerable<Trade> trades = SystemPerformance.RealTimeTrades.Where(k => k.Exit.Order.OrderId == lastTrade.Exit.Order.OrderId);

                                if (trades != null)
                                {
                                    foreach (var trade in trades)
                                    {
                                        Commission += trade.Commission;
                                        //ProfitCurrency += trade.ProfitCurrency;
                                    }
                                    MaePercent += Math.Round(lastTrade.MaePercent, 4);
                                }
                            }

                            var LastOrderPendingTemp = Database.GetLastOrderMongo(StrategyId, Account.Name, trades_server, trades_collection);
                            if (LastOrderPendingTemp != null)
                                LastOrderPendingTemp = LastOrderPendingTemp.Status.Last() != "Completado" && LastOrderPendingTemp.Quantity.Last() > 0 ? LastOrderPendingTemp : null;

                            if (LastOrderPendingTemp != null)
                            {
                                ProfitCurrency = (execution.Order.AverageFillPrice * execution.Order.Filled) - (LastOrderPendingTemp.Price * execution.Order.Filled);
                                if (Direction == "Short")
                                {
                                    ProfitCurrency *= -1;
                                }
                            }

                            orderExitObj = new OrderExitFile()
                            {
                                Name = execution.Order.Name,
                                SignalName = execution.Order.FromEntrySignal,
                                Date = execution.Time,
                                Price = execution.Order.AverageFillPrice,
                                OrderType = null,
                                Quantity = execution.Order.Filled,
                                ExitRollover = false,
                                Commission = Commission,
                                MaePercent = MaePercent,
                                ProfitCurrency = ProfitCurrency,
                                //                            ExtraParameters = new BsonDocument(),
                            };

                            //Print("Protit Currency: " + ProfitCurrency);
                            //var orderSave = Database.AddNewOrderExitObject(pathDirectory, this.Name, orderExitObj);
                            bool orderSave = false;
                            //Only Real Time
                            if (SaveTrade)
                            {
                                orderEntryObj = Database.GetLastOrderMongo(StrategyId, Account.Name, trades_server, trades_collection);

                                double slippage = 0;

                                if (Direction == "Short")
                                {
                                    if (execution.Order.Name == StoplossName)
                                    {
                                        slippage = ((execution.Order.AverageFillPrice / StopLoss.Price) - 1) * -1;
                                    }
                                    else if (execution.Order.Name == ProfitTargetName)
                                    {
                                        slippage = ((execution.Order.AverageFillPrice / ProfitTarget.Price) - 1) * -1;

                                    }
                                    else if (execution.Order.Name == ExitShortName)
                                    {
                                        slippage = ((execution.Order.AverageFillPrice / Close[CurrentSecondarySeries]) - 1) * -1;

                                    }
                                }
                                else if (Direction == "Long")
                                {
                                    if (execution.Order.Name == StoplossName)
                                    {
                                        slippage = (execution.Order.AverageFillPrice / StopLoss.Price) - 1;
                                    }
                                    else if (execution.Order.Name == ProfitTargetName)
                                    {
                                        slippage = (execution.Order.AverageFillPrice / ProfitTarget.Price) - 1;
                                    }
                                    else if (execution.Order.Name == ExitLongName)
                                    {
                                        slippage = (execution.Order.AverageFillPrice / Close[CurrentSecondarySeries]) - 1;
                                    }
                                }

                                Print("order entry = ");
                                Print(orderEntryObj.OrdersExit);

                                if (orderEntryObj.OrdersExit == null) {
                                    Print("instanciando");
                                    orderEntryObj.OrdersExit = new List<OrderExitFile>();
                                }//Merge
                                else 
                                {
                                    Print("profit = " + orderEntryObj.OrdersExit.FirstOrDefault().ProfitCurrency);
                                    var proft = orderEntryObj.OrdersExit.FirstOrDefault().ProfitCurrency;
                                    orderExitObj.ProfitCurrency = orderExitObj.ProfitCurrency + proft;
                                    orderEntryObj.OrdersExit.Clear();

                                    //var com = orderEntryObj.OrdersExit.FirstOrDefault().Commission;
                                    //orderExitObj.ProfitCurrency = orderExitObj.ProfitCurrency + proft;
                                }

                                orderEntryObj.OrdersExit.Add(orderExitObj);

                                if (execution.Order.OrderState.ToString() == "PartFilled")
                                    orderEntryObj.Quantity.Add(orderEntryObj.Quantity.Last() - orderExitObj.Quantity);
                                else if (execution.Order.OrderState.ToString() == "Filled")
                                    orderEntryObj.Quantity.Add(orderEntryObj.Quantity.First() - orderExitObj.Quantity);

                                //orderEntryObj.Quantity.Add(orderEntryObj.Quantity.Last() - orderExitObj.Quantity);

                                if (orderEntryObj.Quantity.Last() == 0)
                                    orderEntryObj.Status.Add("Completado");

                                orderSave = UpdateExitOrders(orderEntryObj, trades_server, trades_collection);
                                Print("Salida C UpdateExitOrders: " + orderSave);
                                var slip_result = Database.AddSlippage(slippage, StrategyId, MyAccount.Name, trades_server, trades_collection);

                            }

                            ReplaceTemplate(execution);

                            if (ParametersList.ContainsKey("MAE"))
                                ParametersList["MAE"] = MaePercent.ToString();
                            else
                                ParametersList.Add("MAE", MaePercent.ToString());

                            if (ParametersList.ContainsKey("Ganancia/P�rdida"))
                                ParametersList["Ganancia/P�rdida"] = ProfitCurrency.ToString();
                            else
                                ParametersList.Add("Ganancia/P�rdida", ProfitCurrency.ToString());

                            ListTelegram = "\n\nParametros\n\n" + string.Join("\n", ParametersList.Select(x => x.Key + ": " + x.Value));
                            //Telegram.SendMessageInChannelEWMDevelopment(AlertMessageTelegram + ListTelegram);
                            //if (!TestTelegram && telegramNotification)
                            //{
                            //    Telegram.SendMessageInChannelEWMReport(AlertMessageTelegram + ListTelegram);
                            //}
                            //if (TelegramDev)
                            //{
                            //    Telegram.SendMessageInChannelEWMDevelopment(AlertMessageTelegram + ListTelegram);
                            //}
                            //if (TelegramLive)
                            //{
                            //    Telegram.SendMessageInChannelEWMReport(AlertMessageTelegram + ListTelegram);
                            //}
                            //if (TelegramInc)
                            //{
                            //    Telegram.SendMessageInChannelEWMIncubation(AlertMessageTelegram + ListTelegram);
                            //}

                            if (execution.Order.OrderState == OrderState.Filled)
                            {
                                orderEntryObj = null;
                                orderExitObj = null;
                                StopLoss = null;
                                ProfitTarget = null;
                            }
                            else if (execution.Order.OrderState == OrderState.Cancelled && execution.Order.Filled > 0)
                            {
                                if (StopLossSwitch)
                                {
                                    StopLoss.Date = time;
                                    StopLoss.Quantity = execution.Order.Quantity - execution.Order.Filled;
                                    orderEntryObj.StoplossList.Add(StopLoss);
                                }
                                else
                                    StopLoss = null;

                                if (ProfitTargetSwitch)
                                {
                                    ProfitTarget.Date = time;
                                    ProfitTarget.Quantity = execution.Order.Quantity - execution.Order.Filled;
                                    orderEntryObj.ProfitTargetList.Add(ProfitTarget);

                                }
                                else
                                    ProfitTarget = null;

                                orderSave = UpdateExitOrders(orderEntryObj, trades_server, trades_collection);
                                //orderSave = Database.UpdatePriceOrdersExit(pathDirectory, this.Name, StopLoss, ProfitTarget);
                            }
                        }

                        if (execution.Order.OrderState.ToString() != "PartFilled")
                            exitOrder = null;
                        //if (execution.Order.OrderState == OrderState.Filled)
                        //{
                        //    var deleteO = Database.DeleteFile(pathDirectory, this.Name);
                        //}
                    }
                    else
                    {
                        Print("*** Se cumplio la orden de salida C pero no los status ***");
                        Print("*************************");
                    }
                }
            }
            catch (Exception ex) 
            {
                Print("Error - OnExecutionUpdateCommon");
                Print("Message: " + ex.Message);
                Print("Source: " + ex.Source);
                Print("Data: " + ex.Data);
                Print("StackTrace: " + ex.StackTrace);
                Print("InnerException: " + ex.InnerException);
                var message = "OnExecutionUpdateCommon Error" + "\nMessage : " + ex.Message + "\nSource: " + ex.Source + "\nData: " + ex.Data + "\nStackTrace: " + ex.StackTrace + "\nInnerException: " + ex.InnerException;

                Telegram.SendMessageInChannelEWMDevelopment(message);
            }
        }

        private void OnExecutionUpdateFuture(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            try
            {
                if (entryOrder != null && entryOrder == execution.Order)
                {
                    Print("\nEntrada F - Strategy Name: " + StrategyVersionName + "\nTime: " + Time[0] + "\nOrderState: " + execution.Order.OrderState.ToString() +"\nFilled: " + execution.Order.Filled.ToString() + "\nPrice: " + execution.Order.AverageFillPrice + "\nAccount: " + Account.Name + "\nInstrument: " + InstrumentName);

                    ExecutionsLog log = new ExecutionsLog()
                    {
                        OperationType = "entry_" + Direction + "_" + execution.Order.Name + "_future",
                        StrategyName = StrategyVersionName,
                        StrategyId = this.StrategyId,
                        BarTime = Time[0],
                        ExecutionTime = time,
                        OrderState = execution.Order.OrderState.ToString(),
                        Filled = execution.Order.Filled,
                        CurrentPrice = price,
                        AveragePrice = execution.Order.AverageFillPrice,
                        Account = this.Account.Name,
                        Symbol = InstrumentName
                    };

                    SaveExecutionsLog(log);

                    if (execution.Order.OrderState == OrderState.Filled || execution.Order.OrderState == OrderState.PartFilled || (execution.Order.OrderState == OrderState.Cancelled && execution.Order.Filled > 0))
                    {
                        if (State == State.Realtime)
                        {
                            if (order_pending != null && execution.Order == order_pending)
                            {
                                order_pending = null;
                            }
                            //try
                            //{
                            //    timer.Stop();
                            //    reorderObj = ReorderSwitch.None;
                            //}
                            //catch (Exception e)
                            //{
                            //    Telegram.SendMessageInChannelEWMDevelopment(e.Message);
                            //}

                            orderEntryObj = new OrderEntryF()
                            {
                                Name = execution.Order.Name,
                                InstrumentName = InstrumentName,
                                Date = execution.Time,
                                Price = execution.Order.AverageFillPrice,
                                Status = new List<string> { "Pendiente" },
                                OrderType = execution.Order.IsLong ? "Long" : (execution.Order.IsShort ? "Short" : null),
                                Quantity = new List<int> { execution.Order.Filled },
                                EntryRollover = entryOnRollover,
                                AccountName = Account.Name,
                                IdStrategy = StrategyId,
                                ExtraParameters = new BsonDocument()
                            };

                            if(execution.Order.IsLong)
                                StopCompressed = (decimal)((StopLossPercent / 100) * execution.Order.AverageFillPrice) + (decimal)execution.Order.AverageFillPrice;
                            else if(execution.Order.IsShort)
                                StopCompressed = (decimal)execution.Order.AverageFillPrice - (decimal)((StopLossPercent / 100) * execution.Order.AverageFillPrice);

                            price_diff_compress = (decimal)((StopLossPercent / 100) * execution.Order.AverageFillPrice);

                            divCompressed = (decimal)0.5;

                            orderEntryObj.ExtraParameters.Add("price_compress", StopCompressed);

                            orderEntryObj.ExtraParameters.Add("div_compress", divCompressed);

                            orderEntryObj.ExtraParameters.Add("price_diff_compress", price_diff_compress);

                            orderEntryObj.ExtraParameters.Add("order_state", execution.Order.OrderState.ToString());

                            var liveOperationResult = CheckLiveOperation(orderEntryObj, trades_server, trades_collection);

                            if (liveOperationResult != null && execution.Order.OrderState.ToString() == "PartFilled")
                            {
                                //Merge - PartFilled
                                orderEntryObj.Quantity.Add(orderEntryObj.Quantity.LastOrDefault() + liveOperationResult.Quantity.LastOrDefault());

                            }

                            if (StopLossSwitch)
                            {
                                double stoploss_percent = StopLossPercent / 100;
                                if (entryOnRollover)
                                {
                                    //double? stoploss_perc_remain = GetStoplossPercentageRemaining(execution.Order.IsLong);
                                    double? stoploss_perc_remain = GetNewStoplossPercentage(execution.Order.IsLong, execution.Order.AverageFillPrice);
                                    if (stoploss_perc_remain != null)
                                    {
                                        stoploss_percent = (double)stoploss_perc_remain;
                                    }
                                }

                                StopLoss = new PriceOrderExit()
                                {
                                    Date = execution.Time,
                                    Name = StoplossName,
                                    SignalName = entryOrder.Name,
                                    Quantity = execution.Order.Filled,
                                    Price = execution.Order.IsLong ? execution.Order.AverageFillPrice * (1 - stoploss_percent)
                                                                   : execution.Order.AverageFillPrice * (1 + stoploss_percent)

                                };

                                if (liveOperationResult != null && execution.Order.OrderState.ToString() == "PartFilled")
                                {
                                    //Merge
                                    StopLoss.Quantity = orderEntryObj.Quantity.LastOrDefault();
                                }

                                orderEntryObj.ExtraParameters.Add("current_stop_loss", StopLoss.Price);
                                orderEntryObj.StoplossList = new List<PriceOrderExit> { StopLoss };

                            }
                            else
                            {
                                StopLoss = null;
                            }

                            if (ProfitTargetSwitch)
                            {
                                double profit_percent = ProfitTargetPercent / 100;

                                if (entryOnRollover)
                                {
                                    //double? profit_perc_remain = GetProfitTargetPercentageRemaining(execution.Order.IsLong);
                                    double? profit_perc_remain = GetNewProfitTargetPercentage(execution.Order.IsLong, execution.Order.AverageFillPrice);
                                    if (profit_perc_remain != null)
                                    {
                                        profit_percent = (double)profit_perc_remain;
                                    }
                                }

                                ProfitTarget = new PriceOrderExit()
                                {
                                    Date = execution.Time,
                                    Name = ProfitTargetName,
                                    SignalName = entryOrder.Name,
                                    Quantity = execution.Order.Filled,
                                    Price = execution.Order.IsLong ? execution.Order.AverageFillPrice * (1 + profit_percent)
                                                                   : execution.Order.AverageFillPrice * (1 - profit_percent)
                                };

                                if (liveOperationResult != null && execution.Order.OrderState.ToString() == "PartFilled")
                                {
                                    //Merge
                                    ProfitTarget.Quantity = orderEntryObj.Quantity.LastOrDefault();
                                }

                                orderEntryObj.ExtraParameters.Add("current_profit_target", ProfitTarget.Price);
                                orderEntryObj.ProfitTargetList = new List<PriceOrderExit> { ProfitTarget };
                            }
                            else
                            {
                                ProfitTarget = null;
                            }

                            orderEntryObj.ExtraParameters.Add("entry_condition_parameters", string.Join(", ", ParametersList.Select(x => x.Key + ": " + x.Value)));
                            bool orderSave = false;

                            //var orderSave = Database.AddNewOrderEntryObject(pathDirectory, this.Name, orderEntryObj);
                            //var existLog = "";
                            //if (orderSave == false)
                            //{
                            //    existLog = MessageLogError(execution.Time);
                            //   // Telegram.SendMessageInChannelEWMDevelopment(existLog);
                            //    if (TelegramDev)
                            //    {
                            //        Telegram.SendMessageInChannelEWMDevelopment(existLog);
                            //    }
                            //    if (TelegramLive)
                            //    {
                            //        Telegram.SendMessageInChannelEWMReport(existLog);
                            //    }
                            //    if (TelegramInc)
                            //    {
                            //        Telegram.SendMessageInChannelEWMIncubation(existLog);
                            //    }
                            //}

                            //Only Real Time
                            if (SaveTrade)
                            {
                                orderSave = UpdateMongoOrders(orderEntryObj, trades_server, trades_collection);
                                Print("SaveMongoOrder Entrada F: " + orderSave);
                            }

                            // orderSave = Database.UpdatePriceOrdersExit(pathDirectory, this.Name, StopLoss, ProfitTarget);

                            ReplaceTemplate(execution);
                            if (ProfitTarget != null && temporalityProfitSwitch == TemporalityProfitSwitch.Main)
                            {
                                AlertMessageTelegram += "\n\nNota: Sale por PT en temporalidad primaria";
                            }
                            ListTelegram = "\n\nParametros\n\n" + string.Join("\n", ParametersList.Select(x => x.Key + ": " + x.Value));
                            //var existLog = "";
                            //if (!File.Exists(pathDirectory + "\\" + this.Name + ".json"))
                            //{
                            //    existLog = "\n\nNo se guardo el log de esta operacion.";
                            //}
                            //Telegram.SendMessageInChannelEWMDevelopment(AlertMessageTelegram + ListTelegram + existLog);
                            //if (!TestTelegram && telegramNotification)
                            //{
                            //    Telegram.SendMessageInChannelEWMReport(AlertMessageTelegram + ListTelegram);
                            //}

                            //if (TelegramDev)
                            //{
                            //    Telegram.SendMessageInChannelEWMDevelopment(AlertMessageTelegram + ListTelegram + existLog);
                            //}
                            //if (TelegramLive)
                            //{
                            //    Telegram.SendMessageInChannelEWMReport(AlertMessageTelegram + ListTelegram + existLog);
                            //}
                            //if (TelegramInc)
                            //{
                            //    Telegram.SendMessageInChannelEWMIncubation(AlertMessageTelegram + ListTelegram + existLog);
                            //}
                        }
                        positionSize = execution.Order.Filled;

                        if (execution.Order.OrderState.ToString() != "PartFilled")
                        {
                            entryOrder = null;
                            entryOnRollover = false;
                            exitOnRollover = false;
                        }
                    }
                    else 
                    {
                        Print("*** Se cumplio la orden de entrada F pero no los status ***");
                        Print("*************************");
                    }
                }

                if (exitOrder != null && exitOrder == execution.Order)
                {
                    Print("\nSalida F - Strategy Name: " + StrategyVersionName + "\nTime: " + Time[0] + "\nOrderState: " + execution.Order.OrderState.ToString() + "\nFilled: " + execution.Order.Filled.ToString() + "\nPrice: " + execution.Order.AverageFillPrice + "\nAccount: " + Account.Name + "\nInstrument: " + InstrumentName);

                    ExecutionsLog log = new ExecutionsLog()
                    {
                        OperationType = "exit_" + Direction + "_" + execution.Order.Name + "_future",
                        StrategyName = StrategyVersionName,
                        StrategyId = this.StrategyId,
                        BarTime = Time[0],
                        ExecutionTime = time,
                        OrderState = execution.Order.OrderState.ToString(),
                        Filled = execution.Order.Filled,
                        CurrentPrice = price,
                        AveragePrice = execution.Order.AverageFillPrice,
                        Account = this.Account.Name,
                        Symbol = InstrumentName
                    };
                    SaveExecutionsLog(log);

                    if (execution.Order.OrderState == OrderState.Filled || execution.Order.OrderState == OrderState.PartFilled || (execution.Order.OrderState == OrderState.Cancelled && execution.Order.Filled > 0))
                    {
                        if (State == State.Realtime)
                        {
                            if (order_pending != null && execution.Order == order_pending)
                            {
                                order_pending = null;
                            }

                            //try
                            //{
                            //    timer.Stop();
                            //    if (execution.Order.OrderState == OrderState.Cancelled && execution.Order.Filled > 0)
                            //    {
                            //        positionSize = execution.Order.Quantity - execution.Order.Filled;
                            //        if (execution.Order.Name == ExitLongName || execution.Order.Name == ExitShortName)
                            //        {
                            //            reorderObj = ReorderSwitch.Exit;
                            //        }
                            //        else if (execution.Order.Name == StoplossName)
                            //        {
                            //            reorderObj = ReorderSwitch.StopLoss;
                            //        }
                            //        else if (execution.Order.Name == ProfitTargetName)
                            //        {
                            //            reorderObj = ReorderSwitch.ProfitTarget;
                            //        }
                            //    }
                            //    else if (execution.Order.OrderState == OrderState.Filled)
                            //    {
                            //        reorderObj = ReorderSwitch.None;
                            //    }
                            //}
                            //catch (Exception e)
                            //{
                            //    //Print("e: " + e.Message);
                            //    Telegram.SendMessageInChannelEWMDevelopment(e.Message);
                            //}

                            double Commission = 0;
                            double MaePercent = 0;
                            double ProfitCurrency = 0;

                            System.Threading.Thread.Sleep(5000);

                            if (SystemPerformance.RealTimeTrades.Any(x => x.Exit.Order.OrderId == orderId))
                            {
                                //Trade lastTrade = SystemPerformance.RealTimeTrades[SystemPerformance.RealTimeTrades.Count - 1];
                                Trade lastTrade = SystemPerformance.RealTimeTrades.Where(x => x.Exit.Order.OrderId == orderId).Last();
                                IEnumerable<Trade> trades = SystemPerformance.RealTimeTrades.Where(k => k.Exit.Order.OrderId == lastTrade.Exit.Order.OrderId);

                                if (trades != null)
                                {
                                    foreach (var trade in trades)
                                    {
                                        Commission += trade.Commission;
                                        //ProfitCurrency += trade.ProfitCurrency;
                                    }
                                    MaePercent += Math.Round(lastTrade.MaePercent, 4);
                                }
                            }

                            var LastOrderPendingTemp = Database.GetLastOrderMongo(StrategyId, Account.Name, trades_server, trades_collection);
                            if (LastOrderPendingTemp != null)
                                LastOrderPendingTemp = LastOrderPendingTemp.Status.Last() != "Completado" && LastOrderPendingTemp.Quantity.Last() > 0 ? LastOrderPendingTemp : null;

                            if (LastOrderPendingTemp != null)
                            {
                                ProfitCurrency = (execution.Order.AverageFillPrice * execution.Instrument.MasterInstrument.PointValue * execution.Order.Filled) - (LastOrderPendingTemp.Price * execution.Instrument.MasterInstrument.PointValue * execution.Order.Filled);
                                if (Direction == "Short")
                                {
                                    ProfitCurrency *= -1;
                                }
                            }

                            orderExitObj = new OrderExitFile()
                            {
                                Name = execution.Order.Name,
                                SignalName = execution.Order.FromEntrySignal,
                                Date = execution.Time,
                                Price = execution.Order.AverageFillPrice,
                                OrderType = null,
                                Quantity = execution.Order.Filled,
                                ExitRollover = exitOnRollover,
                                Commission = Commission,
                                MaePercent = MaePercent,
                                ProfitCurrency = ProfitCurrency,
                                //                            ExtraParameters = new BsonDocument(),
                            };

                            //var orderSave = Database.AddNewOrderExitObject(pathDirectory, this.Name, orderExitObj);
                            bool orderSave = false;

                            //Only Real Time
                            if (SaveTrade)
                            {
                                orderEntryObj = Database.GetLastOrderMongo(StrategyId, Account.Name, trades_server, trades_collection);

                                double slippage = 0;

                                if (Direction == "Short")
                                {
                                    if (execution.Order.Name == StoplossName)
                                    {
                                        slippage = ((execution.Order.AverageFillPrice / StopLoss.Price) - 1) * -1;
                                    }
                                    else if (execution.Order.Name == ProfitTargetName)
                                    {
                                        slippage = ((execution.Order.AverageFillPrice / ProfitTarget.Price) - 1) * -1;
                                    }
                                    else if (execution.Order.Name == ExitShortName)
                                    {
                                        slippage = ((execution.Order.AverageFillPrice / Close[CurrentSecondarySeries]) - 1) * -1;

                                    }
                                }
                                else if (Direction == "Long")
                                {
                                    if (execution.Order.Name == StoplossName)
                                    {
                                        slippage = (execution.Order.AverageFillPrice / StopLoss.Price) - 1;
                                    }
                                    else if (execution.Order.Name == ProfitTargetName)
                                    {
                                        slippage = (execution.Order.AverageFillPrice / ProfitTarget.Price) - 1;
                                    }
                                    else if (execution.Order.Name == ExitLongName)
                                    {
                                        slippage = (execution.Order.AverageFillPrice / Close[CurrentSecondarySeries]) - 1;
                                    }
                                }

                                if (orderEntryObj.OrdersExit == null)
                                {
                                    orderEntryObj.OrdersExit = new List<OrderExitFile>();
                                }//Merge
                                else
                                {
                                    var proft = orderEntryObj.OrdersExit.FirstOrDefault().ProfitCurrency;
                                    orderExitObj.ProfitCurrency = orderExitObj.ProfitCurrency + proft;
                                    orderEntryObj.OrdersExit.Clear();

                                    //var com = orderEntryObj.OrdersExit.FirstOrDefault().Commission;
                                    //orderExitObj.ProfitCurrency = orderExitObj.ProfitCurrency + proft;
                                }

                                orderEntryObj.OrdersExit.Add(orderExitObj);

                                if (execution.Order.OrderState.ToString() == "PartFilled")
                                    orderEntryObj.Quantity.Add(orderEntryObj.Quantity.Last() - orderExitObj.Quantity);
                                else if (execution.Order.OrderState.ToString() == "Filled")
                                    orderEntryObj.Quantity.Add(orderEntryObj.Quantity.First() - orderExitObj.Quantity);

                                if (orderEntryObj.Quantity.Last() == 0)
                                    orderEntryObj.Status.Add("Completado");


                                orderSave = UpdateExitOrders(orderEntryObj, trades_server, trades_collection);
                                //orderSave = ChangeOrdersMongo(orderEntryObj, trades_server, trades_collection);
                                var slip_result = Database.AddSlippage(slippage, StrategyId, MyAccount.Name, trades_server, trades_collection);

                                Print("Salida F UpdateExitOrders: " + orderSave);

                            }

                            ReplaceTemplate(execution);

                            if (ParametersList.ContainsKey("MAE"))
                                ParametersList["MAE"] = MaePercent.ToString();
                            else
                                ParametersList.Add("MAE", MaePercent.ToString());

                            if (ParametersList.ContainsKey("Ganancia/P�rdida"))
                                ParametersList["Ganancia/P�rdida"] = ProfitCurrency.ToString();
                            else
                                ParametersList.Add("Ganancia/P�rdida", ProfitCurrency.ToString());

                            ListTelegram = "\n\nParametros\n\n" + string.Join("\n", ParametersList.Select(x => x.Key + ": " + x.Value));
                            //Telegram.SendMessageInChannelEWMDevelopment(AlertMessageTelegram + ListTelegram);

                            //if (TelegramDev)
                            //{
                            //    Telegram.SendMessageInChannelEWMDevelopment(AlertMessageTelegram + ListTelegram);
                            //}
                            //if (TelegramLive)
                            //{
                            //    Telegram.SendMessageInChannelEWMReport(AlertMessageTelegram + ListTelegram);
                            //}
                            //if (TelegramInc)
                            //{
                            //    Telegram.SendMessageInChannelEWMIncubation(AlertMessageTelegram + ListTelegram);
                            //}

                            //if (!TestTelegram && telegramNotification)
                            //{
                            //    Telegram.SendMessageInChannelEWMReport(AlertMessageTelegram + ListTelegram);
                            //}

                            if (execution.Order.OrderState == OrderState.Filled)
                            {
                                orderEntryObj = null;
                                orderExitObj = null;
                                StopLoss = null;
                                ProfitTarget = null;
                            }
                            else if (execution.Order.OrderState == OrderState.Cancelled && execution.Order.Filled > 0)
                            {
                                if (StopLossSwitch)
                                {
                                    StopLoss.Date = time;
                                    StopLoss.Quantity = execution.Order.Quantity - execution.Order.Filled;
                                    orderEntryObj.StoplossList.Add(StopLoss);
                                }
                                else
                                    StopLoss = null;

                                if (ProfitTargetSwitch)
                                {
                                    ProfitTarget.Date = time;
                                    ProfitTarget.Quantity = execution.Order.Quantity - execution.Order.Filled;
                                    orderEntryObj.ProfitTargetList.Add(ProfitTarget);

                                }
                                else
                                    ProfitTarget = null;

                                orderSave = ChangeOrdersMongo(orderEntryObj, trades_server, trades_collection);
                                //orderSave = Database.UpdatePriceOrdersExit(pathDirectory, this.Name, StopLoss, ProfitTarget);
                            }
                        }
                        if (execution.Order.OrderState.ToString() != "PartFilled")
                            exitOrder = null;
                        //if (execution.Order.OrderState == OrderState.Filled)
                        //{
                        //    var deleteO = Database.DeleteFile(pathDirectory, this.Name);
                        //}
                    }
                    else
                    {
                        Print("*** Se cumplio la orden de salida F pero no los estatus ***");
                        Print("*************************");
                    }
                }
            }
            catch (Exception ex) 
            {
                Print("Error - OnExecutionUpdateFuture");
                Print("Message: " + ex.Message);
                Print("Source: " + ex.Source);
                Print("Data: " + ex.Data);
                Print("StackTrace: " + ex.StackTrace);
                Print("InnerException: " + ex.InnerException);
                var message = "OnExecutionUpdateCommonFuture Error" + "\nMessage : " + ex.Message + "\nSource: " + ex.Source + "\nData: " + ex.Data + "\nStackTrace: " + ex.StackTrace + "\nInnerException: " + ex.InnerException;

                Telegram.SendMessageInChannelEWMDevelopment(message);
            }
        }
        private string MessageLogError(DateTime time)
        {

            var resp = "[Realtime " + Account.Name + "]\nEstrategia:  " + this.Name + "\nInstrumento:  " + InstrumentName + "\nFecha:  " + time.ToLongDateString() + " a las " + time.ToShortTimeString() + "\nNota: No se guardo el log de esta operacion.";
            return resp;
        }

        private void ReplaceTemplate(Cbi.Execution execution)
        {
            AlertMessageTelegram = AlertMessageTelegram.Replace("[StrategyName]", this.Name);
            AlertMessageTelegram = AlertMessageTelegram.Replace("[SignalName]", execution.Order.Name);
            AlertMessageTelegram = AlertMessageTelegram.Replace("[Instrument]", InstrumentName);
            AlertMessageTelegram = AlertMessageTelegram.Replace("[Time]", execution.Time.ToLongDateString() + " a las " + execution.Time.ToShortTimeString());
            AlertMessageTelegram = AlertMessageTelegram.Replace("[Price]", execution.Order.AverageFillPrice.ToString("0.#####"));
            AlertMessageTelegram = AlertMessageTelegram.Replace("[Quantity]", execution.Order.Filled.ToString());
            AlertMessageTelegram = AlertMessageTelegram.Replace("[SLPrice]", StopLoss == null ? "N/A" : StopLoss.Price.ToString("0.#####"));
            AlertMessageTelegram = AlertMessageTelegram.Replace("[TPrice]", ProfitTarget == null ? "N/A" : ProfitTarget.Price.ToString("0.#####"));
        }

        #endregion

        #region Positions Size
        public int GetPositionSize(MasterInstrument mInstrument, double buyPrice)
        {
            double Capital = MyAccount.Get(AccountItem.NetLiquidation, MyAccount.Denomination);
            double positionSize;
            switch (mInstrument.InstrumentType)
            {
                case InstrumentType.Future:
                    positionSize = (Capital * (riskLevel / 100)) / (mInstrument.PointValue * buyPrice * (StopLossPercent / 100));
                    positionSize = Math.Round(positionSize);
                    break;

                default:
                    //Acciones y ETF
                    positionSize = (Capital * (riskLevel / 100)) / (buyPrice * (StopLossPercent / 100));
                    if ((positionSize * buyPrice) >= Capital)
                    {
                        positionSize = Math.Floor(Capital / (positionSize * buyPrice));
                    }

                    positionSize = positionSize < 1 ? 1 : positionSize;
                    break;
            }

            return (int)positionSize;
        }
        #endregion

        #region Rollover
        public bool IsExitOnRollover(string exitRolloverName)
        {

            //var lastTradeList = getLastTrade
            //    Database.GetLastOrdersEntry(pathDirectory, this.Name, 1);
            //if (lastTradeList == null || lastTradeList.Count == 0)
            //    return false;

            OrderEntryF lastTrade = Database.GetLastOrderMongo(StrategyId, Account.Name, trades_server, trades_collection);

            if (lastTrade == null)
                return false;

            var exitOrders = lastTrade.OrdersExit;
            if (exitOrders == null || exitOrders.Count == 0)
                return false;

            OrderExitFile lastExitOrder = exitOrders.Last();
            return lastExitOrder.ExitRollover && exitRolloverName.Equals(lastExitOrder.Name);
        }

        private string GetNextContract(Instrument instrument)
        {
            int i;
            for (i = 0; i < instrument.MasterInstrument.RolloverCollection.Count; i++)
            {
                if (instrument.Expiry == instrument.MasterInstrument.RolloverCollection[i].ContractMonth)
                    break;
            }

            if (i >= instrument.MasterInstrument.RolloverCollection.Count)
                throw new Exception("Update instrument's contract months (expiration and rollover dates)");

            return instrument.MasterInstrument.Name + " " + instrument.MasterInstrument.RolloverCollection[i + 1].ToString();
        }

        private DateTime GetRolloverDate(Instrument instrument)
        {
            int nextRollCount = -1;
            for (int i = 0; i < instrument.MasterInstrument.RolloverCollection.Count; i++)
            {
                if (instrument.Expiry == instrument.MasterInstrument.RolloverCollection[i].ContractMonth && nextRollCount < 0)
                    nextRollCount = i + 1;
            }

            return instrument.MasterInstrument.RolloverCollection[nextRollCount].Date;
        }

        private double? GetStoplossPercentageRemaining(bool IsLong)
        {
            //var lastTrades = Database.GetLastOrdersEntry(pathDirectory, this.Name, 1);
            //if (lastTrades.Count == 0)
            //    return null;

            OrderEntryF lastTrade = Database.GetLastOrderMongo(StrategyId, Account.Name, trades_server, trades_collection);
            if (lastTrade == null)
                return null;
            if (lastTrade.OrdersExit.Count == 0)
                return null;

            OrderExitFile exitOrder = lastTrade.OrdersExit.Last();

            if (lastTrade.StoplossList.Count == 0)
                return null;

            PriceOrderExit stoplossOrder = lastTrade.StoplossList.Last();

            double perc_remain;
            if (IsLong)
            {
                perc_remain = (exitOrder.Price - stoplossOrder.Price) / lastTrade.Price;
            }
            else
            {
                perc_remain = (stoplossOrder.Price - exitOrder.Price) / lastTrade.Price;
            }

            return perc_remain;
        }

        private double? GetNewStoplossPercentage(bool IsLong, double CurrentEntryPrice)
        {
            OrderEntryF lastTrade = Database.GetLastOrderMongo(StrategyId, Account.Name, trades_server, trades_collection);
            if (lastTrade == null)
                return null;
            if (lastTrade.OrdersExit.Count == 0)
                return null;

            OrderExitFile exitOrder = lastTrade.OrdersExit.Last();

            if (lastTrade.StoplossList.Count == 0)
                return null;

            PriceOrderExit stoplossOrder = lastTrade.StoplossList.Last();

            double price, newStoplossPercentage;
            //double newStoploss, stoplossPercentage;

            if (IsLong)
            {
                if (stoplossOrder.Price > lastTrade.Price)
                {
                    price = (stoplossOrder.Price / (1 - (StopLossPercent / 100)));
                    if (((price - stoplossOrder.Price) / CurrentEntryPrice) > (StopLossPercent / 100))
                    {
                        return StopLossPercent / 100;
                    }
                }
                else
                {
                    price = exitOrder.ProfitCurrency > 0 ? lastTrade.Price : exitOrder.Price;
                }
                //newStoploss = (stoplossOrder.Price - price) + CurrentEntryPrice;
                //stoplossPercentage = (CurrentEntryPrice - newStoploss) / CurrentEntryPrice;

                newStoplossPercentage = (price - stoplossOrder.Price) / CurrentEntryPrice;
                //StopLossPercent = (newStoplossPercentage * 100);
            }
            else
            {
                if (stoplossOrder.Price < lastTrade.Price)
                {
                    price = (stoplossOrder.Price / (1 + (StopLossPercent / 100)));
                    if (((price - stoplossOrder.Price) / CurrentEntryPrice) < (StopLossPercent / 100))
                    {
                        return StopLossPercent / 100;
                    }
                }
                else
                {
                    price = exitOrder.ProfitCurrency > 0 ? lastTrade.Price : exitOrder.Price;
                }
                //double newStoploss = (price - stoplossOrder.Price) + CurrentEntryPrice;
                //double stoplossPercentage = (newStoploss - CurrentEntryPrice) / CurrentEntryPrice;

                newStoplossPercentage = (stoplossOrder.Price - price) / CurrentEntryPrice;
                //StopLossPercent = (newStoplossPercentage * 100);
            }
            return newStoplossPercentage;
        }

        private double? GetProfitTargetPercentageRemaining(bool IsLong)
        {
            //var lastTrades = Database.GetLastOrdersEntry(pathDirectory, this.Name, 1);
            //if (lastTrades.Count == 0)
            //    return null;

            OrderEntryF lastTrade = Database.GetLastOrderMongo(StrategyId, Account.Name, trades_server, trades_collection);
            if (lastTrade == null)
                return null;
            if (lastTrade.OrdersExit.Count == 0)
                return null;

            OrderExitFile exitOrder = lastTrade.OrdersExit.Last();

            if (lastTrade.ProfitTargetList.Count == 0)
                return null;

            PriceOrderExit profitOrder = lastTrade.ProfitTargetList.Last();

            double perc_remain;
            if (IsLong)
            {
                perc_remain = (profitOrder.Price - exitOrder.Price) / lastTrade.Price;
            }
            else
            {
                perc_remain = (exitOrder.Price - profitOrder.Price) / lastTrade.Price;
            }
            return perc_remain;
        }

        private double? GetNewProfitTargetPercentage(bool IsLong, double CurrentEntryPrice)
        {
            OrderEntryF lastTrade = Database.GetLastOrderMongo(StrategyId, Account.Name, trades_server, trades_collection);
            if (lastTrade == null)
                return null;
            if (lastTrade.OrdersExit.Count == 0)
                return null;

            OrderExitFile exitOrder = lastTrade.OrdersExit.Last();

            if (lastTrade.ProfitTargetList.Count == 0)
                return null;

            PriceOrderExit profitOrder = lastTrade.ProfitTargetList.Last();

            double price, newProfitTarget, profitTargetPercentage, newProfitTargetPercentage;
            price = exitOrder.ProfitCurrency > 0 ? lastTrade.Price : exitOrder.Price;

            if (IsLong)
            {
                //newProfitTarget = (profitOrder.Price - price) + CurrentEntryPrice;
                //profitTargetPercentage = (newProfitTarget - CurrentEntryPrice) / CurrentEntryPrice;

                newProfitTargetPercentage = (profitOrder.Price - price) / CurrentEntryPrice;
            }
            else
            {
                //newProfitTarget = (price - profitOrder.Price) + CurrentEntryPrice;
                //profitTargetPercentage = (newProfitTarget - CurrentEntryPrice) / CurrentEntryPrice;

                newProfitTargetPercentage = (price - profitOrder.Price) / CurrentEntryPrice;
            }
            return newProfitTargetPercentage;
        }

        public void CheckRollover()
        {
            ContractSession[BarsInProgress] = Bars.BarsSinceNewTradingDay;
            if (State == State.Realtime &&
                BarsArray[CurrentMainSeries].Instrument.MasterInstrument.InstrumentType == InstrumentType.Future &&
                BarsInProgress == CurrentMainSeries &&
                !expiredBar)
            {
                int daysUntilRollover = 5;

                expiredBar = IsExpired(CurrentSecondarySeries, rolloverDate, daysUntilRollover, StopLoss != null ? StopLoss.SignalName : null);
                if (expiredBar)
                {
                    int nBars = BarsArray.Length / 2;
                    CurrentMainSeries += nBars;
                    CurrentSecondarySeries += nBars;
                }
            }
        }

        private bool IsExpired(int barInProgress, DateTime rolloverDate, double daysUntilRollover, string currentEnterName)
        {
            bool expired = false;

            if ((rolloverDate - Times[barInProgress][0]).TotalDays <= daysUntilRollover)
            {
                string currentContract = BarsArray[barInProgress].Instrument.FullName;
                string nextContract = GetNextContract(BarsArray[barInProgress].Instrument);
                int nextBar = barInProgress + BarsArray.Length / 2;

                if (ContractData.ContainsKey(currentContract) &&
                    ContractData.ContainsKey(nextContract) &&
                    ContractData[nextContract] > ContractData[currentContract])
                {
                    if (ContractSession.ContainsKey(barInProgress) &&
                        ContractSession.ContainsKey(nextBar) &&
                        Math.Abs(ContractSession[barInProgress] - ContractSession[nextBar]) < 20 &&
                        ContractSession[barInProgress] > 0 && ContractSession[nextBar] > 0)
                    {
                        expired = true;
                        string AlertRolloverMsg = "Condiciones de vencimiento de " + currentContract + " cumplidas.";

                        string TelegramRolloverMessage = String.Join(
                            "\n",
                            (Account.Name == "Sim101" ? "[Incubation" : "[Realtime") + " " + Account.Name + "]",
                            "Estrategia: " + this.Name,
                            "Evento: Rollover",
                            "Instrumento: " + currentContract,
                            "Fecha: " + Times[barInProgress][0],
                            "Vencimiento: " + rolloverDate.ToString(),
                            "Volumen[" + currentContract + "]: " + ContractData[currentContract],
                            "Volumen[" + nextContract + "]: " + ContractData[nextContract],
                            "Posicion: " + Positions[barInProgress].ToString()
                        );

                        if (Positions[barInProgress].MarketPosition == MarketPosition.Flat)
                        {
                            AlertRolloverMsg += " La estrategia no tiene posicion en dicho contrato.";
                            rolloverDisconnect = true;
                        }
                        else
                        {
                            int qty = Positions[barInProgress].Quantity;
                            exitOnRollover = true;
                            if (Positions[barInProgress].MarketPosition == MarketPosition.Long)
                            {
                                var bid = GetCurrentBid();
                                if (bid > 0)
                                {
                                    AlertMessageTelegram = TemplateTelegramMessage;
                                    ParametersList.Clear();
                                    ParametersList.Add("Bid", bid.ToString());

                                    ExitLongLimit(barInProgress, false, qty, bid, ExitLongRolloverName, currentEnterName.IsNullOrEmpty() ? EnterLongName : currentEnterName);
                                }

                                //ExitLong(barInProgress, qty, ExitLongRolloverName, currentEnterName.IsNullOrEmpty() ? EnterLongName : currentEnterName);
                            }
                            else if (Positions[barInProgress].MarketPosition == MarketPosition.Short)
                            {
                                var ask = GetCurrentAsk();
                                if (ask > 0) 
                                {
                                    AlertMessageTelegram = TemplateTelegramMessage;
                                    ParametersList.Clear();
                                    ParametersList.Add("Ask", ask.ToString());

                                    ExitShortLimit(barInProgress, false, qty, ask, ExitShortRolloverName, currentEnterName.IsNullOrEmpty() ? EnterShortName : currentEnterName);
                                }
                                //ExitShort(barInProgress, qty, ExitShortRolloverName, currentEnterName.IsNullOrEmpty() ? EnterShortName : currentEnterName);
                            }
                        }


                        Print("[" + this.Name + "_Rollover] - " + AlertRolloverMsg);
                        Alert(this.Name + "_Rollover", Priority.High, AlertRolloverMsg, null, 5, Brushes.Black, Brushes.White);

                        Telegram.SendMessageInChannelEWMDevelopment(TelegramRolloverMessage);
                        //if (!TestTelegram && telegramNotification)
                        //{
                        //    Telegram.SendMessageInChannelEWMReport(TelegramRolloverMessage);
                        //}
                        if (TelegramDev)
                        {
                            Telegram.SendMessageInChannelEWMDevelopment(TelegramRolloverMessage);
                        }
                        if (TelegramLive)
                        {
                            Telegram.SendMessageInChannelEWMReport(TelegramRolloverMessage);
                        }
                        if (TelegramInc)
                        {
                            Telegram.SendMessageInChannelEWMIncubation(TelegramRolloverMessage);
                        }
                    }
                    
                }
            }
            return expired;
        }

        protected override void OnMarketData(MarketDataEventArgs e)
        {
            if (e.Instrument.MasterInstrument.InstrumentType == InstrumentType.Future &&
                ContractData != null &&
                e.MarketDataType == MarketDataType.DailyVolume)
            {
                ContractData[e.Instrument.FullName] = e.Volume;
            }
        }

        public void EnterLongOnRollover(int Series)
        {
            if (exitOnRollover && expiredBar)
            {
                entryOnRollover = IsExitOnRollover(ExitLongRolloverName);
            }
            if (Position.MarketPosition == MarketPosition.Flat && entryOnRollover)
            {
                var ask = GetCurrentAsk();
                int qty = GetLastTradeQty();

                AlertMessageTelegram = TemplateTelegramMessage;
                ParametersList.Clear();
                ParametersList.Add("Ask", ask.ToString());
                EnterLongLimit(Series, false, qty, ask, EnterLongRolloverName);
                //EnterLong(Series, qty, EnterLongRolloverName);
            }
        }

        public void EnterShortOnRollover(int Series)
        {
            if (exitOnRollover && expiredBar)
            {
                entryOnRollover = IsExitOnRollover(ExitShortRolloverName);
            }
            if (Position.MarketPosition == MarketPosition.Flat && entryOnRollover)
            {
                var bid = GetCurrentBid();
                int qty = GetLastTradeQty();

                AlertMessageTelegram = TemplateTelegramMessage;
                ParametersList.Clear();
                ParametersList.Add("Bid", bid.ToString());
                EnterShortLimit(Series, false, qty, bid, EnterShortRolloverName);
                //EnterShort(Series, qty, EnterShortRolloverName);
            }
        }

        private int GetLastTradeQty()
        {
            //var lastTradeList = Database.GetLastOrdersEntry(pathDirectory, this.Name, 1);
            //if (lastTradeList == null || lastTradeList.Count == 0)
            //    return -1;

            OrderEntryF lastTrade = Database.GetLastOrderMongo(StrategyId, Account.Name, trades_server, trades_collection);
            if (lastTrade == null)
                return -1;
            var exitOrders = lastTrade.OrdersExit;
            if (exitOrders == null || exitOrders.Count == 0)
                return -1;

            OrderExitFile lastExitOrder = exitOrders.Last();
            return lastExitOrder.Quantity;
        }

        #endregion

        #region Reordering

        public void Reorder(ReorderSwitch reorder = ReorderSwitch.None, int Series = 1)
        {
            if (reorder == ReorderSwitch.Entry && Direction == "Long")
            {
                var ask = GetCurrentAsk(Series);

                AlertMessageTelegram = TemplateTelegramMessage;

                if (ParametersList.ContainsKey("New Ask"))
                    ParametersList["New Ask"] = ask.ToString();
                else
                {
                    ParametersList.Add("New Ask", ask.ToString());
                    ParametersList.Add("Delayed Entry", "True");
                }
                timer.Start();
                EnterLongLimit(Series, true, positionSize, ask, timerOrder.Name);
            }
            else if (reorder == ReorderSwitch.Entry && Direction == "Short")
            {
                var bid = GetCurrentBid(Series);

                AlertMessageTelegram = TemplateTelegramMessage;

                if (ParametersList.ContainsKey("New Bid"))
                    ParametersList["New Bid"] = bid.ToString();
                else
                {
                    ParametersList.Add("New Bid", bid.ToString());
                    ParametersList.Add("Delayed Entry", "True");
                }

                timer.Start();
                EnterShortLimit(Series, true, positionSize, bid, timerOrder.Name);
            }
            else if (reorder == ReorderSwitch.Exit && Direction == "Long")
            {
                var bid = GetCurrentBid(Series);

                AlertMessageTelegram = TemplateTelegramMessage;

                if (ParametersList.ContainsKey("New Bid"))
                    ParametersList["New Bid"] = bid.ToString();
                else
                {
                    ParametersList.Add("New Bid", bid.ToString());
                    ParametersList.Add("Delayed Exit", "True");
                }
                timer.Start();
                ExitLongLimit(Series, true, positionSize, bid, timerOrder.Name, timerOrder.FromEntrySignal);
            }
            else if (reorder == ReorderSwitch.Exit && Direction == "Short")
            {
                var ask = GetCurrentAsk(Series);

                AlertMessageTelegram = TemplateTelegramMessage;

                if (ParametersList.ContainsKey("New Ask"))
                    ParametersList["New Ask"] = ask.ToString();
                else
                {
                    ParametersList.Add("New Ask", ask.ToString());
                    ParametersList.Add("Delayed Exit", "True");
                }

                timer.Start();
                ExitShortLimit(Series, true, positionSize, ask, timerOrder.Name, timerOrder.FromEntrySignal);
            }
        }

        private void TimerEventProcessor(Object obj, EventArgs eventArgs)
        {
            TriggerCustomEvent(CheckOrderStatus, timerOrder);
        }

        private void CheckOrderStatus(object state)
        {
            if (timerOrder != null && timerOrder.OrderState != OrderState.PartFilled && timerOrder.OrderState != OrderState.Filled)
            {
                timer.Stop();
                CancelOrder(timerOrder);
                //timerOrder = null;
            }
        }

        protected void CancelEntry()
        {
            if (reorderObj == ReorderSwitch.Entry && BarsInProgress == CurrentMainSeries)
            {
                timer.Stop();
                reorderObj = ReorderSwitch.None;
            }
        }

        protected void SetTimer(int secs = 10)
        {
            timer = new System.Timers.Timer(secs * 1000);
            timer.Elapsed += TimerEventProcessor;
        }

        protected void UnsetTimer()
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Elapsed -= TimerEventProcessor;
                timer.Dispose();
                timer = null;
                timerOrder = null;
            }
        }

        #endregion

        #region Logging
        private void SaveExecutionsLog(ExecutionsLog log)
        {
            try
            {
                BsonClassMap.RegisterClassMap<ExecutionsLog>();
            }
            catch (Exception)
            {
            }

            try
            {
                MongoClient client_remote = new MongoClient(Database.GetUriDb());
                IMongoDatabase db_remote = client_remote.GetDatabase("server_ninjatrader");
                IMongoCollection<ExecutionsLog> insert = db_remote.GetCollection<ExecutionsLog>("CustomLog");

                insert.InsertOne(log, new InsertOneOptions() { BypassDocumentValidation = false });
            }
            catch (MongoException mex)
            {
                Print("Excecutions Log Message: " + mex.Message);
            }
        }

        public IndicatorsLog SetIndicatorsLog()
        {
            IndicatorsLog log = new IndicatorsLog()
            {
                StrategyId = this.StrategyId,
                Symbol = this.InstrumentName,
                state = State.ToString(),
                market_position = Positions[0].MarketPosition.ToString(),
                BarTime = Times[0][0],
                ExecutionTime = DateTime.Now,
                close = Closes[0][0],
                ExtraParameters = new BsonDocument()
            };
            return log;
        }

        public bool SaveIndicatorsLog(IndicatorsLog log)
        {
            try
            {
                BsonClassMap.RegisterClassMap<IndicatorsLog>();
            }
            catch (Exception)
            {
            }

            if (State == State.Historical)
            {
                if (indicatorsLogs == null)
                    indicatorsLogs = new List<IndicatorsLog>();

                indicatorsLogs.Add(log);

                if ((Bars.LastBarTime - Time[0]).TotalMinutes <= BarsPeriod.Value)
                    if (indicatorsLogs.Count > 0)
                    {
                        try
                        {
                            MongoClient client_remote = new MongoClient(Database.GetUriDb());
                            IMongoDatabase db_remote = client_remote.GetDatabase(indicator_log_server);
                            IMongoCollection<IndicatorsLog> insert = db_remote.GetCollection<IndicatorsLog>(indicator_log_collection);

                            insert.InsertMany(indicatorsLogs, new InsertManyOptions() { BypassDocumentValidation = false, IsOrdered = false });
                            indicatorsLogs.Clear();
                        }
                        catch (MongoException mex)
                        {
                            if (!mex.Message.Contains("duplicate key error"))
                            {
                                Print("Indicators Log Message: " + mex.Message);
                                return false;
                            }
                        }
                    }
            }
            else if (State == State.Realtime)
            {
                try
                {
                    MongoClient client_remote = new MongoClient(Database.GetUriDb());
                    IMongoDatabase db_remote = client_remote.GetDatabase(indicator_log_server);
                    IMongoCollection<IndicatorsLog> insert = db_remote.GetCollection<IndicatorsLog>(indicator_log_collection);

                    insert.InsertOne(log, new InsertOneOptions() { BypassDocumentValidation = false });
                }
                catch (MongoException mex)
                {
                    if (!mex.Message.Contains("duplicate key error"))
                    {
                        Print("Indicators Log Message: " + mex.Message);
                        return false;
                    }
                }
            }

            return true;
        }
        #endregion

        #region Properties

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Generate Log Position", Order = 2, GroupName = "Generate Log")]
        public bool LogPosition
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Entry Price Log", Order = 3, GroupName = "Generate Log")]
        public double EntryPriceLog
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Quantity Log", Order = 4, GroupName = "Generate Log")]
        public int QuantityLog
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Date", Order = 5, GroupName = "Generate Log")]
        public DateTime DateGLog
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Time", Order = 6, GroupName = "Generate Log")]
        public TimeSpan TimeGLog
        { get; set; }

        //[NinjaScriptProperty]
        //[Range(0, int.MaxValue)]
        //[Display(Name = "Minutes back", Order = 5, GroupName = "Generate Log")]
        //public int MinutesBack
        //{ get; set; }



        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Save in database", Order = 7, GroupName = "Generate Log")]
        public bool SaveInDb
        { get; set; }


        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Save Trades", Order = 0, GroupName = "Parameters")]
        public bool SaveTrade
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Emergency Position", Order = 1, GroupName = "Parameters")]
        public EmergencyPosition emergencyPosition { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Telegram Live", Order = 1, GroupName = "Telegram Notification")]
        public bool TelegramLive
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Telegram Development", Order = 2, GroupName = "Telegram Notification")]
        public bool TelegramDev
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Telegram Incubation", Order = 3, GroupName = "Telegram Notification")]
        public bool TelegramInc
        { get; set; }
        #endregion

        #region Enums

        public enum ExecutionSwitch
        {
            Common,
            Future
        }

        public enum OrderSwitch
        {
            Common,
            Future
        }

        public enum EmergencyPosition
        {
            None,
            EnterLong,
            EnterShort,
            ExitLong,
            ExitShort
        }

        public enum TemporalityProfitSwitch
        {
            Secondary,
            Main
        }

        public enum ReorderSwitch
        {
            None,
            Entry,
            Exit,
            StopLoss,
            ProfitTarget
        }
        #endregion

        private class ExecutionsLog
        {
            [JsonIgnore]
            public ObjectId Id { get; set; }
            [BsonElement("operation_type")]
            public string OperationType;
            [BsonElement("strategy_id")]
            public string StrategyId;
            [BsonElement("strategy_name")]
            public string StrategyName;
            [BsonElement("bar_time")]
            public DateTime BarTime;
            [BsonElement("execution_time")]
            public DateTime ExecutionTime;
            [BsonElement("orderState")]
            public string OrderState;
            [BsonElement("filled")]
            public int Filled;
            [BsonElement("current_price")]
            public double CurrentPrice;
            [BsonElement("average_price")]
            public double AveragePrice;
            [BsonElement("account")]
            public string Account;
            [BsonElement("symbol")]
            public string Symbol;
        }

        public class IndicatorsLog
        {
            [JsonIgnore]
            public ObjectId Id { get; set; }
            [BsonElement("strategy_id")]
            public string StrategyId;
            [BsonElement("symbol")]
            public string Symbol;
            [BsonElement("current_state")]
            public string state;
            [BsonElement("market_position")]
            public string market_position;
            [BsonElement("bar_time")]
            public DateTime BarTime;
            [BsonElement("execution_time")]
            public DateTime ExecutionTime;
            [BsonElement("close")]
            public double close;

            [BsonElement("extra_parameters")]
            [BsonExtraElements]
            public BsonDocument ExtraParameters { get; set; }
        }


    }

}
