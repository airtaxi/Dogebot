using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using KoreanNumberParser;

namespace Dogebot.Server.Services;

public partial class ExchangeRateService(IHttpClientFactory httpClientFactory, ILogger<ExchangeRateService> logger) : IExchangeRateService
{
    private const string ExchangeRateSummaryApiAddress = "https://finance.daum.net/api/exchanges/summaries";
    private const string DaumFinanceRefererAddress = "https://finance.daum.net/exchanges";
    private const string DaumFinanceUserAgentValue = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36 Edg/148.0.0.0";
    private const string DefaultSourceCurrencyQuery = "달러";
    private const string DefaultTargetCurrencyQuery = "원화";
    private const string ExchangeRateUnavailableMessage = "환율 정보를 가져오지 못했습니다.\n잠시 후 다시 시도해주세요.";
    private const decimal KoreanNumberDisplayThreshold = 10_000_000m;

    private static readonly ExchangeCurrency s_koreanWonCurrency = new("KRW", "원", "한국", "한국 (KRW/KRW)", 1, 1, string.Empty, true);
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly HttpClient _exchangeRateClient = httpClientFactory.CreateClient();

    public async Task<string> CreateExchangeRateMessageAsync(string queryText)
    {
        var parseResult = ParseRequest(queryText);
        if (parseResult.Message != null) return parseResult.Message;

        var currencies = await FetchExchangeCurrenciesAsync();
        if (currencies == null || currencies.Count == 0) return ExchangeRateUnavailableMessage;

        var request = parseResult.Request!;
        var sourceCurrency = ResolveCurrency(request.SourceCurrencyQuery, currencies);
        if (sourceCurrency == null) return CreateCurrencyNotFoundMessage(request.SourceCurrencyQuery);

        var targetCurrency = ResolveCurrency(request.TargetCurrencyQuery, currencies);
        if (targetCurrency == null) return CreateCurrencyNotFoundMessage(request.TargetCurrencyQuery);

        var sourceAmount = request.Amount ?? checked((long)sourceCurrency.CurrencyUnit);
        var targetAmount = ConvertCurrency(sourceAmount, sourceCurrency, targetCurrency);
        return FormatExchangeRateMessage(sourceAmount, sourceCurrency, targetAmount, targetCurrency);
    }

    private async Task<IReadOnlyList<ExchangeCurrency>?> FetchExchangeCurrenciesAsync()
    {
        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, ExchangeRateSummaryApiAddress);
            ConfigureDaumFinanceRequestHeaders(requestMessage);

            using var responseMessage = await _exchangeRateClient.SendAsync(requestMessage);
            var responseContent = await responseMessage.Content.ReadAsStringAsync();
            if (!responseMessage.IsSuccessStatusCode)
            {
                logger.LogError("[EXCHANGE_RATE] Request failed with status code {StatusCode}. Response: {ResponsePreview}", responseMessage.StatusCode, BuildContentPreview(responseContent, 500));
                return null;
            }

            var responsePayload = JsonSerializer.Deserialize<DaumExchangeRateResponsePayload>(responseContent, s_jsonSerializerOptions);
            if (responsePayload?.Data == null) return null;

            var currencies = responsePayload.Data
                .Where(currencyPayload => !string.IsNullOrWhiteSpace(currencyPayload.CurrencyCode) && currencyPayload.CurrencyUnit > 0 && currencyPayload.BasePrice > 0)
                .Select(MapExchangeCurrency)
                .ToList();
            return currencies;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogError(exception, "[EXCHANGE_RATE] Error fetching exchange rate data");
            return null;
        }
    }

    private static void ConfigureDaumFinanceRequestHeaders(HttpRequestMessage requestMessage)
    {
        requestMessage.Headers.Accept.Clear();
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        requestMessage.Headers.UserAgent.Clear();
        requestMessage.Headers.UserAgent.ParseAdd(DaumFinanceUserAgentValue);
        requestMessage.Headers.Referrer = new Uri(DaumFinanceRefererAddress);
    }

    private static ExchangeRateParseResult ParseRequest(string queryText)
    {
        var trimmedQueryText = queryText.Trim();
        if (string.IsNullOrWhiteSpace(trimmedQueryText)) return new ExchangeRateParseResult(new ExchangeRateRequest(null, DefaultSourceCurrencyQuery, DefaultTargetCurrencyQuery), null);

        var tokens = WhitespaceRegex().Split(trimmedQueryText).Where(token => token.Length > 0).ToArray();
        if (tokens.Length is 0) return new ExchangeRateParseResult(new ExchangeRateRequest(null, DefaultSourceCurrencyQuery, DefaultTargetCurrencyQuery), null);
        if (tokens.Length > 2) return new ExchangeRateParseResult(null, CreateUsageMessage());

        if (!TryCreateSourceToken(tokens[0], tokens.Length > 1, out var amount, out var sourceCurrencyQuery)) return new ExchangeRateParseResult(null, CreateUsageMessage());

        var targetCurrencyQuery = tokens.Length > 1 ? tokens[1] : DefaultTargetCurrencyQuery;
        return new ExchangeRateParseResult(new ExchangeRateRequest(amount, sourceCurrencyQuery, targetCurrencyQuery), null);
    }

    private static bool TryCreateSourceToken(string sourceToken, bool hasTargetCurrencyToken, out long? amount, out string sourceCurrencyQuery)
    {
        amount = null;
        sourceCurrencyQuery = sourceToken;

        if (!TryParseSourceAmount(sourceToken, out var parsedAmount, out var currencyQuery)) return true;
        if (parsedAmount <= 0) return false;

        if (string.IsNullOrWhiteSpace(currencyQuery) && hasTargetCurrencyToken) return false;

        amount = parsedAmount;
        sourceCurrencyQuery = string.IsNullOrWhiteSpace(currencyQuery) ? DefaultSourceCurrencyQuery : currencyQuery;
        return true;
    }

    private static bool TryParseSourceAmount(string sourceToken, out long amount, out string currencyQuery)
    {
        for (var length = sourceToken.Length; length > 0; length--)
        {
            var amountText = sourceToken[..length];
            if (!KoreanNumber.TryParseInt64(amountText, out var parsedAmount)) continue;

            amount = parsedAmount;
            currencyQuery = sourceToken[length..].Trim();
            return true;
        }

        amount = default;
        currencyQuery = string.Empty;
        return false;
    }

    private static ExchangeCurrency? ResolveCurrency(string currencyQuery, IReadOnlyList<ExchangeCurrency> currencies)
    {
        var normalizedCurrencyQuery = NormalizeCurrencySearchText(currencyQuery);
        if (string.IsNullOrWhiteSpace(normalizedCurrencyQuery)) return null;
        if (IsKoreanWonQuery(normalizedCurrencyQuery)) return s_koreanWonCurrency;
        if (normalizedCurrencyQuery.Equals("달러", StringComparison.OrdinalIgnoreCase)) return currencies.FirstOrDefault(currency => currency.CurrencyCode.Equals("USD", StringComparison.OrdinalIgnoreCase));

        var exactMatch = currencies.FirstOrDefault(currency => GetCurrencySearchAliases(currency).Any(alias => alias.Equals(normalizedCurrencyQuery, StringComparison.OrdinalIgnoreCase)));
        if (exactMatch != null) return exactMatch;

        return currencies.FirstOrDefault(currency => GetCurrencySearchAliases(currency).Any(alias => alias.Contains(normalizedCurrencyQuery, StringComparison.OrdinalIgnoreCase)));
    }

    private static IEnumerable<string> GetCurrencySearchAliases(ExchangeCurrency currency)
    {
        yield return NormalizeCurrencySearchText(currency.CurrencyCode);
        yield return NormalizeCurrencySearchText(currency.CurrencyName);
        yield return NormalizeCurrencySearchText(currency.Country);
        yield return NormalizeCurrencySearchText(currency.Name);
        yield return NormalizeCurrencySearchText($"{currency.Country}{currency.CurrencyName}");
        yield return NormalizeCurrencySearchText($"{currency.Country}{currency.CurrencyCode}");
    }

    private static bool IsKoreanWonQuery(string normalizedCurrencyQuery) =>
        normalizedCurrencyQuery.Equals("원", StringComparison.OrdinalIgnoreCase) || normalizedCurrencyQuery.Equals("KRW", StringComparison.OrdinalIgnoreCase) || normalizedCurrencyQuery.Equals("한국", StringComparison.OrdinalIgnoreCase) || normalizedCurrencyQuery.Equals("대한민국", StringComparison.OrdinalIgnoreCase);

    private static decimal ConvertCurrency(long sourceAmount, ExchangeCurrency sourceCurrency, ExchangeCurrency targetCurrency) =>
        sourceAmount * sourceCurrency.KoreanWonUnitPrice / targetCurrency.KoreanWonUnitPrice;

    private static string FormatExchangeRateMessage(long sourceAmount, ExchangeCurrency sourceCurrency, decimal targetAmount, ExchangeCurrency targetCurrency)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("💱 환율 계산");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine($"{FormatCurrencyAmount(sourceAmount, sourceCurrency)} ({FormatCurrencyDisplayName(sourceCurrency)}) = {FormatCurrencyAmount(targetAmount, targetCurrency)} ({FormatCurrencyDisplayName(targetCurrency)})");

        var exchangeRateDate = GetExchangeRateDate(sourceCurrency, targetCurrency);
        if (!string.IsNullOrWhiteSpace(exchangeRateDate)) stringBuilder.AppendLine($"기준: {exchangeRateDate}");

        var appliedRateText = FormatAppliedRateText(sourceCurrency, targetCurrency);
        if (!string.IsNullOrWhiteSpace(appliedRateText)) stringBuilder.AppendLine($"적용 환율: {appliedRateText}");

        return stringBuilder.ToString().TrimEnd();
    }

    private static string FormatAppliedRateText(ExchangeCurrency sourceCurrency, ExchangeCurrency targetCurrency)
    {
        var appliedRates = new List<string>();
        if (!sourceCurrency.IsKoreanWon) appliedRates.Add(FormatKoreanWonRate(sourceCurrency));
        if (!targetCurrency.IsKoreanWon && !targetCurrency.CurrencyCode.Equals(sourceCurrency.CurrencyCode, StringComparison.OrdinalIgnoreCase)) appliedRates.Add(FormatKoreanWonRate(targetCurrency));
        return string.Join(" / ", appliedRates);
    }

    private static string FormatKoreanWonRate(ExchangeCurrency currency) =>
        $"{FormatCurrencyDisplayName(currency)} {FormatDecimal(currency.CurrencyUnit)} = {FormatDecimal(currency.BasePrice)}원";

    private static string FormatCurrencyAmount(decimal amount, ExchangeCurrency currency) =>
        $"{FormatDisplayAmount(amount)}{currency.CurrencyName}";

    private static string FormatDisplayAmount(decimal amount)
    {
        var roundedAmount = decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
        if (decimal.Abs(roundedAmount) <= KoreanNumberDisplayThreshold) return FormatDecimal(roundedAmount);

        var integerAmount = decimal.Truncate(roundedAmount);
        if (integerAmount < long.MinValue || integerAmount > long.MaxValue) return FormatDecimal(roundedAmount);

        var integerAmountText = KoreanNumber.ToKoreanString((long)integerAmount, KoreanNumberFormat.ArabicChunk);
        var fractionalSuffix = FormatFractionalSuffix(decimal.Abs(roundedAmount - integerAmount));
        return $"{integerAmountText}{fractionalSuffix}";
    }

    private static string FormatFractionalSuffix(decimal fractionalAmount) =>
        fractionalAmount == 0 ? string.Empty : FormatDecimal(fractionalAmount)[1..];

    private static string FormatCurrencyDisplayName(ExchangeCurrency currency) =>
        currency.IsKoreanWon ? "한국 원화 (KRW)" : $"{currency.Country} {currency.CurrencyName} ({currency.CurrencyCode})";

    private static string GetExchangeRateDate(ExchangeCurrency sourceCurrency, ExchangeCurrency targetCurrency)
    {
        var dateText = !sourceCurrency.IsKoreanWon ? sourceCurrency.Date : targetCurrency.Date;
        if (string.IsNullOrWhiteSpace(dateText)) return string.Empty;
        if (DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime)) return dateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        return dateText;
    }

    private static string FormatDecimal(decimal value)
    {
        var roundedValue = decimal.Round(value, 4, MidpointRounding.AwayFromZero);
        if (roundedValue == decimal.Truncate(roundedValue)) return roundedValue.ToString("N0", CultureInfo.InvariantCulture);
        return roundedValue.ToString("N4", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
    }

    private static ExchangeCurrency MapExchangeCurrency(DaumExchangeRatePayload currencyPayload) =>
        new(currencyPayload.CurrencyCode?.Trim() ?? string.Empty, currencyPayload.CurrencyName?.Trim() ?? string.Empty, currencyPayload.Country?.Trim() ?? string.Empty, currencyPayload.Name?.Trim() ?? string.Empty, currencyPayload.CurrencyUnit, currencyPayload.BasePrice, currencyPayload.Date?.Trim() ?? string.Empty, false);

    private static string NormalizeCurrencySearchText(string value)
    {
        var normalizedValue = string.Concat(value.Trim().Where(character => !char.IsWhiteSpace(character))).ToUpperInvariant();
        return normalizedValue.Length > 1 && normalizedValue.EndsWith('화') ? normalizedValue[..^1] : normalizedValue;
    }

    private static string CreateCurrencyNotFoundMessage(string currencyQuery) =>
        $"'{currencyQuery}' 통화를 찾지 못했습니다.\n{CreateUsageMessage()}";

    private static string CreateUsageMessage() =>
        "사용법: !환율 [금액+출발통화] [도착통화]\n금액은 출발 통화에 붙여 쓰며 100달러, 1억4천만5백만달러처럼 입력할 수 있습니다.\n예시: !환율, !환율 달러 엔, !환율 미국 일본, !환율 100엔 달러";

    private static string BuildContentPreview(string content, int maximumLength) =>
        content.Length <= maximumLength ? content : $"{content[..maximumLength]}...";

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private sealed record ExchangeRateRequest(long? Amount, string SourceCurrencyQuery, string TargetCurrencyQuery);

    private sealed record ExchangeRateParseResult(ExchangeRateRequest? Request, string? Message);

    private sealed record ExchangeCurrency(string CurrencyCode, string CurrencyName, string Country, string Name, decimal CurrencyUnit, decimal BasePrice, string Date, bool IsKoreanWon)
    {
        public decimal KoreanWonUnitPrice => BasePrice / CurrencyUnit;
    }

    private sealed class DaumExchangeRateResponsePayload
    {
        [JsonPropertyName("data")]
        public IReadOnlyList<DaumExchangeRatePayload>? Data { get; init; }
    }

    private sealed class DaumExchangeRatePayload
    {
        [JsonPropertyName("date")]
        public string? Date { get; init; }

        [JsonPropertyName("currencyCode")]
        public string? CurrencyCode { get; init; }

        [JsonPropertyName("currencyName")]
        public string? CurrencyName { get; init; }

        [JsonPropertyName("currencyUnit")]
        public decimal CurrencyUnit { get; init; }

        [JsonPropertyName("country")]
        public string? Country { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("basePrice")]
        public decimal BasePrice { get; init; }
    }
}
