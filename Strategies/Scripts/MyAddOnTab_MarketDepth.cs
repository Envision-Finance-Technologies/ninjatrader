using NinjaTrader;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NinjaTrader.Custom.Strategies
{
    public class MyAddOnTab_MarketDepth: NTTabPage
    {
        public MarketDepth<MarketDepthRow> marketDepth;
        private MarketDepthLevels levels;

        public List<Double> ListMarketDepthLevelAskPrice;
        public List<long> listMarketDepthLevelAskVolume;
        public List<Double> ListMarketDepthLevelBidPrice;
        public List<long> listMarketDepthLevelBidVolume;
        //public Double[] ListMarketDepthLevelAskPrice;
        #region Variables
        public Double PriceAskL0;
        public Double PriceAskL1;
        public Double PriceAskL2;
        public Double PriceAskL3;
        public Double PriceAskL4;
        public Double PriceBidL0;
        public Double PriceBidL1;
        public Double PriceBidL2;
        public Double PriceBidL3;
        public Double PriceBidL4;
        public Double PriceAskTot;
        public Double PriceBidTot;

        public long VolAskL0;
        public long VolAskL1;
        public long VolAskL2;
        public long VolAskL3;
        public long VolAskL4;
        public long VolBidL0;
        public long VolBidL1;
        public long VolBidL2;
        public long VolBidL3;
        public long VolBidL4;
        public long VolAskTot;
        public long VolBidTot;

        public int Niveles = 0;
        public int? CountMarket = 0;
        #endregion

        //private Instrument instrument;
        public MyAddOnTab_MarketDepth(Instrument instrument)
        {
            // Subscribe to market data. Snapshot data is provided right on subscription
            // Note: "instrument" is a placeholder in this example, you will need to replace          
            // with a valid Instrument object through various methods or properties available depending
            // on the NinjaScript type you are working with (e.g., Bars.Instrument or Instrument.GetInstrument()
            CountMarket = 0;

           //NinjaTrader.Code.Output.Process(string.Format("Instrument: {0}, AQUI {1}",
           //          instrument, "AQUI"), PrintTo.OutputTab1);

            ListMarketDepthLevelAskPrice = new List<double>();
            listMarketDepthLevelAskVolume = new List<long>();
            ListMarketDepthLevelBidPrice = new List<double>();
            listMarketDepthLevelBidVolume = new List<long>();
            //ListMarketDepthLevelAskPrice = new Double[6];

            marketDepth = new MarketDepth<MarketDepthRow>(instrument);
            marketDepth.Update += OnMarketDepth;


        }

        // This method is fired on market depth events and after the snapshot data is updated.
        public void OnMarketDepth(object sender, MarketDepthEventArgs e)
        {
            double starPrice = 0.0;
            
            //NinjaTrader.Code.Output.Process(string.Format("{0}",
            //         "Hello its me you looking for"), PrintTo.OutputTab1);

            var mma = marketDepth.Asks.Take(10);
            var mmb = marketDepth.Bids.Take(10);

            ListMarketDepthLevelAskPrice.Clear();
            listMarketDepthLevelAskVolume.Clear();

            for (int i = 0; i < mma.Count(); i++)
            {
                //NinjaTrader.Code.Output.Process(string.Format("Position: {0} Price: {1} Volume: {2}", i,
                //     marketDepth.Asks[i].Price, marketDepth.Asks[i].Volume), PrintTo.OutputTab1);
                
                ListMarketDepthLevelAskPrice.Add(marketDepth.Asks[i].Price);
                listMarketDepthLevelAskVolume.Add(marketDepth.Asks[i].Volume);
            }

            ListMarketDepthLevelBidPrice.Clear();
            listMarketDepthLevelBidVolume.Clear();

            for (int x = 0; x < mmb.Count(); x++)
            {
                //NinjaTrader.Code.Output.Process(string.Format("Position: {0} Price: {1} Volume: {2}", x,
                //     marketDepth.Bids[x].Price, marketDepth.Bids[x].Volume), PrintTo.OutputTab1);

                ListMarketDepthLevelBidPrice.Add(marketDepth.Bids[x].Price);
                listMarketDepthLevelBidVolume.Add(marketDepth.Bids[x].Volume);
            }

            //CountMarket += 1;

            //if(CountMarket >= 4)
            //    NinjaTrader.Code.Output.Process(string.Format("Nivel 1: {0} Nivel 2: {1} Nivel 3: {2} Nivel 4: {3} Nivel 5: {4}",
            //         PriceAskL0, PriceAskL1, PriceAskL2, PriceAskL3, PriceAskL4), PrintTo.OutputTab1);

            //Cleanup();
            //#region Ask
            //if (e.MarketDataType == MarketDataType.Ask && e.Position == 0)
            //{ 
            //    PriceAskL0 = e.Price;
            //    VolAskL0 = e.Volume;
            //}

            //if (e.MarketDataType == MarketDataType.Ask && e.Position == 1)
            //{
            //    PriceAskL1 = e.Price;
            //    VolAskL1 = e.Volume;
            //}
            //if (e.MarketDataType == MarketDataType.Ask && e.Position == 2) 
            //{
            //    PriceAskL2 = e.Price;
            //    VolAskL2 = e.Volume;
            //}

            //if (e.MarketDataType == MarketDataType.Ask && e.Position == 3)
            //{ 
            //    PriceAskL3 = e.Price;
            //    VolAskL3 = e.Volume;
            //}

            //if (e.MarketDataType == MarketDataType.Ask && e.Position == 4) 
            //{
            //    PriceAskL4 = e.Price;
            //    VolAskL4 = e.Volume;
            //}
            //#endregion

            //#region Bid
            //if (e.MarketDataType == MarketDataType.Bid && e.Position == 0)
            //{
            //    PriceBidL0 = e.Price;
            //    VolBidL0 = e.Volume;
            //}

            //if (e.MarketDataType == MarketDataType.Bid && e.Position == 1)
            //{
            //    PriceBidL1 = e.Price;
            //    VolBidL1 = e.Volume;
            //}
            //if (e.MarketDataType == MarketDataType.Bid && e.Position == 2)
            //{
            //    PriceBidL2 = e.Price;
            //    VolBidL2 = e.Volume;
            //}

            //if (e.MarketDataType == MarketDataType.Bid && e.Position == 3)
            //{
            //    PriceBidL3 = e.Price;
            //    VolBidL3 = e.Volume;
            //}

            //if (e.MarketDataType == MarketDataType.Bid && e.Position == 4)
            //{
            //    PriceBidL4 = e.Price;
            //    VolBidL4 = e.Volume;

            //    NinjaTrader.Code.Output.Process(string.Format("Nivel BID 1: {0} Nivel BID 2: {1} Nivel BID 3: {2} Nivel BID 4: {3} Nivel BID 5: {4}",
            //         PriceBidL0, PriceBidL1, PriceBidL2, PriceBidL3, PriceBidL4), PrintTo.OutputTab1);
            //}
            //#endregion

            //NinjaTrader.Code.Output.Process(string.Format("Nivel 1: {0} Nivel 2: {1} Nivel 3: {2} Nivel 4: {3} Nivel 5: {4}",
            //         PriceAskL0, PriceAskL1, PriceAskL2, PriceAskL3, PriceAskL4), PrintTo.OutputTab1);

            //Niveles++;
            //if (e.Operation == Operation.Update)
            //{
            //    NinjaTrader.Code.Output.Process(string.Format("{0}",
            //         "MARKET DETPH UPDATE"), PrintTo.OutputTab1);

            //    if (e.MarketDataType == MarketDataType.Ask && e.Position == 0) VolAskL0 = e.Price;
            //    if (e.MarketDataType == MarketDataType.Ask && e.Position == 1) VolAskL1 = e.Price;
            //    if (e.MarketDataType == MarketDataType.Ask && e.Position == 2) VolAskL2 = e.Price;
            //    if (e.MarketDataType == MarketDataType.Ask && e.Position == 3) VolAskL3 = e.Price;
            //    if (e.MarketDataType == MarketDataType.Ask && e.Position == 4) VolAskL4 = e.Price;

            //    NinjaTrader.Code.Output.Process(string.Format("Nivel 1: {0} Nivel 2: {1} Nivel 3: {2} Nivel 4: {3} Nivel 5: {4}",
            //         VolAskL0, VolAskL1, VolAskL2, VolAskL3, VolAskL4), PrintTo.OutputTab1);
            //}


            //NinjaTrader.Code.Output.Process(string.Format("Count: {0} Price: {1} Volume: {2}",

            //         marketDepth.Asks.Count, marketDepth.Asks[0].Price, marketDepth.Asks[0].Volume), PrintTo.OutputTab1);
            //// Print the Ask's price ladder
            //for (int i = 0; i < marketDepth.Asks.Count; i++)
            //{

            //    NinjaTrader.Code.Output.Process(string.Format("Position: {0} Price: {1} Volume: {2}", e.Position,

            //         marketDepth.Asks[i].Price, marketDepth.Asks[i].Volume), PrintTo.OutputTab1);

            //}

            //Cleanup();
        }


        // Called by TabControl when tab is being removed or window is closed

        public override void Cleanup()
        {
            // Make sure to unsubscribe to the market data subscription
            if (marketDepth != null)
                marketDepth.Update -= OnMarketDepth;

            NinjaTrader.Code.Output.Process(string.Format("FINALIZADO {0}",

             ""), PrintTo.OutputTab2);
        }

        protected override string GetHeaderPart(string variable)
        {
            throw new NotImplementedException();
        }

        protected override void Restore(XElement element)
        {
            throw new NotImplementedException();
        }

        protected override void Save(XElement element)
        {
            throw new NotImplementedException();
        }

        // Other required NTTabPage members left out for demonstration purposes. Be sure to add them in your own code.
    }
}
