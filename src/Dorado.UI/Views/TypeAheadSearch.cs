using System;
using System.Collections.Generic;

namespace Dorado.UI.Views;

public static class TypeAheadSearch
{
    /// <summary>Index of the first item whose key starts with the prefix, or -1.</summary>
    public static int FindIndex<T>(IEnumerable<T> items, string prefix, Func<T, string> key)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return -1;
        }

        var index = 0;
        foreach (var item in items)
        {
            var value = key(item);
            if (!string.IsNullOrEmpty(value) && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }

            index++;
        }

        return -1;
    }
}
