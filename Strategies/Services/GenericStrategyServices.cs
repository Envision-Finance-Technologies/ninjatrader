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
    public class GenericStrategyServices : Strategy
    {
        private Account MyAccount;
        //private Dictionary<string, StrategiesDataBase> listStrategies;
        //public List<PositionsStrategy> ordersInProcess;
        public int CurrentMainSeries, CurrentSecondarySeries, CurrentTertiarySeries;
        public string InstrumentName;
        DatabaseJson Database = new DatabaseJson();
        public bool searchDone = false;
        public int positionSize;
        public string StrategyVersionName;

        //private string server_name = "server_ninjatrader", open_position_collection = "LiveOpenPositions", trades_collection = "RealtimeDevelopmentTests";
        private string server_name = "server_test", collection = "LiveOpenPositions", order_tracking_collection = "OrderTracking";
        //public List<LiveOpenPositions> ordersInProcess;
        public List<OrderTracking> ordersInProcess;
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
                var _collection = db_remote.GetCollection<LiveOpenPositions>(collection);

                switch (status)
                {
                    case "Entrada Aceptada":

                        var filter = Builders<LiveOpenPositions>.Filter.Eq(x => x.Id, ObjectId.Parse(id)) &
                            Builders<LiveOpenPositions>.Filter.Eq(x => x.OpenPosition, true);

                        var update = Builders<LiveOpenPositions>.Update
                                .Set(r => r.NinjaStatus, status);

                        _collection.UpdateOne(filter, update, new UpdateOptions { IsUpsert = false });
                        
                        break;

                    case "Salida Aceptada":

                        var filter_s = Builders<LiveOpenPositions>.Filter.Eq(x => x.Id, ObjectId.Parse(id)) &
                            Builders<LiveOpenPositions>.Filter.Eq(x => x.OpenPosition, false);

                        var update_s = Builders<LiveOpenPositions>.Update
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
                var _collection = db_remote.GetCollection<LiveOpenPositions>(collection);

                var builder = Builders<LiveOpenPositions>.Filter;
                var filter_status = "{'_id': ObjectId('" + id + "'), 'open_position': true }";
                var sort = Builders<LiveOpenPositions>.Sort.Descending(p => p.EntryDate);
                var result = _collection.Find(filter_status).Sort(sort).Limit(1).FirstOrDefault();

                if (result.NinjaStatus == "Pendiente" || result.NinjaStatus == "Entrada Aceptada")
                {
                    var filter = Builders<LiveOpenPositions>.Filter.Eq(x => x.Id, ObjectId.Parse(id)) &
                    Builders<LiveOpenPositions>.Filter.Eq(x => x.OpenPosition, true);
                    //var update = Builders<LiveOpenPositions>.Update.Set(r => r.NinjaStatus, status).Set(r => r.NinjaQuantity, quantity);
                    var update = Builders<LiveOpenPositions>.Update.Set(r => r.NinjaStatus, status).Set(r => r.NinjaQuantity, quantity);
                    _collection.UpdateOne(filter, update);

                    //Insert 
                    var ot_collection = db_remote.GetCollection<OrderTracking>(order_tracking_collection);
                    var signal_name = "Entry_" + result.Id.ToString() + "_" + Account.Name;
                    //var signal_name = "Entry_" + result.StrategyId + "_" + Account.Name;

                    //var filt = Builders<OrderTracking>.Filter.Eq(x => x.StrategyId, id) &
                    //Builders<OrderTracking>.Filter.Eq(x => x.Trainling_Stop, true);
                    //double? trailing = GetTrailingStop(id, result.Direction);

                    OrderTracking ot = new OrderTracking() 
                    {
                        EntryDate = date,
                        //StrategyId = id,
                        OpenpositionId = id,
                        StrategyId = result.StrategyId,
                        //Quantity = quantity,
                        Quantity = positionSize,
                        Entry = true,
                        //Type = "Entry",
                        Symbol = result.Symbol,
                        Group = result.Group,
                        Direction = result.Direction,
                        NinjaQuantity = positionSize,
                        NinjaStatus = status,
                        SignalName = signal_name,
                        ExitDate = null,
                        Exit = false,
                        //Trainling_Stop = trailing
                    };

                    ot_collection.InsertOne(ot);
                }

                return true;
            }
            catch (MongoException ex)
            {
                Print("Error UpdateEntries: " + ex.Message);
                return false;
            }
        }

        //public Double? GetTrailingStop(string id, string direction)
        //{
        //    MongoClient client_remote = new MongoClient(Database.GetUriDb());
        //    IMongoDatabase db_remote = client_remote.GetDatabase(server_name);
        //    var collection = db_remote.GetCollection<OrderTracking>(order_tracking_collection);
        //    double? trailing = 0;
            
        //    var filter = Builders<OrderTracking>.Filter.And(
        //                Builders<OrderTracking>.Filter.Eq(x => x.OpenpositionId, id),
        //                Builders<OrderTracking>.Filter.Or(
        //                    Builders<OrderTracking>.Filter.Exists(x => x.Trainling_Stop),
        //                    Builders<OrderTracking>.Filter.Ne(x => x.Trainling_Stop, 0)
        //                )
        //            );

        //    var sort = Builders<OrderTracking>.Sort.Descending(p => p.EntryDate);

        //    var result = collection.Find(filter).Sort(sort).Limit(1).FirstOrDefault();
            
        //    if (result == null)
        //    {
        //        trailing = direction == "Long" ? Close[0] - (Close[0] * (Trailing_Stop.Value / 100)) : Close[0] + (Close[0] * (Trailing_Stop.Value / 100));
        //    }
        //    else
        //    {
        //        trailing = result.Trainling_Stop.Value;
        //    }

        //    return trailing;
        //}

        public bool UpdateExits(string id, string status, DateTime date, Double price, int quantity)
        {
            try
            {
                MongoClient client_remote = new MongoClient(Database.GetUriDb());
                IMongoDatabase db_remote = client_remote.GetDatabase(server_name);
                var _collection = db_remote.GetCollection<LiveOpenPositions>(collection);
                var filter_status = "{'_id': ObjectId('" + id + "'), 'open_position': false }";
                var sort = Builders<LiveOpenPositions>.Sort.Descending(p => p.EntryDate);
                var result = _collection.Find(filter_status).Sort(sort).Limit(1).FirstOrDefault();

                //Print("Resultado de Update Exits: " + result);
                //var accountSpecs = resultAccountSpecs.LastOrDefault().AccountSpecs.LastOrDefault();

                if (result.NinjaStatus == "En Proceso" || result.NinjaStatus == "Salida Aceptada")
                {
                    //if (status == "Salida Parcial")
                    //{
                    //    var filter = Builders<LiveOpenPositions>.Filter.Eq(x => x.Id, ObjectId.Parse(id)) &
                    //    Builders<LiveOpenPositions>.Filter.Eq(x => x.OpenPosition, false);
                    //    var update = Builders<LiveOpenPositions>.Update.Set(r => r.NinjaStatus, status);
                    //    _collection.UpdateOne(filter, update);


                    //}
                    //else
                    //{
                        var filter = Builders<LiveOpenPositions>.Filter.Eq(x => x.Id, ObjectId.Parse(id)) &
                        Builders<LiveOpenPositions>.Filter.Eq(x => x.OpenPosition, false);
                        var update = Builders<LiveOpenPositions>.Update.Set(r => r.NinjaStatus, status);
                        //var update = Builders<LiveOpenPositions>.Update.Set(r => r.NinjaStatus, status).Set(r => r.NinjaQuantity, quantity);
                        _collection.UpdateOne(filter, update);
                        var strategy_id = result.StrategyId;
                        var ot_collection = db_remote.GetCollection<OrderTracking>(order_tracking_collection);

                        //var ot_filter = Builders<OrderTracking>.Filter.Eq(x => x.StrategyId, strategy_id);
                        var ot_filter = Builders<OrderTracking>.Filter.Eq(x => x.OpenpositionId, id);
                        var ot_sort = Builders<OrderTracking>.Sort.Descending(p => p.EntryDate);
                        //var result = _collection.Find(filter_status).Sort(sort).Limit(1).FirstOrDefault();
                        var ot_result = ot_collection.Find(ot_filter).Sort(ot_sort).Limit(1).FirstOrDefault();

                        var signal_name = "Entry_" + id + "_" + Account.Name;
                        var exit_name = "Exit_" + id + "_" + Account.Name;
                        //var signal_name = "Entry_" + result.StrategyId + "_" + Account.Name;
                        //var exit_name = "Exit_" + result.StrategyId + "_" + Account.Name;
                        //Print("EXECUTION TIME: " + date);

                        var ot_update = Builders<OrderTracking>.Update.Set(r => r.ExitDate, date)
                            .Set(r => r.Exit, true)
                            .Set(r => r.NinjaStatus, status)
                            .Set(r => r.ExitName, exit_name);

                        ot_collection.UpdateOne(ot_filter, ot_update);
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
                    //}
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

        public LiveOpenPositions GetOpenPosition(string symbol)
        {
            LiveOpenPositions result = new LiveOpenPositions();
            try
            {
                //Print("GET OPEN POSITION SYMBOL " + symbol);
                var instrument_changed = SwitchSymbol(symbol); //symbol = SwitchSymbol(symbol);
                //Print("AFTER SWITCH SYMBOL " + instrument_changed);
                MongoClient client_remote = new MongoClient(Database.GetUriDb());
                IMongoDatabase db_remote = client_remote.GetDatabase(server_name);
                //var _collection = db_remote.GetCollection<LiveOpenPositions>(collection);

                IMongoCollection<LiveOpenPositions> _collection = db_remote.GetCollection<LiveOpenPositions>(collection);
                var builder = Builders<LiveOpenPositions>.Filter;
                var filter = builder.Eq(i => i.Symbol, instrument_changed) & builder.Eq(i => i.OpenPosition, true) & builder.Eq(i => i.NinjaStatus, "Pendiente");
                var sort = Builders<LiveOpenPositions>.Sort.Descending(p => p.EntryDate);
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

        //public OrderTracking GetExitPosition(string symbol)
        //{
        //    LiveOpenPositions result = new LiveOpenPositions();
        //    OrderTracking order_t = new OrderTracking();
        //    try
        //    {
        //        //symbol = SwitchSymbol(symbol);
        //        var instrument_changed = SwitchSymbol(symbol);

        //        MongoClient client_remote = new MongoClient(Database.GetUriDb());
        //        IMongoDatabase db_remote = client_remote.GetDatabase(server_name);
        //        //var _collection = db_remote.GetCollection<LiveOpenPositions>(collection);

        //        IMongoCollection<LiveOpenPositions> _collection = db_remote.GetCollection<LiveOpenPositions>(collection);
        //        var builder = Builders<LiveOpenPositions>.Filter;
        //        var filter = builder.Eq(i => i.Symbol, instrument_changed) & builder.Eq(i => i.OpenPosition, false) & builder.Eq(i => i.NinjaStatus, "En Proceso");
        //        var sort = Builders<LiveOpenPositions>.Sort.Descending(p => p.EntryDate);
        //        result = _collection.Find(filter).Sort(sort).Limit(1).FirstOrDefault();

        //        if(result != null && result.OpenPosition == false) 
        //        {
        //            var _id = result.Id;
        //            IMongoCollection<OrderTracking> _collection_t = db_remote.GetCollection<OrderTracking>(collection);
        //            var builder_t = Builders<OrderTracking>.Filter;
        //            var filter_t = builder_t.Eq(i => i.Id, _id) & builder_t.Eq(i => i.Symbol, instrument_changed) & builder_t.Eq(i => i.NinjaStatus, "En Proceso");
        //            var sort_t = Builders<OrderTracking>.Sort.Descending(p => p.EntryDate);
        //            order_t = _collection_t.Find(filter_t).Sort(sort_t).Limit(1).FirstOrDefault();
        //        }

        //        //if (result != null)
        //        //{
        //        //    Print(result);
        //        //    Print(result.Symbol);
        //        //    Print(result.Direction);
        //        //    Print(result.Group);
        //        //}
        //    }
        //    catch (Exception ex)
        //    {
        //        Print("ERROR GET EXIT POSITION " + ex.Message);
        //    }

        //    return order_t;
        //}

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

                IMongoCollection<LiveOpenPositions> _collection = db_remote.GetCollection<LiveOpenPositions>(collection);
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
            catch (Exception ex)
            {
                Print("ERROR GET EXIT POSITION " + ex.Message);
            }

            return result;
        }

        public LiveOpenPositions GetExitPositionPen(string symbol)
        {
            LiveOpenPositions result = new LiveOpenPositions();
            try
            {
                //symbol = SwitchSymbol(symbol);
                var instrument_changed = SwitchSymbol(symbol);

                MongoClient client_remote = new MongoClient(Database.GetUriDb());
                IMongoDatabase db_remote = client_remote.GetDatabase(server_name);
                //var _collection = db_remote.GetCollection<LiveOpenPositions>(collection);

                IMongoCollection<LiveOpenPositions> _collection = db_remote.GetCollection<LiveOpenPositions>(collection);
                var builder = Builders<LiveOpenPositions>.Filter;
                var filter = builder.Eq(i => i.Symbol, instrument_changed) & builder.Eq(i => i.OpenPosition, false) & (builder.Eq(i => i.NinjaStatus, "En Proceso") | builder.Eq(i => i.NinjaStatus, "Salida Aceptada"));
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
            catch (Exception ex)
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

        public List<LiveOpenPositions> ReorderDB()
        {
            var instrument = InstrumentName;
            var instrument_changed = SwitchSymbol(instrument);
            //Print("REORDER INSTRUMENT NAME 1" + instrument);
            List<LiveOpenPositions> result = new List<LiveOpenPositions>();
            try
            {
                MongoClient client_remote = new MongoClient(Database.GetUriDb());
                IMongoDatabase db_remote = client_remote.GetDatabase(server_name);
                var _collection = db_remote.GetCollection<LiveOpenPositions>(collection);

                var filter_status = "{'symbol': '" + instrument_changed + "', nt_status: {'$in':['Entrada Aceptada', 'Salida Aceptada'] } }";
                var sort = Builders<LiveOpenPositions>.Sort.Descending(p => p.EntryDate);
                result = _collection.Find(filter_status).ToList();

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

        public List<OrderTracking> GetStrategiesInProcess()
        {
            List<OrderTracking> result = new List<OrderTracking>();
            var instrument_changed = SwitchSymbol(InstrumentName);

            try
            {
                MongoClient client_remote = new MongoClient(Database.GetUriDb());
                IMongoDatabase db_remote = client_remote.GetDatabase(server_name);
                var _collection = db_remote.GetCollection<OrderTracking>(order_tracking_collection);
                //MongoClient client_remote = new MongoClient(Database.GetUriDb());
                //IMongoDatabase db_remote = client_remote.GetDatabase(server_name);
                //var _collection = db_remote.GetCollection<LiveOpenPositions>(collection);
                //Print("RECUPERANDO INSTRUMENT NAME " + instrument_changed);
                var filter_status = "{'symbol': '" + instrument_changed + "', nt_status: 'En Proceso'}";
                //var filter_status = "{'symbol': '" + instrument_changed + "', nt_status: 'En Proceso'}";

                var sort = Builders<OrderTracking>.Sort.Descending(p => p.EntryDate);
                result = _collection.Find(filter_status).ToList();
            }
            catch(Exception ex)
            {
                Print("ERROR GET STRATEGIES IN PROCESS" + ex.Message);
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
                foreach (var item in ordersInProcess.ToList())
                {
                    //Print("Order In Proccess ID " + item.OpenpositionId.ToString());
                    //var accountSpecs = item.AccountSpecs.Where(a => a.IbId == Account.Name).Last();
                    var signal_name = "Entry_" + item.OpenpositionId.ToString() + "_" + Account.Name;
                    
                    if (Time[0] >= item.EntryDate)
                    {
                        //Print("Time0: " + Time[0] + " EntryDate: " + item.EntryDate);
                        //Print("AQUI PRRO");
                        if (item.Direction == "Long")
                        {
                            EnterLong(Series, item.NinjaQuantity, signal_name);
                        }
                        else if (item.Direction == "Short")
                        {
                            //Print("ENTRADA SHORT");
                            //Print(Series);
                            //Print(item.NinjaQuantity);
                            //Print(signal_name);
                            try
                            {
                                EnterShort(Series, item.NinjaQuantity, signal_name);
                            }
                            catch(Exception ex)
                            {
                                Print("Se ha producido un error: " + ex.Message);
                                // Realiza acciones adicionales según sea necesario
                            }
                        }

                        ordersInProcess.Remove(item);
                    }
                }
            }
        }

        public int GetPositionSize(MasterInstrument mInstrument, double buyPrice, double StopLossPrice, double riskLevel, double contract_size)
        {
            //double Capital = MyAccount.Get(AccountItem.NetLiquidation, MyAccount.Denomination);
            //double pz;
            //pz = (Capital * (riskLevel)) / ((buyPrice - (StopLossPrice)) * contract_size);
            //pz = pz < 1 ? 1 : pz;
            //return (int)pz;
            return 6;
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

        //#region TrailingStopConditions

        //public bool UpdateTrailing(string id, double? trailing)
        //{
        //    MongoClient client_remote = new MongoClient(Database.GetUriDb());
        //    IMongoDatabase db_remote = client_remote.GetDatabase(server_name);
        //    var collection = db_remote.GetCollection<OrderTracking>(order_tracking_collection);

        //    var filter = Builders<OrderTracking>.Filter.Eq(x => x.OpenpositionId, id);
        //    //var update = Builders<LiveOpenPositions>.Update.Set(r => r.NinjaStatus, status).Set(r => r.NinjaQuantity, quantity);
        //    var update = Builders<OrderTracking>.Update.Set(r => r.Trainling_Stop, trailing);
        //    var resp = collection.UpdateOne(filter, update);

        //    return resp.ModifiedCount > 0;
        //}

        //public bool TrailingStopLongCondition(int CurrentSecondarySeries, int quantity, string exit_name, string signal_name, double? StopLossPercent, string id)
        //{
        //    // Si el precio baja al pagar y encender y es menor que el stop sales directo
        //    // 0.02

        //    //100 ¿? 98
        //    //102 ¿¿ 98.08
        //    //Print("TRAILING STOP: " + StopLossPercent);

        //    //guardar el stoploss actualizado 
        //    // cuando se apague y se encienda, comparar el stoploss guardado con el nuevo si el guardado es mayor que el nuevo no actualizar en base de datos
        //    // verificar si el stoploss baja y choca el trailing entonces alli salir.

        //    double trailing = Close[0] - (Close[0] * (StopLossPercent.Value / 100)); //trailing mas actual.

        //    double? trailing_db = GetTrailingStop(id, "Long"); // trailing base de datos.
        //    Print("Coles[1]: " + Close[1]);
        //    Print("Coles[0]: " + Close[0]);
        //    var col1 = Close[1];
        //    var col0 = Close[0];
        //    if (trailing_db == null || trailing > trailing_db)
        //    {
        //        var update_result = UpdateTrailing(id, trailing);

        //        if (update_result && trailing > Close[1])
        //        {
        //            if (State == State.Realtime)
        //            {
        //                //positionSize
        //                ExitLong(CurrentSecondarySeries, quantity, exit_name, signal_name);
        //            }
        //            return true;
        //        }
        //        else if (trailing_db > Close[0])
        //        {
        //            if (State == State.Realtime)
        //            {
        //                //positionSize
        //                ExitLong(CurrentSecondarySeries, quantity, exit_name, signal_name);
        //            }
        //            return true;
        //        }
        //    }
        //    else
        //    {
        //        if (trailing_db > Close[0])
        //        {
        //            if (State == State.Realtime)
        //            {
        //                //positionSize
        //                ExitLong(CurrentSecondarySeries, quantity, exit_name, signal_name);
        //            }
        //            return true;
        //        }
        //    }

        //    return false;


        //    //if (trailing_db != null && trailing_db != 0) 
        //    //{
        //    //    //Validaciones
        //    //    if (trailing > trailing_db)
        //    //    { // Si el trailing nuevo es mayor al almacenado actualizamos en base de datos.
        //    //        var update_result = UpdateTrailing(id, trailing);

        //    //        if (update_result)
        //    //        {
        //    //            if (trailing > Close[0])
        //    //            {
        //    //                return trailing;
        //    //            }
        //    //        }
        //    //    }
        //    //    else
        //    //        return 0;
        //    //}
        //    //else 
        //    //{
        //    //    var update_result = UpdateTrailing(id, trailing);

        //    //    if (update_result)
        //    //    {
        //    //        if (trailing > Close[0])
        //    //        {
        //    //            return trailing;
        //    //        }
        //    //    }
        //    //    else
        //    //    {
        //    //        return 0;
        //    //    }
        //    //}
        //}

        //public bool TrailingStopShortCondition(int CurrentSecondarySeries, int quantity, string exit_name, string signal_name, double? StopLossPercent, string id)
        //{
        //    double trailing = Close[0] + (Close[0] * (StopLossPercent.Value / 100)); //trailing mas actual.

        //    double? trailing_db = GetTrailingStop(id, "Short"); // trailing base de datos.
        //    Print("Coles[1]: " + Close[1]);
        //    Print("Coles[0]: " + Close[0]);
        //    var col1 = Close[1];
        //    var col0 = Close[0];

        //    if (trailing_db == null || trailing < trailing_db)
        //    {
        //        var update_result = UpdateTrailing(id, trailing);

        //        if (update_result && trailing < Close[0])
        //        {
        //            if (State == State.Realtime)
        //            {
        //                //positionSize
        //                if (State == State.Realtime)
        //                {
        //                    //positionSize
        //                    ExitShort(CurrentSecondarySeries, quantity, exit_name, signal_name);
        //                }
        //            }
        //            return true;
        //        }
        //        else if (trailing_db < Close[0])
        //        {
        //            if (State == State.Realtime)
        //            {
        //                //positionSize
        //                ExitShort(CurrentSecondarySeries, quantity, exit_name, signal_name);
        //            }
        //            return true;
        //        }
        //    }
        //    else
        //    {
        //        if (trailing_db < Close[0])
        //        {
        //            if (State == State.Realtime)
        //            {
        //                //positionSize
        //                ExitShort(CurrentSecondarySeries, quantity, exit_name, signal_name);
        //            }
        //            return true;
        //        }
        //    }

        //    return false;
        //}

        //#endregion

        public class OrderTracking
        {
            [JsonIgnore]
            public ObjectId Id { get; set; }
            [BsonElement("signal_name")]
            public String SignalName { get; set; }
            [BsonElement("exit_name")]
            public String ExitName { get; set; }
            [BsonElement("strategy_id")]
            public String StrategyId { get; set; }
            [BsonElement("direction")]
            public String Direction { get; set; }
            [BsonElement("symbol")]
            public String Symbol { get; set; }
            [BsonElement("group")]
            public String Group { get; set; }
            [BsonElement("quantity")]
            public Int32 Quantity { get; set; }
            [BsonElement("entry_date")]
            public DateTime EntryDate { get; set; }
            [BsonElement("exit_date")]
            public DateTime? ExitDate { get; set; }
            [BsonElement("nt_status")]
            public String NinjaStatus { get; set; }
            [BsonElement("ninja_quantity")]
            public Int32 NinjaQuantity { get; set; }
            [BsonElement("entry")]
            public bool? Entry { get; set; }
            [BsonElement("exit")]
            public bool? Exit { get; set; }
            [BsonElement("open_position_id")]
            public String OpenpositionId { get; set; }
            //[BsonElement("trailing_stop")]
            //public Double? Trainling_Stop { get; set; }
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
