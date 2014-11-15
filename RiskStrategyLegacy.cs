namespace SampleHistoryTesting
{
	using System.Collections.Generic;
	using System.Linq;
	using StockSharp.Algo;
	using StockSharp.Algo.Candles;
	using StockSharp.Algo.Indicators;
	using StockSharp.Algo.Strategies;
	using StockSharp.Logging;
	using StockSharp.BusinessEntities;



	class RiskStrategyLegacy : Strategy
	{
		private readonly CandleSeries _series;
		public RiskStrategyLegacy(CandleSeries series, AverageTrueRange latr, AverageTrueRange satr)
		{
			_series = series;
			LongATR = latr;
			ShortATR = satr;
			PLB = new ParabolicSar();
		}
		public AverageTrueRange LongATR { get; private set; }
		public ParabolicSar PLB { get; private set; }
		public AverageTrueRange ShortATR { get; private set; }
		public SimpleMovingAverage SMA200 { get; private set; }
		private double buyprice = 0;
		private int deflevel = 0;
		protected override void OnStarted()
		{
			_series.WhenCandlesFinished()
				.Do(ProcessFinCandle)
				.Apply(this);
			this.WhenNewMyTrades().Do(OnNewOrderTrades).Apply(this);

			// запоминаем текущее положение относительно друг друга
			// _isShortLessThenLong = true;// ShortSma.GetCurrentValue() < LongSma.GetCurrentValue();

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

			StopLossStrategy stopLoss = new StopLossStrategy(trades.ToArray<MyTrade>()[0], 40);
			stopLoss.IsTrailing = true;
			stopLoss.WhenError().Do(() => { this.AddInfoLog("ololo"); });
			stopLoss.WhenNewMyTrades().Do(() => { this.AddInfoLog("ololo111"); });
			ChildStrategies.Add(stopLoss);
		}
		protected void ProcessFinCandle(Candle candle)
		{
			if (ProcessState == ProcessStates.Stopping)
			{
				// отменяем активные заявки
				CancelActiveOrders();
				return;
			}

			//this.AddInfoLog("Новая свеча {0}: {1};{2};{3};{4}; объем {5}".Put(candle.OpenTime, candle.OpenPrice, candle.HighPrice, candle.LowPrice, candle.ClosePrice, candle.TotalVolume));

			// добавляем новую свечку
			LongATR.Process(candle);
			ShortATR.Process(candle);
			PLB.Process(candle);
			int tt = PLB.GetCurrentValue<int>();

			if ((double)candle.ClosePrice < buyprice - deflevel && buyprice > 0)
			{

			}
			//+! костыльная проверка что индикатор заполнен.
			// нужно всегда заполнять перед торговлей
			if (LongATR.Container.Count >= LongATR.Length)
			{

				if (LongATR.GetCurrentValue<int>() > ShortATR.GetCurrentValue<int>() && this.Position == 0)
				{
					var direction = OrderDirections.Buy;
					buyprice = (double)candle.ClosePrice;
					//спизжено из стоп лос стретеджи
					//var longPos = this.BuyAtMarket();

					//// регистрируем правило, отслеживающее появление новых сделок по заявке
					//longPos
					//    .WhenNewTrades()
					//    .Do(OnNewOrderTrades)
					//    .Apply(this);

					//// отправляем заявку на регистрацию
					//RegisterOrder(longPos);

					double dl = 2.5 * ShortATR.GetCurrentValue<int>();
					deflevel = (int)(dl + 0.5);
					Order longPos;
					if (!((EmulationTrader)Trader).MarketEmulator.Settings.UseMarketDepth)
					{
						// регистрируем псевдо-маркетную заявку - лимитная заявка с ценой гарантирующей немедленное исполнение.
						longPos = this.CreateOrder(direction, Security.GetMarketPrice(direction), Volume);
						RegisterOrder(longPos);
					}
					else
					{
						// переворачиваем позицию через котирование
						var strategy = new MarketQuotingStrategy(direction, Volume)
						{
							WaitAllTrades = true,
						};
						ChildStrategies.Add(strategy);
					}

					// если произошло пересечение
					//if (_isShortLessThenLong != isShortLessThenLong)
					//{
					//     если короткая меньше чем длинная, то продажа, иначе, покупка.
					//    var direction = isShortLessThenLong ? OrderDirections.Sell : OrderDirections.Buy;

					//    if (!((EmulationTrader)Trader).MarketEmulator.Settings.UseMarketDepth)
					//    {
					//         регистрируем псевдо-маркетную заявку - лимитная заявка с ценой гарантирующей немедленное исполнение.
					//        RegisterOrder(this.CreateOrder(direction, Security.GetMarketPrice(direction), Volume));
					//    }
					//    else
					//    {
					//         переворачиваем позицию через котирование
					//        var strategy = new MarketQuotingStrategy(direction, Volume)
					//        {
					//            WaitAllTrades = true,
					//        };
					//        ChildStrategies.Add(strategy);
					//    }

					//     запоминаем текущее положение относительно друг друга
					//    _isShortLessThenLong = isShortLessThenLong;
					//}

					//+! щас будет костыльный параметр 2.5 на ATR  в стопах
				}

			}

		}
	}
}
