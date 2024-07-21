using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;

public sealed class CashMachine
{
    private static CashMachine instance = null;
    private static readonly object padlock = new object();

    public float AccountAmount { get; private set; }

    private List<string> transactionHistory;

    private static readonly HashSet<string> supportedCurrencies = new HashSet<string> { "UAH", "USD", "EUR", "PLN" };
    private readonly Dictionary<string, float> exchangeRates;   // 1 UAH TO {USD, EUR, PLN} = float
    // private string MainCurrency; not nessesary.

    // exhangeratesapi.io config
    private static readonly string apiUrl = "http://api.exchangeratesapi.io/v1/latest";
    private static readonly string apiKey = "c24247f5c52b236875800705b83c81cc";


    // Приватний конструктор для запобігання створенню об'єктів ззовні
    private CashMachine(float accountAmount)
    {
        AccountAmount = accountAmount;
        transactionHistory = new List<string>();
        exchangeRates = new Dictionary<string, float>();
        LogTransaction(0, "UAH");
    }

    // Singleton паттерн для забезпечення існування лише одного екземпляру CashMachine
    public static CashMachine Construct(float accountAmount)
    {
        lock (padlock)
        {
            if (instance == null)
            {
                instance = new CashMachine(accountAmount);
            }
            return instance;
        }
    }

    // Функція логування транзакції
    private void LogTransaction(float amount, string currency, bool isDeposit = true, string originalCurrency = null, float originalAmount = 0)
    {
        string transactionType = isDeposit ? "+" : "-";
        string transactionEntry = $"{DateTime.Now:dd/MM/yyyy hh:mm tt} {transactionType}{amount} {currency}";

        if (!isDeposit && originalCurrency != null)
        {
            transactionEntry += $" (inCur {originalAmount} {originalCurrency})";
        }

        transactionHistory.Add(transactionEntry);
    }

    // Депозит
    public void Deposit(float amount)
    {
        if (amount <= 0)
        {
            Console.WriteLine("Error: amount must be greater than 0");
            return;
        }
        // Оновлюємо баланс
        AccountAmount += amount;
        LogTransaction(amount, "UAH");
        Console.WriteLine($"Deposited {amount}. Current balance: {AccountAmount} UAH");
    }

    // Зняття коштів
    public void Withdraw(float amount, string currency = "UAH", bool displayMessage = true)
    {
        if (amount <= 0)
        {
            Console.WriteLine("Error: amount must be greater than 0");
            return;
        }

        if (amount > AccountAmount)
        {
            Console.WriteLine("Insufficient funds");
            return;
        }
        // оновлюємо баланс
        AccountAmount -= amount;
        LogTransaction(amount, currency, false);

        // Перевірка чи повідомлення було виведено (для запобігання конфлікту)
        if (displayMessage)
        {
            Console.WriteLine($"Withdrawn: {amount} {currency}. Current balance: {AccountAmount} UAH");
        }
    }

    // Отримання курсів валют
    public void GetCurrencies()
    {
        Console.WriteLine("Current Exchange Rates:");

        // тут також мав бути цикл для supportedCurrencies
        PrintCurrencyRate("USD");
        PrintCurrencyRate("EUR");
        PrintCurrencyRate("PLN");
    }

    private void PrintCurrencyRate(string currency)
    {
        // Перевірка чи є курс валюти у exchangeRates
        if (exchangeRates.TryGetValue(currency, out float rate))
        {
            Console.WriteLine($"{currency}: {rate}");
        }
        else
        {
            Console.WriteLine($"{currency}: Rate not available");
        }
    }

    // Синхронізація курсів валют
    public void SyncCurrencies(string staffPassword)
    {
        if (staffPassword != "NotMyCodeVision")
        {
            Console.WriteLine("Invalid password.");
            return;
        }
        try
        {
            using (HttpClient client = new HttpClient())
            {
                // Робимо запит до exhangeratesapi.io
                string requestUri = $"{apiUrl}?access_key={apiKey}";

                // Отримуємо відвовідь використовуючи синхронний метод GetAsync()
                HttpResponseMessage response = client.GetAsync(requestUri).Result;

                // Перевірка на вдалу відповідь (code 200)
                if (response.IsSuccessStatusCode)
                {
                    // Валідуємо відповідь
                    string responseBody = response.Content.ReadAsStringAsync().Result;
                    var jsonResponse = JsonDocument.Parse(responseBody);

                    // Парсимо курси валют
                    var rates = jsonResponse.RootElement.GetProperty("rates")
                        .EnumerateObject()
                        .ToDictionary(prop => prop.Name, prop => prop.Value.GetSingle());

                    if (rates != null)
                    {
                        //  Використання baseCurrency не обов'язкове
                        float baseRate = rates["UAH"];

                        // Тут мав бути цикл для запису курсів supported currencies у exchangeRates
                        UpdateExchangeRate(rates, "USD", baseRate);
                        UpdateExchangeRate(rates, "EUR", baseRate);
                        UpdateExchangeRate(rates, "PLN", baseRate);

                        Console.WriteLine("Exchange rates updated successfully.");

                        GetCurrencies();
                    }
                    else
                    {
                        Console.WriteLine("Failed to retrieve exchange rates.");
                    }
                }
                else
                {
                    Console.WriteLine($"Error: {response.ReasonPhrase}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while syncing currencies: {ex.Message}");
        }
    }

    private void UpdateExchangeRate(Dictionary<string, float> rates, string currency, float baseRate)
    {
        if (rates.ContainsKey(currency))
        {
            exchangeRates[currency] = baseRate / rates[currency];
        }
    }

    // Перевірка підтримки валюти
    public bool CheckCurrency(string currencyCode)
    {
        return supportedCurrencies.Contains(currencyCode);
    }

    // Зняття коштів у зазначеній валюті
    public void WithdrawInCurrency(string currencyCode, float amount)
    {
        if (!CheckCurrency(currencyCode))
        {
            Console.WriteLine($"Currency {currencyCode} not supported");
            return;
        }

        if (exchangeRates.TryGetValue(currencyCode, out float rate))
        {
            float amountInUah = amount * rate;
            if (amountInUah <= AccountAmount)
            {
                AccountAmount -= amountInUah;
                LogTransaction(amountInUah, "UAH", false, currencyCode, amount);
                Console.WriteLine($"Withdrawn: {amount} {currencyCode} (equivalent to {amountInUah} UAH). Current balance: {AccountAmount} UAH");
            }
            else
            {
                Console.WriteLine("Insufficient funds");
            }
        }
        else
        {
            Console.WriteLine("Exchange rate not found for the specified currency");
        }
    }

    // Перевірка балансу
    public float Balance()
    {
        return AccountAmount;
    }

    // Отримання історії транзакцій
    public string History()
    {
        return string.Join(" | ", transactionHistory);
    }
}

class Program
{
    static void Main()
    {
        CashMachine cashMachine = CashMachine.Construct(0f);
        cashMachine.SyncCurrencies("NotMyCodeVision");
        cashMachine.Deposit(1230f);
        cashMachine.Withdraw(100f);
        Console.WriteLine($"Current balance: {cashMachine.Balance()} UAH");
        cashMachine.Withdraw(1000f);
        cashMachine.WithdrawInCurrency("USD", 1f);
        Console.WriteLine("Transaction History: " + cashMachine.History());
    }
}