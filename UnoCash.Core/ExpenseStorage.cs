namespace UnoCash.Core
{
    public static class ExpenseStorage
    {
        public static string GetPartitionKey(string account, string upn) =>
            AzureTableStorage.FormatPartitionKey(upn) + account;
    }
}