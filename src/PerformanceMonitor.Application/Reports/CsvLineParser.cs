namespace PerformanceMonitor.Application.Reports;

/// <summary>解析一行 CSV（RFC 4180：逗號分隔、雙引號包住含特殊字元的欄位、內部雙引號重複一次）。</summary>
public static class CsvLineParser
{
    /// <summary>UTF-8 BOM（第一行開頭）。</summary>
    public const char Bom = '﻿';

    /// <summary>拆欄位；引號不成對等格式錯誤時回傳 null。</summary>
    public static string[]? Split(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        int i = 0;
        while (true)
        {
            if (i < line.Length && line[i] == '"')
            {
                i++;
                while (true)
                {
                    if (i >= line.Length)
                    {
                        return null; // 引號沒有結束（CSV 寫入時已把換行包在引號內，這裡一行一筆，視為損毀）
                    }

                    if (line[i] == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i += 2;
                            continue;
                        }

                        i++;
                        break;
                    }

                    current.Append(line[i++]);
                }

                if (i < line.Length && line[i] != ',')
                {
                    return null; // 結束引號後還有其他字元
                }
            }
            else
            {
                while (i < line.Length && line[i] != ',')
                {
                    if (line[i] == '"')
                    {
                        return null;
                    }

                    current.Append(line[i++]);
                }
            }

            fields.Add(current.ToString());
            current.Clear();
            if (i >= line.Length)
            {
                return [.. fields];
            }

            i++; // 逗號
        }
    }
}
