using System.Security.Cryptography;
using System.Text;
using Flurl.Http;
using Reaper.CommonLib.Interfaces;
using Reaper.Exchanges.Kucoin.Services.Models;

namespace Reaper.Exchanges.Kucoin.Services;
public static class FlurlExtensions
{
    private static string CreateSignature(string strToSign, string secretKey)
    {
        using var hmacsha256 = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey.Trim()));
        var hash = hmacsha256.ComputeHash(Encoding.UTF8.GetBytes(strToSign.Trim()));
        return Convert.ToBase64String(hash);
    }

    public static IFlurlRequest WithSignatureHeaders(this IFlurlRequest flurlRequest,
        KucoinOptions kucoinOptions,
        string method,
        string jsonBody = "")
    {
        string apiKey = kucoinOptions.ApiKey ?? throw new InvalidOperationException(nameof(apiKey));
        string apiSecret = kucoinOptions.ApiSecret ?? throw new InvalidOperationException(nameof(apiSecret));
        string apiPassphrase = kucoinOptions.ApiPassphrase ?? throw new InvalidOperationException(nameof(apiPassphrase));

        var passphraseSignature = CreateSignature(apiPassphrase, apiSecret);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var query  = string.IsNullOrEmpty(flurlRequest.Url.Query) ? string.Empty : $"?{flurlRequest.Url.Query}";

        var strForSign = timestamp 
            + method 
            + Uri.UnescapeDataString(flurlRequest.Url.Path + query)
            + jsonBody;


        var httpSignature = CreateSignature(strForSign, apiSecret);

        return flurlRequest
            .WithHeader("KC-API-KEY", apiKey)
            .WithHeader("KC-API-SIGN", httpSignature)
            .WithHeader("KC-API-TIMESTAMP", timestamp)
            .WithHeader("KC-API-PASSPHRASE", passphraseSignature)
            .WithHeader("KC-API-KEY-VERSION", "2");
    }

     public static async Task<Result<TResponse>> CallAsync<TResponse>(
        this Func<IFlurlClient, object?, CancellationToken, Task<TResponse>> flurlCall,
        IFlurlClient client,
        object? data,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await flurlCall(client, data, cancellationToken);
            return new() { Data = response };
        }
        catch (FlurlHttpException ex)
        {
            return new() { Error = ex };
        }
        catch (Exception ex)
        {
            return new() { Error = ex };
        }
        
    }

}