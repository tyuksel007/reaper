﻿namespace Reaper.CommonLib.Interfaces;
public interface IBrokerService
{
    Task<Result<string>> BuyLimitAsync(string symbol, decimal amount, decimal limitPrice, CancellationToken cancellationToken);
    Task<Result<string>> BuyMarketAsync(string symbol, decimal amount, CancellationToken cancellationToken);
    Task<Result<string>> SellLimitAsync(string symbol, decimal amount, decimal limitPrice, CancellationToken cancellationToken);
    Task<Result<string>> SellMarketAsync(string symbol, decimal amount, CancellationToken cancellationToken);
}
