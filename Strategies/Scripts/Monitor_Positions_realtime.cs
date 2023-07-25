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
    public class Monitor_Positions_realtime : Strategy
    {
        private Account MyAccount;
        private List<Monitor_Account_Positions> listPositions;
        private Monitor_Account_Positions accountPositions;
       
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
        
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Save real-time positions by instruments";
                Name = "Monitor_Positions_realtime";
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

                listPositions = new List<Monitor_Account_Positions>();
                //MyAccount = Account;
                // Find our Sim101 account
                //lock (Account.All)
                //    MyAccount = Account.All.FirstOrDefault(a => a.Name == MyAccount.Name);
                //MyAccount = Account;
            }
            else if (State == State.Configure)
            {
                MyAccount = Account;
				getPosition = true;
                //Print(MyAccount.Name);
            }
            else if (State == State.DataLoaded)
            {
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
            if (seconds >= 15)
            {
                //getPosition = true;
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
            //if (CurrentBars[0] < 1 && !getPosition)
            //    return;
            //if (CurrentBars[0] < 1)
            //{
            //    timerPositions.Start();
            //}
            //else if (getPosition) 
            //{ 
            //    //Print("seconds: " + seconds); 
            //}


            //if (BarsInProgress == 0)
//			if (CurrentBars[0] < 1 )
//                return;

            if (State == State.Realtime)
            {
                //if (BarsInProgress == 0 || getPosition == true)
                if(getPosition == true)
                {
					getPosition = false;
                    var count = MyAccount.Positions.Count();
                    //Print("Positions: " + count);
                    var date = DateTime.Now;
                    var time = new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Minute, date.Second, DateTimeKind.Utc);
                    //Print("date: " + date + " time: " + time);
                    foreach (Position position in MyAccount.Positions)
                    {
                        accountPositions = new Monitor_Account_Positions()
                        {
                            Symbol = position.Instrument.FullName,
                            Quantity = position.Quantity,
                            Account_Name = MyAccount.Name,
                            Date = time
                        };

                        listPositions.Add(accountPositions);

                        //Print(String.Format("Instrument: {0} quantity {1} Position {2}", position.Instrument.FullName, position.Quantity, position.MarketPosition));
                    }

                    VerifyMonitorPositions(listPositions, MyAccount.Name);

                    if (listPositions.Count() > 0)
                    {
                        SaveMonitorPositions(listPositions);
                        listPositions.Clear();
                    }

                    
                    seconds = 0;
                }
            }
        }

        private void RegisterCMap() 
        {
            try
            {
                BsonClassMap.RegisterClassMap<Monitor_Account_Positions>();
            }
            catch (Exception)
            {
            }
        }

        private void SaveMonitorPositions(List<Monitor_Account_Positions> listPositions)
        {
            //Print("Empezamos a Insertar y Actualizar");
            List<Monitor_Account_Positions> listUpdates;
            List<Monitor_Account_Positions> listInserts;

            //try
            //{
            //    BsonClassMap.RegisterClassMap<Monitor_Account_Positions>();
            //}
            //catch (Exception)
            //{
            //}

            try
            {
                listUpdates = new List<Monitor_Account_Positions>();
                listInserts = new List<Monitor_Account_Positions>();

                IMongoCollection<Monitor_Account_Positions> _collection = db_remote.GetCollection<Monitor_Account_Positions>("MonitorPositions");
                
                foreach (var item in listPositions.ToList())
                {
                    //Print("SYMBOL:" + item.Symbol + " accname: " + item.Account_Name);

                    var builder = Builders<Monitor_Account_Positions>.Filter;
                    var filter = builder.Eq(i => i.Symbol, item.Symbol) & builder.Eq(i => i.Account_Name, item.Account_Name);

                    var fields = Builders<Monitor_Account_Positions>.Projection.Include(p => p.Symbol).Include(p => p.Quantity).Include(p => p.Account_Name).Include(p => p.Date);
                    var result = _collection.Find(filter).Project<Monitor_Account_Positions>(fields).FirstOrDefault();

                    //Print("Result: " + result);

                    if (result != null){
						//Print("AGREGO A UPDATE");
                        listUpdates.Add(item);
					}
                    else{
						//Print("AGREGO A INSERT");
                        listInserts.Add(item);
					}
					
					
					
                }

                //Print("ListUpdates: " + listUpdates.Count());
                //Print("ListInserts: " + listInserts.Count());
				
				if(listUpdates.Count() > 0 && listInserts.Count() > 0)
				{
					foreach (var up in listUpdates)
                    {
						
                        var builder = Builders<Monitor_Account_Positions>.Filter;
                        var filter = builder.Eq(i => i.Symbol, up.Symbol) & builder.Eq(i => i.Account_Name, up.Account_Name);
                        var update = Builders<Monitor_Account_Positions>.Update
                            .Set(r => r.Quantity, up.Quantity).Set(r => r.Account_Name, up.Account_Name).Set(r => r.Date, up.Date);

                        _collection.UpdateOne(filter, update, new UpdateOptions{IsUpsert=false});
						//Print("Actualizo");
                    }
					
					var insert = db_remote.GetCollection<Monitor_Account_Positions>("MonitorPositions");
                    insert.InsertMany(listInserts, new InsertManyOptions() { IsOrdered = false });
					//Print("Inserto");
				}
				
                else if(listUpdates.Count() > 0) 
                {
                    foreach (var up in listUpdates)
                    {
                        var builder = Builders<Monitor_Account_Positions>.Filter;
                        var filter = builder.Eq(i => i.Symbol, up.Symbol) & builder.Eq(i => i.Account_Name, up.Account_Name);
                        var update = Builders<Monitor_Account_Positions>.Update
                            .Set(r => r.Quantity, up.Quantity).Set(r => r.Account_Name, up.Account_Name).Set(r => r.Date, up.Date);

                        //_collection.UpdateOne(filter, update);
						_collection.UpdateOne(filter, update, new UpdateOptions{IsUpsert=false});
						//Print("Actualizo");
                    }
                }

                else if(listInserts.Count() > 0) 
                {
                    var insert = db_remote.GetCollection<Monitor_Account_Positions>("MonitorPositions");
                    insert.InsertMany(listInserts, new InsertManyOptions() { IsOrdered = false });
					//Print("Inserto");
                }

                listUpdates.Clear();
                listInserts.Clear();
				getPosition = true;

            }
            catch (MongoException ex)
            {
                Print("ERROR MONITOR POSITIONS: " + ex.Message);
            }
        }

        private void VerifyMonitorPositions(List<Monitor_Account_Positions> listMPositions, string account)
        {
            //Print("Verificar Monitor Positions");
            List<Monitor_Account_Positions> listDeletes;
            
            try 
            {
                IMongoCollection<Monitor_Account_Positions> _collection = db_remote.GetCollection<Monitor_Account_Positions>("MonitorPositions");
                var builder = Builders<Monitor_Account_Positions>.Filter;
                var filter = builder.Eq(i => i.Account_Name, MyAccount.Name);
                var fields = Builders<Monitor_Account_Positions>.Projection.Include(p => p.Symbol).Include(p => p.Account_Name);
                var result = _collection.Find(filter).Project<Monitor_Account_Positions>(fields).ToList();

                var listSymbols = new List<string>();
                foreach(var s in listMPositions.ToList()) { listSymbols.Add(s.Symbol); }
                if (result.Any())
                {
                   // Print("Consiguio Resgistros -> Verificar: " + result.Count());
                    listDeletes = new List<Monitor_Account_Positions>();

                    if (listMPositions.Count() == 0)
                    {
                        listDeletes.AddRange(result);
                    }
                    else
                    {
                        for (var i = 0; i < result.Count(); i++)
                        {
                            // si no encuentra el simbolo lo a�ade a la lista de deletes
                            if (!listSymbols.Contains(result[i].Symbol))
                            {
                                //El symbolo no esta contenigo en la lista actualizada
                                listDeletes.Add(result[i]);
                            }
                        }
                    }

                    //Print(listDeletes.Count());
                    if (listDeletes.Count() > 0)
                    {
                        foreach (var it in listDeletes.ToList())
                        {
                            //Print("Quedaron Symbol: " + it.Symbol);
                            var filterD = builder.Eq(i => i.Symbol, it.Symbol) & builder.Eq(i => i.Account_Name, MyAccount.Name);
                            var resultD = _collection.Find(filterD).Project<Monitor_Account_Positions>(fields).ToList();

                            if(resultD.Any())
                                _collection.DeleteOne(filterD);
                        }
                    }
					listDeletes.Clear();
                }
                else
                {
                    //Print("No se encontraron registros en monitor Positions");
                }
				

                //Print("Termino la Verificacion");
            }
            catch(Exception ex) 
            {
                Print("ERROR"+ ex.Message);
            }
        }

        public class Monitor_Account_Positions
        {
            [JsonIgnore]
            public ObjectId Id { get; set; }
            [BsonElement("symbol")]
            public String Symbol { get; set; }
            [BsonElement("quantity")]
            public int Quantity { get; set; }
            [BsonElement("account_name")]
            public String Account_Name { get; set; }
            [BsonElement("date")]
            public DateTime Date { get; set; }
        }
    }
}
