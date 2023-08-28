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
	public class EnvisionReplicatorRealtime : GenericStrategyServices
	{
		private Account MyAccount;
		private static DatabaseJson Database = new DatabaseJson();
		private static MongoClient client_remote = new MongoClient(Database.GetUriDb());
        private static IMongoDatabase db_remote = client_remote.GetDatabase("server_skynet");
		private Instruments_AUX instrumentsAux;
		int cantidad = 1;
		private LiveOpenPositions liveOpenPositions;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				//Defaults
				StartDefault();
				Name = "EnvisionReplicatorRealtime";
				Description = @"Generic strategy";
			}
			else if (State == State.Configure)
			{
				if (IsEnabled)
				{
					StartConfigure();
					TraceOrders = true;
					MyAccount = Account;
					AddDataSeries(BarsPeriodType.Second, 30);
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
					//Print("STEP 1");
					//var liveOpenPositions = GetOpenPosition(InstrumentName);

					var listStrategiesReorder = ReorderDB(); // Salida Aceptada o Entrada Aceptada.

					if (listStrategiesReorder != null)
					{
						//Print("Reorder != NULL && Count: " + listStrategiesReorder.Count());
						foreach (var r in listStrategiesReorder)
						{
							//if (liveOpenPositions != null && (String.IsNullOrEmpty(liveOpenPositions.NinjaStatus)))
							//Print("Que tenemos en Open Position: " + r.OpenPosition);
							if (r.OpenPosition && r.Type == "news_oscillator")
							{
								//Print("Dentro de la Opcion Open Position");
								var id = r.Id.ToString();
								var signal_name = "Entry_" + id + "_" + Account.Name;
								//Print("Mi Signal Name es: " + signal_name);

								positionSize = GetPositionSize(BarsArray[CurrentMainSeries].Instrument.MasterInstrument, Closes[CurrentMainSeries][0], r.StopLossPrice, r.Risk, r.ContractSize);
								//positionSize = GetPositionSize(BarsArray[CurrentMainSeries].Instrument.MasterInstrument, Closes[CurrentMainSeries][0], r.StopLossPrice);

								if (r.Direction == "Long")
								{
									StrategyEnterLong(CurrentSecondarySeries, positionSize, signal_name);
									//StrategyEnterLong(CurrentMainSeries, r.Quantity, signal_name);
								}
								if (r.Direction == "Short")
                                {
									StrategyEnterShort(CurrentSecondarySeries, positionSize, signal_name);
									//StrategyEnterShort(CurrentMainSeries, r.Quantity, signal_name);
                                }
							}
							else if (!r.OpenPosition && r.Type == "news_oscillator")
							{
								var id = r.Id.ToString();
								var signal_name = "Entry_" + id + "_" + Account.Name;
								var exit_name = "Exit_" + id + "_" + Account.Name;

								if (r.Direction == "Long")
									StrategyExitLong(CurrentSecondarySeries, r.NinjaQuantity, exit_name, signal_name);
								else if (r.Direction == "Short")
									StrategyExitShort(CurrentSecondarySeries, r.NinjaQuantity, exit_name, signal_name);
							}
							
						}

					}
				}

				return;
				//instrumentsAux = new Instruments_AUX();
				//instrumentsAux.Group = "Futuros";
				//if (cantidad == 1)
				//{
				//	Print("Cantidad");
				//	GetInstruments(instrumentsAux);
				//	cantidad++;
				//}
				//Print("Holas");
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

			//Print("GET POSITIONS ENTRY");
			var StrategyEntry = GetOpenPosition(InstrumentName);
			//Print("GET POSITIONS EXIT");
			var StrategyExit = GetExitPosition(InstrumentName);
			//Print("END GET POSITION");
			//Exits
			if (StrategyExit != null)
			{
				var id = StrategyExit.Id.ToString();
				var signal_name = "Entry_" + id + "_" + Account.Name;
				var exit_name = "Exit_" + id + "_" + Account.Name;
				//Print("DIRECTION EXIT: " + StrategyExit.Direction);
				//Print("ES UNA SALIDA");
				if (Position.MarketPosition != MarketPosition.Flat)
				{
					if (StrategyExit.Direction == "Long")
					{
						StopLossLongCondition(CurrentSecondarySeries, StrategyExit.StopLossPrice, StrategyExit.NinjaQuantity, exit_name, signal_name);
						ProfitTargetLongCondition(CurrentSecondarySeries, StrategyExit.StopLossPrice, StrategyExit.NinjaQuantity, exit_name, signal_name);
						//StrategyExitLong(CurrentMainSeries, StrategyExit.Quantity, exit_name, signal_name);
					}
					else if (StrategyExit.Direction == "Short")
					{

						StopLossShortCondition(CurrentSecondarySeries, StrategyExit.StopLossPrice, StrategyExit.NinjaQuantity, exit_name, signal_name);
						ProfitTargetShortCondition(CurrentSecondarySeries, StrategyExit.StopLossPrice, StrategyExit.NinjaQuantity, exit_name, signal_name);
						//StrategyExitShort(CurrentMainSeries, StrategyExit.Quantity, exit_name, signal_name);
					}
				}
			}

			var StrategyExitt = GetExitPositionPen(InstrumentName);

			//Entries
			if (StrategyEntry != null && StrategyExitt == null)
			{
				var id = StrategyEntry.Id.ToString();
				var signal_name = "Entry_" + id + "_" + Account.Name;
				//Print("DIRECTION ENTRY: " + StrategyEntry.Direction);

				if (Position.MarketPosition == MarketPosition.Flat)
				{

					if (StrategyEntry.Direction == "Long")
					{
						//positionSize = GetPositionSize(BarsArray[CurrentMainSeries].Instrument.MasterInstrument, Closes[CurrentMainSeries][0]);
						positionSize = GetPositionSize(BarsArray[CurrentMainSeries].Instrument.MasterInstrument, Closes[CurrentMainSeries][0], StrategyEntry.StopLossPrice, StrategyEntry.Risk, StrategyEntry.ContractSize);
						StrategyEnterLong(CurrentSecondarySeries, positionSize, signal_name);
						//StrategyEnterLong(CurrentMainSeries, StrategyEntry.Quantity, signal_name);
					}
					else if (StrategyEntry.Direction == "Short")
					{
						positionSize = GetPositionSize(BarsArray[CurrentMainSeries].Instrument.MasterInstrument, Closes[CurrentMainSeries][0], StrategyEntry.StopLossPrice, StrategyEntry.Risk, StrategyEntry.ContractSize);
						StrategyEnterShort(CurrentSecondarySeries, positionSize, signal_name);
						//StrategyEnterShort(CurrentMainSeries, StrategyEntry.Quantity, signal_name);
					}
				}
			}
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
		
		private bool GetInstruments(Instruments_AUX instrument)
        {
			//Add your custom strategy logic here.
			IMongoCollection<Instruments_AUX> _collection = db_remote.GetCollection<Instruments_AUX>("Instrument");
            var builder = Builders<Instruments_AUX>.Filter;
            var filter = builder.Eq(i => i.Group, instrument.Group);
            var fields = Builders<Instruments_AUX>.Projection.Include(p => p.Symbol).Include(p => p.Name).Include(p => p.Group);
			var result = _collection.Find(filter).Project<Instruments_AUX>(fields).ToList();

            //var result = _collection.Find(filter).Project<Instruments_AUX>(fields).ToList();
			//Print(result);
			//foreach (var item in result)
			//{
			//	Print(item.Symbol);
			//Print(item.Name);
			//	Print(item.Group);
			//}

            return false;
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
	}
}
