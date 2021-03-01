using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos.Table;
using static UnoCash.Core.ExpenseStorage;

namespace UnoCash.Core
{
    public static class ExpenseReader
    {
        public static Task<IEnumerable<Expense>> GetAsync(string account, string upn, Guid id) =>
            GetAllAsync(account, upn).WhereAsync(expense => expense.Id == id);

        public static Task<IEnumerable<Expense>> GetAllAsync(string account, string upn) =>
            AzureTableStorage.GetAllAsync(nameof(Expense), GetPartitionKey(account, upn))
                             .SelectAsync(ToExpense);

        static Expense ToExpense(this DynamicTableEntity expense) =>
            new Expense
            {
                Id = Guid.Parse(expense.RowKey),
                Account = expense.Properties[nameof(Expense.Account)].StringValue,
                Payee = expense.Properties[nameof(Expense.Payee)].StringValue,
                Description = expense.Properties[nameof(Expense.Description)].StringValue,
                Status = expense.Properties[nameof(Expense.Status)].StringValue,
                Type = expense.Properties[nameof(Expense.Type)].StringValue,
                Date = expense.Properties[nameof(Expense.Date)].DateTime ?? throw new Exception(),
                Amount = expense.Properties[nameof(Expense.Amount)].Int64Value / 100m ?? throw new Exception(),
                Tags = expense[nameof(Expense.Tags)].StringValue
            };
    }
}