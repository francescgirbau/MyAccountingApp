using System;
using System.Collections.Generic;
using System.Linq;

namespace MyAccountingApp.Application.Services;

public static class TransactionCategoryFilter
{
    public static IEnumerable<T> FilterByCategories<T>(
        IEnumerable<T> transactions,
        IReadOnlySet<string> categories,
        Func<T, string> categorySelector)
    {
        if (categories.Count == 0)
        {
            return transactions;
        }

        return transactions.Where(t => categories.Contains(categorySelector(t)));
    }
}