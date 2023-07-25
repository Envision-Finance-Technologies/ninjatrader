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
using System.Collections.ObjectModel;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public class Monitor_Capital_realtime : Strategy
    {
        private Account MyAccount;
        private List<Account_Collection> capital_list;
        private Account_Collection capital;
        private List<Account_Collection> account_list;
        private Collection<Account> allAcct;


        private static DatabaseJson Database = new DatabaseJson();
        /*Apuntar a local*/
        //private static MongoClient client_remote = new MongoClient("mongodb://localhost:27017");
        private static MongoClient client_remote = new MongoClient(Database.GetUriDb());
        //private static IMongoDatabase db_remote = client_remote.GetDatabase("server_test");
        private static IMongoDatabase db_remote = client_remote.GetDatabase("server_ninjatrader");
        protected System.Timers.Timer timer;
        private Timer timerPositions;
        private int seconds = 0;
        private bool getCapital = false;
        
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Save real-time positions by instruments";
                Name = "Monitor_Capital_realtime";
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

                capital_list = new List<Account_Collection>(); 
            }
            else if (State == State.Configure)
            {
                MyAccount = Account;
                account_list = new List<Account_Collection>();
                account_list = GetAccountCollection();
                getCapital = true;
            }
            else if (State == State.DataLoaded)
            {
                //foreach (Account sampleAccount in Account.All)
                //    Print(String.Format("The account {0} has a {1} unit FX lotsize set", sampleAccount.Name, sampleAccount.ForexLotSize));

                RegisterCMap();
                timerPositions = new Timer(1000);
                timerPositions.Elapsed += new ElapsedEventHandler(TimerPositions);
                timerPositions.Enabled = true;
                timerPositions.Start();
            }
            else if (State == State.Terminated)
            {
                UnsetTimer();
                TimerOff();
            }
        }

        private void TimerPositions(object Sender, EventArgs e)
        {
            // Set the caption to the current time.  
            seconds += 1;
            //Print("SEC: " + seconds);
            if (seconds >= 30)
            {
                //getCapital = true;
                seconds = 0;
                OnBarUpdate();
                //Print("Finalizo ");
                //timerPositions.Stop();
                //SetState(State.Terminated);
            }
        }

        protected void TimerOff()
        {
            if (timerPositions != null)
            {
                timerPositions.Stop();
                timerPositions.Elapsed -= TimerPositions;
                timerPositions.Dispose();
                timerPositions = null;
            }
        }

        protected void UnsetTimer()
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Elapsed -= TimerPositions;
                timer.Dispose();
                timer = null;

            }
        }

        protected override void OnBarUpdate()
        {
            if (State == State.Realtime)
            {
                try
                {
                    if (getCapital == true)
                    {
                        getCapital = false;

                        foreach (Account acct in Account.All) 
                        {
                            var cap = acct.Get(AccountItem.NetLiquidation, acct.Denomination);
                            //Print("Capital: " + cap + " Name: " + acct.DisplayName);
                            capital = new Account_Collection()
                            {
                                Ninja_Trader_Id = acct.DisplayName,
                                Capital = cap
                            };
                            capital_list.Add(capital);
                        }

                        foreach(var item in account_list)
                        {
                            foreach(var item2 in capital_list) 
                            {
                                if(item.Ninja_Trader_Id == item2.Ninja_Trader_Id) 
                                {
                                    //Print("Va a actualizar");
                                    updateCapital(item2);
                                }
                            }
                            
                        }
                        capital_list.Clear();
                        getCapital = true;
                        seconds = 0;
                    }
                }
                catch(Exception ex) 
                {
                    Print("Error:" + ex.Message);
                    UnsetTimer();
                    TimerOff();
                }
            }
        }

        private void RegisterCMap() 
        {
            try
            {
                BsonClassMap.RegisterClassMap<Account_Collection>();
            }
            catch (Exception)
            {
            }
        }
        
        private List<Account_Collection> GetAccountCollection() 
        {
            IMongoCollection<Account_Collection> _collection = db_remote.GetCollection<Account_Collection>("accounts");
            var builder = Builders<Account_Collection>.Filter;
            var filter = new BsonDocument();
            var fields = Builders<Account_Collection>.Projection.Include(p => p.Ninja_Trader_Id);
            var result = _collection.Find(filter).Project<Account_Collection>(fields).ToList();

            return result;
        }

        private void updateCapital(Account_Collection capital) 
        {
            IMongoCollection<Account_Collection> _collection = db_remote.GetCollection<Account_Collection>("accounts");
            var builder = Builders<Account_Collection>.Filter;
            var filter = builder.Eq(i => i.Ninja_Trader_Id, capital.Ninja_Trader_Id);
            var update = Builders<Account_Collection>.Update
                .Set(r => r.Capital, capital.Capital);

            _collection.UpdateOne(filter, update, new UpdateOptions { IsUpsert = false });
            //Print("Actualizo: " + capital.Ninja_Trader_Id);
        }

        public class Account_Collection
        {
            [JsonIgnore]
            public ObjectId Id { get; set; }
            [BsonElement("ninjatrader_id")]
            public String Ninja_Trader_Id { get; set; }
            [BsonElement("capital")]
            public Double Capital { get; set; }

        }
    }
}
