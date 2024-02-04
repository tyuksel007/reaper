using System.Diagnostics;
using System.Security.Cryptography;
using Reaper.CommonLib.Interfaces;
using Reaper.Exchanges.Kucoin.Interfaces;
using Reaper.SignalSentinel.Strategies;

namespace Reaper.Exchanges.Kucoin.Services;
public class TilsonService(IMarketDataService marketDataService,
    IBrokerService brokerService,
    IPositionInfoService positionInfoService,
    IFuturesHub futuresHub) : ITilsonService
{
    internal async Task<SignalType> GetTargetActionAsync(
            SignalType position,
            string symbol,
            int interval,
            CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow.AddMinutes(-(interval * 50)).ToString("dd-MM-yyyy HH:mm");
        var endTime = DateTime.UtcNow.ToString("dd-MM-yyyy HH:mm");
        var pricesResult = await marketDataService.GetKlinesAsync(symbol, startTime, endTime, interval, cancellationToken);

        if (pricesResult.Error != null)
        {
            throw new InvalidOperationException("Error getting klines", pricesResult.Error);
        }

        var t3Values = TilsonT3.CalculateT3([.. pricesResult.Data], period: 6, volumeFactor: 0.5m);

        var t3Last = t3Values.Last();
        var originLast = pricesResult.Data!.Last();

        if (position != SignalType.Buy && t3Last > originLast)
        {
            RLogger.AppLog.Information($"Buy signal detected for {symbol} at {DateTime.UtcNow}");
            return SignalType.Buy;
        }
        else if (position != SignalType.Sell && t3Last < originLast)
        {
            RLogger.AppLog.Information($"Sell signal detected for {symbol} at {DateTime.UtcNow}");
            return SignalType.Sell;
        }

        RLogger.AppLog.Information($"Holding {symbol} at {DateTime.UtcNow}");
        return SignalType.Hold;
    }




    public async Task TryBuyOrSellAsync(string symbol, SignalType actionToTake, decimal amount, CancellationToken cancellationToken)
    {
        if (actionToTake == SignalType.Buy)
        {
            await brokerService.BuyMarketAsync(symbol, amount, cancellationToken);
        }
        else if (actionToTake == SignalType.Sell)
        {
            await brokerService.SellMarketAsync(symbol, amount, cancellationToken);
        }
    }


    internal static SignalType GetOppositeAction(SignalType side)
    {
        if (side == SignalType.Buy)
        {
            return SignalType.Sell;
        }
        else if (side == SignalType.Sell)
        {
            return SignalType.Buy;
        }
        return side;
    }


    internal async Task<Result<(bool takeProfit, decimal percent)>> WatchTargetProfitAsync(
        string symbol,
        decimal entryPrice,
        decimal targetPnlPercent,
        CancellationToken cancellationToken)
    {
        var markPrice = await marketDataService.GetSymbolPriceAsync(symbol, cancellationToken);

        if (markPrice.Error != null)
        {
            return new() { Error = markPrice.Error };
        }
        var currentProfitRatio = (markPrice.Data - entryPrice) / entryPrice;

        if (currentProfitRatio >= targetPnlPercent)
        {
            return new() { Data = (true, currentProfitRatio) };
        }
        return new() { Data = (false, currentProfitRatio) };
    }


    public async Task RunAsync(
        string symbol,
        decimal amount,
        decimal profitPercentage,
        int interval,
        CancellationToken cancellationToken)
    {
        TimeSpan profitTimeOut = TimeSpan.FromMinutes(interval);
        SignalType currentAction = SignalType.Undefined;

        while (cancellationToken.IsCancellationRequested == false)
        {
            RLogger.AppLog.Information($".......................amount: {amount}......................".ToUpper());
            SignalType actionToTake = await GetTargetActionAsync(currentAction,
                symbol,
                interval,
                cancellationToken);

            //close position before target action, if not first trade 
            if (currentAction != SignalType.Undefined)
            {
                RLogger.AppLog.Information("Closing position before target action".ToUpper());
                await TryBuyOrSellAsync(symbol, actionToTake, amount, cancellationToken);
            }
            //open position
            await TryBuyOrSellAsync(symbol, actionToTake, amount, cancellationToken);

            if (actionToTake != SignalType.Hold)
            {
                currentAction = actionToTake;
            }

            var positionDetails = await positionInfoService.GetPositionInfoAsync(symbol, cancellationToken);

            if (positionDetails.Error != null)
            {
                throw positionDetails.Error;
            }

            //try to take profit
            using var timeOutCTS = new CancellationTokenSource(profitTimeOut);
            timeOutCTS.Token.Register(() =>
            {
                RLogger.AppLog.Information("Profit watch timeout. Closing watch.".ToUpper());
            });

            (amount, decimal entryPrice, _) = positionDetails.Data!;

            while (timeOutCTS.IsCancellationRequested == false)
            {
                await Task.Delay(30 * 1000, cancellationToken);
                var profit = await WatchTargetProfitAsync(
                                    symbol,
                                    entryPrice,
                                    profitPercentage,
                                    cancellationToken);

                if (profit.Error != null)
                {
                    RLogger.AppLog.Information($"Error watching target profit: {profit.Error}");
                    continue;
                }

                var (takeProfit, profitPercent) = profit.Data!;
                if (takeProfit)
                {
                    RLogger.AppLog.Information($"Realized Pnl: {profitPercent}");
                    RLogger.AppLog.Information("Taking profit....");

                    var profitAmount = amount * profitPercent;
                    await TryBuyOrSellAsync(symbol, GetOppositeAction(currentAction), profitAmount, cancellationToken);

                    positionDetails = await positionInfoService.GetPositionInfoAsync(symbol, cancellationToken);
                    if (positionDetails.Error != null)
                    {
                        throw positionDetails.Error;
                    }

                    (amount, _, _) = positionDetails.Data!;
                }
            }
        }
    }
}