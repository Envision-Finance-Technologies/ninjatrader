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
using MongoDB.Driver;
using Newtonsoft.Json;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver.Core.Operations;
using NinjaTraderServices;
using System.Timers;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public class Monitor_Instruments_realtime : Strategy
    {
        private Account MyAccount;
       
        private static DatabaseJson Database = new DatabaseJson();
        /*Apuntar a local*/
        //private static MongoClient client_remote = new MongoClient("mongodb://localhost:27017");
        private static MongoClient client_remote = new MongoClient(Database.GetUriDb());
        //private static IMongoDatabase db_remote = client_remote.GetDatabase("server_test");
        private static IMongoDatabase db_remote = client_remote.GetDatabase("server_ninjatrader");
        protected System.Timers.Timer timer;
        private Timer timerPositions;
        private int seconds = 0;
        private bool getPosition = false;
        private List<string> InstrumentsName;
        private MonitorInstruments monitorInstrument;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Save real-time positions by instruments";
                Name = "Monitor_Instruments_realtime";
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
                BarsRequiredToTrade = 1;
                IsInstantiatedOnEachOptimizationIteration = true;
                IncludeTradeHistoryInBacktest = true;
            }
            else if (State == State.Configure)
            {
                MyAccount = Account;
                InstrumentsName = new List<string>();
                InstrumentsName.Add("");
                getPosition = true;

                var listStrategies = MyAccount.Strategies.Where(u => u.Name.Contains("Delegate"));

                foreach (StrategyBase strategy in listStrategies)
                {
                    Print("Agregar Serie: " + strategy.Instrument.FullName);
                    var InstrumentName = System.Text.RegularExpressions.Regex.Match(strategy.Instrument.FullName, @"^([\w\-]+)").ToString();
                    InstrumentsName.Add(InstrumentName);
                    AddDataSeries(strategy.Instrument.FullName, new BarsPeriod { BarsPeriodType = BarsPeriodType.Minute, Value = 1 });
                }
            }
            else if (State == State.DataLoaded)
            {
                RegisterCMap();
            }
            else if (State == State.Terminated)
            {
            }
        }

        protected override void OnBarUpdate()
        {
            if (BarsInProgress == 0)
                return;

            if (State == State.Realtime)
            {
                Print("Realtime");
                var time = new DateTime(Time[0].Year, Time[0].Month, Time[0].Day, Time[0].Hour, Time[0].Minute, Time[0].Second, DateTimeKind.Utc);
                monitorInstrument = new MonitorInstruments()
                {
                    Symbol = InstrumentsName[BarsInProgress],
                    Close = Close[0],
                    Date = time,
                    Account_Name = MyAccount.Name
                };

                Print("Symbol: " + monitorInstrument.Symbol + " Close: " + monitorInstrument.Close + " Date: " + monitorInstrument.Date);

                var resp = UpdateMonitorInstruments(monitorInstrument);
            }
            
        }

        private void RegisterCMap() 
        {
            try
            {
                BsonClassMap.RegisterClassMap<MonitorInstruments>();
            }
            catch (Exception)
            {
            }
        }

        private bool UpdateMonitorInstruments(MonitorInstruments monitor) 
        {
            try 
            {
                IMongoCollection<MonitorInstruments> _collection = db_remote.GetCollection<MonitorInstruments>("MonitorInstruments");
                var builder = Builders<MonitorInstruments>.Filter;
                var filter = builder.Eq(i => i.Account_Name, monitor.Account_Name) & builder.Eq(i => i.Symbol, monitor.Symbol);
                var fields = Builders<MonitorInstruments>.Projection.Include(p => p.Symbol).Include(p => p.Account_Name);
                var result = _collection.Find(filter).Project<MonitorInstruments>(fields).FirstOrDefault();

                if (result != null)
                {
                    var update = Builders<MonitorInstruments>.Update
                                .Set(r => r.Date, monitor.Date).Set(r => r.Close, monitor.Close);

                    _collection.UpdateOne(filter, update, new UpdateOptions { IsUpsert = false });
                }
                else
                {
                    _collection.InsertOne(monitor);
                }
                return true;
            }
            catch(MongoException ex) 
            {
                Print("ERROR UpdateMonitorInstruments: " + ex.Message);
                return false;
            }
        }

        public class MonitorInstruments 
        {
            [JsonIgnore]
            public ObjectId Id { get; set; }
            [BsonElement("symbol")]
            public String Symbol { get; set; }
            [BsonElement("date")]
            public DateTime Date { get; set; }
            [BsonElement("close")]
            public Double Close { get; set; }
            [BsonElement("account_name")]
            public String Account_Name { get; set; }
        }
    }
}
