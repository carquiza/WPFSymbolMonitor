namespace CryptoChart.Services.News;

/// <summary>
/// Helper class for mapping cryptocurrency symbols between different formats.
/// </summary>
public static class CryptoSymbolMapper
{
    /// <summary>
    /// Standard cryptocurrency symbol mappings.
    /// </summary>
    private static readonly Dictionary<string, CryptoSymbolInfo> KnownSymbols = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BTCUSDT"] = new("BTC", "Bitcoin", "CRYPTO:BTC"),
        ["ETHUSDT"] = new("ETH", "Ethereum", "CRYPTO:ETH"),
        ["ETHBTC"] = new("ETH", "Ethereum", "CRYPTO:ETH"),
        ["BTC"] = new("BTC", "Bitcoin", "CRYPTO:BTC"),
        ["ETH"] = new("ETH", "Ethereum", "CRYPTO:ETH"),
        ["SOLUSDT"] = new("SOL", "Solana", "CRYPTO:SOL"),
        ["XRPUSDT"] = new("XRP", "Ripple", "CRYPTO:XRP"),
        ["ADAUSDT"] = new("ADA", "Cardano", "CRYPTO:ADA"),
        ["DOGEUSDT"] = new("DOGE", "Dogecoin", "CRYPTO:DOGE"),
        ["DOTUSDT"] = new("DOT", "Polkadot", "CRYPTO:DOT"),
        ["LINKUSDT"] = new("LINK", "Chainlink", "CRYPTO:LINK"),
        ["AVAXUSDT"] = new("AVAX", "Avalanche", "CRYPTO:AVAX"),
    };

    /// <summary>
    /// Gets the base asset symbol from a trading pair.
    /// </summary>
    /// <param name="symbol">Trading pair symbol (e.g., "BTCUSDT").</param>
    /// <returns>Base asset symbol (e.g., "BTC").</returns>
    public static string GetBaseAsset(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return symbol;

        if (KnownSymbols.TryGetValue(symbol, out var info))
            return info.BaseAsset;

        // Try to extract base asset from common patterns
        symbol = symbol.ToUpperInvariant();
        
        if (symbol.EndsWith("USDT"))
            return symbol.Replace("USDT", "");
        if (symbol.EndsWith("BUSD"))
            return symbol.Replace("BUSD", "");
        if (symbol.EndsWith("USDC"))
            return symbol.Replace("USDC", "");
        if (symbol.EndsWith("BTC") && symbol.Length > 3)
            return symbol.Replace("BTC", "");
        if (symbol.EndsWith("ETH") && symbol.Length > 3)
            return symbol.Replace("ETH", "");

        return symbol;
    }

    /// <summary>
    /// Gets the Finnhub format for a symbol.
    /// </summary>
    /// <param name="symbol">Trading pair symbol (e.g., "BTCUSDT").</param>
    /// <returns>Finnhub format (e.g., "BTC").</returns>
    public static string ToFinnhubFormat(string symbol)
    {
        return GetBaseAsset(symbol);
    }

    /// <summary>
    /// Gets the Alpha Vantage format for a symbol.
    /// </summary>
    /// <param name="symbol">Trading pair symbol (e.g., "BTCUSDT").</param>
    /// <returns>Alpha Vantage format (e.g., "CRYPTO:BTC").</returns>
    public static string ToAlphaVantageFormat(string symbol)
    {
        if (KnownSymbols.TryGetValue(symbol, out var info))
            return info.AlphaVantageFormat;

        var baseAsset = GetBaseAsset(symbol);
        return $"CRYPTO:{baseAsset}";
    }

    /// <summary>
    /// Gets the full name of a cryptocurrency if known.
    /// </summary>
    /// <param name="symbol">Trading pair or base asset symbol.</param>
    /// <returns>Full name (e.g., "Bitcoin") or null if unknown.</returns>
    public static string? GetFullName(string symbol)
    {
        if (KnownSymbols.TryGetValue(symbol, out var info))
            return info.FullName;

        var baseAsset = GetBaseAsset(symbol);
        if (KnownSymbols.TryGetValue(baseAsset, out info))
            return info.FullName;

        return null;
    }

    /// <summary>
    /// Gets related search terms for a cryptocurrency symbol.
    /// Useful for filtering news articles.
    /// </summary>
    /// <param name="symbol">Trading pair symbol.</param>
    /// <returns>Collection of search terms.</returns>
    public static IEnumerable<string> GetSearchTerms(string symbol)
    {
        var baseAsset = GetBaseAsset(symbol);
        var fullName = GetFullName(symbol);

        var terms = new List<string> { baseAsset };

        if (!string.IsNullOrEmpty(fullName))
        {
            terms.Add(fullName);
        }

        // Add common variations
        terms.Add($"${baseAsset}"); // Twitter-style symbol
        terms.Add($"CRYPTO:{baseAsset}");

        return terms.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a text mentions a specific cryptocurrency.
    /// </summary>
    /// <param name="text">Text to search in.</param>
    /// <param name="symbol">Cryptocurrency symbol to search for.</param>
    /// <returns>True if the cryptocurrency is mentioned.</returns>
    public static bool TextMentionsSymbol(string? text, string symbol)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var searchTerms = GetSearchTerms(symbol);
        return searchTerms.Any(term => 
            text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Information about a cryptocurrency symbol.
/// </summary>
public record CryptoSymbolInfo(
    string BaseAsset,
    string FullName,
    string AlphaVantageFormat);
