using StockSharp.Algo.Strategies.Protective;

namespace SampleHistoryTesting
{
	using System.Linq;
	using System.Collections.Generic;

	using Ecng.Common;

	using StockSharp.Algo;
	using StockSharp.Algo.Candles;
	using StockSharp.Algo.Indicators;
	using StockSharp.Algo.Strategies;
	using StockSharp.Algo.Strategies.Quoting;
	using StockSharp.Logging;
	using StockSharp.BusinessEntities;
	using StockSharp.Messages;
	using StockSharp.Xaml.Charting;
	using StockSharp.Localization;

	class RiskStrategy : Strategy
	{
		private readonly IChart _chart;
		private readonly ChartCandleElement _candlesElem;
		private readonly ChartTradeElement _tradesElem;
		private readonly ChartIndicatorElement _shortElem;
		private readonly ChartIndicatorElement _longElem;
		private readonly List<MyTrade> _myTrades = new List<MyTrade>();
		private readonly CandleSeries _series;
		private bool _isShortLessThenLong;

		public AverageTrueRange LongATR { get; private set; }
		public ParabolicSar PLB { get; private set; }
		public AverageTrueRange ShortATR { get; private set; }
		public SimpleMovingAverage SMA200 { get; private set; }

		private double buyprice = 0;
		private int deflevel = 0;

		public RiskStrategy(IChart chart, ChartCandleElement candlesElem,
			ChartTradeElement tradesElem, ChartIndicatorElement shortElem, ChartIndicatorElement longElem,
			CandleSeries series)
		{
			_chart = chart;
			_candlesElem = candlesElem;
			_tradesElem = tradesElem;
			_shortElem = shortElem;
			_longElem = longElem;
			
			_series = series;

			LongATR = (AverageTrueRange)longElem.Indicator;
			ShortATR = (AverageTrueRange)shortElem.Indicator;
			// PLB = new ParabolicSar();
		}
		
		protected override void OnStarted()
		{
			_series
				.WhenCandlesFinished()
				.Do(ProcessCandle)
				.Apply(this);

			this
				.WhenNewMyTrades()
				.Do(OnNewOrderTrades)
				.Apply(this);

			// запоминаем текущее положение относительно друг друга
			// _isShortLessThenLong = ShortSma.GetCurrentValue() < LongSma.GetCurrentValue();

			base.OnStarted();
		}

		private void OnNewOrderTrades(IEnumerable<MyTrade> trades)
		{
			// для каждой сделки добавляем защитную пару стратегии 
			/*
			var protectiveStrategies = trades.Select(t =>
			{
				// выставляет стоп-лосс в deflevel пунктов 
				var stopLoss = new StopLossStrategy(t, deflevel);
				stopLoss.IsTrailing = true;
				return stopLoss;
			});

			//ChildStrategies.AddRange(protectiveStrategies);
			foreach (var st in protectiveStrategies)
				ChildStrategies.Add(st);
			 * */
			//var unit = new Unit(40, UnitTypes.Point, Security);
			//var stopLoss = new StopLossStrategy(trades.ToArray<MyTrade>()[0], unit) { IsTrailing = true};
			//stopLoss.IsTrailing = true;
			//stopLoss.WhenError().Do(() => { this.AddInfoLog("ololo"); });
			//stopLoss.NewMyTrades+=AddTradesFromStop;
			//ChildStrategies.Add(stopLoss);
			//stopLoss.IsTrailing = true;
			_myTrades.AddRange(trades);
			maxPrice = trades.First().Trade.Price;
		}

		private void AddTradesFromStop(IEnumerable<MyTrade> trades)
		{
			_myTrades.AddRange(trades);
		}

		private decimal maxPrice;
		private bool needProtect = false;
		private decimal protectLevel = 0;
		private void ProcessCandle(Candle candle)
		{
			// если наша стратегия в процессе остановки
			if (ProcessState == ProcessStates.Stopping)
			{
				// отменяем активные заявки
				CancelActiveOrders();
				return;
			}

			this.AddInfoLog(LocalizedStrings.Str2177Params.Put(candle.OpenTime, candle.OpenPrice, candle.HighPrice, candle.LowPrice, candle.ClosePrice, candle.TotalVolume));
			
			// добавляем новую свечу
			var longValue = LongATR.Process(candle);
			var shortValue = ShortATR.Process(candle);
			if (Position < 0)
			{
				if (maxPrice > candle.ClosePrice)
					maxPrice = candle.ClosePrice;
			}
			// вычисляем новое положение относительно друг друга
			// var isShortLessThenLong = ShortSma.GetCurrentValue() < LongSma.GetCurrentValue();
			if (-maxPrice + candle.ClosePrice > protectLevel && needProtect)
			{
				var price = Security.GetMarketPrice(Connector, Sides.Buy);

				// регистрируем псевдо-маркетную заявку - лимитная заявка с ценой гарантирующей немедленное исполнение.
				if (price != null)
				{
					RegisterOrder(this.CreateOrder(Sides.Buy, price.Value, Volume));
					needProtect = false;
				}
			}
			// если индикаторы заполнились
			if (LongATR.Container.Count >= LongATR.Length)
			{

				if (LongATR.GetCurrentValue<int>() > ShortATR.GetCurrentValue<int>() && Position == 0)
				{
					// если короткая меньше чем длинная, то продажа, иначе, покупка.
					var direction = Sides.Sell;

					// вычисляем размер для открытия или переворота позы
					var volume = Volume;

					if (!SafeGetConnector().RegisteredMarketDepths.Contains(Security))
					{
						var price = Security.GetMarketPrice(Connector, direction);
						
						// регистрируем псевдо-маркетную заявку - лимитная заявка с ценой гарантирующей немедленное исполнение.
						if (price != null)
						{
							RegisterOrder(this.CreateOrder(direction, price.Value, volume));
							needProtect = true;
							protectLevel = LongATR.GetCurrentValue<int>()*3;
						}
					}
					else
					{
						// переворачиваем позицию через котирование
						var strategy = new MarketQuotingStrategy(direction, volume)
						{
							WaitAllTrades = true,
						};
						ChildStrategies.Add(strategy);
					}
				}
			}

			
			_myTrades.Clear();

			var dict = new Dictionary<IChartElement, object>
			{
				{ _candlesElem, candle },
			};

			_chart.Draw(candle.OpenTime, dict);
		}
	}
}