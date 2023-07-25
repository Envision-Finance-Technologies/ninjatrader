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
using NinjaTraderServices;
using MongoDB.Bson.Serialization;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies.Realtime
{
	public class Trailing_stop_realtime : GenericNormalStrategyServices
	{
		private TrailinStopStrategy.Direction direction;
		private Account MyAccount;
		private static DatabaseJson Database = new DatabaseJson();
		private static MongoClient client_remote = new MongoClient(Database.GetUriDb());
        private static IMongoDatabase db_remote = client_remote.GetDatabase("server_skynet");
		private Instruments_AUX instrumentsAux;
		int cantidad = 1;
		private LiveOpenPositions liveOpenPositions;
		private string StrategyID;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				//Defaults
				StartDefault();
				Name = "Trailing_stop_realtime";
				Description = @"Generic strategy";
			}
			else if (State == State.Configure)
			{
				if (IsEnabled)
				{
					StartConfigure();
					Direction = direction.ToString();
					TraceOrders = true;
					MyAccount = Account;
					Trailing_Stop = tsValue * 100;
					StrategyVersionName = CreateStrategyName(InstrumentName);
					var create = CreateTrailinStrategyDocument();

					AddDataSeries(BarsPeriodType.Second, 30);

					EnterLongName = "Trailin_Stop_EnterLong";
					EnterShortName = "Trailin_Stop_EnterShort";
					ExitLongName = "Trailin_Stop_ExitLong";
					ExitShortName = "Trailin_Stop_ExitShort";
				}
			}
			else if (State == State.DataLoaded)
			{
				RegisterCMap();
				liveOpenPositions = new LiveOpenPositions();
			}
		}

		protected override void OnBarUpdate()
		{

			if (CurrentBars[CurrentMainSeries] < 1 && CurrentBars[CurrentSecondarySeries] < 1)
				return;

			
			if (BarsInProgress == 1)
			{
				//Found, Processing, 
				//Print("BarsInProgress: " + BarsInProgress);
				if (State == State.Realtime)
				{
					var StrategiesReorder = ReorderDB();

					//if (listStrategiesReorder != null && listStrategiesReorder.Count != 0)
					if (StrategiesReorder != null)
					{
						//if (liveOpenPositions != null && (String.IsNullOrEmpty(liveOpenPositions.NinjaStatus)))
						//Print("Que tenemos en Open Position: " + r.OpenPosition);
						if (StrategiesReorder.OpenPosition)
						{
							var id = StrategiesReorder.Id.ToString();								
							var signal_name = "Entry_" + id + "_" + Account.Name;
							//Print("Mi Signal Name es: " + signal_name);

							//positionSize = GetPositionSize(BarsArray[CurrentMainSeries].Instrument.MasterInstrument, Closes[CurrentMainSeries][0], Trailing_Stop, r.Risk, r.ContractSize);

							if (Direction == "Long")
							{
								StrategyEnterLong(CurrentSecondarySeries, quantity, signal_name);
								//StrategyEnterLong(CurrentMainSeries, r.Quantity, signal_name);
							}
							if (Direction == "Short")
                            {
								StrategyEnterShort(CurrentSecondarySeries, quantity, signal_name);
								//StrategyEnterShort(CurrentMainSeries, r.Quantity, signal_name);
                            }
						}
						else if (!StrategiesReorder.OpenPosition)
						{
							var id = StrategiesReorder.Id.ToString();
							StrategyID = id;
							var signal_name = "Entry_" + id + "_" + Account.Name;
							var exit_name = "Exit_" + id + "_" + Account.Name;

							if (StrategiesReorder.Direction == "Long")
							{
								var trailin_success = TrailingStopLongCondition(CurrentSecondarySeries, StrategiesReorder.Quantity, exit_name, signal_name, Trailing_Stop, id);
								//StrategyExitLong(CurrentSecondarySeries, r.NinjaQuantity, exit_name, signal_name);
							}
							else if (StrategiesReorder.Direction == "Short")
							{
								var trailin_success = TrailingStopShortCondition(CurrentSecondarySeries, StrategiesReorder.Quantity, exit_name, signal_name, Trailing_Stop, id);
								//StrategyExitShort(CurrentSecondarySeries, r.NinjaQuantity, exit_name, signal_name);
							}
						}
					}
                    //else
                    //{

                    //    if (StrategiesReorder.Direction == "Long")
                    //    {
                    //        var trailin_success = TrailingStopLongCondition(CurrentSecondarySeries, StrategiesReorder.Quantity, exit_name, signal_name, Trailing_Stop, id);
                    //        //StrategyExitLong(CurrentSecondarySeries, r.NinjaQuantity, exit_name, signal_name);
                    //    }
                    //    else if (StrategiesReorder.Direction == "Short")
                    //    {
                    //        var trailin_success = TrailingStopShortCondition(CurrentSecondarySeries, StrategiesReorder.Quantity, exit_name, signal_name, Trailing_Stop, id);
                    //        //StrategyExitShort(CurrentSecondarySeries, r.NinjaQuantity, exit_name, signal_name);
                    //    }
                    //}


                }

				return;
			}

			if (BarsInProgress != CurrentMainSeries)
            {
				return;
            }

			if (State == State.Historical)
			{
				//Print("TRY TO RECOVERY POSITION");
				RecoveryOrdersInProcess(CurrentSecondarySeries);
				return;
			}

			//Entrada.
			//Print("GET POSITIONS ENTRY");
			//var StrategyEntry = GetOpenPosition(InstrumentName);
			//var StrategyExit = GetExitPosition(InstrumentName);
			//var strategy = GetStrategy();
			if (Position.MarketPosition == MarketPosition.Flat && !OrderExit)
            {
				//logica de entrada.
				var signal_name = "Entry_" + StrategyId + "_" + Account.Name;

				if (Direction == "Long")
				{
					StrategyEnterLong(CurrentSecondarySeries, quantity, signal_name);
					//StrategyEnterLong(CurrentMainSeries, r.Quantity, signal_name);
				}
				if (Direction == "Short")
				{
					StrategyEnterShort(CurrentSecondarySeries, quantity, signal_name);
					//StrategyEnterShort(CurrentMainSeries, r.Quantity, signal_name);
				}
			}
            else if(Position.MarketPosition != MarketPosition.Flat)
			{
				var signal_name = "Entry_" + StrategyId + "_" + Account.Name;
				var exit_name = "Exit_" + StrategyId + "_" + Account.Name;

				if (Direction == "Long")
				{
					var trailin_success = TrailingStopLongCondition(CurrentSecondarySeries, quantity, exit_name, signal_name, Trailing_Stop, StrategyId);
				}
				else if (Direction == "Short")
				{
					var trailin_success = TrailingStopShortCondition(CurrentSecondarySeries, quantity, exit_name, signal_name, Trailing_Stop, StrategyId);
				}

			}

			//Exits
		//	if (StrategyExit != null)
		//	{
		//		var id = StrategyExit.Id.ToString();
		//		var signal_name = "Entry_" + id + "_" + Account.Name;
		//		var exit_name = "Exit_" + id + "_" + Account.Name;
		//		//Print("DIRECTION EXIT: " + StrategyExit.Direction);
		//		//Print("ES UNA SALIDA");

		//		if (StrategyExit.Direction == "Long")
		//		{
		//			var trailin_success = TrailingStopLongCondition(CurrentSecondarySeries, StrategyExit.NinjaQuantity, exit_name, signal_name, Trailing_Stop, id);
		//			//StopLossLongCondition(CurrentSecondarySeries, StrategyExit.StopLossPrice, StrategyExit.NinjaQuantity, exit_name, signal_name);
		//			//ProfitTargetLongCondition(CurrentSecondarySeries, StrategyExit.StopLossPrice, StrategyExit.NinjaQuantity, exit_name, signal_name);
		//		}
		//		else if (StrategyExit.Direction == "Short")
		//		{
		//			var trailin_success = TrailingStopShortCondition(CurrentSecondarySeries, StrategyExit.NinjaQuantity, exit_name, signal_name, Trailing_Stop, id);
		//			//StopLossShortCondition(CurrentSecondarySeries, StrategyExit.StopLossPrice, StrategyExit.NinjaQuantity, exit_name, signal_name);
		//			//ProfitTargetShortCondition(CurrentSecondarySeries, StrategyExit.StopLossPrice, StrategyExit.NinjaQuantity, exit_name, signal_name);
		//		}
		//	}
  //          else 
		//	{
		//		var id = StrategyExit.Id.ToString();
		//		//GetStrategy();
		//		//GetTrailingStop();
		//	}

		//	//Entries
		//	if (StrategyEntry != null)
		//	{
		//		var id = StrategyEntry.Id.ToString();
		//		var signal_name = "Entry_" + id + "_" + Account.Name;
		//		//Print("DIRECTION ENTRY: " + StrategyEntry.Direction);

		//		if (StrategyEntry.Direction == "Long")
		//		{
		//			//positionSize = GetPositionSize(BarsArray[CurrentMainSeries].Instrument.MasterInstrument, Closes[CurrentMainSeries][0]);
		//			positionSize = GetPositionSize(BarsArray[CurrentMainSeries].Instrument.MasterInstrument, Closes[CurrentMainSeries][0], StrategyEntry.StopLossPrice, StrategyEntry.Risk, StrategyEntry.ContractSize);
		//			StrategyEnterLong(CurrentSecondarySeries, positionSize, signal_name);
		//			//StrategyEnterLong(CurrentMainSeries, StrategyEntry.Quantity, signal_name);
		//		}
		//		else if (StrategyEntry.Direction == "Short")
		//		{
		//			positionSize = GetPositionSize(BarsArray[CurrentMainSeries].Instrument.MasterInstrument, Closes[CurrentMainSeries][0], StrategyEntry.StopLossPrice, StrategyEntry.Risk, StrategyEntry.ContractSize);
		//			StrategyEnterShort(CurrentSecondarySeries, positionSize, signal_name);
		//			//StrategyEnterShort(CurrentMainSeries, StrategyEntry.Quantity, signal_name);
		//		}
		//	}

		}

		private string CreateStrategyName(string instrument_name)
		{
			var name = "";

			if (Direction == "Long")
				name = "trailin_stop_" + instrument_name + "_" + Direction;
			else if (Direction == "Short")
				name = "trailin_stop_" + instrument_name + "_" + Direction;
			
			return name;
		}

		private void RegisterCMap()
        {
            try
            {
                BsonClassMap.RegisterClassMap<Instruments_AUX>();
            }
            catch (Exception)
            {
            }
        }
			
		public class Instruments_AUX
        {
            [JsonIgnore]
            public ObjectId Id { get; set; }
            [BsonElement("symbol")]
            public String Symbol { get; set; }
            [BsonElement("name")]
            public String Name { get; set; }
            [BsonElement("group")]
            public String Group { get; set; }
        }

		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(Name = "Trailing Stop", Order = 0, GroupName = "Exit Condition")]
		public double tsValue
		{ get; set; }

		[NinjaScriptProperty]
		[Range(0, double.MaxValue)]
		[Display(Name = "Quantity", Order = 0, GroupName = "Parameters")]
		public int quantity
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Bias", Description = "Direction Selection", Order = 1, GroupName = "Parameters")]
		public TrailinStopStrategy.Direction directionStrategy
		{
			get { return direction; }
			set { direction = value; }
		}
	}
}

namespace TrailinStopStrategy
{
	public enum Direction
	{
		Long = 1,
		Short = 2
	}
}
