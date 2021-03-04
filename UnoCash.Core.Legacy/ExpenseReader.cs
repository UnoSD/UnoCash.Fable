using System;
using UnoCash.Dto;
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
            (
                id         : Guid.Parse(expense.RowKey),
                account    : expense.Properties[nameof(Expense.Account)].StringValue,
                payee      : expense.Properties[nameof(Expense.Payee)].StringValue,
                description: expense.Properties[nameof(Expense.Description)].StringValue,
                status     : expense.Properties[nameof(Expense.Status)].StringValue,
                type       : expense.Properties[nameof(Expense.Type)].StringValue,
                date       : expense.Properties[nameof(Expense.Date)].DateTime ?? throw new Exception(),
                amount     : expense.Properties[nameof(Expense.Amount)].Int64Value / 100m ?? throw new Exception(),
                tags       : expense[nameof(Expense.Tags)].StringValue
            );
    }
}