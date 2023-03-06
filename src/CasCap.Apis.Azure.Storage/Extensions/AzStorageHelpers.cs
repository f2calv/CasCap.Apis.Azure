﻿using System.Globalization;
using System.Text.RegularExpressions;
namespace CasCap.Common.Extensions;

public static class AzStorageHelpers
{
    static readonly ILogger _logger = ApplicationLogging.CreateLogger(nameof(AzStorageHelpers));

    public static async Task<bool> ExistsAsync(this TableServiceClient client, string tableName)
    {
        await foreach (var tbl in client.QueryAsync(t => t.Name == tableName))
            return true;
        return false;
    }

    //static readonly Regex DisallowedCharsInTableKeys = new(@"[\\\\#%+/?\u0000-\u001F\u007F-\u009F]", RegexOptions.Compiled);
    static readonly Regex DisallowedCharsInTableKeys = new(@"[\\\\#%/?\^\u0000-\u001F\u007F-\u009F]", RegexOptions.Compiled);
    //static readonly Regex DisallowedCharsInTableKeys = new("[#]", RegexOptions.Compiled);
    //https://stackoverflow.com/questions/11514707/azure-table-storage-rowkey-restricted-character-patterns
    public static bool IsKeyValid(this string tableKey)
    {
        return !DisallowedCharsInTableKeys.IsMatch(tableKey);
        //string sanitizedKey = DisallowedCharsInTableKeys.Replace(tableKey, disallowedCharReplacement);
    }

    public static DateTime GetDateFromFileName(this string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        //someDirectory/2016-05-17-some-suffix.log.gz -> 2016-05-17
        var strDt = fileName.Substring(0, 10);
        if (DateTime.TryParse(strDt, out var date))
            return date;
        else
            throw new ArgumentException("unable to parse {path} to retrieve date", path);
    }

    /// <summary>
    /// Used primarily for PartitionKey
    /// </summary>
    /// <param name="thisDate"></param>
    /// <returns></returns>
    [Obsolete("replaced with GetPartitionKeyNEW")]
    public static string GetPartitionKeyOLD(this DateTime thisDate)
    {
        return (DateTime.MaxValue.Ticks - new DateTime(thisDate.Year, thisDate.Month, thisDate.Day).Ticks).ToString("d19");
    }

    public static readonly DateTime newMaxDate = new(2050, 1, 1);

    const string yyMMdd = "yyMMdd";

    public static string GetPartitionKeyNEW(this DateTime thisDate)
    {
        //partitonKey limitation always assumes ticks are from 2000 onwards - which is something we can live with!
        return thisDate.ToString(yyMMdd);
    }
    [Obsolete("half-replaced with GetPartitionKeyDateNEW")]
    public static DateTime GetPartitionKeyDateTimeOLD(this string thisString)
    {
        long.TryParse(thisString, out long tickCount);
        return new DateTime(DateTime.MaxValue.Ticks - tickCount, DateTimeKind.Utc);
    }
    public static DateTime GetPartitionKeyDateNEW(this string partitionKey)
    {
        return DateTime.ParseExact(partitionKey, yyMMdd, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Used primarily for RowKey
    /// </summary>
    /// <param name="thisDate"></param>
    /// <returns></returns>
    [Obsolete("replaced with GetRowKeyNEW")]
    public static string GetRowKeyOLD(this DateTime thisDate)
    {
        return (DateTime.MaxValue.Ticks - thisDate.Ticks).ToString("d19");
    }

    //public static string GetAzureRowKey(this TimeSpan ts)
    //{
    //    var str = (DateTime.MaxValue.Ticks - ts.Ticks).ToString("d19");
    //    return str[^12];
    //}

    const long ticksInADay = 863999999999;

    public static string GetRowKeyNEW(this DateTime thisDate, bool lexical = true)
    {
        var dayTicks = new DateTime(thisDate.Year, thisDate.Month, thisDate.Day).Ticks;
        var todayTicks = thisDate.Ticks - dayTicks;
        if (lexical) todayTicks = ticksInADay - todayTicks;
        var output = todayTicks.ToString("d12");
        return output;
    }

    public static DateTime GetRowKeyDateTimeOLD(this string thisRowKey)
    {
        if (!long.TryParse(thisRowKey, out long _tickCount))
            throw new ArgumentException("unable to parse {rowKey} to retrieve tick count", thisRowKey);
        return new DateTime(DateTime.MaxValue.Ticks - _tickCount, DateTimeKind.Utc);
    }

    public static DateTime GetRowKeyDateTimeNEW(this string thisRowKey, string PartitionKey, bool lexical = true)
    {
        var dt = PartitionKey.GetPartitionKeyDateNEW();
        if (!long.TryParse(thisRowKey, out long _tickCount))
            throw new ArgumentException("unable to parse {rowKey} to retrieve tick count", thisRowKey);
        var ticks = lexical ? ticksInADay - _tickCount : _tickCount;
        return DateTime.SpecifyKind(dt.AddTicks(ticks), DateTimeKind.Utc);
    }
}
