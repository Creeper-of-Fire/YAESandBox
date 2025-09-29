using System.Globalization;
using NLua;

#pragma warning disable CS8974 // 将方法组转换为非委托类型

// ReSharper disable InconsistentNaming
#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
namespace YAESandBox.Plugin.Rune.LuaScript.LuaRunner.Bridge;

/// <summary>
/// 日期时间符文的工厂类，暴露给 Lua 作为 'datetime'。
/// </summary>
public class LuaDateTimeBridge : ILuaBridge
{
    private static LuaDateTimeObject utcnow(LuaLogBridge logger) => new(DateTimeOffset.UtcNow, logger);
    private static LuaDateTimeObject now(LuaLogBridge logger) => new(DateTimeOffset.Now, logger);

    private static LuaDateTimeObject? parse(string dateString, LuaLogBridge logger, string? format = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dateString))
                return null;

            DateTimeOffset result;

            if (string.IsNullOrEmpty(format))
            {
                // 支持更多格式
                if (DateTimeOffset.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                    return new LuaDateTimeObject(result, logger);

                // 尝试常见格式
                string[] formats =
                {
                    "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy",
                    "yyyyMMdd", "ddMMMyyyy", "MMM dd, yyyy"
                };

                if (DateTimeOffset.TryParseExact(
                        dateString,
                        formats,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out result))
                {
                    return new LuaDateTimeObject(result, logger);
                }

                return null;
            }

            format = format switch
            {
                // 处理特殊格式标记
                "r" => "ddd, dd MMM yyyy HH':'mm':'ss 'GMT'",
                "o" => "yyyy-MM-dd'T'HH:mm:ss.fffffffK",
                _ => format
            };

            return DateTimeOffset.TryParseExact(
                dateString,
                format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result)
                ? new LuaDateTimeObject(result, logger)
                : null;
        }
        catch (Exception ex)
        {
            logger.error($"datetime.parse 失败: {ex.Message}");
            return null;
        }
    }

    /// <inheritdoc />
    public string BridgeName => "datetime";

    /// <inheritdoc />
    public void Register(Lua luaState, LuaLogBridge logger)
    {
        // 注册 datetime.*
        luaState.NewTable(this.BridgeName);
        var datetimeTable = (LuaTable)luaState[this.BridgeName];
        datetimeTable["utcnow"] = () => utcnow(logger);
        datetimeTable["now"] = () => now(logger);
        datetimeTable["parse"] = (string dateString, string? format = null) => parse(dateString, logger, format);
    }
}

/// <summary>
/// 一个安全的 DateTimeOffset 包装器，暴露给 Lua。
/// 这个对象是不可变的；所有 'add' 方法都返回一个新对象。
/// </summary>
public class LuaDateTimeObject(DateTimeOffset dateTimeOffset, LuaLogBridge logger)
{
    private DateTimeOffset Dto { get; } = dateTimeOffset;
    private LuaLogBridge Logger { get; } = logger;

    // --- 属性 ---
    public int year => this.Dto.Year;
    public int month => this.Dto.Month;
    public int day => this.Dto.Day;
    public int hour => this.Dto.Hour;
    public int minute => this.Dto.Minute;
    public int second => this.Dto.Second;
    public int millisecond => this.Dto.Millisecond;
    public int day_of_week => (int)this.Dto.DayOfWeek; // 0=Sunday, ..., 6=Saturday
    public int day_of_year => this.Dto.DayOfYear;

    // --- 操作方法 (返回新对象) ---
    public LuaDateTimeObject add_years(object years)
    {
        try
        {
            int yearValue = Convert.ToInt32(years);
            return new LuaDateTimeObject(this.Dto.AddYears(yearValue), this.Logger);
        }
        catch
        {
            // 尝试转换 double
            try
            {
                double yearsDouble = Convert.ToDouble(years);
                int yearsInt = (int)Math.Floor(yearsDouble);
                int months = (int)Math.Round((yearsDouble - yearsInt) * 12);

                var newDate = this.Dto.AddYears(yearsInt).AddMonths(months);
                return new LuaDateTimeObject(newDate, this.Logger);
            }
            catch (Exception ex)
            {
                this.Logger.error($"add_years 失败: {ex.Message}");
                return this; // 返回原始对象
            }
        }
    }

    public LuaDateTimeObject add_months(int months) => new(this.Dto.AddMonths(months), this.Logger);
    public LuaDateTimeObject add_days(double days) => new(this.Dto.AddDays(days), this.Logger);
    public LuaDateTimeObject add_hours(double hours) => new(this.Dto.AddHours(hours), this.Logger);
    public LuaDateTimeObject add_minutes(double minutes) => new(this.Dto.AddMinutes(minutes), this.Logger);
    public LuaDateTimeObject add_seconds(double seconds) => new(this.Dto.AddSeconds(seconds), this.Logger);

    // --- 格式化 ---
    /// <summary>
    /// 根据 .NET 格式化字符串来格式化日期。
    /// </summary>
    public string format(string formatString)
    {
        try
        {
            // 处理特殊格式标记
            switch (formatString)
            {
                case "r":
                    return this.Dto.ToString("r", CultureInfo.InvariantCulture);
                case "o":
                    return this.Dto.ToString("o");
                default:
                    return this.Dto.ToString(formatString, CultureInfo.InvariantCulture);
            }
        }
        catch (Exception ex)
        {
            this.Logger.error($"datetime.format 失败: {ex.Message}");
            return this.Dto.ToString("o"); // ISO 8601 格式
        }
    }

    /// <summary>
    /// 默认输出为 ISO 8601 格式，方便调试。
    /// </summary>
    public override string ToString() => this.Dto.ToString("o"); // Round-trip format
}