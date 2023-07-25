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
using Newtonsoft.Json;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
    public class GenericBacktestingStrategy : Strategy
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
        public double Capital;

        //Log Json
        public bool LogJson;
        public string path;
        private Account MyAccount;
        public string pathDirectory;
        DatabaseJson Database = new DatabaseJson();

        //Data Series index
        public int CurrentMainSeries, CurrentSecondarySeries, CurrentTertiarySeries;

        //Helpers
        public int positionSize;

        public bool StopLossSwitch, ProfitTargetSwitch, TrailingStopSwitch;
        public ExecutionSwitch executionSwitch;
        public OrderSwitch orderSwitch;

        //TelegramMessage
        TelegramBots Telegram = new TelegramBots();
        public string AlertMessageTelegram, ListTelegram, TemplateTelegramMessage;
        private bool NotifyEndStrategy = false;
        [XmlIgnore]
        public Dictionary<string, string> ParametersList = new Dictionary<string, string>();


        //Database
        private static DatabaseJson DB = new DatabaseJson();
        private static MongoClient client_remote = new MongoClient(DB.GetUriDb());
        private static IMongoDatabase db_remote = client_remote.GetDatabase("server_skynet");

        //Dates
        private DateTime toDate;
        private List<String> listEndDates;

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
            //Slippage = 1;
            StartBehavior = StartBehavior.WaitUntilFlat;
            TimeInForce = TimeInForce.Gtc;
            TraceOrders = false;
            RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
            StopTargetHandling = StopTargetHandling.PerEntryExecution;
            BarsRequiredToTrade = 1;
            IsInstantiatedOnEachOptimizationIteration = true;
            IncludeTradeHistoryInBacktest = true;
            CurrentMainSeries = 0;
            CurrentSecondarySeries = 1;
            CurrentTertiarySeries = 2;
            SaveTrade = false;
            StopLossSwitch = true;
            executionSwitch = ExecutionSwitch.Common;
            orderSwitch = OrderSwitch.Common;
        }

        public void StartConfigure()
        {
            MyAccount = Account;
            Capital = 500000;
            riskLevel = 1;
            InstrumentName = System.Text.RegularExpressions.Regex.Match(Instrument.MasterInstrument.Name, @"^([\w\-]+)").ToString();

            string[] separator = new string[] { "\\" };
            var path = Directory.GetCurrentDirectory();
            var pathSplit = path.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            pathSplit[pathSplit.Count() - 1] = "files\\" + MyAccount.Name + "\\" + InstrumentName;
            pathDirectory = String.Join("\\", pathSplit);

            listEndDates = new List<String>();
        }

        /// <summary>
        /// Carga en una lista, los ultimos dias de la fecha final (End date) de la estrategia.
        /// </summary>
        public void DataLoaded() 
        {
            toDate = To.Date;

            //Todate Config
            if (toDate.DayOfWeek == DayOfWeek.Saturday)
                toDate = To.Date.AddDays(-1);
            else if (toDate.DayOfWeek == DayOfWeek.Sunday)
                toDate = toDate.AddDays(-2);

            // margen de error de fechas finales 1 semana
            listEndDates.Add(toDate.ToShortDateString());
            listEndDates.Add(toDate.AddDays(-1).ToShortDateString());
            listEndDates.Add(toDate.AddDays(-2).ToShortDateString());
            listEndDates.Add(toDate.AddDays(-3).ToShortDateString());
            listEndDates.Add(toDate.AddDays(-4).ToShortDateString());
        }

        public void SearchStrategyId()
        {
            StrategyId = Database.GetIdStrategy(StrategyVersionName, Temporality, Direction, InstrumentName, "server_skynet", "StrategySpecs");
        }

        /// <summary>
        /// Delete all trades by id if SaveTrade == True
        /// </summary>
        /// <param name="strategy_id"></param>
        /// <param name="SaveTrade"></param>
        /// <returns></returns>
        public string DeleteTrades(string strategy_id, bool SaveTrade) 
        {
             return Database.DeleteTrades(strategy_id, SaveTrade);
        }

        /// <summary>
        /// Cambia el status de la estrategia a Pendiente por aprobar, si Save Trades esta en True.!
        /// </summary>
        /// <returns></returns>
        public bool ChangeStatus() 
        {
            if (SaveTrade) 
            {
                try 
                {
                    var _collection = db_remote.GetCollection<BsonDocument>("StrategySpecs");
                    var builder = Builders<BsonDocument>.Filter;
                    var filter = builder.Eq("_id", ObjectId.Parse(StrategyId)) & builder.Eq("ninja_trader", "BT en proceso");
                    var fields = Builders<BsonDocument>.Projection.Include("ninja_trader");
                    var result = _collection.Find(filter).Project<BsonDocument>(fields).FirstOrDefault();


                    var update = Builders<BsonDocument>.Update.Set("ninja_trader", "Pendiente por aprobar");
                    _collection.UpdateOne(filter, update);
                    return true;
                }
                catch(MongoException ex) 
                {
                    Print("Error: " + ex.Message);
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Envia una notificación via telegram, cuando la estrategia esta a punto de finalizar, si Save Trades esta en True..
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public string TelegramBacktestStrategy(DateTime time)
        {
            if (SaveTrade)
            {
                if (listEndDates.Contains(time.ToShortDateString()) && NotifyEndStrategy == false) 
                {
                    /*Actualizaron los trades*/
                    var _collection = db_remote.GetCollection<BsonDocument>("StrategySpecs");
                    var builder = Builders<BsonDocument>.Filter;
                    var filter = builder.Eq("_id", ObjectId.Parse(StrategyId));
                    var fields = Builders<BsonDocument>.Projection.Include("ninja_trader");
                    var result = _collection.Find(filter).Project<BsonDocument>(fields).FirstOrDefault();
                    //Print("resultado: " + result["ninja_trader"]);

                    if (result["ninja_trader"] == "Pendiente por aprobar") 
                    {
                        string instrument = BarsArray[0].Instrument.FullName;
                        AlertMessageTelegram = TemplateTelegramMessage;
                        ParametersList.Add("Status", "Pendiente por aprobar");
                        ListTelegram = "\n\nParametros\n\n" + string.Join("\n", ParametersList.Select(x => x.Key + ": " + x.Value));

                        string TelegramMessage = String.Join(
                           "\n",
                           "Incubation " + Account.Name,
                           "Estrategia: " + this.Name,
                           "Instrumento: " + instrument,
                           "Temporalidad: " + Temporality,
                           "Status: " + "Pendiente por aprobar"
                       );
                        Telegram.SendMessageInChannelEWMStrategies(TelegramMessage);
                        NotifyEndStrategy = true;
                    }
                }
            }
            return "Ok";
        }

        #endregion

        #region StopLossConditions
        public void StopLossLongCondition(int Series)
        {
            if (StopLoss != null)
            {
                if (Close[0] <= StopLoss.Price)
                {
                    if (State == State.Historical)
                    {
                        ExitLong(Series, StopLoss.Quantity, StopLoss.Name, StopLoss.SignalName);
                    }

                }
            }
        }

        public void StopLossShortCondition(int Series)
        {
            if (StopLoss != null)
            {
                if (Close[0] >= StopLoss.Price)
                {
                    if (State == State.Historical)
                    {
                        ExitShort(Series, StopLoss.Quantity, StopLoss.Name, StopLoss.SignalName);
                    }

                }
            }
        }
        #endregion

        #region ProfitTargetConditions
        public void ProfitTargetLongCondition(int Series)
        {
            if (ProfitTarget != null)
            {
                if (Close[0] >= ProfitTarget.Price)
                {
                    if (State == State.Historical)
                    {
                        ExitLong(Series, ProfitTarget.Quantity, ProfitTarget.Name, ProfitTarget.SignalName);
                    }

                }
            }
        }

        public void ProfitTargetShortCondition(int Series)
        {
            if (ProfitTarget != null)
            {
                if (Close[0] <= ProfitTarget.Price)
                {
                    if (State == State.Historical)
                    {
                        ExitShort(Series, ProfitTarget.Quantity, ProfitTarget.Name, ProfitTarget.SignalName);
                    }

                }
            }
        }
        #endregion

        #region TrailingStopConditions
        public void TrailingStopLongCondition()
        {
            if (StopLoss != null)
            {
                double trailing = Close[0] - (Close[0] * (StopLossPercent / 100));
                if (trailing > StopLoss.Price)
                {
                    StopLoss.Date = Time[0];
                    StopLoss.Price = trailing;
                    var orderSave = Database.UpdatePriceOrdersExit(pathDirectory, this.Name, StopLoss, null);
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
                    StopLoss.Date = Time[0];
                    StopLoss.Price = trailing;
                    var orderSave = Database.UpdatePriceOrdersExit(pathDirectory, this.Name, StopLoss, null);
                }
            }
        }
        #endregion

        #region OnOrderUpdate
        protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string nativeError)
        {
            switch (orderSwitch)
            {
                case OrderSwitch.Common:
                    OnOrderUpdateCommon(order, limitPrice, stopPrice, quantity, filled, averageFillPrice, orderState, time, error, nativeError);
                    break;
            }
            
        }

        private void OnOrderUpdateCommon(Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string nativeError)
        {
            if (order.Name == EnterLongName || order.Name == EnterShortName)
            {
                entryOrder = order;

                if (order.OrderState == OrderState.Cancelled && order.Filled == 0)
                    entryOrder = null;
            }
            else
            {
                exitOrder = order;

                if (order.OrderState == OrderState.Cancelled && order.Filled == 0)
                    exitOrder = null;
            }
        }
        #endregion

        #region OnExecutionUpdate
        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            switch (executionSwitch)
            {
                case ExecutionSwitch.Common:
                    OnExecutionUpdateCommon(execution, executionId, price, quantity, marketPosition, orderId, time);
                    break;
            }

        }

        private void OnExecutionUpdateCommon(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            if (entryOrder != null && entryOrder == execution.Order)
            {
                if (execution.Order.OrderState == OrderState.Filled)
                {
                    if (State == State.Historical)
                    {
                        orderEntryObj = new OrderEntryF()
                        {
                            Name = execution.Order.Name,
                            InstrumentName = InstrumentName,
                            Date = execution.Time,
                            Price = execution.Order.AverageFillPrice,
                            Status = new List<string> { "Pendiente" },
                            OrderType = execution.Order.IsLong ? "Long" : (execution.Order.IsShort ? "Short" : null),
                            Quantity = new List<int> { execution.Order.Quantity },
                            EntryRollover = false,
                            AccountName = Account.Name,
                            IdStrategy = StrategyId
                        };
                        var orderSave = Database.AddNewOrderEntryObject(pathDirectory, this.Name, orderEntryObj);

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
                        }
                        else
                        {
                            ProfitTarget = null;
                        }
                        
                        orderSave = Database.UpdatePriceOrdersExit(pathDirectory, this.Name, StopLoss, ProfitTarget);
                    }
                    positionSize = execution.Order.Quantity;
                    entryOrder = null;
                }

            }

            if (exitOrder != null && exitOrder == execution.Order)
            {
                if (execution.Order.OrderState == OrderState.Filled)
                {
                    if (State == State.Historical)
                    {
                        double Commission = 0;
                        double MaePercent = 0;
                        double ProfitCurrency = 0;

                        if (SystemPerformance.AllTrades.Count > 0)
                        {
                            Trade lastTrade = SystemPerformance.AllTrades[SystemPerformance.AllTrades.Count - 1];
                            IEnumerable<Trade> trades = SystemPerformance.AllTrades.Where(k => k.Exit.Order.OrderId == lastTrade.Exit.Order.OrderId);

                            if (trades != null)
                            {
                                foreach (var trade in trades)
                                {
                                    Commission += trade.Commission;
                                    ProfitCurrency += trade.ProfitCurrency;
                                }
                                MaePercent += Math.Round(lastTrade.MaePercent, 4);
                            }
                        }

                        orderExitObj = new OrderExitFile()
                        {
                            Name = execution.Order.Name,
                            SignalName = execution.Order.FromEntrySignal,
                            Date = execution.Time,
                            Price = execution.Order.AverageFillPrice,
                            OrderType = null,
                            Quantity = execution.Order.Quantity,
                            ExitRollover = false,
                            Commission = Commission,
                            MaePercent = MaePercent,
                            ProfitCurrency = ProfitCurrency,
                            //                            ExtraParameters = new BsonDocument(),
                        };
                        Capital += ProfitCurrency;

                        /*Evitar que insertte el Exit on session close*/
                        if(execution.Order.Name == "Exit on session close") 
                        {
                            return;
                        }

                        var orderSave = Database.AddNewOrderExitObject(pathDirectory, this.Name, orderExitObj);

                        //Only Backtesting
                        if (SaveTrade)
                        {

                            if (orderEntryObj.OrdersExit == null)
                                orderEntryObj.OrdersExit = new List<OrderExitFile>();

                            orderEntryObj.OrdersExit.Add(orderExitObj);

                            orderEntryObj.Quantity.Add(orderEntryObj.Quantity.Last() - orderExitObj.Quantity);

                            if (orderEntryObj.Quantity.Last() == 0)
                                orderEntryObj.Status.Add("Completado");

                            orderSave = Database.SaveMongoOrder(orderEntryObj, "server_ninjatrader", "Trade");
                        }

                        orderEntryObj = null;
                        orderExitObj = null;
                        StopLoss = null;
                        ProfitTarget = null;
                    }
                    exitOrder = null;
                }

            }
        }

        #endregion

        #region Positions Size
        public int GetPositionSize(MasterInstrument mInstrument, double buyPrice, double Capital)
        {
            //double pointValue;
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
                        positionSize = Math.Floor(Capital / buyPrice);
                    }

                    positionSize = positionSize < 1 ? 1 : positionSize;
                    break;
            }

            return (int)positionSize;
        }
        #endregion

        #region Properties

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Save Trades", Order = 4, GroupName = "Parameters")]
        public bool SaveTrade
        { get; set; }
        #endregion

        #region Enums

        public enum ExecutionSwitch
        {
            Common
        }

        public enum OrderSwitch
        {
            Common
        }
        #endregion


    }

}
