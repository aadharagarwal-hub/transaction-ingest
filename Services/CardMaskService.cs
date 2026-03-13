namespace TransactionIngest.Services;

public class CardMaskService
{
    public string GetLast4(string cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber) || cardNumber.Length < 4)
        {
            return "0000";
        }

        return cardNumber[^4..];
    }
}