using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Newtonsoft.Json;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Strategies;
using NinjaTraderServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class GenericNormalStrategyServices : Strategy
    {
        public string EnterLongName;
        public string EnterShortName;
        public string ExitLongName;
        public string ExitShortName;

        private Account MyAccount;
        //private Dictionary<string, StrategiesDataBase> listStrategies;
        //public List<PositionsStrategy> ordersInProcess;
        public int CurrentMainSeries, CurrentSecondarySeries, CurrentTertiarySeries;
        public string InstrumentName;
        DatabaseJson Database = new DatabaseJson();
        public bool searchDone = false;
        public int positionSize;
        public string StrategyVersionName;
        public string Direction;
        public string StrategyId;

        //private string server_name = "server_ninjatrader", open_position_collection = "LiveOpenPositions", trades_collection = "RealtimeDevelopmentTests";
        private string server_name = "server_test", collection_db = "TrailinStopStrategies";
        //public List<LiveOpenPositions> ordersInProcess;
        public TrailinDB ordersInProcess;
        public Double? Trailing_Stop;
        public bool OrderExit = false;

        public void StartDefault()
        {
            Calculate = Calculate.OnBarClose;
            EntriesPerDirection = 2;
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
            //reorderObj = ReorderSwitch.None;
            SetOrderQuantity = SetOrderQuantity.Strategy;
            StartBehavior = StartBehavior.ImmediatelySubmit;
            searchDone = false;
            //IsResetOnNewTradingDays = null;
        }

        public void StartConfigure()
        {
            MyAccount = Account;
            InstrumentName = System.Text.RegularExpressions.Regex.Match(Instrument.MasterInstrument.Name, @"^([\w\-]+)").ToString();
            //InstrumentName = SwitchSymbol(InstrumentName);
            //Print("INSTRUMENT NAME " + Instrument);
            //RegisterClassMap();
        }

        public bool CreateTrailinStrategyDocument()
        {
            MongoClient client_remote = new MongoClient(Database.GetUriDb());
            IMongoDatabase db_remote = client_remote.GetDatabase(server_name);
            var collection = db_remote.GetCollection<BsonDocument>(collection_db);
            var builder = Builders<BsonDocument>.Filter;

            //double pb = 0.33;

            //var filter = builder.Eq("strategy_name", StrategyVersionName) & builder.Eq("nt_status", "Completada");
            var filter = builder.Eq("strategy_name", StrategyVersionName) & builder.Ne("nt_status", "Completada");

            var result = collection.Find(filter).FirstOrDefault();

            if (result == null)
            {
                //No existe el registro, asi que procedemos a crearlo.
                var document = new BsonDocument {
                   { "_id", ObjectId.GenerateNewId() },
                   { "strategy_name", StrategyVersionName },
                   { "direction", Direction },
                   { "symbol", InstrumentName },
                   { "nt_status", "Pendiente"},
                   { "open_position", true},
                };

                collection.InsertOne(document);
                var id = document["_id"];
                //Print("Collection id: " + id);
                StrategyId = document["_id"].ToString();
                Description = StrategyId;
                return true;
            }
            else
            {
                //Print("RESULT: " + result["_id"]);
                StrategyId = result["_id"].ToString();
                Description = StrategyId;
                return false;
            }
        }

        #region Entries
        public bool StrategyEnterLong(int CurrentSeries, int Quantity, string SignalName)
        {
            try
            {
                EnterLong(CurrentSeries, Quantity, SignalName);
                return true;
            }
            catch (Exception ex)
            {
                Print("Strategy Enter Long Error: " + ex.Message);
                return false;
            }
        }

        public bool StrategyEnterShort(int CurrentSeries, int Quantity, string SignalName)
        {
            try
            {
                EnterShort(CurrentSeries, Quantity, SignalName);
                return true;
            }
            catch (Exception ex)
            {
                Print("Strategy Enter Short Error: " + ex.Message);
                return false;
            }
        }
        #endregion

        #region Exits
        public bool StrategyExitLong(int CurrentSeries, int Quantity, string ExitName, string SignalName)
        {
            try
            {
                ExitLong(CurrentSeries, Quantity, ExitName, SignalName);
                return true;
            }
            catch (Exception ex)
            {
                Print("Exit Long Delegate Error: " + ex.Message);
                return false;
            }
        }

        public bool StrategyExitShort(int CurrentSeries, int Quantity, string ExitName, string SignalName)
        {
            try
            {
                ExitShort(CurrentSeries, Quantity, ExitName, SignalName);
                return true;
            }
            catch (Exception ex)
            {
                Print("Exit Long Delegate Error: " + ex.Message);
                return false;
            }
        }
        #endregion

        protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string nativeError)
        {
            if (State == State.Realtime)
            {

                var name = order.Name.Split('_')[0];
                //Print("ON ORDER UPDATE NAME: " + name);
                if (order.OrderState == OrderState.Accepted)
                {
                    if (name == "Entry")
                    {
                        UpdateStatus(order.Name.Split('_')[1], "Entrada Aceptada");
                        //Print("Entrada Aceptada");
                        //listOrdersStrategies.Add(order.Name, order);
                    }
                    else if (name == "Exit")
                    {
                        UpdateStatus(order.Name.Split('_')[1], "Salida Aceptada");
                        //Print("Salida Aceptada");
                        //listOrdersStrategies.Add(order.Name, order);
                    }
                }
            }
        }

        private bool UpdateStatus(string id, string status)
        {
            try
            {
                MongoClient client_remote = new MongoClient(Database.GetUriDb());
                IMongoDatabase db_remote = client_remote.GetDatabase(server_name);
                var _collection = db_remote.GetCollection<TrailinDB>(collection_db);

                switch (status)
                {
                    case "Entrada Aceptada":

                        var filter = Builders<TrailinDB>.Filter.Eq(x => x.Id, ObjectId.Parse(id)) &
                            Builders<TrailinDB>.Filter.Eq(x => x.OpenPosition, true);

                        var update = Builders<TrailinDB>.Update
                                .Set(r => r.NinjaStatus, status);

                        _collection.UpdateOne(filter, update, new UpdateOptions { IsUpsert = false });
                        
                        break;

                    case "Salida Aceptada":

                        var filter_s = Builders<TrailinDB>.Filter.Eq(x => x.Id, ObjectId.Parse(id)) &
                            Builders<TrailinDB>.Filter.Eq(x => x.OpenPosition, false);

                        var update_s = Builders<TrailinDB>.Update
                                .Set(r => r.NinjaStatus, status);

                        _collection.UpdateOne(filter_s, update_s, new UpdateOptions { IsUpsert = false });

                        break;
                }
                return true;
            }
            catch (MongoException ex)
            {
                Print("Error Update NinjaStatus: " + ex.Message);
                return false;
            }
        }

        public bool UpdateEntries(string id, string status, DateTime date, Double price, int quantity)
        {
            try
            {
                MongoClient client_remote = new MongoClient(Database.GetUriDb());
                IMongoDatabase db_remote = client_remote.GetDatabase(server_name);
                var _collection = db_remote.GetCollection<TrailinDB>(collection_db);

                var builder = Builders<TrailinDB>.Filter;
                var filter_status = "{'_id': ObjectId('" + id + "'), 'open_position': true }";
                //var sort = Builders<TrailinDB>.Sort.Descending(p => p.EntryDate);
                //var result = _collection.Find(filter_status).Sort(sort).Limit(1).FirstOrDefault();
                var result = _collection.Find(filter_status).Limit(1).FirstOrDefault();

                if (result.NinjaStatus == "Pendiente" || result.NinjaStatus == "Entrada Aceptada")
                {
                    double? trailing = GetTrailingStop(id, result.Direction);
                    var signal_name = "Entry_" + result.Id.ToString() + "_" + Account.Name;

                    var filter = Builders<TrailinDB>.Filter.Eq(x => x.Id, ObjectId.Parse(id)) &
                    Builders<TrailinDB>.Filter.Eq(x => x.OpenPosition, true);
                    //var update = Builders<LiveOpenPositions>.Update.Set(r => r.NinjaStatus, status).Set(r => r.NinjaQuantity, quantity);
                    var update = Builders<TrailinDB>.Update.Set(r => r.NinjaStatus, status)
                        .Set(r => r.EntryDate, date)
                        .Set(r => r.StrategyId, result.StrategyId)
                        .Set(r => r.Quantity, quantity)
                        .Set(r => r.Entry, true)
                        .Set(r => r.Symbol, result.Symbol)
                        .Set(r => r.Direction, result.Direction)
                        .Set(r => r.NinjaStatus, status)
                        .Set(r => r.SignalName, signal_name)
                        .Set(r => r.Exit, false)
                        .Set(r => r.Trainling_Stop, trailing)
                        .Set(r => r.OpenPosition, false);

                    _collection.UpdateOne(filter, update);

                    //Insert 
                    //var ot_collection = db_remote.GetCollection<TrailinDB>(collection_db);
                    //var signal_name = "Entry_" + result.StrategyId + "_" + Account.Name;

                    //var filt = Builders<OrderTracking>.Filter.Eq(x => x.StrategyId, id) &
                    //Builders<OrderTracking>.Filter.Eq(x => x.Trainling_Stop, true);
                    //TrailinDB ot = new TrailinDB()
                    //{
                    //    EntryDate = date,
                    //    //StrategyId = id,
                    //    StrategyId = result.StrategyId,
                    //    //Quantity = quantity,
                    //    Quantity = positionSize,
                    //    Entry = true,
                    //    //Type = "Entry",
                    //    Symbol = result.Symbol,
                    //    Direction = result.Direction,
                    //    NinjaStatus = status,
                    //    SignalName = signal_name,
                    //    ExitDate = null,
                    //    Exit = false,
                    //    Trainling_Stop = trailing,
                    //    OpenPosition = false
                    //};

                    //ot_collection.InsertOne(ot);
                }

                return true;
            }
            catch (MongoException ex)
            {
                Print("Error UpdateEntries: " + ex.Message);
                return false;
            }
        }

        public Double? GetTrailingStop(string id, string direction)
        {
            MongoClient client_remote = new MongoClient(Database.GetUriDb());
            IMongoDatabase db_remote = client_remote.GetDatabase(server_name);
            var collection = db_remote.GetCollection<TrailinDB>(collection_db);
            double? trailing = 0;
            
            var filter = Builders<TrailinDB>.Filter.And(
                        Builders<TrailinDB>.Filter.Eq(x => x.Id, ObjectId.Parse(id)),
                        Builders<TrailinDB>.Filter.Or(
                            Builders<TrailinDB>.Filter.Exists(x => x.Trainling_Stop),
                            Builders<TrailinDB>.Filter.Ne(x => x.Trainling_Stop, 0)
                        )
                    );

            var sort = Builders<TrailinDB>.Sort.Descending(p => p.EntryDate);

            var result = collection.Find(filter).Sort(sort).Limit(1).FirstOrDefault();
            
            if(result == null) 
            {
                trailing = direction == "Long" ? Close[0] - (Close[0] * (Trailing_Stop.Value / 100)) : Close[0] + (Close[0] * (Trailing_Stop.Value / 100));
            }
            else if (result.Trainling_Stop == null)
            {
                trailing = direction == "Long" ? Close[0] - (Close[0] * (Trailing_Stop.Value / 100)) : Close[0] + (Close[0] * (Trailing_Stop.Value / 100));
            }
            else
            {
                trailing = result.Trainling_Stop.Value;
            }

            return trailing;
        }

        public bool UpdateExits(string id, string status, DateTime date, Double price, int quantity)
        {
            try
            {
                MongoClient client_remote = new MongoClient(Database.GetUriDb());
                IMongoDatabase db_remote = client_remote.GetDatabase(server_name);
                var _collection = db_remote.GetCollection<TrailinDB>(collection_db);
                var filter_status = "{'_id': ObjectId('" + id + "'), 'open_position': false }";
                var sort = Builders<TrailinDB>.Sort.Descending(p => p.EntryDate);
                var result = _collection.Find(filter_status).Sort(sort).Limit(1).FirstOrDefault();

                //Print("Resultado de Update Exits: " + result);
                //var accountSpecs = resultAccountSpecs.LastOrDefault().AccountSpecs.LastOrDefault();

                if (result.NinjaStatus == "En Proceso" || result.NinjaStatus == "Salida Aceptada")
                {
                    if (status == "Salida Parcial")
                    {
                        var filter = Builders<TrailinDB>.Filter.Eq(x => x.Id, ObjectId.Parse(id)) &
                        Builders<TrailinDB>.Filter.Eq(x => x.OpenPosition, false);
                        var update = Builders<TrailinDB>.Update.Set(r => r.NinjaStatus, status);
                        _collection.UpdateOne(filter, update);
                    }
                    else
                    {
                        var filter = Builders<TrailinDB>.Filter.Eq(x => x.Id, ObjectId.Parse(id)) &
                        Builders<TrailinDB>.Filter.Eq(x => x.OpenPosition, false);
                        var update = Builders<TrailinDB>.Update.Set(r => r.NinjaStatus, status);
                        //var update = Builders<LiveOpenPositions>.Update.Set(r => r.NinjaStatus, status).Set(r => r.NinjaQuantity, quantity);
                        _collection.UpdateOne(filter, update);
                        var strategy_id = result.Id;
                        var ot_collection = db_remote.GetCollection<TrailinDB>(collection_db);

                        var ot_filter = Builders<TrailinDB>.Filter.Eq(x => x.Id, ObjectId.Parse(id));
                        var ot_result = ot_collection.Find(ot_filter).FirstOrDefault();

                        var signal_name = "Entry_" + id + "_" + Account.Name;
                        var exit_name = "Exit_" + id + "_" + Account.Name;
                        //var signal_name = "Entry_" + result.StrategyId + "_" + Account.Name;
                        //var exit_name = "Exit_" + result.StrategyId + "_" + Account.Name;
                        //Print("EXECUTION TIME: " + date);

                        var ot_update = Builders<TrailinDB>.Update.Set(r => r.ExitDate, date)
                            .Set(r => r.Exit, true)
                            .Set(r => r.NinjaStatus, status)
                            .Set(r => r.ExitName, exit_name);

                        ot_collection.UpdateOne(ot_filter, ot_update);
                        OrderExit = true;
                        //OrderTracking ot = new OrderTracking()
                        //{
                        //    //EntryDate = result.EntryDate,
                        //    ExitDate = date,
                        //    //StrategyId = id,
                        //    StrategyId = result.StrategyId,
                        //    Quantity = quantity,
                        //    Type = "Exit",
                        //    Symbol = result.Symbol,
                        //    Group = result.Group,
                        //    Direction = result.Direction,
                        //    NinjaQuantity = quantity,
                        //    NinjaStatus = status,
                        //    SignalName = signal_name,
                        //    ExitName = exit_name
                        //};

                        //ot_collection.InsertOne(ot);
                    }
                }

                //var filter = builder.Eq(i => i.Symbol, InstrumentName) & !builder.AnyIn("ask.price", new[] { 0 }) & !builder.AnyIn("bid.price", new[] { 0 });
                return true;
            }
            catch (MongoException ex)
            {
                Print("Error Update Exits: " + ex.Message);
                return false;
            }
        }

        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            var name = execution.Order.Name.Split('_')[0];
            //Print("OnExecutionUpdate: " + Name);
            if (name == "Entry")
            {
                if (execution.Order.OrderState == OrderState.Filled || execution.Order.OrderState == OrderState.PartFilled || (execution.Order.OrderState == OrderState.Cancelled && execution.Order.Filled > 0))
                {
                    if (State == State.Realtime)
                    {

                        // Entrada PartFilled
                        if (execution.Order.OrderState == OrderState.Filled)
                        {
                            //Print("Success Entry Update");
                            //listOrdersStrategies.Remove(execution.Order.Name);
                            var resultUpdate = UpdateEntries(execution.Order.Name.Split('_')[1], "En Proceso", execution.Time, execution.Order.AverageFillPrice, quantity);
                            if (resultUpdate)
                                Print("Success Entry Update");
                            else
                                Print("Error Entry Update");
                        }
                    }
                }
            }

            if (name == "Exit")
            {
                //Print("OnExecutionUpdate SALIDA: " + name);
                if (execution.Order.OrderState == OrderState.Filled || execution.Order.OrderState == OrderState.PartFilled || (execution.Order.OrderState == OrderState.Cancelled && execution.Order.Filled > 0))
                {
                    //Print("MI ESTATUS EN ESTE PUNTO ES: " + State.ToString());
                    if (State == State.Realtime)
                    {
                        //Print("REALTIME");
                        // Salida PartFilled
                        if (execution.Order.OrderState == OrderState.Filled)
                        {
                            //Print("Success Exit Update");
                            // listOrdersStrategies.Remove(execution.Order.Name);
                            var resultUpdate = UpdateExits(execution.Order.Name.Split('_')[1], "Completada", execution.Time, execution.Order.AverageFillPrice, quantity);
                            if (resultUpdate)
                                Print("Success Exit Update");
                            else
                                Print("Error Exit Update");
                        }
                        else if (execution.Order.OrderState == OrderState.PartFilled)
                        {
                            Print("Salida Parcial");
                            var resultUpdate = UpdateExits(execution.Order.Name.Split('_')[1], "Salida Parcial", execution.Time, execution.Order.AverageFillPrice, quantity);
                        }

                    }
                }
            }
        }

        public void GetStrategyEntries()
        {
            //MongoClient client_remote = new MongoClient(Database.GetUriDb());
            //IMongoDatabase db_remote = client_remote.GetDatabase(server_name);
            //var _collection = db_remote.GetCollection<PositionsStrategy>(open_position_collection);

            //var filter_aggregate = @"
            //{
            //   aggregate: '" + open_position_collection + @"',
            //   pipeline:[
            //    {'$unwind': '$account_specs'},
            //    {
            //        '$addFields': {
            //            'last_status': { '$slice': [ '$account_specs.status', -1 ]},
            //            'account_specs': ['$account_specs']
            //        }
            //    },
            //    { '$match':{ 'symbol': '" + InstrumentName + "', 'open_position': true, 'account_specs.ib_id': '" + Account.Name + @"', 'last_status' : 'Pendiente' } },
            //    {
            //        '$project': 
            //        { 'last_status':0 }
            //    }],
            //    cursor: {}, allowDiskUse: true }";

            //var executePositionStrategy = db_remote.RunCommand<BsonDocument>(filter_aggregate);
            //var castPositionStrategy = executePositionStrategy["cursor"]["firstBatch"];
            //var result = BsonSerializer.Deserialize<List<PositionsStrategy>>(castPositionStrategy.ToJson());

            //return result;
        }

        public TrailinDB GetOpenPosition(string symbol)
        {
            TrailinDB result = new TrailinDB();
            try
            {
                //Print("GET OPEN POSITION SYMBOL " + symbol);
                //var instrument_changed = SwitchSymbol(symbol); //symbol = SwitchSymbol(symbol);
                //Print("AFTER SWITCH SYMBOL " + instrument_changed);
                MongoClient client_remote = new MongoClient(Database.GetUriDb());
                IMongoDatabase db_remote = client_remote.GetDatabase(server_name);
                //var _collection = db_remote.GetCollection<LiveOpenPositions>(collection);

                IMongoCollection<TrailinDB> _collection = db_remote.GetCollection<TrailinDB>(collection_db);
                var builder = Builders<TrailinDB>.Filter;
                var filter = builder.Eq(i => i.Symbol, symbol) & builder.Eq(i => i.OpenPosition, true) & builder.Eq(i => i.NinjaStatus, "Pendiente");
                var sort = Builders<TrailinDB>.Sort.Descending(p => p.EntryDate);
                result = _collection.Find(filter).Sort(sort).Limit(1).FirstOrDefault();

                //Print("Result Open Position: ");
                //if (result != null)
                //{
                //    Print(result);
                //    Print(result.Symbol);
                //    Print(result.Direction);
                //    Print(result.Group);
                //}
            }
            catch(Exception ex) 
            {
                Print("ERROR GET OPEN POSITION " + ex.Message);
            }

            return result;   
        }

        public LiveOpenPositions GetExitPosition(string symbol)
        {
            LiveOpenPositions result = new LiveOpenPositions();
            try
            {
                //symbol = SwitchSymbol(symbol);
                var instrument_changed = SwitchSymbol(symbol);

                MongoClient client_remote = new MongoClient(Database.GetUriDb());
                IMongoDatabase db_remote = client_remote.GetDatabase(server_name);
                //var _collection = db_remote.GetCollection<LiveOpenPositions>(collection);

                IMongoCollection<LiveOpenPositions> _collection = db_remote.GetCollection<LiveOpenPositions>(collection_db);
                var builder = Builders<LiveOpenPositions>.Filter;
                var filter = builder.Eq(i => i.Symbol, instrument_changed) & builder.Eq(i => i.OpenPosition, false) & builder.Eq(i => i.NinjaStatus, "En Proceso");
                var sort = Builders<LiveOpenPositions>.Sort.Descending(p => p.EntryDate);
                result = _collection.Find(filter).Sort(sort).Limit(1).FirstOrDefault();

                //if (result != null)
                //{
                //    Print(result);
                //    Print(result.Symbol);
                //    Print(result.Direction);
                //    Print(result.Group);
                //}
            }
            catch(Exception ex) 
            {
                Print("ERROR GET EXIT POSITION " + ex.Message);
            }

            return result;
        }

        public string SwitchSymbol(string symbol)
        {
            if (symbol == "MES")
                symbol = "SPY";
            else if (symbol == "MNQ")
                symbol = "QQQ";
            
            return symbol;
        }

        public TrailinDB ReorderDB()
        {
            var instrument = InstrumentName;
            //var instrument_changed = SwitchSymbol(instrument);
            //Print("REORDER INSTRUMENT NAME 1" + instrument);
            TrailinDB result = new TrailinDB();
            try
            {
                MongoClient client_remote = new MongoClient(Database.GetUriDb());
                IMongoDatabase db_remote = client_remote.GetDatabase(server_name);
                var _collection = db_remote.GetCollection<TrailinDB>(collection_db);

                //var filter_status = "{'symbol': '" + instrument + "', direction: '" + Direction + "' nt_status: {'$in':['Entrada Aceptada', 'Salida Aceptada'] } }";
                var filter_status = "{'symbol': '" + instrument + "', direction: '" + Direction + "', " +
                    "nt_status: { '$in': ['Entrada Aceptada', 'Salida Aceptada'], '$ne': { '$in': ['Entrada Aceptada', 'Salida Aceptada'] } } }";
                var sort = Builders<TrailinDB>.Sort.Descending(p => p.EntryDate);
                result = _collection.Find(filter_status).FirstOrDefault();
                //Print("REORDER RESULT" + instrument);
            }
            catch(Exception ex)
            {
                Print("ERROR REORDER DB " + ex.Message);
            }
            //var executePositionStrategy = db_remote.RunCommand<BsonDocument>(filter_aggregate);
            //var castPositionStrategy = executePositionStrategy["cursor"]["firstBatch"];
            //var result = BsonSerializer.Deserialize<List<PositionsStrategy>>(castPositionStrategy.ToJson());

            return result;
        }

        public TrailinDB GetStrategiesInProcess()
        {
            TrailinDB result = new TrailinDB();
            //var instrument_changed = SwitchSymbol(InstrumentName);

            try
            {
                MongoClient client_remote = new MongoClient(Database.GetUriDb());
                IMongoDatabase db_remote = client_remote.GetDatabase(server_name);
                var _collection = db_remote.GetCollection<TrailinDB>(collection_db);
                //MongoClient client_remote = new MongoClient(Database.GetUriDb());
                //IMongoDatabase db_remote = client_remote.GetDatabase(server_name);
                //var _collection = db_remote.GetCollection<LiveOpenPositions>(collection);
                //Print("RECUPERANDO INSTRUMENT NAME " + instrument_changed);
                var filter_status = "{'symbol': '" + InstrumentName + "', nt_status: 'En Proceso'}";
                //var filter_status = "{'symbol': '" + instrument_changed + "', nt_status: 'En Proceso'}";

                var sort = Builders<TrailinDB>.Sort.Descending(p => p.EntryDate);
                result = _collection.Find(filter_status).FirstOrDefault();
            }
            catch(Exception ex)
            {
                Print("ERROR GET STRATEGIE IN PROCESS" + ex.Message);
            }
            //var executePositionStrategy = db_remote.RunCommand<BsonDocument>(filter_aggregate);
            //var castPositionStrategy = executePositionStrategy["cursor"]["firstBatch"];
            //var result = BsonSerializer.Deserialize<List<PositionsStrategy>>(castPositionStrategy.ToJson());

            return result;
        }

        public void RecoveryOrdersInProcess(int Series)
        {
            if (!searchDone)
            {
                //Find Orders
                //ordersPending = new List<PositionsStrategy>();
                //Print("Before Get Strategies");
                ordersInProcess = GetStrategiesInProcess();
                //Print("CANTIDAD RECOVERY: " + ordersInProcess.Count());
                //if (ordersInProcess.Count() > 0)
                //{
                //Print("SEARCH DONE");
                searchDone = true;
                //}
            }

            if (ordersInProcess != null)
            {
                //Print("Order In Proccess");
                
                //Print("Order In Proccess ID " + item.OpenpositionId.ToString());
                //var accountSpecs = item.AccountSpecs.Where(a => a.IbId == Account.Name).Last();
                var signal_name = "Entry_" + ordersInProcess.Id.ToString() + "_" + Account.Name;
                    
                if (Time[0] >= ordersInProcess.EntryDate)
                {
                    //Print("Time0: " + Time[0] + " EntryDate: " + item.EntryDate);
                    //Print("AQUI PRRO");
                    if (ordersInProcess.Direction == "Long")
                    {
                        EnterLong(Series, ordersInProcess.Quantity, signal_name);
                    }
                    else if (ordersInProcess.Direction == "Short")
                    {
                        //Print("ENTRADA SHORT");
                        //Print(Series);
                        //Print(item.NinjaQuantity);
                        //Print(signal_name);
                        try
                        {
                            EnterShort(Series, ordersInProcess.Quantity, signal_name);
                        }
                        catch(Exception ex)
                        {
                            Print("Se ha producido un error: " + ex.Message);
                            // Realiza acciones adicionales según sea necesario
                        }
                    }

                    ordersInProcess = null;
                }
                
            }
        }

        public int GetPositionSize(MasterInstrument mInstrument, double buyPrice, double StopLossPrice, double riskLevel, double contract_size)
        {
            double Capital = MyAccount.Get(AccountItem.NetLiquidation, MyAccount.Denomination);
            //double positionSize;
            //var riskLevel = 0.01; //Temporal mientras me pasan el valor real.
            //var contract_size = 1; //Temporal mientras me pasan el valor real.
            //(Valor neto de liquidación* nivel de riesgo por operación) / (( | precio de entrada - precio de salida por stop loss | ) *contract size)
            double pz;
            pz = (Capital * (riskLevel)) / ((buyPrice - (StopLossPrice)) * contract_size);
            pz = pz < 1 ? 1 : pz;
            //Print("POSITION SIZE: " + pz);

            return (int)pz;
        }

        #region StopLossConditions

        public void StopLossLongCondition(int Series, double StopLossPrice, int Quantity, string ExitName, string SignalName, bool Test = false)
        {
            if (StopLossPrice > 0)
            {
                if (Close[0] <= StopLossPrice || Test == true)
                {
                    if (State == State.Realtime)
                    {
                        //positionSize
                        ExitLong(Series, Quantity, ExitName, SignalName);

                        //AlertMessageTelegram = TemplateTelegramMessage;
                        //ParametersList.Clear();
                        //ExitLong(Series, Quantity, StopLoss.Name, StopLoss.SignalName);
                    }

                }
            }
        }

        public void StopLossShortCondition(int Series, double StopLossPrice, int Quantity, string ExitName, string SignalName, bool Test = false)
        {
            if (StopLossPrice > 0)
            {
                if (Close[0] >= StopLossPrice || Test == true)
                {
                    if (State == State.Realtime)
                    {
                        //positionSize
                        ExitShort(Series, Quantity, ExitName, SignalName);
                    }
                }
            }
        }
        #endregion

        #region ProfitTargetConditions
        public void ProfitTargetLongCondition(int Series, double TargetProfitPrice, int Quantity, string ExitName, string SignalName, bool Test = false)
        {
            if (TargetProfitPrice > 0)
            {
                if (Close[0] >= TargetProfitPrice || Test == true)
                {
                    if (State == State.Realtime)
                    {
                        //AlertMessageTelegram = TemplateTelegramMessage;
                        //ParametersList.Clear();
                        ExitLong(Series, Quantity, ExitName, SignalName);
                    }
                }
            }
        }

        public void ProfitTargetShortCondition(int Series, double TargetProfitPrice, int Quantity, string ExitName, string SignalName, bool Test = false)
        {
            if (TargetProfitPrice > 0)
            {
                if (Close[0] <= TargetProfitPrice || Test == true)
                {
                    if (State == State.Realtime)
                    {
                        //AlertMessageTelegram = TemplateTelegramMessage;
                        //ParametersList.Clear();
                        ExitShort(Series, Quantity, ExitName, SignalName);
                    }
                }
            }
        }

        #endregion

        #region TrailingStopConditions

        public bool UpdateTrailing(string id, double? trailing)
        {
            MongoClient client_remote = new MongoClient(Database.GetUriDb());
            IMongoDatabase db_remote = client_remote.GetDatabase(server_name);
            var collection = db_remote.GetCollection<TrailinDB>(collection_db);

            var filter = Builders<TrailinDB>.Filter.Eq(x => x.Id, ObjectId.Parse(id));
            //var update = Builders<LiveOpenPositions>.Update.Set(r => r.NinjaStatus, status).Set(r => r.NinjaQuantity, quantity);
            var update = Builders<TrailinDB>.Update.Set(r => r.Trainling_Stop, trailing);
            var resp = collection.UpdateOne(filter, update);

            return resp.ModifiedCount > 0;
        }

        public bool TrailingStopLongCondition(int CurrentSecondarySeries, int quantity, string exit_name, string signal_name, double? StopLossPercent, string id)
        {
            // Si el precio baja al pagar y encender y es menor que el stop sales directo
            // 0.02

            //100 ¿? 98
            //102 ¿¿ 98.08
            //Print("TRAILING STOP: " + StopLossPercent);

            //guardar el stoploss actualizado 
            // cuando se apague y se encienda, comparar el stoploss guardado con el nuevo si el guardado es mayor que el nuevo no actualizar en base de datos
            // verificar si el stoploss baja y choca el trailing entonces alli salir.

            double trailing = Close[0] - (Close[0] * (StopLossPercent.Value / 100)); //trailing mas actual.
            double? trailing_db = GetTrailingStop(id, "Long"); // trailing base de datos.
            Print("Coles[1]: " + Close[1]);
            Print("Coles[0]: " + Close[0]);
            var col1 = Close[1];
            var col0 = Close[0];
            if (trailing_db == null || trailing > trailing_db)
            {
                var update_result = UpdateTrailing(id, trailing);

                if (update_result && trailing > Close[1])
                {
                    if (State == State.Realtime)
                    {
                        //positionSize
                        ExitLong(CurrentSecondarySeries, quantity, exit_name, signal_name);
                    }
                    return true;
                }
                else if (trailing_db > Close[0])
                {
                    if (State == State.Realtime)
                    {
                        //positionSize
                        ExitLong(CurrentSecondarySeries, quantity, exit_name, signal_name);
                    }
                    return true;
                }
            }
            else
            {
                if (trailing_db > Close[0])
                {
                    if (State == State.Realtime && Position.MarketPosition == MarketPosition.Long)
                    {
                        //positionSize
                        ExitLong(CurrentSecondarySeries, quantity, exit_name, signal_name);
                    }
                    return true;
                }
            }

            return false;


            //if (trailing_db != null && trailing_db != 0) 
            //{
            //    //Validaciones
            //    if (trailing > trailing_db)
            //    { // Si el trailing nuevo es mayor al almacenado actualizamos en base de datos.
            //        var update_result = UpdateTrailing(id, trailing);

            //        if (update_result)
            //        {
            //            if (trailing > Close[0])
            //            {
            //                return trailing;
            //            }
            //        }
            //    }
            //    else
            //        return 0;
            //}
            //else 
            //{
            //    var update_result = UpdateTrailing(id, trailing);

            //    if (update_result)
            //    {
            //        if (trailing > Close[0])
            //        {
            //            return trailing;
            //        }
            //    }
            //    else
            //    {
            //        return 0;
            //    }
            //}
        }

        public bool TrailingStopShortCondition(int CurrentSecondarySeries, int quantity, string exit_name, string signal_name, double? StopLossPercent, string id)
        {
            double trailing = Close[0] + (Close[0] * (StopLossPercent.Value / 100)); //trailing mas actual.

            double? trailing_db = GetTrailingStop(id, "Short"); // trailing base de datos.
            Print("Coles[1]: " + Close[1]);
            Print("Coles[0]: " + Close[0]);
            var col1 = Close[1];
            var col0 = Close[0];

            if (trailing_db == null || trailing < trailing_db)
            {
                var update_result = UpdateTrailing(id, trailing);

                if (update_result && trailing < Close[0])
                {
                    if (State == State.Realtime)
                    {
                        //positionSize
                        if (State == State.Realtime && Position.MarketPosition == MarketPosition.Short)
                        {
                            //positionSize
                            ExitShort(CurrentSecondarySeries, quantity, exit_name, signal_name);
                        }
                    }
                    return true;
                }
                else if (trailing_db < Close[0])
                {
                    if (State == State.Realtime)
                    {
                        //positionSize
                        ExitShort(CurrentSecondarySeries, quantity, exit_name, signal_name);
                    }
                    return true;
                }
            }
            else
            {
                if (trailing_db < Close[0])
                {
                    if (State == State.Realtime)
                    {
                        //positionSize
                        ExitShort(CurrentSecondarySeries, quantity, exit_name, signal_name);
                    }
                    return true;
                }
            }

            return false;
        }

        public TrailinDB GetStrategy()
        {
            MongoClient client_remote = new MongoClient(Database.GetUriDb());
            IMongoDatabase db_remote = client_remote.GetDatabase(server_name);
            var _collection = db_remote.GetCollection<TrailinDB>(collection_db);

            var builder = Builders<TrailinDB>.Filter;
            var filter_status = "{'_id': ObjectId('" + StrategyId + "') }";
            //var sort = Builders<TrailinDB>.Sort.Descending(p => p.EntryDate);
            //var result = _collection.Find(filter_status).Sort(sort).Limit(1).FirstOrDefault();
            var result = _collection.Find(filter_status).Limit(1).FirstOrDefault();

            return result;
        }

        #endregion

        public class TrailinDB
        {
            [JsonIgnore]
            public ObjectId Id { get; set; }
            [BsonElement("signal_name")]
            public String SignalName { get; set; }
            [BsonElement("exit_name")]
            public String ExitName { get; set; }
            [BsonElement("strategy_id")]
            public String StrategyId { get; set; }
            [BsonElement("strategy_name")]
            public String StrategyName { get; set; }
            [BsonElement("direction")]
            public String Direction { get; set; }
            [BsonElement("symbol")]
            public String Symbol { get; set; }
            [BsonElement("quantity")]
            public Int32 Quantity { get; set; }
            [BsonElement("open_position")]
            public Boolean OpenPosition { get; set; }
            [BsonElement("entry_date")]
            public DateTime EntryDate { get; set; }
            [BsonElement("exit_date")]
            public DateTime? ExitDate { get; set; }
            [BsonElement("nt_status")]
            public String NinjaStatus { get; set; }
            [BsonElement("entry")]
            public bool? Entry { get; set; }
            [BsonElement("exit")]
            public bool? Exit { get; set; }
            [BsonElement("trailing_stop")]
            public Double? Trainling_Stop { get; set; }
        }

        public class LiveOpenPositions
        {
            [JsonIgnore]
            public ObjectId Id { get; set; }
            [BsonElement("strategy_name")]
            public String StrategyName { get; set; }
            [BsonElement("direction")]
            public String Direction { get; set; }
            [BsonElement("symbol")]
            public String Symbol { get; set; }
            [BsonElement("group")]
            public String Group { get; set; }
            [BsonElement("quantity")]
            public Int32 Quantity { get; set; }
            [BsonElement("open_position")]
            public Boolean OpenPosition { get; set; }
            [BsonElement("entry_date")]
            public DateTime EntryDate { get; set; }
            [BsonElement("nt_status")]
            public String NinjaStatus { get; set; }
            [BsonElement("ninja_quantity")]
            public Int32 NinjaQuantity { get; set; }
            [BsonElement("entry_price")]
            public Double EntryPrice;
            [BsonElement("stop_loss_price")]
            public Double StopLossPrice;
            [BsonElement("target_profit_price")]
            public Double TargetProfitPrice;
            [BsonElement("strategy_id")]
            public String StrategyId { get; set; }
            [BsonElement("risk")]
            public Double Risk { get; set; }
            [BsonElement("contract_size")]
            public Double ContractSize { get; set; }
            [BsonExtraElements]
            [BsonElement("extra_parameters")]
            [JsonIgnore]
            public BsonDocument ExtraParameters { get; set; }
        }
    }
}
